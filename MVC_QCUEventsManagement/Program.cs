using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.Models;
using System.Threading.RateLimiting;

var createAdmin = args.Length > 0 && args[0] == "--create-admin";
var seedDemo = args.Length == 1 && args[0] == "--seed-demo";
var builder = WebApplication.CreateBuilder(createAdmin || seedDemo ? [] : args);

// Add services to the container.
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
var securePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = securePolicy);
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/access-denied";
    options.Cookie.Name = "QcuEvents.Auth";
    options.Cookie.SecurePolicy = securePolicy;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
    options.EventsType = typeof(AccountCookieEvents);
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 15,
            Window = TimeSpan.FromMinutes(1)
        }));
});

var connectionString = builder.Configuration.GetConnectionString("EventsDatabase")
    ?? throw new InvalidOperationException("Configure ConnectionStrings:EventsDatabase before starting the application.");
var databaseConnection = new SqlConnectionStringBuilder(connectionString);
if (!string.Equals(databaseConnection.InitialCatalog, "MVC_EventsManagement", StringComparison.Ordinal))
{
    throw new InvalidOperationException("EventsDatabase must target the dedicated MVC_EventsManagement database.");
}

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<EventClock>();
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<AccountCookieEvents>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<DevelopmentDemoData>();
builder.Services.AddSingleton<EventBannerStore>();

await using var app = builder.Build();

if (seedDemo)
{
    if (!app.Environment.IsDevelopment()) throw new InvalidOperationException("Sample data is available only in Development.");
    await using var scope = app.Services.CreateAsyncScope();
    var password = Environment.GetEnvironmentVariable("MVC_DEMO_PASSWORD") ?? "Preview!" + Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(12));
    var result = await scope.ServiceProvider.GetRequiredService<DevelopmentDemoData>().CreateAsync(password);
    Console.WriteLine(result.Created ? "Created 3 fictional events, 32 student accounts and 1 preview administrator." : "Demo data already exists; no records or passwords were changed.");
    Console.WriteLine($"Events: {result.LiveEventId}, {result.CompletedEventId}, {result.CancelledEventId}");
    if (result.Created) Console.WriteLine($"Admin: {DevelopmentDemoData.AdminEmail}\nStudent: demo.student01@example.test\nPassword for new demo accounts: {password}");
    return;
}

if (createAdmin)
{
    await using var scope = app.Services.CreateAsyncScope();
    Environment.ExitCode = await FirstAdminCommand.RunAsync(args, scope.ServiceProvider.GetRequiredService<AccountService>());
    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


await app.RunAsync();

public partial class Program;
