using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record AttributionHypothesisFeedbackRecord(
    string HypothesisId,
    string Source,
    string Asset,
    string Timeframe,
    string StrategyPattern,
    string CausalFactor,
    string Finding,
    string EvidenceSummary,
    string BaselineMetrics,
    string MutationMetrics,
    string Confidence,
    string Status,
    string NextStep,
    bool FrankRequired,
    DateTimeOffset CreatedAtUtc);

public sealed record AttributionHypothesisFeedbackReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string MutationAttributionPath,
    string MutationExecutionPath,
    string StrategyResearchHypothesesPath,
    string CognitiveHypothesesPath,
    int HypothesesAdded,
    bool HypothesisAppended,
    bool FrankRequired,
    string OperatorSummary,
    string NextPlannedStep,
    IReadOnlyList<string> Warnings,
    AttributionHypothesisFeedbackRecord Hypothesis,
    string ReportPath,
    string MarkdownPath);

public sealed class AttributionHypothesisFeedbackService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public AttributionHypothesisFeedbackService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "attribution_hypothesis_feedback");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "attribution_hypothesis_feedback.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "attribution_hypothesis_feedback.md");
    public string CognitiveHypothesesPath => Path.Combine(_storagePaths.Root, "cognitive_core", "insights", "hypotheses.json");

    public AttributionHypothesisFeedbackReport Run()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Path.GetDirectoryName(CognitiveHypothesesPath)!);

        var mutationAttribution = LoadJson<MutationAttributionAnalysisReport>(Path.Combine(_storagePaths.Root, "reports", "mutation_attribution_analysis", "mutation_attribution_analysis.json"));
        var mutationExecution = new MutationValidationExecutorService(_storagePaths, Directory.GetCurrentDirectory()).Load()
            ?? new MutationValidationExecutorService(_storagePaths, Directory.GetCurrentDirectory()).Run();
        var latestSuccess = StrategyBacktestResultArchiveService.LoadLatestSuccess(_storagePaths);
        var failureLearning = new StrategyBacktestFailureLearningService(_storagePaths).Load();
        var mutationQueue = new MutationCandidateQueueService(_storagePaths).Load() ?? new MutationCandidateQueueService(_storagePaths).Run();
        var parameterPlanner = new StrategyParameterResearchPlannerService(_storagePaths, Directory.GetCurrentDirectory()).Load();

        var sourceHypothesis = mutationAttribution?.Items.FirstOrDefault();
        var hypotheses = LoadCognitiveHypotheses();
        var hypothesisId = BuildHypothesisId(mutationAttribution, mutationExecution);
        var existing = hypotheses.FirstOrDefault(item => item.HypothesisId.Equals(hypothesisId, StringComparison.OrdinalIgnoreCase));
        var hypothesis = new CognitiveHypothesis(
            HypothesisId: hypothesisId,
            Domain: "trading",
            Title: "Mean Reversion Rejection auf XAUUSD M5 funktioniert besser ohne schwache Sessions",
            Description: "Die Attributionsanalyse zeigt, dass die Session-Filter-Mutation die Performance des XAUUSD-M5-Mean-Reversion-Rejection-Falls verbessert hat.",
            SourceItemIds: BuildSourceIds(mutationAttribution, mutationExecution, latestSuccess, failureLearning, mutationQueue, parameterPlanner),
            ProposedValidation: "OOS validation required for session-filter hypothesis; keep research-only safety gates active.",
            Status: "research_hypothesis",
            Trust: new TrustScore(0.42, "preliminary", ["mutation_attribution_analysis", "session_filter_sharpen", "improved_backtest"]),
            Evidence: new EvidenceScore(0.48, "preliminary", [mutationAttribution?.ReportPath ?? mutationExecution?.ReportPath ?? "mutation_attribution_analysis"]),
            HumanReviewRequired: false);

        var appended = false;
        if (existing is null)
        {
            var updated = hypotheses
                .Concat([hypothesis])
                .OrderBy(item => item.Domain, StringComparer.Ordinal)
                .ThenBy(item => item.HypothesisId, StringComparer.Ordinal)
                .ToList();
            File.WriteAllText(CognitiveHypothesesPath, JsonSerializer.Serialize(updated, JsonDefaults.WriteOptions));
            appended = true;
        }

        var record = new AttributionHypothesisFeedbackRecord(
            HypothesisId: hypothesis.HypothesisId,
            Source: "mutation_attribution_analysis",
            Asset: mutationAttribution?.BaselineAsset ?? mutationExecution?.Execution?.Asset ?? "XAUUSD",
            Timeframe: mutationAttribution?.BaselineTimeframe ?? mutationExecution?.Execution?.Timeframe ?? "M5",
            StrategyPattern: mutationAttribution?.BaselineStrategyPattern ?? mutationExecution?.Execution?.StrategyPattern ?? "Mean Reversion Rejection",
            CausalFactor: "session_filter",
            Finding: "session filter likely improved result",
            EvidenceSummary: BuildEvidenceSummary(mutationAttribution, mutationExecution, latestSuccess, failureLearning),
            BaselineMetrics: BuildMetricSummary(
                mutationAttribution?.BaselineProfitFactor ?? latestSuccess?.Execution.ProfitFactor ?? 0,
                mutationAttribution?.BaselineExpectancy ?? latestSuccess?.Execution.Expectancy ?? 0,
                mutationAttribution?.BaselineWinRate ?? latestSuccess?.Execution.WinRate ?? 0,
                mutationAttribution?.BaselineMaxDrawdown ?? latestSuccess?.Execution.MaxDrawdown ?? 0),
            MutationMetrics: BuildMetricSummary(
                mutationAttribution?.MutationProfitFactor ?? mutationExecution?.Execution?.ProfitFactor ?? 0,
                mutationAttribution?.MutationExpectancy ?? mutationExecution?.Execution?.Expectancy ?? 0,
                mutationAttribution?.MutationWinRate ?? mutationExecution?.Execution?.WinRate ?? 0,
                mutationAttribution?.MutationMaxDrawdown ?? mutationExecution?.Execution?.MaxDrawdown ?? 0),
            Confidence: "preliminary",
            Status: "research_hypothesis",
            NextStep: "oos_validation_required",
            FrankRequired: false,
            CreatedAtUtc: DateTimeOffset.UtcNow);

        var report = new AttributionHypothesisFeedbackReport(
            ReportVersion: "attribution_hypothesis_feedback_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            MutationAttributionPath: Path.Combine(_storagePaths.Root, "reports", "mutation_attribution_analysis", "mutation_attribution_analysis.json"),
            MutationExecutionPath: mutationExecution?.ReportPath ?? mutationExecution?.LatestSuccessPath ?? "-",
            StrategyResearchHypothesesPath: Path.Combine(_storagePaths.Root, "cognitive_core", "insights", "hypotheses.json"),
            CognitiveHypothesesPath: CognitiveHypothesesPath,
            HypothesesAdded: appended ? 1 : 0,
            HypothesisAppended: appended,
            FrankRequired: false,
            OperatorSummary: "Hermes hat aus der verbesserten Mutation eine neue Research-Hypothese gebildet. Die Hypothese ist noch nicht bestätigt. Nächster Schritt ist OOS-Validierung. Frank muss aktuell nichts tun.",
            NextPlannedStep: "oos_validation_required",
            Warnings: existing is null ? [] : ["hypothesis_already_present"],
            Hypothesis: record,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report);
        return report;
    }

    public AttributionHypothesisFeedbackReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AttributionHypothesisFeedbackReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static string BuildHypothesisId(MutationAttributionAnalysisReport? attribution, MutationValidationExecutorReport execution)
        => attribution?.Items.FirstOrDefault()?.MutationId is { Length: > 0 } id
            ? $"hypothesis_{id}"
            : $"hypothesis_{execution.SelectedJob?.MutationId ?? execution.Execution?.MutationType ?? "session_filter"}";

    private static IReadOnlyList<string> BuildSourceIds(
        MutationAttributionAnalysisReport? attribution,
        MutationValidationExecutorReport execution,
        StrategyBacktestExecutorResultArtifact? latestSuccess,
        StrategyBacktestFailureLearningReport? failureLearning,
        MutationCandidateQueueReport queue,
        StrategyParameterResearchPlannerReport? parameterPlanner)
    {
        var ids = new List<string>
        {
            attribution?.ReportPath ?? "-",
            execution.ReportPath,
            latestSuccess?.Job.BacktestJobId ?? "-",
            failureLearning?.ReportPath ?? "-",
            queue.ReportPath,
        };

        if (parameterPlanner is not null)
        {
            ids.Add(parameterPlanner.ReportPath);
        }

        return ids.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string BuildEvidenceSummary(
        MutationAttributionAnalysisReport? attribution,
        MutationValidationExecutorReport? execution,
        StrategyBacktestExecutorResultArtifact? latestSuccess,
        StrategyBacktestFailureLearningReport? failureLearning)
    {
        var attributionCause = attribution?.Cause ?? "unknown";
        var baseline = latestSuccess?.Execution.ProfitFactor ?? execution.Comparison?.BaselineProfitFactor ?? 0;
        var mutation = execution.Comparison?.MutationProfitFactor ?? execution.Execution?.ProfitFactor ?? 0;
        var delta = mutation - baseline;
        return $"Attribution={attributionCause}; baseline_pf={baseline:0.####}; mutation_pf={mutation:0.####}; delta_pf={delta:0.####}; learning_decision={(failureLearning?.LearningDecision ?? "unknown")}";
    }

    private static string BuildMetricSummary(double pf, double expectancy, double winRate, double drawdown)
        => $"pf={pf:0.####}; expectancy={expectancy:0.####}; win_rate={winRate:0.####}; max_drawdown={drawdown:0.####}";

    private IReadOnlyList<CognitiveHypothesis> LoadCognitiveHypotheses()
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

    private void WriteArtifacts(AttributionHypothesisFeedbackReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(AttributionHypothesisFeedbackReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Attribution Hypothesis Feedback");
        sb.AppendLine();
        sb.AppendLine($"- Hypothesis ID: {report.Hypothesis.HypothesisId}");
        sb.AppendLine($"- Source: {report.Hypothesis.Source}");
        sb.AppendLine($"- Status: {report.Hypothesis.Status}");
        sb.AppendLine($"- Next step: {report.Hypothesis.NextStep}");
        sb.AppendLine();
        sb.AppendLine(report.OperatorSummary);
        return sb.ToString();
    }
}
