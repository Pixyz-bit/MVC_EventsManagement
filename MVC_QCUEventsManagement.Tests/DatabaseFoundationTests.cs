using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.Models;
using MVC_QCUEventsManagement.Services;

namespace MVC_QCUEventsManagement.Tests;

// Every SQL test is rolled back, even on assertion failure. No seed/test rows remain.
public sealed class DatabaseFoundationTests : IAsyncLifetime
{
    private SqlConnection _connection = null!;
    private IDbContextTransaction _transaction = null!;
    private DbContextOptions<ApplicationDbContext> _options = null!;
    private ApplicationDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable("MVC_EVENTS_TEST_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=MVC_EventsManagement;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";
        var connection = new SqlConnectionStringBuilder(connectionString);
        if (connection.InitialCatalog != "MVC_EventsManagement")
        {
            throw new InvalidOperationException("Foundation tests must target MVC_EventsManagement only.");
        }

        _connection = new SqlConnection(connectionString);
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(_connection).Options;
        _db = new ApplicationDbContext(_options);
        _transaction = await _db.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
        }
        if (_db is not null) await _db.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Related_records_round_trip_with_sql_types_and_no_cascade_delete()
    {
        var (ev, student) = await CreateGraphAsync();
        var registration = NewRegistration(ev, student);
        _db.EventRegistrations.Add(registration);
        _db.Sponsors.Add(new Sponsor { Event = ev, SponsorName = "Campus Research Council", CreatedAt = ev.RegStart });
        await _db.SaveChangesAsync();
        var eventId = ev.EventId;
        _db.ChangeTracker.Clear();

        var saved = await _db.EventRegistrations.AsNoTracking()
            .Include(item => item.Student).ThenInclude(item => item.User)
            .Include(item => item.Event).ThenInclude(item => item.Sponsors)
            .SingleAsync(item => item.EventRegistrationId == registration.EventRegistrationId);
        Assert.Equal(new DateOnly(2005, 4, 14), saved.Student.BirthDate);
        Assert.Null(saved.Student.MiddleName);
        Assert.Equal("NoShow", saved.Status);
        Assert.Null(saved.CheckInTimestamp);
        Assert.Equal("Campus Research Council", Assert.Single(saved.Event.Sponsors).SponsorName);
        Assert.Equal(8, saved.RowVersion.Length);

        var error = await Assert.ThrowsAsync<SqlException>(() => _db.Events.Where(item => item.EventId == eventId).ExecuteDeleteAsync());
        Assert.Equal(547, error.Number);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected_by_the_database()
    {
        var email = $"foundation-{Guid.NewGuid():N}@example.test";
        _db.Users.Add(NewUser(email));
        await _db.SaveChangesAsync();
        _db.Users.Add(NewUser(email));
        await AssertUniqueViolationAsync();
    }

    [Fact]
    public async Task An_account_cannot_have_two_student_profiles()
    {
        var (_, student) = await CreateGraphAsync();
        var otherProfile = NewStudent(student.User);
        _db.ChangeTracker.Clear();
        otherProfile.User = null!;
        _db.Students.Add(otherProfile);
        await AssertUniqueViolationAsync();
    }

    [Fact]
    public async Task Cancelled_history_allows_rebooking_but_a_second_active_booking_fails()
    {
        var (ev, student) = await CreateGraphAsync();
        var cancelled = NewRegistration(ev, student);
        cancelled.Status = "Cancelled";
        _db.EventRegistrations.Add(cancelled);
        _db.EventRegistrations.Add(NewRegistration(ev, student));
        await _db.SaveChangesAsync();
        Assert.Equal(2, await _db.EventRegistrations.CountAsync(item => item.EventId == ev.EventId));

        _db.EventRegistrations.Add(NewRegistration(ev, student));
        await AssertUniqueViolationAsync();
    }

    [Theory]
    [InlineData("Reserved", null)]
    [InlineData("Present", null)]
    [InlineData("NoShow", "2030-07-01T09:00:00")]
    public async Task Invalid_registration_status_or_timestamp_is_rejected(string status, string? timestamp)
    {
        var (ev, student) = await CreateGraphAsync();
        var registration = NewRegistration(ev, student);
        registration.Status = status;
        registration.CheckInTimestamp = timestamp is null ? null : DateTime.Parse(timestamp, System.Globalization.CultureInfo.InvariantCulture);
        _db.EventRegistrations.Add(registration);
        await AssertConstraintViolationAsync();
    }

    [Theory]
    [InlineData(0, "SBIT-3A")]
    [InlineData(6, "SBIT-3A")]
    [InlineData(3, "   ")]
    public async Task Invalid_registration_year_or_section_is_rejected(int year, string section)
    {
        var (ev, student) = await CreateGraphAsync();
        var registration = NewRegistration(ev, student);
        registration.CurrentYearLvl = year;
        registration.CurrentSection = section;
        _db.EventRegistrations.Add(registration);
        await AssertConstraintViolationAsync();
    }

    [Theory]
    [InlineData("capacity")]
    [InlineData("schedule")]
    [InlineData("status")]
    [InlineData("cancelled reason")]
    public async Task Invalid_event_invariants_are_rejected(string invalidField)
    {
        var (ev, _) = await CreateGraphAsync();
        switch (invalidField)
        {
            case "capacity": ev.CurrentRegistrations = ev.MaxCapacity + 1; break;
            case "schedule": ev.RegEnd = ev.EventStart; break;
            case "status": ev.Status = "Open"; break;
            case "cancelled reason": ev.Status = "Cancelled"; ev.CancellationReason = null; break;
        }
        await AssertConstraintViolationAsync();
    }

    [Fact]
    public async Task Stale_event_update_is_rejected_by_rowversion()
    {
        var (ev, _) = await CreateGraphAsync();
        await using var other = new ApplicationDbContext(_options);
        await other.Database.UseTransactionAsync(_transaction.GetDbTransaction());
        var stale = await other.Events.SingleAsync(item => item.EventId == ev.EventId);
        ev.Title = "First administrator's update";
        await _db.SaveChangesAsync();
        stale.Title = "Stale administrator's update";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => other.SaveChangesAsync());
        Assert.Equal(ev.Title, await _db.Events.AsNoTracking().Where(item => item.EventId == ev.EventId).Select(item => item.Title).SingleAsync());
    }

