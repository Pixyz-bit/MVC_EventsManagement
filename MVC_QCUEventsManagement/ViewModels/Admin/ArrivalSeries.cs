namespace MVC_QCUEventsManagement.ViewModels.Admin;

public sealed record ArrivalSeries(IReadOnlyList<ArrivalBucket> Buckets, int Page, int TotalPages,
    DateTime? PeakStart, int PeakCount, int TiedPeaks);
