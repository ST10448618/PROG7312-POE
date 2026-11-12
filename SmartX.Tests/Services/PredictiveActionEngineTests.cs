using SmartX.Api.Domain.Models;
using SmartX.Api.Services;

namespace SmartX.Tests.Services;

public class PredictiveActionEngineTests
{
    private const string Moisture = "AA:BB:CC:00:01:01";
    private const string Power = "AA:BB:CC:00:01:02";
    private const string Pump = "AA:BB:CC:00:01:03";

    private sealed record Rig(
        PredictiveActionEngine Engine, LiveDeviceRegistry Registry, ActiveFaultTracker Faults, CommandStreamService Commands);

    private static Rig Build(bool registerPump = true)
    {
        var registry = new LiveDeviceRegistry();
        if (registerPump) registry.Register(Pump, "Facility A/Zone 1", "Actuator");
        var faults = new ActiveFaultTracker();
        var commands = new CommandStreamService();
        return new Rig(new PredictiveActionEngine(registry, faults, commands), registry, faults, commands);
    }

    // ------------------------------------------------------------ suggestions

    [Fact]
    public void ALearnedPattern_BecomesASuggestion_OnceItsTriggerAlertFires()
    {
        var rig = Build();
        rig.Engine.SeedAssociation($"Critical:{Moisture}", $"Command:TriggerPump:{Pump}", 3);

        Assert.Empty(rig.Engine.GetSuggestions());   // nothing has alerted yet, so nothing to anticipate

        rig.Engine.RecordAlert(Moisture, "Critical");

        var suggestion = Assert.Single(rig.Engine.GetSuggestions());
        Assert.Equal("Command", suggestion.Kind);
        Assert.Equal("TriggerPump", suggestion.CommandType);
        Assert.Equal(Pump, suggestion.TargetMac);
        Assert.Equal(3, suggestion.Support);
        Assert.InRange(suggestion.Confidence, 0.99, 1.0);
    }

    [Fact]
    public void Suggestions_AreRankedByConfidence_AndConfidenceIsEachActionsShare()
    {
        var rig = Build();
        rig.Engine.SeedAssociation($"Critical:{Moisture}", $"Command:TriggerPump:{Pump}", 6);
        rig.Engine.SeedAssociation($"Critical:{Moisture}", $"Command:ShutdownValve:{Pump}", 2);
        rig.Engine.RecordAlert(Moisture, "Critical");

        var suggestions = rig.Engine.GetSuggestions();

        Assert.Equal(2, suggestions.Count);
        Assert.Equal("TriggerPump", suggestions[0].CommandType);
        Assert.Equal(0.75, suggestions[0].Confidence, 0.01);
        Assert.Equal("ShutdownValve", suggestions[1].CommandType);
        Assert.Equal(0.25, suggestions[1].Confidence, 0.01);
    }

    [Fact]
    public void APatternSeenOnlyOnce_IsNotTrustedYet()
    {
        var rig = Build();
        rig.Engine.SeedAssociation($"Critical:{Moisture}", $"Command:TriggerPump:{Pump}", 1);
        rig.Engine.RecordAlert(Moisture, "Critical");

        Assert.Empty(rig.Engine.GetSuggestions());
    }

    [Fact]
    public void ASuggestionForADeviceThatNoLongerExists_IsDropped()
    {
        var rig = Build(registerPump: false);
        rig.Engine.SeedAssociation($"Critical:{Moisture}", $"Command:TriggerPump:{Pump}", 5);
        rig.Engine.RecordAlert(Moisture, "Critical");

        Assert.Empty(rig.Engine.GetSuggestions());
    }

    [Fact]
    public void TheEngineLearnsFromWhatTheOperatorDoesAfterAnAlert()
    {
        var rig = Build();
        rig.Engine.RecordAlert(Moisture, "Critical");

        rig.Engine.RecordUserAction($"Command:TriggerPump:{Pump}");
        Assert.Empty(rig.Engine.GetSuggestions());   // once is a coincidence

        rig.Engine.RecordUserAction($"Command:TriggerPump:{Pump}");
        var suggestion = Assert.Single(rig.Engine.GetSuggestions());
        Assert.Equal(2, suggestion.Support);
        Assert.Equal("TriggerPump", suggestion.CommandType);
    }

