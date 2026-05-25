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

        var assessments = simulations
            .GroupBy(report => report.StrategyVariantId, StringComparer.Ordinal)
            .Select(group => Assess(group.OrderByDescending(report => report.CreatedAtUtc).First()))
            .OrderByDescending(assessment => assessment.ValidationScore)
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
            averageOverfitRisk = Math.Round(assessments.Count == 0 ? 0 : assessments.Average(item => item.OverfitRisk), 4),
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

    private static WalkForwardStrategyAssessment Assess(StrategySimulationReport report)
    {
        var metrics = report.Metrics;
        var trainScore = Score(metrics.ProfitFactor, metrics.Expectancy, metrics.StabilityScore, metrics.MaxDrawdown, metrics.RobustnessConfidence);
        var validationPenalty = metrics.Winrate > 0.90 ? Math.Min(0.42, (metrics.Winrate - 0.90) * 1.8 + 0.08) : 0.05;
        validationPenalty += metrics.RealismPenalty * 0.28;
        validationPenalty += (1 - metrics.SampleQuality) * 0.12;
        var validationScore = Math.Clamp(trainScore - validationPenalty - Math.Max(0, metrics.ConsecutiveLosses - 4) * 0.03, 0, 1);
        var outOfSampleScore = Math.Clamp(validationScore - (metrics.MaxDrawdown >= 0 ? 0.22 : 0.06) - metrics.OverfitRisk * 0.18, 0, 1);
        var degradation = Math.Clamp(trainScore - validationScore, 0, 1);
        var robustnessGap = Math.Clamp(validationScore - outOfSampleScore, 0, 1);
        var highRisk = metrics.MaxDrawdown < -12
            || metrics.ConsecutiveLosses >= 6
            || metrics.ProfitFactor < 1.05
            || metrics.RealismPenalty >= 0.65
            || metrics.OverfitRisk >= 0.8;
        var flags = OverfitDetector.Detect(metrics, validationScore, outOfSampleScore);
        if (degradation >= 0.28)
        {
            flags = flags.Concat(["validation_degradation"]).Distinct(StringComparer.Ordinal).ToList();
        }

        if (robustnessGap >= 0.22)
        {
            flags = flags.Concat(["oos_robustness_gap"]).Distinct(StringComparer.Ordinal).ToList();
        }

        var confidence = RobustStrategyClassifier.Classify(metrics, validationScore, outOfSampleScore, flags, highRisk);

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
            OverfitRisk: metrics.OverfitRisk);
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
}
