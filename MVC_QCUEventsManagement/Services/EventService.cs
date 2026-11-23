using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.ViewModels.Admin;
using MVC_QCUEventsManagement.Models;
using System.ComponentModel.DataAnnotations;

namespace MVC_QCUEventsManagement.Services;

public sealed class EventService(ApplicationDbContext db, EventClock clock)
{
    private const int PageSize = 25;

    public async Task<EventDetailsViewModel?> GetDetailsAsync(int id, bool editing, CancellationToken ct)
    {
        var ev = await db.Events.AsNoTracking().Where(e => e.EventId == id)
            .Select(e => new EventDetailsData(e.EventId, e.Title, e.Description, e.VenueLocation, e.MaxCapacity,
                e.CurrentRegistrations, e.EventStart, e.EventEnd, e.RegStart, e.RegEnd, e.Status, e.CancellationReason,
                e.TargetBranch, e.TargetDepartment, e.TargetProgram, e.TargetYearLevel, e.EventPhotoPath, e.RowVersion,
                e.Sponsors.OrderBy(s => s.SponsorEntryId).Select(s => s.SponsorName).ToList())).SingleOrDefaultAsync(ct);
        if (ev is null) return null;
        return new EventDetailsViewModel
        {
            Event = ev, CanManage = ev.Status == "Upcoming" && ev.End > clock.Now,
            Editing = editing && ev.Status == "Upcoming" && ev.End > clock.Now,
            Input = new CreateEventInput { Title = ev.Title, Description = ev.Description, VenueLocation = ev.Venue,
                MaxCapacity = ev.Capacity, EventDate = DateOnly.FromDateTime(ev.Start), StartTime = TimeOnly.FromDateTime(ev.Start),
                EndTime = TimeOnly.FromDateTime(ev.End), RegStart = ev.RegistrationOpen, RegEnd = ev.RegistrationDeadline,
                TargetBranch = ev.Campus, TargetDepartment = ev.College, Programs = ev.Programs?.Split(',').ToList() ?? [],
                TargetYearLevel = ev.Year, Sponsors = ev.Sponsors.ToList(), Confirm = true }
        };
    }

    public async Task<string?> EditAsync(int id, CreateEventInput input, byte[] version, string? banner, bool removeBanner, CancellationToken ct)
    {
        Validator.ValidateObject(input, new ValidationContext(input), true);
        var ev = await db.Events.AsTracking().Include(e => e.Sponsors).SingleOrDefaultAsync(e => e.EventId == id, ct)
            ?? throw new KeyNotFoundException();
        if (ev.Status != "Upcoming" || ev.EventEnd <= clock.Now) throw new ValidationException("This event can no longer be edited.");
        if (input.MaxCapacity < ev.CurrentRegistrations) throw new ValidationException("Capacity cannot be lower than the number of registered students.");
        var end = input.EventDate!.Value.ToDateTime(input.EndTime!.Value);
        if (end <= clock.Now) throw new ValidationException("Choose an event that has not ended.");
        db.Entry(ev).Property(e => e.RowVersion).OriginalValue = version;
        var oldBanner = ev.EventPhotoPath;
        ev.Title = input.Title.Trim(); ev.Description = input.Description?.Trim(); ev.VenueLocation = input.VenueLocation.Trim();
        ev.MaxCapacity = input.MaxCapacity; ev.EventStart = input.EventDate.Value.ToDateTime(input.StartTime!.Value); ev.EventEnd = end;
        ev.RegStart = input.RegStart!.Value; ev.RegEnd = input.RegEnd!.Value;
        ev.TargetBranch = string.IsNullOrWhiteSpace(input.TargetBranch) ? null : input.TargetBranch;
        ev.TargetDepartment = string.IsNullOrWhiteSpace(input.TargetDepartment) ? null : input.TargetDepartment;
        ev.TargetProgram = input.Programs.Count == 0 ? null : string.Join(',', input.Programs.Distinct()); ev.TargetYearLevel = input.TargetYearLevel;
        if (banner is not null || removeBanner) ev.EventPhotoPath = banner;
        db.Sponsors.RemoveRange(ev.Sponsors);
        ev.Sponsors = input.Sponsors.Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(s => new Sponsor { SponsorName = s, CreatedAt = clock.Now }).ToList();
        await db.SaveChangesAsync(ct);
        return oldBanner != ev.EventPhotoPath ? oldBanner : null;
    }

    public async Task CancelAsync(int id, string? reason, byte[] version, CancellationToken ct)
    {
        reason = reason?.Trim();
        if (string.IsNullOrEmpty(reason) || reason.Length > 500) throw new ValidationException("Enter a cancellation reason of 1 to 500 characters.");
        var ev = await db.Events.AsTracking().SingleOrDefaultAsync(e => e.EventId == id, ct) ?? throw new KeyNotFoundException();
        if (ev.Status != "Upcoming" || ev.EventEnd <= clock.Now) throw new ValidationException("This event can no longer be cancelled.");
        db.Entry(ev).Property(e => e.RowVersion).OriginalValue = version;
        ev.Status = "Cancelled"; ev.CancellationReason = reason;
        await db.SaveChangesAsync(ct);
    }

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
            RegStart = input.RegStart!.Value,
            RegEnd = input.RegEnd!.Value,
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
        var today = DateOnly.FromDateTime(now);
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
                Status = ev.CurrentRegistrations >= ev.MaxCapacity || ev.RegEnd < today
                    ? RegistrationStatus.Closed
                    : ev.RegStart > today ? RegistrationStatus.Soon : RegistrationStatus.Open
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
                row.Event.CurrentRegistrations, row.Event.MaxCapacity, row.Status, row.Event.EventId))
            .ToListAsync(cancellationToken);

        return new EventListViewModel(search, selectedStatus, counts, events, page, totalPages, total);
    }
}