    [Fact]
    public async Task Checkin_timestamp_is_preserved_when_an_administrator_voids_a_present_pass()
    {
        var (ev, student) = await CreateGraphAsync();
        var registration = NewRegistration(ev, student);
        registration.Status = "Present";
        registration.CheckInTimestamp = ev.EventStart;
        _db.EventRegistrations.Add(registration);
        await _db.SaveChangesAsync();
        registration.Status = "Cancelled";
        await _db.SaveChangesAsync();
        Assert.Equal(ev.EventStart, await _db.EventRegistrations.AsNoTracking()
            .Where(item => item.EventRegistrationId == registration.EventRegistrationId)
            .Select(item => item.CheckInTimestamp).SingleAsync());
    }

    [Fact]
    public void Application_clock_uses_Philippine_time_independent_of_machine_timezone()
    {
        var clock = new EventClock(new FixedTimeProvider(new DateTimeOffset(2030, 6, 30, 18, 0, 0, TimeSpan.Zero)));
        Assert.Equal(new DateTime(2030, 7, 1, 2, 0, 0, DateTimeKind.Unspecified), clock.Now);
        Assert.Equal(DateTimeKind.Unspecified, clock.Now.Kind);
    }

    private async Task<(Event Event, Student Student)> CreateGraphAsync()
    {
        var admin = NewUser($"foundation-admin-{Guid.NewGuid():N}@example.test");
        var user = NewUser($"foundation-student-{Guid.NewGuid():N}@example.test");
        user.Role = "Student";
        var student = NewStudent(user);
        var ev = new Event
        {
            Title = "Campus Research Forum", VenueLocation = "Main Campus Hall", MaxCapacity = 30,
            CreatedByUser = admin, EventStart = new DateTime(2030, 7, 1, 9, 0, 0),
            EventEnd = new DateTime(2030, 7, 1, 17, 0, 0),
            RegStart = new DateTime(2030, 6, 1, 9, 0, 0), RegEnd = new DateTime(2030, 6, 30, 17, 0, 0)
        };
        _db.Students.Add(student);
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        return (ev, student);
    }

    private static User NewUser(string email) => new()
    {
        Email = email, PasswordHash = "not-an-authentication-fixture", PasswordSalt = "test-only", Role = "Admin"
    };

    private static Student NewStudent(User user) => new()
    {
        StudentId = $"TEST-{Guid.NewGuid():N}", FirstName = "Althea", LastName = "Navarro",
        Gender = "Female", CampusBranch = "San Bartolome", Department = "College of Computer Studies",
        Program = "BS Information Technology", BirthDate = new DateOnly(2005, 4, 14), User = user, UserId = user.UserId
    };

    private static EventRegistration NewRegistration(Event ev, Student student) => new()
    {
        Event = ev, Student = student, StudentId = student.StudentId, CurrentYearLvl = 3, CurrentSection = "SBIT-3A"
    };

    private async Task AssertUniqueViolationAsync()
    {
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
        var sql = Assert.IsType<SqlException>(error.InnerException);
        Assert.Contains(sql.Number, new[] { 2601, 2627 });
    }

    private async Task AssertConstraintViolationAsync()
    {
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
        Assert.Equal(547, Assert.IsType<SqlException>(error.InnerException).Number);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
