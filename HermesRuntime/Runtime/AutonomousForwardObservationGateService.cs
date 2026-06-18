using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record AutonomousForwardObservationGateObservation(
    string ObservationId,
    string ForwardValidationJobId,
    string SourceOosJobId,
    string HypothesisId,
    string Asset,
    string Timeframe,
    string StrategyPattern,
    string MarketSnapshotStatus,
    string SignalStatus,
    string Result,
    string Note,
    DateTimeOffset ObservedAtUtc,
    bool NoOrders,
    bool NoAutoTrading);

public sealed record AutonomousForwardObservationGateReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string GateStatus,
    string WindowStatus,
    int PlansSeen,
    int PlansReadyToObserve,
    int PlansWaiting,
    int PlansBlocked,
    AutonomousForwardValidationPlan? SelectedPlan,
    AutonomousForwardObservationGateObservation? Observation,
    IReadOnlyList<string> SourceReports,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string NextSafeStep,
    string SafetySummary,
    bool FrankRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class AutonomousForwardObservationGateService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public AutonomousForwardObservationGateService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "autonomous_forward_observation_gate");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "autonomous_forward_observation_gate.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "autonomous_forward_observation_gate.md");
    public string HistoryPath => Path.Combine(Root, "history", "autonomous_forward_observation_history.jsonl");

    public AutonomousForwardObservationGateReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AutonomousForwardObservationGateReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public AutonomousForwardObservationGateReport Run()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);

        var forwardPlanning = new AutonomousForwardValidationPlanningService(_storagePaths, _runtimeRoot).Load()
            ?? new AutonomousForwardValidationPlanningService(_storagePaths, _runtimeRoot).Run();
        var currentMarket = new CurrentMarketSnapshotService(_storagePaths, _runtimeRoot).LoadOrCreateStatus();
        var timeControl = new HermesInternalScheduler(_storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json")).GetTimeControlStatus();
        var forwardSignals = LoadDemoSignals();
        var warnings = new List<string>();
        var readyPlans = forwardPlanning.Plans
            .Where(plan => plan.ReadinessStatus.Equals("ready_to_observe", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var waitingPlans = forwardPlanning.Plans
            .Count(plan => plan.ReadinessStatus.Equals("waiting_for_market_data", StringComparison.OrdinalIgnoreCase));
        var blockedPlans = forwardPlanning.Plans
            .Count(plan => plan.ReadinessStatus.Equals("blocked", StringComparison.OrdinalIgnoreCase));
        var selectedPlan = readyPlans.FirstOrDefault();
        var windowOpen = timeControl.InWorkWindow || timeControl.LearningWindow.ActiveNow || timeControl.NightlyWindow.ActiveNow;
        var marketAvailable = currentMarket.SnapshotStatus.Equals("available", StringComparison.OrdinalIgnoreCase)
            && currentMarket.AssetsAvailable.Any(asset => selectedPlan is not null && asset.Equals(selectedPlan.Asset, StringComparison.OrdinalIgnoreCase));
        var safetyOk = selectedPlan is not null
            && selectedPlan.SafetyFlags.Contains("observation_only=true", StringComparer.OrdinalIgnoreCase)
            && selectedPlan.SafetyFlags.Contains("no_auto_trading=true", StringComparer.OrdinalIgnoreCase)
            && selectedPlan.SafetyFlags.Contains("no_orders=true", StringComparer.OrdinalIgnoreCase);

        var gateStatus = "waiting_for_allowed_window";
        AutonomousForwardObservationGateObservation? observation = null;

        if (selectedPlan is null)
        {
            warnings.Add("no_ready_to_observe_forward_plan_found");
            gateStatus = "waiting_for_allowed_window";
        }
        else if (!windowOpen)
        {
            gateStatus = "waiting_for_allowed_window";
        }
        else if (!marketAvailable)
        {
            gateStatus = "waiting_for_market_data";
        }
        else if (!safetyOk)
        {
            warnings.Add("safety_flags_invalid");
            gateStatus = "blocked";
        }
        else
        {
            observation = BuildObservation(selectedPlan, currentMarket, forwardSignals, warnings);
            gateStatus = observation.Result == "signal_seen" || observation.Result == "completed"
                ? "completed"
                : observation.Result;
            AppendHistory(observation);
        }

        var report = new AutonomousForwardObservationGateReport(
            ReportVersion: "autonomous_forward_observation_gate_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            GateStatus: gateStatus,
            WindowStatus: BuildWindowStatus(timeControl),
            PlansSeen: forwardPlanning.Plans.Count,
            PlansReadyToObserve: readyPlans.Count,
            PlansWaiting: waitingPlans,
            PlansBlocked: blockedPlans,
            SelectedPlan: selectedPlan,
            Observation: observation,
            SourceReports: BuildSourceReports(),
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            OperatorSummary: BuildOperatorSummary(observation, selectedPlan, gateStatus),
            NextSafeStep: BuildNextSafeStep(gateStatus),
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true, observation_only=true, no_orders=true, no_demo_orders=true",
            FrankRequired: false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report);
        return report;
    }

    private AutonomousForwardObservationGateObservation BuildObservation(
        AutonomousForwardValidationPlan plan,
        CurrentMarketStatusSnapshot market,
        IReadOnlyList<DemoSignalFeedItem> signals,
        List<string> warnings)
    {
        var matchingSignal = signals.FirstOrDefault(signal =>
            signal.Asset.Equals(plan.Asset, StringComparison.OrdinalIgnoreCase)
            && signal.Timeframe.Equals(plan.Timeframe, StringComparison.OrdinalIgnoreCase));

        var result = matchingSignal is null ? "no_signal" : "signal_seen";
        var note = matchingSignal is null
            ? "read_only_market_snapshot_available; no matching demo signal"
            : "read_only_market_snapshot_available; matching demo signal observed";
        if (matchingSignal is null)
        {
            warnings.Add("no_matching_demo_signal");
        }

        return new AutonomousForwardObservationGateObservation(
            ObservationId: $"forward_observation_{NormalizeId(plan.ForwardValidationJobId)}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            ForwardValidationJobId: plan.ForwardValidationJobId,
            SourceOosJobId: plan.SourceOosJobId,
            HypothesisId: plan.HypothesisId,
            Asset: plan.Asset,
            Timeframe: plan.Timeframe,
            StrategyPattern: plan.StrategyPattern,
            MarketSnapshotStatus: market.SnapshotStatus,
            SignalStatus: matchingSignal?.SignalId ?? "-",
            Result: result,
            Note: note,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            NoOrders: true,
            NoAutoTrading: true);
    }

    private IReadOnlyList<DemoSignalFeedItem> LoadDemoSignals()
    {
        var path = Path.Combine(_storagePaths.Root, "reports", "demo_signals", "latest_demo_signals.json");
        if (!File.Exists(path))
        {
            var fallback = Path.Combine(_storagePaths.Root, "reports", "signal_watch", "latest_demo_signals.json");
            path = File.Exists(fallback) ? fallback : path;
        }

        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<DemoSignalFeedItem>>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    private void AppendHistory(AutonomousForwardObservationGateObservation observation)
    {
        var line = JsonSerializer.Serialize(observation, JsonDefaults.WriteOptions);
        File.AppendAllText(HistoryPath, line + Environment.NewLine);
    }

    private static IReadOnlyList<string> BuildSourceReports() =>
    [
        "/mnt/d/HermesData/reports/autonomous_forward_validation_planning/autonomous_forward_validation_planning.json",
        "/mnt/d/HermesData/reports/current_market_snapshot/current_market_status.json",
        "/mnt/d/HermesData/reports/demo_signals/latest_demo_signals.json",
        "/mnt/d/HermesData/reports/signal_agent_specs/signal_agent_specs.json",
        "/mnt/d/HermesData/config/schedules.json"
    ];

    private static string BuildWindowStatus(ScheduleTimeControlStatus status)
        => $"work_window={status.InWorkWindow.ToString().ToLowerInvariant()}, learning_window={status.LearningWindow.ActiveNow.ToString().ToLowerInvariant()}, nightly_window={status.NightlyWindow.ActiveNow.ToString().ToLowerInvariant()}";

    private static string BuildOperatorSummary(AutonomousForwardObservationGateObservation? observation, AutonomousForwardValidationPlan? plan, string gateStatus)
    {
        if (plan is null)
        {
            return "Hermes hat keinen Forward-Plan zur Beobachtung gefunden. Frank muss nichts tun.";
        }

        if (observation is null)
        {
            return $"Hermes wartet auf Beobachtungsfenster für {plan.ForwardValidationJobId}. Frank muss nichts tun.";
        }

        return $"Hermes hat eine Forward-Beobachtung geschrieben. Ergebnis={observation.Result}. Frank muss nichts tun.";
    }

    private static string BuildNextSafeStep(string gateStatus)
        => gateStatus switch
        {
            "signal_seen" => "Forward-Beobachtung fortsetzen im erlaubten Zeitfenster",
            "no_signal" => "Weiter beobachten; kein Signal gesehen",
            "completed" => "Forward-Status auswerten",
            "waiting_for_market_data" => "Warten auf read-only Market Snapshot",
            "blocked" => "Safety-Gates korrigieren",
            _ => "Warten auf erlaubtes Zeitfenster"
        };

    private void WriteArtifacts(AutonomousForwardObservationGateReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(AutonomousForwardObservationGateReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Autonomous Forward Observation Gate");
        sb.AppendLine();
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine($"- Gate: {report.GateStatus}");
        sb.AppendLine($"- Next step: {report.NextSafeStep}");
        if (report.Observation is not null)
        {
            sb.AppendLine($"- Observation: {report.Observation.Result}");
        }
        return sb.ToString();
    }

    private static string NormalizeId(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_').ToArray();
        return new string(chars).Trim('_');
    }
}
