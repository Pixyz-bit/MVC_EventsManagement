using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.Models;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Admin;
using Microsoft.Extensions.Configuration;
using MVC_QCUEventsManagement.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;

namespace MVC_QCUEventsManagement.Tests;

[Collection("Shared SQL rollback fixtures")]
public sealed class AuthenticationTests : IAsyncLifetime
{
    private readonly string _password = Guid.NewGuid().ToString("N");
    private readonly string _studentId = $"T-{Guid.NewGuid():N}";
    private readonly string _adminEmail = $"admin-{Guid.NewGuid():N}@example.test";
    private readonly string _studentEmail = $"student-{Guid.NewGuid():N}@example.test";
    private SqlConnection _connection = null!;
    private SqlTransaction _transaction = null!;
    private WebApplicationFactory<Program> _factory = null!;
    private DbContextOptions<ApplicationDbContext> _options = null!;
    private readonly string _bannerRoot = Path.Combine(Path.GetTempPath(), $"qcu-event-test-{Guid.NewGuid():N}");

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("MVC_EVENTS_TEST_CONNECTION")
            ?? "Server=localhost;Database=MVC_EventsManagement;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";
        if (new SqlConnectionStringBuilder(connectionString).InitialCatalog != "MVC_EventsManagement")
        {
            throw new InvalidOperationException("Authentication tests must target MVC_EventsManagement only.");
        }
        _connection = new SqlConnection(connectionString);
        await _connection.OpenAsync();
        _transaction = (SqlTransaction)await _connection.BeginTransactionAsync();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection).Options;
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["EventImages:RootPath"] = _bannerRoot }));
            web.ConfigureServices(services => services.AddScoped(_ => CreateContext()));
        });

        await using var db = CreateContext();
        var student = NewAccount(_studentEmail, "Student");
        student.Student = new Student
        {
            StudentId = _studentId, FirstName = "Test", LastName = "Student", Gender = "Not specified",
            CampusBranch = "Main", Department = "Computing", Program = "Information Technology"
        };
        db.Users.AddRange(student, NewAccount(_adminEmail, "Admin"));
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        if (_transaction is not null) await _transaction.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        if (Directory.Exists(_bannerRoot))
        {
            foreach (var file in Directory.GetFiles(_bannerRoot)) File.Delete(file);
            Directory.Delete(_bannerRoot);
        }
    }

    [Fact]
    public async Task Root_redirects_to_login_and_portal_requires_authentication()
    {
        using var client = NewClient();
        using var root = await client.GetAsync("/");
        Assert.Equal("/login", RedirectPath(root));
        using var portal = await client.GetAsync("/admin");
        Assert.Equal("/login?ReturnUrl=%2Fadmin", RedirectPath(portal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Student_can_sign_in_by_id_or_email_and_cannot_open_admin_portal(bool useId)
    {
        using var client = NewClient();
        using var login = await SignInAsync(client, useId ? $" {_studentId} " : _studentEmail, _password);
        Assert.Equal("/student", RedirectPath(login));
        using var portal = await client.GetAsync("/student");
        Assert.Equal(HttpStatusCode.OK, portal.StatusCode);
        var html = await portal.Content.ReadAsStringAsync();
        Assert.Contains(_studentEmail, html);

        using var denied = await client.GetAsync("/admin/events");
        Assert.StartsWith("/access-denied?", RedirectPath(denied));
        using var denialPage = await client.GetAsync(denied.Headers.Location);
        Assert.Equal(HttpStatusCode.Forbidden, denialPage.StatusCode);
    }

    [Theory]
    [InlineData("//example.test/", "/admin")]
    [InlineData("/admin", "/admin")]
    [InlineData("/Home/Privacy", "/Home/Privacy")]
    public async Task Administrator_is_routed_to_local_urls_and_can_log_out(string returnUrl, string target)
    {
        using var client = NewClient();
        using var login = await SignInAsync(client, _adminEmail, _password, returnUrl);
        Assert.Equal(target, RedirectPath(login));
        using var portal = await client.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.OK, portal.StatusCode);
        using var eventList = await client.GetAsync("/admin/events?status=Closed&q=no-matching-event");
        Assert.Equal(HttpStatusCode.OK, eventList.StatusCode);
        Assert.Contains("Event Directory", await eventList.Content.ReadAsStringAsync());
        using var wrongPortal = await client.GetAsync("/student");
        Assert.StartsWith("/access-denied?", RedirectPath(wrongPortal));

        using var rejectedLogout = await client.PostAsync("/logout", Form());
        Assert.Equal(HttpStatusCode.BadRequest, rejectedLogout.StatusCode);
        using var logout = await client.PostAsync("/logout", Form(("__RequestVerificationToken", await TokenAsync(portal))));
        Assert.Equal("/login", RedirectPath(logout));
        using var signedOut = await client.GetAsync("/admin");
        Assert.StartsWith("/login?", RedirectPath(signedOut));
    }

    [Fact]
    public async Task Event_directory_renders_filtered_results_and_a_recoverable_empty_state()
    {
        var title = $"Campus Research Forum {Guid.NewGuid():N}";
        var now = new EventClock(TimeProvider.System).Now;
        await using (var db = CreateContext())
        {
            db.Events.Add(new Event
            {
                Title = title, VenueLocation = "Main Campus Hall", MaxCapacity = 30,
                CreatedByUserId = await db.Users.Where(user => user.Email == _adminEmail).Select(user => user.UserId).SingleAsync(),
                EventStart = now.AddDays(3), EventEnd = now.AddDays(3).AddHours(2),
                RegStart = DateOnly.FromDateTime(now.AddDays(-1)), RegEnd = DateOnly.FromDateTime(now.AddDays(1))
            });
            await db.SaveChangesAsync();
        }

        using var client = NewClient();
        using var login = await SignInAsync(client, _adminEmail, _password);
        var query = Uri.EscapeDataString(title);
        var results = await client.GetStringAsync($"/admin/events?q={query}&status=Open");
        Assert.Contains(title, results);
        Assert.Contains("Main Campus Hall", results);
        Assert.Contains("0 of 30 seats registered", results);

        var empty = await client.GetStringAsync($"/admin/events?q={query}&status=Closed");
        Assert.Contains("No matching events", empty);
        Assert.Contains("Clear filters", empty);
        Assert.DoesNotContain("0 of 30 seats registered", empty);
    }

    [Fact]
    public async Task Login_requires_antiforgery_token_and_bad_credentials_do_not_create_a_session()
    {
        using var client = NewClient();
        using var noToken = await client.PostAsync("/login", Form(("Identifier", _adminEmail), ("Password", _password)));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);

        var wrongPassword = Guid.NewGuid().ToString("N");
        using var wrong = await SignInAsync(client, _adminEmail, wrongPassword);
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        var html = await wrong.Content.ReadAsStringAsync();
        Assert.Contains("The student ID, email, or password is incorrect.", html);
        Assert.DoesNotContain(wrongPassword, html);
        using var portal = await client.GetAsync("/admin");
        Assert.StartsWith("/login?", RedirectPath(portal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Account_changes_revoke_an_existing_cookie(bool deactivate)
    {
        using var client = NewClient();
        using var login = await SignInAsync(client, _adminEmail, _password);
        Assert.Equal("/admin", RedirectPath(login));
        await using (var db = CreateContext())
        {
            var admin = await db.Users.SingleAsync(user => user.Email == _adminEmail);
            if (deactivate) admin.IsActive = false;
            else admin.Role = "Student";
            await db.SaveChangesAsync();
        }
        using var portal = await client.GetAsync("/admin");
        Assert.StartsWith("/login?", RedirectPath(portal));

        if (deactivate)
        {
            using var retry = await SignInAsync(client, _adminEmail, _password);
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
            Assert.Contains("The student ID, email, or password is incorrect.", await retry.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Event_creation_enforces_role_antiforgery_and_invalid_input_without_writes()
    {
        using var anonymous = NewClient();
        using var gated = await anonymous.GetAsync("/admin/events/create");
        Assert.StartsWith("/login?", RedirectPath(gated));
        using var student = NewClient();
        using var studentLogin = await SignInAsync(student, _studentEmail, _password);
        using var forbidden = await student.PostAsync("/admin/events/create", Form());
        Assert.StartsWith("/access-denied?", RedirectPath(forbidden));

        using var admin = NewClient();
        using var login = await SignInAsync(admin, _adminEmail, _password);
        using var rejected = await admin.PostAsync("/admin/events/create", Form(("Title", "Forged event")));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var page = await admin.GetAsync("/admin/events/create");
        Assert.Contains("event-editor-footer", await page.Content.ReadAsStringAsync());
        await using var db = CreateContext();
        var before = await db.Events.CountAsync();
        using var invalid = await admin.PostAsync("/admin/events/create", Form(
            ("__RequestVerificationToken", await TokenAsync(page)), ("Title", "Invalid event"),
            ("VenueLocation", "Hall A"), ("MaxCapacity", "0")));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("Please check the event details", await invalid.Content.ReadAsStringAsync());
        Assert.Equal(before, await db.Events.CountAsync());
    }

    [Fact]
    public async Task Event_creation_persists_authenticated_creator_audience_sponsors_and_reencoded_banner()
    {
        using var client = NewClient();
        using var login = await SignInAsync(client, _adminEmail, _password);
        using var page = await client.GetAsync("/admin/events/create");
        var day = DateOnly.FromDateTime(new EventClock(TimeProvider.System).Now.AddDays(10));
        var title = $"QCU Research Forum {Guid.NewGuid():N}";
        using var form = new MultipartFormDataContent();
        foreach (var (name, value) in new (string, string)[]
        {
            ("__RequestVerificationToken", await TokenAsync(page)), ("Title", title), ("VenueLocation", " Research Hall "),
            ("MaxCapacity", "75"), ("EventDate", day.ToString("yyyy-MM-dd")), ("StartTime", "09:00"), ("EndTime", "12:00"),
            ("RegStart", day.AddDays(-1).ToString("yyyy-MM-dd")), ("RegEnd", day.AddDays(-1).ToString("yyyy-MM-dd")),
            ("TargetBranch", "San Bartolome"), ("TargetDepartment", "College of Computer Studies"),
            ("Programs", "BSIT"), ("Programs", "BSCS"), ("TargetYearLevel", "2"),
            ("Sponsors[0]", " QCU Alumni Association "), ("Sponsors[1]", "DOST-NCR"), ("Confirm", "true"),
            ("CreatedByUserId", "-1"), ("CurrentRegistrations", "74"), ("Status", "Cancelled")
        }) form.Add(new StringContent(value), name);
        using var bitmap = new SkiaSharp.SKBitmap(2, 2);
        bitmap.Erase(SkiaSharp.SKColors.Blue);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        form.Add(new ByteArrayContent(encoded.ToArray()), "Banner", "../../banner.png");
        using var saved = await client.PostAsync("/admin/events/create", form);
        Assert.Equal("/admin/events", RedirectPath(saved));
        await using var db = CreateContext();
        var ev = await db.Events.AsNoTracking().Include(e => e.Sponsors).SingleAsync(e => e.Title == title);
        Assert.Equal(await db.Users.Where(u => u.Email == _adminEmail).Select(u => u.UserId).SingleAsync(), ev.CreatedByUserId);
        Assert.Equal(0, ev.CurrentRegistrations);
        Assert.Equal("Upcoming", ev.Status);
        Assert.Equal(day.AddDays(-1), ev.RegStart);
        Assert.Equal(day.AddDays(-1), ev.RegEnd);
        Assert.Equal("Research Hall", ev.VenueLocation);
        Assert.Equal("BSIT,BSCS", ev.TargetProgram);
        Assert.Equal(2, ev.TargetYearLevel);
        Assert.Equal(["QCU Alumni Association", "DOST-NCR"], ev.Sponsors.OrderBy(s => s.SponsorEntryId).Select(s => s.SponsorName));
        Assert.Matches("^[a-f0-9]{32}\\.png$", ev.EventPhotoPath!);
        Assert.True(File.Exists(Path.Combine(_bannerRoot, ev.EventPhotoPath!)));
        using var banner = await client.GetAsync($"/admin/events/{ev.EventId}/banner");
        Assert.Equal("image/png", banner.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", banner.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains(title, await client.GetStringAsync("/admin/events"));
    }

    [Theory]
    [InlineData("same-day")]
    [InlineData("reverse")]
    [InlineData("registration-order")]
    [InlineData("audience")]
    [InlineData("sponsors")]
    [InlineData("sql-range")]
    public void Event_creation_rejects_invalid_schedule_associations_and_sql_dates(string scenario)
    {
        var input = new CreateEventInput
        {
            Title = "Campus Research Forum", VenueLocation = "Hall A", MaxCapacity = 20,
            EventDate = new DateOnly(2030, 1, 10), StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(12, 0),
            RegStart = new DateOnly(2030, 1, 1), RegEnd = new DateOnly(2030, 1, 9), Confirm = true
        };
        if (scenario == "same-day") input.RegEnd = new DateOnly(2030, 1, 10);
        if (scenario == "reverse") input.EndTime = new TimeOnly(8, 0);
        if (scenario == "registration-order") input.RegStart = new DateOnly(2030, 1, 10);
        if (scenario == "audience") { input.TargetDepartment = "College of Education"; input.Programs = ["BSIT"]; }
        if (scenario == "sponsors") input.Sponsors = [new string('x', 151)];
        if (scenario == "sql-range") input.RegStart = new DateOnly(1700, 1, 1);
        var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        Assert.False(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(input,
            new System.ComponentModel.DataAnnotations.ValidationContext(input), errors, true));
        Assert.NotEmpty(errors);
    }

    [Fact]
    public async Task Invalid_banner_is_rejected_without_creating_an_event_or_file()
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<EventBannerStore>();
        await using var bytes = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<script>alert(1)</script>"));
        var file = new Microsoft.AspNetCore.Http.FormFile(bytes, 0, bytes.Length, "Banner", "banner.png");
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(file, CancellationToken.None));
        Assert.False(Directory.Exists(_bannerRoot));
    }

    [Fact]
    public async Task Removed_preview_routes_return_not_found_and_real_pages_require_login()
    {
        using var client = NewClient();
        foreach (var path in new[] { "/preview/admin/events", "/preview/admin/events/create" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using var protectedPage = await client.GetAsync("/admin/events/create");
        Assert.StartsWith("/login?", RedirectPath(protectedPage));
    }

    [Fact]
    public async Task Event_details_edit_and_cancellation_are_authorized_atomic_and_concurrency_checked()
    {
        var day = DateOnly.FromDateTime(new EventClock(TimeProvider.System).Now.AddDays(10));
        int id; string version;
        await using (var db = CreateContext())
        {
            var ev = new Event { Title = "Details workflow fixture", VenueLocation = "Research Hall", MaxCapacity = 20,
                CurrentRegistrations = 2, CreatedByUserId = await db.Users.Where(u => u.Email == _adminEmail).Select(u => u.UserId).SingleAsync(),
                EventStart = day.ToDateTime(new TimeOnly(9, 0)), EventEnd = day.ToDateTime(new TimeOnly(12, 0)),
                RegStart = day.AddDays(-5), RegEnd = day.AddDays(-1), Sponsors = [new Sponsor { SponsorName = "Original partner", CreatedAt = new EventClock(TimeProvider.System).Now }] };
            db.Events.Add(ev); await db.SaveChangesAsync(); id = ev.EventId; version = Convert.ToBase64String(ev.RowVersion);
        }
        using var client = NewClient();
        using var anonymous = await client.GetAsync($"/admin/events/{id}");
        Assert.StartsWith("/login?", RedirectPath(anonymous));
        using var login = await SignInAsync(client, _adminEmail, _password);
        using var missing = await client.GetAsync("/admin/events/2147483647");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var page = await client.GetAsync($"/admin/events/{id}?edit=true");
        var csrf = await TokenAsync(page);
        FormUrlEncodedContent EditForm(int capacity, string title = "Updated research forum") => Form(
            ("__RequestVerificationToken", csrf), ("version", version), ("Input.Title", title), ("Input.VenueLocation", "Main Auditorium"),
            ("Input.MaxCapacity", capacity.ToString()), ("Input.EventDate", day.ToString("yyyy-MM-dd")),
            ("Input.StartTime", "10:00"), ("Input.EndTime", "13:00"), ("Input.RegStart", day.AddDays(-4).ToString("yyyy-MM-dd")),
            ("Input.RegEnd", day.AddDays(-1).ToString("yyyy-MM-dd")), ("Input.Confirm", "true"), ("Input.Sponsors[0]", "New partner"));
        using var noToken = await client.PostAsync($"/admin/events/{id}/edit", Form());
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        using var tooSmall = await client.PostAsync($"/admin/events/{id}/edit", EditForm(1));
        Assert.Contains("Capacity cannot be lower", await tooSmall.Content.ReadAsStringAsync());
        await using (var db = CreateContext()) Assert.Equal("Original partner", await db.Sponsors.Where(s => s.EventId == id).Select(s => s.SponsorName).SingleAsync());
        using var saved = await client.PostAsync($"/admin/events/{id}/edit", EditForm(30));
        Assert.Equal($"/admin/events/{id}", RedirectPath(saved));
        using var stale = await client.PostAsync($"/admin/events/{id}/edit", EditForm(40, "Stale overwrite"));
        Assert.Contains("changed while you were editing", await stale.Content.ReadAsStringAsync());
        await using (var db = CreateContext())
        {
            var ev = await db.Events.AsNoTracking().SingleAsync(e => e.EventId == id);
            Assert.Equal("Updated research forum", ev.Title); Assert.Equal(30, ev.MaxCapacity); Assert.Equal(2, ev.CurrentRegistrations);
            Assert.Equal("New partner", await db.Sponsors.Where(s => s.EventId == id).Select(s => s.SponsorName).SingleAsync());
            version = Convert.ToBase64String(ev.RowVersion);
        }
        using var confirmationMissing = await client.PostAsync($"/admin/events/{id}/cancel", Form(("__RequestVerificationToken", csrf), ("version", version), ("reason", "Venue unavailable")));
        Assert.Contains("Confirm that you want to cancel", await confirmationMissing.Content.ReadAsStringAsync());
        using var cancelled = await client.PostAsync($"/admin/events/{id}/cancel", Form(("__RequestVerificationToken", csrf), ("version", version), ("reason", " Venue unavailable "), ("confirmed", "true")));
        Assert.Equal($"/admin/events/{id}", RedirectPath(cancelled));
        using var repeat = await client.PostAsync($"/admin/events/{id}/cancel", Form(("__RequestVerificationToken", csrf), ("version", version), ("reason", "Overwrite"), ("confirmed", "true")));
        Assert.Contains("can no longer be cancelled", await repeat.Content.ReadAsStringAsync());
        await using (var db = CreateContext())
        {
            var ev = await db.Events.AsNoTracking().SingleAsync(e => e.EventId == id);
            Assert.Equal("Cancelled", ev.Status); Assert.Equal("Venue unavailable", ev.CancellationReason); Assert.Equal(2, ev.CurrentRegistrations);
        }
        var details = await client.GetStringAsync($"/admin/events/{id}?edit=true");
        Assert.Contains("Venue unavailable", details); Assert.DoesNotContain("id=\"event-edit-form\"", details);
        using var forbiddenEdit = await client.PostAsync($"/admin/events/{id}/edit", EditForm(30));
        Assert.Contains("can no longer be edited", await forbiddenEdit.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ended_events_are_read_only_and_students_cannot_open_details()
    {
        int id;
        var now = new EventClock(TimeProvider.System).Now;
        await using (var db = CreateContext())
        {
            var ev = new Event { Title = "Ended event fixture", VenueLocation = "Hall A", MaxCapacity = 10,
                CreatedByUserId = await db.Users.Where(u => u.Email == _adminEmail).Select(u => u.UserId).SingleAsync(),
                EventStart = now.AddDays(-2), EventEnd = now.AddDays(-2).AddHours(1),
                RegStart = DateOnly.FromDateTime(now.AddDays(-5)), RegEnd = DateOnly.FromDateTime(now.AddDays(-3)) };
            db.Events.Add(ev); await db.SaveChangesAsync(); id = ev.EventId;
        }
        using var student = NewClient();
        using var studentLogin = await SignInAsync(student, _studentEmail, _password);
        using var denied = await student.GetAsync($"/admin/events/{id}");
        Assert.StartsWith("/access-denied?", RedirectPath(denied));
        await using var context = CreateContext();
        var service = new EventService(context, new EventClock(TimeProvider.System));
        var model = await service.GetDetailsAsync(id, true, default);
        Assert.NotNull(model); Assert.False(model.CanManage); Assert.False(model.Editing);
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.CancelAsync(id, "Ended event", model.Event.Version, default));
    }

    [Fact]
    public async Task Event_history_is_admin_only_and_opens_read_only_details_with_safe_filter_navigation()
    {
        var title = $"History <script>fixture</script> {Guid.NewGuid():N}";
        var now = new EventClock(TimeProvider.System).Now;
        int id;
        await using (var db = CreateContext())
        {
            var ev = new Event { Title = title, VenueLocation = "History Hall", MaxCapacity = 20,
                EventStart = now.AddDays(-3), EventEnd = now.AddDays(-3).AddHours(2),
                RegStart = DateOnly.FromDateTime(now.AddDays(-6)), RegEnd = DateOnly.FromDateTime(now.AddDays(-4)),
                CreatedByUserId = await db.Users.Where(u => u.Email == _adminEmail).Select(u => u.UserId).SingleAsync() };
            db.Events.Add(ev); await db.SaveChangesAsync(); id = ev.EventId;
        }
        using var anonymous = NewClient();
        using var redirect = await anonymous.GetAsync("/admin/events/history");
        Assert.StartsWith("/login?", RedirectPath(redirect));
        using var student = NewClient();
        using var studentLogin = await SignInAsync(student, _studentEmail, _password);
        using var denied = await student.GetAsync("/admin/events/history");
        Assert.StartsWith("/access-denied?", RedirectPath(denied));
        using var admin = NewClient();
        using var adminLogin = await SignInAsync(admin, _adminEmail, _password);
        var html = await admin.GetStringAsync($"/admin/events/history?q={Uri.EscapeDataString(title)}&outcome=Completed");
        Assert.Contains(WebUtility.HtmlEncode(title), html);
        Assert.DoesNotContain(title, html);
        Assert.Contains("Open to all colleges", html);
        Assert.Contains("History Hall", html);
        Assert.Contains($"/admin/events/{id}?from=history", html);
        Assert.Contains($"/admin/events/{id}/analytics?from=history", html);
        var details = await admin.GetStringAsync($"/admin/events/{id}?from=history&edit=true");
        Assert.Contains("Back to history", details);
        Assert.DoesNotContain("id=\"event-edit-form\"", details);
        Assert.DoesNotContain("id=\"open-cancel-event\"", details);
        var empty = await admin.GetStringAsync($"/admin/events/history?q={Uri.EscapeDataString(title)}&outcome=Cancelled");
        Assert.Contains("No matching events", empty);
        Assert.Contains("Clear filters", empty);
    }

    [Fact]
    public async Task Preregistered_roster_hides_email_orders_columns_and_protects_pass_cancellation()
    {
        int id, registrationId; string version, eventVersion;
        await using (var db = CreateContext())
        {
            var day = new EventClock(TimeProvider.System).Now.AddDays(5);
            var ev = new Event { Title = "Roster verification event", VenueLocation = "Campus Hall", MaxCapacity = 30, CurrentRegistrations = 1,
                CreatedByUserId = await db.Users.Where(u => u.Email == _adminEmail).Select(u => u.UserId).SingleAsync(),
                EventStart = day, EventEnd = day.AddHours(2), RegStart = DateOnly.FromDateTime(day.AddDays(-3)), RegEnd = DateOnly.FromDateTime(day.AddDays(-1)) };
            var pass = new EventRegistration { Event = ev, StudentId = _studentId, CurrentYearLvl = 3, CurrentSection = "SBIT-3A" };
            db.EventRegistrations.Add(pass); await db.SaveChangesAsync();
            id = ev.EventId; registrationId = pass.EventRegistrationId; version = Convert.ToBase64String(pass.RowVersion); eventVersion = Convert.ToBase64String(ev.RowVersion);
        }
        var path = $"/admin/events/{id}/preregistered";
        var post = $"{path}/{registrationId}/void";
        using var anonymous = NewClient(); using var anonymousPage = await anonymous.GetAsync(path);
        Assert.StartsWith("/login?", RedirectPath(anonymousPage));
        using var student = NewClient(); using var studentLogin = await SignInAsync(student, _studentEmail, _password);
        using var denied = await student.GetAsync(path); Assert.StartsWith("/access-denied?", RedirectPath(denied));
        using var client = NewClient(); using var login = await SignInAsync(client, _adminEmail, _password);
        using var missing = await client.GetAsync("/admin/events/2147483647/preregistered"); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var page = await client.GetAsync(path); var html = await page.Content.ReadAsStringAsync();
        Assert.DoesNotContain(_studentEmail, html); Assert.DoesNotContain("Institutional email", html);
        Assert.Contains("Test Student", html); Assert.Contains(_studentId, html);
        var headings = Regex.Match(html, "aria-hidden=\"true\"[^>]*>\\s*<span>Ticket ref</span>[\\s\\S]*?</div>").Value;
        Assert.NotEmpty(headings);
        headings = WebUtility.HtmlDecode(headings);
        Assert.True(headings.IndexOf("Ticket ref", StringComparison.Ordinal) < headings.IndexOf("Student", StringComparison.Ordinal));
        Assert.True(headings.IndexOf("Student", StringComparison.Ordinal) < headings.IndexOf("College & program", StringComparison.Ordinal));
        Assert.True(headings.IndexOf("College & program", StringComparison.Ordinal) < headings.IndexOf("Year & section", StringComparison.Ordinal));
        Assert.True(headings.IndexOf("Year & section", StringComparison.Ordinal) < headings.IndexOf("Status", StringComparison.Ordinal));
        if (Environment.GetEnvironmentVariable("MVC_EVENTS_ROSTER_CAPTURE") is { Length: > 0 } capture) await File.WriteAllTextAsync(capture, html);
        using var noCsrf = await client.PostAsync(post, Form(("Version", version), ("EventVersion", eventVersion), ("Confirmed", "true")));
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        var csrf = await TokenAsync(page);
        using var wrongEvent = await client.PostAsync($"/admin/events/2147483647/preregistered/{registrationId}/void", Form(("Version", version), ("EventVersion", eventVersion), ("Confirmed", "true"), ("__RequestVerificationToken", csrf)));
        Assert.Equal(HttpStatusCode.NotFound, wrongEvent.StatusCode);
        using var unconfirmed = await client.PostAsync(post, Form(("Version", version), ("EventVersion", eventVersion), ("__RequestVerificationToken", csrf)));
        Assert.StartsWith(path, RedirectPath(unconfirmed));
        var error = await client.GetStringAsync(path); Assert.Contains("Confirm that you want to cancel", error);
        using var success = await client.PostAsync(post, Form(("Version", version), ("EventVersion", eventVersion), ("Confirmed", "true"), ("__RequestVerificationToken", csrf)));
        Assert.StartsWith(path, RedirectPath(success));
        var cancelled = await client.GetStringAsync($"{path}?cohort=Cancelled");
        Assert.Contains(_studentId, cancelled); Assert.DoesNotContain("data-prepare-void", cancelled);
        var filtered = await client.GetStringAsync($"{path}?cohort=Cancelled&from=history&q=Test&college=Computing&program=Information%20Technology&year=3");
        Assert.Contains(_studentId, filtered);
        Assert.Contains("aria-label=\"3 active filters\"", filtered);
        var reset = Regex.Match(filtered, "href=\"([^\"]+)\"[^>]*>Reset</a>");
        Assert.True(reset.Success);
        var resetQuery = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(new Uri(client.BaseAddress!, WebUtility.HtmlDecode(reset.Groups[1].Value)).Query);
        Assert.Equal("Test", resetQuery["q"].ToString());
        Assert.Equal("Cancelled", resetQuery["cohort"].ToString());
        Assert.Equal("history", resetQuery["from"].ToString());
        Assert.False(resetQuery.ContainsKey("college") || resetQuery.ContainsKey("program") || resetQuery.ContainsKey("year") || resetQuery.ContainsKey("page"));
        await using var verification = CreateContext();
        Assert.Equal(0, await verification.Events.Where(e => e.EventId == id).Select(e => e.CurrentRegistrations).SingleAsync());
        Assert.Equal("Cancelled", await verification.EventRegistrations.Where(r => r.EventRegistrationId == registrationId).Select(r => r.Status).SingleAsync());
    }

    [Fact]
    public async Task Scanner_routes_are_admin_only_csrf_protected_and_confirm_only_the_selected_event()
    {
        int id, passId;
        await using (var db = CreateContext())
        {
            var start = new EventClock(TimeProvider.System).Now.AddDays(5);
            var ev = new Event { Title = "Scanner verification event", VenueLocation = "Campus Hall", MaxCapacity = 20, CurrentRegistrations = 1,
                CreatedByUserId = await db.Users.Where(u => u.Email == _adminEmail).Select(u => u.UserId).SingleAsync(),
                EventStart = start, EventEnd = start.AddHours(2), RegStart = DateOnly.FromDateTime(start.AddDays(-3)), RegEnd = DateOnly.FromDateTime(start.AddDays(-1)) };
            var pass = new EventRegistration { Event = ev, StudentId = _studentId, CurrentYearLvl = 3, CurrentSection = "SBIT-3A" };
            db.EventRegistrations.Add(pass); await db.SaveChangesAsync(); id = ev.EventId; passId = pass.EventRegistrationId;
        }
        var path = $"/admin/events/{id}/scanner";
        using var anonymous = NewClient(); using var unauthenticated = await anonymous.GetAsync(path); Assert.StartsWith("/login?", RedirectPath(unauthenticated));
        using var student = NewClient(); using var studentLogin = await SignInAsync(student, _studentEmail, _password);
        using var denied = await student.GetAsync(path); Assert.StartsWith("/access-denied?", RedirectPath(denied));
        using var client = NewClient(); using var login = await SignInAsync(client, _adminEmail, _password);
        using var page = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync(); Assert.Contains("Confirm check-in", html); Assert.DoesNotContain("type=\"checkbox\"", html);
        Assert.Contains("html5-qrcode-2.3.8.min.js", html);
        if (Environment.GetEnvironmentVariable("MVC_EVENTS_SCANNER_CAPTURE") is { Length: > 0 } capture) await File.WriteAllTextAsync(capture, html);
        using var missingCsrf = await client.PostAsync(path + "/lookup", Form(("query", _studentId))); Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        var csrf = await TokenAsync(page);
        using var forged = await client.PostAsync(path + "/lookup", Form(("query", $"TCK-{id + 1:D4}-{passId:D5}"), ("__RequestVerificationToken", csrf)));
        Assert.Contains("WrongEventWarning", await forged.Content.ReadAsStringAsync());
        using var lookup = await client.PostAsync(path + "/lookup", Form(("query", _studentEmail), ("__RequestVerificationToken", csrf)));
        var json = await lookup.Content.ReadAsStringAsync(); Assert.DoesNotContain(_studentEmail, json);
        using var document = System.Text.Json.JsonDocument.Parse(json); var attendee = document.RootElement.GetProperty("attendee");
        Assert.True(document.RootElement.GetProperty("canConfirm").GetBoolean());
        var version = attendee.GetProperty("version").GetString()!; var eventVersion = attendee.GetProperty("eventVersion").GetString()!;
        using var missingConfirmCsrf = await client.PostAsync(path + "/confirm", Form(("RegistrationId", passId.ToString()), ("Version", version), ("EventVersion", eventVersion), ("Confirmed", "true")));
        Assert.Equal(HttpStatusCode.BadRequest, missingConfirmCsrf.StatusCode);
        using var unknownGate = await client.PostAsync("/admin/events/2147483647/scanner/confirm", Form(("RegistrationId", passId.ToString()), ("Version", version), ("EventVersion", eventVersion), ("Confirmed", "true"), ("__RequestVerificationToken", csrf)));
        Assert.Equal(HttpStatusCode.NotFound, unknownGate.StatusCode);
        using var success = await client.PostAsync(path + "/confirm", Form(("RegistrationId", passId.ToString()), ("Version", version), ("EventVersion", eventVersion), ("Confirmed", "true"), ("__RequestVerificationToken", csrf)));
        Assert.Contains("CheckedIn", await success.Content.ReadAsStringAsync());
        using var repeat = await client.PostAsync(path + "/confirm", Form(("RegistrationId", passId.ToString()), ("Version", version), ("EventVersion", eventVersion), ("Confirmed", "true"), ("__RequestVerificationToken", csrf)));
        Assert.Contains("DuplicateWarning", await repeat.Content.ReadAsStringAsync());
        await using var verification = CreateContext();
        Assert.Equal("Present", await verification.EventRegistrations.Where(r => r.EventRegistrationId == passId).Select(r => r.Status).SingleAsync());
        Assert.Equal(1, await verification.Events.Where(e => e.EventId == id).Select(e => e.CurrentRegistrations).SingleAsync());
    }

    [Fact]
    public async Task Attendance_requires_admin_renders_real_data_and_shares_the_event_header()
    {
        int id;
        await using (var db = CreateContext())
        {
            var day = new EventClock(TimeProvider.System).Now.AddDays(5);
            var ev = new Event { Title = "Attendance <verification>", VenueLocation = "Campus Hall", MaxCapacity = 30, CurrentRegistrations = 1,
                CreatedByUserId = await db.Users.Where(u => u.Email == _adminEmail).Select(u => u.UserId).SingleAsync(),
                EventStart = day, EventEnd = day.AddHours(2), RegStart = DateOnly.FromDateTime(day.AddDays(-3)), RegEnd = DateOnly.FromDateTime(day.AddDays(-1)) };
            db.EventRegistrations.Add(new EventRegistration { Event = ev, StudentId = _studentId, CurrentYearLvl = 4, CurrentSection = "SBIT-4B", Status = "Present", CheckInTimestamp = day });
            await db.SaveChangesAsync(); id = ev.EventId;
        }
        var path = $"/admin/events/{id}/attendance";
        using var anonymous = NewClient(); using var anonymousPage = await anonymous.GetAsync(path);
        Assert.StartsWith("/login?", RedirectPath(anonymousPage));
        using var student = NewClient(); using var studentLogin = await SignInAsync(student, _studentEmail, _password);
        using var denied = await student.GetAsync(path); Assert.StartsWith("/access-denied?", RedirectPath(denied));
        using var client = NewClient(); using var login = await SignInAsync(client, _adminEmail, _password);
        using var missing = await client.GetAsync("/admin/events/2147483647/attendance"); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var page = await client.GetAsync(path + "?from=history"); Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Test Student", html); Assert.Contains(_studentId, html); Assert.Contains("SBIT-4B", html);
        Assert.DoesNotContain(_studentEmail, html); Assert.DoesNotContain("Attendance <verification>", html);
        Assert.Contains("Attendance &lt;verification&gt;", html); Assert.Contains("Checked in (PHT)", html);
        Assert.Contains("Back to history", html); Assert.Contains("Page 1 of 1", html);
        Assert.DoesNotContain("open-cancel-event", html); Assert.Contains("no-store", page.Headers.CacheControl!.ToString());
        var header = Regex.Match(html, "<header>[\\s\\S]*?</header>").Value;
        Assert.Contains("Registered seats", header); Assert.Contains("Venue", header);
        foreach (var suffix in new[] { "", "/preregistered", "/scanner" })
        {
            var otherHtml = await client.GetStringAsync($"/admin/events/{id}{suffix}");
            Assert.Contains(Regex.Match(header, "<dl[\\s\\S]*?</dl>").Value, otherHtml);
            Assert.Contains($"/admin/events/{id}/attendance", otherHtml);
        }
        var empty = await client.GetStringAsync(path + "?q=absent-name&college=unlisted&program=other");
        Assert.Contains("No matching students", empty); Assert.Contains("value=\"absent-name\"", empty);
        Assert.Contains("Reset filters", empty);
    }

    [Fact]
    public async Task Analytics_is_admin_only_encoded_read_only_and_supports_history_and_cohort_filters()
    {
        int id;
        await using (var db = CreateContext())
        {
            var day = new EventClock(TimeProvider.System).Now.AddDays(-3);
            var ev = new Event { Title = "Analytics <verification>", VenueLocation = "Report Hall", MaxCapacity = 50, CurrentRegistrations = 1,
                CreatedByUserId = await db.Users.Where(u => u.Email == _adminEmail).Select(u => u.UserId).SingleAsync(),
                EventStart = day, EventEnd = day.AddHours(2), RegStart = DateOnly.FromDateTime(day.AddDays(-3)), RegEnd = DateOnly.FromDateTime(day.AddDays(-1)) };
            db.EventRegistrations.Add(new EventRegistration { Event = ev, StudentId = _studentId, CurrentYearLvl = 4, CurrentSection = "SBIT-4B", Status = "Present", CheckInTimestamp = day });
            await db.SaveChangesAsync(); id = ev.EventId;
        }
        var path = $"/admin/events/{id}/analytics";
        using var anonymous = NewClient(); using var redirect = await anonymous.GetAsync(path); Assert.StartsWith("/login?", RedirectPath(redirect));
        using var student = NewClient(); using var studentLogin = await SignInAsync(student, _studentEmail, _password);
        using var denied = await student.GetAsync(path); Assert.StartsWith("/access-denied?", RedirectPath(denied));
        using var client = NewClient(); using var login = await SignInAsync(client, _adminEmail, _password);
        using var missing = await client.GetAsync("/admin/events/2147483647/analytics"); Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var page = await client.GetAsync(path + "?from=history&interval=5"); Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Analytics &lt;verification&gt;", html); Assert.DoesNotContain(_studentEmail, html);
        Assert.Contains("Test Student", html); Assert.Contains("SBIT-4B", html); Assert.Contains("Back to history", html);
        Assert.Contains("Confirmed arrivals per 5 minutes", html); Assert.DoesNotContain("Attendance by college", html);
        Assert.Contains("<polyline data-arrival-line", html); Assert.Contains("data-arrival-point", html);
        Assert.Contains("data-demographic-slice", html); Assert.DoesNotContain("data-demographic-legend", html);
        Assert.DoesNotContain("data-demographic-readout", html);
        Assert.Contains("data-chart-label=\"Booking fill:", html);
        Assert.Contains("data-chart-label=\"Attendance / capacity:", html);
        Assert.Contains("Attendance turnout", html); Assert.Contains("Did not attend", html);
        Assert.Contains("event-analytics.js", html); Assert.DoesNotContain("persistent.oaistatic.com", html);
        Assert.DoesNotContain(".ToString(&quot;", html);
        Assert.DoesNotContain("open-cancel-event", html); Assert.Contains("no-store", page.Headers.CacheControl!.ToString());
        var empty = await client.GetStringAsync(path + "?cohort=Cancelled&q=absent&interval=30&from=history");
        Assert.Contains("No students in this cohort", empty); Assert.Contains("value=\"absent\"", empty);
        Assert.Contains("Confirmed arrivals per 30 minutes", empty);
        var college = await client.GetStringAsync(path + "?dimension=College&interval=5&from=history");
        Assert.Contains("data-demographic-panel=\"College\" class=", college);
        Assert.Contains("name=\"dimension\" value=\"College\"", college);
    }

    [Theory]
    [InlineData("Upcoming", "Awaiting registrations")]
    [InlineData("Completed", "Final attendance")]
    [InlineData("Cancelled", "Event cancelled")]
    public async Task Analytics_empty_charts_are_honest_and_turnout_status_follows_event_state(string status, string label)
    {
        int id;
        await using (var db = CreateContext())
        {
            var day = new EventClock(TimeProvider.System).Now.AddDays(3);
            var ev = new Event { Title = "Empty graph fixture", VenueLocation = "Campus Hall", MaxCapacity = 50, Status = status,
                CancellationReason = status == "Cancelled" ? "Fixture cancellation" : null,
                CreatedByUserId = await db.Users.Where(u => u.Email == _adminEmail).Select(u => u.UserId).SingleAsync(),
                EventStart = day, EventEnd = day.AddHours(2), RegStart = DateOnly.FromDateTime(day.AddDays(-3)), RegEnd = DateOnly.FromDateTime(day.AddDays(-1)) };
            db.Events.Add(ev); await db.SaveChangesAsync(); id = ev.EventId;
        }
        using var client = NewClient(); using var login = await SignInAsync(client, _adminEmail, _password);
        var html = await client.GetStringAsync($"/admin/events/{id}/analytics?dimension=invalid");
        Assert.Contains(label, html); Assert.Contains("No demographic cohorts registered yet.", html);
        Assert.Contains("No confirmed arrivals yet.", html); Assert.DoesNotContain("data-demographic-slice=", html);
        Assert.DoesNotContain("data-arrival-chart", html); Assert.Contains("data-demographic-panel=\"Program\" class=", html);
        Assert.Contains("0.0%", html);
    }

    [Fact]
    public async Task User_directory_combines_roles_creates_edits_and_exports_students_with_snapshot_protection()
    {
        using var anonymous = NewClient();
        foreach (var path in new[] { "/admin/users", "/admin/users/template", "/admin/users/export" })
        { using var response = await anonymous.GetAsync(path); Assert.StartsWith("/login?", RedirectPath(response)); }
        using var student = NewClient(); using var studentLogin = await SignInAsync(student, _studentEmail, _password);
        using var denied = await student.GetAsync("/admin/users"); Assert.StartsWith("/access-denied?", RedirectPath(denied));
        using var deniedCreation = await student.PostAsync("/admin/users/create", Form()); Assert.StartsWith("/access-denied?", RedirectPath(deniedCreation));
        using var client = NewClient(); using var login = await SignInAsync(client, _adminEmail, _password);
        using var page = await client.GetAsync("/admin/users"); Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync(); Assert.Contains("user-directory.js", html); Assert.Contains("no-store", page.Headers.CacheControl!.ToString());
        Assert.Contains("User Directory</h1>", html); Assert.DoesNotContain(">Account Management</a>", html); Assert.DoesNotContain(">Student Directory</a>", html);
        Assert.Contains("Add account</a>", html); Assert.Contains("name=\"NewAccount.Role\"", html);
        var csrf = await TokenAsync(page); var id = "HTTP-" + Guid.NewGuid().ToString("N"); var email = $"http-{Guid.NewGuid():N}@example.test";
        Dictionary<string,string> Fields() => new() { ["NewAccount.Role"] = "Student", ["NewAccount.StudentId"] = id, ["NewAccount.Email"] = email, ["NewAccount.FirstName"] = "Mikaela <test>", ["NewAccount.LastName"] = "Dizon", ["NewAccount.BirthDate"] = "2005-04-14", ["NewAccount.Gender"] = "Female", ["NewAccount.Campus"] = "San Bartolome", ["NewAccount.College"] = "College of Computer Studies", ["NewAccount.Program"] = "BSIT", ["NewAccount.Active"] = "true", ["NewAccount.Password"] = _password, ["__RequestVerificationToken"] = csrf };
        using var missingCsrf = await client.PostAsync("/admin/users/create", Form()); Assert.Equal(HttpStatusCode.BadRequest, missingCsrf.StatusCode);
        using var created = await client.PostAsync("/admin/users/create", new FormUrlEncodedContent(Fields())); Assert.StartsWith("/admin/users", RedirectPath(created));
        await using var db = CreateContext();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.UserId).SingleAsync();
        var result = await client.GetStringAsync($"/admin/users?q={id}&id={userId}");
        Assert.DoesNotContain("Mikaela <test>", result); Assert.Contains("Mikaela &lt;test&gt;", result); Assert.DoesNotContain(_password, result);
        var version = WebUtility.HtmlDecode(Regex.Match(result, "name=\"Editor.Version\"[^>]*value=\"([^\"]+)\"").Groups[1].Value); Assert.NotEmpty(version);
        var edit = new Dictionary<string,string> { ["Editor.Id"] = userId.ToString(), ["Editor.Version"] = version, ["Editor.Email"] = email, ["Editor.Role"] = "Student", ["Editor.StudentId"] = "forged", ["Editor.FirstName"] = "Mikaela", ["Editor.LastName"] = "Dizon", ["Editor.BirthDate"] = "2005-05-15", ["Editor.Gender"] = "Female", ["Editor.Campus"] = "San Bartolome", ["Editor.College"] = "College of Computer Studies", ["Editor.Program"] = "BSIT", ["Editor.Active"] = "false", ["Editor.Password"] = _password + "new", ["__RequestVerificationToken"] = csrf };
        using var saved = await client.PostAsync("/admin/users/save", new FormUrlEncodedContent(edit)); Assert.StartsWith("/admin/users", RedirectPath(saved));
        using var stale = await client.PostAsync("/admin/users/save", new FormUrlEncodedContent(edit)); var staleHtml = await stale.Content.ReadAsStringAsync(); Assert.Contains("The account changed", staleHtml); Assert.DoesNotContain(_password, staleHtml);
        var profile = await db.Students.AsNoTracking().Include(s => s.User).SingleAsync(s => s.StudentId == id);
        Assert.False(profile.User.IsActive); Assert.Equal(new DateOnly(2005, 5, 15), profile.BirthDate);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<User>().VerifyHashedPassword(profile.User, profile.User.PasswordHash, _password + "new"));
        using var template = await client.GetAsync("/admin/users/template"); Assert.Contains(StudentCsvService.Header, await template.Content.ReadAsStringAsync());
        var csv = await client.GetStringAsync("/admin/users/export?q=" + id); Assert.Contains(email, csv); Assert.DoesNotContain(_password, csv); Assert.Contains("Inactive", csv); Assert.Contains("05/15/2005", csv);
    }

    [Fact]
    public async Task User_directory_creates_admin_without_student_fields_and_clears_password_on_validation_errors()
    {
        using var client = NewClient(); using var login = await SignInAsync(client, _adminEmail, _password);
        using var page = await client.GetAsync("/admin/users?open=create"); var csrf = await TokenAsync(page);
        var email = $"admin-create-{Guid.NewGuid():N}@example.test";
        FormUrlEncodedContent Input(string role, string targetEmail) => Form(("NewAccount.Role", role), ("NewAccount.Email", targetEmail), ("NewAccount.Password", _password), ("NewAccount.Active", "true"), ("__RequestVerificationToken", csrf));
        using var created = await client.PostAsync("/admin/users/create", Input("Admin", email)); Assert.StartsWith("/admin/users", RedirectPath(created));
        await using var db = CreateContext();
        var user = await db.Users.AsNoTracking().Include(u => u.Student).SingleAsync(u => u.Email == email); Assert.Equal("Admin", user.Role); Assert.Null(user.Student);
        using var duplicate = await client.PostAsync("/admin/users/create", Input("Admin", email)); var html = await duplicate.Content.ReadAsStringAsync(); Assert.Contains("already belongs", html); Assert.DoesNotContain(_password, html);
        Assert.Contains("id=\"create-account-dialog\" open", html); Assert.Contains("id=\"create-student-fields\" hidden", html);
        var invalidEmail = $"incomplete-{Guid.NewGuid():N}@example.test";
        using var incomplete = await client.PostAsync("/admin/users/create", Input("Student", invalidEmail)); Assert.DoesNotContain(_password, await incomplete.Content.ReadAsStringAsync()); Assert.False(await db.Users.AnyAsync(u => u.Email == invalidEmail));
        using var unsupported = await client.PostAsync("/admin/users/create", Input("SuperAdmin", invalidEmail)); Assert.Contains("Choose Student or Administrator", await unsupported.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task User_directory_filters_and_export_match_and_legacy_urls_use_the_same_page()
    {
        using var client = NewClient(); using var login = await SignInAsync(client, _adminEmail, _password);
        var filters = $"q={_studentId}&role=Student&status=Active&college=Computing&program=Information%20Technology&campus=Main";
        var html = await client.GetStringAsync("/admin/users?" + filters);
        Assert.Contains(_studentEmail, html); Assert.Contains("Information Technology", html); Assert.Contains("1 users", html);
        var link = WebUtility.HtmlDecode(Regex.Match(html, "href=\"([^\"]+)\"[^>]*>View</a>").Groups[1].Value); Assert.Contains("college=Computing", link); Assert.Contains("campus=Main", link);
        var csv = await client.GetStringAsync("/admin/users/export?" + filters); Assert.Contains(_studentEmail, csv); Assert.DoesNotContain(_adminEmail, csv); Assert.DoesNotContain("Password", csv);
        var conflicting = await client.GetStringAsync("/admin/users/export?role=Admin&college=Computing"); Assert.DoesNotContain(_studentEmail, conflicting); Assert.DoesNotContain(_adminEmail, conflicting);
        foreach (var path in new[] { "/admin/accounts", "/admin/students" }) { var alias = await client.GetStringAsync(path); Assert.Contains("User Directory</h1>", alias); Assert.Contains("user-directory.js", alias); }
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Admin")]
    public async Task Account_creation_uses_selected_role_hashes_password_and_creates_only_required_profile(string role)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountManagementService>();
        await using var db = CreateContext();
        var actor = await db.Users.Where(u => u.Email == _adminEmail).Select(u => u.UserId).SingleAsync();
        var input = new CreateAccountInput { Role = role, Email = $"create-{Guid.NewGuid():N}@example.test", Password = _password,
            StudentId = $"NEW-{Guid.NewGuid():N}", FirstName = "Mikaela", LastName = "Dizon", BirthDate = new DateOnly(2005, 4, 14) };
        await service.CreateAsync(input, actor, default);
        var account = await db.Users.AsNoTracking().Include(u => u.Student).SingleAsync(u => u.Email == input.Email);
        Assert.Equal(role, account.Role);
        Assert.NotEqual(_password, account.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<User>().VerifyHashedPassword(account, account.PasswordHash, _password));
        Assert.Equal(role == "Student", account.Student is not null);
        if (account.Student is { } profile) Assert.Equal(input.StudentId, profile.StudentId);
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.CreateAsync(input, actor, default));
        Assert.Equal(1, await db.Users.CountAsync(u => u.Email == input.Email));
    }

    [Fact]
    public async Task Account_creation_rejects_invalid_student_role_and_nonadministrator_without_orphans()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AccountManagementService>();
        await using var db = CreateContext();
        var actor = await db.Users.Where(u => u.Email == _adminEmail).Select(u => u.UserId).SingleAsync();
        var studentActor = await db.Users.Where(u => u.Email == _studentEmail).Select(u => u.UserId).SingleAsync();
        var input = new CreateAccountInput { Role = "Student", Email = $"invalid-{Guid.NewGuid():N}@example.test", Password = _password };
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.CreateAsync(input, actor, default));
        input.Role = "SuperAdmin";
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.CreateAsync(input, actor, default));
        input.Role = "Admin";
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.CreateAsync(input, studentActor, default));
        Assert.False(await db.Users.AnyAsync(u => u.Email == input.Email));
    }

    [Fact]
    public async Task Student_csv_endpoint_enforces_csrf_and_reports_real_partial_results_without_echoing_passwords()
    {
        using var client = NewClient(); using var login = await SignInAsync(client, _adminEmail, _password);
        using var page = await client.GetAsync("/admin/users"); var csrf = await TokenAsync(page);
        using var denied = await client.PostAsync("/admin/users/import", Form()); Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        var marker = Guid.NewGuid().ToString("N"); var csv = StudentCsvService.Header + $"\r\nCSV-{marker},Althea,,Navarro,Female,San Bartolome,College of Computer Studies,BSIT,{marker}@example.test,2005-04-14\r\nBAD-{marker},Lara,,Reyes,Female,San Bartolome,College of Computer Studies,BSIT,bad-{marker}@example.test,invalid-date\r\n";
        using var upload = new MultipartFormDataContent(); upload.Add(new StringContent(csrf), "__RequestVerificationToken"); upload.Add(new StringContent(_password), "password"); upload.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(csv)), "roster", "students.csv");
        using var response = await client.PostAsync("/admin/users/import", upload); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(); Assert.Contains("1 created", html); Assert.Contains("1 errors", html); Assert.DoesNotContain(_password, html);
        await using var db = CreateContext(); Assert.True(await db.Students.AnyAsync(s => s.StudentId == "CSV-" + marker)); Assert.False(await db.Users.AnyAsync(u => u.Email == "bad-" + marker + "@example.test"));
    }

    [Fact]
    public async Task Account_management_protects_access_and_saves_current_account_snapshots()
    {
        using var anonymous = NewClient();
        using var anonymousPage = await anonymous.GetAsync("/admin/accounts");
        Assert.StartsWith("/login?", RedirectPath(anonymousPage));
        using var student = NewClient();
        using var studentLogin = await SignInAsync(student, _studentEmail, _password);
        using var studentPage = await student.GetAsync("/admin/accounts");
        Assert.StartsWith("/access-denied?", RedirectPath(studentPage));
        using var client = NewClient();
        using var login = await SignInAsync(client, _adminEmail, _password);
        await using var db = CreateContext();
        var studentId = await db.Users.Where(u => u.Email == _studentEmail).Select(u => u.UserId).SingleAsync();
        var adminId = await db.Users.Where(u => u.Email == _adminEmail).Select(u => u.UserId).SingleAsync();
        using var page = await client.GetAsync($"/admin/accounts?id={studentId}&q={_studentId}");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.Content.ReadAsStringAsync();
        Assert.Contains("Student Information", html);
        Assert.Contains("Account Information", html);
        Assert.Contains("open=create", html);
        Assert.Contains("open=import", html);
        Assert.Contains("aria-label=\"View ", html);
        Assert.DoesNotContain("Deactivate account", html);
        Assert.DoesNotContain(_password, html);
        var csrf = await TokenAsync(page);
        string ReadVersion(string markup)
        {
            var match = Regex.Match(markup, "name=\"Editor.Version\"[^>]*value=\"([^\"]+)\"");
            Assert.True(match.Success, "Account details should contain a protected version.");
            return WebUtility.HtmlDecode(match.Groups[1].Value);
        }
        var version = ReadVersion(html);
        var updatedEmail = $"updated-{Guid.NewGuid():N}@example.test";
        FormUrlEncodedContent EditStudent(string email) => Form(
            ("Editor.Id", studentId.ToString()), ("Editor.Email", email), ("Editor.Role", "Student"),
            ("Editor.Active", "false"), ("Editor.Version", version), ("Editor.StudentId", "forged"),
            ("Editor.FirstName", "Test"), ("Editor.LastName", "Student"), ("Editor.Gender", "Not specified"),
            ("Editor.Campus", "Main"), ("Editor.College", "Computing"), ("Editor.Program", "Information Technology"),
            ("__RequestVerificationToken", csrf));
        using var noCsrf = await client.PostAsync("/admin/accounts/save", Form());
        Assert.Equal(HttpStatusCode.BadRequest, noCsrf.StatusCode);
        using var saved = await client.PostAsync("/admin/accounts/save", EditStudent(updatedEmail));
        Assert.StartsWith("/admin/users", RedirectPath(saved));
        Assert.True(await db.Users.AnyAsync(a => a.UserId == studentId && a.Email == updatedEmail && !a.IsActive));
        Assert.True(await db.Students.AnyAsync(s => s.UserId == studentId && s.StudentId == _studentId));
        using var stale = await client.PostAsync("/admin/accounts/save", EditStudent(_studentEmail));
        Assert.Contains("The account changed", await stale.Content.ReadAsStringAsync());
        using var ownPage = await client.GetAsync($"/admin/accounts?id={adminId}");
        using var deactivateSelf = await client.PostAsync("/admin/accounts/save", Form(
            ("Editor.Id", adminId.ToString()), ("Editor.Email", _adminEmail), ("Editor.Role", "Admin"),
            ("Editor.Active", "false"), ("Editor.Version", ReadVersion(await ownPage.Content.ReadAsStringAsync())),
            ("__RequestVerificationToken", csrf)));
        Assert.Contains("Your account must remain an active administrator", await deactivateSelf.Content.ReadAsStringAsync());
        var csv = await client.GetStringAsync($"/admin/accounts/export?q={updatedEmail}&status=Inactive");
        Assert.Contains(updatedEmail, csv);
        Assert.DoesNotContain(_adminEmail, csv);
        Assert.DoesNotContain("Password", csv);
    }

    [Fact]
    public async Task Student_dashboard_shows_eligible_events_and_only_owned_registration_references()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        await using var db = CreateContext();
        var admin = await db.Users.Where(u => u.Email == _adminEmail).Select(u => u.UserId).SingleAsync();
        var student = await db.Students.SingleAsync(s => s.StudentId == _studentId);
        var today = new EventClock(TimeProvider.System).Now.Date;
        Event MakeEvent(string label, int offset = 6) => new()
        {
            Title = label + " " + marker, VenueLocation = "Campus Hall", MaxCapacity = 30, CreatedByUserId = admin,
            EventStart = today.AddDays(offset).AddHours(9), EventEnd = today.AddDays(offset).AddHours(11),
            RegStart = DateOnly.FromDateTime(today.AddDays(Math.Min(-4, offset - 3))), RegEnd = DateOnly.FromDateTime(today.AddDays(offset - 1))
        };
        var eligible = MakeEvent("Eligible workshop"); eligible.TargetDepartment = student.Department; eligible.TargetProgram = " BSCS, BSIT "; eligible.TargetYearLevel = 3;
        var wrongCampus = MakeEvent("Restricted campus"); wrongCampus.TargetBranch = "Batasan";
        var wrongProgram = MakeEvent("Restricted program"); wrongProgram.TargetProgram = "XBSIT";
        var wrongCollege = MakeEvent("Restricted college"); wrongCollege.TargetDepartment = "Other college";
        var owned = MakeEvent("Owned completed seminar", -2); owned.Status = "Completed"; owned.TargetBranch = "Batasan";
        var voided = MakeEvent("Returned to catalog");
        var bannerName = Guid.NewGuid().ToString("N") + ".png";
        Directory.CreateDirectory(_bannerRoot);
        await File.WriteAllBytesAsync(Path.Combine(_bannerRoot, bannerName), Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAACklEQVR4nGNgAAAAAgAB9HFkpgAAAABJRU5ErkJggg=="));
        eligible.EventPhotoPath = wrongCampus.EventPhotoPath = owned.EventPhotoPath = bannerName;
        var otherStudent = NewAccount($"another-{marker}@example.test", "Student");
        otherStudent.Student = new Student
        {
            StudentId = "ANOTHER-" + marker, FirstName = "Other", LastName = "Student", Gender = "Not Specified",
            CampusBranch = "Batasan", Department = "Computing", Program = "BSIT"
        };
        db.Users.Add(otherStudent);
        db.Events.AddRange(eligible, wrongCampus, wrongProgram, wrongCollege, owned, voided);
        var pass = new EventRegistration { Event = owned, StudentId = _studentId, CurrentYearLvl = 3, CurrentSection = "BSIT-3A", Status = "Present", CheckInTimestamp = owned.EventStart };
        var foreignPass = new EventRegistration { Event = owned, Student = otherStudent.Student, StudentId = otherStudent.Student.StudentId, CurrentYearLvl = 3, CurrentSection = "BSIT-3B" };
        db.EventRegistrations.AddRange(pass, foreignPass,
            new EventRegistration { Event = voided, StudentId = _studentId, CurrentYearLvl = 3, CurrentSection = "BSIT-3A", Status = "Cancelled" });
        await db.SaveChangesAsync();
        using var client = NewClient();
        using var login = await SignInAsync(client, _studentEmail, _password);
        var html = await client.GetStringAsync("/student");
        Assert.Contains("user-portal user-dashboard", html);
        Assert.Contains("hero-showcase-container", html);
        Assert.Contains("Eligible workshop " + marker, html);
        Assert.Contains("Returned to catalog " + marker, html);
        Assert.DoesNotContain("Restricted campus " + marker, html);
        Assert.DoesNotContain("Restricted program " + marker, html);
        Assert.DoesNotContain("Restricted college " + marker, html);
        Assert.Contains($"TCK-{owned.EventId:D4}-{pass.EventRegistrationId:D5}", html);
        Assert.DoesNotContain($"TCK-{owned.EventId:D4}-{foreignPass.EventRegistrationId:D5}", html);
        Assert.DoesNotContain("ANOTHER-" + marker, html);
        using var ownedPass = await client.GetAsync($"/student/passes/{pass.EventRegistrationId}");
        Assert.Contains("Attendance has already been recorded", await ownedPass.Content.ReadAsStringAsync());
        using var foreign = await client.GetAsync($"/student/passes/{foreignPass.EventRegistrationId}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using var missing = await client.GetAsync($"/student/events/{wrongCampus.EventId}/banner");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var visibleBanner = await client.GetAsync($"/student/events/{eligible.EventId}/banner");
        Assert.Equal("image/png", visibleBanner.Content.Headers.ContentType?.MediaType);
        using var ownedBanner = await client.GetAsync($"/student/events/{owned.EventId}/banner");
        Assert.Equal(HttpStatusCode.OK, ownedBanner.StatusCode);
    }

    [Fact]
    public async Task Student_can_book_view_cancel_and_rebook_through_the_portal()
    {
        await using var db = CreateContext();
        var now = new EventClock(TimeProvider.System).Now;
        var ev = new Event
        {
            Title = "Student portal registration fixture", VenueLocation = "Campus Hall", MaxCapacity = 1,
            EventStart = now.AddDays(2), EventEnd = now.AddDays(2).AddHours(2),
            RegStart = DateOnly.FromDateTime(now), RegEnd = DateOnly.FromDateTime(now),
            TargetYearLevel = 3, CreatedByUserId = await db.Users.Where(u => u.Email == _adminEmail).Select(u => u.UserId).SingleAsync()
        };
        db.Events.Add(ev);
        await db.SaveChangesAsync();
        var path = $"/student/events/{ev.EventId}/register";
        using var client = NewClient();
        using var login = await SignInAsync(client, _studentEmail, _password);
        using var profile = await client.GetAsync("/student/profile");
        Assert.Contains("Academic information", await profile.Content.ReadAsStringAsync());
        using var page = await client.GetAsync(path);
        var csrf = await TokenAsync(page);
        FormUrlEncodedContent Booking(string year) => Form(("Year", year), ("Section", "BSIT-3A"),
            ("Terms", "true"), ("__RequestVerificationToken", csrf));

        using var wrongYear = await client.PostAsync(path, Booking("2"));
        Assert.Contains("restricted to a different academic group", await wrongYear.Content.ReadAsStringAsync());
        Assert.False(await db.EventRegistrations.AnyAsync(r => r.EventId == ev.EventId));
        using var booked = await client.PostAsync(path, Booking("3"));
        var passPath = RedirectPath(booked);
        Assert.StartsWith("/student/passes/", passPath);
        using var pass = await client.GetAsync(passPath);
        Assert.Contains("qrCanvas", await pass.Content.ReadAsStringAsync());
        using var duplicate = await client.PostAsync(path, Booking("3"));
        Assert.Equal(passPath, RedirectPath(duplicate));
        Assert.Equal(1, await db.Events.Where(e => e.EventId == ev.EventId).Select(e => e.CurrentRegistrations).SingleAsync());
        using var rejected = await client.PostAsync(passPath + "/cancel", Form());
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        using var cancelled = await client.PostAsync(passPath + "/cancel", Form(("__RequestVerificationToken", csrf)));
        Assert.StartsWith("/student?view=registered", RedirectPath(cancelled));
        using var cancelledPass = await client.GetAsync(passPath);
        Assert.Contains("Pass inactive", await cancelledPass.Content.ReadAsStringAsync());
        using var repeated = await client.PostAsync(passPath + "/cancel", Form(("__RequestVerificationToken", csrf)));
        Assert.Equal(0, await db.Events.Where(e => e.EventId == ev.EventId).Select(e => e.CurrentRegistrations).SingleAsync());
        using var bookedAgain = await client.PostAsync(path, Booking("3"));
        Assert.NotEqual(passPath, RedirectPath(bookedAgain));
        Assert.Equal(2, await db.EventRegistrations.CountAsync(r => r.EventId == ev.EventId));
    }

    [Fact]
    public async Task Student_password_change_requires_the_current_password_and_updates_own_sign_in()
    {
        using var client = NewClient();
        using var login = await SignInAsync(client, _studentEmail, _password);
        using var profile = await client.GetAsync("/student/profile");
        var csrf = await TokenAsync(profile);
        var newPassword = _password + "!updated";
        FormUrlEncodedContent PasswordChange(string current) => Form(("CurrentPassword", current), ("NewPassword", newPassword),
            ("ConfirmPassword", newPassword), ("__RequestVerificationToken", csrf));
        using var wrong = await client.PostAsync("/student/profile/password", PasswordChange("incorrect"));
        var rejected = await client.GetStringAsync(RedirectPath(wrong));
        Assert.Contains("current password is incorrect", rejected);
        using var saved = await client.PostAsync("/student/profile/password", PasswordChange(_password));
        var confirmation = await client.GetStringAsync(RedirectPath(saved));
        Assert.Contains("Your password was updated.", confirmation);
        using var oldSession = NewClient();
        using var oldLogin = await SignInAsync(oldSession, _studentEmail, _password);
        Assert.Equal(HttpStatusCode.OK, oldLogin.StatusCode);
        using var newSession = NewClient();
        using var newLogin = await SignInAsync(newSession, _studentEmail, newPassword);
        Assert.Equal("/student", RedirectPath(newLogin));
    }

    private ApplicationDbContext CreateContext()
    {
        var db = new ApplicationDbContext(_options);
        db.Database.UseTransaction(_transaction);
        return db;
    }

    private User NewAccount(string email, string role)
    {
        var user = new User { Email = email, Role = role, PasswordSalt = "", PasswordHash = "" };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, _password);
        return user;
    }

    private HttpClient NewClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    });

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string id, string password, string returnUrl = "")
    {
        using var page = await client.GetAsync("/login");
        using var form = Form(("Identifier", id), ("Password", password), ("ReturnUrl", returnUrl),
            ("__RequestVerificationToken", await TokenAsync(page)));
        return await client.PostAsync("/login", form);
    }

    private static FormUrlEncodedContent Form(params (string Name, string Value)[] fields) =>
        new(fields.Select(field => new KeyValuePair<string, string>(field.Name, field.Value)));

    private static async Task<string> TokenAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, "The page should render an antiforgery token.");
        return WebUtility.HtmlDecode(token.Groups[1].Value);
    }

    private static string RedirectPath(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location, exactMatch: false);
        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }
}
