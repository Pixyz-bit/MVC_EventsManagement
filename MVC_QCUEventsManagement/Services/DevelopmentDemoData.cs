using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.Models;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Services;

public sealed class DevelopmentDemoData(ApplicationDbContext db, IPasswordHasher<User> hasher, EventClock clock)
{
    public const string AdminEmail = "demo.admin@example.test";
    private const string Marker = "[MVC-DEMO-V1]";

    public async Task<DemoDataResult> CreateAsync(string password, CancellationToken ct = default)
    {
        var connection = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
        if (connection.InitialCatalog != "MVC_EventsManagement" || connection.DataSource is not ("localhost" or "." or "(local)"))
            throw new InvalidOperationException("Demo data can only target the local MVC_EventsManagement database.");
        if (password.Length is < 15 or > 128) throw new ArgumentException("Use a demo password of 15–128 characters.", nameof(password));
        // SQL integration fixtures may already own a rollback transaction.
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var existing = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == AdminEmail, ct);
        if (existing is not null)
        {
            var events = await db.Events.AsNoTracking().Where(e => e.CreatedByUserId == existing.UserId && e.Description != null && e.Description.StartsWith(Marker)).Select(e => new { e.EventId, e.Title }).ToListAsync(ct);
            if (existing.Role != "Admin" || events.Count != 3) throw new InvalidOperationException("The demo namespace is already in use; existing records were not changed.");
            return new(false, events.Single(e => e.Title == "Sample · Campus Research Forum").EventId,
                events.Single(e => e.Title == "Sample · Innovation & Career Summit").EventId,
                events.Single(e => e.Title == "Sample · Community Leadership Workshop").EventId);
        }
        if (await db.Users.AnyAsync(u => u.Email.StartsWith("demo.student"), ct) || await db.Students.AnyAsync(s => s.StudentId.StartsWith("DEMO-"), ct))
            throw new InvalidOperationException("The demo namespace is already in use; existing records were not changed.");
        User Account(string email, string role)
        {
            var user = new User { Email = email, Role = role, PasswordHash = "", PasswordSalt = "" };
            user.PasswordHash = hasher.HashPassword(user, password); return user;
        }
        var admin = Account(AdminEmail, "Admin");
        var names = new[] { ("Mikaela", "Dizon"), ("Rafael", "Mendoza"), ("Althea", "Navarro"), ("Gabriel", "Santos"),
            ("Bianca", "Reyes"), ("Enzo", "Villanueva"), ("Camille", "Bautista"), ("Nico", "Soriano"),
            ("Isabella", "Ramos"), ("Paolo", "Aquino"), ("Jasmine", "Castillo"), ("Adrian", "Flores"),
            ("Trisha", "Garcia"), ("Miguel", "Torres"), ("Sofia", "Mercado"), ("Ethan", "Valdez"),
            ("Lara", "Fernandez"), ("Joshua", "Manalo"), ("Clarisse", "Lim"), ("Marco", "Pascual"),
            ("Andrea", "Cruz"), ("Daniel", "Salazar"), ("Patricia", "Alonzo"), ("Luis", "Domingo"),
            ("Nicole", "Tolentino"), ("Carlo", "Velasco"), ("Alyssa", "Gonzales"), ("Francis", "Ignacio"),
            ("Beatrice", "David"), ("Jerome", "Padilla"), ("Denise", "De Leon"), ("Vincent", "Rosales") };
        var students = names.Select((name, i) => {
            var program = CreateEventInput.ProgramOptions[i % CreateEventInput.ProgramOptions.Length];
            return new Student { StudentId = $"DEMO-2026-{i + 1:D4}", FirstName = name.Item1, LastName = name.Item2,
                Gender = i % 2 == 0 ? "Female" : "Male", CampusBranch = CreateEventInput.Branches[i % CreateEventInput.Branches.Length],
                Department = program.Department, Program = program.Code, BirthDate = new DateOnly(2003 + i % 4, i % 12 + 1, i % 25 + 1),
                User = Account($"demo.student{i + 1:D2}@example.test", "Student") };
        }).ToList();
        var now = clock.Now;
        Event DemoEvent(string title, string venue, DateTime start, DateTime end, string status = "Upcoming") => new() {
            Title = "Sample · " + title, Description = Marker + " Fictional preview event and students for local design review. No real attendees.",
            VenueLocation = venue, MaxCapacity = 150, EventStart = start, EventEnd = end,
            RegStart = DateOnly.FromDateTime(start.AddDays(-14)), RegEnd = DateOnly.FromDateTime(start.AddDays(-1)),
            CreatedByUser = admin, Status = status, CancellationReason = status == "Cancelled" ? "Sample cancellation: venue maintenance." : null };
        var live = DemoEvent("Campus Research Forum", "San Bartolome · Main Auditorium", now.AddHours(-2), now.AddHours(6));
        var completed = DemoEvent("Innovation & Career Summit", "Batasan · Multipurpose Hall", now.Date.AddDays(-7).AddHours(9), now.Date.AddDays(-7).AddHours(17), "Completed");
        var cancelled = DemoEvent("Community Leadership Workshop", "San Francisco · Seminar Room", now.Date.AddDays(10).AddHours(9), now.Date.AddDays(10).AddHours(12), "Cancelled");
        foreach (var ev in new[] { live, completed, cancelled })
        {
            var size = ev == cancelled ? 12 : 32;
            for (var i = 0; i < size; i++)
            {
                var status = i >= (ev == cancelled ? 10 : 28) ? "Cancelled" : ev == cancelled ? "NoShow" : i < (ev == live ? 20 : 24) ? "Present" : "NoShow";
                var year = i % 4 + 1;
                ev.Registrations.Add(new EventRegistration { Student = students[i], StudentId = students[i].StudentId,
                    CurrentYearLvl = year, CurrentSection = $"{students[i].Program}-{year}{(i % 2 == 0 ? "A" : "B")}",
                    Status = status, CheckInTimestamp = status == "Present" ? ev.EventStart.AddMinutes(5 + i * 3 + i % 4 * 2) : null });
            }
            ev.CurrentRegistrations = ev.Registrations.Count(r => r.Status != "Cancelled");
            ev.Sponsors.Add(new Sponsor { SponsorName = "Sample Student Research Council", CreatedAt = now });
        }
        db.Events.AddRange(live, completed, cancelled);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        return new(true, live.EventId, completed.EventId, cancelled.EventId);
    }
}
