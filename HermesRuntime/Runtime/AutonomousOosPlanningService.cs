using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record AutonomousOosPlan(
    string OosJobId,
    string HypothesisId,
    string Asset,
    string Timeframe,
    string StrategyPattern,
    string CausalFactor,
    string RequiredDataset,
    string OosPeriod,
    string InSampleReference,
    string MutationReference,
    string ReadinessStatus,
    IReadOnlyList<string> Blockers,
    int MaxRuns,
    IReadOnlyList<string> SafetyFlags,
    string Status);

public sealed record AutonomousOosPlanningReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int HypothesesRead,
    int PlansGenerated,
    int ReadyToExecuteCount,
    int WaitingForDataCount,
    int WaitingForSpecificationCount,
    int BlockedCount,
    IReadOnlyList<AutonomousOosPlan> Plans,
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

public sealed class AutonomousOosPlanningService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public AutonomousOosPlanningService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "autonomous_oos_planning");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "autonomous_oos_planning.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "autonomous_oos_planning.md");
    public string CognitiveHypothesesPath => Path.Combine(_storagePaths.Root, "cognitive_core", "insights", "hypotheses.json");

    public AutonomousOosPlanningReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AutonomousOosPlanningReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public AutonomousOosPlanningReport Run()
    {
        Directory.CreateDirectory(Root);
        var hypotheses = LoadHypotheses();
        var attributionFeedback = LoadJson<AttributionHypothesisFeedbackReport>(Path.Combine(_storagePaths.Root, "reports", "attribution_hypothesis_feedback", "attribution_hypothesis_feedback.json"));
        var mutationExecution = LoadJson<MutationValidationExecutorReport>(Path.Combine(_storagePaths.Root, "reports", "mutation_validation_execution", "mutation_validation_execution.json"));
        var mutationAttribution = LoadJson<MutationAttributionAnalysisReport>(Path.Combine(_storagePaths.Root, "reports", "mutation_attribution_analysis", "mutation_attribution_analysis.json"));
        var latestSuccess = StrategyBacktestResultArchiveService.LoadLatestSuccess(_storagePaths);
        var executionGate = LoadJson<AutonomousOosExecutionGateReport>(Path.Combine(_storagePaths.Root, "reports", "autonomous_oos_execution_gate", "autonomous_oos_execution_gate.json"));

        var plans = BuildPlans(hypotheses, attributionFeedback, mutationExecution, mutationAttribution, latestSuccess, executionGate);
        var report = new AutonomousOosPlanningReport(
            ReportVersion: "autonomous_oos_planning_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            HypothesesRead: hypotheses.Count,
            PlansGenerated: plans.Count,
            ReadyToExecuteCount: plans.Count(plan => plan.ReadinessStatus.Equals("ready_to_execute", StringComparison.OrdinalIgnoreCase)),
            WaitingForDataCount: plans.Count(plan => plan.ReadinessStatus.Equals("waiting_for_data", StringComparison.OrdinalIgnoreCase)),
            WaitingForSpecificationCount: plans.Count(plan => plan.ReadinessStatus.Equals("waiting_for_specification", StringComparison.OrdinalIgnoreCase)),
            BlockedCount: plans.Count(plan => plan.ReadinessStatus.Equals("blocked", StringComparison.OrdinalIgnoreCase)),
            Plans: plans,
            SourceReports: BuildSourceReports(attributionFeedback, mutationExecution, mutationAttribution, latestSuccess),
            Warnings: plans.Count == 0 ? ["no_oos_candidate_hypotheses_found"] : [],
            OperatorSummary: BuildOperatorSummary(plans),
            NextSafeStep: BuildNextSafeStep(plans),
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true",
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

    private IReadOnlyList<AutonomousOosPlan> BuildPlans(
        IReadOnlyList<CognitiveHypothesis> hypotheses,
        AttributionHypothesisFeedbackReport? attributionFeedback,
        MutationValidationExecutorReport? mutationExecution,
        MutationAttributionAnalysisReport? mutationAttribution,
        StrategyBacktestExecutorResultArtifact? latestSuccess,
        AutonomousOosExecutionGateReport? executionGate)
    {
        var candidates = hypotheses.ToList();
        if (attributionFeedback is not null)
        {
            candidates.Add(new CognitiveHypothesis(
                HypothesisId: attributionFeedback.Hypothesis.HypothesisId,
                Domain: "trading",
                Title: attributionFeedback.Hypothesis.StrategyPattern,
                Description: attributionFeedback.Hypothesis.Finding,
                SourceItemIds: [
                    attributionFeedback.MutationAttributionPath,
                    attributionFeedback.MutationExecutionPath,
                    attributionFeedback.StrategyResearchHypothesesPath,
                    attributionFeedback.CognitiveHypothesesPath
                ],
                ProposedValidation: attributionFeedback.NextPlannedStep,
                Status: attributionFeedback.Hypothesis.Status,
                Trust: new TrustScore(0.42, "preliminary", ["attribution_hypothesis_feedback"]),
                Evidence: new EvidenceScore(0.48, "preliminary", [attributionFeedback.Hypothesis.HypothesisId]),
                HumanReviewRequired: attributionFeedback.Hypothesis.FrankRequired));
        }

        var plans = candidates
            .Where(item => item.Status.Equals("research_hypothesis", StringComparison.OrdinalIgnoreCase))
            .Where(item => item.Trust.Classification.Equals("preliminary", StringComparison.OrdinalIgnoreCase) || item.Trust.Value < 0.7)
            .Where(item => HasOosNextStep(item))
            .OrderBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.HypothesisId, StringComparer.OrdinalIgnoreCase)
            .Select(item =>
            {
                var blockers = new List<string>();
                var dataset = ResolveRequiredDataset(item, attributionFeedback, mutationExecution, latestSuccess, blockers);
                var mutationReference = ResolveMutationReference(item, attributionFeedback, mutationExecution, mutationAttribution, blockers);
                var readinessStatus = DetermineReadinessStatus(dataset, mutationReference, latestSuccess, blockers);

                return new AutonomousOosPlan(
                    OosJobId: $"oos_planning_{NormalizeId(item.HypothesisId)}",
                    HypothesisId: item.HypothesisId,
                    Asset: ResolveAsset(item, attributionFeedback, mutationExecution),
                    Timeframe: ResolveTimeframe(item, attributionFeedback, mutationExecution),
                    StrategyPattern: ResolveStrategyPattern(item, attributionFeedback, mutationExecution),
                    CausalFactor: ResolveCausalFactor(item, attributionFeedback),
                    RequiredDataset: dataset,
                    OosPeriod: ResolveOosPeriod(item, mutationExecution, latestSuccess),
                    InSampleReference: ResolveInSampleReference(mutationExecution, latestSuccess),
                    MutationReference: mutationReference,
                    ReadinessStatus: readinessStatus,
                    Blockers: blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    MaxRuns: latestSuccess is not null ? 25 : 0,
                    SafetyFlags: BuildSafetyFlags(),
                    Status: "prepared");
            })
            .ToList();

        return ApplyExecutionStatus(plans, executionGate);
    }

    private static IReadOnlyList<AutonomousOosPlan> ApplyExecutionStatus(
        IReadOnlyList<AutonomousOosPlan> plans,
        AutonomousOosExecutionGateReport? executionGate)
    {
        if (executionGate?.Result is null)
        {
            return plans;
        }

        var completedStatus = executionGate.Result.Outcome switch
        {
            "improved" => "completed_improved",
            "worse" => "completed_worse",
            _ => "completed_inconclusive",
        };

        return plans
            .Select(plan => plan.OosJobId.Equals(executionGate.Result.OosJobId, StringComparison.OrdinalIgnoreCase)
                ? new AutonomousOosPlan(
                    OosJobId: plan.OosJobId,
                    HypothesisId: plan.HypothesisId,
                    Asset: plan.Asset,
                    Timeframe: plan.Timeframe,
                    StrategyPattern: plan.StrategyPattern,
                    CausalFactor: plan.CausalFactor,
                    RequiredDataset: plan.RequiredDataset,
                    OosPeriod: plan.OosPeriod,
                    InSampleReference: plan.InSampleReference,
                    MutationReference: plan.MutationReference,
                    ReadinessStatus: completedStatus,
                    Blockers: plan.Blockers,
                    MaxRuns: plan.MaxRuns,
                    SafetyFlags: plan.SafetyFlags,
                    Status: completedStatus)
                : plan)
            .ToList();
    }

    private static string BuildNextSafeStep(IReadOnlyList<AutonomousOosPlan> plans)
    {
        var completed = plans.FirstOrDefault(plan => plan.Status.StartsWith("completed_", StringComparison.OrdinalIgnoreCase));
        if (completed is not null)
        {
            return completed.Status switch
            {
                "completed_improved" => "Forward Validation planen.",
                "completed_worse" => "Hypothese zurückstufen.",
                _ => "Weitere OOS-Daten nötig.",
            };
        }

        return plans.Any(plan => plan.ReadinessStatus.Equals("ready_to_execute", StringComparison.OrdinalIgnoreCase))
            ? "OOS-Plan ist vorbereitet; keine Ausfuehrung."
            : "Daten oder Spezifikation vervollstaendigen; keine Ausfuehrung.";
    }

    private static bool HasOosNextStep(CognitiveHypothesis hypothesis) =>
        hypothesis.ProposedValidation.Contains("oos", StringComparison.OrdinalIgnoreCase)
        || hypothesis.Description.Contains("oos", StringComparison.OrdinalIgnoreCase)
        || hypothesis.Status.Equals("research_hypothesis", StringComparison.OrdinalIgnoreCase);

    private static string ResolveAsset(CognitiveHypothesis hypothesis, AttributionHypothesisFeedbackReport? attributionFeedback, MutationValidationExecutorReport? mutationExecution)
        => attributionFeedback?.Hypothesis.Asset
            ?? mutationExecution?.Execution?.Asset
            ?? "unknown";

    private static string ResolveTimeframe(CognitiveHypothesis hypothesis, AttributionHypothesisFeedbackReport? attributionFeedback, MutationValidationExecutorReport? mutationExecution)
        => attributionFeedback?.Hypothesis.Timeframe
            ?? mutationExecution?.Execution?.Timeframe
            ?? "unknown";

    private static string ResolveStrategyPattern(CognitiveHypothesis hypothesis, AttributionHypothesisFeedbackReport? attributionFeedback, MutationValidationExecutorReport? mutationExecution)
        => attributionFeedback?.Hypothesis.StrategyPattern
            ?? mutationExecution?.Execution?.StrategyPattern
            ?? hypothesis.Title;

    private static string ResolveCausalFactor(CognitiveHypothesis hypothesis, AttributionHypothesisFeedbackReport? attributionFeedback)
        => attributionFeedback?.Hypothesis.CausalFactor
            ?? "unknown";

    private static string ResolveRequiredDataset(
        CognitiveHypothesis hypothesis,
        AttributionHypothesisFeedbackReport? attributionFeedback,
        MutationValidationExecutorReport? mutationExecution,
        StrategyBacktestExecutorResultArtifact? latestSuccess,
        List<string> blockers)
    {
        var asset = attributionFeedback?.Hypothesis.Asset ?? mutationExecution?.Execution?.Asset ?? latestSuccess?.Job.Asset;
        var timeframe = attributionFeedback?.Hypothesis.Timeframe ?? mutationExecution?.Execution?.Timeframe ?? latestSuccess?.Job.Timeframe;
        if (!string.IsNullOrWhiteSpace(asset) && !string.IsNullOrWhiteSpace(timeframe))
        {
            return $"historical_data:{asset}:{timeframe}";
        }

        blockers.Add("waiting_for_data");
        return "-";
    }

    private static string ResolveOosPeriod(
        CognitiveHypothesis hypothesis,
        MutationValidationExecutorReport? mutationExecution,
        StrategyBacktestExecutorResultArtifact? latestSuccess)
        => mutationExecution?.Execution is not null
            ? $"{mutationExecution.Execution.Asset}:{mutationExecution.Execution.Timeframe}:oos"
            : latestSuccess is not null
                ? $"{latestSuccess.Job.Asset}:{latestSuccess.Job.Timeframe}:oos"
                : "-";

    private static string ResolveInSampleReference(
        MutationValidationExecutorReport? mutationExecution,
        StrategyBacktestExecutorResultArtifact? latestSuccess)
        => mutationExecution?.Comparison?.BaselineBacktestJobId
            ?? latestSuccess?.Job.BacktestJobId
            ?? "-";

    private static string ResolveMutationReference(
        CognitiveHypothesis hypothesis,
        AttributionHypothesisFeedbackReport? attributionFeedback,
        MutationValidationExecutorReport? mutationExecution,
        MutationAttributionAnalysisReport? mutationAttribution,
        List<string> blockers)
    {
        if (!string.IsNullOrWhiteSpace(attributionFeedback?.Hypothesis.HypothesisId))
        {
            return attributionFeedback.Hypothesis.HypothesisId;
        }

        var mutationId = mutationAttribution?.Items.FirstOrDefault()?.MutationId ?? mutationExecution?.Execution?.MutationType;
        if (!string.IsNullOrWhiteSpace(mutationId))
        {
            return mutationId;
        }

        blockers.Add("waiting_for_specification");
        return "-";
    }

    private static string DetermineReadinessStatus(string requiredDataset, string mutationReference, StrategyBacktestExecutorResultArtifact? latestSuccess, List<string> blockers)
    {
        if (string.IsNullOrWhiteSpace(requiredDataset) || requiredDataset == "-")
        {
            blockers.Add("waiting_for_data");
        }

        if (string.IsNullOrWhiteSpace(mutationReference) || mutationReference == "-")
        {
            blockers.Add("waiting_for_specification");
        }

        if (latestSuccess is null)
        {
            blockers.Add("waiting_for_data");
        }

        if (blockers.Count == 0)
        {
            return "ready_to_execute";
        }

        if (blockers.Any(item => item.Equals("waiting_for_data", StringComparison.OrdinalIgnoreCase)))
        {
            return "waiting_for_data";
        }

        if (blockers.Any(item => item.Equals("waiting_for_specification", StringComparison.OrdinalIgnoreCase)))
        {
            return "waiting_for_specification";
        }

        return "blocked";
    }

    private static IReadOnlyList<string> BuildSafetyFlags() =>
    [
        "no_auto_trading=true",
        "human_review_required=true",
        "broker_orders_enabled=false",
        "live_trading_enabled=false",
        "research_only=true",
        "no_oos_execution=true"
    ];

    private static IReadOnlyList<string> BuildSourceReports(
        AttributionHypothesisFeedbackReport? attributionFeedback,
        MutationValidationExecutorReport? mutationExecution,
        MutationAttributionAnalysisReport? mutationAttribution,
        StrategyBacktestExecutorResultArtifact? latestSuccess)
    {
        var sources = new List<string>
        {
            "/mnt/d/HermesData/cognitive_core/insights/hypotheses.json",
            attributionFeedback?.ReportPath ?? "/mnt/d/HermesData/reports/attribution_hypothesis_feedback/attribution_hypothesis_feedback.json",
            mutationExecution?.ReportPath ?? "/mnt/d/HermesData/reports/mutation_validation_execution/mutation_validation_execution.json",
            mutationAttribution?.ReportPath ?? "/mnt/d/HermesData/reports/mutation_attribution_analysis/mutation_attribution_analysis.json",
        };

        if (latestSuccess is not null)
        {
            sources.Add("/mnt/d/HermesData/reports/strategy_backtest_execution/strategy_backtest_latest_success.json");
        }

        return sources.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string BuildOperatorSummary(IReadOnlyList<AutonomousOosPlan> plans)
    {
        if (plans.Count == 0)
        {
            return "Hermes hat OOS-Validierung vorbereitet. Keine Hypothesen mit passender OOS-Bedingung gefunden. Frank muss nichts tun.";
        }

        var ready = plans.Count(plan => plan.ReadinessStatus.Equals("ready_to_execute", StringComparison.OrdinalIgnoreCase));
        return $"Hermes hat OOS-Validierung vorbereitet. {plans.Count} OOS-Plan(e) abgeleitet, davon {ready} ready_to_execute. Frank muss nichts tun.";
    }

    private IReadOnlyList<CognitiveHypothesis> LoadHypotheses()
    {
        if (!File.Exists(CognitiveHypothesesPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<CognitiveHypothesis>>(File.ReadAllText(CognitiveHypothesesPath), JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
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

    private void WriteArtifacts(AutonomousOosPlanningReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(AutonomousOosPlanningReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Autonomous OOS Planning");
        sb.AppendLine();
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("| OOS Job | Hypothesis | Readiness | Status |");
        sb.AppendLine("| --- | --- | --- | --- |");
        foreach (var plan in report.Plans)
        {
            sb.AppendLine($"| {plan.OosJobId} | {plan.HypothesisId} | {plan.ReadinessStatus} | {plan.Status} |");
        }

        return sb.ToString();
    }

    private static string NormalizeId(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_').ToArray();
        return new string(chars).Trim('_');
    }
}
