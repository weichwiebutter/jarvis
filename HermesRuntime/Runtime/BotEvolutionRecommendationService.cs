using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record BotEvolutionRecommendationOpportunity(
    string Priority,
    string AffectedAsset,
    string RootCause,
    decimal ExpectedScoreGain,
    string RequiredManualAction,
    string BlockingDependency);

public sealed record BotEvolutionRecommendationReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    decimal EvolutionScore,
    decimal? PreviousScore,
    decimal? ImprovementDelta,
    string Trend,
    int EvaluatedSignals,
    int InvalidatedSignals,
    int ExpiredSignals,
    int ApprovedAnnotationCount,
    int PendingReviewCount,
    IReadOnlyList<BotEvolutionRecommendationOpportunity> TopImprovementOpportunities,
    IReadOnlyList<string> Warnings,
    string EvolutionHistoryReportPath,
    string BotEvolutionScoreReportPath,
    string ForwardEvaluationReportPath,
    string SignalExplainReportPath,
    string SignalEvaluationReportPath,
    string ApprovedChartAnnotationsReportPath,
    string ChartAnnotationReviewQueueReportPath,
    string ReportPath,
    string MarkdownPath);

public sealed class BotEvolutionRecommendationService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public BotEvolutionRecommendationService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "bot_evolution_recommendation");
    public string ReportPath => Path.Combine(Root, "bot_evolution_recommendation.json");
    public string MarkdownPath => Path.Combine(Root, "bot_evolution_recommendation.md");

    public BotEvolutionRecommendationReport Run()
    {
        Directory.CreateDirectory(Root);

        var evolutionHistoryService = new BotEvolutionHistoryService(_storagePaths, _runtimeRoot);
        var botEvolutionScoreService = new BotEvolutionScoreService(_storagePaths, _runtimeRoot);
        var forwardEvaluationService = new PaperForwardEvaluationService(_storagePaths, _runtimeRoot);
        var signalExplainService = new PaperSignalExplainService(_storagePaths, _runtimeRoot);
        var signalEvaluationService = new PaperSignalEvaluationService(_storagePaths, _runtimeRoot);
        var approvedRegistryService = new ApprovedChartAnnotationRegistryService(_storagePaths, _runtimeRoot);
        var reviewQueueService = new ChartAnnotationReviewQueueService(_storagePaths, _runtimeRoot);

        var evolutionHistory = evolutionHistoryService.LoadLatestReport() ?? evolutionHistoryService.Run();
        var botEvolutionScore = botEvolutionScoreService.LoadLatestReport() ?? botEvolutionScoreService.Run();
        var forwardEvaluation = forwardEvaluationService.LoadLatestReport() ?? forwardEvaluationService.Run();
        var signalExplain = signalExplainService.LoadLatestReport() ?? signalExplainService.Run();
        var signalEvaluation = signalEvaluationService.LoadLatestReport() ?? signalEvaluationService.Run(null, null);
        var approvedRegistry = approvedRegistryService.LoadLatestReport();
        var reviewQueue = reviewQueueService.LoadLatestReport();

        var warnings = new List<string>();
        if (signalEvaluation is null)
        {
            warnings.Add("signal_evaluation_report_missing");
        }

        if (signalExplain is null)
        {
            warnings.Add("signal_explain_report_missing");
        }

        if (approvedRegistry is null)
        {
            warnings.Add("approved_chart_annotations_report_missing");
        }

        if (reviewQueue is null)
        {
            warnings.Add("chart_annotation_review_queue_missing");
        }

        var opportunities = BuildOpportunities(signalEvaluation, signalExplain, approvedRegistry, reviewQueue, warnings)
            .OrderBy(item => PriorityRank(item.Priority))
            .ThenBy(item => item.AffectedAsset, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var report = new BotEvolutionRecommendationReport(
            ReportVersion: "bot_evolution_recommendation_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: opportunities.Count > 0 ? "ready" : "empty",
            EvolutionScore: botEvolutionScore.EvolutionScore,
            PreviousScore: botEvolutionScore.PreviousScore,
            ImprovementDelta: botEvolutionScore.ImprovementDelta,
            Trend: evolutionHistory.Trend,
            EvaluatedSignals: signalEvaluation?.EvaluatedSignals ?? 0,
            InvalidatedSignals: signalEvaluation?.InvalidatedSignals ?? 0,
            ExpiredSignals: signalEvaluation?.ExpiredSignals ?? 0,
            ApprovedAnnotationCount: approvedRegistry?.ApprovedCount ?? 0,
            PendingReviewCount: reviewQueue?.PendingCount ?? 0,
            TopImprovementOpportunities: opportunities,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            EvolutionHistoryReportPath: evolutionHistoryService.ReportPath,
            BotEvolutionScoreReportPath: botEvolutionScoreService.ReportPath,
            ForwardEvaluationReportPath: forwardEvaluationService.ReportPath,
            SignalExplainReportPath: signalExplainService.ReportPath,
            SignalEvaluationReportPath: signalEvaluationService.ReportPath,
            ApprovedChartAnnotationsReportPath: approvedRegistryService.ReportPath,
            ChartAnnotationReviewQueueReportPath: reviewQueueService.ReportPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public BotEvolutionRecommendationReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BotEvolutionRecommendationReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<BotEvolutionRecommendationOpportunity> BuildOpportunities(
        PaperSignalEvaluationReport? signalEvaluation,
        PaperSignalExplainReport? signalExplain,
        ApprovedChartAnnotationRegistryReport? approvedRegistry,
        ChartAnnotationReviewQueueReport? reviewQueue,
        List<string> warnings)
    {
        var opportunities = new List<BotEvolutionRecommendationOpportunity>();
        var evaluationByAsset = (signalEvaluation?.Signals ?? [])
            .GroupBy(item => item.Asset, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var explainByAsset = (signalExplain?.Signals ?? [])
            .GroupBy(ParseAssetFromSignalId, StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var reviewByAsset = (reviewQueue?.Items ?? [])
            .GroupBy(item => item.Asset, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var approvedByAsset = (approvedRegistry?.Items ?? [])
            .GroupBy(item => item.Asset, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var assets = evaluationByAsset.Keys
            .Concat(explainByAsset.Keys)
            .Concat(reviewByAsset.Keys)
            .Concat(approvedByAsset.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(asset => asset, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var asset in assets)
        {
            evaluationByAsset.TryGetValue(asset, out var evaluationItems);
            explainByAsset.TryGetValue(asset, out var explainItems);
            reviewByAsset.TryGetValue(asset, out var reviewItems);
            approvedByAsset.TryGetValue(asset, out var approvedItems);

            var invalidated = evaluationItems?.Any(item => item.SignalInvalidated) == true;
            var expired = evaluationItems?.Any(item => item.SignalExpired) == true;
            var sessionBlocked = evaluationItems?.Any(item => !item.SessionAllowed) == true
                || explainItems?.Any(item => !item.SessionAllowed) == true;
            var approvedButNotPromoted = approvedItems?.Any(item => item.Approved && !item.PromotedToEmbedded) == true;
            var reviewNeedsPrice = reviewItems?.Any(item => string.Equals(item.Status, "needs_price_review", StringComparison.OrdinalIgnoreCase)) == true;

            if (invalidated && reviewNeedsPrice)
            {
                opportunities.Add(new BotEvolutionRecommendationOpportunity(
                    Priority: "high",
                    AffectedAsset: asset,
                    RootCause: "missing_chart_annotation_price_fields",
                    ExpectedScoreGain: 3.2m,
                    RequiredManualAction: "complete the chart annotation price fields and approve the review artifact",
                    BlockingDependency: "chart_annotation_review_queue"));
                continue;
            }

            if (invalidated)
            {
                opportunities.Add(new BotEvolutionRecommendationOpportunity(
                    Priority: "high",
                    AffectedAsset: asset,
                    RootCause: "paper_entry_disabled",
                    ExpectedScoreGain: 3.2m,
                    RequiredManualAction: "inspect the chart annotation mapping and re-export the embedded package",
                    BlockingDependency: "approved_chart_annotations"));
                continue;
            }

            if (expired)
            {
                opportunities.Add(new BotEvolutionRecommendationOpportunity(
                    Priority: "medium",
                    AffectedAsset: asset,
                    RootCause: "signal_expiry_window_too_short",
                    ExpectedScoreGain: 0.9m,
                    RequiredManualAction: "refresh the embedded signal expiry window and re-export the bot package",
                    BlockingDependency: "embedded_signal_package"));
                continue;
            }

            if (sessionBlocked)
            {
                opportunities.Add(new BotEvolutionRecommendationOpportunity(
                    Priority: "medium",
                    AffectedAsset: asset,
                    RootCause: "session_gate_mismatch",
                    ExpectedScoreGain: 0.9m,
                    RequiredManualAction: "test the signal during an allowed session or correct the session mapping",
                    BlockingDependency: "session_filter"));
                continue;
            }

            if (approvedButNotPromoted)
            {
                opportunities.Add(new BotEvolutionRecommendationOpportunity(
                    Priority: "low",
                    AffectedAsset: asset,
                    RootCause: "approved_annotation_pending_review_or_promotion",
                    ExpectedScoreGain: 0.4m,
                    RequiredManualAction: "complete the review decision and promote the annotation if desired",
                    BlockingDependency: "chart_annotation_review_decisions"));
                continue;
            }

            opportunities.Add(new BotEvolutionRecommendationOpportunity(
                Priority: "low",
                AffectedAsset: asset,
                RootCause: "no_immediate_action_required",
                ExpectedScoreGain: 0.0m,
                RequiredManualAction: "none",
                BlockingDependency: "none"));
        }

        if (opportunities.Count == 0)
        {
            warnings.Add("no_assets_available_for_recommendation");
        }

        return opportunities;
    }

    private static string ParseAssetFromSignalId(PaperSignalExplainItem item)
    {
        var parts = item.SignalId.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2 ? parts[1] : string.Empty;
    }

    private static int PriorityRank(string priority)
        => priority.ToLowerInvariant() switch
        {
            "high" => 0,
            "medium" => 1,
            "low" => 2,
            _ => 3,
        };

    private static string BuildMarkdown(BotEvolutionRecommendationReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Bot Evolution Recommendation");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- evolution_score: {report.EvolutionScore:0.0}");
        sb.AppendLine($"- previous_score: {report.PreviousScore?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-"}");
        sb.AppendLine($"- improvement_delta: {report.ImprovementDelta?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-"}");
        sb.AppendLine($"- trend: {report.Trend}");
        sb.AppendLine($"- evaluated_signals: {report.EvaluatedSignals}");
        sb.AppendLine($"- invalidated_signals: {report.InvalidatedSignals}");
        sb.AppendLine($"- expired_signals: {report.ExpiredSignals}");
        sb.AppendLine($"- approved_annotation_count: {report.ApprovedAnnotationCount}");
        sb.AppendLine($"- pending_review_count: {report.PendingReviewCount}");
        sb.AppendLine();

        foreach (var item in report.TopImprovementOpportunities)
        {
            sb.AppendLine($"## {item.Priority.ToUpperInvariant()} / {item.AffectedAsset}");
            sb.AppendLine($"- root_cause: {item.RootCause}");
            sb.AppendLine($"- expected_score_gain: {item.ExpectedScoreGain:0.0}");
            sb.AppendLine($"- required_manual_action: {item.RequiredManualAction}");
            sb.AppendLine($"- blocking_dependency: {item.BlockingDependency}");
            sb.AppendLine();
        }

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
        }

        return sb.ToString();
    }
}
