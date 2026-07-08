using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record BotEvolutionMetricBreakdown(
    decimal NetR,
    decimal WinRate,
    decimal AverageR,
    decimal ProfitFactor,
    decimal SignalQuality,
    decimal Confidence,
    int CompletedForwardTests,
    string SafetyStatus,
    decimal ExplainabilityScore);

public sealed record BotEvolutionScoreReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    decimal EvolutionScore,
    decimal? PreviousScore,
    decimal? ImprovementDelta,
    string Recommendation,
    string ConfidenceLevel,
    BotEvolutionMetricBreakdown Metrics,
    string PaperRuntimeStepReportPath,
    string PaperSignalExplainReportPath,
    string PaperTradeSummaryReportPath,
    string PaperTradeHistoryReportPath,
    string PaperForwardSessionReportPath,
    string? CurrentBotVersionRecommendationReportPath,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath);

public sealed class BotEvolutionScoreService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public BotEvolutionScoreService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "bot_evolution_score");
    public string ReportPath => Path.Combine(Root, "bot_evolution_score.json");
    public string MarkdownPath => Path.Combine(Root, "bot_evolution_score.md");

    public BotEvolutionScoreReport Run()
    {
        Directory.CreateDirectory(Root);

        var baseline = new BotEvolutionBaselineService(_storagePaths, _runtimeRoot).LoadLatest();
        var stepService = new PaperRuntimeStepService(_storagePaths, _runtimeRoot);
        var signalExplainService = new PaperSignalExplainService(_storagePaths, _runtimeRoot);
        var summaryService = new PaperTradeSummaryService(_storagePaths, _runtimeRoot);
        var historyService = new PaperTradeHistoryService(_storagePaths, _runtimeRoot);
        var forwardSessionService = new PaperForwardSessionReportService(_storagePaths, _runtimeRoot);

        var stepReport = stepService.LoadLatestReport() ?? stepService.Run();
        var signalExplainReport = signalExplainService.LoadLatestReport() ?? signalExplainService.Run();
        var summaryReport = summaryService.LoadLatestReport() ?? summaryService.Run();
        var historyReport = historyService.LoadLatestReport() ?? historyService.Run();
        var forwardSessionReport = forwardSessionService.LoadLatestReport() ?? forwardSessionService.Run();

        var warnings = new List<string>();
        if (!stepReport.RuntimeReady)
        {
            warnings.Add("paper_runtime_not_ready");
        }

        if (!stepReport.SafetyFlagsActive)
        {
            warnings.Add("paper_safety_flags_inactive");
        }

        if (!stepReport.BrokerActionNone)
        {
            warnings.Add("paper_broker_action_not_none");
        }

        if (historyReport.ClosedTradeCount == 0)
        {
            warnings.Add("no_closed_paper_trades");
        }

        if (signalExplainReport.ExplainedSignals == 0)
        {
            warnings.Add("no_signal_explainability_report");
        }

        var metrics = BuildMetrics(stepReport, signalExplainReport, summaryReport, historyReport, forwardSessionReport);
        var evolutionScore = ComputeEvolutionScore(metrics);
        var previousScore = baseline?.Score;
        var improvementDelta = previousScore.HasValue ? Math.Round(evolutionScore - previousScore.Value, 1) : (decimal?)null;
        var recommendation = DetermineRecommendation(evolutionScore, improvementDelta, metrics.SafetyStatus);
        var confidenceLevel = DetermineConfidenceLevel(stepReport, signalExplainReport, summaryReport, historyReport, forwardSessionReport, warnings);

        var report = new BotEvolutionScoreReport(
            ReportVersion: "bot_evolution_score_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: warnings.Count == 0 ? "ready" : "partial",
            EvolutionScore: evolutionScore,
            PreviousScore: previousScore.HasValue ? Math.Round(previousScore.Value, 1) : null,
            ImprovementDelta: improvementDelta,
            Recommendation: recommendation,
            ConfidenceLevel: confidenceLevel,
            Metrics: metrics,
            PaperRuntimeStepReportPath: stepService.ReportPath,
            PaperSignalExplainReportPath: signalExplainService.ReportPath,
            PaperTradeSummaryReportPath: summaryService.ReportPath,
            PaperTradeHistoryReportPath: historyService.ReportPath,
            PaperForwardSessionReportPath: forwardSessionService.ReportPath,
            CurrentBotVersionRecommendationReportPath: Path.Combine(_storagePaths.Root, "reports", "bot_version_recommendation", "bot_version_recommendation_report.json"),
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public BotEvolutionScoreReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BotEvolutionScoreReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static BotEvolutionMetricBreakdown BuildMetrics(
        PaperRuntimeStepReport stepReport,
        PaperSignalExplainReport signalExplainReport,
        PaperTradeSummaryReport summaryReport,
        PaperTradeHistoryReport historyReport,
        PaperForwardSessionReport forwardSessionReport)
    {
        var netR = summaryReport.NetR;
        var winRate = historyReport.ClosedTradeCount <= 0
            ? 0m
            : Math.Round(historyReport.ClosedTrades.Count(trade => trade.RMultiple > 0m) / (decimal)historyReport.ClosedTradeCount, 4);
        var averageR = summaryReport.AverageRMultiple;
        var profitFactor = summaryReport.GrossLossR <= 0m
            ? (summaryReport.GrossProfitR > 0m ? summaryReport.GrossProfitR : 0m)
            : Math.Round(summaryReport.GrossProfitR / summaryReport.GrossLossR, 4);

        var evaluatedSignals = Math.Max(stepReport.EvaluatedSignals, 0);
        decimal signalQuality = 0m;
        if (evaluatedSignals > 0)
        {
            var actionableRatio = stepReport.ActionableSignals / (decimal)evaluatedSignals;
            var triggerRatio = stepReport.WouldTriggerSignals / (decimal)evaluatedSignals;
            var runtimeReadyScore = stepReport.RuntimeReady && stepReport.SafetyFlagsActive && stepReport.BrokerActionNone ? 1m : 0m;
            var invalidatedPenalty = stepReport.InvalidatedSignals / (decimal)evaluatedSignals;
            var skippedPenalty = stepReport.SkippedSignals / (decimal)evaluatedSignals;
            signalQuality = Math.Clamp((actionableRatio * 0.5m) + (triggerRatio * 0.3m) + (runtimeReadyScore * 0.2m) - (invalidatedPenalty * 0.15m) - (skippedPenalty * 0.05m), 0m, 1m);
        }

        var confidenceValues = signalExplainReport.Signals
            .Select(signal => signal.Confidence)
            .Where(value => value > 0m)
            .ToList();
        var confidence = confidenceValues.Count == 0 ? 0m : Math.Round(confidenceValues.Average(), 4);

        var completedForwardTests = forwardSessionReport.TimerTicks > 0 && forwardSessionReport.ClosedTrades > 0 ? forwardSessionReport.ClosedTrades : historyReport.ClosedTradeCount;
        var safetyStatus = forwardSessionReport.SafetyStatus.Equals("safe", StringComparison.OrdinalIgnoreCase)
            && stepReport.RuntimeReady
            && stepReport.SafetyFlagsActive
            && stepReport.BrokerActionNone
                ? "safe"
                : "partial";

        var explainabilityScore = ComputeExplainabilityScore(signalExplainReport);

        return new BotEvolutionMetricBreakdown(
            NetR: Math.Round(netR, 4),
            WinRate: Math.Round(winRate, 4),
            AverageR: Math.Round(averageR, 4),
            ProfitFactor: Math.Round(profitFactor, 4),
            SignalQuality: Math.Round(signalQuality, 4),
            Confidence: Math.Round(confidence, 4),
            CompletedForwardTests: completedForwardTests,
            SafetyStatus: safetyStatus,
            ExplainabilityScore: Math.Round(explainabilityScore, 4));
    }

    private static decimal ComputeEvolutionScore(BotEvolutionMetricBreakdown metrics)
    {
        var netRScore = Clamp01((metrics.NetR + 1m) / 2m);
        var averageRScore = Clamp01((metrics.AverageR + 1m) / 2m);
        var profitFactorScore = metrics.ProfitFactor <= 0m ? 0m : Clamp01(metrics.ProfitFactor / 2m);
        var completedTestsScore = Clamp01(metrics.CompletedForwardTests / 2m);
        var safetyScore = metrics.SafetyStatus.Equals("safe", StringComparison.OrdinalIgnoreCase) ? 1m : 0.35m;

        var rawScore =
            (netRScore * 0.18m) +
            (metrics.WinRate * 0.14m) +
            (averageRScore * 0.12m) +
            (profitFactorScore * 0.14m) +
            (metrics.SignalQuality * 0.12m) +
            (metrics.Confidence * 0.10m) +
            (completedTestsScore * 0.08m) +
            (safetyScore * 0.06m) +
            (metrics.ExplainabilityScore * 0.06m);

        return Math.Round(Clamp01(rawScore) * 100m, 1);
    }

    private static decimal ComputeExplainabilityScore(PaperSignalExplainReport explainReport)
    {
        if (explainReport.Signals.Count == 0 || explainReport.ExplainedSignals == 0)
        {
            return 0m;
        }

        var explainedCoverage = explainReport.ExplainedSignals / (decimal)Math.Max(explainReport.Signals.Count, 1);
        var blockerFree = explainReport.Signals.Count(signal => signal.ConfidenceBlockers.Count == 0) / (decimal)Math.Max(explainReport.Signals.Count, 1);
        var completeDetails = explainReport.Signals.Count(signal => signal.MissingConfidenceFields.Count == 0) / (decimal)Math.Max(explainReport.Signals.Count, 1);
        return Clamp01((explainedCoverage * 0.4m) + (blockerFree * 0.3m) + (completeDetails * 0.3m));
    }

    private static string DetermineRecommendation(decimal evolutionScore, decimal? improvementDelta, string safetyStatus)
    {
        if (!safetyStatus.Equals("safe", StringComparison.OrdinalIgnoreCase))
        {
            return "do_not_recommend_safety_partial";
        }

        if (evolutionScore >= 75m && (!improvementDelta.HasValue || improvementDelta.Value >= 0m))
        {
            return "recommend_new_version";
        }

        if (evolutionScore >= 60m)
        {
            return "hold_current_version";
        }

        return "do_not_recommend";
    }

    private static string DetermineConfidenceLevel(
        PaperRuntimeStepReport stepReport,
        PaperSignalExplainReport signalExplainReport,
        PaperTradeSummaryReport summaryReport,
        PaperTradeHistoryReport historyReport,
        PaperForwardSessionReport forwardSessionReport,
        IReadOnlyList<string> warnings)
    {
        var availableSources = 0;
        if (stepReport.RuntimeReady) availableSources++;
        if (signalExplainReport.ExplainedSignals > 0) availableSources++;
        if (summaryReport.ReportVersion is not null) availableSources++;
        if (historyReport.ClosedTradeCount >= 0) availableSources++;
        if (forwardSessionReport.TimerTicks >= 0) availableSources++;

        return warnings.Count == 0 && availableSources >= 5
            ? "very_high"
            : warnings.Count <= 1 && availableSources >= 4
                ? "high"
                : warnings.Count <= 3 && availableSources >= 3
                    ? "medium"
                    : "low";
    }

    private static decimal Clamp01(decimal value) => Math.Clamp(value, 0m, 1m);

    private static string BuildMarkdown(BotEvolutionScoreReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Bot Evolution Score");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- evolution_score: {report.EvolutionScore:0.0}");
        sb.AppendLine($"- previous_score: {report.PreviousScore?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-"}");
        sb.AppendLine($"- improvement_delta: {report.ImprovementDelta?.ToString("0.0", CultureInfo.InvariantCulture) ?? "-"}");
        sb.AppendLine($"- recommendation: {report.Recommendation}");
        sb.AppendLine($"- confidence_level: {report.ConfidenceLevel}");
        sb.AppendLine();
        sb.AppendLine("## Metrics");
        sb.AppendLine($"- net_r: {report.Metrics.NetR:0.####}");
        sb.AppendLine($"- win_rate: {report.Metrics.WinRate:0.####}");
        sb.AppendLine($"- average_r: {report.Metrics.AverageR:0.####}");
        sb.AppendLine($"- profit_factor: {report.Metrics.ProfitFactor:0.####}");
        sb.AppendLine($"- signal_quality: {report.Metrics.SignalQuality:0.####}");
        sb.AppendLine($"- confidence: {report.Metrics.Confidence:0.####}");
        sb.AppendLine($"- completed_forward_tests: {report.Metrics.CompletedForwardTests}");
        sb.AppendLine($"- safety_status: {report.Metrics.SafetyStatus}");
        sb.AppendLine($"- explainability_score: {report.Metrics.ExplainabilityScore:0.####}");
        sb.AppendLine();
        sb.AppendLine("## Report Paths");
        sb.AppendLine($"- paper_runtime_step_report_path: {report.PaperRuntimeStepReportPath}");
        sb.AppendLine($"- paper_signal_explain_report_path: {report.PaperSignalExplainReportPath}");
        sb.AppendLine($"- paper_trade_summary_report_path: {report.PaperTradeSummaryReportPath}");
        sb.AppendLine($"- paper_trade_history_report_path: {report.PaperTradeHistoryReportPath}");
        sb.AppendLine($"- paper_forward_session_report_path: {report.PaperForwardSessionReportPath}");
        if (!string.IsNullOrWhiteSpace(report.CurrentBotVersionRecommendationReportPath))
        {
            sb.AppendLine($"- current_bot_version_recommendation_report_path: {report.CurrentBotVersionRecommendationReportPath}");
        }

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
