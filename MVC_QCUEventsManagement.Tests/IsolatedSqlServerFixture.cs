using System.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MVC_QCUEventsManagement.Data;

namespace MVC_QCUEventsManagement.Tests;

// Never uses MVC_EVENTS_TEST_CONNECTION or the developer's existing LocalDB/default instance.
public sealed class IsolatedSqlServerFixture : IAsyncLifetime
{
    private readonly string _instance = "QcuConcurrency_" + Guid.NewGuid().ToString("N");
    private string _directory = "";
    private bool _created;
    public string ConnectionString { get; private set; } = "";

    public async Task InitializeAsync()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Concurrency tests require SQL Server LocalDB on Windows.");
        try
        {
            await LocalDbAsync("create", _instance);
            _created = true;
            await LocalDbAsync("start", _instance);
            _directory = Path.Combine(AppContext.BaseDirectory, "Concurrency", _instance);
            Directory.CreateDirectory(_directory);
            ConnectionString = new SqlConnectionStringBuilder
            {
                DataSource = @"(localdb)\" + _instance, InitialCatalog = "MVC_EventsManagement",
                IntegratedSecurity = true, Encrypt = true, TrustServerCertificate = true, ConnectTimeout = 30
            }.ConnectionString;
            await using (var master = new SqlConnection(MasterConnection()))
            {
                await master.OpenAsync();
                await using var command = master.CreateCommand();
                static string SqlPath(string path) => path.Replace("'", "''");
                command.CommandText = $"CREATE DATABASE [MVC_EventsManagement] ON PRIMARY (NAME=N'MVC_EventsManagement', FILENAME=N'{SqlPath(Path.Combine(_directory, "events.mdf"))}') LOG ON (NAME=N'MVC_EventsManagement_log', FILENAME=N'{SqlPath(Path.Combine(_directory, "events.ldf"))}')";
                await command.ExecuteNonQueryAsync();
            }
            await using var db = CreateContext();
            await db.Database.MigrateAsync();
            await db.Database.MigrateAsync(); // Fresh setup and migration repeatability, without EnsureCreated.
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.False(await db.Users.AnyAsync());
        }
        catch { await DisposeAsync(); throw; }
    }

    public ApplicationDbContext CreateContext(params IInterceptor[] interceptors)
    {
        ValidateOwnedConnection();
        return new(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(ConnectionString)
            .AddInterceptors(interceptors).Options);
    }

    public async Task ResetAsync()
    {
        await using var db = CreateContext();
        await db.Sponsors.ExecuteDeleteAsync();
        await db.EventRegistrations.ExecuteDeleteAsync();
        await db.Events.ExecuteDeleteAsync();
        await db.Students.ExecuteDeleteAsync();
        await db.Users.ExecuteDeleteAsync();
    }

    private void ValidateOwnedConnection()
    {
        var connection = new SqlConnectionStringBuilder(ConnectionString);
        if (!_created || !_instance.StartsWith("QcuConcurrency_", StringComparison.Ordinal) ||
            connection.DataSource != @"(localdb)\" + _instance || connection.InitialCatalog != "MVC_EventsManagement")
            throw new InvalidOperationException("Only this fixture's newly created SQL instance is allowed.");
    }

    private string MasterConnection() => new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master", Pooling = false }.ConnectionString;

    public async Task DisposeAsync()
    {
        if (!_created) return;
        try
        {
            if (ConnectionString.Length > 0)
            {
                ValidateOwnedConnection();
                await using var master = new SqlConnection(MasterConnection());
                await master.OpenAsync();
                await using var command = master.CreateCommand();
                // This exact database exists only on the unique instance created above.
                command.CommandText = "IF DB_ID(N'MVC_EventsManagement') IS NOT NULL BEGIN ALTER DATABASE [MVC_EventsManagement] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [MVC_EventsManagement]; END";
                await command.ExecuteNonQueryAsync();
            }
        }
        finally
        {
            await LocalDbAsync("stop", _instance, "-k");
            await LocalDbAsync("delete", _instance);
            _created = false;
            if (_directory.Length > 0 && Directory.Exists(_directory) && !Directory.EnumerateFileSystemEntries(_directory).Any())
                Directory.Delete(_directory); // Non-recursive; SQL Server owns deletion of its disposable database files.
        }
    }

    private static async Task LocalDbAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo("SqlLocalDB.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start SQL Server LocalDB tooling.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(45));
        if (process.ExitCode != 0) throw new InvalidOperationException($"LocalDB {arguments[0]} failed: {await output} {await error}");
    }
}
