using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.Models;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Admin;

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
            ?? "Server=localhost;Database=MVC_EventsManagement;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;";
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
        _db.Sponsors.Add(new Sponsor { Event = ev, SponsorName = "Campus Research Council", CreatedAt = ev.RegStart.ToDateTime(TimeOnly.MinValue) });
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
        Assert.Equal(new DateOnly(2030, 6, 1), saved.Event.RegStart);
        Assert.Equal(new DateOnly(2030, 6, 30), saved.Event.RegEnd);

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
            case "schedule": ev.RegEnd = DateOnly.FromDateTime(ev.EventStart); break;
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

    [Fact]
    public async Task Upcoming_events_apply_registration_boundaries_search_and_pagination_in_sql_server()
    {
        var utcNow = new DateTimeOffset(2037, 4, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new EventClock(new FixedTimeProvider(utcNow));
        var now = clock.Now;
        var marker = Guid.NewGuid().ToString("N");
        var owner = NewUser($"events-{marker}@example.test");
        var service = new EventService(_db, clock);

        Event MakeEvent(string title, DateTime regStart, DateTime regEnd, DateTime? start = null) => new()
        {
            Title = title, VenueLocation = $"Venue {marker}", TargetDepartment = $"Department {marker}",
            CreatedByUser = owner, MaxCapacity = 5,
            EventStart = start ?? now.AddDays(3), EventEnd = (start ?? now.AddDays(3)).AddHours(1),
            RegStart = DateOnly.FromDateTime(regStart), RegEnd = DateOnly.FromDateTime(regEnd)
        };

        var full = MakeEvent($"Full {marker}", now.AddDays(-1), now.AddDays(1));
        full.CurrentRegistrations = full.MaxCapacity;
        var cancelled = MakeEvent($"Cancelled {marker}", now.AddDays(-1), now.AddDays(1));
        cancelled.Status = "Cancelled";
        cancelled.CancellationReason = "Unavailable venue";
        var completed = MakeEvent($"Completed {marker}", now.AddDays(-1), now.AddDays(1));
        completed.Status = "Completed";
        _db.Events.AddRange(
            MakeEvent($"Opening boundary {marker}", now, now.AddDays(1), now.AddDays(4)),
            MakeEvent($"Deadline boundary {marker}", now.AddDays(-1), now),
            MakeEvent($"Opening soon {marker}", now.AddDays(1), now.AddDays(1)),
            MakeEvent($"Past deadline {marker}", now.AddDays(-2), now.AddDays(-1), now.AddDays(2)),
            MakeEvent($"Ended {marker}", now.AddDays(-2), now.AddDays(-1), now.AddHours(-1)),
            full, cancelled, completed);
        for (var i = 0; i < 26; i++)
        {
            _db.Events.Add(MakeEvent($"Page {i:D2} {marker}", now.AddDays(1), now.AddDays(1), now.AddDays(5 + i)));
        }
        await _db.SaveChangesAsync();

        var open = await service.GetUpcomingAsync($"venue {marker.ToUpperInvariant()}", "open", 1, default);
        Assert.Equal(new[] { $"Deadline boundary {marker}", $"Opening boundary {marker}" }, open.Events.Select(ev => ev.Title));
        var closed = await service.GetUpcomingAsync($"department {marker}", "closed", 1, default);
        Assert.Equal(new[] { $"Past deadline {marker}", $"Full {marker}" }, closed.Events.Select(ev => ev.Title));
        var soon = await service.GetUpcomingAsync($"venue {marker}", "soon", int.MaxValue, default);
        Assert.Equal(27, soon.MatchingCount);
        Assert.Equal(2, soon.Page);
        Assert.Equal(new[] { $"Page 24 {marker}", $"Page 25 {marker}" }, soon.Events.Select(ev => ev.Title));
        var all = await service.GetUpcomingAsync($"venue {marker}", null, 1, default);
        Assert.Equal(31, all.MatchingCount);
    }

    [Theory]
    [InlineData("2037-03-31T15:59:59Z", RegistrationStatus.Soon)]
    [InlineData("2037-03-31T16:00:00Z", RegistrationStatus.Open)]
    [InlineData("2037-04-01T15:59:59Z", RegistrationStatus.Open)]
    [InlineData("2037-04-01T16:00:00Z", RegistrationStatus.Closed)]
    public async Task Registration_dates_cover_the_entire_Philippine_day_including_one_day_windows(string utc, RegistrationStatus expected)
    {
        var (ev, _) = await CreateGraphAsync();
        ev.Title = $"Single-day registration {Guid.NewGuid():N}";
        ev.EventStart = new DateTime(2037, 4, 3, 9, 0, 0);
        ev.EventEnd = ev.EventStart.AddHours(2);
        ev.RegStart = new DateOnly(2037, 4, 1);
        ev.RegEnd = ev.RegStart;
        await _db.SaveChangesAsync();
        var clock = new EventClock(new FixedTimeProvider(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture)));
        var result = await new EventService(_db, clock).GetUpcomingAsync(ev.Title, null, 1, default);
        Assert.Equal(expected, Assert.Single(result.Events).Status);
        Assert.Equal(new DateOnly(2037, 4, 1), result.Events[0].RegistrationOpensAt);
        Assert.Equal(new DateOnly(2037, 4, 1), result.Events[0].RegistrationClosesAt);
    }

    [Fact]
    public async Task History_uses_effective_outcomes_and_August_academic_years_without_mutating_events()
    {
        var utc = new DateTimeOffset(2031, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new EventClock(new FixedTimeProvider(utc));
        var service = new EventService(_db, clock);
        var baseline = await service.GetHistoryAsync(null, null, null, 1, default);
        var marker = Guid.NewGuid().ToString("N");
        var owner = NewUser($"history-{marker}@example.test");
        Event Make(string title, DateTime start, string status = "Upcoming") => new()
        {
            Title = $"{title} {marker}", VenueLocation = $"Venue {marker}", TargetDepartment = $"College {marker}",
            CreatedByUser = owner, MaxCapacity = 20, CurrentRegistrations = 3,
            EventStart = start, EventEnd = start.AddHours(2), RegStart = DateOnly.FromDateTime(start.AddDays(-3)),
            RegEnd = DateOnly.FromDateTime(start.AddDays(-1)), Status = status,
            CancellationReason = status == "Cancelled" ? "Original cancellation reason" : null
        };
        var boundary = Make("Boundary", clock.Now.AddHours(-2));
        var cancelled = Make("Future cancelled", new DateTime(2031, 8, 2, 9, 0, 0), "Cancelled");
        _db.Events.AddRange(
            Make("July", new DateTime(2030, 7, 31, 9, 0, 0)),
            Make("August", new DateTime(2030, 8, 1, 9, 0, 0)),
            Make("January", new DateTime(2031, 1, 15, 9, 0, 0)),
            Make("June", new DateTime(2031, 6, 1, 9, 0, 0)), boundary, cancelled,
            Make("Stored completed", new DateTime(2032, 8, 2, 9, 0, 0), "Completed"),
            Make("Still upcoming", clock.Now.AddHours(1)));
        await _db.SaveChangesAsync();
        var originalVersion = boundary.RowVersion.ToArray();
        var all = await service.GetHistoryAsync($"VENUE {marker.ToUpperInvariant()}", null, null, 1, default);
        Assert.Equal(7, all.Total);
        Assert.Equal(baseline.Counts.GetValueOrDefault("Completed") + 6, all.Counts["Completed"]);
        Assert.Equal(baseline.Counts.GetValueOrDefault("Cancelled") + 1, all.Counts["Cancelled"]);
        Assert.Equal(all.Events.OrderByDescending(e => e.StartsAt).ThenByDescending(e => e.EventId), all.Events);
        var academic = await service.GetHistoryAsync($"college {marker}", 2030, " completed ", 1, default);
        Assert.Equal(4, academic.Total);
        Assert.Equal(all.Counts, academic.Counts);
        var july = await service.GetHistoryAsync(marker, 2029, null, 1, default);
        Assert.StartsWith("July", Assert.Single(july.Events).Title);
        var jan = await service.GetHistoryAsync($"January {marker}", 2030, null, 1, default);
        Assert.Single(jan.Events);
        var cancelledYear = await service.GetHistoryAsync(marker, 2031, "cancelled", 1, default);
        Assert.Equal(cancelled.EventId, Assert.Single(cancelledYear.Events).EventId);
        Assert.Contains(2032, all.AcademicYears);
        Assert.Contains(2029, all.AcademicYears);
        var before = new EventService(_db, new EventClock(new FixedTimeProvider(utc.AddSeconds(-1))));
        Assert.Empty((await before.GetHistoryAsync(boundary.Title, null, null, 1, default)).Events);
        Assert.Single((await before.GetUpcomingAsync(boundary.Title, null, 1, default)).Events);
        Assert.Empty((await service.GetUpcomingAsync(boundary.Title, null, 1, default)).Events);
        _db.ChangeTracker.Clear();
        var retained = await _db.Events.SingleAsync(e => e.EventId == boundary.EventId);
        Assert.Equal("Upcoming", retained.Status);
        Assert.Equal(3, retained.CurrentRegistrations);
        Assert.Equal(originalVersion, retained.RowVersion);
        Assert.Equal("Original cancellation reason", (await _db.Events.SingleAsync(e => e.EventId == cancelled.EventId)).CancellationReason);
    }

    [Fact]
    public async Task History_paginates_and_normalizes_filters_with_stable_newest_first_order()
    {
        var service = new EventService(_db, new EventClock(new FixedTimeProvider(new DateTimeOffset(2031, 7, 1, 0, 0, 0, TimeSpan.Zero))));
        var marker = Guid.NewGuid().ToString("N");
        var owner = NewUser($"history-pages-{marker}@example.test");
        for (var i = 0; i < 27; i++)
        {
            var start = new DateTime(2030, 8, 1, 9, 0, 0).AddDays(i / 2);
            _db.Events.Add(new Event { Title = $"Page {i:D2} {marker}", VenueLocation = "Hall A", CreatedByUser = owner,
                MaxCapacity = 10, EventStart = start, EventEnd = start.AddHours(1),
                RegStart = DateOnly.FromDateTime(start.AddDays(-2)), RegEnd = DateOnly.FromDateTime(start.AddDays(-1)) });
        }
        await _db.SaveChangesAsync();
        var first = await service.GetHistoryAsync($" {marker} ", 2030, "invalid", -100, default);
        Assert.Equal(27, first.Total); Assert.Equal(1, first.Page); Assert.Equal(2, first.TotalPages);
        Assert.Equal(25, first.Events.Count); Assert.Null(first.Outcome);
        Assert.StartsWith("Page 26", first.Events[0].Title);
        var last = await service.GetHistoryAsync(marker, 2030, null, int.MaxValue, default);
        Assert.Equal(2, last.Page); Assert.Equal(2, last.Events.Count);
        Assert.StartsWith("Page 01", last.Events[0].Title); Assert.StartsWith("Page 00", last.Events[1].Title);
        var empty = await service.GetHistoryAsync(marker, 2029, null, int.MaxValue, default);
        Assert.Empty(empty.Events); Assert.Equal(1, empty.Page); Assert.Equal(1, empty.TotalPages);
        Assert.Equal(first.TotalHistorical, empty.TotalHistorical);
    }

    [Fact]
    public async Task Roster_keeps_present_students_active_and_filters_by_registered_academic_fields()
    {
        var (ev, student) = await CreateGraphAsync();
        student.FirstName = "Lina"; student.MiddleName = "Mae"; student.LastName = "Rivera";
        var engineer = NewStudent(NewUser($"roster-engineer-{Guid.NewGuid():N}@example.test"));
        engineer.Department = "College of Engineering"; engineer.Program = "BSIE";
        var educator = NewStudent(NewUser($"roster-educator-{Guid.NewGuid():N}@example.test"));
        educator.Department = "College of Education"; educator.Program = "BECEd";
        var reserved = NewRegistration(ev, student);
        var present = NewRegistration(ev, engineer); present.CurrentYearLvl = 4; present.CurrentSection = "SBIE-4B";
        present.Status = "Present"; present.CheckInTimestamp = ev.EventStart;
        var cancelled = NewRegistration(ev, educator); cancelled.Status = "Cancelled";
        ev.CurrentRegistrations = 2;
        _db.EventRegistrations.AddRange(reserved, present, cancelled); await _db.SaveChangesAsync();
        var service = new RegistrationService(_db, new EventClock(new FixedTimeProvider(new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero))));
        var all = await service.GetRosterAsync(ev.EventId, new(), default);
        Assert.NotNull(all); Assert.Equal(2, all.Active); Assert.Equal(1, all.Cancelled);
        Assert.Equal("Completed", all.Event.Status); Assert.Equal(2, all.Event.Registered);
        Assert.Contains(all.Attendees, a => a.DisplayStatus == "Present");
        Assert.Contains(all.Attendees, a => a.DisplayStatus == "Reserved");
        Assert.Equal(3, all.Colleges.Count); Assert.Equal(3, all.Programs.Count);
        var search = await service.GetRosterAsync(ev.EventId, new() { Q = " lina mae RIVERA " }, default);
        Assert.Equal(reserved.EventRegistrationId, Assert.Single(search!.Attendees).RegistrationId);
        var filtered = await service.GetRosterAsync(ev.EventId, new() { Q = engineer.StudentId, College = engineer.Department, Program = "BSIE", Year = 4 }, default);
        Assert.Equal("SBIE-4B", Assert.Single(filtered!.Attendees).Section);
        Assert.Equal(all.Active, filtered.Active);
        var empty = await service.GetRosterAsync(ev.EventId, new() { Program = "BSIE", Year = 3 }, default);
        Assert.Empty(empty!.Attendees);
        var revoked = await service.GetRosterAsync(ev.EventId, new() { Cohort = "cancelled", Page = int.MaxValue }, default);
        Assert.Equal(cancelled.EventRegistrationId, Assert.Single(revoked!.Attendees).RegistrationId);
        Assert.Equal(1, revoked.Query.Page);
        Assert.Null(await service.GetRosterAsync(int.MaxValue, new(), default));
    }

    [Fact]
    public async Task Roster_paginates_with_stable_ticket_order_and_bounds_invalid_queries()
    {
        var (ev, _) = await CreateGraphAsync();
        for (var i = 0; i < 27; i++)
        {
            var student = NewStudent(NewUser($"roster-page-{Guid.NewGuid():N}@example.test"));
            _db.EventRegistrations.Add(NewRegistration(ev, student));
        }
        ev.CurrentRegistrations = 27; await _db.SaveChangesAsync();
        var service = new RegistrationService(_db, new EventClock(TimeProvider.System));
        var first = await service.GetRosterAsync(ev.EventId, new() { Page = -1, Cohort = "unknown", Year = 99 }, default);
        Assert.Equal(27, first!.Total); Assert.Equal(25, first.Attendees.Count); Assert.Equal(1, first.Query.Page);
        Assert.Null(first.Query.Cohort); Assert.Null(first.Query.Year);
        Assert.Equal(first.Attendees.OrderByDescending(a => a.RegistrationId), first.Attendees);
        var last = await service.GetRosterAsync(ev.EventId, new() { Page = int.MaxValue }, default);
        Assert.Equal(2, last!.Query.Page); Assert.Equal(2, last.Attendees.Count);
        Assert.DoesNotContain(last.Attendees[0].RegistrationId, first.Attendees.Select(a => a.RegistrationId));
    }

    [Fact]
    public async Task Administrator_void_is_event_bound_confirmed_and_preserves_checkin_history_even_after_event_cancellation()
    {
        var (ev, student) = await CreateGraphAsync();
        ev.CurrentRegistrations = 1; ev.Status = "Cancelled"; ev.CancellationReason = "Venue unavailable";
        var registration = NewRegistration(ev, student); registration.Status = "Present"; registration.CheckInTimestamp = ev.EventStart;
        _db.EventRegistrations.Add(registration); await _db.SaveChangesAsync();
        var service = new RegistrationService(_db, new EventClock(TimeProvider.System));
        var input = new VoidPassInput { Version = Convert.ToBase64String(registration.RowVersion), EventVersion = Convert.ToBase64String(ev.RowVersion) };
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.VoidPassAsync(ev.EventId, registration.EventRegistrationId, input, default));
        input.Confirmed = true;
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.VoidPassAsync(int.MaxValue, registration.EventRegistrationId, input, default));
        await service.VoidPassAsync(ev.EventId, registration.EventRegistrationId, input, default);
        _db.ChangeTracker.Clear();
        var saved = await _db.EventRegistrations.Include(r => r.Event).SingleAsync(r => r.EventRegistrationId == registration.EventRegistrationId);
        Assert.Equal("Cancelled", saved.Status); Assert.Equal(ev.EventStart, saved.CheckInTimestamp);
        Assert.Equal(0, saved.Event.CurrentRegistrations); Assert.Equal("Venue unavailable", saved.Event.CancellationReason);
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.VoidPassAsync(ev.EventId, registration.EventRegistrationId, input, default));
        Assert.Equal(0, await _db.Events.Where(e => e.EventId == ev.EventId).Select(e => e.CurrentRegistrations).SingleAsync());
    }

    [Fact]
    public async Task Stale_pass_or_event_tokens_roll_back_the_entire_void_and_counter_update()
    {
        var (ev, student) = await CreateGraphAsync();
        ev.CurrentRegistrations = 1;
        var registration = NewRegistration(ev, student);
        _db.EventRegistrations.Add(registration); await _db.SaveChangesAsync();
        var eventVersion = ev.RowVersion.ToArray(); var passVersion = registration.RowVersion.ToArray();
        ev.Title = "Changed event"; await _db.SaveChangesAsync();
        var service = new RegistrationService(_db, new EventClock(TimeProvider.System));
        var input = new VoidPassInput { Confirmed = true, Version = Convert.ToBase64String(passVersion), EventVersion = Convert.ToBase64String(eventVersion) };
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.VoidPassAsync(ev.EventId, registration.EventRegistrationId, input, default));
        _db.ChangeTracker.Clear();
        Assert.Equal("NoShow", await _db.EventRegistrations.Where(r => r.EventRegistrationId == registration.EventRegistrationId).Select(r => r.Status).SingleAsync());
        var freshEvent = await _db.Events.SingleAsync(e => e.EventId == ev.EventId);
        Assert.Equal(1, freshEvent.CurrentRegistrations);
        var pass = await _db.EventRegistrations.SingleAsync(r => r.EventRegistrationId == registration.EventRegistrationId);
        pass.Status = "Present"; pass.CheckInTimestamp = ev.EventStart; await _db.SaveChangesAsync();
        input.EventVersion = Convert.ToBase64String(freshEvent.RowVersion);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.VoidPassAsync(ev.EventId, registration.EventRegistrationId, input, default));
        _db.ChangeTracker.Clear();
        Assert.Equal(1, await _db.Events.Where(e => e.EventId == ev.EventId).Select(e => e.CurrentRegistrations).SingleAsync());
        var retained = await _db.EventRegistrations.SingleAsync(r => r.EventRegistrationId == registration.EventRegistrationId);
        Assert.Equal("Present", retained.Status); Assert.Equal(ev.EventStart, retained.CheckInTimestamp);
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
            RegStart = new DateOnly(2030, 6, 1), RegEnd = new DateOnly(2030, 6, 30)
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
