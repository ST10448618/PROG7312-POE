using SmartX.Api.Domain.Models;
using SmartX.Api.Services;

namespace SmartX.Tests.Services;

public class TelemetryHistoryServiceTests
{
    private const string Node = "AA:BB:CC:00:01:01";
    private static readonly DateTime Midnight = new(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc);

    private static TelemetryPoint Point(DateTime at, double value) => new(at, value, "%", "Single");

    private static TelemetryHistoryService DayOfMinutes()
    {
        var history = new TelemetryHistoryService();
        for (var minute = 0; minute < 24 * 60; minute++)
            history.Add(Node, Point(Midnight.AddMinutes(minute), minute));
        return history;
    }

    [Fact]
    public void Query_ReturnsOnlyReadingsInsideTheWindow_BothEndsInclusive()
    {
        var history = DayOfMinutes();

        var result = history.Query(Node, Midnight.AddHours(14), Midnight.AddHours(16), take: 1000)!;

        Assert.Equal(121, result.Total);
        Assert.Equal(121, result.Points.Count);
        Assert.Equal(Midnight.AddHours(14), result.Points[0].Timestamp);
        Assert.Equal(Midnight.AddHours(16), result.Points[^1].Timestamp);
        Assert.Equal(840, result.Min);
        Assert.Equal(960, result.Max);
        Assert.Equal(900, result.Average);
    }

    [Fact]
    public void Query_Paging_ReturnsASliceButReportsTheFullTotal()
    {
        var history = DayOfMinutes();

        var page = history.Query(Node, Midnight.AddHours(14), Midnight.AddHours(16), skip: 10, take: 5)!;

        Assert.Equal(121, page.Total);
        Assert.Equal(5, page.Points.Count);
        Assert.Equal(850, page.Points[0].Value);
        Assert.Equal(854, page.Points[^1].Value);
        Assert.Equal(900, page.Average);   // statistics cover the whole window, not just the page
    }

    [Fact]
    public void Query_Descending_ReturnsNewestFirst()
    {
        var history = DayOfMinutes();

        var result = history.Query(Node, Midnight.AddHours(14), Midnight.AddHours(16), take: 3, descending: true)!;

        Assert.Equal(new double[] { 960, 959, 958 }, result.Points.Select(p => p.Value));
        Assert.True(result.Descending);
    }

    [Fact]
    public void Query_OnAnEmptyWindow_ReportsNoStatistics()
    {
        var history = DayOfMinutes();

        var result = history.Query(Node, Midnight.AddDays(3), Midnight.AddDays(4))!;

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Points);
        Assert.Null(result.Min);
        Assert.Null(result.Max);
        Assert.Null(result.Average);
    }

    [Fact]
    public void Query_ForAnUnknownSensor_ReturnsNull()
    {
        var history = DayOfMinutes();

        Assert.Null(history.Query("AA:BB:CC:FF:FF:FF", Midnight, Midnight.AddHours(1)));
        Assert.Null(history.Summarise("AA:BB:CC:FF:FF:FF"));
    }

    [Fact]
    public void Query_ClampsTakeToTheSupportedRange()
    {
        var history = DayOfMinutes();

        var tiny = history.Query(Node, Midnight, Midnight.AddDays(1), take: 0)!;
        var huge = history.Query(Node, Midnight, Midnight.AddDays(1), take: 50_000)!;

        Assert.Single(tiny.Points);
        Assert.Equal(1000, huge.Points.Count);
        Assert.Equal(24 * 60, huge.Total);
    }

    [Fact]
    public void SensorIds_AreMatchedWithoutRegardToCase()
    {
        var history = DayOfMinutes();

        Assert.NotNull(history.Query(Node.ToLowerInvariant(), Midnight, Midnight.AddHours(1)));
    }

    [Fact]
    public void ReadingsArrivingOutOfOrder_AreStillReturnedInTimeOrder()
    {
        var history = new TelemetryHistoryService();
        var order = new[] { 5, 1, 9, 3, 7, 2, 8, 4, 6, 0 };
        foreach (var m in order) history.Add(Node, Point(Midnight.AddMinutes(m), m));

        var result = history.Query(Node, Midnight, Midnight.AddMinutes(10))!;

        Assert.Equal(Enumerable.Range(0, 10).Select(i => (double)i), result.Points.Select(p => p.Value));
    }

    [Fact]
    public void Query_ReportsTreeSizeAndHeight_SoTheUiCanShowTheStructureIsBalanced()
    {
        var history = DayOfMinutes();

        var result = history.Query(Node, Midnight, Midnight.AddHours(1))!;

        Assert.Equal(24 * 60, result.TreeSize);
        Assert.InRange(result.TreeHeight, 11, 15);   // 1440 readings: perfect height is 11, AVL allows a little more
        Assert.True(result.ElapsedMicroseconds >= 0);
    }

    [Fact]
    public void WarmUp_SkipsLiveReadingsTheDatabaseLoadAlreadyCovers()
    {
        var history = new TelemetryHistoryService();
        var cutoff = Midnight.AddHours(12);
        history.BeginWarmUp(cutoff);

        history.Add(Node, Point(cutoff.AddMinutes(-5), 1));   // older than the cutoff: the DB load will supply it
        history.Add(Node, Point(cutoff.AddMinutes(5), 2));    // newer: only the live path can supply it

        Assert.False(history.IsReady);
        Assert.Equal(1, history.TotalPoints);

        history.AddHistorical(Node, Point(cutoff.AddMinutes(-5), 1));   // the load itself is never filtered
        Assert.Equal(2, history.TotalPoints);
    }

    [Fact]
    public void AfterWarmUp_EveryReadingIsAccepted()
    {
        var history = new TelemetryHistoryService();
        history.BeginWarmUp(Midnight.AddHours(12));
        history.CompleteWarmUp();

        history.Add(Node, Point(Midnight, 1));

        Assert.True(history.IsReady);
        Assert.Equal(1, history.TotalPoints);
    }

    [Fact]
    public void Summarise_ReportsSpanCountAndABalancedTree()
    {
        var history = DayOfMinutes();

        var summary = history.Summarise(Node)!;

        Assert.Equal(24 * 60, summary.Count);
        Assert.Equal(Midnight, summary.First);
        Assert.Equal(Midnight.AddMinutes(24 * 60 - 1), summary.Last);
        Assert.True(summary.IsBalanced, summary.Problem);
        Assert.True(summary.TreeHeight >= summary.MinimumPossibleHeight);
    }

    [Fact]
    public void Insert_CapsEachSensorAtTheMaximum_DroppingTheOldestReadings()
    {
        var history = new TelemetryHistoryService();
        var extra = 10;

        for (var i = 0; i < TelemetryHistoryService.MaxPointsPerSensor + extra; i++)
            history.Add(Node, Point(Midnight.AddSeconds(i), i));

        var summary = history.Summarise(Node)!;
        Assert.Equal(TelemetryHistoryService.MaxPointsPerSensor, summary.Count);
        Assert.Equal(Midnight.AddSeconds(extra), summary.First);   // the first ten were evicted
        Assert.True(summary.IsBalanced, summary.Problem);
    }

    [Fact]
    public void TotalPointsAndSensorIds_CoverEverySensor()
    {
        var history = new TelemetryHistoryService();
        history.Add("A", Point(Midnight, 1));
        history.Add("A", Point(Midnight.AddMinutes(1), 2));
        history.Add("B", Point(Midnight, 3));

        Assert.Equal(3, history.TotalPoints);
        Assert.Equal(new[] { "A", "B" }, history.SensorIds.OrderBy(id => id));
    }
}
