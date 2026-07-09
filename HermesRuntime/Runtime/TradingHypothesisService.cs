using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record TradingHypothesis(
    string HypothesisId,
    string SourcePattern,
    string Confidence,
    string ExpectedBenefit,
    bool ValidationRequired,
    int RequiredSampleSize,
    int CurrentSampleSize,
    string Status,
    string Priority,
    string Recommendation,
    string Basis,
    IReadOnlyList<string> SupportingMetrics,
    IReadOnlyList<string> RequiredManualActions,
    IReadOnlyList<string> BlockingDependencies);

public sealed record TradingHypothesisReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int PatternCount,
    int HypothesisCount,
    int HighPriorityCount,
    int MediumPriorityCount,
    int LowPriorityCount,
    IReadOnlyList<TradingHypothesis> Hypotheses,
    IReadOnlyList<string> Warnings,
    string TradingPatternLearningReportPath,
    string BotEvolutionHistoryReportPath,
    string BotEvolutionRecommendationReportPath,
    string PaperForwardEvaluationReportPath,
    string ReportPath,
    string MarkdownPath);

public sealed class TradingHypothesisService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public TradingHypothesisService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "trading_hypotheses");
    public string ReportPath => Path.Combine(Root, "trading_hypotheses.json");
    public string MarkdownPath => Path.Combine(Root, "trading_hypotheses.md");

    public TradingHypothesisReport Run()
    {
        Directory.CreateDirectory(Root);

        var patternService = new TradingPatternLearningService(_storagePaths, _runtimeRoot);
        var evolutionHistoryService = new BotEvolutionHistoryService(_storagePaths, _runtimeRoot);
        var recommendationService = new BotEvolutionRecommendationService(_storagePaths, _runtimeRoot);
        var forwardEvaluationService = new PaperForwardEvaluationService(_storagePaths, _runtimeRoot);

        var patternReport = patternService.LoadLatestReport() ?? patternService.Run();
        var evolutionHistoryReport = evolutionHistoryService.LoadLatestReport() ?? evolutionHistoryService.Run();
        var recommendationReport = recommendationService.LoadLatestReport() ?? recommendationService.Run();
        var forwardEvaluationReport = forwardEvaluationService.LoadLatestReport() ?? forwardEvaluationService.Run();

        var warnings = new List<string>();
        if (patternReport.Patterns.Count == 0)
        {
            warnings.Add("trading_pattern_learning_report_empty");
        }

        var hypotheses = BuildHypotheses(patternReport, evolutionHistoryReport, recommendationReport, forwardEvaluationReport)
            .OrderByDescending(item => PriorityRank(item.Priority))
            .ThenByDescending(item => StatusRank(item.Status))
            .ThenByDescending(item => item.CurrentSampleSize)
            .ThenBy(item => item.HypothesisId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var report = new TradingHypothesisReport(
            ReportVersion: "trading_hypotheses_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: hypotheses.Count > 0 ? "ready" : "empty",
            PatternCount: patternReport.PatternCount,
            HypothesisCount: hypotheses.Count,
            HighPriorityCount: hypotheses.Count(item => item.Priority.Equals("high", StringComparison.OrdinalIgnoreCase)),
            MediumPriorityCount: hypotheses.Count(item => item.Priority.Equals("medium", StringComparison.OrdinalIgnoreCase)),
            LowPriorityCount: hypotheses.Count(item => item.Priority.Equals("low", StringComparison.OrdinalIgnoreCase)),
            Hypotheses: hypotheses,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            TradingPatternLearningReportPath: patternService.ReportPath,
            BotEvolutionHistoryReportPath: evolutionHistoryService.ReportPath,
            BotEvolutionRecommendationReportPath: recommendationService.ReportPath,
            PaperForwardEvaluationReportPath: forwardEvaluationService.ReportPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public TradingHypothesisReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TradingHypothesisReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<TradingHypothesis> BuildHypotheses(
        TradingPatternLearningReport patternReport,
        BotEvolutionHistoryReport evolutionHistoryReport,
        BotEvolutionRecommendationReport recommendationReport,
        PaperForwardEvaluationReport forwardEvaluationReport)
    {
        var hypotheses = new List<TradingHypothesis>();

        foreach (var pattern in patternReport.Patterns)
        {
            hypotheses.Add(BuildPatternHypothesis(pattern));
        }

        if (recommendationReport.TopImprovementOpportunities.Count > 0)
        {
            foreach (var opportunity in recommendationReport.TopImprovementOpportunities.Take(5))
            {
                hypotheses.Add(BuildRecommendationHypothesis(opportunity, forwardEvaluationReport));
            }
        }
        else if (evolutionHistoryReport.Trend.Equals("stable", StringComparison.OrdinalIgnoreCase))
        {
            hypotheses.Add(new TradingHypothesis(
                HypothesisId: "hypothesis_bot_evolution_stability_requires_validation",
                SourcePattern: "bot_evolution_trend_stable",
                Confidence: "medium",
                ExpectedBenefit: "Preserve the current export baseline until a measurable improvement is detected.",
                ValidationRequired: true,
                RequiredSampleSize: Math.Max(3, evolutionHistoryReport.EntryCount + 2),
                CurrentSampleSize: evolutionHistoryReport.EntryCount,
                Status: evolutionHistoryReport.EntryCount == 0 ? "new" : "collecting_data",
                Priority: "low",
                Recommendation: "Continue collecting export history before making the next recommendation.",
                Basis: "The evolution history is stable and needs more samples before a new conclusion is justified.",
                SupportingMetrics:
                [
                    $"trend={evolutionHistoryReport.Trend}",
                    $"entry_count={evolutionHistoryReport.EntryCount}",
                    $"best_score={FormatNullableDecimal(evolutionHistoryReport.BestScore)}",
                    $"worst_score={FormatNullableDecimal(evolutionHistoryReport.WorstScore)}",
                    $"average_score={FormatNullableDecimal(evolutionHistoryReport.AverageScore)}",
                ],
                RequiredManualActions:
                [
                    "save a new evolution baseline after a new export",
                    "rerun bot-evolution-score on the next candidate",
                ],
                BlockingDependencies:
                [
                    "new_export_candidate",
                    "fresh_baseline_history",
                ]));
        }

        if (!hypotheses.Any(item => item.SourcePattern.Equals("signals_reach_would_trigger_when_session_and_spread_pass", StringComparison.OrdinalIgnoreCase)))
        {
            hypotheses.Add(new TradingHypothesis(
                HypothesisId: "hypothesis_trading_signal_quality_session_filter",
                SourcePattern: "signals_reach_would_trigger_when_session_and_spread_pass",
                Confidence: forwardEvaluationReport.ForwardRunStatus.Equals("green", StringComparison.OrdinalIgnoreCase) ? "medium" : "low",
                ExpectedBenefit: "Reduce skipped signals and increase the fraction of actionable forward signals.",
                ValidationRequired: true,
                RequiredSampleSize: 20,
                CurrentSampleSize: Math.Max(1, forwardEvaluationReport.SignalCount),
                Status: forwardEvaluationReport.SignalCount >= 20 ? "ready_for_validation" : "collecting_data",
                Priority: "medium",
                Recommendation: "Keep validating session and spread filters with additional forward sessions.",
                Basis: "Forward evaluation currently shows a limited sample and the current filters are still a primary gating mechanism.",
                SupportingMetrics:
                [
                    $"forward_run_status={forwardEvaluationReport.ForwardRunStatus}",
                    $"signal_count={forwardEvaluationReport.SignalCount}",
                    $"expired_signal_count={forwardEvaluationReport.ExpiredSignalCount}",
                    $"invalidated_signal_count={forwardEvaluationReport.InvalidatedSignalCount}",
                ],
                RequiredManualActions:
                [
                    "run additional forward sessions",
                    "review signal explainability for session and spread blockers",
                ],
                BlockingDependencies:
                [
                    "additional_forward_sessions",
                    "session_and_spread_validation",
                ]));
        }

        return hypotheses
            .GroupBy(item => item.HypothesisId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private static TradingHypothesis BuildPatternHypothesis(TradingPatternLearningPattern pattern)
    {
        var confidence = pattern.Confidence;
        var requiredSampleSize = DetermineRequiredSampleSize(pattern);
        var status = DetermineStatus(pattern, requiredSampleSize);
        var priority = DeterminePriority(pattern);
        var expectedBenefit = DetermineExpectedBenefit(pattern);
        var recommendation = pattern.Recommendation;
        var basis = pattern.Observation;
        var manualActions = pattern.RequiresValidation
            ? new List<string> { "collect more forward data", "review the pattern against the current paper reports" }
            : new List<string> { "review pattern evidence" };
        var blockers = BuildBlockingDependencies(pattern);

        return new TradingHypothesis(
            HypothesisId: $"hypothesis_{NormalizeId(pattern.PatternId)}",
            SourcePattern: pattern.PatternId,
            Confidence: confidence,
            ExpectedBenefit: expectedBenefit,
            ValidationRequired: pattern.RequiresValidation,
            RequiredSampleSize: requiredSampleSize,
            CurrentSampleSize: pattern.SampleSize,
            Status: status,
            Priority: priority,
            Recommendation: recommendation,
            Basis: basis,
            SupportingMetrics: pattern.SupportingMetrics.ToList(),
            RequiredManualActions: manualActions,
            BlockingDependencies: blockers);
    }

    private static TradingHypothesis BuildRecommendationHypothesis(BotEvolutionRecommendationOpportunity opportunity, PaperForwardEvaluationReport forwardEvaluationReport)
    {
        var status = opportunity.Priority.Equals("high", StringComparison.OrdinalIgnoreCase) ? "ready_for_validation" : "collecting_data";
        var requiredSampleSize = opportunity.Priority.Equals("high", StringComparison.OrdinalIgnoreCase) ? 12 : 20;
        var currentSampleSize = Math.Max(1, forwardEvaluationReport.SignalCount);
        return new TradingHypothesis(
            HypothesisId: $"hypothesis_{NormalizeId(opportunity.AffectedAsset)}_{NormalizeId(opportunity.RootCause)}",
            SourcePattern: opportunity.RootCause,
            Confidence: opportunity.Priority,
            ExpectedBenefit: $"+{opportunity.ExpectedScoreGain:0.0} evolution score potential",
            ValidationRequired: true,
            RequiredSampleSize: requiredSampleSize,
            CurrentSampleSize: currentSampleSize,
            Status: status,
            Priority: opportunity.Priority,
            Recommendation: opportunity.RequiredManualAction,
            Basis: $"Opportunity surfaced by bot evolution recommendation: {opportunity.BlockingDependency}.",
            SupportingMetrics:
            [
                $"affected_asset={opportunity.AffectedAsset}",
                $"expected_score_gain={opportunity.ExpectedScoreGain:0.0}",
                $"blocking_dependency={opportunity.BlockingDependency}",
            ],
            RequiredManualActions:
            [
                opportunity.RequiredManualAction,
            ],
            BlockingDependencies:
            [
                opportunity.BlockingDependency,
            ]);
    }

    private static IReadOnlyList<string> BuildBlockingDependencies(TradingPatternLearningPattern pattern)
    {
        var blockers = new List<string>();
        if (pattern.Observation.Contains("coverage", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add("annotation_coverage");
        }

        if (pattern.Observation.Contains("session", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add("session_filter");
        }

        if (pattern.Observation.Contains("expiry", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add("signal_expiry_window");
        }

        if (pattern.Observation.Contains("spread", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add("spread_filter");
        }

        if (pattern.Observation.Contains("stable", StringComparison.OrdinalIgnoreCase))
        {
            blockers.Add("new_export_candidate");
        }

        return blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static int DetermineRequiredSampleSize(TradingPatternLearningPattern pattern)
    {
        if (pattern.PatternId.Contains("coverage", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (pattern.Confidence.Equals("high", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(12, pattern.SampleSize);
        }

        if (pattern.Confidence.Equals("medium", StringComparison.OrdinalIgnoreCase))
        {
            return Math.Max(20, pattern.SampleSize + 8);
        }

        return Math.Max(20, pattern.SampleSize + 12);
    }

    private static string DetermineStatus(TradingPatternLearningPattern pattern, int requiredSampleSize)
    {
        if (pattern.PatternId.Contains("coverage", StringComparison.OrdinalIgnoreCase))
        {
            return "ready_for_validation";
        }

        if (pattern.SampleSize >= requiredSampleSize)
        {
            return "ready_for_validation";
        }

        if (pattern.SampleSize > 0)
        {
            return "collecting_data";
        }

        return "new";
    }

    private static string DeterminePriority(TradingPatternLearningPattern pattern)
    {
        if (pattern.PatternId.Contains("coverage", StringComparison.OrdinalIgnoreCase))
        {
            return "high";
        }

        if (pattern.PatternId.Contains("expiry", StringComparison.OrdinalIgnoreCase))
        {
            return "medium";
        }

        if (pattern.PatternId.Contains("session", StringComparison.OrdinalIgnoreCase))
        {
            return "medium";
        }

        return pattern.Confidence.Equals("high", StringComparison.OrdinalIgnoreCase) ? "high" : "low";
    }

    private static string DetermineExpectedBenefit(TradingPatternLearningPattern pattern)
    {
        if (pattern.PatternId.Contains("coverage", StringComparison.OrdinalIgnoreCase))
        {
            return "Remove the current embedded blocker and make the asset available for downstream validation.";
        }

        if (pattern.PatternId.Contains("expiry", StringComparison.OrdinalIgnoreCase))
        {
            return "Reduce expired signals and improve forward execution readiness.";
        }

        if (pattern.PatternId.Contains("session", StringComparison.OrdinalIgnoreCase))
        {
            return "Increase actionable signals by aligning evaluation with the correct session window.";
        }

        if (pattern.PatternId.Contains("would_trigger", StringComparison.OrdinalIgnoreCase))
        {
            return "Increase the number of actionable signals that survive the runtime filters.";
        }

        return "Improve the observed pattern performance before the next recommendation step.";
    }

    private static int StatusRank(string status)
    {
        return status.ToLowerInvariant() switch
        {
            "validated" => 4,
            "ready_for_validation" => 3,
            "collecting_data" => 2,
            "new" => 1,
            "rejected" => 0,
            _ => 0,
        };
    }

    private static int PriorityRank(string priority)
    {
        return priority.ToLowerInvariant() switch
        {
            "high" => 3,
            "medium" => 2,
            "low" => 1,
            _ => 0,
        };
    }

    private static string NormalizeId(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_');
        }

        var normalized = builder.ToString().Trim('_');
        while (normalized.Contains("__", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("__", "_", StringComparison.Ordinal);
        }

        return normalized;
    }

    private static string FormatNullableDecimal(decimal? value)
        => value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : "-";

    private void WriteReport(TradingHypothesisReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(TradingHypothesisReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Trading Hypotheses");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- pattern_count: {report.PatternCount}");
        sb.AppendLine($"- hypothesis_count: {report.HypothesisCount}");
        sb.AppendLine($"- high_priority_count: {report.HighPriorityCount}");
        sb.AppendLine($"- medium_priority_count: {report.MediumPriorityCount}");
        sb.AppendLine($"- low_priority_count: {report.LowPriorityCount}");
        sb.AppendLine();
        sb.AppendLine("## Sources");
        sb.AppendLine($"- trading_pattern_learning_report_path: {report.TradingPatternLearningReportPath}");
        sb.AppendLine($"- bot_evolution_history_report_path: {report.BotEvolutionHistoryReportPath}");
        sb.AppendLine($"- bot_evolution_recommendation_report_path: {report.BotEvolutionRecommendationReportPath}");
        sb.AppendLine($"- paper_forward_evaluation_report_path: {report.PaperForwardEvaluationReportPath}");

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("## Hypotheses");
        foreach (var hypothesis in report.Hypotheses)
        {
            sb.AppendLine($"- hypothesis_id: {hypothesis.HypothesisId}");
            sb.AppendLine($"  - source_pattern: {hypothesis.SourcePattern}");
            sb.AppendLine($"  - confidence: {hypothesis.Confidence}");
            sb.AppendLine($"  - expected_benefit: {hypothesis.ExpectedBenefit}");
            sb.AppendLine($"  - validation_required: {hypothesis.ValidationRequired.ToString().ToLowerInvariant()}");
            sb.AppendLine($"  - required_sample_size: {hypothesis.RequiredSampleSize}");
            sb.AppendLine($"  - current_sample_size: {hypothesis.CurrentSampleSize}");
            sb.AppendLine($"  - status: {hypothesis.Status}");
            sb.AppendLine($"  - priority: {hypothesis.Priority}");
            sb.AppendLine($"  - recommendation: {hypothesis.Recommendation}");
            sb.AppendLine($"  - basis: {hypothesis.Basis}");
            sb.AppendLine($"  - supporting_metrics: {string.Join("; ", hypothesis.SupportingMetrics)}");
            sb.AppendLine($"  - required_manual_actions: {string.Join("; ", hypothesis.RequiredManualActions)}");
            sb.AppendLine($"  - blocking_dependencies: {string.Join("; ", hypothesis.BlockingDependencies)}");
        }

        return sb.ToString();
    }
}
