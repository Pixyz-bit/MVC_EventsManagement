using MVC_QCUEventsManagement.Services;

namespace MVC_QCUEventsManagement.Tests;

public sealed class AnalyticsIntervalTests
{
    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(30)]
    public void Arrivals_floor_fill_gaps_cross_midnight_and_choose_first_tied_peak(int minutes)
    {
        var first = new DateTime(2030, 6, 30, 23, 30, 0);
        var series = AnalyticsService.BuildArrivals([first.AddSeconds(1), first.AddMinutes(1), first.AddMinutes(minutes * 2), first.AddMinutes(minutes * 2 + 1)], minutes, -1);
        Assert.Equal(3, series.Buckets.Count); Assert.Equal(0, series.Buckets[1].Count);
        Assert.Equal(first, series.Buckets[0].Start); Assert.Equal(first.AddMinutes(minutes * 2), series.Buckets[^1].Start);
        Assert.Equal(2, series.PeakCount); Assert.Equal(first, series.PeakStart); Assert.Equal(2, series.TiedPeaks); Assert.Equal(1, series.Page);
    }

    [Fact]
    public void Sparse_multi_day_arrivals_page_empty_windows_without_allocating_the_whole_range()
    {
        var first = new DateTime(2030, 1, 1, 0, 0, 0);
        var series = AnalyticsService.BuildArrivals([first, first.AddDays(2)], 15, int.MaxValue);
        Assert.Equal(9, series.TotalPages); Assert.Equal(9, series.Page); Assert.Single(series.Buckets);
        Assert.Equal(first.AddDays(2), series.Buckets[0].Start); Assert.Equal(1, series.Buckets[0].Count);
        var empty = AnalyticsService.BuildArrivals([], 99, int.MaxValue);
        Assert.Empty(empty.Buckets); Assert.Null(empty.PeakStart); Assert.Equal(1, empty.TotalPages);
    }
}
