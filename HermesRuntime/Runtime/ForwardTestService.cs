using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ForwardTestMetrics(
    int SignalsGenerated,
    int SignalsTriggered,
    int SignalsInvalidated,
    int HypotheticalWins,
    int HypotheticalLosses,
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

public sealed record ForwardTestStatusSnapshot(
    string StatusVersion,
    DateTimeOffset UpdatedAtUtc,
    string PackageId,
    string ForwardTestStatus,
    string ForwardTestMode,
    IReadOnlyList<string> ForwardTestAssets,
    int ForwardTestSignalsObserved,
    string ForwardTestHealth,
    bool ForwardTestRequiresHumanReview,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings,
    ForwardTestMetrics Metrics,
    string PlanPath,
    string LogPath,
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
        "triggered",
        "invalidated",
        "expired",
        "hypothetical_win",
        "hypothetical_loss",
        "manual_review",
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

    public ForwardTestStatusSnapshot CreatePlan()
    {
        var gates = ValidateForwardTestGates();
        var plan = BuildPlan(gates.Package);
        var warnings = new List<string>();
        var blockers = gates.Blockers;

        Directory.CreateDirectory(Root);
        File.WriteAllText(PlanPath, JsonSerializer.Serialize(plan, JsonDefaults.WriteOptions));
        File.WriteAllText(PlanMarkdownPath, BuildPlanMarkdown(plan));

        var status = BuildStatus(plan, blockers, warnings, ReadObservationEntries());
        WriteStatus(status);
        AppendLog("create_forward_test_plan", status, null, null, null);
        return status;
    }

    public ForwardTestStatusSnapshot LoadOrCreateStatus()
    {
        if (File.Exists(StatusPath))
        {
            var snapshot = JsonSerializer.Deserialize<ForwardTestStatusSnapshot>(File.ReadAllText(StatusPath), JsonDefaults.SnapshotReadOptions);
            if (snapshot is not null)
            {
                return snapshot;
            }
        }

        var gates = ValidateForwardTestGates();
        var plan = LoadPlan() ?? BuildPlan(gates.Package);
        var status = BuildStatus(plan, gates.Blockers, [], ReadObservationEntries());
        WriteStatus(status);
        AppendLog("forward_test_status_check", status, null, null, null);
        return status;
    }

    public ForwardTestStatusSnapshot RecordObservation(string signalId, string result, string note)
    {
        if (string.IsNullOrWhiteSpace(signalId))
        {
            throw new InvalidOperationException("signal_id_required");
        }

        var normalizedResult = result.Trim().ToLowerInvariant();
        if (!AllowedObservationResults.Contains(normalizedResult))
        {
            throw new InvalidOperationException($"invalid_forward_test_result:{result}");
        }

        var current = LoadOrCreateStatus();
        if (current.Blockers.Count > 0)
        {
            throw new InvalidOperationException($"forward_test_blocked:{string.Join(",", current.Blockers)}");
        }

        var plan = LoadPlan() ?? throw new InvalidOperationException("forward_test_plan_missing");
        var observations = ReadObservationEntries()
            .Where(entry => entry.Action == "record_forward_test_observation")
            .ToList();

        observations.Add(new ForwardTestObservationLogEntry(
            TimestampUtc: DateTimeOffset.UtcNow,
            Action: "record_forward_test_observation",
            PackageId: plan.PackageId,
            SignalId: signalId,
            Result: normalizedResult,
            Note: note,
            ForwardTestStatus: current.ForwardTestStatus,
            ForwardTestMode: current.ForwardTestMode,
            ForwardTestSignalsObserved: current.ForwardTestSignalsObserved,
            Blockers: current.Blockers,
            Warnings: current.Warnings,
            Metrics: current.Metrics,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false));

        var status = BuildStatus(plan, current.Blockers, current.Warnings, observations);
        WriteStatus(status);
        AppendLog("record_forward_test_observation", status, signalId, normalizedResult, note);
        return status;
    }

    public ForwardTestPlan? LoadPlan()
    {
        return File.Exists(PlanPath)
            ? JsonSerializer.Deserialize<ForwardTestPlan>(File.ReadAllText(PlanPath), JsonDefaults.SnapshotReadOptions)
            : null;
    }

    private void WriteStatus(ForwardTestStatusSnapshot status)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(StatusPath, JsonSerializer.Serialize(status, JsonDefaults.WriteOptions));
    }

    private void AppendLog(string action, ForwardTestStatusSnapshot status, string? signalId, string? result, string? note)
    {
        Directory.CreateDirectory(Root);
        var entry = new ForwardTestObservationLogEntry(
            TimestampUtc: DateTimeOffset.UtcNow,
            Action: action,
            PackageId: status.PackageId,
            SignalId: signalId,
            Result: result,
            Note: note,
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
        File.AppendAllText(LogPath, JsonSerializer.Serialize(entry, JsonDefaults.WriteOptions) + Environment.NewLine);
    }

    private ForwardTestStatusSnapshot BuildStatus(
        ForwardTestPlan plan,
        IReadOnlyList<string> blockers,
        IReadOnlyList<string> warnings,
        IReadOnlyList<ForwardTestObservationLogEntry> observationEntries)
    {
        var observationOnlyEntries = observationEntries
            .Where(entry => entry.Action == "record_forward_test_observation")
            .ToList();
        var metrics = BuildMetrics(observationOnlyEntries);
        var observed = observationOnlyEntries.Count;
        var health = blockers.Count > 0 ? "needs_attention" : "ok";
        var status = blockers.Count > 0 ? "blocked" : "observation_ready";

        return new ForwardTestStatusSnapshot(
            StatusVersion: "forward_test_status_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            PackageId: plan.PackageId,
            ForwardTestStatus: status,
            ForwardTestMode: plan.Mode,
            ForwardTestAssets: plan.Assets,
            ForwardTestSignalsObserved: observed,
            ForwardTestHealth: health,
            ForwardTestRequiresHumanReview: true,
            Blockers: blockers,
            Warnings: warnings,
            Metrics: metrics,
            PlanPath: PlanPath,
            LogPath: LogPath,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }

    private static ForwardTestMetrics BuildMetrics(IReadOnlyList<ForwardTestObservationLogEntry> observations)
    {
        var triggered = observations.Count(entry => entry.Result == "triggered");
        var invalidated = observations.Count(entry => entry.Result == "invalidated");
        var wins = observations.Count(entry => entry.Result == "hypothetical_win");
        var losses = observations.Count(entry => entry.Result == "hypothetical_loss");
        var completedHypotheticals = wins + losses;
        var winRate = completedHypotheticals == 0 ? 0 : Math.Round((double)wins / completedHypotheticals, 4);
        var averageR = completedHypotheticals == 0 ? 0 : Math.Round((wins - losses) / (double)completedHypotheticals, 4);
        var runningDrawdown = 0.0;
        var maxDrawdown = 0.0;
        foreach (var entry in observations)
        {
            if (entry.Result == "hypothetical_loss")
            {
                runningDrawdown += 1.0;
                maxDrawdown = Math.Max(maxDrawdown, runningDrawdown);
            }
            else if (entry.Result == "hypothetical_win")
            {
                runningDrawdown = Math.Max(0, runningDrawdown - 1.0);
            }
        }

        return new ForwardTestMetrics(
            SignalsGenerated: 0,
            SignalsTriggered: triggered,
            SignalsInvalidated: invalidated,
            HypotheticalWins: wins,
            HypotheticalLosses: losses,
            WinRate: winRate,
            AverageR: averageR,
            MaxDrawdownR: Math.Round(maxDrawdown, 4),
            MaxDailyDrawdownR: Math.Round(maxDrawdown, 4),
            SlippageNotes: observations.Where(entry => entry.Note?.Contains("slippage", StringComparison.OrdinalIgnoreCase) == true).Select(entry => entry.Note!).ToList(),
            SpreadNotes: observations.Where(entry => entry.Note?.Contains("spread", StringComparison.OrdinalIgnoreCase) == true).Select(entry => entry.Note!).ToList(),
            MissedSignalNotes: observations.Where(entry => entry.Note?.Contains("missed", StringComparison.OrdinalIgnoreCase) == true).Select(entry => entry.Note!).ToList(),
            ManualReviewNotes: observations.Where(entry => entry.Result == "manual_review" && !string.IsNullOrWhiteSpace(entry.Note)).Select(entry => entry.Note!).ToList());
    }

    private IReadOnlyList<ForwardTestObservationLogEntry> ReadObservationEntries()
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
- signals_generated
- signals_triggered
- signals_invalidated
- hypothetical_wins
- hypothetical_losses
- win_rate
- average_r
- max_drawdown_r
- max_daily_drawdown_r
- slippage_notes
- spread_notes
- missed_signal_notes
- manual_review_notes

## Safety
Forward Test ist:
- Beobachtung
- Demo-Signal-Tracking
- keine Order
- kein Live-Trading
- kein Broker-Zugriff
- no_auto_trading=true
- human_review_required=true
- broker_orders_enabled=false
- live_trading_enabled=false
""";
}
