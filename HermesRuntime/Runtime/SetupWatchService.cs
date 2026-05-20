using System.Text.Json;

namespace Hermes.Runtime;

public sealed class SetupWatchService
{
    private const string SetupWatchSource = "hermes_setup_watch_service";

    private readonly EventBus _eventBus;
    private readonly string _runtimeVersion;
    private readonly string _setupWatchDirectory;

    public SetupWatchService(
        StoragePaths storagePaths,
        EventBus eventBus,
        string runtimeVersion)
    {
        _eventBus = eventBus;
        _runtimeVersion = runtimeVersion;
        _setupWatchDirectory = Path.Combine(storagePaths.Root, "setup_watch");
        Directory.CreateDirectory(_setupWatchDirectory);
    }

    public SetupWatchWriteResult CreateDemoSetupWatches()
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        var candidates = new List<SetupWatchCandidate>
        {
            new(
                SetupId: $"setup_xauusd_long_{createdAtUtc:yyyyMMddHHmmssfff}",
                Symbol: "XAUUSD",
                Bias: "long",
                Status: SetupWatchStatus.watching,
                Confidence: 0.68m,
                EntryZone: "2368.20 - 2371.80",
                SuggestedStopLoss: "2361.40",
                SuggestedTarget: "2382.00",
                TriggerCondition: "Bullish rejection near pullback zone after candle close.",
                InvalidationLevel: "2360.80",
                TimeWindowMinutes: 30,
                Notes: "Demo only. No broker connection, no order execution.",
                CreatedAtUtc: createdAtUtc),
            new(
                SetupId: $"setup_eurusd_neutral_{createdAtUtc:yyyyMMddHHmmssfff}",
                Symbol: "EURUSD",
                Bias: "neutral",
                Status: SetupWatchStatus.expired,
                Confidence: 0.42m,
                EntryZone: "No active entry zone",
                SuggestedStopLoss: "n/a",
                SuggestedTarget: "n/a",
                TriggerCondition: "No confirmed directional trigger during the demo window.",
                InvalidationLevel: "n/a",
                TimeWindowMinutes: 10,
                Notes: "Demo neutral watch expired without signal.",
                CreatedAtUtc: createdAtUtc),
            new(
                SetupId: $"setup_ger40_breakout_{createdAtUtc:yyyyMMddHHmmssfff}",
                Symbol: "GER40",
                Bias: "possible_breakout",
                Status: SetupWatchStatus.armed,
                Confidence: 0.57m,
                EntryZone: "18420 - 18445",
                SuggestedStopLoss: "18380",
                SuggestedTarget: "18510",
                TriggerCondition: "Break and close above local resistance with acceptable spread.",
                InvalidationLevel: "18375",
                TimeWindowMinutes: 20,
                Notes: "Demo breakout watch. Alerts only, no auto-trading.",
                CreatedAtUtc: createdAtUtc)
        };

        var outputPath = Path.Combine(_setupWatchDirectory, "setup_watch.json");
        File.WriteAllText(outputPath, JsonSerializer.Serialize(candidates, JsonDefaults.WriteOptions));

        foreach (var candidate in candidates)
        {
            PublishCreated(candidate, outputPath);
        }

        PublishUpdated(candidates[2], "Demo GER40 breakout watch is armed.");
        PublishExpired(candidates[1], "Demo EURUSD neutral watch expired without trigger.");

        return new SetupWatchWriteResult(candidates, outputPath);
    }

    private void PublishCreated(SetupWatchCandidate candidate, string outputPath)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.SetupWatchCreated,
            SetupWatchSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Demo setup watch candidate created. No trade execution is possible.",
                candidate.SetupId,
                candidate.Symbol,
                candidate.Bias,
                candidate.Status,
                candidate.Confidence,
                candidate.EntryZone,
                candidate.SuggestedStopLoss,
                candidate.SuggestedTarget,
                candidate.TriggerCondition,
                candidate.InvalidationLevel,
                candidate.TimeWindowMinutes,
                candidate.Notes,
                candidate.CreatedAtUtc,
                outputPath,
                noAutoTrading = true,
                humanReviewRequired = true
            }));
    }

    private void PublishUpdated(SetupWatchCandidate candidate, string message)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.SetupWatchUpdated,
            SetupWatchSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message,
                candidate.SetupId,
                candidate.Symbol,
                candidate.Bias,
                candidate.Status,
                candidate.Confidence,
                candidate.TriggerCondition,
                noAutoTrading = true,
                humanReviewRequired = true
            }));
    }

    private void PublishExpired(SetupWatchCandidate candidate, string message)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.SetupWatchExpired,
            SetupWatchSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message,
                candidate.SetupId,
                candidate.Symbol,
                candidate.Bias,
                candidate.Status,
                candidate.Confidence,
                candidate.TimeWindowMinutes,
                noAutoTrading = true,
                humanReviewRequired = true
            }));
    }
}
