using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record SignalWatchEvaluation(
    string EvaluationId,
    DateTimeOffset EvaluatedAtUtc,
    string SignalId,
    string Asset,
    string Timeframe,
    string CandidateId,
    string Direction,
    string SignalLifecycleStatus,
    double? ObservedPrice,
    double? ObservedHigh,
    double? ObservedLow,
    double EntryLevel,
    double EntryZoneLower,
    double EntryZoneUpper,
    double StopLoss,
    double TakeProfit,
    double InvalidationLevel,
    bool ObservedEntryHit,
    bool ObservedInvalidationHit,
    bool ObservedStopLossHit,
    bool ObservedTakeProfitHit,
    bool ObservedNearMiss,
    bool ObservedExpired,
    bool OutcomePending,
    string HypotheticalResult,
    double? RMultiple,
    bool RequiresHumanReview,
    bool Simulated,
    string MarketDataSource,
    IReadOnlyList<string> Warnings,
    string Note,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record SignalWatchStatusSnapshot(
    string StatusVersion,
    DateTimeOffset UpdatedAtUtc,
    string WatchStatus,
    int SignalsEvaluated,
    int WaitingForTriggerCount,
    int WatchingCount,
    int ArmedCount,
    int TriggeredCount,
    int ActiveCount,
    int NearMissCount,
    int InvalidatedCount,
    int ExpiredCount,
    int CompletedCount,
    int NoSignalCount,
    bool UsingCurrentMarketSnapshot,
    string LatestEvaluationsJsonPath,
    string LatestEvaluationsMarkdownPath,
    string LogPath,
    IReadOnlyList<string> Warnings,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record SignalWatchLogEntry(
    DateTimeOffset TimestampUtc,
    string Action,
    int SignalsEvaluated,
    string WatchStatus,
    IReadOnlyList<string> Warnings,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed class SignalWatchService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedRoot;

    public SignalWatchService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => _resolvedRoot ??= ResolveRoot();
    public string StatusPath => Path.Combine(Root, "signal_watch_status.json");
    public string LatestEvaluationsJsonPath => Path.Combine(Root, "latest_signal_watch.json");
    public string LatestEvaluationsMarkdownPath => Path.Combine(Root, "latest_signal_watch.md");
    public string LogPath => Path.Combine(Root, "signal_watch_log.jsonl");

    public SignalWatchStatusSnapshot Run()
    {
        var (_, snapshot) = EvaluateLatestSignalsInternal();
        return snapshot;
    }

    public SignalWatchStatusSnapshot LoadOrCreateStatus()
    {
        if (File.Exists(StatusPath))
        {
            var snapshot = JsonSerializer.Deserialize<SignalWatchStatusSnapshot>(File.ReadAllText(StatusPath), JsonDefaults.SnapshotReadOptions);
            if (snapshot is not null)
            {
                return snapshot;
            }
        }

        return Run();
    }

    public IReadOnlyList<SignalWatchEvaluation> LoadLatestEvaluations()
    {
        if (!File.Exists(LatestEvaluationsJsonPath))
        {
            Run();
        }

        var evaluations = JsonSerializer.Deserialize<List<SignalWatchEvaluation>>(File.ReadAllText(LatestEvaluationsJsonPath), JsonDefaults.SnapshotReadOptions);
        return evaluations ?? [];
    }

    public SignalWatchEvaluation EvaluateSignal(DemoSignalFeedItem signal, List<string>? warnings = null)
    {
        return EvaluateSingleSignal(signal, warnings ?? []);
    }

    private (IReadOnlyList<SignalWatchEvaluation> Evaluations, SignalWatchStatusSnapshot Snapshot) EvaluateLatestSignalsInternal()
    {
        var warnings = new List<string>();
        var signals = new DemoSignalFeedService(_storagePaths, _runtimeRoot).LoadLatestSignals();
        var evaluations = signals.Select(signal => EvaluateSingleSignal(signal, warnings)).ToList();
        var snapshot = BuildSnapshot(evaluations, warnings);

        Directory.CreateDirectory(Root);
        File.WriteAllText(StatusPath, JsonSerializer.Serialize(snapshot, JsonDefaults.WriteOptions));
        File.WriteAllText(LatestEvaluationsJsonPath, JsonSerializer.Serialize(evaluations, JsonDefaults.WriteOptions));
        File.WriteAllText(LatestEvaluationsMarkdownPath, BuildMarkdown(snapshot, evaluations));
        AppendLog(snapshot);
        return (evaluations, snapshot);
    }

    private string ResolveRoot()
    {
        var preferred = Path.Combine(_storagePaths.Root, "reports", "signal_watch");
        try
        {
            Directory.CreateDirectory(preferred);
            return preferred;
        }
        catch (IOException)
        {
            var fallback = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "signal_watch");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
        catch (UnauthorizedAccessException)
        {
            var fallback = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "signal_watch");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    private SignalWatchEvaluation EvaluateSingleSignal(DemoSignalFeedItem signal, List<string> warnings)
    {
        var evaluationWarnings = new List<string>();
        var direction = NormalizeDirection(signal.Direction, evaluationWarnings);
        var timeframe = string.IsNullOrWhiteSpace(signal.Timeframe) ? "M5" : signal.Timeframe.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(signal.Timeframe))
        {
            evaluationWarnings.Add("timeframe_missing_used_default_m5");
        }

        var createdUtc = signal.CreatedUtc == default ? DateTimeOffset.UtcNow : signal.CreatedUtc;
        var expiresUtc = signal.ExpiresUtc ?? createdUtc.Add(EstimateSignalLifetime(timeframe));
        if (!signal.ExpiresUtc.HasValue)
        {
            evaluationWarnings.Add($"expires_utc_missing_used_timeframe_default:{expiresUtc:O}");
        }

        var riskDistance = Math.Abs(signal.EntryLevel - signal.StopLoss);
        if (riskDistance <= 0)
        {
            evaluationWarnings.Add("risk_distance_missing_or_zero_human_review_required");
            riskDistance = MinimumTick(signal.Asset);
        }

        var entryBand = Math.Max(riskDistance * 0.15, MinimumTick(signal.Asset));
        var entryZoneLower = signal.EntryZoneLower ?? RoundPrice(signal.Asset, signal.EntryLevel - entryBand);
        var entryZoneUpper = signal.EntryZoneUpper ?? RoundPrice(signal.Asset, signal.EntryLevel + entryBand);

        var snapshot = new CurrentMarketSnapshotService(_storagePaths, _runtimeRoot).FindSnapshot(signal.Asset);
        var usingSnapshot = snapshot is not null
            && snapshot.Status == "available"
            && snapshot.IsLiveReadonly
            && !snapshot.IsPlaceholder
            && snapshot.Mid.HasValue;
        var candle = usingSnapshot ? null : LoadLatestCandle(signal.Asset, timeframe);

        double? observedPrice = usingSnapshot
            ? RoundPrice(signal.Asset, snapshot!.Mid ?? snapshot.Bid ?? snapshot.Ask ?? signal.EntryLevel)
            : candle is not null ? RoundPrice(signal.Asset, candle.Close) : null;
        double? observedHigh = usingSnapshot
            ? observedPrice
            : candle is not null ? RoundPrice(signal.Asset, candle.High) : null;
        double? observedLow = usingSnapshot
            ? observedPrice
            : candle is not null ? RoundPrice(signal.Asset, candle.Low) : null;
        var dataSource = usingSnapshot
            ? $"current_market_snapshot:{snapshot!.Source}"
            : candle is not null ? $"market_data_candle:{candle.Timeframe}" : "no_market_data";

        if (!usingSnapshot && candle is null)
        {
            evaluationWarnings.Add($"market_data_missing:{signal.Asset}:{timeframe}");
        }

        var expired = DateTimeOffset.UtcNow > expiresUtc;
        var observedEntryHit = false;
        var observedInvalidationHit = false;
        var observedStopLossHit = false;
        var observedTakeProfitHit = false;
        var observedNearMiss = false;
        var outcomePending = true;
        var hypotheticalResult = "outcome_pending";
        var lifecycleStatus = "watching";
        var note = "read_only_signal_watch:no_order_execution";
        var requiresHumanReview = false;
        double? rMultiple = null;

        if (direction == "neutral")
        {
            lifecycleStatus = "no_signal";
            hypotheticalResult = "no_signal";
            outcomePending = false;
            requiresHumanReview = true;
            note = "signal_watch:neutral_direction_no_signal";
        }
        else if (!observedPrice.HasValue || !observedHigh.HasValue || !observedLow.HasValue)
        {
            lifecycleStatus = expired ? "expired" : "watching";
            hypotheticalResult = expired ? "expired" : "outcome_pending";
            outcomePending = !expired;
            requiresHumanReview = true;
            note = expired ? "signal_watch:expired_without_usable_market_data" : "signal_watch:watching_market_data_missing";
        }
        else
        {
            var isShort = direction.Contains("short", StringComparison.OrdinalIgnoreCase);
            var isLong = direction.Contains("long", StringComparison.OrdinalIgnoreCase);
            observedEntryHit = isLong
                ? observedHigh.Value >= entryZoneLower
                : observedLow.Value <= entryZoneUpper;
            observedInvalidationHit = isLong
                ? observedLow.Value <= signal.InvalidationLevel
                : observedHigh.Value >= signal.InvalidationLevel;
            observedStopLossHit = isLong
                ? observedLow.Value <= signal.StopLoss
                : observedHigh.Value >= signal.StopLoss;
            observedTakeProfitHit = isLong
                ? observedHigh.Value >= signal.TakeProfit
                : observedLow.Value <= signal.TakeProfit;

            var minDistanceToZone = Math.Min(
                Math.Abs(observedPrice.Value - entryZoneLower),
                Math.Abs(observedPrice.Value - entryZoneUpper));
            observedNearMiss = !observedEntryHit && minDistanceToZone <= Math.Max(entryBand * 0.35, MinimumTick(signal.Asset));

            if (expired && !observedEntryHit)
            {
                lifecycleStatus = "expired";
                hypotheticalResult = "expired";
                outcomePending = false;
                note = "signal_watch:expired_before_trigger";
            }
            else if (observedTakeProfitHit)
            {
                lifecycleStatus = "completed";
                hypotheticalResult = "hypothetical_win";
                outcomePending = false;
                rMultiple = ComputeRMultiple(signal, signal.TakeProfit);
                note = "signal_watch:take_profit_reached";
            }
            else if (observedInvalidationHit || observedStopLossHit)
            {
                lifecycleStatus = observedEntryHit ? "completed" : "invalidated";
                hypotheticalResult = observedEntryHit ? "hypothetical_loss" : "invalidated";
                outcomePending = false;
                rMultiple = observedEntryHit ? ComputeRMultiple(signal, signal.StopLoss) : null;
                note = observedStopLossHit ? "signal_watch:stop_loss_reached" : "signal_watch:invalidation_reached";
            }
            else if (observedEntryHit)
            {
                var insideZone = observedPrice.Value >= entryZoneLower && observedPrice.Value <= entryZoneUpper;
                lifecycleStatus = usingSnapshot
                    ? insideZone ? "armed" : "triggered"
                    : "active";
                rMultiple = ComputeRMultiple(signal, observedPrice.Value);
                note = usingSnapshot
                    ? insideZone ? "signal_watch:entry_zone_armed" : "signal_watch:entry_triggered_snapshot"
                    : "signal_watch:entry_triggered_active_candle";
            }
            else if (observedNearMiss)
            {
                lifecycleStatus = "near_miss";
                hypotheticalResult = "near_miss";
                note = "signal_watch:near_miss";
            }
            else
            {
                var zoneDistance = isLong
                    ? entryZoneLower - observedPrice.Value
                    : observedPrice.Value - entryZoneUpper;
                lifecycleStatus = zoneDistance <= Math.Max(entryBand * 2.5, MinimumTick(signal.Asset) * 2)
                    ? "watching"
                    : "waiting_for_trigger";
                note = lifecycleStatus == "watching" ? "signal_watch:watching_entry_zone" : "signal_watch:waiting_for_trigger";
            }
        }

        if (signal.Reason.Contains("missing", StringComparison.OrdinalIgnoreCase)
            || evaluationWarnings.Count > 0
            || lifecycleStatus is "no_signal" or "near_miss")
        {
            requiresHumanReview = true;
        }

        warnings.AddRange(evaluationWarnings.Select(item => $"{signal.SignalId}:{item}"));
        return new SignalWatchEvaluation(
            EvaluationId: $"signal_watch_{signal.SignalId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            EvaluatedAtUtc: DateTimeOffset.UtcNow,
            SignalId: signal.SignalId,
            Asset: signal.Asset,
            Timeframe: timeframe,
            CandidateId: signal.CandidateId,
            Direction: direction,
            SignalLifecycleStatus: lifecycleStatus,
            ObservedPrice: observedPrice,
            ObservedHigh: observedHigh,
            ObservedLow: observedLow,
            EntryLevel: signal.EntryLevel,
            EntryZoneLower: entryZoneLower,
            EntryZoneUpper: entryZoneUpper,
            StopLoss: signal.StopLoss,
            TakeProfit: signal.TakeProfit,
            InvalidationLevel: signal.InvalidationLevel,
            ObservedEntryHit: observedEntryHit,
            ObservedInvalidationHit: observedInvalidationHit,
            ObservedStopLossHit: observedStopLossHit,
            ObservedTakeProfitHit: observedTakeProfitHit,
            ObservedNearMiss: observedNearMiss,
            ObservedExpired: expired,
            OutcomePending: outcomePending,
            HypotheticalResult: hypotheticalResult,
            RMultiple: rMultiple,
            RequiresHumanReview: requiresHumanReview,
            Simulated: !observedPrice.HasValue,
            MarketDataSource: dataSource,
            Warnings: evaluationWarnings,
            Note: note,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }

    private SignalWatchStatusSnapshot BuildSnapshot(IReadOnlyList<SignalWatchEvaluation> evaluations, IReadOnlyList<string> warnings)
    {
        var watchStatus = evaluations.Count == 0
            ? "no_signals"
            : evaluations.Any(item => item.RequiresHumanReview) ? "needs_attention" : "ok";
        return new SignalWatchStatusSnapshot(
            StatusVersion: "signal_watch_status_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            WatchStatus: watchStatus,
            SignalsEvaluated: evaluations.Count,
            WaitingForTriggerCount: evaluations.Count(item => item.SignalLifecycleStatus == "waiting_for_trigger"),
            WatchingCount: evaluations.Count(item => item.SignalLifecycleStatus == "watching"),
            ArmedCount: evaluations.Count(item => item.SignalLifecycleStatus == "armed"),
            TriggeredCount: evaluations.Count(item => item.SignalLifecycleStatus == "triggered"),
            ActiveCount: evaluations.Count(item => item.SignalLifecycleStatus == "active"),
            NearMissCount: evaluations.Count(item => item.SignalLifecycleStatus == "near_miss"),
            InvalidatedCount: evaluations.Count(item => item.SignalLifecycleStatus == "invalidated"),
            ExpiredCount: evaluations.Count(item => item.SignalLifecycleStatus == "expired"),
            CompletedCount: evaluations.Count(item => item.SignalLifecycleStatus == "completed"),
            NoSignalCount: evaluations.Count(item => item.SignalLifecycleStatus == "no_signal"),
            UsingCurrentMarketSnapshot: evaluations.Any(item => item.MarketDataSource.StartsWith("current_market_snapshot:", StringComparison.OrdinalIgnoreCase)),
            LatestEvaluationsJsonPath: LatestEvaluationsJsonPath,
            LatestEvaluationsMarkdownPath: LatestEvaluationsMarkdownPath,
            LogPath: LogPath,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }

    private void AppendLog(SignalWatchStatusSnapshot snapshot)
    {
        Directory.CreateDirectory(Root);
        var entry = new SignalWatchLogEntry(
            TimestampUtc: DateTimeOffset.UtcNow,
            Action: "run_signal_watch",
            SignalsEvaluated: snapshot.SignalsEvaluated,
            WatchStatus: snapshot.WatchStatus,
            Warnings: snapshot.Warnings,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        File.AppendAllText(LogPath, JsonSerializer.Serialize(entry, JsonDefaults.WriteOptions) + Environment.NewLine);
    }

    private static string BuildMarkdown(SignalWatchStatusSnapshot snapshot, IReadOnlyList<SignalWatchEvaluation> evaluations)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Signal Watch Status");
        builder.AppendLine();
        builder.AppendLine($"- watch_status: {snapshot.WatchStatus}");
        builder.AppendLine($"- signals_evaluated: {snapshot.SignalsEvaluated}");
        builder.AppendLine($"- waiting_for_trigger_count: {snapshot.WaitingForTriggerCount}");
        builder.AppendLine($"- watching_count: {snapshot.WatchingCount}");
        builder.AppendLine($"- armed_count: {snapshot.ArmedCount}");
        builder.AppendLine($"- triggered_count: {snapshot.TriggeredCount}");
        builder.AppendLine($"- active_count: {snapshot.ActiveCount}");
        builder.AppendLine($"- near_miss_count: {snapshot.NearMissCount}");
        builder.AppendLine($"- invalidated_count: {snapshot.InvalidatedCount}");
        builder.AppendLine($"- expired_count: {snapshot.ExpiredCount}");
        builder.AppendLine($"- completed_count: {snapshot.CompletedCount}");
        builder.AppendLine($"- no_signal_count: {snapshot.NoSignalCount}");
        builder.AppendLine("- Observation only");
        builder.AppendLine("- No orders");
        builder.AppendLine("- No broker action");
        builder.AppendLine("- No cTrader Order API");
        builder.AppendLine("- no_auto_trading=true");
        builder.AppendLine("- human_review_required=true");
        builder.AppendLine("- broker_orders_enabled=false");
        builder.AppendLine("- live_trading_enabled=false");
        builder.AppendLine();
        foreach (var evaluation in evaluations)
        {
            builder.AppendLine($"## {evaluation.SignalId}");
            builder.AppendLine($"- asset: {evaluation.Asset}");
            builder.AppendLine($"- timeframe: {evaluation.Timeframe}");
            builder.AppendLine($"- lifecycle_status: {evaluation.SignalLifecycleStatus}");
            builder.AppendLine($"- observed_price: {(evaluation.ObservedPrice.HasValue ? evaluation.ObservedPrice.Value.ToString("0.#####") : "n/a")}");
            builder.AppendLine($"- observed_entry_hit: {evaluation.ObservedEntryHit.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- observed_take_profit_hit: {evaluation.ObservedTakeProfitHit.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- observed_stop_loss_hit: {evaluation.ObservedStopLossHit.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- observed_invalidation_hit: {evaluation.ObservedInvalidationHit.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- observed_near_miss: {evaluation.ObservedNearMiss.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- hypothetical_result: {evaluation.HypotheticalResult}");
            builder.AppendLine($"- note: {evaluation.Note}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string NormalizeDirection(string? direction, List<string> warnings)
    {
        var normalized = string.IsNullOrWhiteSpace(direction) ? "neutral" : direction.Trim().ToLowerInvariant();
        return normalized switch
        {
            "long" or "short" or "long_watch" or "short_watch" or "neutral" => normalized,
            "watch" => "neutral",
            _ => warnings.AddReturn("direction_unknown_used_neutral", "neutral")
        };
    }

    private static TimeSpan EstimateSignalLifetime(string timeframe) => timeframe.ToUpperInvariant() switch
    {
        "M1" => TimeSpan.FromHours(4),
        "M5" => TimeSpan.FromHours(12),
        "M15" => TimeSpan.FromDays(1),
        "M30" => TimeSpan.FromDays(2),
        "H1" => TimeSpan.FromDays(3),
        "H4" => TimeSpan.FromDays(5),
        _ => TimeSpan.FromDays(2)
    };

    private MarketDataCandle? LoadLatestCandle(string asset, string timeframe)
    {
        var directory = Path.Combine(_storagePaths.Root, "market_data", "candles", asset, timeframe);
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var latestFile = Directory.GetFiles(directory, "*.candles.jsonl", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (latestFile is null)
        {
            return null;
        }

        var line = File.ReadLines(latestFile).Reverse().FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        return string.IsNullOrWhiteSpace(line)
            ? null
            : JsonSerializer.Deserialize<MarketDataCandle>(line, JsonDefaults.SnapshotReadOptions);
    }

    private static double? ComputeRMultiple(DemoSignalFeedItem signal, double observedPrice)
    {
        var risk = Math.Abs(signal.EntryLevel - signal.StopLoss);
        if (risk <= 0)
        {
            return null;
        }

        var isShort = signal.Direction.Contains("short", StringComparison.OrdinalIgnoreCase);
        var delta = isShort
            ? signal.EntryLevel - observedPrice
            : observedPrice - signal.EntryLevel;
        return Math.Round(delta / risk, 4);
    }

    private static double MinimumTick(string asset) => asset.ToUpperInvariant() switch
    {
        "EURUSD" => 0.0001,
        "XAUUSD" => 0.1,
        "GER40" or "DE40" => 0.5,
        _ => 0.01
    };

    private static double RoundPrice(string asset, double value) => asset.ToUpperInvariant() switch
    {
        "EURUSD" => Math.Round(value, 5),
        "GER40" or "DE40" => Math.Round(value, 1),
        _ => Math.Round(value, 2)
    };
}

internal static class SignalWatchWarningsExtensions
{
    public static string AddReturn(this List<string> warnings, string warning, string value)
    {
        warnings.Add(warning);
        return value;
    }
}
