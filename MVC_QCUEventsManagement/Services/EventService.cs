using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Services;

public sealed class EventService(ApplicationDbContext db, EventClock clock)
{
    private const int PageSize = 25;

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
