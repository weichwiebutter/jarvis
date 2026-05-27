using System.Text.Json;

namespace Hermes.Runtime;

public sealed class BotCandidateRejectionAnalyzer
{
    private const double MinimumWalkForwardConfidence = 0.62;
    private const double MinimumRealismScore = 0.68;
    private const double MinimumRegimeConsistency = 0.52;
    private const double MinimumSampleQuality = 0.55;
    private const double MinimumPositiveSimulationRatio = 0.90;
    private const double MaximumRiskOfRuinProbability = 0.05;

    private readonly StoragePaths _storagePaths;

    public BotCandidateRejectionAnalyzer(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string StrategyResearchRoot => Path.Combine(_storagePaths.Root, "strategy_research");

    public string AnalysisPath => Path.Combine(StrategyResearchRoot, "bot_candidate_rejection_analysis.json");

    public string NearMissPath => Path.Combine(StrategyResearchRoot, "near_miss_strategies.json");

    public string ImprovementExperimentsPath => Path.Combine(StrategyResearchRoot, "recommended_improvement_experiments.json");

    public BotCandidateRejectionAnalysisReport Run()
    {
        Directory.CreateDirectory(StrategyResearchRoot);
        var pipeline = new BotCandidatePipelineService(_storagePaths);
        var report = pipeline.LoadReport() ?? pipeline.Evaluate();
        var rejected = LoadCandidates(pipeline.RejectedCandidatesPath);
        if (rejected.Count == 0)
        {
            rejected = report.RejectedCandidates;
        }

        var diagnostics = rejected
            .Select(BuildDiagnostics)
            .OrderByDescending(item => item.NearMissScore)
            .ThenBy(item => item.PrimaryRejectionReason, StringComparer.Ordinal)
            .ToList();
        var reasonSummaries = BuildReasonSummaries(rejected);
        var nearMisses = diagnostics
            .Where(item => item.IsNearMiss)
            .Take(100)
            .ToList();
        var bestRejected = diagnostics
            .Take(100)
            .ToList();
        var suggestions = BuildImprovementSuggestions(reasonSummaries, diagnostics);
        var whyNoCandidates = BuildWhyNoCandidates(report, reasonSummaries, nearMisses.Count);

        var analysis = new BotCandidateRejectionAnalysisReport(
            ReportId: $"bot_candidate_rejection_analysis_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            CandidatesAnalyzed: rejected.Count,
            RejectedCandidates: rejected.Count,
            NearMissCount: nearMisses.Count,
            WhyNoCandidates: whyNoCandidates,
            ReasonSummaries: reasonSummaries,
            CandidateDiagnostics: diagnostics,
            NearMissStrategies: nearMisses,
            BestRejectedStrategies: bestRejected,
            PotentialClusters: BuildPotentialClusters(diagnostics),
            UnsuitableClusters: BuildUnsuitableClusters(diagnostics),
            RecommendedImprovementExperiments: suggestions,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(AnalysisPath, JsonSerializer.Serialize(analysis, JsonDefaults.WriteOptions));
        File.WriteAllText(NearMissPath, JsonSerializer.Serialize(nearMisses, JsonDefaults.WriteOptions));
        File.WriteAllText(ImprovementExperimentsPath, JsonSerializer.Serialize(suggestions, JsonDefaults.WriteOptions));
        return analysis;
    }

    public BotCandidateRejectionAnalysisReport? LoadAnalysis()
    {
        if (!File.Exists(AnalysisPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BotCandidateRejectionAnalysisReport>(
                File.ReadAllText(AnalysisPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static CandidateGateDiagnostics BuildDiagnostics(BotCandidate candidate)
    {
        var primary = PrimaryReason(candidate.RejectionReasons);
        var secondary = candidate.RejectionReasons
            .Where(reason => !reason.Equals(primary, StringComparison.Ordinal))
            .Take(8)
            .ToList();
        var metrics = BuildMetricGaps(candidate).ToList();
        var weakest = metrics
            .OrderByDescending(metric => metric.Gap)
            .FirstOrDefault();
        var nearest = metrics
            .Where(metric => metric.Gap > 0 && metric.Gap < 1)
            .OrderBy(metric => metric.Gap)
            .FirstOrDefault();
        var score = Math.Round(metrics.Count == 0 ? 0 : metrics.Average(metric => metric.Score), 4);
        var hasCoverageMissing = candidate.RejectionReasons.Any(reason => reason.Contains("report_missing", StringComparison.Ordinal));
        var hasOosMissing = candidate.RejectionReasons.Contains("missing_out_of_sample", StringComparer.Ordinal);
        var hardFailures = candidate.RejectionReasons.Count(reason =>
            reason.Contains("too_good_to_be_true", StringComparison.Ordinal)
            || reason.Contains("risk_of_ruin", StringComparison.Ordinal)
            || reason.Contains("stress_cost", StringComparison.Ordinal)
            || reason.Contains("monte_carlo_failed", StringComparison.Ordinal));
        var isNearMiss = score >= 0.72
            && !hasCoverageMissing
            && !hasOosMissing
            && hardFailures <= 1;
        var isCompletelyUnsuitable = score < 0.35
            || hardFailures >= 3
            || candidate.RejectionReasons.Count >= 10;

        return new CandidateGateDiagnostics(
            CandidateId: candidate.CandidateId,
            StrategyId: candidate.StrategyId,
            StrategyFamily: candidate.StrategyFamily,
            PatternId: candidate.PatternId,
            Symbol: candidate.Symbol,
            Timeframe: candidate.Timeframe,
            Status: candidate.Status.ToString(),
            PrimaryRejectionReason: primary,
            SecondaryRejectionReasons: secondary,
            WeakestMetric: weakest is null ? "unknown" : $"{weakest.Name}:{weakest.Value:0.####}",
            NearestPassThreshold: nearest is null ? "no_near_numeric_threshold" : $"{nearest.Name}: {nearest.Value:0.####} -> {nearest.Threshold}",
            ImprovementHint: ImprovementHint(primary, weakest?.Name ?? string.Empty),
            NearMissScore: score,
            IsNearMiss: isNearMiss,
            IsCompletelyUnsuitable: isCompletelyUnsuitable);
    }

    private static IEnumerable<MetricGap> BuildMetricGaps(BotCandidate candidate)
    {
        var criteria = candidate.Criteria;
        yield return BoolGap("oos_available", criteria.OosAvailable);
        yield return LowerBoundGap("walkforward_confidence", criteria.WalkForwardConfidence, MinimumWalkForwardConfidence);
        yield return LowerBoundGap("realism_score", criteria.RealismScore, MinimumRealismScore);
        yield return LowerBoundGap("sample_quality", criteria.SampleQuality, MinimumSampleQuality);
        yield return LowerBoundGap("regime_consistency", criteria.RegimeConsistencyScore, MinimumRegimeConsistency);
        yield return LowerBoundGap("positive_simulation_ratio", criteria.PositiveSimulationRatio, MinimumPositiveSimulationRatio);
        yield return BoolGap("monte_carlo_passed", criteria.MonteCarloPassed);
        yield return BoolGap("survives_spread_x2", criteria.SurvivesSpreadX2);
        yield return BoolGap("survives_stress_cost", criteria.SurvivesStressCost);
        yield return UpperBoundGap("risk_of_ruin", criteria.RiskOfRuinProbabilityEstimate, MaximumRiskOfRuinProbability);
        yield return BoolGap("recommended_risk_available", criteria.RecommendedRiskAvailable);
    }

    private static MetricGap LowerBoundGap(string name, double value, double threshold)
    {
        var score = threshold <= 0 ? 1 : Math.Clamp(value / threshold, 0, 1);
        return new MetricGap(name, value, $">={threshold:0.####}", Math.Round(1 - score, 4), Math.Round(score, 4));
    }

    private static MetricGap UpperBoundGap(string name, double value, double threshold)
    {
        var score = value <= threshold ? 1 : Math.Clamp(threshold / Math.Max(value, threshold), 0, 1);
        return new MetricGap(name, value, $"<={threshold:0.####}", Math.Round(1 - score, 4), Math.Round(score, 4));
    }

    private static MetricGap BoolGap(string name, bool passed) =>
        new(name, passed ? 1 : 0, "true", passed ? 0 : 1, passed ? 1 : 0);

    private static string PrimaryReason(IReadOnlyList<string> reasons)
    {
        if (reasons.Count == 0)
        {
            return "not_rejected";
        }

        var priority = new[]
        {
            "missing_out_of_sample",
            "realism_score_too_low",
            "sample_quality_too_low",
            "walkforward_confidence_too_low",
            "monte_carlo_failed",
            "monte_carlo_report_missing",
            "stress_cost_failed",
            "spread_x2_failed",
            "cost_stress_report_missing",
            "risk_of_ruin_too_high",
            "risk_of_ruin_report_missing",
            "regime_consistency_too_low",
            "profit_factor_too_low",
            "max_drawdown_unacceptable"
        };

        return priority.FirstOrDefault(reason => reasons.Contains(reason, StringComparer.Ordinal))
            ?? reasons.First();
    }

    private static IReadOnlyList<RejectionReasonSummary> BuildReasonSummaries(IReadOnlyList<BotCandidate> candidates)
    {
        var total = Math.Max(1, candidates.Count);
        return candidates
            .SelectMany(candidate => candidate.RejectionReasons)
            .GroupBy(reason => reason, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new RejectionReasonSummary(
                Reason: group.Key,
                Count: group.Count(),
                Share: Math.Round((double)group.Count() / total, 4),
                Category: Category(group.Key),
                ImprovementHint: ImprovementHint(group.Key, group.Key)))
            .ToList();
    }

    private static IReadOnlyList<StrategyImprovementSuggestion> BuildImprovementSuggestions(
        IReadOnlyList<RejectionReasonSummary> summaries,
        IReadOnlyList<CandidateGateDiagnostics> diagnostics)
    {
        var reasons = summaries.Select(summary => summary.Reason).ToHashSet(StringComparer.Ordinal);
        var suggestions = new List<StrategyImprovementSuggestion>();
        AddIf(reasons.Contains("missing_out_of_sample"), new(
            "collect_oos_validation_data",
            "must",
            "Improve OOS stability",
            "Run stricter train/validation/OOS windows before any strategy can become a demo-bot candidate.",
            "oos_available",
            ["missing_out_of_sample", "validation_degradation", "out_of_sample_decay"],
            "Prevents in-sample winners from entering demo validation."));
        AddIf(reasons.Contains("monte_carlo_report_missing") || reasons.Contains("cost_stress_report_missing") || reasons.Contains("risk_of_ruin_report_missing"), new(
            "expand_quality_gate_coverage",
            "must",
            "Run quality gates for more candidates",
            "Increase max_quality_candidates gradually so promising rejected strategies receive Monte-Carlo, stress and risk reports.",
            "quality_gate_coverage",
            ["monte_carlo_report_missing", "cost_stress_report_missing", "risk_of_ruin_report_missing"],
            "Separates untested candidates from candidates that truly fail risk gates."));
        AddIf(reasons.Contains("realism_score_too_low"), new(
            "reduce_trading_frequency",
            "must",
            "Reduce unrealistic trade frequency",
            "Add filters for duplicate signals, low-quality sessions and overactive patterns before re-running realism scoring.",
            "realism_score",
            ["realism_score_too_low", "too_good_to_be_true", "suspicious_winrate"],
            "Improves realism and reduces too-good-to-be-true candidates."));
        AddIf(reasons.Contains("stress_cost_failed") || reasons.Contains("spread_x2_failed") || reasons.Contains("cost_stress_report_missing"), new(
            "improve_cost_resilience",
            "should",
            "Improve spread/slippage resilience",
            "Avoid high-spread sessions, add session filters, and retest with conservative Fusion Markets cost assumptions.",
            "cost_stress",
            ["stress_cost_failed", "spread_x2_failed", "cost_stress_report_missing"],
            "Filters out strategies that only work before execution costs."));
        AddIf(reasons.Contains("sample_quality_too_low"), new(
            "increase_sample_quality",
            "should",
            "Increase sample quality",
            "Require more valid trades per symbol/timeframe/regime and reduce tiny-sample conclusions.",
            "sample_quality",
            ["sample_quality_too_low", "too_few_trades", "small_sample_quality_penalty"],
            "Makes candidate classification less fragile."));
        AddIf(reasons.Contains("regime_consistency_too_low"), new(
            "add_regime_session_filters",
            "should",
            "Add volatility/session filters",
            "Evaluate strategy families separately by trend/range/volatility regime and London/New York sessions.",
            "regime_consistency",
            ["regime_consistency_too_low"],
            "Finds where a strategy works instead of forcing one global score."));
        AddIf(reasons.Contains("risk_of_ruin_too_high") || reasons.Contains("risk_of_ruin_report_missing"), new(
            "reduce_drawdown_sensitivity",
            "should",
            "Reduce drawdown sensitivity",
            "Retest with lower risk-per-trade assumptions and stricter losing-streak constraints.",
            "risk_of_ruin",
            ["risk_of_ruin_too_high", "risk_of_ruin_report_missing"],
            "Keeps demo candidates compatible with conservative risk limits."));
        AddIf(diagnostics.Any(item => item.NearMissScore > 0.55), new(
            "test_different_timeframes",
            "later",
            "Test different timeframe specialization",
            "Take the best rejected clusters and retest them on symbol/timeframe subsets instead of global scoring.",
            "timeframe_stability",
            ["walkforward_confidence_too_low", "regime_consistency_too_low"],
            "May reveal narrow but stable candidate niches."));

        return suggestions
            .DistinctBy(suggestion => suggestion.SuggestionId)
            .ToList();

        void AddIf(bool condition, StrategyImprovementSuggestion suggestion)
        {
            if (condition)
            {
                suggestions.Add(suggestion);
            }
        }
    }

    private static IReadOnlyList<string> BuildWhyNoCandidates(
        BotCandidateReport report,
        IReadOnlyList<RejectionReasonSummary> summaries,
        int nearMissCount)
    {
        var top = summaries.Take(5).Select(summary => $"{summary.Reason}:{summary.Count}").ToList();
        var reasons = new List<string>
        {
            $"demo_bot_candidate_count:{report.DemoBotCandidateCount}",
            $"rejected_count:{report.RejectedCandidateCount}",
            $"near_miss_count:{nearMissCount}"
        };
        reasons.AddRange(top.Select(item => $"top_blocker:{item}"));
        reasons.Add("no_bot_created_no_trades_no_broker_action");
        return reasons;
    }

    private static IReadOnlyList<string> BuildPotentialClusters(IReadOnlyList<CandidateGateDiagnostics> diagnostics)
    {
        return diagnostics
            .Where(item => !item.IsCompletelyUnsuitable)
            .GroupBy(item => $"{item.StrategyFamily}/{item.PatternId ?? "-"}", StringComparer.Ordinal)
            .Select(group => new
            {
                Cluster = group.Key,
                Count = group.Count(),
                AverageScore = group.Average(item => item.NearMissScore),
                NearMisses = group.Count(item => item.IsNearMiss)
            })
            .Where(item => item.Count >= 2)
            .OrderByDescending(item => item.AverageScore)
            .ThenByDescending(item => item.NearMisses)
            .Take(12)
            .Select(item => $"{item.Cluster}:avg_score={item.AverageScore:0.####},count={item.Count},near_miss={item.NearMisses}")
            .ToList();
    }

    private static IReadOnlyList<string> BuildUnsuitableClusters(IReadOnlyList<CandidateGateDiagnostics> diagnostics)
    {
        return diagnostics
            .GroupBy(item => $"{item.StrategyFamily}/{item.PatternId ?? "-"}", StringComparer.Ordinal)
            .Select(group => new
            {
                Cluster = group.Key,
                Count = group.Count(),
                Unsuitable = group.Count(item => item.IsCompletelyUnsuitable),
                AverageScore = group.Average(item => item.NearMissScore)
            })
            .Where(item => item.Count >= 4)
            .OrderByDescending(item => (double)item.Unsuitable / item.Count)
            .ThenBy(item => item.AverageScore)
            .Take(12)
            .Select(item => $"{item.Cluster}:unsuitable={item.Unsuitable}/{item.Count},avg_score={item.AverageScore:0.####}")
            .ToList();
    }

    private static string Category(string reason)
    {
        if (reason.Contains("oos", StringComparison.Ordinal)
            || reason.Contains("out_of_sample", StringComparison.Ordinal)
            || reason.Contains("validation", StringComparison.Ordinal))
        {
            return "walkforward_oos";
        }

        if (reason.Contains("realism", StringComparison.Ordinal) || reason.Contains("too_good", StringComparison.Ordinal))
        {
            return "realism";
        }

        if (reason.Contains("cost", StringComparison.Ordinal) || reason.Contains("spread", StringComparison.Ordinal) || reason.Contains("slippage", StringComparison.Ordinal))
        {
            return "cost_stress";
        }

        if (reason.Contains("monte_carlo", StringComparison.Ordinal))
        {
            return "monte_carlo";
        }

        if (reason.Contains("risk_of_ruin", StringComparison.Ordinal) || reason.Contains("risk", StringComparison.Ordinal))
        {
            return "risk";
        }

        if (reason.Contains("sample", StringComparison.Ordinal) || reason.Contains("trades", StringComparison.Ordinal))
        {
            return "sample_quality";
        }

        if (reason.Contains("regime", StringComparison.Ordinal))
        {
            return "regime";
        }

        if (reason.Contains("overfit", StringComparison.Ordinal) || reason.Contains("parameter", StringComparison.Ordinal))
        {
            return "overfit";
        }

        return "performance";
    }

    private static string ImprovementHint(string reason, string weakestMetric)
    {
        var combined = $"{reason} {weakestMetric}";
        if (combined.Contains("oos", StringComparison.Ordinal)
            || combined.Contains("out_of_sample", StringComparison.Ordinal)
            || combined.Contains("validation", StringComparison.Ordinal)
            || combined.Contains("walkforward", StringComparison.Ordinal))
        {
            return "improve OOS stability";
        }

        if (combined.Contains("cost", StringComparison.Ordinal) || combined.Contains("spread", StringComparison.Ordinal) || combined.Contains("slippage", StringComparison.Ordinal))
        {
            return "improve cost resilience and avoid high-spread sessions";
        }

        if (combined.Contains("monte_carlo", StringComparison.Ordinal))
        {
            return "reduce drawdown sensitivity and unstable return sequencing";
        }

        if (combined.Contains("risk_of_ruin", StringComparison.Ordinal) || combined.Contains("recommended_risk", StringComparison.Ordinal))
        {
            return "lower risk assumptions and reduce losing-streak exposure";
        }

        if (combined.Contains("realism", StringComparison.Ordinal) || combined.Contains("too_good", StringComparison.Ordinal))
        {
            return "reduce trading frequency and remove unrealistic signals";
        }

        if (combined.Contains("sample", StringComparison.Ordinal))
        {
            return "increase sample quality";
        }

        if (combined.Contains("regime", StringComparison.Ordinal))
        {
            return "add volatility/session filter";
        }

        if (combined.Contains("timeframe", StringComparison.Ordinal))
        {
            return "test different timeframe";
        }

        return "continue research with stricter validation";
    }

    private IReadOnlyList<BotCandidate> LoadCandidates(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<BotCandidate>>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    private sealed record MetricGap(string Name, double Value, string Threshold, double Gap, double Score);
}
