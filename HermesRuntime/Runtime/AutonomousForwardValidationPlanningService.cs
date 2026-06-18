using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record AutonomousForwardValidationPlan(
    string ForwardValidationJobId,
    string SourceOosJobId,
    string HypothesisId,
    string Asset,
    string Timeframe,
    string StrategyPattern,
    string CausalFactor,
    string ValidationMode,
    string RequiredLiveData,
    int DurationDays,
    int MinObservations,
    int MaxObservationsPerRun,
    string ReadinessStatus,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> SafetyFlags,
    string Status);

public sealed record AutonomousForwardValidationPlanningReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int OosPlansRead,
    int CompletedImprovedOosPlans,
    int PlansGenerated,
    int ReadyToObserveCount,
    int WaitingForMarketDataCount,
    int BlockedCount,
    IReadOnlyList<AutonomousForwardValidationPlan> Plans,
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

public sealed class AutonomousForwardValidationPlanningService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public AutonomousForwardValidationPlanningService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "autonomous_forward_validation_planning");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "autonomous_forward_validation_planning.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "autonomous_forward_validation_planning.md");

    public AutonomousForwardValidationPlanningReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AutonomousForwardValidationPlanningReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public AutonomousForwardValidationPlanningReport Run()
    {
        Directory.CreateDirectory(Root);
        var oosPlanning = LoadJson<AutonomousOosPlanningReport>(Path.Combine(_storagePaths.Root, "reports", "autonomous_oos_planning", "autonomous_oos_planning.json"));
        var oosExecution = LoadJson<AutonomousOosExecutionGateReport>(Path.Combine(_storagePaths.Root, "reports", "autonomous_oos_execution_gate", "autonomous_oos_execution_gate.json"));
        var attributionFeedback = LoadJson<AttributionHypothesisFeedbackReport>(Path.Combine(_storagePaths.Root, "reports", "attribution_hypothesis_feedback", "attribution_hypothesis_feedback.json"));
        var mutationExecution = LoadJson<MutationValidationExecutorReport>(Path.Combine(_storagePaths.Root, "reports", "mutation_validation_execution", "mutation_validation_execution.json"));
        var mutationAttribution = LoadJson<MutationAttributionAnalysisReport>(Path.Combine(_storagePaths.Root, "reports", "mutation_attribution_analysis", "mutation_attribution_analysis.json"));
        var latestSuccess = StrategyBacktestResultArchiveService.LoadLatestSuccess(_storagePaths);
        var marketStatus = new CurrentMarketSnapshotService(_storagePaths, _runtimeRoot).LoadOrCreateStatus();

        var completedImproved = oosPlanning?.Plans.Count(plan => plan.Status.Equals("completed_improved", StringComparison.OrdinalIgnoreCase)) ?? 0;
        var plans = BuildPlans(oosPlanning, oosExecution, attributionFeedback, mutationExecution, mutationAttribution, latestSuccess, marketStatus);
        var report = new AutonomousForwardValidationPlanningReport(
            ReportVersion: "autonomous_forward_validation_planning_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            OosPlansRead: oosPlanning?.Plans.Count ?? 0,
            CompletedImprovedOosPlans: completedImproved,
            PlansGenerated: plans.Count,
            ReadyToObserveCount: plans.Count(plan => plan.ReadinessStatus.Equals("ready_to_observe", StringComparison.OrdinalIgnoreCase)),
            WaitingForMarketDataCount: plans.Count(plan => plan.ReadinessStatus.Equals("waiting_for_market_data", StringComparison.OrdinalIgnoreCase)),
            BlockedCount: plans.Count(plan => plan.ReadinessStatus.Equals("blocked", StringComparison.OrdinalIgnoreCase)),
            Plans: plans,
            SourceReports: BuildSourceReports(),
            Warnings: completedImproved == 0 ? ["no_completed_improved_oos_plan_found"] : [],
            OperatorSummary: BuildOperatorSummary(plans),
            NextSafeStep: BuildNextSafeStep(plans),
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true, observation_only=true",
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

    private IReadOnlyList<AutonomousForwardValidationPlan> BuildPlans(
        AutonomousOosPlanningReport? oosPlanning,
        AutonomousOosExecutionGateReport? oosExecution,
        AttributionHypothesisFeedbackReport? attributionFeedback,
        MutationValidationExecutorReport? mutationExecution,
        MutationAttributionAnalysisReport? mutationAttribution,
        StrategyBacktestExecutorResultArtifact? latestSuccess,
        CurrentMarketStatusSnapshot marketStatus)
    {
        if (oosPlanning is null)
        {
            return [];
        }

        var source = oosPlanning.Plans
            .Where(plan => plan.Status.Equals("completed_improved", StringComparison.OrdinalIgnoreCase))
            .Select(plan =>
            {
                var blockers = new List<string>();
                var marketAvailable = marketStatus.SnapshotStatus.Equals("available", StringComparison.OrdinalIgnoreCase)
                    && marketStatus.AssetsAvailable.Any(asset => asset.Equals(plan.Asset, StringComparison.OrdinalIgnoreCase));
                if (!marketAvailable)
                {
                    blockers.Add("waiting_for_market_data");
                }

                if (!HasSignalDefinition(plan.Asset, plan.Timeframe, attributionFeedback, mutationExecution, mutationAttribution, latestSuccess))
                {
                    blockers.Add("waiting_for_specification");
                }

                var readiness = blockers.Count == 0
                    ? "ready_to_observe"
                    : blockers.Contains("waiting_for_market_data", StringComparer.OrdinalIgnoreCase)
                        ? "waiting_for_market_data"
                        : blockers.Contains("waiting_for_specification", StringComparer.OrdinalIgnoreCase)
                            ? "blocked"
                            : "blocked";

                return new AutonomousForwardValidationPlan(
                    ForwardValidationJobId: $"forward_validation_{NormalizeId(plan.OosJobId)}",
                    SourceOosJobId: plan.OosJobId,
                    HypothesisId: plan.HypothesisId,
                    Asset: plan.Asset,
                    Timeframe: plan.Timeframe,
                    StrategyPattern: plan.StrategyPattern,
                    CausalFactor: plan.CausalFactor,
                    ValidationMode: "observation_only",
                    RequiredLiveData: "read_only_quotes",
                    DurationDays: 7,
                    MinObservations: 20,
                    MaxObservationsPerRun: 5,
                    ReadinessStatus: readiness,
                    Blockers: blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    SafetyFlags: BuildSafetyFlags(),
                    Status: readiness switch
                    {
                        "ready_to_observe" => "planned",
                        "waiting_for_market_data" => "waiting_for_market_data",
                        _ => "blocked"
                    });
            })
            .OrderBy(plan => plan.ReadinessStatus, StringComparer.OrdinalIgnoreCase)
            .ThenBy(plan => plan.HypothesisId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return source;
    }

    private static bool HasSignalDefinition(
        string asset,
        string timeframe,
        AttributionHypothesisFeedbackReport? attributionFeedback,
        MutationValidationExecutorReport? mutationExecution,
        MutationAttributionAnalysisReport? mutationAttribution,
        StrategyBacktestExecutorResultArtifact? latestSuccess)
    {
        var pattern = attributionFeedback?.Hypothesis.StrategyPattern
            ?? mutationExecution?.Execution?.StrategyPattern
            ?? mutationAttribution?.BaselineStrategyPattern
            ?? latestSuccess?.Job.StrategyPattern
            ?? string.Empty;

        return !string.IsNullOrWhiteSpace(pattern)
            && !string.IsNullOrWhiteSpace(asset)
            && !string.IsNullOrWhiteSpace(timeframe);
    }

    private static IReadOnlyList<string> BuildSafetyFlags() =>
    [
        "no_auto_trading=true",
        "human_review_required=true",
        "broker_orders_enabled=false",
        "live_trading_enabled=false",
        "research_only=true",
        "observation_only=true",
        "no_orders=true",
        "no_demo_orders=true",
        "no_certification=true"
    ];

    private static IReadOnlyList<string> BuildSourceReports() =>
    [
        "/mnt/d/HermesData/reports/autonomous_oos_planning/autonomous_oos_planning.json",
        "/mnt/d/HermesData/reports/autonomous_oos_execution_gate/autonomous_oos_execution_gate.json",
        "/mnt/d/HermesData/reports/attribution_hypothesis_feedback/attribution_hypothesis_feedback.json",
        "/mnt/d/HermesData/reports/mutation_validation_execution/mutation_validation_execution.json",
        "/mnt/d/HermesData/reports/mutation_attribution_analysis/mutation_attribution_analysis.json",
        "/mnt/d/HermesData/reports/strategy_backtest_execution/strategy_backtest_latest_success.json"
    ];

    private static string BuildOperatorSummary(IReadOnlyList<AutonomousForwardValidationPlan> plans)
    {
        if (plans.Count == 0)
        {
            return "Hermes hat keinen Forward-Validierungsplan erzeugt. Frank muss nichts tun.";
        }

        var ready = plans.Count(plan => plan.ReadinessStatus.Equals("ready_to_observe", StringComparison.OrdinalIgnoreCase));
        return $"Hermes hat Forward-Validierung vorbereitet. {plans.Count} Forward-Plan(e) erzeugt, davon {ready} ready_to_observe. Frank muss nichts tun.";
    }

    private static string BuildNextSafeStep(IReadOnlyList<AutonomousForwardValidationPlan> plans)
    {
        if (plans.Any(plan => plan.ReadinessStatus.Equals("ready_to_observe", StringComparison.OrdinalIgnoreCase)))
        {
            return "Forward-Beobachtung im erlaubten Zeitfenster.";
        }

        if (plans.Any(plan => plan.ReadinessStatus.Equals("waiting_for_market_data", StringComparison.OrdinalIgnoreCase)))
        {
            return "Warten auf read-only Market Snapshot.";
        }

        return "Warten auf Forward-Validierungs-Definition.";
    }

    private static T? LoadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return default;
        }
    }

    private void WriteArtifacts(AutonomousForwardValidationPlanningReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(AutonomousForwardValidationPlanningReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Autonomous Forward Validation Planning");
        sb.AppendLine();
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("| Job | OOS Job | Readiness | Status |");
        sb.AppendLine("| --- | --- | --- | --- |");
        foreach (var plan in report.Plans)
        {
            sb.AppendLine($"| {plan.ForwardValidationJobId} | {plan.SourceOosJobId} | {plan.ReadinessStatus} | {plan.Status} |");
        }

        return sb.ToString();
    }

    private static string NormalizeId(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_').ToArray();
        return new string(chars).Trim('_');
    }
}
