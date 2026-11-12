using SmartX.Api.Services;

namespace SmartX.Tests.Services;

public class ActiveFaultTrackerTests
{
    [Fact]
    public void MarkFaulted_ReportsWhetherTheFaultWasNew()
    {
        var tracker = new ActiveFaultTracker();

        Assert.True(tracker.MarkFaulted("A"));
        Assert.False(tracker.MarkFaulted("A"));   // a duplicate disconnect is ignored
        Assert.Equal(1, tracker.Count);
        Assert.True(tracker.IsFaulted("A"));
    }

    [Fact]
    public void ClearFault_RemovesTheDevice_OnlyOnce()
    {
        var tracker = new ActiveFaultTracker();
        tracker.MarkFaulted("A");

        Assert.True(tracker.ClearFault("A"));
        Assert.False(tracker.ClearFault("A"));
        Assert.False(tracker.IsFaulted("A"));
        Assert.Equal(0, tracker.Count);
    }

    [Fact]
    public void GetAllFaulted_ReturnsACopy()
    {
        var tracker = new ActiveFaultTracker();
        tracker.MarkFaulted("A");
        tracker.MarkFaulted("B");

        var snapshot = tracker.GetAllFaulted();
        tracker.ClearFault("A");

        Assert.Equal(2, snapshot.Count);   // later changes do not disturb a list already handed out
        Assert.Equal(1, tracker.Count);
    }

    [Fact]
    public void ConcurrentDuplicateDisconnects_ProduceExactlyOneNewFault()
    {
        var tracker = new ActiveFaultTracker();
        var wins = 0;

        Parallel.For(0, 2_000, _ =>
        {
            if (tracker.MarkFaulted("A")) Interlocked.Increment(ref wins);
        });

        Assert.Equal(1, wins);
        Assert.Equal(1, tracker.Count);
    }
}

public class AnomalyDetectionServiceTests
{
    [Fact]
    public void ASpike_AgainstASteadyBaseline_IsCritical()
    {
        var svc = new AnomalyDetectionService();
        for (var i = 0; i < 10; i++) svc.Score("S", i % 2 == 0 ? 49 : 51);

        var spike = svc.Score("S", 500);

        Assert.Equal(AnomalyColour.Red, spike.Colour);
        Assert.True(spike.Score > 2);
        Assert.Equal("Critical", AnomalyDetectionService.SeverityFor(spike.Colour));
    }

    [Fact]
    public void ADrop_IsAlsoCritical_BecauseTheScoreIsAbsolute()
    {
        var svc = new AnomalyDetectionService();
        for (var i = 0; i < 10; i++) svc.Score("S", i % 2 == 0 ? 49 : 51);

        var drop = svc.Score("S", -500);

        Assert.Equal(AnomalyColour.Red, drop.Colour);
        Assert.True(drop.Score < -2);
    }

    [Fact]
    public void FirstFiveReadings_AreNeverFlagged_BecauseThereIsNoBaselineYet()
    {
        var svc = new AnomalyDetectionService();

        foreach (var wild in new double[] { 0, 1000, 0, 1000, 0 })
        {
            var result = svc.Score("S", wild);
            Assert.Equal(AnomalyColour.Green, result.Colour);
            Assert.Equal(0, result.Score);
        }
    }

    [Fact]
    public void OrdinaryReadings_StayGreen()
    {
        var svc = new AnomalyDetectionService();
        for (var i = 0; i < 10; i++) svc.Score("S", i % 2 == 0 ? 49 : 51);

        Assert.Equal(AnomalyColour.Green, svc.Score("S", 50).Colour);
    }

    [Fact]
    public void ADisconnectedSensor_IsBlue_WithNoScore()
    {
        var svc = new AnomalyDetectionService();

        var result = svc.Score("S", 0, sensorConnected: false);

        Assert.Equal(AnomalyColour.Blue, result.Colour);
        Assert.Equal(0, result.Score);
        Assert.Equal("Disconnected", AnomalyDetectionService.SeverityFor(result.Colour));
    }

    [Fact]
    public void SensorsAreScoredAgainstTheirOwnBaseline()
    {
        var svc = new AnomalyDetectionService();
        for (var i = 0; i < 10; i++) svc.Score("A", i % 2 == 0 ? 49 : 51);

        // B has no history, so a huge first reading is not an anomaly for B.
        Assert.Equal(AnomalyColour.Green, svc.Score("B", 900).Colour);
    }

    [Theory]
    [InlineData(AnomalyColour.Green, "Normal")]
    [InlineData(AnomalyColour.Amber, "Warning")]
    [InlineData(AnomalyColour.Red, "Critical")]
    [InlineData(AnomalyColour.Blue, "Disconnected")]
    public void SeverityFor_MapsEveryColour(AnomalyColour colour, string expected)
    {
        Assert.Equal(expected, AnomalyDetectionService.SeverityFor(colour));
    }
}