    [Fact]
    public void ASearchPattern_BecomesAFilterSuggestion()
    {
        var rig = Build();
        rig.Engine.SeedAssociation($"Disconnected:{Power}", "Search:Power Consumption|Faulted|", 3);
        rig.Engine.RecordAlert(Power, "Disconnected");

        var suggestion = Assert.Single(rig.Engine.GetSuggestions());

        Assert.Equal("Search", suggestion.Kind);
        Assert.Equal("Power Consumption", suggestion.Category);
        Assert.Equal("Faulted", suggestion.State);
        Assert.Equal("", suggestion.Text);
    }

    [Fact]
    public void EmptySearches_AreNotLearned()
    {
        var rig = Build();
        rig.Engine.RecordAlert(Moisture, "Critical");

        rig.Engine.RecordSearch("  ", "", null);
        rig.Engine.RecordSearch(null, null, null);

        Assert.Empty(rig.Engine.GetSuggestions());
    }

    // --------------------------------------------------------------- insights

    [Fact]
    public void ADeviceThatKeepsFailing_IsFlaggedAsARepeatOffender()
    {
        var rig = Build();

        rig.Engine.RecordAlert(Moisture, "Critical");
        rig.Engine.RecordAlert(Moisture, "Disconnected");
        Assert.DoesNotContain(rig.Engine.GetInsights(), i => i.Title.Contains("repeat offender"));

        rig.Engine.RecordAlert(Moisture, "Critical");

        var insight = Assert.Single(rig.Engine.GetInsights(), i => i.Title.Contains("repeat offender"));
        Assert.Equal(Moisture, insight.Mac);
        Assert.Equal("Warning", insight.Severity);
    }

    [Fact]
    public void ARepeatOffender_EscalatesToCritical_AtDoubleTheThreshold()
    {
        var rig = Build();
        for (var i = 0; i < 6; i++) rig.Engine.RecordAlert(Moisture, "Critical");

        var insight = Assert.Single(rig.Engine.GetInsights(), i => i.Title.Contains("repeat offender"));

        Assert.Equal("Critical", insight.Severity);
    }

    [Fact]
    public void WarningsAlone_DoNotMakeARepeatOffender()
    {
        var rig = Build();
        for (var i = 0; i < 10; i++) rig.Engine.RecordAlert(Moisture, "Warning");

        Assert.DoesNotContain(rig.Engine.GetInsights(), i => i.Title.Contains("repeat offender"));
    }

    [Fact]
    public void AFaultedDevice_RaisesACriticalInsight_ThatSortsFirst()
    {
        var rig = Build();
        rig.Registry.UpdateReading(Moisture, 900, "Critical");   // also out of range, a Warning
        rig.Faults.MarkFaulted(Pump);

        var insights = rig.Engine.GetInsights();

        Assert.Equal("Critical", insights[0].Severity);
        Assert.Equal(Pump, insights[0].Mac);
        Assert.Contains("disconnected", insights[0].Title);
    }

    [Fact]
    public void AReadingOutsideTheExpectedRange_IsReported()
    {
        var rig = Build();
        rig.Registry.UpdateReading(Moisture, 900, "Critical");

        var insight = Assert.Single(rig.Engine.GetInsights(), i => i.Mac == Moisture);

        Assert.Equal("Warning", insight.Severity);
        Assert.Contains("out of range", insight.Title);
    }

    [Fact]
    public void AHealthyInRangeDevice_RaisesNothing()
    {
        var rig = Build();
        rig.Registry.UpdateReading(Moisture, 42, "Online");

        Assert.DoesNotContain(rig.Engine.GetInsights(), i => i.Mac == Moisture);
    }

    [Fact]
    public void ABacklogInThePriorityLane_IsReported()
    {
        var rig = Build();
        for (var i = 0; i < 8; i++)
            rig.Commands.EnqueuePriority(new QueuedReading(Moisture, 1, "Critical", DateTime.UtcNow));

        Assert.Contains(rig.Engine.GetInsights(), i => i.Title == "Priority queue backlog");
    }

    [Fact]
    public void Snapshot_BundlesSuggestionsAndInsights()
    {
        var rig = Build();
        rig.Engine.SeedAssociation($"Critical:{Moisture}", $"Command:TriggerPump:{Pump}", 3);
        rig.Engine.RecordAlert(Moisture, "Critical");

        var snapshot = rig.Engine.Snapshot();

        Assert.Single(snapshot.Suggestions);
        Assert.NotNull(snapshot.Insights);
    }
}
