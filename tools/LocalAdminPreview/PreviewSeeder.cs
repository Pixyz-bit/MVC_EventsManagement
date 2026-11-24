using System.Data;
using System.Text.Json.Nodes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.Models;

namespace LocalAdminPreview;

internal static class PreviewSeeder
{
    public static async Task<string> ExistingFingerprintAsync(string app, PreviewDataset data)
    {
        await using var connection = new SqlConnection(Connection(app)); await connection.OpenAsync();
        var signatures = new List<string>();
        foreach (var (table, key, condition) in new[]
        {
            ("UserTable", "UserId", "Email NOT LIKE @emails"),
            ("StudentTable", "StudentId", "StudentId NOT LIKE @students"),
            ("EventsTable", "EventId", "Description IS NULL OR Description NOT LIKE @marker"),
            ("SponsorListTable", "SponsorEntryId", "EventId NOT IN (SELECT EventId FROM dbo.EventsTable WHERE Description LIKE @marker)"),
            ("EventRegistrationTable", "EventRegistrationId", "EventId NOT IN (SELECT EventId FROM dbo.EventsTable WHERE Description LIKE @marker)")
        })
        {
            // Only schema-owned constants form SQL identifiers; every value is parameterized.
            var signature = await Scalar(connection, null,
                $"SELECT CONVERT(varchar(64),HASHBYTES('SHA2_256',(SELECT * FROM dbo.{table} WHERE {condition} ORDER BY {key} FOR JSON PATH,INCLUDE_NULL_VALUES)),2)",
                ("emails", data.Admin.Email[..data.Admin.Email.LastIndexOf("admin@", StringComparison.Ordinal)] + "%"),
                ("students", data.StudentPrefix + "%"), ("marker", data.Marker.Replace("[", "[[]") + "%"));
            signatures.Add(table + ":" + signature);
        }
        return string.Join('|', signatures);
    }

    public static string Connection(string app) => app switch
    {
        "mvc" => "Server=localhost;Database=MVC_EventsManagement;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;",
        "webforms" => "Server=localhost;Database=UniversityEventDB;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;",
        _ => throw new ArgumentException("Unknown local application.")
    };

