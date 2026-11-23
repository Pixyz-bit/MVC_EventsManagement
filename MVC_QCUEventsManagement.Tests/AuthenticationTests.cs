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
        Assert.Contains("Confirmed arrivals per 5 minutes", html); Assert.Contains("Attendance by college", html);
        Assert.DoesNotContain("open-cancel-event", html); Assert.Contains("no-store", page.Headers.CacheControl!.ToString());
        var empty = await client.GetStringAsync(path + "?cohort=Cancelled&q=absent&interval=30&from=history");
        Assert.Contains("No students in this cohort", empty); Assert.Contains("value=\"absent\"", empty);
        Assert.Contains("Confirmed arrivals per 30 minutes", empty);
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
