using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MVC_QCUEventsManagement.Models;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Tests;

public sealed class WorkflowTests(IsolatedSqlServerFixture database) : IClassFixture<IsolatedSqlServerFixture>
{
    [Fact]
    public async Task Created_event_flows_through_discovery_booking_pass_checkin_history_and_reports()
    {
        await database.ResetAsync();
        var clock = new WorkflowClock();
        var password = Guid.NewGuid().ToString("N"); var hasher = new PasswordHasher<User>();
        User Account(string email, string role)
        {
            var account = new User { Email = email, Role = role, PasswordHash = "", PasswordSalt = "" };
            account.PasswordHash = hasher.HashPassword(account, password); return account;
        }
        var admin = Account("workflow-admin@example.test", "Admin");
        var student = new Student { StudentId = "WORKFLOW-2031-0001", User = Account("workflow-student@example.test", "Student"),
            FirstName = "Mikaela", LastName = "Dizon", Gender = "Female", CampusBranch = "San Bartolome",
            Department = "College of Computer Studies", Program = "BSIT" };
        await using (var seed = database.CreateContext()) { seed.Users.Add(admin); seed.Students.Add(student); await seed.SaveChangesAsync(); }
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(web =>
        {
            web.UseEnvironment("Development");
            web.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:EventsDatabase"] = database.ConnectionString }));
            web.ConfigureServices(services =>
            {
                services.AddScoped(_ => database.CreateContext()); services.AddSingleton<TimeProvider>(clock);
                services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            });
        });
        using var administrator = Client(factory); using var learner = Client(factory); using var anonymous = Client(factory);
        await LoginAsync(administrator, admin.Email, password); await LoginAsync(learner, student.StudentId, password);
        const string title = "Workflow verification research forum";
        using var created = await administrator.PostAsync("/admin/events/create", Form(
            ("Title", title), ("VenueLocation", "Research Hall"), ("MaxCapacity", "2"), ("EventDate", "2031-01-07"),
            ("StartTime", "09:00"), ("EndTime", "11:00"), ("RegStart", "2031-01-05"), ("RegEnd", "2031-01-06"),
            ("TargetBranch", "San Bartolome"), ("TargetDepartment", "College of Computer Studies"), ("Programs", "BSIT"),
            ("TargetYearLevel", "3"), ("Sponsors[0]", "QCU Alumni Association"), ("Confirm", "true"),
            ("__RequestVerificationToken", await TokenAsync(administrator, "/admin/events/create"))));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        await using var verify = database.CreateContext();
        var ev = await verify.Events.AsNoTracking().Include(e => e.Sponsors).SingleAsync();
        Assert.Equal(admin.UserId, ev.CreatedByUserId); Assert.Single(ev.Sponsors); Assert.Equal(0, ev.CurrentRegistrations);
        Assert.Contains(title, await learner.GetStringAsync("/student"));
        var registration = $"/student/events/{ev.EventId}/register";
        using var booked = await learner.PostAsync(registration, Form(("Year", "3"), ("Section", "WORKFLOW-3A"), ("Terms", "true"),
            ("__RequestVerificationToken", await TokenAsync(learner, registration))));
        Assert.Equal(HttpStatusCode.Redirect, booked.StatusCode);
        var passPath = booked.Headers.Location!.OriginalString;
        var stored = await verify.EventRegistrations.AsNoTracking().SingleAsync();
        var reference = $"TCK-{ev.EventId:D4}-{stored.EventRegistrationId:D5}";
        var passHtml = await learner.GetStringAsync(passPath);
        Assert.Contains($"data-reference=\"{reference}\"", passHtml); Assert.Contains("btnDownloadPass", passHtml);
        Assert.Contains("student-pass.js", passHtml); Assert.Contains("WORKFLOW-3A", passHtml);
        using var denied = await anonymous.GetAsync(passPath); Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Equal(1, await verify.Events.Select(e => e.CurrentRegistrations).SingleAsync());
        var scan = $"/admin/events/{ev.EventId}/scanner"; var csrf = await TokenAsync(administrator, scan);
        using var lookup = await administrator.PostAsync(scan + "/lookup", Form(("query", reference), ("__RequestVerificationToken", csrf)));
        using var json = JsonDocument.Parse(await lookup.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("canConfirm").GetBoolean());
        var attendee = json.RootElement.GetProperty("attendee");
        Assert.Equal("NoShow", await verify.EventRegistrations.Select(r => r.Status).SingleAsync()); // Lookup is read-only.
        using var confirmed = await administrator.PostAsync(scan + "/confirm", Form(("RegistrationId", stored.EventRegistrationId.ToString()),
            ("Version", attendee.GetProperty("version").GetString()!), ("EventVersion", attendee.GetProperty("eventVersion").GetString()!),
            ("Confirmed", "true"), ("__RequestVerificationToken", csrf)));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode); Assert.Contains("CheckedIn", await confirmed.Content.ReadAsStringAsync());
        stored = await verify.EventRegistrations.AsNoTracking().SingleAsync(); Assert.Equal("Present", stored.Status); Assert.NotNull(stored.CheckInTimestamp);
        Assert.Contains(reference, await administrator.GetStringAsync($"/admin/events/{ev.EventId}/attendance"));
        Assert.Contains(reference, await administrator.GetStringAsync($"/admin/events/{ev.EventId}/preregistered"));
        var consumedPass = await learner.GetStringAsync(passPath);
        Assert.Contains("Checked in", consumedPass); Assert.DoesNotContain("id=\"qrCanvas\"", consumedPass);
        await AssertReportAsync(factory, ev.EventId, reserved: 1, attended: 1, cancelled: 0);
        Assert.Contains(title, await administrator.GetStringAsync($"/admin/events/{ev.EventId}/analytics"));

