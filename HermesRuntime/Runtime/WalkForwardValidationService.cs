using System.Text.Json;

namespace Hermes.Runtime;

public sealed class WalkForwardValidationService
{
    private readonly StoragePaths _storagePaths;

    public WalkForwardValidationService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string SimulationRoot => Path.Combine(_storagePaths.Root, "simulation");

    public string WalkForwardPath => Path.Combine(SimulationRoot, "walkforward_validation.json");

    public string OverfitReportPath => Path.Combine(SimulationRoot, "overfit_report.json");

    public string StrategyResearchOverfitReportPath => Path.Combine(_storagePaths.Root, "strategy_research", "overfit_report.json");

    public string RobustStrategiesPath => Path.Combine(_storagePaths.Root, "strategy_research", "robust_strategies.json");

    public string WalkForwardSummaryPath => Path.Combine(_storagePaths.Root, "reports", "simulation", "walkforward_summary.json");

    public WalkForwardValidationReport Run()
    {
        Directory.CreateDirectory(SimulationRoot);
        var simulations = new RealisticSimulationService(_storagePaths).LoadReports();
        if (simulations.Count == 0)
        {
            simulations = new RealisticSimulationService(_storagePaths).Run();
        }

        var regimePerformance = new MarketRegimeClassifier(_storagePaths).LoadStrategyPerformance();
        var regimeEntries = regimePerformance?.Entries ?? [];
        var assessments = simulations
            .GroupBy(report => report.StrategyVariantId, StringComparer.Ordinal)
            .Select(group => Assess(group.OrderByDescending(report => report.CreatedAtUtc).First(), regimeEntries))
            .OrderByDescending(assessment => assessment.WalkForwardConfidence)
            .ThenByDescending(assessment => assessment.ValidationScore)
            .ThenByDescending(assessment => assessment.OutOfSampleScore)
            .ToList();

        var report = new WalkForwardValidationReport(
            ReportId: $"walkforward_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            TrainFromUtc: new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            TrainToUtc: new DateTimeOffset(2024, 12, 31, 23, 59, 59, TimeSpan.Zero),
            ValidationFromUtc: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero),
            ValidationToUtc: new DateTimeOffset(2025, 12, 31, 23, 59, 59, TimeSpan.Zero),
            StrategiesEvaluated: assessments.Count,
            RobustStrategies: assessments.Count(item => item.Robust),
            OverfitSuspectedStrategies: assessments.Count(item => item.StrategyConfidence == "overfit_suspected"),
            HighRiskStrategies: assessments.Count(item => item.HighRisk),
            Assessments: assessments,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(WalkForwardPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        Directory.CreateDirectory(Path.Combine(_storagePaths.Root, "reports", "simulation"));
        var overfit = new
        {
            report.ReportId,
            report.CreatedAtUtc,
            report.OverfitSuspectedStrategies,
            report.HighRiskStrategies,
            reportPath = OverfitReportPath,
            overfitSuspects = assessments.Where(item => item.StrategyConfidence == "overfit_suspected").Take(50).ToList(),
            highRisk = assessments.Where(item => item.HighRisk).Take(50).ToList(),
            noAutoTrading = true,
            humanReviewRequired = true
        };
        File.WriteAllText(OverfitReportPath, JsonSerializer.Serialize(overfit, JsonDefaults.WriteOptions));
        File.WriteAllText(Path.Combine(_storagePaths.Root, "reports", "simulation", "overfit_report.json"), JsonSerializer.Serialize(overfit, JsonDefaults.WriteOptions));
        Directory.CreateDirectory(Path.Combine(_storagePaths.Root, "strategy_research"));
        File.WriteAllText(StrategyResearchOverfitReportPath, JsonSerializer.Serialize(overfit, JsonDefaults.WriteOptions));
        var robust = new
        {
            report.ReportId,
            report.CreatedAtUtc,
            robustStrategies = assessments.Where(item => item.Robust).Take(100).ToList(),
            promisingStrategies = assessments.Where(item => item.StrategyConfidence == "promising").Take(100).ToList(),
            noAutoTrading = true,
            humanReviewRequired = true
        };
        File.WriteAllText(RobustStrategiesPath, JsonSerializer.Serialize(robust, JsonDefaults.WriteOptions));
        var summary = new
        {
            report.ReportId,
            report.CreatedAtUtc,
            report.TrainFromUtc,
            report.TrainToUtc,
            report.ValidationFromUtc,
            report.ValidationToUtc,
            report.StrategiesEvaluated,
            report.RobustStrategies,
            report.OverfitSuspectedStrategies,
            report.HighRiskStrategies,
            averageDegradation = Math.Round(assessments.Count == 0 ? 0 : assessments.Average(item => item.DegradationScore), 4),
            averageRobustnessGap = Math.Round(assessments.Count == 0 ? 0 : assessments.Average(item => item.RobustnessGap), 4),
            averageRealismPenalty = Math.Round(assessments.Count == 0 ? 0 : assessments.Average(item => item.RealismPenalty), 4),
            averageRealismScore = Math.Round(assessments.Count == 0 ? 0 : assessments.Average(item => item.RealismScore), 4),
            averageOverfitRisk = Math.Round(assessments.Count == 0 ? 0 : assessments.Average(item => item.OverfitRisk), 4),
            averageCostSensitivity = Math.Round(assessments.Count == 0 ? 0 : assessments.Average(item => item.CostSensitivity), 4),
            averageRegimeConsistency = Math.Round(assessments.Count == 0 ? 0 : assessments.Average(item => item.RegimeConsistencyScore), 4),
            oosAvailable = assessments.Count(item => item.OosAvailable),
            tooGoodToBeTrue = assessments.Count(item => item.TooGoodToBeTrue),
            topRobust = assessments.Where(item => item.Robust).Take(25).ToList(),
            rejected = assessments.Where(item => item.StrategyConfidence == "rejected").Take(25).ToList(),
            noAutoTrading = true,
            humanReviewRequired = true
        };
        File.WriteAllText(WalkForwardSummaryPath, JsonSerializer.Serialize(summary, JsonDefaults.WriteOptions));
        return report;
    }

    public WalkForwardValidationReport? LoadReport()
    {
        if (!File.Exists(WalkForwardPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<WalkForwardValidationReport>(
                File.ReadAllText(WalkForwardPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static WalkForwardStrategyAssessment Assess(
        StrategySimulationReport report,
        IReadOnlyList<StrategyRegimePerformanceEntry> regimeEntries)
    {
        var metrics = report.Metrics;
        var regimeGate = BuildRegimeGate(report, regimeEntries);
        var oosAvailable = HasOutOfSampleTrades(report);
        var trainScore = Score(metrics.ProfitFactor, metrics.Expectancy, metrics.StabilityScore, metrics.MaxDrawdown, metrics.RobustnessConfidence);
        var validationPenalty = metrics.Winrate > 0.88 ? Math.Min(0.5, (metrics.Winrate - 0.88) * 2.2 + 0.08) : 0.06;
        validationPenalty += metrics.RealismPenalty * 0.36;
        validationPenalty += metrics.CostSensitivity * 0.16;
        validationPenalty += (1 - metrics.LossDistributionQuality) * 0.12;
        validationPenalty += (1 - metrics.SampleQuality) * 0.16;
        validationPenalty += metrics.TooGoodToBeTrue ? 0.3 : 0;
        validationPenalty += regimeGate.ConsistencyScore < 0.45 ? 0.12 : 0;
        var validationScore = Math.Clamp(trainScore - validationPenalty - Math.Max(0, metrics.ConsecutiveLosses - 4) * 0.03, 0, 1);
        var outOfSamplePenalty = (metrics.MaxDrawdown >= -0.25 ? 0.24 : 0.08)
            + metrics.OverfitRisk * 0.24
            + metrics.CostSensitivity * 0.12
            + (oosAvailable ? 0 : 0.5);
        var outOfSampleScore = Math.Clamp(validationScore - outOfSamplePenalty, 0, 1);
        var degradation = Math.Clamp(trainScore - validationScore + (oosAvailable ? 0 : 0.22), 0, 1);
        var robustnessGap = Math.Clamp(validationScore - outOfSampleScore, 0, 1);
        var highRisk = metrics.MaxDrawdown < -12
            || metrics.ConsecutiveLosses >= 6
            || metrics.ProfitFactor < 1.05
            || metrics.RealismPenalty >= 0.55
            || metrics.OverfitRisk >= 0.72
            || metrics.CostSensitivity >= 0.78
            || metrics.TooGoodToBeTrue;
        var flags = OverfitDetector.Detect(metrics, validationScore, outOfSampleScore);
        if (!oosAvailable)
        {
            flags = flags.Concat(["missing_out_of_sample_data"]).Distinct(StringComparer.Ordinal).ToList();
        }

        if (degradation >= 0.28)
        {
            flags = flags.Concat(["validation_degradation"]).Distinct(StringComparer.Ordinal).ToList();
        }

        if (robustnessGap >= 0.22)
        {
            flags = flags.Concat(["oos_robustness_gap"]).Distinct(StringComparer.Ordinal).ToList();
        }

        if (regimeGate.ConsistencyScore < 0.45)
        {
            flags = flags.Concat(["weak_regime_consistency"]).Distinct(StringComparer.Ordinal).ToList();
        }

        if (regimeGate.SampleQuality < 0.35)
        {
            flags = flags.Concat(["low_regime_sample_quality"]).Distinct(StringComparer.Ordinal).ToList();
        }

        var tooGoodToBeTrue = metrics.TooGoodToBeTrue
            || flags.Contains("too_good_to_be_true", StringComparer.Ordinal)
            || (flags.Contains("suspicious_winrate", StringComparer.Ordinal)
                && flags.Contains("high_realism_penalty", StringComparer.Ordinal));
        var walkForwardConfidence = Math.Clamp(
            validationScore * 0.28
            + outOfSampleScore * 0.28
            + metrics.RealismScore * 0.16
            + metrics.RobustnessConfidence * 0.12
            + regimeGate.ConsistencyScore * 0.1
            + metrics.LossDistributionQuality * 0.06
            - degradation * 0.22
            - metrics.CostSensitivity * 0.14
            - (tooGoodToBeTrue ? 0.25 : 0),
            0,
            1);

        var confidence = RobustStrategyClassifier.Classify(
            metrics,
            validationScore,
            outOfSampleScore,
            flags,
            highRisk,
            oosAvailable,
            walkForwardConfidence,
            regimeGate.ConsistencyScore,
            regimeGate.SampleQuality);

        return new WalkForwardStrategyAssessment(
            StrategyVariantId: report.StrategyVariantId,
            StrategyFamily: report.StrategyFamily,
            PatternId: report.PatternId,
            TrainScore: Math.Round(trainScore, 4),
            ValidationScore: Math.Round(validationScore, 4),
            OutOfSampleScore: Math.Round(outOfSampleScore, 4),
            StrategyConfidence: confidence,
            OverfitFlags: flags,
            Robust: confidence == "robust",
            HighRisk: highRisk,
            TrainPerformance: Math.Round(trainScore, 4),
            ValidationPerformance: Math.Round(validationScore, 4),
            DegradationScore: Math.Round(degradation, 4),
            RobustnessGap: Math.Round(robustnessGap, 4),
            RealismPenalty: metrics.RealismPenalty,
            RobustnessConfidence: metrics.RobustnessConfidence,
            ParameterStability: metrics.ParameterStability,
            SampleQuality: metrics.SampleQuality,
            OverfitRisk: metrics.OverfitRisk,
            RealismScore: metrics.RealismScore,
            RealismPenaltyReason: metrics.RealismPenaltyReason,
            TooGoodToBeTrue: tooGoodToBeTrue,
            CostSensitivity: metrics.CostSensitivity,
            LossDistributionQuality: metrics.LossDistributionQuality,
            OosAvailable: oosAvailable,
            WalkForwardConfidence: Math.Round(walkForwardConfidence, 4),
            RegimeConsistencyScore: regimeGate.ConsistencyScore,
            PreferredRegimes: regimeGate.PreferredRegimes,
            AvoidedRegimes: regimeGate.AvoidedRegimes,
            RegimeSampleQuality: regimeGate.SampleQuality);
    }

    private static double Score(double profitFactor, double expectancy, double stability, double maxDrawdown, double robustness)
    {
        return Math.Clamp(
            (Math.Min(3, profitFactor) / 3 * 0.25)
            + (Math.Clamp(expectancy, -1, 2) + 1) / 3 * 0.2
            + stability * 0.25
            + robustness * 0.2
            + Math.Max(0, 1 - Math.Abs(maxDrawdown) / 20) * 0.1,
            0,
            1);
    }

    private static bool HasOutOfSampleTrades(StrategySimulationReport report)
    {
        var trades = report.PositionLifecycles?.Count > 0
            ? report.PositionLifecycles.Select(position => position.OpenedAtUtc)
            : report.SampleTrades.Select(trade => trade.OpenedAtUtc);
        return report.Metrics.TradeCount >= 50
            && report.Metrics.SampleQuality >= 0.45
            && trades.Any(timestamp => timestamp >= new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    private static RegimeGateInfo BuildRegimeGate(
        StrategySimulationReport report,
        IReadOnlyList<StrategyRegimePerformanceEntry> entries)
    {
        var matches = entries
            .Where(entry => entry.StrategyFamily.Equals(report.StrategyFamily, StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(report.PatternId)
                    || entry.PatternId.Equals(report.PatternId, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (matches.Count == 0)
        {
            return new RegimeGateInfo(0, [], ["no_regime_profile"], 0);
        }

        var byRegime = matches
            .GroupBy(entry => entry.RegimeType, StringComparer.OrdinalIgnoreCase)
            .Select(group => new
            {
                Regime = group.Key,
                Score = group.Average(entry => entry.RegimeFitScore),
                Variants = group.Sum(entry => entry.VariantCount)
            })
            .OrderByDescending(item => item.Score)
            .ToList();

        var average = byRegime.Average(item => item.Score);
        var variance = byRegime.Average(item => Math.Pow(item.Score - average, 2));
        var sampleQuality = Math.Clamp(byRegime.Count / 4.0, 0, 1);
        var top = byRegime.First().Score;
        var second = byRegime.Skip(1).FirstOrDefault()?.Score ?? 0;
        var singleRegimePenalty = top >= 0.82 && second < 0.5 ? 0.24 : 0;
        var consistency = Math.Clamp(average - Math.Sqrt(variance) * 0.55 + sampleQuality * 0.18 - singleRegimePenalty, 0, 1);

        return new RegimeGateInfo(
            ConsistencyScore: Math.Round(consistency, 4),
            PreferredRegimes: byRegime
                .Take(4)
                .Select(item => $"{item.Regime}:fit={item.Score:0.####},variants={item.Variants}")
                .ToList(),
            AvoidedRegimes: byRegime
                .OrderBy(item => item.Score)
                .Take(4)
                .Select(item => $"{item.Regime}:fit={item.Score:0.####},variants={item.Variants}")
                .ToList(),
            SampleQuality: Math.Round(sampleQuality, 4));
    }

    private sealed record RegimeGateInfo(
        double ConsistencyScore,
        IReadOnlyList<string> PreferredRegimes,
        IReadOnlyList<string> AvoidedRegimes,
        double SampleQuality);
}
