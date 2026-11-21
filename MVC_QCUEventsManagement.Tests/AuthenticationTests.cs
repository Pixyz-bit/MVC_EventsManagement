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
            web.ConfigureServices(services => services.AddScoped(_ => CreateContext())));

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
                RegStart = now.AddDays(-1), RegEnd = now.AddDays(1)
            });
            await db.SaveChangesAsync();
        }

        using var client = NewClient();
        using var login = await SignInAsync(client, _adminEmail, _password);
        var query = Uri.EscapeDataString(title);
        var results = await client.GetStringAsync($"/admin/events?q={query}&status=Open");
        Assert.Contains(title, results);
        Assert.Contains("Main Campus Hall", results);
        Assert.Contains("0 of 30 registered", results);

        var empty = await client.GetStringAsync($"/admin/events?q={query}&status=Closed");
        Assert.Contains("No matching events", empty);
        Assert.Contains("Clear filters", empty);
        Assert.DoesNotContain("0 of 30 registered", empty);
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
