using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using MVC_QCUEventsManagement.Models;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace LocalAdminPreview;

internal sealed class PreviewDataset
{
    public required User Admin { get; init; }
    public required User InactiveAdmin { get; init; }
    public required List<Student> Students { get; init; }
    public required List<Event> Events { get; init; }
    public required string Marker { get; init; }
    public required string StudentPrefix { get; init; }

    public static PreviewDataset Create(string app, string batch, string password, DateTime now, string banner)
    {
        var marker = $"[ADMIN-PREVIEW:{app}:{batch}]";
        var prefix = $"PV-{app}-{batch}-";
        User Account(string suffix, string role, bool active = true)
        {
            var user = new User { Email = $"preview.{app}.{batch}.{suffix}@example.test", Role = role,
                IsActive = active, PasswordHash = "", PasswordSalt = "" };
            if (app == "mvc") user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password);
            else
            {
                var salt = RandomNumberGenerator.GetBytes(32);
                user.PasswordSalt = Convert.ToBase64String(salt);
                // Match the original Web Forms PasswordHelper; never replace existing credentials.
                user.PasswordHash = Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password, salt, 10000, HashAlgorithmName.SHA1, 32));
            }
            return user;
        }
        var names = new[] { ("Mikaela", "Dizon"), ("Rafael", "Mendoza"), ("Althea", "Navarro"), ("Gabriel", "Santos"),
            ("Bianca", "Reyes"), ("Enzo", "Villanueva"), ("Camille", "Bautista"), ("Nico", "Soriano"),
            ("Isabella", "Ramos"), ("Paolo", "Aquino"), ("Jasmine", "Castillo"), ("Adrian", "Flores"),
            ("Trisha", "Garcia"), ("Miguel", "Torres"), ("Sofia", "Mercado"), ("Ethan", "Valdez"),
            ("Lara", "Fernandez"), ("Joshua", "Manalo"), ("Clarisse", "Lim"), ("Marco", "Pascual"),
            ("Andrea", "Cruz"), ("Daniel", "Salazar"), ("Patricia", "Alonzo"), ("Luis", "Domingo"),
            ("Nicole", "Tolentino"), ("Carlo", "Velasco"), ("Alyssa", "Gonzales"), ("Francis", "Ignacio"),
            ("Beatrice", "David"), ("Jerome", "Padilla"), ("Denise", "De Leon"), ("Vincent", "Rosales"),
            ("Angela", "Mercado"), ("Sebastian", "Rivera"), ("Celine", "Valencia"), ("Andrei", "Soriano") };
        var students = names.Select((name, index) =>
        {
            var program = CreateEventInput.ProgramOptions[index % CreateEventInput.ProgramOptions.Length];
            return new Student { StudentId = prefix + (index + 1).ToString("D2"), FirstName = name.Item1,
                MiddleName = index % 5 == 0 ? "Cruz" : null, LastName = name.Item2,
                Gender = index % 2 == 0 ? "Female" : "Male", CampusBranch = CreateEventInput.Branches[index % 3],
                Department = program.Department, Program = program.Code, BirthDate = new DateOnly(2003 + index % 4, index % 12 + 1, index % 25 + 1),
                User = Account($"student{index + 1:D2}", "Student", index < 34) };
        }).ToList();
        var admin = Account("admin", "Admin");
        var inactiveAdmin = Account("inactive-admin", "Admin", false);
        var events = new List<Event>();
        Event Add(string name, int day, string status = "Upcoming", int capacity = 120)
        {
            var start = now.Date.AddDays(day).AddHours(9);
            var ev = new Event { Title = $"Preview {batch} · {name}", Description = marker + "\nFictional local preview. " +
                "Explore event details, audience, sponsors, rosters, attendance, analytics and history. All preview people are fictional.",
                VenueLocation = events.Count % 2 == 0 ? "Main Auditorium" : "Multipurpose Hall", MaxCapacity = capacity,
                CreatedByUser = admin, EventStart = start, EventEnd = start.AddHours(8),
                RegStart = DateOnly.FromDateTime(start.AddDays(-14)), RegEnd = DateOnly.FromDateTime(start.AddDays(-1)),
                Status = status, CancellationReason = status == "Cancelled" ? "Preview cancellation: scheduled venue maintenance." : null,
                EventPhotoPath = banner };
            ev.Sponsors.Add(new Sponsor { SponsorName = "Preview Student Research Council", CreatedAt = now });
            ev.Sponsors.Add(new Sponsor { SponsorName = "Preview QCU Alumni Association", CreatedAt = now });
            events.Add(ev); return ev;
        }
        void Book(Event ev, int count, int present, int cancelled = 0)
        {
            for (var i = 0; i < count; i++)
            {
                var state = i >= count - cancelled ? "Cancelled" : i < present ? "Present" : "NoShow";
                // Spread arrivals without creating timestamps in the future.
                var arrival = ev.EventStart > now ? now.AddMinutes(-30 + i) : ev.EventStart.AddMinutes(3 + i * 2);
                if (arrival > now) arrival = now.AddSeconds(-1);
                ev.Registrations.Add(new EventRegistration { Student = students[i], StudentId = students[i].StudentId,
                    CurrentYearLvl = i % 5 + 1, CurrentSection = $"{students[i].Program}-{i % 5 + 1}{(i % 2 == 0 ? "A" : "B")}",
                    Status = state, CheckInTimestamp = state == "Present" ? arrival : null });
            }
            ev.CurrentRegistrations = ev.Registrations.Count(r => r.Status != "Cancelled");
        }
        var open = Add("Open registration · Career fair", 3); Book(open, 24, 0, 2);
        var soon = Add("Registration soon · Research showcase", 10);
        soon.RegStart = DateOnly.FromDateTime(now.AddDays(2)); Book(soon, 0, 0);
        var closed = Add("Registration closed · Leadership forum", 1);
        closed.RegEnd = DateOnly.FromDateTime(now.AddDays(-1)); Book(closed, 28, 0, 3);
        var ongoing = Add("Ongoing · Campus innovation day", 0);
        ongoing.EventStart = now.Hour >= 2 ? now.AddHours(-2) : now.Date;
        ongoing.EventEnd = now.Date.AddDays(1).AddSeconds(-1);
        ongoing.RegStart = DateOnly.FromDateTime(now.AddDays(-14)); ongoing.RegEnd = DateOnly.FromDateTime(now.AddDays(-1));
        Book(ongoing, 36, 18, 4);
        var completed = Add("Completed · Community technology summit", -7, "Completed"); Book(completed, 36, 26, 3);
        var cancelled = Add("Cancelled · Student leadership workshop", 7, "Cancelled"); Book(cancelled, 14, 0, 2);
        var practice = Add("QR scanner practice · UNSCANNED PASSES", 1, capacity: 80); Book(practice, 14, 4, 2);
        var full = Add("Fully booked · Maker laboratory", 4, capacity: 8); Book(full, 8, 0);
        var restricted = Add("Restricted audience · Computing seminar", 6);
        restricted.TargetBranch = "San Bartolome"; restricted.TargetDepartment = "College of Computer Studies";
        restricted.TargetProgram = "BSIT,BSCS"; restricted.TargetYearLevel = 2;
        return new() { Admin = admin, InactiveAdmin = inactiveAdmin, Students = students, Events = events,
            Marker = marker, StudentPrefix = prefix };
    }
}