        // Advance only the injected clock to EventEnd; history is derived without editing stored event status.
        clock.UtcNow = new DateTimeOffset(2031, 1, 7, 3, 0, 0, TimeSpan.Zero);
        await LoginAsync(administrator, admin.Email, password);
        var history = await administrator.GetStringAsync("/admin/events/history"); Assert.Contains(title, history);
        var historical = await administrator.GetStringAsync($"/admin/events/{ev.EventId}?from=history");
        Assert.Contains("Completed", historical); Assert.DoesNotContain("id=\"open-cancel-event\"", historical);
        Assert.Equal("Upcoming", await verify.Events.Select(e => e.Status).SingleAsync());
        await AssertReportAsync(factory, ev.EventId, 1, 1, 0);
        Assert.Equal(stored.CheckInTimestamp, await verify.EventRegistrations.Select(r => r.CheckInTimestamp).SingleAsync());

        // Admin voiding a historical Present pass is deliberately broader than student cancellation.
        ev = await verify.Events.AsNoTracking().SingleAsync();
        var roster = $"/admin/events/{ev.EventId}/preregistered";
        using var cancelled = await administrator.PostAsync(roster + $"/{stored.EventRegistrationId}/void", Form(
            ("Version", Convert.ToBase64String(stored.RowVersion)), ("EventVersion", Convert.ToBase64String(ev.RowVersion)), ("Confirmed", "true"),
            ("__RequestVerificationToken", await TokenAsync(administrator, roster))));
        Assert.Equal(HttpStatusCode.Redirect, cancelled.StatusCode);
        Assert.DoesNotContain(reference, await administrator.GetStringAsync($"/admin/events/{ev.EventId}/attendance"));
        Assert.Contains(reference, await administrator.GetStringAsync(roster + "?cohort=Cancelled"));
        await AssertReportAsync(factory, ev.EventId, 0, 0, 1);
        var retained = await verify.EventRegistrations.AsNoTracking().SingleAsync();
        Assert.Equal("Cancelled", retained.Status); Assert.Equal(stored.CheckInTimestamp, retained.CheckInTimestamp);
        Assert.Equal(0, await verify.Events.Select(e => e.CurrentRegistrations).SingleAsync());
    }

    private static async Task AssertReportAsync(WebApplicationFactory<Program> factory, int id, int reserved, int attended, int cancelled)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var report = await scope.ServiceProvider.GetRequiredService<AnalyticsService>().GetAsync(id, new EventAnalyticsQuery(), default);
        Assert.NotNull(report); Assert.Equal(reserved, report.Reserved); Assert.Equal(attended, report.Attended); Assert.Equal(cancelled, report.Cancelled);
        Assert.Equal(reserved, report.Event.Registered); Assert.Equal(reserved - attended, report.Unscanned);
        Assert.Equal(reserved == 0 ? 0d : 100d * attended / reserved, report.Turnout);
    }
    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(new() { AllowAutoRedirect = false, BaseAddress = new("https://localhost") });
    private static async Task LoginAsync(HttpClient client, string identifier, string password)
    {
        using var response = await client.PostAsync("/login", Form(("Identifier", identifier), ("Password", password), ("__RequestVerificationToken", await TokenAsync(client, "/login"))));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
    private static async Task<string> TokenAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var match = Regex.Match(await response.Content.ReadAsStringAsync(), "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, path); return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
    private static FormUrlEncodedContent Form(params (string Name, string Value)[] values) => new(values.Select(v => new KeyValuePair<string, string>(v.Name, v.Value)));
    private sealed class WorkflowClock : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2031, 1, 5, 1, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
