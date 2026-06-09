using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ForwardTestMetrics(
    int SignalsGenerated,
    int SignalsObserved,
    int ObservationsTotal,
    int TriggeredCount,
    int InvalidatedCount,
    int ExpiredCount,
    int HypotheticalWins,
    int HypotheticalLosses,
    int ManualReviewCount,
    int SimulatedObservationCount,
    double WinRate,
    double AverageR,
    double MaxDrawdownR,
    double MaxDailyDrawdownR,
    IReadOnlyList<string> SlippageNotes,
    IReadOnlyList<string> SpreadNotes,
    IReadOnlyList<string> MissedSignalNotes,
    IReadOnlyList<string> ManualReviewNotes);

public sealed record ForwardTestPlan(
    string PlanVersion,
    DateTimeOffset CreatedUtc,
    string PackageId,
    string PlanStatus,
    string Mode,
    DateTimeOffset PlannedStartUtc,
    DateTimeOffset PlannedEndUtc,
    int DurationDays,
    IReadOnlyList<string> Assets,
    string Timeframe,
    string SignalSource,
    string Objective,
    bool ObservationOnly,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record ForwardTestObservation(
    string ObservationId,
    DateTimeOffset CreatedUtc,
    string SignalId,
    string Asset,
    string CandidateId,
    string ObservedStatus,
    double? ObservedPrice,
    double EntryLevel,
    double StopLoss,
    double TakeProfit,
    double InvalidationLevel,
    string Result,
    string Note,
    bool Simulated,
    bool HumanReviewRequired,
    bool NoAutoTrading);

public sealed record ForwardTestStatusSnapshot(
    string StatusVersion,
    DateTimeOffset UpdatedAtUtc,
    string PackageId,
    string ForwardTestStatus,
    string ForwardTestMode,
    IReadOnlyList<string> ForwardTestAssets,
    int ForwardTestSignalsObserved,
    int ForwardTestObservationsTotal,
    int ForwardTestTriggeredCount,
    int ForwardTestInvalidatedCount,
    int ForwardTestSimulatedObservationCount,
    DateTimeOffset? ForwardTestLatestObservationUtc,
    bool UsingCurrentMarketSnapshot,
    string ForwardTestHealth,
    bool ForwardTestRequiresHumanReview,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings,
    ForwardTestMetrics Metrics,
    string PlanPath,
    string LogPath,
    string LatestObservationsJsonPath,
    string LatestObservationsMarkdownPath,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record ForwardTestObservationLogEntry(
    DateTimeOffset TimestampUtc,
    string Action,
    string PackageId,
    string? SignalId,
    string? Result,
    string? Note,
    ForwardTestObservation? Observation,
    string ForwardTestStatus,
    string ForwardTestMode,
    int ForwardTestSignalsObserved,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings,
    ForwardTestMetrics Metrics,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed class ForwardTestService
{
    private static readonly HashSet<string> AllowedObservationResults =
    [
        "still_waiting",
        "triggered",
        "invalidated",
        "expired",
        "hypothetical_win",
        "hypothetical_loss",
        "manual_review_required",
        "simulated_observation",
    ];

    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ForwardTestService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "forward_test");
    public string PlanPath => Path.Combine(Root, "forward_test_plan.json");
    public string PlanMarkdownPath => Path.Combine(Root, "forward_test_plan.md");
    public string StatusPath => Path.Combine(Root, "forward_test_status.json");
    public string LogPath => Path.Combine(Root, "forward_test_log.jsonl");
    public string LatestObservationsJsonPath => Path.Combine(Root, "latest_observations.json");
    public string LatestObservationsMarkdownPath => Path.Combine(Root, "latest_observations.md");

    public ForwardTestStatusSnapshot CreatePlan()
    {
        var gates = ValidateForwardTestGates();
        var plan = BuildPlan(gates.Package);

        Directory.CreateDirectory(Root);
        File.WriteAllText(PlanPath, JsonSerializer.Serialize(plan, JsonDefaults.WriteOptions));
        File.WriteAllText(PlanMarkdownPath, BuildPlanMarkdown(plan));

        var status = BuildStatus(plan, gates.Blockers, [], ReadObservationLogEntries());
        WriteStatusArtifacts(status, []);
        AppendLog("create_forward_test_plan", status, null, null, null, null);
        return status;
    }

    public ForwardTestStatusSnapshot LoadOrCreateStatus()
    {
        if (File.Exists(StatusPath))
        {
            var snapshot = JsonSerializer.Deserialize<ForwardTestStatusSnapshot>(File.ReadAllText(StatusPath), JsonDefaults.SnapshotReadOptions);
            if (snapshot is not null
                && !string.IsNullOrWhiteSpace(snapshot.PlanPath)
                && !string.IsNullOrWhiteSpace(snapshot.LogPath)
                && !string.IsNullOrWhiteSpace(snapshot.LatestObservationsJsonPath)
                && !string.IsNullOrWhiteSpace(snapshot.LatestObservationsMarkdownPath))
            {
                return snapshot;
            }
        }

        var gates = ValidateForwardTestGates();
        var plan = LoadPlan() ?? BuildPlan(gates.Package);
        var entries = ReadObservationLogEntries();
        var status = BuildStatus(plan, gates.Blockers, [], entries);
        WriteStatusArtifacts(status, ExtractObservations(entries));
        AppendLog("forward_test_status_check", status, null, null, null, null);
        return status;
    }

    public ForwardTestStatusSnapshot RecordObservation(string signalId, string result, string note)
    {
        if (string.IsNullOrWhiteSpace(signalId))
        {
            throw new InvalidOperationException("signal_id_required");
        }

        var normalizedResult = NormalizeResult(result);
        var current = LoadOrCreateStatus();
        if (current.Blockers.Count > 0)
        {
            throw new InvalidOperationException($"forward_test_blocked:{string.Join(",", current.Blockers)}");
        }

        var plan = LoadPlan() ?? throw new InvalidOperationException("forward_test_plan_missing");
        var latestSignals = new DemoSignalFeedService(_storagePaths, _runtimeRoot).LoadLatestSignals();
        var signal = latestSignals.FirstOrDefault(item => item.SignalId.Equals(signalId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"forward_test_signal_not_found:{signalId}");
        var observation = BuildManualObservation(signal, normalizedResult, note);

        var entries = ReadObservationLogEntries().ToList();
        var previewStatus = BuildStatus(plan, current.Blockers, current.Warnings, entries);
        var entry = BuildLogEntry("record_forward_test_observation", previewStatus, signal.SignalId, normalizedResult, note, observation);
        entries.Add(entry);

        var status = BuildStatus(plan, current.Blockers, current.Warnings, entries);
        WriteStatusArtifacts(status, ExtractObservations(entries));
        AppendLog("record_forward_test_observation", status, signal.SignalId, normalizedResult, note, observation);
        return status;
    }

    public ForwardTestStatusSnapshot RunObservation()
    {
        var current = LoadOrCreateStatus();
        if (current.Blockers.Count > 0)
        {
            throw new InvalidOperationException($"forward_test_blocked:{string.Join(",", current.Blockers)}");
        }

        var plan = LoadPlan() ?? throw new InvalidOperationException("forward_test_plan_missing");
        var demoSignals = new DemoSignalFeedService(_storagePaths, _runtimeRoot).LoadLatestSignals();
        var warnings = new List<string>();
        var entries = ReadObservationLogEntries().ToList();
        var observations = new List<ForwardTestObservation>();

        foreach (var signal in demoSignals)
        {
            var observation = BuildObservation(signal, warnings);
            observations.Add(observation);
            var previewStatus = BuildStatus(plan, current.Blockers, warnings, entries);
            entries.Add(BuildLogEntry(
                "run_forward_test_observation",
                previewStatus,
                signal.SignalId,
                observation.Result,
                observation.Note,
                observation));
        }

        var status = BuildStatus(plan, current.Blockers, warnings, entries);
        WriteStatusArtifacts(status, ExtractObservations(entries));
        AppendLog("run_forward_test_observation_summary", status, null, null, $"observations={observations.Count}", null);
        return status;
    }

    public IReadOnlyList<ForwardTestObservation> LoadLatestObservations()
    {
        if (!File.Exists(LatestObservationsJsonPath))
        {
            return [];
        }

        var json = File.ReadAllText(LatestObservationsJsonPath);
        var observations = JsonSerializer.Deserialize<List<ForwardTestObservation>>(json, JsonDefaults.SnapshotReadOptions);
        if (observations is not null)
        {
            return observations;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var fallback = new List<ForwardTestObservation>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            fallback.Add(new ForwardTestObservation(
                ObservationId: item.GetProperty("observation_id").GetString() ?? string.Empty,
                CreatedUtc: item.GetProperty("created_utc").GetDateTimeOffset(),
                SignalId: item.GetProperty("signal_id").GetString() ?? string.Empty,
                Asset: item.GetProperty("asset").GetString() ?? string.Empty,
                CandidateId: item.GetProperty("candidate_id").GetString() ?? string.Empty,
                ObservedStatus: item.GetProperty("observed_status").GetString() ?? string.Empty,
                ObservedPrice: item.TryGetProperty("observed_price", out var observedPrice) && observedPrice.ValueKind != JsonValueKind.Null
                    ? observedPrice.GetDouble()
                    : null,
                EntryLevel: item.GetProperty("entry_level").GetDouble(),
                StopLoss: item.GetProperty("stop_loss").GetDouble(),
                TakeProfit: item.GetProperty("take_profit").GetDouble(),
                InvalidationLevel: item.GetProperty("invalidation_level").GetDouble(),
                Result: item.GetProperty("result").GetString() ?? string.Empty,
                Note: item.GetProperty("note").GetString() ?? string.Empty,
                Simulated: item.GetProperty("simulated").GetBoolean(),
                HumanReviewRequired: item.GetProperty("human_review_required").GetBoolean(),
                NoAutoTrading: item.GetProperty("no_auto_trading").GetBoolean()));
        }

        return fallback;
    }

    public ForwardTestPlan? LoadPlan()
    {
        return File.Exists(PlanPath)
            ? JsonSerializer.Deserialize<ForwardTestPlan>(File.ReadAllText(PlanPath), JsonDefaults.SnapshotReadOptions)
            : null;
    }

    private void WriteStatusArtifacts(ForwardTestStatusSnapshot status, IReadOnlyList<ForwardTestObservation> observations)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(StatusPath, JsonSerializer.Serialize(status, JsonDefaults.WriteOptions));
        File.WriteAllText(LatestObservationsJsonPath, JsonSerializer.Serialize(observations, JsonDefaults.WriteOptions));
        File.WriteAllText(LatestObservationsMarkdownPath, BuildObservationsMarkdown(status, observations));
    }

    private void AppendLog(
        string action,
        ForwardTestStatusSnapshot status,
        string? signalId,
        string? result,
        string? note,
        ForwardTestObservation? observation)
    {
        Directory.CreateDirectory(Root);
        var entry = BuildLogEntry(action, status, signalId, result, note, observation);
        File.AppendAllText(LogPath, JsonSerializer.Serialize(entry, JsonDefaults.WriteOptions) + Environment.NewLine);
    }

    private static ForwardTestObservationLogEntry BuildLogEntry(
        string action,
        ForwardTestStatusSnapshot status,
        string? signalId,
        string? result,
        string? note,
        ForwardTestObservation? observation)
    {
        return new ForwardTestObservationLogEntry(
            TimestampUtc: DateTimeOffset.UtcNow,
            Action: action,
            PackageId: status.PackageId,
            SignalId: signalId,
            Result: result,
            Note: note,
            Observation: observation,
            ForwardTestStatus: status.ForwardTestStatus,
            ForwardTestMode: status.ForwardTestMode,
            ForwardTestSignalsObserved: status.ForwardTestSignalsObserved,
            Blockers: status.Blockers,
            Warnings: status.Warnings,
            Metrics: status.Metrics,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }

    private ForwardTestStatusSnapshot BuildStatus(
        ForwardTestPlan plan,
        IReadOnlyList<string> blockers,
        IReadOnlyList<string> warnings,
        IReadOnlyList<ForwardTestObservationLogEntry> observationEntries)
    {
        var observations = ExtractObservations(observationEntries);
        var metrics = BuildMetrics(observations);
        var marketSnapshotStatus = new CurrentMarketSnapshotService(_storagePaths, _runtimeRoot).LoadOrCreateStatus();
        DateTimeOffset? latestObservationUtc = observations.Count == 0
            ? null
            : observations.Max(observation => observation.CreatedUtc);
        var health = blockers.Count > 0
            ? "needs_attention"
            : metrics.ManualReviewCount > 0 ? "needs_attention" : "ok";
        var status = blockers.Count > 0 ? "blocked" : "observation_ready";

        return new ForwardTestStatusSnapshot(
            StatusVersion: "forward_test_status_v2",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            PackageId: plan.PackageId,
            ForwardTestStatus: status,
            ForwardTestMode: plan.Mode,
            ForwardTestAssets: plan.Assets,
            ForwardTestSignalsObserved: metrics.SignalsObserved,
            ForwardTestObservationsTotal: metrics.ObservationsTotal,
            ForwardTestTriggeredCount: metrics.TriggeredCount,
            ForwardTestInvalidatedCount: metrics.InvalidatedCount,
            ForwardTestSimulatedObservationCount: metrics.SimulatedObservationCount,
            ForwardTestLatestObservationUtc: latestObservationUtc,
            UsingCurrentMarketSnapshot: marketSnapshotStatus.AssetsAvailable.Count > 0,
            ForwardTestHealth: health,
            ForwardTestRequiresHumanReview: true,
            Blockers: blockers,
            Warnings: warnings,
            Metrics: metrics,
            PlanPath: PlanPath,
            LogPath: LogPath,
            LatestObservationsJsonPath: LatestObservationsJsonPath,
            LatestObservationsMarkdownPath: LatestObservationsMarkdownPath,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }

    private static ForwardTestMetrics BuildMetrics(IReadOnlyList<ForwardTestObservation> observations)
    {
        var uniqueSignals = observations.Select(observation => observation.SignalId).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var triggered = observations.Count(observation => observation.Result == "triggered");
        var invalidated = observations.Count(observation => observation.Result == "invalidated");
        var expired = observations.Count(observation => observation.Result == "expired");
        var wins = observations.Count(observation => observation.Result == "hypothetical_win");
        var losses = observations.Count(observation => observation.Result == "hypothetical_loss");
        var manualReviews = observations.Count(observation => observation.Result == "manual_review_required");
        var simulated = observations.Count(observation => observation.Result == "simulated_observation");
        var completedHypotheticals = wins + losses;
        var winRate = completedHypotheticals == 0 ? 0 : Math.Round((double)wins / completedHypotheticals, 4);
        var averageR = completedHypotheticals == 0 ? 0 : Math.Round((wins - losses) / (double)completedHypotheticals, 4);
        var runningDrawdown = 0.0;
        var maxDrawdown = 0.0;
        foreach (var observation in observations)
        {
            if (observation.Result == "hypothetical_loss")
            {
                runningDrawdown += 1.0;
                maxDrawdown = Math.Max(maxDrawdown, runningDrawdown);
            }
            else if (observation.Result == "hypothetical_win")
            {
                runningDrawdown = Math.Max(0, runningDrawdown - 1.0);
            }
        }

        return new ForwardTestMetrics(
            SignalsGenerated: uniqueSignals,
            SignalsObserved: uniqueSignals,
            ObservationsTotal: observations.Count,
            TriggeredCount: triggered,
            InvalidatedCount: invalidated,
            ExpiredCount: expired,
            HypotheticalWins: wins,
            HypotheticalLosses: losses,
            ManualReviewCount: manualReviews,
            SimulatedObservationCount: simulated,
            WinRate: winRate,
            AverageR: averageR,
            MaxDrawdownR: Math.Round(maxDrawdown, 4),
            MaxDailyDrawdownR: Math.Round(maxDrawdown, 4),
            SlippageNotes: observations.Where(observation => observation.Note.Contains("slippage", StringComparison.OrdinalIgnoreCase)).Select(observation => observation.Note).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SpreadNotes: observations.Where(observation => observation.Note.Contains("spread", StringComparison.OrdinalIgnoreCase)).Select(observation => observation.Note).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            MissedSignalNotes: observations.Where(observation => observation.Note.Contains("missed", StringComparison.OrdinalIgnoreCase)).Select(observation => observation.Note).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ManualReviewNotes: observations.Where(observation => observation.Result == "manual_review_required").Select(observation => observation.Note).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    private IReadOnlyList<ForwardTestObservationLogEntry> ReadObservationLogEntries()
    {
        if (!File.Exists(LogPath))
        {
            return [];
        }

        var entries = new List<ForwardTestObservationLogEntry>();
        foreach (var line in File.ReadLines(LogPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize<ForwardTestObservationLogEntry>(line, JsonDefaults.SnapshotReadOptions);
            if (entry is not null)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    private static List<ForwardTestObservation> ExtractObservations(IReadOnlyList<ForwardTestObservationLogEntry> entries)
    {
        return entries
            .Where(entry => entry.Observation is not null)
            .Select(entry => entry.Observation!)
            .OrderByDescending(observation => observation.CreatedUtc)
            .Take(200)
            .ToList();
    }

    private ForwardTestObservation BuildObservation(DemoSignalFeedItem signal, List<string> warnings)
    {
        var marketSnapshot = new CurrentMarketSnapshotService(_storagePaths, _runtimeRoot).FindSnapshot(signal.Asset);
        if (marketSnapshot is not null
            && marketSnapshot.Status == "available"
            && marketSnapshot.IsLiveReadonly
            && !marketSnapshot.IsPlaceholder
            && marketSnapshot.Mid.HasValue)
        {
            var snapshotResult = EvaluateSnapshotObservation(signal, marketSnapshot);
            return new ForwardTestObservation(
                ObservationId: $"observation_{signal.SignalId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
                CreatedUtc: DateTimeOffset.UtcNow,
                SignalId: signal.SignalId,
                Asset: signal.Asset,
                CandidateId: signal.CandidateId,
                ObservedStatus: snapshotResult.ObservedStatus,
                ObservedPrice: RoundPrice(signal.Asset, marketSnapshot.Mid.Value),
                EntryLevel: signal.EntryLevel,
                StopLoss: signal.StopLoss,
                TakeProfit: signal.TakeProfit,
                InvalidationLevel: signal.InvalidationLevel,
                Result: snapshotResult.Result,
                Note: snapshotResult.Note,
                Simulated: false,
                HumanReviewRequired: true,
                NoAutoTrading: true);
        }

        if (marketSnapshot is not null && marketSnapshot.Status != "unavailable")
        {
            warnings.Add($"current_market_snapshot_not_usable:{signal.Asset}:{marketSnapshot.Status}");
        }

        var candle = LoadLatestCandle(signal.Asset, signal.Timeframe);
        if (candle is null)
        {
            return new ForwardTestObservation(
                ObservationId: $"observation_{signal.SignalId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
                CreatedUtc: DateTimeOffset.UtcNow,
                SignalId: signal.SignalId,
                Asset: signal.Asset,
                CandidateId: signal.CandidateId,
                ObservedStatus: "simulated_observation",
                ObservedPrice: null,
                EntryLevel: signal.EntryLevel,
                StopLoss: signal.StopLoss,
                TakeProfit: signal.TakeProfit,
                InvalidationLevel: signal.InvalidationLevel,
                Result: "simulated_observation",
                Note: "simulated_observation:no_current_market_data_available;tracking_structure_only;no_real_performance_claim",
                Simulated: true,
                HumanReviewRequired: true,
                NoAutoTrading: true);
        }

        var result = EvaluateSignalObservation(signal, candle);
        if (DateTimeOffset.UtcNow - candle.TimestampUtc > TimeSpan.FromDays(7))
        {
            warnings.Add($"market_data_stale_for_observation:{signal.Asset}:{signal.Timeframe}:{candle.TimestampUtc:O}");
        }

        return new ForwardTestObservation(
            ObservationId: $"observation_{signal.SignalId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            CreatedUtc: DateTimeOffset.UtcNow,
            SignalId: signal.SignalId,
            Asset: signal.Asset,
            CandidateId: signal.CandidateId,
            ObservedStatus: result.ObservedStatus,
            ObservedPrice: RoundPrice(signal.Asset, candle.Close),
            EntryLevel: signal.EntryLevel,
            StopLoss: signal.StopLoss,
            TakeProfit: signal.TakeProfit,
            InvalidationLevel: signal.InvalidationLevel,
            Result: result.Result,
            Note: result.Note,
            Simulated: false,
            HumanReviewRequired: true,
            NoAutoTrading: true);
    }

    private static (string ObservedStatus, string Result, string Note) EvaluateSnapshotObservation(
        DemoSignalFeedItem signal,
        CurrentMarketAssetSnapshot snapshot)
    {
        var price = snapshot.Mid ?? snapshot.Bid ?? snapshot.Ask ?? 0;
        var direction = signal.Direction.ToLowerInvariant();
        var isShort = direction.Contains("short", StringComparison.OrdinalIgnoreCase);
        var isLong = direction.Contains("long", StringComparison.OrdinalIgnoreCase) || !isShort;

        if (isLong)
        {
            if (price <= signal.InvalidationLevel || price <= signal.StopLoss)
            {
                return ("invalidated", "invalidated", $"read_only_snapshot_observation:invalidation_level_reached;source={snapshot.Source};no_order");
            }

            if (price >= signal.EntryLevel)
            {
                return ("triggered", "triggered", $"read_only_snapshot_observation:entry_level_reached;source={snapshot.Source};no_order");
            }
        }
        else
        {
            if (price >= signal.InvalidationLevel || price >= signal.StopLoss)
            {
                return ("invalidated", "invalidated", $"read_only_snapshot_observation:invalidation_level_reached;source={snapshot.Source};no_order");
            }

            if (price <= signal.EntryLevel)
            {
                return ("triggered", "triggered", $"read_only_snapshot_observation:entry_level_reached;source={snapshot.Source};no_order");
            }
        }

        var age = DateTimeOffset.UtcNow - signal.CreatedUtc;
        if (age > TimeSpan.FromDays(2))
        {
            return ("expired", "expired", $"read_only_snapshot_observation:signal_age_expired;source={snapshot.Source};no_order");
        }

        return ("still_waiting", "still_waiting", $"read_only_snapshot_observation:still_waiting;source={snapshot.Source};no_order");
    }

    private static (string ObservedStatus, string Result, string Note) EvaluateSignalObservation(DemoSignalFeedItem signal, MarketDataCandle candle)
    {
        var close = candle.Close;
        var high = candle.High;
        var low = candle.Low;
        var direction = signal.Direction.ToLowerInvariant();
        var isShort = direction.Contains("short", StringComparison.OrdinalIgnoreCase);
        var isLong = direction.Contains("long", StringComparison.OrdinalIgnoreCase);

        if (isLong)
        {
            if (low <= signal.StopLoss)
            {
                return ("invalidated", "invalidated", "read_only_observation:stop_level_touched;no_order");
            }

            if (high >= signal.TakeProfit)
            {
                return ("triggered", "hypothetical_win", "read_only_observation:take_profit_zone_reached;no_order");
            }

            if (close >= signal.EntryLevel)
            {
                return ("triggered", "triggered", "read_only_observation:entry_zone_reached;no_order");
            }
        }
        else if (isShort)
        {
            if (high >= signal.StopLoss)
            {
                return ("invalidated", "invalidated", "read_only_observation:stop_level_touched;no_order");
            }

            if (low <= signal.TakeProfit)
            {
                return ("triggered", "hypothetical_win", "read_only_observation:take_profit_zone_reached;no_order");
            }

            if (close <= signal.EntryLevel)
            {
                return ("triggered", "triggered", "read_only_observation:entry_zone_reached;no_order");
            }
        }

        var age = DateTimeOffset.UtcNow - signal.CreatedUtc;
        if (age > TimeSpan.FromDays(2))
        {
            return ("expired", "expired", "read_only_observation:signal_age_expired;no_order");
        }

        return ("still_waiting", "still_waiting", "read_only_observation:still_waiting;no_order");
    }

    private static ForwardTestObservation BuildManualObservation(DemoSignalFeedItem signal, string result, string note)
    {
        return new ForwardTestObservation(
            ObservationId: $"observation_{signal.SignalId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            CreatedUtc: DateTimeOffset.UtcNow,
            SignalId: signal.SignalId,
            Asset: signal.Asset,
            CandidateId: signal.CandidateId,
            ObservedStatus: result,
            ObservedPrice: null,
            EntryLevel: signal.EntryLevel,
            StopLoss: signal.StopLoss,
            TakeProfit: signal.TakeProfit,
            InvalidationLevel: signal.InvalidationLevel,
            Result: result,
            Note: string.IsNullOrWhiteSpace(note) ? "manual_forward_test_observation" : note,
            Simulated: result == "simulated_observation",
            HumanReviewRequired: true,
            NoAutoTrading: true);
    }

    private string NormalizeResult(string result)
    {
        var normalizedResult = result.Trim().ToLowerInvariant();
        if (!AllowedObservationResults.Contains(normalizedResult))
        {
            throw new InvalidOperationException($"invalid_forward_test_result:{result}");
        }

        return normalizedResult;
    }

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

    private (EnsembleSignalAgentPackage? Package, List<string> Blockers) ValidateForwardTestGates()
    {
        var blockers = new List<string>();
        var exportService = new ScalpingEnsembleExportService(_storagePaths, _runtimeRoot);
        var package = File.Exists(exportService.SignalAgentJsonPath)
            ? JsonSerializer.Deserialize<EnsembleSignalAgentPackage>(File.ReadAllText(exportService.SignalAgentJsonPath), JsonDefaults.SnapshotReadOptions)
            : null;
        var reviewState = new ScalpingEnsembleReviewService(_storagePaths, _runtimeRoot).LoadOrCreate();
        var demoFeed = new DemoSignalFeedService(_storagePaths, _runtimeRoot).LoadOrCreateStatus();

        if (package is null) blockers.Add("ensemble_signal_agent_package_missing");
        if (package is not null && !package.Status.Equals("ensemble_ready", StringComparison.OrdinalIgnoreCase)) blockers.Add($"ensemble_not_ready:{package.Status}");
        if (reviewState.ReviewStatus is not ScalpingEnsembleReviewStatus.approved_for_demo_signal_use and not ScalpingEnsembleReviewStatus.approved_for_forward_test_preparation)
        {
            blockers.Add($"ensemble_not_approved_for_forward_test_preparation:{reviewState.ReviewStatus}");
        }

        if (!demoFeed.DemoSignalsAvailable) blockers.Add("demo_signal_feed_not_ready");
        if (package is not null && !package.HumanReviewRequired) blockers.Add("human_review_required_not_confirmed");
        return (package, blockers);
    }

    private static ForwardTestPlan BuildPlan(EnsembleSignalAgentPackage? package)
    {
        var start = DateTimeOffset.UtcNow;
        var end = start.AddDays(21);
        return new ForwardTestPlan(
            PlanVersion: "forward_test_plan_v1",
            CreatedUtc: DateTimeOffset.UtcNow,
            PackageId: package?.PackageId ?? "missing_package",
            PlanStatus: "forward_test_preparation_ready",
            Mode: "demo_signal_observation",
            PlannedStartUtc: start,
            PlannedEndUtc: end,
            DurationDays: 21,
            Assets: package?.Members.Select(member => member.Asset).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(asset => asset).ToList() ?? ["EURUSD", "XAUUSD"],
            Timeframe: "M5",
            SignalSource: "Demo Signal Feed",
            Objective: "Vergleich Backtest/Simulation vs. reale Signalqualitaet ohne Trades oder Orders.",
            ObservationOnly: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }

    private static string BuildPlanMarkdown(ForwardTestPlan plan) => $"""
# Forward Test Plan

- package_id: {plan.PackageId}
- plan_status: {plan.PlanStatus}
- mode: {plan.Mode}
- planned_start_utc: {plan.PlannedStartUtc:O}
- planned_end_utc: {plan.PlannedEndUtc:O}
- duration_days: {plan.DurationDays}
- assets: {string.Join(", ", plan.Assets)}
- timeframe: {plan.Timeframe}
- signal_source: {plan.SignalSource}
- objective: {plan.Objective}

## Metrics
- signals_observed
- observations_total
- triggered_count
- invalidated_count
- expired_count
- hypothetical_wins
- hypothetical_losses
- manual_review_count
- simulated_observation_count
- forward_test_health

## Safety
- Observation only
- No orders
- No broker action
- No cTrader Order API
- no_auto_trading=true
- human_review_required=true
""";

    private static string BuildObservationsMarkdown(ForwardTestStatusSnapshot status, IReadOnlyList<ForwardTestObservation> observations)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Forward Test Observations");
        builder.AppendLine();
        builder.AppendLine($"- forward_test_status: {status.ForwardTestStatus}");
        builder.AppendLine($"- forward_test_mode: {status.ForwardTestMode}");
        builder.AppendLine($"- observations_total: {status.ForwardTestObservationsTotal}");
        builder.AppendLine($"- triggered_count: {status.ForwardTestTriggeredCount}");
        builder.AppendLine($"- invalidated_count: {status.ForwardTestInvalidatedCount}");
        builder.AppendLine($"- simulated_observation_count: {status.ForwardTestSimulatedObservationCount}");
        builder.AppendLine("- Observation only");
        builder.AppendLine("- No orders");
        builder.AppendLine("- No broker action");
        builder.AppendLine("- No cTrader Order API");
        builder.AppendLine("- no_auto_trading=true");
        builder.AppendLine("- human_review_required=true");
        builder.AppendLine();
        foreach (var observation in observations)
        {
            builder.AppendLine($"## {observation.ObservationId}");
            builder.AppendLine($"- signal_id: {observation.SignalId}");
            builder.AppendLine($"- asset: {observation.Asset}");
            builder.AppendLine($"- candidate_id: {observation.CandidateId}");
            builder.AppendLine($"- observed_status: {observation.ObservedStatus}");
            builder.AppendLine($"- observed_price: {(observation.ObservedPrice.HasValue ? observation.ObservedPrice.Value.ToString("0.#####") : "n/a")}");
            builder.AppendLine($"- result: {observation.Result}");
            builder.AppendLine($"- simulated: {observation.Simulated}");
            builder.AppendLine($"- note: {observation.Note}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static double RoundPrice(string asset, double value) => asset.ToUpperInvariant() switch
    {
        "EURUSD" => Math.Round(value, 5),
        _ => Math.Round(value, 2)
    };
}
