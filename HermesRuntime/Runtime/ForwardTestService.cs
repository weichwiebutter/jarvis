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
    string SignalLifecycleStatus,
    string ObservedStatus,
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
        "waiting_for_trigger",
        "watching",
        "armed",
        "triggered",
        "active",
        "near_miss",
        "invalidated",
        "expired",
        "completed",
        "no_signal",
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
                SignalLifecycleStatus: item.TryGetProperty("signal_lifecycle_status", out var signalLifecycleStatus) ? signalLifecycleStatus.GetString() ?? string.Empty : string.Empty,
                ObservedStatus: item.GetProperty("observed_status").GetString() ?? string.Empty,
                ObservedPrice: item.TryGetProperty("observed_price", out var observedPrice) && observedPrice.ValueKind != JsonValueKind.Null
                    ? observedPrice.GetDouble()
                    : null,
                ObservedHigh: item.TryGetProperty("observed_high", out var observedHigh) && observedHigh.ValueKind != JsonValueKind.Null
                    ? observedHigh.GetDouble()
                    : null,
                ObservedLow: item.TryGetProperty("observed_low", out var observedLow) && observedLow.ValueKind != JsonValueKind.Null
                    ? observedLow.GetDouble()
                    : null,
                EntryLevel: item.GetProperty("entry_level").GetDouble(),
                EntryZoneLower: item.TryGetProperty("entry_zone_lower", out var entryZoneLower) ? entryZoneLower.GetDouble() : item.GetProperty("entry_level").GetDouble(),
                EntryZoneUpper: item.TryGetProperty("entry_zone_upper", out var entryZoneUpper) ? entryZoneUpper.GetDouble() : item.GetProperty("entry_level").GetDouble(),
                StopLoss: item.GetProperty("stop_loss").GetDouble(),
                TakeProfit: item.GetProperty("take_profit").GetDouble(),
                InvalidationLevel: item.GetProperty("invalidation_level").GetDouble(),
                ObservedEntryHit: item.TryGetProperty("observed_entry_hit", out var observedEntryHit) && observedEntryHit.GetBoolean(),
                ObservedInvalidationHit: item.TryGetProperty("observed_invalidation_hit", out var observedInvalidationHit) && observedInvalidationHit.GetBoolean(),
                ObservedStopLossHit: item.TryGetProperty("observed_stop_loss_hit", out var observedStopLossHit) && observedStopLossHit.GetBoolean(),
                ObservedTakeProfitHit: item.TryGetProperty("observed_take_profit_hit", out var observedTakeProfitHit) && observedTakeProfitHit.GetBoolean(),
                ObservedNearMiss: item.TryGetProperty("observed_near_miss", out var observedNearMiss) && observedNearMiss.GetBoolean(),
                ObservedExpired: item.TryGetProperty("observed_expired", out var observedExpired) && observedExpired.GetBoolean(),
                OutcomePending: item.TryGetProperty("outcome_pending", out var outcomePending) ? outcomePending.GetBoolean() : true,
                HypotheticalResult: item.TryGetProperty("hypothetical_result", out var hypotheticalResult) ? hypotheticalResult.GetString() ?? string.Empty : string.Empty,
                RMultiple: item.TryGetProperty("r_multiple", out var rMultiple) && rMultiple.ValueKind != JsonValueKind.Null ? rMultiple.GetDouble() : null,
                RequiresHumanReview: item.TryGetProperty("requires_human_review", out var requiresHumanReview) ? requiresHumanReview.GetBoolean() : true,
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
        var triggered = observations.Count(observation => observation.SignalLifecycleStatus is "triggered" or "active" or "completed");
        var invalidated = observations.Count(observation => observation.Result == "invalidated");
        var expired = observations.Count(observation => observation.Result == "expired");
        var wins = observations.Count(observation => observation.HypotheticalResult == "hypothetical_win");
        var losses = observations.Count(observation => observation.HypotheticalResult == "hypothetical_loss");
        var manualReviews = observations.Count(observation => observation.RequiresHumanReview);
        var simulated = observations.Count(observation => observation.Result == "simulated_observation");
        var completedHypotheticals = wins + losses;
        var winRate = completedHypotheticals == 0 ? 0 : Math.Round((double)wins / completedHypotheticals, 4);
        var averageR = observations.Where(observation => observation.RMultiple.HasValue).Select(observation => observation.RMultiple!.Value).DefaultIfEmpty(0).Average();
        var runningDrawdown = 0.0;
        var maxDrawdown = 0.0;
        foreach (var observation in observations)
        {
            if (observation.HypotheticalResult == "hypothetical_loss")
            {
                runningDrawdown += 1.0;
                maxDrawdown = Math.Max(maxDrawdown, runningDrawdown);
            }
            else if (observation.HypotheticalResult == "hypothetical_win")
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
            AverageR: Math.Round(averageR, 4),
            MaxDrawdownR: Math.Round(maxDrawdown, 4),
            MaxDailyDrawdownR: Math.Round(maxDrawdown, 4),
            SlippageNotes: observations.Where(observation => observation.Note.Contains("slippage", StringComparison.OrdinalIgnoreCase)).Select(observation => observation.Note).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SpreadNotes: observations.Where(observation => observation.Note.Contains("spread", StringComparison.OrdinalIgnoreCase)).Select(observation => observation.Note).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            MissedSignalNotes: observations.Where(observation => observation.Note.Contains("missed", StringComparison.OrdinalIgnoreCase)).Select(observation => observation.Note).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ManualReviewNotes: observations.Where(observation => observation.RequiresHumanReview).Select(observation => observation.Note).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
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
        var evaluation = new SignalWatchService(_storagePaths, _runtimeRoot).EvaluateSignal(signal, warnings);
        var result = evaluation.HypotheticalResult switch
        {
            "hypothetical_win" => "hypothetical_win",
            "hypothetical_loss" => "hypothetical_loss",
            "expired" => "expired",
            "near_miss" => "near_miss",
            "invalidated" => "invalidated",
            "no_signal" => "no_signal",
            _ when evaluation.Simulated => "simulated_observation",
            _ when evaluation.RequiresHumanReview => "manual_review_required",
            _ => evaluation.SignalLifecycleStatus
        };

        return new ForwardTestObservation(
            ObservationId: $"observation_{signal.SignalId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            CreatedUtc: DateTimeOffset.UtcNow,
            SignalId: signal.SignalId,
            Asset: signal.Asset,
            CandidateId: signal.CandidateId,
            SignalLifecycleStatus: evaluation.SignalLifecycleStatus,
            ObservedStatus: evaluation.SignalLifecycleStatus,
            ObservedPrice: evaluation.ObservedPrice,
            ObservedHigh: evaluation.ObservedHigh,
            ObservedLow: evaluation.ObservedLow,
            EntryLevel: signal.EntryLevel,
            EntryZoneLower: evaluation.EntryZoneLower,
            EntryZoneUpper: evaluation.EntryZoneUpper,
            StopLoss: signal.StopLoss,
            TakeProfit: signal.TakeProfit,
            InvalidationLevel: signal.InvalidationLevel,
            ObservedEntryHit: evaluation.ObservedEntryHit,
            ObservedInvalidationHit: evaluation.ObservedInvalidationHit,
            ObservedStopLossHit: evaluation.ObservedStopLossHit,
            ObservedTakeProfitHit: evaluation.ObservedTakeProfitHit,
            ObservedNearMiss: evaluation.ObservedNearMiss,
            ObservedExpired: evaluation.ObservedExpired,
            OutcomePending: evaluation.OutcomePending,
            HypotheticalResult: evaluation.HypotheticalResult,
            RMultiple: evaluation.RMultiple,
            RequiresHumanReview: evaluation.RequiresHumanReview,
            Result: result,
            Note: $"{evaluation.Note};source={evaluation.MarketDataSource}",
            Simulated: evaluation.Simulated,
            HumanReviewRequired: true,
            NoAutoTrading: true);
    }

    private static ForwardTestObservation BuildManualObservation(DemoSignalFeedItem signal, string result, string note)
    {
        return new ForwardTestObservation(
            ObservationId: $"observation_{signal.SignalId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            CreatedUtc: DateTimeOffset.UtcNow,
            SignalId: signal.SignalId,
            Asset: signal.Asset,
            CandidateId: signal.CandidateId,
            SignalLifecycleStatus: result,
            ObservedStatus: result,
            ObservedPrice: null,
            ObservedHigh: null,
            ObservedLow: null,
            EntryLevel: signal.EntryLevel,
            EntryZoneLower: signal.EntryZoneLower ?? signal.EntryLevel,
            EntryZoneUpper: signal.EntryZoneUpper ?? signal.EntryLevel,
            StopLoss: signal.StopLoss,
            TakeProfit: signal.TakeProfit,
            InvalidationLevel: signal.InvalidationLevel,
            ObservedEntryHit: result is "triggered" or "active" or "completed",
            ObservedInvalidationHit: result == "invalidated",
            ObservedStopLossHit: result == "hypothetical_loss",
            ObservedTakeProfitHit: result == "hypothetical_win",
            ObservedNearMiss: result == "near_miss",
            ObservedExpired: result == "expired",
            OutcomePending: result is "waiting_for_trigger" or "watching" or "armed" or "triggered" or "active",
            HypotheticalResult: result is "hypothetical_win" or "hypothetical_loss" ? result : result == "expired" ? "expired" : "outcome_pending",
            RMultiple: null,
            RequiresHumanReview: true,
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
- near_miss_count
- completed_count
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
            builder.AppendLine($"- signal_lifecycle_status: {observation.SignalLifecycleStatus}");
            builder.AppendLine($"- observed_status: {observation.ObservedStatus}");
            builder.AppendLine($"- observed_price: {(observation.ObservedPrice.HasValue ? observation.ObservedPrice.Value.ToString("0.#####") : "n/a")}");
            builder.AppendLine($"- observed_entry_hit: {observation.ObservedEntryHit.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- observed_invalidation_hit: {observation.ObservedInvalidationHit.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- observed_stop_loss_hit: {observation.ObservedStopLossHit.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- observed_take_profit_hit: {observation.ObservedTakeProfitHit.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- observed_near_miss: {observation.ObservedNearMiss.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- observed_expired: {observation.ObservedExpired.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- outcome_pending: {observation.OutcomePending.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- hypothetical_result: {observation.HypotheticalResult}");
            builder.AppendLine($"- r_multiple: {(observation.RMultiple.HasValue ? observation.RMultiple.Value.ToString("0.####") : "n/a")}");
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
