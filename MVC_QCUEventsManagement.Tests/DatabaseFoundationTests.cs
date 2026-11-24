using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.Models;
using MVC_QCUEventsManagement.Services;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Tests;

// Every SQL test is rolled back, even on assertion failure. No seed/test rows remain.
[Collection("Shared SQL rollback fixtures")]
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

    [Fact]
    public async Task Scanner_lookup_is_read_only_event_scoped_and_validates_the_full_ticket_reference()
    {
        var (ev, student) = await CreateGraphAsync();
        var pass = NewRegistration(ev, student); ev.CurrentRegistrations = 1;
        _db.EventRegistrations.Add(pass); await _db.SaveChangesAsync();
        var version = pass.RowVersion.ToArray(); var eventVersion = ev.RowVersion.ToArray();
        var service = new AttendanceService(_db, new EventClock(new FixedTimeProvider(new DateTimeOffset(2030, 6, 30, 0, 0, 0, TimeSpan.Zero))));
        var reference = $"TCK-{ev.EventId:D4}-{pass.EventRegistrationId:D5}";
        foreach (var query in new[] { reference, reference.ToLowerInvariant() + "|legacy", pass.EventRegistrationId.ToString(), student.StudentId.ToLowerInvariant(), student.User.Email.ToUpperInvariant() })
        {
            var lookup = await service.LookupAsync(ev.EventId, query, default);
            Assert.Equal("ValidPending", lookup.State); Assert.True(lookup.CanConfirm);
            Assert.Equal(reference, lookup.Attendee!.TicketReference);
        }
        var forged = await service.LookupAsync(ev.EventId, $"TCK-{ev.EventId + 1:D4}-{pass.EventRegistrationId:D5}", default);
        Assert.Equal("WrongEventWarning", forged.State); Assert.False(forged.CanConfirm);
        Assert.Equal("NotFound", (await service.LookupAsync(ev.EventId, "TCK-no-event-no-pass", default)).State);
        Assert.Equal("NotFound", (await service.LookupAsync(ev.EventId, "not-a-student", default)).State);
        Assert.Equal("NotFound", (await service.LookupAsync(ev.EventId, new string('x', 151), default)).State);
        var other = new Event { Title = "Other scanner gate", VenueLocation = "Hall B", MaxCapacity = 5, CreatedByUserId = ev.CreatedByUserId,
            EventStart = ev.EventStart, EventEnd = ev.EventEnd, RegStart = ev.RegStart, RegEnd = ev.RegEnd };
        _db.Events.Add(other); await _db.SaveChangesAsync();
        Assert.Equal("WrongEventWarning", (await service.LookupAsync(other.EventId, reference, default)).State);
        var wrongGate = await service.ConfirmAsync(other.EventId, new CheckInInput { Confirmed = true, RegistrationId = pass.EventRegistrationId,
            Version = Convert.ToBase64String(pass.RowVersion), EventVersion = Convert.ToBase64String(ev.RowVersion) }, default);
        Assert.Equal("WrongEventWarning", wrongGate.State); Assert.False(wrongGate.CanConfirm);
        var local = NewRegistration(other, student); _db.EventRegistrations.Add(local); await _db.SaveChangesAsync();
        Assert.Equal(local.EventRegistrationId, (await service.LookupAsync(other.EventId, student.StudentId, default)).Attendee!.RegistrationId);
        _db.ChangeTracker.Clear();
        var retained = await _db.EventRegistrations.SingleAsync(r => r.EventRegistrationId == pass.EventRegistrationId);
        Assert.Equal("NoShow", retained.Status); Assert.Null(retained.CheckInTimestamp); Assert.Equal(version, retained.RowVersion);
        var retainedEvent = await _db.Events.SingleAsync(e => e.EventId == ev.EventId);
        Assert.Equal(1, retainedEvent.CurrentRegistrations); Assert.Equal(eventVersion, retainedEvent.RowVersion);
    }

    [Fact]
    public async Task Scanner_confirmation_atomically_marks_present_once_without_changing_reserved_seats()
    {
        var (ev, student) = await CreateGraphAsync();
        ev.CurrentRegistrations = 1; var pass = NewRegistration(ev, student);
        _db.EventRegistrations.Add(pass); await _db.SaveChangesAsync();
        var service = new AttendanceService(_db, new EventClock(new FixedTimeProvider(new DateTimeOffset(2030, 6, 30, 0, 0, 0, TimeSpan.Zero))));
        var lookup = await service.LookupAsync(ev.EventId, student.StudentId, default);
        var input = new CheckInInput { RegistrationId = pass.EventRegistrationId, Version = Convert.ToBase64String(lookup.Attendee!.Version), EventVersion = Convert.ToBase64String(lookup.Attendee.EventVersion) };
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.ConfirmAsync(ev.EventId, input, default));
        input.Confirmed = true;
        var success = await service.ConfirmAsync(ev.EventId, input, default);
        Assert.Equal("CheckedIn", success.State); Assert.False(success.CanConfirm); Assert.Equal(1, success.CheckedInCount);
        Assert.Equal("Present", success.Attendee!.Status); Assert.NotNull(success.Attendee.CheckInTimestamp);
        Assert.True(success.Attendee.CheckInTimestamp < ev.EventStart); // Source has no door-opening/start-time restriction.
        var repeat = await service.ConfirmAsync(ev.EventId, input, default);
        Assert.Equal("DuplicateWarning", repeat.State); Assert.Equal(success.Attendee.CheckInTimestamp, repeat.Attendee!.CheckInTimestamp);
        _db.ChangeTracker.Clear();
        Assert.Equal(1, await _db.Events.Where(e => e.EventId == ev.EventId).Select(e => e.CurrentRegistrations).SingleAsync());
        var roster = await new RegistrationService(_db, new EventClock(TimeProvider.System)).GetRosterAsync(ev.EventId, new(), default);
        Assert.Equal("Present", Assert.Single(roster!.Attendees).Status);
    }

    [Theory]
    [InlineData("cancel-pass", "CancelledWarning")]
    [InlineData("check-in", "DuplicateWarning")]
    [InlineData("cancel-event", "CancelledWarning")]
    [InlineData("end-event", "CancelledWarning")]
    [InlineData("change-event", "Changed")]
    public async Task Scanner_confirmation_revalidates_changes_after_lookup(string change, string expected)
    {
        var (ev, student) = await CreateGraphAsync();
        var pass = NewRegistration(ev, student); ev.CurrentRegistrations = 1;
        _db.EventRegistrations.Add(pass); await _db.SaveChangesAsync();
        var clock = new EventClock(new FixedTimeProvider(new DateTimeOffset(2030, 7, 1, 4, 0, 0, TimeSpan.Zero)));
        var service = new AttendanceService(_db, clock);
        var lookup = await service.LookupAsync(ev.EventId, student.StudentId, default);
        Assert.True(lookup.CanConfirm);
        var input = new CheckInInput { Confirmed = true, RegistrationId = pass.EventRegistrationId,
            Version = Convert.ToBase64String(lookup.Attendee!.Version), EventVersion = Convert.ToBase64String(lookup.Attendee.EventVersion) };
        if (change == "cancel-pass") pass.Status = "Cancelled";
        if (change == "check-in") { pass.Status = "Present"; pass.CheckInTimestamp = clock.Now.AddSeconds(-1); }
        if (change == "cancel-event") { ev.Status = "Cancelled"; ev.CancellationReason = "Gate unavailable"; }
        if (change == "end-event") ev.EventEnd = clock.Now;
        if (change == "change-event") ev.Title = "Revised event";
        await _db.SaveChangesAsync();
        var result = await service.ConfirmAsync(ev.EventId, input, default);
        Assert.Equal(expected, result.State); Assert.False(result.CanConfirm);
        _db.ChangeTracker.Clear();
        var retained = await _db.EventRegistrations.SingleAsync(r => r.EventRegistrationId == pass.EventRegistrationId);
        if (change != "check-in") Assert.Null(retained.CheckInTimestamp);
        Assert.Equal(change == "cancel-pass" ? "Cancelled" : change == "check-in" ? "Present" : "NoShow", retained.Status);
    }

    [Fact]
    public async Task Attendance_is_present_only_event_scoped_searchable_and_read_only()
    {
        var (ev, student) = await CreateGraphAsync();
        student.FirstName = "Mikaela"; student.MiddleName = "Mae"; student.LastName = "Dizon";
        var present = NewRegistration(ev, student); present.Status = "Present"; present.CheckInTimestamp = ev.EventStart;
        var engineer = NewStudent(NewUser($"attendance-{Guid.NewGuid():N}@example.test"));
        engineer.Department = "College of Engineering"; engineer.Program = "BSIE";
        var later = NewRegistration(ev, engineer); later.Status = "Present"; later.CheckInTimestamp = ev.EventStart.AddMinutes(1); later.CurrentYearLvl = 4;
        var reserved = NewRegistration(ev, NewStudent(NewUser($"reserved-{Guid.NewGuid():N}@example.test")));
        var cancelled = NewRegistration(ev, NewStudent(NewUser($"cancelled-{Guid.NewGuid():N}@example.test")));
        cancelled.Status = "Cancelled"; cancelled.CheckInTimestamp = ev.EventStart.AddHours(1);
        var (other, otherStudent) = await CreateGraphAsync();
        var elsewhere = NewRegistration(other, otherStudent); elsewhere.Status = "Present"; elsewhere.CheckInTimestamp = other.EventStart;
        ev.CurrentRegistrations = 3;
        _db.EventRegistrations.AddRange(present, later, reserved, cancelled, elsewhere); await _db.SaveChangesAsync();
        var version = ev.RowVersion.ToArray();
        var service = new AttendanceService(_db, new EventClock(TimeProvider.System));
        var all = await service.GetAttendanceAsync(ev.EventId, new(), default);
        Assert.NotNull(all); Assert.Equal(2, all.CheckedIn); Assert.Equal(2, all.Total);
        Assert.Equal(new[] { later.EventRegistrationId, present.EventRegistrationId }, all.Attendees.Select(a => a.RegistrationId));
        Assert.Equal(ev.EventEnd, all.Event.EndsAt); Assert.Equal(2, all.Colleges.Count);
        var filtered = await service.GetAttendanceAsync(ev.EventId, new() { Q = " mikaela mae DIZON ", College = student.Department, Program = student.Program, From = "history" }, default);
        Assert.Equal(present.EventRegistrationId, Assert.Single(filtered!.Attendees).RegistrationId);
        Assert.Equal(3, filtered.Attendees[0].Year); Assert.Equal("SBIT-3A", filtered.Attendees[0].Section);
        Assert.Equal(2, filtered.CheckedIn); Assert.Equal(all.Programs, filtered.Programs); Assert.Equal("history", filtered.Query.From);
        var byTicket = await service.GetAttendanceAsync(ev.EventId, new() { Q = $"tck-{ev.EventId:D4}-{later.EventRegistrationId:D5}" }, default);
        Assert.Equal(later.EventRegistrationId, Assert.Single(byTicket!.Attendees).RegistrationId);
        var byId = await service.GetAttendanceAsync(ev.EventId, new() { Q = student.StudentId.ToLowerInvariant() }, default);
        Assert.Single(byId!.Attendees);
        var wrongEvent = await service.GetAttendanceAsync(ev.EventId, new() { Q = $"TCK-{other.EventId:D4}-{present.EventRegistrationId:D5}" }, default);
        Assert.Empty(wrongEvent!.Attendees);
        Assert.Null(await service.GetAttendanceAsync(int.MaxValue, new(), default));
        var saved = await _db.Events.AsNoTracking().SingleAsync(e => e.EventId == ev.EventId);
        Assert.Equal(version, saved.RowVersion); Assert.Equal(3, saved.CurrentRegistrations);
        Assert.Equal("NoShow", await _db.EventRegistrations.Where(r => r.EventRegistrationId == reserved.EventRegistrationId).Select(r => r.Status).SingleAsync());
    }

    [Fact]
    public async Task Attendance_paginates_stable_ties_and_bounds_queries()
    {
        var (ev, student) = await CreateGraphAsync();
        for (var i = 0; i < 27; i++)
        {
            var pass = NewRegistration(ev, i == 0 ? student : NewStudent(NewUser($"attendance-page-{Guid.NewGuid():N}@example.test")));
            pass.Status = "Present"; pass.CheckInTimestamp = ev.EventStart;
            _db.EventRegistrations.Add(pass);
        }
        ev.CurrentRegistrations = 27; await _db.SaveChangesAsync();
        var service = new AttendanceService(_db, new EventClock(TimeProvider.System));
        var first = await service.GetAttendanceAsync(ev.EventId, new() { Page = -1, From = "invalid" }, default);
        Assert.Equal(25, first!.Attendees.Count); Assert.Equal(1, first.Query.Page); Assert.Null(first.Query.From);
        Assert.Equal(first.Attendees.OrderByDescending(a => a.RegistrationId), first.Attendees);
        var last = await service.GetAttendanceAsync(ev.EventId, new() { Page = int.MaxValue }, default);
        Assert.Equal(2, last!.Query.Page); Assert.Equal(2, last.Attendees.Count); Assert.Equal(ev.EventStart, last.Attendees[^1].CheckedInAt);
        Assert.DoesNotContain(last.Attendees[0].RegistrationId, first.Attendees.Select(a => a.RegistrationId));
        var empty = await service.GetAttendanceAsync(ev.EventId, new() { Q = " " + new string('z', 200), Page = int.MaxValue }, default);
        Assert.Empty(empty!.Attendees); Assert.Equal(150, empty.Query.Q!.Length); Assert.Equal(1, empty.Query.Page); Assert.Equal(27, empty.CheckedIn);
    }

    [Fact]
    public async Task Analytics_counts_distributions_cohorts_and_filters_use_correct_denominators_without_writes()
    {
        var (ev, _) = await CreateGraphAsync(); ev.MaxCapacity = 60; ev.CurrentRegistrations = 30;
        for (var i = 0; i < 33; i++)
        {
            var student = NewStudent(NewUser($"analytics-{Guid.NewGuid():N}@example.test"));
            student.FirstName = $"Demo{i}"; student.MiddleName = null; student.LastName = "Researcher";
            student.Department = i % 2 == 0 ? "College A" : "College B"; student.Program = "Program " + i % 8;
            student.CampusBranch = "Campus " + i % 3;
            var pass = NewRegistration(ev, student); pass.CurrentYearLvl = i % 4 + 1;
            pass.Status = i < 18 ? "Present" : i < 30 ? "NoShow" : "Cancelled";
            pass.CheckInTimestamp = pass.Status == "NoShow" ? null : ev.EventStart.AddMinutes(i * 3);
            _db.EventRegistrations.Add(pass);
        }
        await _db.SaveChangesAsync(); var version = ev.RowVersion.ToArray();
        var service = new AnalyticsService(_db, new AttendanceService(_db, new EventClock(TimeProvider.System)));
        var all = await service.GetAsync(ev.EventId, new() { Cohort = "Reserved", Page = int.MaxValue }, default);
        Assert.NotNull(all); Assert.Equal(30, all.Reserved); Assert.Equal(18, all.Attended); Assert.Equal(12, all.Unscanned); Assert.Equal(3, all.Cancelled);
        Assert.Equal(60d, all.Turnout); Assert.Equal(30d, all.Occupancy); Assert.Equal(50d, all.BookingFill);
        Assert.Equal(100d * 3 / 33, all.Attrition, 6);
        Assert.Equal(6, all.Distributions["Program"].Count); Assert.Equal("Other Programs", all.Distributions["Program"][^1].Label);
        foreach (var groups in all.Distributions.Values) Assert.Equal(30, groups.Sum(g => g.Reserved));
        Assert.Equal(2, all.Query.Page); Assert.Equal(5, all.CohortRows.Count);
        var filtered = await service.GetAsync(ev.EventId, new() { Cohort = "Present", Q = " demo0 RESEARCHER ", College = "College A", Program = "Program 0", Interval = 99, From = "history", Dimension = "Campus" }, default);
        Assert.Single(filtered!.CohortRows); Assert.Equal(1, filtered.CohortRows[0].Year); Assert.Equal(15, filtered.Query.Interval);
        Assert.Equal(all.Reserved, filtered.Reserved); Assert.Equal("history", filtered.Query.From);
        Assert.Equal("Campus", filtered.Query.Dimension); Assert.Equal(ev.EventStart.AddMinutes(51), filtered.LatestCheckIn);
        var ticket = filtered.CohortRows[0].TicketReference(ev.EventId);
        var exact = await service.GetAsync(ev.EventId, new() { Q = ticket.ToLowerInvariant() }, default); Assert.Single(exact!.CohortRows);
        var cancelled = await service.GetAsync(ev.EventId, new() { Cohort = "Cancelled" }, default);
        Assert.Equal(3, cancelled!.Total); Assert.Equal(all.Arrivals.PeakCount, cancelled.Arrivals.PeakCount);
        var unscanned = await service.GetAsync(ev.EventId, new() { Cohort = "NoShow" }, default); Assert.Equal(12, unscanned!.Total);
        Assert.Equal(version, await _db.Events.Where(e => e.EventId == ev.EventId).Select(e => e.RowVersion).SingleAsync());
        Assert.Null(await service.GetAsync(int.MaxValue, new(), default));
    }

    [Fact]
    public async Task Empty_analytics_has_zero_rates_and_no_invented_arrival_series()
    {
        var (ev, _) = await CreateGraphAsync();
        var service = new AnalyticsService(_db, new AttendanceService(_db, new EventClock(TimeProvider.System)));
        var empty = await service.GetAsync(ev.EventId, new() { Cohort = "invalid", Q = new string('q', 200), From = "invalid", Page = -1 }, default);
        Assert.NotNull(empty); Assert.Equal(0d, empty.Turnout); Assert.Equal(0d, empty.Attrition); Assert.Equal(0d, empty.Occupancy); Assert.Equal(0d, empty.BookingFill);
        Assert.Empty(empty.Arrivals.Buckets); Assert.All(empty.Distributions.Values, groups => Assert.Empty(groups));
        Assert.Equal(150, empty.Query.Q!.Length); Assert.Equal("Present", empty.Query.Cohort); Assert.Null(empty.Query.From); Assert.Equal(1, empty.Query.Page);
        Assert.Equal("Program", empty.Query.Dimension); Assert.Null(empty.LatestCheckIn);
    }

    [Fact]
    public async Task Local_demo_data_is_additive_hashed_complete_and_idempotent()
    {
        var (original, _) = await CreateGraphAsync(); var version = original.RowVersion.ToArray();
        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
        var service = new DevelopmentDemoData(_db, hasher, new EventClock(TimeProvider.System));
        const string fixturePassword = "Fixture-only-preview-2030!";
        var result = await service.CreateAsync(fixturePassword);
        Assert.Equal(32, await _db.Students.CountAsync(s => s.StudentId.StartsWith("DEMO-")));
        Assert.Equal(33, await _db.Users.CountAsync(u => u.Email == DevelopmentDemoData.AdminEmail || u.Email.StartsWith("demo.student")));
        var demoEvents = await _db.Events.AsNoTracking().Where(e => e.EventId == result.LiveEventId || e.EventId == result.CompletedEventId || e.EventId == result.CancelledEventId).ToListAsync();
        Assert.Equal(3, demoEvents.Count); Assert.Equal(76, await _db.EventRegistrations.CountAsync(r => demoEvents.Select(e => e.EventId).Contains(r.EventId)));
        foreach (var ev in demoEvents)
        {
            Assert.StartsWith("Sample · ", ev.Title);
            Assert.Equal(ev.CurrentRegistrations, await _db.EventRegistrations.CountAsync(r => r.EventId == ev.EventId && r.Status != "Cancelled"));
        }
        var admin = await _db.Users.AsNoTracking().SingleAsync(u => u.Email == DevelopmentDemoData.AdminEmail);
        Assert.NotEqual(fixturePassword, admin.PasswordHash);
        if (result.Created) Assert.NotEqual(Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(admin, admin.PasswordHash, fixturePassword));
        var again = await service.CreateAsync("Different-fixture-password!");
        Assert.False(again.Created); Assert.Equal(result.LiveEventId, again.LiveEventId); Assert.Equal(result.CompletedEventId, again.CompletedEventId); Assert.Equal(result.CancelledEventId, again.CancelledEventId);
        Assert.Equal(admin.PasswordHash, await _db.Users.Where(u => u.Email == DevelopmentDemoData.AdminEmail).Select(u => u.PasswordHash).SingleAsync());
        Assert.Equal(version, await _db.Events.Where(e => e.EventId == original.EventId).Select(e => e.RowVersion).SingleAsync());
    }

    private StudentService StudentDirectoryService() => new(_db, new Microsoft.AspNetCore.Identity.PasswordHasher<User>(), new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(), new EventClock(TimeProvider.System));
    private static StudentProfileInput DirectoryInput(string? id = null) => new() { StudentId = id ?? $"DIR-{Guid.NewGuid():N}", Email = $"directory-{Guid.NewGuid():N}@example.test", FirstName = "Mikaela", LastName = "Dizon", BirthDate = new DateOnly(2005, 4, 14), Gender = "Female", Campus = "San Bartolome", College = "College of Computer Studies", Program = "BSIT", Password = "Fixture-student-password!" };
    private static StudentProfileInput DirectoryEdit(StudentProfileData s) => new() { StudentId = s.StudentId, Email = s.Email, FirstName = s.FirstName, MiddleName = s.MiddleName, LastName = s.LastName, BirthDate = s.BirthDate, Gender = s.Gender, Campus = s.Campus, College = s.College, Program = s.Program, Active = s.Active, Token = s.Token };

    [Fact]
    public async Task Student_directory_creates_linked_accounts_and_preserves_history_with_stale_snapshot_protection()
    {
        var service = StudentDirectoryService(); var input = DirectoryInput(); input.Active = false;
        await service.CreateAsync(input, default);
        var stored = await _db.Students.AsNoTracking().Include(s => s.User).SingleAsync(s => s.StudentId == input.StudentId);
        Assert.Equal("Student", stored.User.Role); Assert.False(stored.User.IsActive); Assert.NotEqual(input.Password, stored.User.PasswordHash);
        var profile = Assert.Single((await service.GetAsync(new() { Q = input.StudentId }, default)).Students);
        _db.ChangeTracker.Clear(); await service.UpdateAsync(input.StudentId, new() { Token = profile.Token, Active = true }, "access", default);
        _db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.UpdateAsync(input.StudentId, new() { Token = profile.Token, Active = false }, "access", default));
        profile = Assert.Single((await service.GetAsync(new() { Q = input.StudentId }, default)).Students);
        var (ev, _) = await CreateGraphAsync();
        _db.EventRegistrations.Add(new EventRegistration { EventId = ev.EventId, StudentId = input.StudentId, CurrentYearLvl = 3, CurrentSection = "SBIT-3A" }); await _db.SaveChangesAsync();
        var edit = DirectoryEdit(profile); edit.FirstName = "Mikaela Ana"; edit.Email = $"edited-{Guid.NewGuid():N}@example.test";
        _db.ChangeTracker.Clear(); await service.UpdateAsync(input.StudentId, edit, "edit", default);
        Assert.Equal(edit.Email, await _db.Users.Where(u => u.UserId == stored.UserId).Select(u => u.Email).SingleAsync());
        Assert.True(await _db.EventRegistrations.AnyAsync(r => r.StudentId == input.StudentId && r.CurrentYearLvl == 3));
        var duplicate = DirectoryInput(input.StudentId); await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.CreateAsync(duplicate, default));
        Assert.False(await _db.Users.AnyAsync(u => u.Email == duplicate.Email));
    }

    [Fact]
    public async Task Student_directory_password_and_identity_changes_are_bound_validated_and_atomic()
    {
        var service = StudentDirectoryService(); var input = DirectoryInput(); await service.CreateAsync(input, default);
        var second = DirectoryInput(); await service.CreateAsync(second, default);
        var profile = Assert.Single((await service.GetAsync(new() { Q = input.StudentId }, default)).Students);
        _db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.UpdateAsync(second.StudentId, new() { Token = profile.Token, Active = false }, "access", default));
        var edit = DirectoryEdit(profile); edit.FirstName = "Must not save"; edit.Email = second.Email;
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.UpdateAsync(input.StudentId, edit, "edit", default));
        Assert.Equal(input.FirstName, await _db.Students.Where(s => s.StudentId == input.StudentId).Select(s => s.FirstName).SingleAsync());
        _db.ChangeTracker.Clear(); const string replacement = "Replacement-fixture-password!";
        await service.UpdateAsync(input.StudentId, new() { Token = profile.Token, Password = replacement }, "password", default);
        var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>(); var accounts = new AccountService(_db, hasher);
        Assert.Null(await accounts.AuthenticateAsync(input.Email, input.Password!, default));
        Assert.NotNull(await accounts.AuthenticateAsync(input.StudentId, replacement, default));
        _db.ChangeTracker.Clear(); await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.UpdateAsync(input.StudentId, DirectoryEdit(profile), "edit", default));
        var invalid = DirectoryInput(); invalid.College = "College of Engineering";
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => service.CreateAsync(invalid, default));
        Assert.False(await _db.Users.AnyAsync(u => u.Email == invalid.Email));
    }

    [Fact]
    public async Task Student_directory_filters_and_pages_independently_of_global_options()
    {
        var marker = Guid.NewGuid().ToString("N");
        for (var i = 0; i < 27; i++) { var student = NewStudent(NewUser($"dir-page-{Guid.NewGuid():N}@example.test")); student.User.Role = "Student"; student.LastName = marker; student.FirstName = $"Student {i:D2}"; student.User.IsActive = i % 2 == 0; _db.Students.Add(student); }
        await _db.SaveChangesAsync(); var service = StudentDirectoryService();
        var all = await service.GetAsync(new() { Q = marker, Page = -1 }, default); Assert.Equal(27, all.Total); Assert.Equal(25, all.Students.Count);
        var last = await service.GetAsync(new() { Q = marker, Page = int.MaxValue }, default); Assert.Equal(2, last.Query.Page); Assert.Equal(2, last.Students.Count);
        var active = await service.GetAsync(new() { Q = marker, College = "College of Computer Studies", Program = "BS Information Technology", Campus = "San Bartolome", Status = "Active" }, default);
        Assert.Equal(14, active.Total); Assert.Contains("College of Computer Studies", active.Colleges);
        Assert.Empty((await service.GetAsync(new() { Q = marker, Campus = "Batasan" }, default)).Students);
    }

    [Fact]
    public async Task Student_csv_quotes_legacy_dates_duplicates_and_partial_success_leave_no_orphans()
    {
        var service = StudentDirectoryService(); var csv = new StudentCsvService(service);
        var id = $"CSV-{Guid.NewGuid():N}"; var email = $"csv-{Guid.NewGuid():N}@example.test";
        var text = StudentCsvService.Header + "\r\n" + $"{id},\"Althea, Ana\",,\"De \"\"la\"\", Cruz\",Female,San Bartolome,College of Computer Studies,BSIT,{email},2005-04-14\r\n" +
            $"BAD-{id},Lara,,Reyes,Female,San Bartolome,College of Computer Studies,BSIT,bad-{email},wrong-date\r\n" +
            $"{id},Duplicate,,Name,Female,San Bartolome,College of Computer Studies,BSIT,duplicate-{email},2005-04-14\r\n";
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)); var report = await csv.ImportAsync(stream, "Fixture-import-password!", default);
        Assert.Equal(1, report.Created); Assert.Equal(2, report.Errors.Count);
        var saved = await _db.Students.AsNoTracking().SingleAsync(s => s.StudentId == id); Assert.Equal("Althea, Ana", saved.FirstName); Assert.Equal("De \"la\", Cruz", saved.LastName);
        Assert.False(await _db.Users.AnyAsync(u => u.Email == "duplicate-" + email || u.Email == "bad-" + email));
        var legacy = "StudentId,FirstName,MiddleName,LastName,Gender,CampusBranch,Department,Program,YearLevel,Section,Email,BirthDate\r\n" + $"LEG-{id},Rafael,,Mendoza,,,College of Engineering,BSIE,99,Ignored,legacy-{email},\r\n";
        using var old = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(legacy)); Assert.Equal(1, (await csv.ImportAsync(old, "Fixture-import-password!", default)).Created);
        var legacyStudent = await _db.Students.AsNoTracking().SingleAsync(s => s.StudentId == "LEG-" + id); Assert.Null(legacyStudent.BirthDate); Assert.Equal("Not Specified", legacyStudent.Gender); Assert.Equal("San Bartolome", legacyStudent.CampusBranch);
    }

    [Fact]
    public async Task Student_csv_rejects_wrong_schema_and_oversized_batches_before_writes_and_exports_safe_cells()
    {
        var service = StudentDirectoryService(); var csv = new StudentCsvService(service); var input = DirectoryInput(); input.FirstName = "=FORMULA()"; await service.CreateAsync(input, default);
        var exported = System.Text.Encoding.UTF8.GetString(await service.ExportAsync(new() { Q = input.StudentId }, default));
        Assert.Contains("'=" , exported); Assert.Contains("BirthDate(MM/dd/yyyy)", exported); Assert.DoesNotContain(input.Password!, exported); Assert.DoesNotContain("PasswordHash", exported);
        using var wrong = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("wrong,header\r\n"));
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => csv.ImportAsync(wrong, "Fixture-import-password!", default));
        var many = StudentCsvService.Header + "\r\n" + string.Concat(Enumerable.Repeat("x,a,,b,Female,San Bartolome,College of Computer Studies,BSIT,x@example.test,2005-04-14\r\n", 201));
        using var large = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(many));
        await Assert.ThrowsAsync<System.ComponentModel.DataAnnotations.ValidationException>(() => csv.ImportAsync(large, "Fixture-import-password!", default));
        Assert.False(await _db.Users.AnyAsync(u => u.Email == "x@example.test"));
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
