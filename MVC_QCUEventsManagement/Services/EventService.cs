using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.ViewModels.Admin;
using MVC_QCUEventsManagement.Models;
using System.ComponentModel.DataAnnotations;

namespace MVC_QCUEventsManagement.Services;

public sealed class EventService(ApplicationDbContext db, EventClock clock)
{
    private const int PageSize = 25;

    public async Task<int> CreateAsync(CreateEventInput input, int adminId, string? bannerName, CancellationToken cancellationToken)
    {
        Validator.ValidateObject(input, new ValidationContext(input), validateAllProperties: true);
        var start = input.EventDate!.Value.ToDateTime(input.StartTime!.Value);
        var end = input.EventDate.Value.ToDateTime(input.EndTime!.Value);
        if (end <= clock.Now) throw new ValidationException("Choose an event that has not ended.");
        if (!await db.Users.AnyAsync(user => user.UserId == adminId && user.Role == "Admin" && user.IsActive, cancellationToken))
            throw new UnauthorizedAccessException();
        static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        var ev = new Event
        {
            Title = input.Title.Trim(), Description = Optional(input.Description), VenueLocation = input.VenueLocation.Trim(),
            MaxCapacity = input.MaxCapacity, CreatedByUserId = adminId, EventStart = start, EventEnd = end,
            RegStart = DateTime.SpecifyKind(input.RegStart!.Value, DateTimeKind.Unspecified),
            RegEnd = DateTime.SpecifyKind(input.RegEnd!.Value, DateTimeKind.Unspecified),
            TargetBranch = Optional(input.TargetBranch), TargetDepartment = Optional(input.TargetDepartment),
            TargetProgram = input.Programs.Count == 0 ? null : string.Join(",", input.Programs.Distinct()),
            TargetYearLevel = input.TargetYearLevel, EventPhotoPath = bannerName,
            Sponsors = input.Sponsors.Select(name => name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => new Sponsor { SponsorName = name, CreatedAt = clock.Now }).ToList()
        };
        db.Events.Add(ev);
        // EF writes the event and all sponsors in the same SaveChanges transaction.
        await db.SaveChangesAsync(cancellationToken);
        return ev.EventId;
    }

    public Task<string?> GetBannerAsync(int id, CancellationToken cancellationToken) => db.Events.AsNoTracking()
        .Where(ev => ev.EventId == id).Select(ev => ev.EventPhotoPath).SingleOrDefaultAsync(cancellationToken);

    public async Task<EventListViewModel> GetUpcomingAsync(string? query, string? status, int page, CancellationToken cancellationToken)
    {
        var now = clock.Now;
        var search = query?.Trim() ?? "";
        if (search.Length > 150) search = search[..150];

        var selectedStatus = status?.Trim().ToUpperInvariant() switch
        {
            "OPEN" => RegistrationStatus.Open,
            "SOON" => RegistrationStatus.Soon,
            "CLOSED" or "CLOSE" => RegistrationStatus.Closed,
            _ => (RegistrationStatus?)null
        };

        var upcoming = db.Events.AsNoTracking()
            .Where(ev => ev.Status == "Upcoming" && ev.EventEnd > now)
            .Select(ev => new
            {
                Event = ev,
                Status = ev.CurrentRegistrations >= ev.MaxCapacity || ev.RegEnd < now
                    ? RegistrationStatus.Closed
                    : ev.RegStart > now ? RegistrationStatus.Soon : RegistrationStatus.Open
            });

        var counts = await upcoming.GroupBy(row => row.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Status, group => group.Count, cancellationToken);
        var matching = upcoming;
        if (selectedStatus is { } filter) matching = matching.Where(row => row.Status == filter);
        if (search.Length > 0)
        {
            matching = matching.Where(row =>
                EF.Functions.Collate(row.Event.Title, "Latin1_General_100_CI_AS").Contains(search)
                || EF.Functions.Collate(row.Event.VenueLocation, "Latin1_General_100_CI_AS").Contains(search)
                || (row.Event.TargetDepartment != null
                    && EF.Functions.Collate(row.Event.TargetDepartment, "Latin1_General_100_CI_AS").Contains(search)));
        }

        var total = search.Length > 0 ? await matching.CountAsync(cancellationToken)
            : selectedStatus is { } current ? counts.GetValueOrDefault(current) : counts.Values.Sum();
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var events = await matching.OrderBy(row => row.Event.EventStart).ThenBy(row => row.Event.EventId)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(row => new EventListItem(row.Event.Title, row.Event.VenueLocation,
                row.Event.TargetDepartment, row.Event.EventStart, row.Event.RegStart, row.Event.RegEnd,
                row.Event.CurrentRegistrations, row.Event.MaxCapacity, row.Status))
            .ToListAsync(cancellationToken);

        return new EventListViewModel(search, selectedStatus, counts, events, page, totalPages, total);
    }
}
