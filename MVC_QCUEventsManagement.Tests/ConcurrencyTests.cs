using System.Data.Common;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.Models;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Tests;

// Methods in this class run serially; each pair of HTTP requests uses independent scoped contexts/connections.
public sealed class ConcurrencyTests(IsolatedSqlServerFixture database) : IClassFixture<IsolatedSqlServerFixture>
{
    private readonly string _password = Guid.NewGuid().ToString("N");
    private readonly PasswordHasher<User> _hasher = new();

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task Two_admins_confirming_one_pass_store_one_timestamp(int iteration)
    {
        var fixture = await SeedAsync(1, reserved: true, iteration: iteration);
        var gate = new RaceGate();
        using var factory = Factory(new CommandGate(gate, command => command.CommandText.Contains("UPDATE", StringComparison.Ordinal) && command.CommandText.Contains("EventRegistrationTable")));
        using var first = Client(factory); using var second = Client(factory);
        await LoginAsync(first, fixture.Admin1); await LoginAsync(second, fixture.Admin2);
        var path = $"/admin/events/{fixture.EventId}/scanner";
        var firstToken = await TokenAsync(first, path); var secondToken = await TokenAsync(second, path);
        using var lookup = await first.PostAsync(path + "/lookup", Form(("query", fixture.Student1.StudentId), ("__RequestVerificationToken", firstToken)));
        using var json = JsonDocument.Parse(await lookup.Content.ReadAsStringAsync());
        var attendee = json.RootElement.GetProperty("attendee");
        var version = attendee.GetProperty("version").GetString()!; var eventVersion = attendee.GetProperty("eventVersion").GetString()!;
        FormUrlEncodedContent Confirm(string csrf) => Form(("RegistrationId", fixture.PassId.ToString()), ("Version", version), ("EventVersion", eventVersion), ("Confirmed", "true"), ("__RequestVerificationToken", csrf));
        var results = await TogetherAsync(() => first.PostAsync(path + "/confirm", Confirm(firstToken)), () => second.PostAsync(path + "/confirm", Confirm(secondToken)));
        await AssertHandledAsync(results);
        var states = new List<string>();
        foreach (var response in results)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            states.Add(result.RootElement.GetProperty("state").GetString()!);
            response.Dispose();
        }
        Assert.Equal(new[] { "CheckedIn", "DuplicateWarning" }, states.Order().ToArray());
        gate.AssertContended();
        await using var verification = database.CreateContext();
        var pass = await verification.EventRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal("Present", pass.Status); Assert.NotNull(pass.CheckInTimestamp);
        var timestamp = pass.CheckInTimestamp; var rowVersion = pass.RowVersion.ToArray();
        Assert.Equal(1, await verification.Events.Select(e => e.CurrentRegistrations).SingleAsync());
        using var repeat = await second.PostAsync(path + "/confirm", Confirm(secondToken));
        Assert.Contains("DuplicateWarning", await repeat.Content.ReadAsStringAsync());
        var unchanged = await verification.EventRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal(timestamp, unchanged.CheckInTimestamp); Assert.Equal(rowVersion, unchanged.RowVersion);
        Assert.Equal(1, await verification.EventRegistrations.CountAsync(r => r.Status == "Present"));
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)] [InlineData(false, 3)] [InlineData(false, 4)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)] [InlineData(true, 3)] [InlineData(true, 4)]
    public async Task Simultaneous_bookings_never_oversell_or_create_duplicate_passes(bool sameStudent, int iteration)
    {
        // The duplicate test has spare seats, so capacity cannot hide a broken uniqueness rule.
        var fixture = await SeedAsync(sameStudent ? 5 : 1, iteration: iteration);
        var gate = new RaceGate();
        using var factory = Factory(new SaveGate(gate));
        using var first = Client(factory); using var second = Client(factory);
        await LoginAsync(first, fixture.Student1.User); await LoginAsync(second, sameStudent ? fixture.Student1.User : fixture.Student2.User);
        var path = $"/student/events/{fixture.EventId}/register";
        var firstToken = await TokenAsync(first, path); var secondToken = await TokenAsync(second, path);
        FormUrlEncodedContent Book(string csrf) => Form(("Year", "3"), ("Section", "RACE-3A"), ("Terms", "true"), ("__RequestVerificationToken", csrf));
        var results = await TogetherAsync(() => first.PostAsync(path, Book(firstToken)), () => second.PostAsync(path, Book(secondToken)));
        await AssertHandledAsync(results);
        gate.AssertContended();
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Redirect);
        var failure = Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Contains("Availability changed", await failure.Content.ReadAsStringAsync());
        foreach (var response in results) response.Dispose();
        await using var verification = database.CreateContext();
        var pass = Assert.Single(await verification.EventRegistrations.AsNoTracking().ToListAsync());
        Assert.Equal("NoShow", pass.Status); Assert.Null(pass.CheckInTimestamp);
        var ev = await verification.Events.AsNoTracking().SingleAsync();
        Assert.Equal(1, ev.CurrentRegistrations); Assert.InRange(ev.CurrentRegistrations, 0, ev.MaxCapacity);
        Assert.Equal(ev.CurrentRegistrations, await verification.EventRegistrations.CountAsync(r => r.Status != "Cancelled"));
        if (sameStudent) Assert.Equal(fixture.Student1.StudentId, pass.StudentId);
        // A normal retry must show the stored pass/full state without consuming another seat.
        using var retry = await second.PostAsync(path, Book(secondToken));
        Assert.Contains(retry.StatusCode, new[] { HttpStatusCode.Redirect, HttpStatusCode.OK });
        Assert.Equal(1, await verification.EventRegistrations.CountAsync());
        Assert.Equal(1, await verification.Events.Select(e => e.CurrentRegistrations).SingleAsync());
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task Conflicting_account_updates_commit_one_complete_change(int iteration)
    {
        var fixture = await SeedAsync(5, extraAdmin: true, iteration: iteration);
        var gate = new RaceGate();
        using var factory = Factory(new SaveGate(gate));
        using var first = Client(factory); using var second = Client(factory);
        await LoginAsync(first, fixture.Admin1); await LoginAsync(second, fixture.Admin2);
        var firstInput = await AccountAsync(fixture.ExtraAdmin!.UserId); var secondInput = await AccountAsync(fixture.ExtraAdmin.UserId);
        firstInput.Email = "first-change@example.test"; firstInput.Password = "First-concurrent-password!";
        secondInput.Email = "second-change@example.test"; secondInput.Password = "Second-concurrent-password!";
        var firstToken = await TokenAsync(first, "/admin/accounts"); var secondToken = await TokenAsync(second, "/admin/accounts");
        var results = await TogetherAsync(() => first.PostAsync("/admin/accounts/save", AccountForm(firstInput, firstToken)), () => second.PostAsync("/admin/accounts/save", AccountForm(secondInput, secondToken)));
        await AssertHandledAsync(results);
        gate.AssertContended();
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Redirect);
        var failure = Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        var body = await failure.Content.ReadAsStringAsync();
        Assert.True(body.Contains("The account changed") || body.Contains("The account could not be saved"), body);
        Assert.DoesNotContain(firstInput.Password!, body); Assert.DoesNotContain(secondInput.Password!, body);
        var winningInput = results[0].StatusCode == HttpStatusCode.Redirect ? firstInput : secondInput;
        var losingInput = results[0].StatusCode == HttpStatusCode.Redirect ? secondInput : firstInput;
        await using var verification = database.CreateContext();
        var stored = await verification.Users.AsNoTracking().SingleAsync(u => u.UserId == fixture.ExtraAdmin.UserId);
        Assert.Equal(winningInput.Email, stored.Email); Assert.Equal("Admin", stored.Role); Assert.True(stored.IsActive);
        Assert.Equal(PasswordVerificationResult.Success, _hasher.VerifyHashedPassword(stored, stored.PasswordHash, winningInput.Password!));
        Assert.Equal(PasswordVerificationResult.Failed, _hasher.VerifyHashedPassword(stored, stored.PasswordHash, losingInput.Password!));
        // Reusing the losing snapshot after the race must be a handled stale edit.
        using var stale = await second.PostAsync("/admin/accounts/save", AccountForm(losingInput, secondToken));
        Assert.Equal(HttpStatusCode.OK, stale.StatusCode); Assert.Contains("The account changed", await stale.Content.ReadAsStringAsync());
        Assert.Equal(stored.PasswordHash, await verification.Users.Where(u => u.UserId == stored.UserId).Select(u => u.PasswordHash).SingleAsync());
        foreach (var response in results) response.Dispose();
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)] [InlineData(false, 3)] [InlineData(false, 4)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)] [InlineData(true, 3)] [InlineData(true, 4)]
    public async Task Admins_cannot_concurrently_remove_each_others_last_access(bool demote, int iteration)
    {
        var fixture = await SeedAsync(5, iteration: iteration);
        var gate = new RaceGate();
        // Both SERIALIZABLE transactions are open before either actor-access read begins.
        using var factory = Factory(new CommandGate(gate, command => command.CommandText.Contains("EXISTS") && command.CommandText.Contains("UserTable")));
        using var first = Client(factory); using var second = Client(factory);
        await LoginAsync(first, fixture.Admin1); await LoginAsync(second, fixture.Admin2);
        var firstInput = await AccountAsync(fixture.Admin2.UserId); var secondInput = await AccountAsync(fixture.Admin1.UserId);
        RemoveAccess(firstInput, demote); RemoveAccess(secondInput, demote);
        var firstToken = await TokenAsync(first, "/admin/accounts"); var secondToken = await TokenAsync(second, "/admin/accounts");
        var results = await TogetherAsync(() => first.PostAsync("/admin/accounts/save", AccountForm(firstInput, firstToken)), () => second.PostAsync("/admin/accounts/save", AccountForm(secondInput, secondToken)));
        await AssertHandledAsync(results);
        gate.AssertContended();
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.Redirect);
        Assert.Single(results, r => r.StatusCode == HttpStatusCode.OK);
        await using var verification = database.CreateContext();
        var survivor = Assert.Single(await verification.Users.AsNoTracking().Where(u => u.IsActive && u.Role == "Admin").ToListAsync());
        var removed = await verification.Users.AsNoTracking().SingleAsync(u => u.UserId == (survivor.UserId == fixture.Admin1.UserId ? fixture.Admin2.UserId : fixture.Admin1.UserId));
        Assert.True(demote ? removed.Role == "Student" && removed.IsActive : removed.Role == "Admin" && !removed.IsActive);
        Assert.Equal(demote ? 3 : 2, await verification.Students.CountAsync());
        foreach (var response in results) response.Dispose();

        // With exactly one active admin left, neither self-deactivation nor self-demotion may remove access.
        var survivorClient = survivor.UserId == fixture.Admin1.UserId ? first : second;
        foreach (var selfDemote in new[] { false, true })
        {
            var input = await AccountAsync(survivor.UserId); RemoveAccess(input, selfDemote);
            var csrf = await TokenAsync(survivorClient, "/admin/accounts");
            using var rejected = await survivorClient.PostAsync("/admin/accounts/save", AccountForm(input, csrf));
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            Assert.Contains("Your account must remain an active administrator", await rejected.Content.ReadAsStringAsync());
            Assert.Equal(1, await verification.Users.CountAsync(u => u.IsActive && u.Role == "Admin"));
        }
        // The removed administrator's pre-existing cookie no longer grants access.
        var removedClient = survivor.UserId == fixture.Admin1.UserId ? second : first;
        using var denied = await removedClient.GetAsync("/admin/accounts");
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        var destination = new Uri(removedClient.BaseAddress!, denied.Headers.Location!);
        Assert.Equal("/login", destination.AbsolutePath);
        Assert.Contains("ReturnUrl=", destination.Query);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task Cancelling_a_pass_while_another_student_books_preserves_seat_counts(int iteration)
    {
        var fixture = await SeedAsync(2, reserved: true, iteration: iteration);
        var gate = new RaceGate(); using var factory = Factory(new SaveGate(gate));
        using var owner = Client(factory); using var other = Client(factory);
        await LoginAsync(owner, fixture.Student1.User); await LoginAsync(other, fixture.Student2.User);
        var cancelPath = $"/student/passes/{fixture.PassId}/cancel"; var bookPath = $"/student/events/{fixture.EventId}/register";
        var ownerToken = await TokenAsync(owner, "/student"); var otherToken = await TokenAsync(other, bookPath);
        var results = await TogetherAsync(() => owner.PostAsync(cancelPath, Form(("__RequestVerificationToken", ownerToken))),
            () => other.PostAsync(bookPath, Form(("Year", "3"), ("Section", "RACE-3B"), ("Terms", "true"), ("__RequestVerificationToken", otherToken))));
        await AssertHandledAsync(results); gate.AssertContended();
        await using var verify = database.CreateContext();
        var original = await verify.EventRegistrations.AsNoTracking().SingleAsync(r => r.EventRegistrationId == fixture.PassId);
        var newPass = await verify.EventRegistrations.AsNoTracking().SingleOrDefaultAsync(r => r.StudentId == fixture.Student2.StudentId);
        Assert.True(original.Status == "Cancelled" && newPass is null || original.Status == "NoShow" && newPass?.Status == "NoShow");
        await AssertSeatsAsync(verify, fixture.EventId);
        // Retry only the rolled-back operation with current state; both actions should then be represented.
        using var retry = original.Status == "Cancelled"
            ? await other.PostAsync(bookPath, Form(("Year", "3"), ("Section", "RACE-3B"), ("Terms", "true"), ("__RequestVerificationToken", otherToken)))
            : await owner.PostAsync(cancelPath, Form(("__RequestVerificationToken", ownerToken)));
        Assert.Equal(HttpStatusCode.Redirect, retry.StatusCode);
        Assert.Equal("Cancelled", await verify.EventRegistrations.Where(r => r.EventRegistrationId == fixture.PassId).Select(r => r.Status).SingleAsync());
        Assert.Equal(1, await verify.EventRegistrations.CountAsync(r => r.Status == "NoShow" && r.StudentId == fixture.Student2.StudentId));
        Assert.Equal(1, await verify.Events.Select(e => e.CurrentRegistrations).SingleAsync());
        await AssertSeatsAsync(verify, fixture.EventId);
        foreach (var response in results) response.Dispose();
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)] [InlineData(false, 3)] [InlineData(false, 4)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)] [InlineData(true, 3)] [InlineData(true, 4)]
    public async Task Pass_cancellation_racing_checkin_commits_one_consistent_outcome(bool administratorVoid, int iteration)
    {
        var fixture = await SeedAsync(2, reserved: true, iteration: iteration);
        var gate = new RaceGate(); using var factory = Factory(new CommandGate(gate,
            command => command.CommandText.Contains("UPDATE") && command.CommandText.Contains("EventRegistrationTable")));
        using var operatorClient = Client(factory); using var cancelling = Client(factory);
        await LoginAsync(operatorClient, fixture.Admin1); await LoginAsync(cancelling, administratorVoid ? fixture.Admin2 : fixture.Student1.User);
        var scan = $"/admin/events/{fixture.EventId}/scanner";
        var scanToken = await TokenAsync(operatorClient, scan);
        var cancelToken = await TokenAsync(cancelling, administratorVoid ? $"/admin/events/{fixture.EventId}/preregistered" : "/student");
        await using var before = database.CreateContext();
        var original = await before.EventRegistrations.AsNoTracking().SingleAsync(); var ev = await before.Events.AsNoTracking().SingleAsync();
        FormUrlEncodedContent Tokens(string csrf) => Form(("RegistrationId", fixture.PassId.ToString()), ("Version", Convert.ToBase64String(original.RowVersion)),
            ("EventVersion", Convert.ToBase64String(ev.RowVersion)), ("Confirmed", "true"), ("__RequestVerificationToken", csrf));
        var cancelPath = administratorVoid ? $"/admin/events/{fixture.EventId}/preregistered/{fixture.PassId}/void" : $"/student/passes/{fixture.PassId}/cancel";
        var results = await TogetherAsync(() => cancelling.PostAsync(cancelPath, Tokens(cancelToken)), () => operatorClient.PostAsync(scan + "/confirm", Tokens(scanToken)));
        await AssertHandledAsync(results, allowUnavailable: true); gate.AssertContended();
        await using var verify = database.CreateContext();
        var stored = await verify.EventRegistrations.AsNoTracking().SingleAsync();
        Assert.Contains(stored.Status, new[] { "Present", "Cancelled" });
        Assert.Equal(stored.Status == "Present", stored.CheckInTimestamp.HasValue);
        await AssertSeatsAsync(verify, fixture.EventId);
        var version = stored.RowVersion.ToArray(); var timestamp = stored.CheckInTimestamp;
        using var repeat = await operatorClient.PostAsync(scan + "/confirm", Tokens(scanToken));
        Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
        Assert.Contains(stored.Status == "Present" ? "DuplicateWarning" : "CancelledWarning", await repeat.Content.ReadAsStringAsync());
        stored = await verify.EventRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal(version, stored.RowVersion); Assert.Equal(timestamp, stored.CheckInTimestamp);
        foreach (var response in results) response.Dispose();
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(false, 1)] [InlineData(false, 2)] [InlineData(false, 3)] [InlineData(false, 4)]
    [InlineData(true, 0)] [InlineData(true, 1)] [InlineData(true, 2)] [InlineData(true, 3)] [InlineData(true, 4)]
    public async Task Event_cancellation_racing_booking_or_checkin_blocks_future_admission(bool checkIn, int iteration)
    {
        var fixture = await SeedAsync(2, reserved: checkIn, iteration: iteration);
        var gate = new RaceGate();
        using var factory = Factory(checkIn
            ? new CommandGate(gate, command => command.CommandText.Contains("UPDATE") && (command.CommandText.Contains("EventRegistrationTable") || command.CommandText.Contains("EventsTable")))
            : new SaveGate(gate));
        using var admin = Client(factory); using var other = Client(factory);
        await LoginAsync(admin, fixture.Admin1); await LoginAsync(other, checkIn ? fixture.Admin2 : fixture.Student1.User);
        var eventPath = $"/admin/events/{fixture.EventId}"; var actionPath = checkIn ? eventPath + "/scanner" : $"/student/events/{fixture.EventId}/register";
        var adminToken = await TokenAsync(admin, eventPath); var otherToken = await TokenAsync(other, actionPath);
        await using var before = database.CreateContext(); var ev = await before.Events.AsNoTracking().SingleAsync();
        var pass = checkIn ? await before.EventRegistrations.AsNoTracking().SingleAsync() : null;
        FormUrlEncodedContent Cancel(byte[] version) => Form(("reason", "Isolated cancellation conflict"), ("version", Convert.ToBase64String(version)), ("confirmed", "true"), ("__RequestVerificationToken", adminToken));
        var results = await TogetherAsync(() => admin.PostAsync(eventPath + "/cancel", Cancel(ev.RowVersion)), () => checkIn
            ? other.PostAsync(actionPath + "/confirm", Form(("RegistrationId", fixture.PassId.ToString()), ("Version", Convert.ToBase64String(pass!.RowVersion)), ("EventVersion", Convert.ToBase64String(ev.RowVersion)), ("Confirmed", "true"), ("__RequestVerificationToken", otherToken)))
            : other.PostAsync(actionPath, Form(("Year", "3"), ("Section", "RACE-3A"), ("Terms", "true"), ("__RequestVerificationToken", otherToken))));
        await AssertHandledAsync(results, allowUnavailable: true); gate.AssertContended();
        await using var verify = database.CreateContext(); var storedEvent = await verify.Events.AsNoTracking().SingleAsync();
        if (storedEvent.Status != "Cancelled")
        {
            // A booking may win the event rowversion. Cancel again only after reviewing the fresh version.
            Assert.False(checkIn);
            using var retry = await admin.PostAsync(eventPath + "/cancel", Cancel(storedEvent.RowVersion));
            Assert.Equal(HttpStatusCode.Redirect, retry.StatusCode);
        }
        storedEvent = await verify.Events.AsNoTracking().SingleAsync();
        Assert.Equal("Cancelled", storedEvent.Status); Assert.Equal("Isolated cancellation conflict", storedEvent.CancellationReason);
        await AssertSeatsAsync(verify, fixture.EventId);
        var storedPass = await verify.EventRegistrations.AsNoTracking().SingleOrDefaultAsync();
        if (checkIn)
        {
            Assert.NotNull(storedPass); Assert.Contains(storedPass.Status, new[] { "NoShow", "Present" });
            Assert.Equal(storedPass.Status == "Present", storedPass.CheckInTimestamp.HasValue);
        }
        // A fresh scan after the cancellation must never admit even a previously valid reservation.
        var scanToken = await TokenAsync(admin, eventPath + "/scanner");
        using var lookup = await admin.PostAsync(eventPath + "/scanner/lookup", Form(("query", fixture.Student1.StudentId), ("__RequestVerificationToken", scanToken)));
        var lookupJson = await lookup.Content.ReadAsStringAsync();
        Assert.DoesNotContain("\"canConfirm\":true", lookupJson);
        if (storedPass is not null) Assert.Contains("CancelledWarning", lookupJson);
        foreach (var response in results) response.Dispose();
    }

    private static async Task AssertSeatsAsync(ApplicationDbContext db, int eventId)
    {
        var seats = await db.Events.Where(e => e.EventId == eventId).Select(e => e.CurrentRegistrations).SingleAsync();
        Assert.Equal(await db.EventRegistrations.CountAsync(r => r.EventId == eventId && r.Status != "Cancelled"), seats);
    }

    private static void RemoveAccess(ManagedAccountInput input, bool demote)
    {
        if (!demote) { input.Active = false; return; }
        input.Role = "Student"; input.StudentId = $"RACE-ADMIN-{input.Id}";
        input.FirstName = "Althea"; input.LastName = "Navarro"; input.Gender = "Female";
        input.Campus = "San Bartolome"; input.College = "College of Computer Studies"; input.Program = "BSIT";
    }

    private WebApplicationFactory<Program> Factory(IInterceptor interceptor) => new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
    {
        web.UseEnvironment("Development");
        web.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:EventsDatabase"] = database.ConnectionString }));
        web.ConfigureServices(services =>
        {
            services.AddScoped(_ => database.CreateContext(interceptor));
            services.AddSingleton(_protection);
        });
    });

    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new("https://localhost") });
    private async Task LoginAsync(HttpClient client, User user)
    {
        var csrf = await TokenAsync(client, "/login");
        using var response = await client.PostAsync("/login", Form(("Identifier", user.Email), ("Password", _password), ("__RequestVerificationToken", csrf)));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private static async Task<string> TokenAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"Missing antiforgery on {path}");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private static FormUrlEncodedContent Form(params (string Name, string Value)[] values) => new(values.Select(v => new KeyValuePair<string, string>(v.Name, v.Value)));
    private static FormUrlEncodedContent AccountForm(ManagedAccountInput input, string csrf) => Form(
        ("Editor.Id", input.Id.ToString()), ("Editor.Email", input.Email), ("Editor.Role", input.Role), ("Editor.Active", input.Active.ToString()),
        ("Editor.Version", input.Version!), ("Editor.Password", input.Password ?? ""), ("Editor.StudentId", input.StudentId ?? ""),
        ("Editor.FirstName", input.FirstName ?? ""), ("Editor.LastName", input.LastName ?? ""), ("Editor.Gender", input.Gender ?? ""),
        ("Editor.Campus", input.Campus ?? ""), ("Editor.College", input.College ?? ""), ("Editor.Program", input.Program ?? ""), ("__RequestVerificationToken", csrf));

    private static async Task<HttpResponseMessage[]> TogetherAsync(Func<Task<HttpResponseMessage>> first, Func<Task<HttpResponseMessage>> second) =>
        await Task.WhenAll(Task.Run(first), Task.Run(second)).WaitAsync(TimeSpan.FromSeconds(45));

    private static async Task AssertHandledAsync(IEnumerable<HttpResponseMessage> results, bool allowUnavailable = false)
    {
        foreach (var result in results)
            if ((int)result.StatusCode >= 400 && !(allowUnavailable && result.StatusCode == HttpStatusCode.ServiceUnavailable))
                Assert.Fail($"Unexpected HTTP {(int)result.StatusCode}: {string.Join('\n', (await result.Content.ReadAsStringAsync()).Split('\n').Take(6))}");
    }

    private async Task<ManagedAccountInput> AccountAsync(int id)
    {
        await using var db = database.CreateContext();
        return await Accounts(db).GetAsync(id, default);
    }
    private AccountManagementService Accounts(ApplicationDbContext db)
    {
        // GetAsync and SaveAsync must use the same provider as the HTTP application.
        return new(db, new StudentService(db, _hasher, _protection, new EventClock(TimeProvider.System)), _hasher, _protection);
    }
    private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();

    private async Task<Scenario> SeedAsync(int capacity, bool reserved = false, bool extraAdmin = false, int iteration = 0)
    {
        await database.ResetAsync();
        await using var db = database.CreateContext();
        User Account(string email, string role)
        {
            var user = new User { Email = email, Role = role, PasswordHash = "", PasswordSalt = "" };
            user.PasswordHash = _hasher.HashPassword(user, _password); return user;
        }
        Student Student(string id, string email) => new() { StudentId = id, User = Account(email, "Student"), FirstName = "Mikaela", LastName = "Dizon",
            Gender = "Female", CampusBranch = "San Bartolome", Department = "College of Computer Studies", Program = "BSIT" };
        var admin1 = Account("race-admin1@example.test", "Admin"); var admin2 = Account("race-admin2@example.test", "Admin");
        var third = extraAdmin ? Account("race-target@example.test", "Admin") : null;
        var student1 = Student("RACE-STUDENT-1", "race-student1@example.test"); var student2 = Student("RACE-STUDENT-2", "race-student2@example.test");
        var now = new EventClock(TimeProvider.System).Now;
        var ev = new Event { Title = $"Isolated concurrency verification {iteration}", VenueLocation = "Test Hall", MaxCapacity = capacity, CreatedByUser = admin1,
            EventStart = now.Date.AddDays(2).AddHours(9), EventEnd = now.Date.AddDays(2).AddHours(11),
            RegStart = DateOnly.FromDateTime(now.AddDays(-1)), RegEnd = DateOnly.FromDateTime(now.AddDays(1)) };
        db.Users.Add(admin2); if (third is not null) db.Users.Add(third);
        db.Students.AddRange(student1, student2); db.Events.Add(ev);
        EventRegistration? pass = null;
        if (reserved) { ev.CurrentRegistrations = 1; pass = new() { Event = ev, Student = student1, StudentId = student1.StudentId, CurrentYearLvl = 3, CurrentSection = "RACE-3A" }; db.EventRegistrations.Add(pass); }
        await db.SaveChangesAsync();
        return new(admin1, admin2, third, student1, student2, ev.EventId, pass?.EventRegistrationId ?? 0);
    }

    private sealed record Scenario(User Admin1, User Admin2, User? ExtraAdmin, Student Student1, Student Student2, int EventId, int PassId);
    private sealed class RaceGate
    {
        private int _arrivals;
        private readonly ConcurrentDictionary<Guid, byte> _connections = new();
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task WaitAsync(DbConnection connection, CancellationToken ct)
        {
            var arrival = Interlocked.Increment(ref _arrivals);
            if (arrival > 2) return;
            var sql = Assert.IsType<SqlConnection>(connection);
            Assert.NotEqual(Guid.Empty, sql.ClientConnectionId);
            _connections.TryAdd(sql.ClientConnectionId, 0);
            if (arrival == 2) _ready.TrySetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        }
        public void AssertContended()
        {
            Assert.True(Volatile.Read(ref _arrivals) >= 2, "Both independent requests must reach the synchronized SQL/save boundary.");
            Assert.Equal(2, _connections.Count);
        }
    }
    private sealed class SaveGate(RaceGate gate) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        {
            await eventData.Context!.Database.OpenConnectionAsync(ct);
            await gate.WaitAsync(eventData.Context.Database.GetDbConnection(), ct);
            return result;
        }
    }
    private sealed class CommandGate(RaceGate gate, Func<DbCommand, bool> matches) : DbCommandInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
        { if (matches(command)) await gate.WaitAsync(command.Connection!, ct); return result; }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        { if (matches(command)) await gate.WaitAsync(command.Connection!, ct); return result; }
    }
}