    public static async Task SeedAsync(string app, PreviewDataset data)
    {
        if (app == "mvc")
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(Connection(app)).Options);
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            if (await db.Users.AnyAsync(u => u.Email == data.Admin.Email))
            {
                await ValidateExistingAsync(app, data);
                return;
            }
            if (await db.Students.AnyAsync(s => s.StudentId.StartsWith(data.StudentPrefix))) throw new InvalidOperationException("Preview namespace collision; no rows changed.");
            var accountPrefix = data.Admin.Email[..data.Admin.Email.LastIndexOf("admin@", StringComparison.Ordinal)];
            if (await db.Users.AnyAsync(u => u.Email.StartsWith(accountPrefix))) throw new InvalidOperationException("Preview account namespace collision; no rows changed.");
            db.Users.Add(data.InactiveAdmin);
            // Student 35 and 36 are registered in historical/ongoing fixtures too; persist every profile explicitly.
            db.Students.AddRange(data.Students);
            db.Events.AddRange(data.Events);
            await db.SaveChangesAsync();
            await ValidateRowsAsync((SqlConnection)db.Database.GetDbConnection(), (SqlTransaction)tx.GetDbTransaction(), data);
            await tx.CommitAsync();
        }
        else
        {
            await using var connection = new SqlConnection(Connection(app)); await connection.OpenAsync();
            await using var tx = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
            if (Convert.ToInt32(await Scalar(connection, tx, "SELECT COUNT(*) FROM dbo.UserTable WHERE Email=@email", ("email", data.Admin.Email))) > 0)
            {
                // Release this transaction before opening the independent validation reader.
                await tx.CommitAsync(); await ValidateExistingAsync(app, data); return;
            }
            if (Convert.ToInt32(await Scalar(connection, tx, "SELECT COUNT(*) FROM dbo.StudentTable WHERE StudentId LIKE @prefix", ("prefix", data.StudentPrefix + "%"))) > 0)
                throw new InvalidOperationException("Preview namespace collision; no rows changed.");
            var accountPrefix = data.Admin.Email[..data.Admin.Email.LastIndexOf("admin@", StringComparison.Ordinal)];
            if (Convert.ToInt32(await Scalar(connection, tx, "SELECT COUNT(*) FROM dbo.UserTable WHERE Email LIKE @prefix", ("prefix", accountPrefix + "%"))) > 0)
                throw new InvalidOperationException("Preview account namespace collision; no rows changed.");
            async Task AddUser(User user) => user.UserId = Convert.ToInt32(await Scalar(connection, tx,
                "INSERT dbo.UserTable(Email,PasswordHash,PasswordSalt,Role,IsActive) OUTPUT INSERTED.UserId VALUES(@email,@hash,@salt,@role,@active)",
                ("email", user.Email), ("hash", user.PasswordHash), ("salt", user.PasswordSalt), ("role", user.Role), ("active", user.IsActive)));
            await AddUser(data.Admin); await AddUser(data.InactiveAdmin);
            foreach (var s in data.Students)
            {
                await AddUser(s.User);
                await Scalar(connection, tx, "INSERT dbo.StudentTable(StudentId,FirstName,MiddleName,LastName,Gender,CampusBranch,Department,Program,UserId,BirthDate) VALUES(@id,@first,@middle,@last,@gender,@campus,@college,@program,@user,@birth)",
                    ("id", s.StudentId), ("first", s.FirstName), ("middle", s.MiddleName), ("last", s.LastName), ("gender", s.Gender),
                    ("campus", s.CampusBranch), ("college", s.Department), ("program", s.Program), ("user", s.User.UserId), ("birth", s.BirthDate?.ToDateTime(TimeOnly.MinValue)));
            }
            foreach (var e in data.Events)
            {
                e.EventId = Convert.ToInt32(await Scalar(connection, tx, "INSERT dbo.EventsTable(Title,Description,VenueLocation,MaxCapacity,CurrentRegistrations,CreatedByUserId,EventStart,EventEnd,RegStart,RegEnd,Status,CancellationReason,TargetBranch,TargetDepartment,TargetProgram,TargetYearLevel,EventPhotoPath) OUTPUT INSERTED.EventId VALUES(@title,@description,@venue,@capacity,@count,@creator,@start,@end,@regStart,@regEnd,@status,@reason,@branch,@college,@program,@year,@banner)",
                    ("title", e.Title), ("description", e.Description), ("venue", e.VenueLocation), ("capacity", e.MaxCapacity), ("count", e.CurrentRegistrations),
                    ("creator", data.Admin.UserId), ("start", e.EventStart), ("end", e.EventEnd), ("regStart", e.RegStart.ToDateTime(TimeOnly.MinValue)),
                    ("regEnd", e.RegEnd.ToDateTime(new TimeOnly(23, 59, 59))), ("status", e.Status), ("reason", e.CancellationReason),
                    ("branch", e.TargetBranch), ("college", e.TargetDepartment), ("program", e.TargetProgram), ("year", e.TargetYearLevel), ("banner", e.EventPhotoPath)));
                foreach (var sponsor in e.Sponsors)
                    await Scalar(connection, tx, "INSERT dbo.SponsorListTable(SponsorName,CreatedAt,EventId) VALUES(@name,@time,@event)", ("name", sponsor.SponsorName), ("time", sponsor.CreatedAt), ("event", e.EventId));
                foreach (var r in e.Registrations)
                    r.EventRegistrationId = Convert.ToInt32(await Scalar(connection, tx, "INSERT dbo.EventRegistrationTable(EventId,StudentId,CurrentYearLvl,CurrentSection,Status,CheckInTimestamp) OUTPUT INSERTED.EventRegistrationId VALUES(@event,@student,@year,@section,@status,@time)",
                        ("event", e.EventId), ("student", r.StudentId), ("year", r.CurrentYearLvl), ("section", r.CurrentSection), ("status", r.Status), ("time", r.CheckInTimestamp)));
            }
            await ValidateRowsAsync(connection, tx, data);
            await tx.CommitAsync();
        }
        await ValidateExistingAsync(app, data);
    }

    public static async Task<JsonObject> ReadAsync(string app, PreviewDataset data)
    {
        await using var connection = new SqlConnection(Connection(app)); await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT e.EventId,e.Title,e.Status,e.EventStart,e.EventEnd,e.CurrentRegistrations,e.MaxCapacity FROM dbo.EventsTable e JOIN dbo.UserTable u ON u.UserId=e.CreatedByUserId WHERE u.Email=@email AND e.Description LIKE @marker ORDER BY e.EventId";
        command.Parameters.AddWithValue("@email", data.Admin.Email); command.Parameters.AddWithValue("@marker", data.Marker.Replace("[", "[[]") + "%");
        var events = new JsonArray();
        await using (var rows = await command.ExecuteReaderAsync())
            while (await rows.ReadAsync()) events.Add(new JsonObject { ["eventId"] = rows.GetInt32(0), ["title"] = rows.GetString(1), ["status"] = rows.GetString(2),
                ["start"] = rows.GetDateTime(3).ToString("s"), ["end"] = rows.GetDateTime(4).ToString("s"), ["reserved"] = rows.GetInt32(5), ["capacity"] = rows.GetInt32(6) });
        command.CommandText = "SELECT r.EventRegistrationId,r.EventId,r.StudentId,s.FirstName,s.LastName,e.EventEnd FROM dbo.EventRegistrationTable r JOIN dbo.EventsTable e ON e.EventId=r.EventId JOIN dbo.StudentTable s ON s.StudentId=r.StudentId JOIN dbo.UserTable u ON u.UserId=e.CreatedByUserId WHERE u.Email=@email AND e.Description LIKE @marker AND e.Title LIKE '%QR scanner practice%' AND r.Status='NoShow' AND r.CheckInTimestamp IS NULL AND e.Status='Upcoming' AND e.EventEnd> @now ORDER BY r.EventRegistrationId";
        command.Parameters.AddWithValue("@now", TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Asia/Manila").DateTime);
        var passes = new JsonArray();
        await using (var rows = await command.ExecuteReaderAsync())
            while (await rows.ReadAsync()) passes.Add(new JsonObject { ["reference"] = $"TCK-{rows.GetInt32(1):D4}-{rows.GetInt32(0):D5}", ["registrationId"] = rows.GetInt32(0),
                ["eventId"] = rows.GetInt32(1), ["studentId"] = rows.GetString(2), ["name"] = rows.GetString(3) + " " + rows.GetString(4), ["validUntilPhilippineTime"] = rows.GetDateTime(5).ToString("s") });
        return new() { ["app"] = app, ["admin"] = data.Admin.Email, ["events"] = events, ["unscannedPasses"] = passes };
    }

    private static async Task ValidateExistingAsync(string app, PreviewDataset data)
    {
        await using var connection = new SqlConnection(Connection(app)); await connection.OpenAsync();
        await ValidateRowsAsync(connection, null, data);
    }

    private static async Task ValidateRowsAsync(SqlConnection connection, SqlTransaction? tx, PreviewDataset data)
    {
        var events = Convert.ToInt32(await Scalar(connection, tx, "SELECT COUNT(*) FROM dbo.EventsTable WHERE Description LIKE @marker AND CreatedByUserId=(SELECT UserId FROM dbo.UserTable WHERE Email=@email AND Role='Admin' AND IsActive=1)", ("marker", data.Marker.Replace("[", "[[]") + "%"), ("email", data.Admin.Email)));
        var students = Convert.ToInt32(await Scalar(connection, tx, "SELECT COUNT(*) FROM dbo.StudentTable WHERE StudentId LIKE @prefix", ("prefix", data.StudentPrefix + "%")));
        var broken = Convert.ToInt32(await Scalar(connection, tx, "SELECT COUNT(*) FROM dbo.EventsTable e WHERE e.Description LIKE @marker AND (e.CurrentRegistrations<>(SELECT COUNT(*) FROM dbo.EventRegistrationTable r WHERE r.EventId=e.EventId AND r.Status<>'Cancelled') OR e.CurrentRegistrations>e.MaxCapacity)", ("marker", data.Marker.Replace("[", "[[]") + "%")));
        var invalidAttendance = Convert.ToInt32(await Scalar(connection, tx, "SELECT COUNT(*) FROM dbo.EventRegistrationTable r JOIN dbo.EventsTable e ON e.EventId=r.EventId WHERE e.Description LIKE @marker AND ((r.Status='NoShow' AND r.CheckInTimestamp IS NOT NULL) OR (r.Status='Present' AND r.CheckInTimestamp IS NULL))", ("marker", data.Marker.Replace("[", "[[]") + "%")));
        if (events != 9 || students != 36 || broken != 0 || invalidAttendance != 0) throw new InvalidOperationException("Preview namespace is incomplete or inconsistent; no existing records were overwritten.");
    }

    private static async Task<object?> Scalar(SqlConnection connection, SqlTransaction? tx, string sql, params (string Name, object? Value)[] values)
    {
        await using var command = new SqlCommand(sql, connection, tx);
        foreach (var (name, value) in values) command.Parameters.AddWithValue("@" + name, value ?? DBNull.Value);
        return await command.ExecuteScalarAsync();
    }
}
