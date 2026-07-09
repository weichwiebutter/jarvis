using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record HermesLearningStatusReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LearningMaturityPercent,
    string LearningConfidence,
    decimal EvolutionScore,
    string EvolutionTrend,
    int PatternCount,
    int HypothesisCount,
    int ReadyForValidationCount,
    int CollectingDataCount,
    string ForwardStatus,
    string LastForwardResult,
    string LastTradeOutcome,
    string BrokerAction,
    string SafetyStatus,
    string HighestPriorityImprovement,
    string CurrentBlocker,
    string RecommendedNextAction,
    int ApprovedAnnotations,
    int PendingReviews,
    IReadOnlyList<string> Warnings,
    string BotEvolutionScoreReportPath,
    string BotEvolutionHistoryReportPath,
    string BotEvolutionRecommendationReportPath,
    string TradingPatternLearningReportPath,
    string TradingHypothesesReportPath,
    string TradingHypothesisReadinessReportPath,
    string PaperForwardEvaluationReportPath,
    string ApprovedChartAnnotationsReportPath,
    string ChartAnnotationReviewQueueReportPath,
    string ReportPath,
    string MarkdownPath);

public sealed class HermesLearningStatusService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public HermesLearningStatusService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "hermes_learning_status");
    public string ReportPath => Path.Combine(Root, "hermes_learning_status.json");
    public string MarkdownPath => Path.Combine(Root, "hermes_learning_status.md");

    public HermesLearningStatusReport Run()
    {
        Directory.CreateDirectory(Root);

        var scoreService = new BotEvolutionScoreService(_storagePaths, _runtimeRoot);
        var historyService = new BotEvolutionHistoryService(_storagePaths, _runtimeRoot);
        var recommendationService = new BotEvolutionRecommendationService(_storagePaths, _runtimeRoot);
        var patternService = new TradingPatternLearningService(_storagePaths, _runtimeRoot);
        var hypothesisService = new TradingHypothesisService(_storagePaths, _runtimeRoot);
        var readinessService = new TradingHypothesisReadinessService(_storagePaths, _runtimeRoot);
        var forwardService = new PaperForwardEvaluationService(_storagePaths, _runtimeRoot);
        var approvedService = new ApprovedChartAnnotationRegistryService(_storagePaths, _runtimeRoot);
        var reviewQueueService = new ChartAnnotationReviewQueueService(_storagePaths, _runtimeRoot);

        var scoreReport = scoreService.LoadLatestReport() ?? scoreService.Run();
        var historyReport = historyService.LoadLatestReport() ?? historyService.Run();
        var recommendationReport = recommendationService.LoadLatestReport() ?? recommendationService.Run();
        var patternReport = patternService.LoadLatestReport() ?? patternService.Run();
        var hypothesisReport = hypothesisService.LoadLatestReport() ?? hypothesisService.Run();
        var readinessReport = readinessService.LoadLatestReport() ?? readinessService.Run();
        var forwardReport = forwardService.LoadLatestReport() ?? forwardService.Run();
        var approvedReport = approvedService.LoadLatestReport();
        var reviewQueueReport = reviewQueueService.LoadLatestReport();

        var warnings = new List<string>();
        if (approvedReport is null)
        {
            warnings.Add("approved_chart_annotations_report_missing");
        }

        if (reviewQueueReport is null)
        {
            warnings.Add("chart_annotation_review_queue_report_missing");
        }

        var readyForValidationCount = readinessReport.Items.Count(item => item.Readiness.Equals("ready_for_validation", StringComparison.OrdinalIgnoreCase));
        var collectingDataCount = readinessReport.Items.Count(item => item.Readiness.Equals("insufficient_data", StringComparison.OrdinalIgnoreCase));
        var approvedAnnotations = approvedReport?.ApprovedCount ?? 0;
        var pendingReviews = reviewQueueReport?.PendingCount ?? 0;
        var learningMaturityPercent = ComputeLearningMaturityPercent(scoreReport, patternReport, hypothesisReport, readinessReport, forwardReport, approvedAnnotations, pendingReviews);
        var learningConfidence = DetermineLearningConfidence(scoreReport, historyReport, readinessReport, forwardReport, approvedAnnotations, pendingReviews);
        var highestPriorityImprovement = recommendationReport.TopImprovementOpportunities.FirstOrDefault() is { } opportunity
            ? $"{opportunity.Priority.ToUpperInvariant()} / {opportunity.AffectedAsset} / {opportunity.RootCause}"
            : "none";
        var currentBlocker = DetermineCurrentBlocker(recommendationReport, readinessReport);
        var recommendedNextAction = DetermineRecommendedNextAction(recommendationReport, readinessReport, learningMaturityPercent, currentBlocker);

        var report = new HermesLearningStatusReport(
            ReportVersion: "hermes_learning_status_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: "ready",
            LearningMaturityPercent: learningMaturityPercent,
            LearningConfidence: learningConfidence,
            EvolutionScore: scoreReport.EvolutionScore,
            EvolutionTrend: historyReport.Trend,
            PatternCount: patternReport.PatternCount,
            HypothesisCount: hypothesisReport.HypothesisCount,
            ReadyForValidationCount: readyForValidationCount,
            CollectingDataCount: collectingDataCount,
            ForwardStatus: forwardReport.ForwardRunStatus,
            LastForwardResult: forwardReport.LastDecision,
            LastTradeOutcome: DetermineLastTradeOutcome(forwardReport),
            BrokerAction: "none",
            SafetyStatus: forwardReport.SafetyStatus,
            HighestPriorityImprovement: highestPriorityImprovement,
            CurrentBlocker: currentBlocker,
            RecommendedNextAction: recommendedNextAction,
            ApprovedAnnotations: approvedAnnotations,
            PendingReviews: pendingReviews,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            BotEvolutionScoreReportPath: scoreService.ReportPath,
            BotEvolutionHistoryReportPath: historyService.ReportPath,
            BotEvolutionRecommendationReportPath: recommendationService.ReportPath,
            TradingPatternLearningReportPath: patternService.ReportPath,
            TradingHypothesesReportPath: hypothesisService.ReportPath,
            TradingHypothesisReadinessReportPath: readinessService.ReportPath,
            PaperForwardEvaluationReportPath: forwardService.ReportPath,
            ApprovedChartAnnotationsReportPath: approvedService.ReportPath,
            ChartAnnotationReviewQueueReportPath: reviewQueueService.ReportPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public HermesLearningStatusReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<HermesLearningStatusReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static int ComputeLearningMaturityPercent(
        BotEvolutionScoreReport scoreReport,
        TradingPatternLearningReport patternReport,
        TradingHypothesisReport hypothesisReport,
        TradingHypothesisReadinessReport readinessReport,
        PaperForwardEvaluationReport forwardReport,
        int approvedAnnotations,
        int pendingReviews)
    {
        var evolutionComponent = Clamp01(scoreReport.EvolutionScore / 100m);
        var patternComponent = Clamp01(patternReport.PatternCount / 10m);
        var hypothesisComponent = Clamp01(hypothesisReport.HypothesisCount / 12m);
        var readinessComponent = hypothesisReport.Hypotheses.Count == 0
            ? 0m
            : Clamp01(readinessReport.ReadyForValidationCount / (decimal)hypothesisReport.Hypotheses.Count);
        var approvalComponent = Clamp01(approvedAnnotations / 10m);
        var reviewPenalty = Clamp01(pendingReviews / 10m);
        var safetyComponent = forwardReport.ForwardRunStatus.Equals("green", StringComparison.OrdinalIgnoreCase)
            && forwardReport.SafetyStatus.Equals("safe", StringComparison.OrdinalIgnoreCase)
                ? 1m
                : 0.45m;

        var raw = (evolutionComponent * 0.30m)
            + (patternComponent * 0.12m)
            + (hypothesisComponent * 0.12m)
            + (readinessComponent * 0.18m)
            + (approvalComponent * 0.10m)
            + (safetyComponent * 0.18m)
            - (reviewPenalty * 0.10m);

        return (int)Math.Round(Clamp01(raw) * 100m, MidpointRounding.AwayFromZero);
    }

    private static string DetermineLearningConfidence(
        BotEvolutionScoreReport scoreReport,
        BotEvolutionHistoryReport historyReport,
        TradingHypothesisReadinessReport readinessReport,
        PaperForwardEvaluationReport forwardReport,
        int approvedAnnotations,
        int pendingReviews)
    {
        var safe = forwardReport.ForwardRunStatus.Equals("green", StringComparison.OrdinalIgnoreCase)
            && forwardReport.SafetyStatus.Equals("safe", StringComparison.OrdinalIgnoreCase);
        var mature = scoreReport.EvolutionScore >= 60m
            && readinessReport.ReadyForValidationCount > 0
            && approvedAnnotations >= pendingReviews;

        if (safe && mature && historyReport.Trend.Equals("improving", StringComparison.OrdinalIgnoreCase))
        {
            return "high";
        }

        if (safe || mature)
        {
            return "medium";
        }

        return "low";
    }

    private static string DetermineCurrentBlocker(BotEvolutionRecommendationReport recommendationReport, TradingHypothesisReadinessReport readinessReport)
    {
        var topRecommendation = recommendationReport.TopImprovementOpportunities.FirstOrDefault();
        if (topRecommendation is not null && !topRecommendation.BlockingDependency.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return $"{topRecommendation.BlockingDependency} / {topRecommendation.RootCause}";
        }

        var blockedReadiness = readinessReport.Items.FirstOrDefault(item => item.Readiness.Equals("insufficient_data", StringComparison.OrdinalIgnoreCase));
        if (blockedReadiness is not null)
        {
            return $"{blockedReadiness.HypothesisId} / {blockedReadiness.NextRequiredData}";
        }

        return "none";
    }

    private static string DetermineRecommendedNextAction(
        BotEvolutionRecommendationReport recommendationReport,
        TradingHypothesisReadinessReport readinessReport,
        int learningMaturityPercent,
        string currentBlocker)
    {
        if (learningMaturityPercent >= 80 && readinessReport.ReadyForValidationCount > 0)
        {
            return "run_validation_state_sync";
        }

        var topRecommendation = recommendationReport.TopImprovementOpportunities.FirstOrDefault();
        if (topRecommendation is not null && !topRecommendation.RequiredManualAction.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return topRecommendation.RequiredManualAction;
        }

        if (!currentBlocker.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return "collect more data for the current blocker";
        }

        return "continue read-only learning";
    }

    private static string DetermineLastTradeOutcome(PaperForwardEvaluationReport forwardReport)
    {
        if (forwardReport.ExpiredSignalCount > 0)
        {
            return "expired_signals_present";
        }

        if (forwardReport.InvalidatedSignalCount > 0)
        {
            return "invalidated_signals_present";
        }

        if (forwardReport.NetR > 0m)
        {
            return "positive_net_r";
        }

        if (forwardReport.NetR < 0m)
        {
            return "negative_net_r";
        }

        return "no_closed_trade_outcome";
    }

    private static decimal Clamp01(decimal value)
        => value < 0m ? 0m : value > 1m ? 1m : value;

    private static string BuildMarkdown(HermesLearningStatusReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Hermes Learning Status");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- learning_maturity_percent: {report.LearningMaturityPercent}");
        sb.AppendLine($"- learning_confidence: {report.LearningConfidence}");
        sb.AppendLine($"- evolution_score: {report.EvolutionScore:0.0}");
        sb.AppendLine($"- evolution_trend: {report.EvolutionTrend}");
        sb.AppendLine($"- pattern_count: {report.PatternCount}");
        sb.AppendLine($"- hypothesis_count: {report.HypothesisCount}");
        sb.AppendLine($"- ready_for_validation_count: {report.ReadyForValidationCount}");
        sb.AppendLine($"- collecting_data_count: {report.CollectingDataCount}");
        sb.AppendLine($"- forward_status: {report.ForwardStatus}");
        sb.AppendLine($"- last_forward_result: {report.LastForwardResult}");
        sb.AppendLine($"- last_trade_outcome: {report.LastTradeOutcome}");
        sb.AppendLine($"- broker_action: {report.BrokerAction}");
        sb.AppendLine($"- safety_status: {report.SafetyStatus}");
        sb.AppendLine($"- highest_priority_improvement: {report.HighestPriorityImprovement}");
        sb.AppendLine($"- current_blocker: {report.CurrentBlocker}");
        sb.AppendLine($"- recommended_next_action: {report.RecommendedNextAction}");
        sb.AppendLine($"- approved_annotations: {report.ApprovedAnnotations}");
        sb.AppendLine($"- pending_reviews: {report.PendingReviews}");
        sb.AppendLine();
        sb.AppendLine("## Sources");
        sb.AppendLine($"- bot_evolution_score_report_path: {report.BotEvolutionScoreReportPath}");
        sb.AppendLine($"- bot_evolution_history_report_path: {report.BotEvolutionHistoryReportPath}");
        sb.AppendLine($"- bot_evolution_recommendation_report_path: {report.BotEvolutionRecommendationReportPath}");
        sb.AppendLine($"- trading_pattern_learning_report_path: {report.TradingPatternLearningReportPath}");
        sb.AppendLine($"- trading_hypotheses_report_path: {report.TradingHypothesesReportPath}");
        sb.AppendLine($"- trading_hypothesis_readiness_report_path: {report.TradingHypothesisReadinessReportPath}");
        sb.AppendLine($"- paper_forward_evaluation_report_path: {report.PaperForwardEvaluationReportPath}");
        sb.AppendLine($"- approved_chart_annotations_report_path: {report.ApprovedChartAnnotationsReportPath}");
        sb.AppendLine($"- chart_annotation_review_queue_report_path: {report.ChartAnnotationReviewQueueReportPath}");

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
        }

        return sb.ToString();
    }
}
