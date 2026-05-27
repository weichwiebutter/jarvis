using System.Text.Json;

namespace Hermes.Runtime;

public sealed class ResearchQualityImprovementExperimentService
{
    private readonly StoragePaths _storagePaths;

    public ResearchQualityImprovementExperimentService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string StrategyResearchRoot => Path.Combine(_storagePaths.Root, "strategy_research");

    public string QualityImprovementPath => Path.Combine(StrategyResearchRoot, "quality_improvement_experiments.json");

    public string CostResiliencePath => Path.Combine(StrategyResearchRoot, "cost_resilience_experiments.json");

    public string OosStabilityPath => Path.Combine(StrategyResearchRoot, "oos_stability_experiments.json");

    public string RiskSensitivityPath => Path.Combine(StrategyResearchRoot, "risk_sensitivity_experiments.json");

    public QualityImprovementExperimentReport Run(int maxBatchSize = 64)
    {
        Directory.CreateDirectory(StrategyResearchRoot);
        maxBatchSize = Math.Clamp(maxBatchSize, 1, 250);

        var rejectionAnalyzer = new BotCandidateRejectionAnalyzer(_storagePaths);
        var rejectionAnalysis = rejectionAnalyzer.LoadAnalysis() ?? rejectionAnalyzer.Run();
        var targets = SelectTargets(rejectionAnalysis, maxBatchSize);
        var blockers = rejectionAnalysis.ReasonSummaries
            .Take(12)
            .Select(summary => $"{summary.Reason}:{summary.Count}:{summary.Category}")
            .ToList();

        var oos = targets
            .Select((target, index) => BuildOosExperiment(target, index + 1))
            .ToList();
        var cost = targets
            .Select((target, index) => BuildCostExperiment(target, index + 1))
            .ToList();
        var risk = targets
            .Select((target, index) => BuildRiskExperiment(target, index + 1))
            .ToList();
        var regimes = targets
            .Select((target, index) => BuildRegimeExperiment(target, index + 1))
            .ToList();

        var report = new QualityImprovementExperimentReport(
            ReportId: $"quality_improvement_experiments_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            CandidatesAnalyzed: rejectionAnalysis.CandidatesAnalyzed,
            BaselineNearMissCount: rejectionAnalysis.NearMissCount,
            BatchSize: targets.Count,
            BlockersAddressed: blockers,
            ExpectedBlockerReduction: BuildExpectedBlockerReduction(rejectionAnalysis.ReasonSummaries),
            OosExperiments: oos,
            CostResilienceExperiments: cost,
            RiskSensitivityExperiments: risk,
            RegimeSessionFilterExperiments: regimes,
            NearMissCountChanged: false,
            NearMissImpactNote: "Experiments are research plans only; near_miss_count changes only after new simulations, walk-forward validation, and candidate gates rerun.",
            NoCandidateApprovalForced: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(QualityImprovementPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(CostResiliencePath, JsonSerializer.Serialize(cost, JsonDefaults.WriteOptions));
        File.WriteAllText(OosStabilityPath, JsonSerializer.Serialize(oos, JsonDefaults.WriteOptions));
        File.WriteAllText(RiskSensitivityPath, JsonSerializer.Serialize(risk, JsonDefaults.WriteOptions));
        return report;
    }

    public QualityImprovementExperimentReport? LoadReport()
    {
        if (!File.Exists(QualityImprovementPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<QualityImprovementExperimentReport>(
                File.ReadAllText(QualityImprovementPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public IReadOnlyList<CostResilienceExperiment> LoadCostResilienceExperiments() =>
        LoadList<CostResilienceExperiment>(CostResiliencePath);

    public IReadOnlyList<OosQualityImprovementExperiment> LoadOosStabilityExperiments() =>
        LoadList<OosQualityImprovementExperiment>(OosStabilityPath);

    public IReadOnlyList<RiskSensitivityExperiment> LoadRiskSensitivityExperiments() =>
        LoadList<RiskSensitivityExperiment>(RiskSensitivityPath);

    private static IReadOnlyList<CandidateGateDiagnostics> SelectTargets(
        BotCandidateRejectionAnalysisReport rejectionAnalysis,
        int maxBatchSize)
    {
        var preferred = rejectionAnalysis.CandidateDiagnostics
            .Where(item => !item.IsCompletelyUnsuitable)
            .OrderByDescending(item => item.NearMissScore)
            .Take(maxBatchSize)
            .ToList();

        if (preferred.Count > 0)
        {
            return preferred;
        }

        return rejectionAnalysis.BestRejectedStrategies
            .OrderByDescending(item => item.NearMissScore)
            .Take(maxBatchSize)
            .ToList();
    }

    private static OosQualityImprovementExperiment BuildOosExperiment(CandidateGateDiagnostics target, int priority)
    {
        return new OosQualityImprovementExperiment(
            ExperimentId: $"oos_{target.StrategyId}_{priority:000}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            TargetStrategyId: target.StrategyId,
            StrategyFamily: target.StrategyFamily,
            PatternId: target.PatternId,
            Symbol: target.Symbol,
            Timeframe: target.Timeframe,
            PriorityRank: priority,
            SourceNearMissScore: target.NearMissScore,
            AddressedBlockers: EnsureBlocker(
                FilterBlockers(target, ["missing_out_of_sample", "walkforward", "validation", "sample_quality"]),
                "missing_out_of_sample"),
            ProposedFilters: [
                "tighten_signal_quality_score_threshold",
                "require_minimum_atr_or_candle_range_percentile",
                "require_train_validation_oos_split",
                "increase_walkforward_degradation_penalty",
                "require_parameter_stability_across_rolling_windows",
                "drop_variants_with_single_window_performance_spike"
            ],
            WalkForwardPlan: "train_2024_validate_2025_plus_rolling_quarterly_windows",
            RollingValidationWindows: [
                "2024_q1_train_2024_q2_validate",
                "2024_q2_train_2024_q3_validate",
                "2024_q3_train_2024_q4_validate",
                "2024_full_train_2025_oos_validate"
            ],
            ExpectedImpact: "Improves OOS stability and prevents in-sample-only winners from moving toward bot-candidate status.",
            NoTradingExecution: true,
            HumanReviewRequired: true);
    }

    private static CostResilienceExperiment BuildCostExperiment(CandidateGateDiagnostics target, int priority)
    {
        return new CostResilienceExperiment(
            ExperimentId: $"cost_{target.StrategyId}_{priority:000}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            TargetStrategyId: target.StrategyId,
            StrategyFamily: target.StrategyFamily,
            PatternId: target.PatternId,
            Symbol: target.Symbol,
            Timeframe: target.Timeframe,
            PriorityRank: priority,
            SourceNearMissScore: target.NearMissScore,
            AddressedBlockers: EnsureBlocker(
                FilterBlockers(target, ["cost", "spread", "slippage", "monte_carlo", "realism"]),
                "cost_stress_resilience"),
            ProposedFilters: [
                "minimum_expected_move_at_least_4x_estimated_cost",
                "skip_spread_widening_periods",
                "reject_entries_when_spread_exceeds_symbol_baseline",
                "stress_test_slippage_0_1_to_0_5_pip"
            ],
            MinimumMoveToCostRatio: 4.0,
            SpreadStressScenario: "normal_cost_vs_spread_x2_vs_spread_x3_vs_stress_cost",
            SlippageStressScenario: "0.1_pip_0.3_pip_0.5_pip",
            AvoidSessions: ["session_asia_low_liquidity", "session_unknown", "news_like_volatility_spread_widening"],
            ExpectedImpact: "Filters strategies that only work before Fusion-Markets-style spread, commission, and slippage assumptions.",
            NoTradingExecution: true,
            HumanReviewRequired: true);
    }

    private static RiskSensitivityExperiment BuildRiskExperiment(CandidateGateDiagnostics target, int priority)
    {
        return new RiskSensitivityExperiment(
            ExperimentId: $"risk_{target.StrategyId}_{priority:000}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            TargetStrategyId: target.StrategyId,
            StrategyFamily: target.StrategyFamily,
            PatternId: target.PatternId,
            Symbol: target.Symbol,
            Timeframe: target.Timeframe,
            PriorityRank: priority,
            SourceNearMissScore: target.NearMissScore,
            AddressedBlockers: EnsureBlocker(
                FilterBlockers(target, ["risk", "ruin", "drawdown", "monte_carlo", "realism"]),
                "risk_of_ruin_sensitivity"),
            RiskProfiles: [0.0025, 0.005, 0.01],
            MaxTradeFrequencyHint: "cap_daily_signals_and_enforce_cooldown_after_loss",
            DrawdownControl: "reject_if_worst_case_drawdown_or_losing_streak_exceeds_conservative_gate",
            TargetRuinProbability: 0.05,
            ExpectedImpact: "Reduces risk-of-ruin sensitivity before any strategy can be considered for demo validation.",
            NoTradingExecution: true,
            HumanReviewRequired: true);
    }

    private static RegimeSessionFilterExperiment BuildRegimeExperiment(CandidateGateDiagnostics target, int priority)
    {
        var isMeanReversion = target.StrategyFamily.Contains("mean", StringComparison.OrdinalIgnoreCase)
            || (target.PatternId?.Contains("reversion", StringComparison.OrdinalIgnoreCase) == true);
        var isBreakout = target.StrategyFamily.Contains("breakout", StringComparison.OrdinalIgnoreCase)
            || (target.PatternId?.Contains("breakout", StringComparison.OrdinalIgnoreCase) == true);
        string[] preferredRegimes = isMeanReversion
            ? ["ranging", "low_volatility"]
            : isBreakout
                ? ["breakout", "trending", "high_volatility"]
                : ["trending", "breakout", "ranging"];
        string[] avoidedRegimes = isMeanReversion
            ? ["news_like_volatility", "high_volatility"]
            : isBreakout
                ? ["low_volatility", "ranging"]
                : ["unknown", "news_like_volatility"];

        return new RegimeSessionFilterExperiment(
            ExperimentId: $"regime_{target.StrategyId}_{priority:000}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            TargetStrategyId: target.StrategyId,
            StrategyFamily: target.StrategyFamily,
            PatternId: target.PatternId,
            Symbol: target.Symbol,
            Timeframe: target.Timeframe,
            PriorityRank: priority,
            SourceNearMissScore: target.NearMissScore,
            AddressedBlockers: EnsureBlocker(
                FilterBlockers(target, ["regime", "session", "sample_quality", "realism"]),
                "regime_session_quality"),
            PreferredRegimes: preferredRegimes,
            AvoidedRegimes: avoidedRegimes,
            PreferredSessions: ["session_london", "session_newyork"],
            AvoidedSessions: ["session_asia_low_liquidity", "session_unknown"],
            VolatilityFilter: isMeanReversion ? "avoid_atr_expansion_and_news_like_volatility" : "require_atr_expansion_or_breakout_frequency",
            ExpectedImpact: "Improves sample quality by testing strategy families only where their market-regime assumptions are plausible.",
            NoTradingExecution: true,
            HumanReviewRequired: true);
    }

    private static IReadOnlyList<string> FilterBlockers(CandidateGateDiagnostics target, IReadOnlyList<string> fragments)
    {
        return new[] { target.PrimaryRejectionReason }
            .Concat(target.SecondaryRejectionReasons)
            .Where(reason => fragments.Any(fragment => reason.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.Ordinal)
            .DefaultIfEmpty(target.PrimaryRejectionReason)
            .Take(8)
            .ToList();
    }

    private static IReadOnlyList<string> EnsureBlocker(IReadOnlyList<string> blockers, string fallback)
    {
        if (blockers.Any(item => item.Contains(fallback, StringComparison.OrdinalIgnoreCase)))
        {
            return blockers;
        }

        return blockers
            .Concat([fallback])
            .Distinct(StringComparer.Ordinal)
            .Take(8)
            .ToList();
    }

    private static IReadOnlyList<string> BuildExpectedBlockerReduction(IReadOnlyList<RejectionReasonSummary> summaries)
    {
        var byCategory = summaries
            .GroupBy(summary => summary.Category, StringComparer.Ordinal)
            .OrderByDescending(group => group.Sum(summary => summary.Count))
            .Take(8)
            .Select(group => $"{group.Key}:targeted_by_experiments,count={group.Sum(summary => summary.Count)}")
            .ToList();

        if (byCategory.Count == 0)
        {
            return ["no_current_blockers_detected"];
        }

        return byCategory;
    }

    private static IReadOnlyList<T> LoadList<T>(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<T>>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }
}
