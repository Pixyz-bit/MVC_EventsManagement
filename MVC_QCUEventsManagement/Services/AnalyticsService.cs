using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.ViewModels.Admin;

namespace MVC_QCUEventsManagement.Services;

public sealed class AnalyticsService(ApplicationDbContext db, AttendanceService attendance)
{
    public async Task<EventAnalyticsViewModel?> GetAsync(int id, EventAnalyticsQuery input, CancellationToken ct)
    {
        var context = await attendance.GetScannerAsync(id, input.From == "history", ct);
        if (context is null) return null;
        static string? Limit(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, 150)];
        var query = new EventAnalyticsQuery { Interval = input.Interval is 5 or 15 or 30 ? input.Interval : 15,
            Dimension = input.Dimension is "College" or "Campus" or "Year level" ? input.Dimension : "Program",
            Cohort = input.Cohort is "NoShow" or "Cancelled" or "Reserved" ? input.Cohort : "Present",
            Q = Limit(input.Q), College = Limit(input.College), Program = Limit(input.Program), From = input.From == "history" ? "history" : null };
        var rows = await db.EventRegistrations.AsNoTracking().Where(r => r.EventId == id)
            .Select(r => new AnalyticsRegistration(r.EventRegistrationId, r.StudentId,
                r.Student.FirstName + " " + (r.Student.MiddleName == null || r.Student.MiddleName == "" ? "" : r.Student.MiddleName + " ") + r.Student.LastName,
                r.Student.CampusBranch, r.Student.Department, r.Student.Program, r.CurrentYearLvl, r.CurrentSection, r.Status, r.CheckInTimestamp)).ToListAsync(ct);
        var active = rows.Where(r => r.Status != "Cancelled").ToList();
        static IReadOnlyList<AnalyticsGroup> Group(IEnumerable<AnalyticsRegistration> cohort, Func<AnalyticsRegistration, string> key) => cohort.GroupBy(key)
            .Select(g => new AnalyticsGroup(g.Key, g.Count(), g.Count(r => r.Status == "Present")))
            .OrderByDescending(g => g.Reserved).ThenBy(g => g.Label, StringComparer.Ordinal).ToList();
        var programs = Group(active, r => r.Program);
        if (programs.Count > 6) programs = programs.Take(5).Append(new AnalyticsGroup("Other Programs", programs.Skip(5).Sum(g => g.Reserved), programs.Skip(5).Sum(g => g.Attended))).ToList();
        var distributions = new Dictionary<string, IReadOnlyList<AnalyticsGroup>> {
            ["College"] = Group(active, r => r.College), ["Program"] = programs,
            ["Campus"] = Group(active, r => r.Campus), ["Year level"] = Group(active, r => $"Year {r.Year}").OrderBy(g => g.Label).ToList() };
        var present = rows.Where(r => r.Status == "Present").ToList();
        var arrivals = BuildArrivals(present.Where(r => r.CheckedInAt.HasValue).Select(r => r.CheckedInAt!.Value), query.Interval, input.ArrivalPage);
        query.ArrivalPage = arrivals.Page;
        var matching = rows.Where(r => query.Cohort == "Reserved" ? r.Status != "Cancelled" : r.Status == query.Cohort);
        if (query.Q is { } text) matching = matching.Where(r => r.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
            || r.StudentId.Contains(text, StringComparison.OrdinalIgnoreCase) || r.TicketReference(id).Contains(text, StringComparison.OrdinalIgnoreCase));
        if (query.College is { } college) matching = matching.Where(r => r.College == college);
        if (query.Program is { } program) matching = matching.Where(r => r.Program == program);
        var total = matching.Count(); var pages = Math.Max(1, (int)Math.Ceiling(total / 25d));
        query.Page = Math.Clamp(input.Page, 1, pages);
        return new(context.Event, query, active.Count, present.Count, rows.Count - active.Count, distributions, arrivals,
            rows.Select(r => r.College).Distinct().Order().ToList(), rows.Select(r => r.Program).Distinct().Order().ToList(),
            matching.OrderByDescending(r => r.CheckedInAt).ThenByDescending(r => r.Id).Skip((query.Page - 1) * 25).Take(25).ToList(), total, pages,
            present.Select(r => r.CheckedInAt).Max());
    }

    public static ArrivalSeries BuildArrivals(IEnumerable<DateTime> timestamps, int minutes, int page)
    {
        minutes = minutes is 5 or 15 or 30 ? minutes : 15;
        var width = TimeSpan.FromMinutes(minutes).Ticks;
        var counts = timestamps.GroupBy(t => t.Ticks / width * width).ToDictionary(g => g.Key, g => g.Count());
        if (counts.Count == 0) return new([], 1, 1, null, 0, 0);
        var first = counts.Keys.Min(); var last = counts.Keys.Max();
        var total = (last - first) / width + 1;
        const int pageSize = 24;
        var pages = (int)((total + pageSize - 1) / pageSize); page = Math.Clamp(page, 1, pages);
        var buckets = Enumerable.Range(0, (int)Math.Min(pageSize, total - (long)(page - 1) * pageSize))
            .Select(i => first + ((long)(page - 1) * pageSize + i) * width).Select(t => new ArrivalBucket(new DateTime(t), counts.GetValueOrDefault(t))).ToList();
        var peak = counts.Values.Max(); var peaks = counts.Where(c => c.Value == peak).OrderBy(c => c.Key).ToList();
        return new(buckets, page, pages, new DateTime(peaks[0].Key), peak, peaks.Count);
    }
}
