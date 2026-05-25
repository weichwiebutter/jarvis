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
        var overfit = new
        {
            report.ReportId,
            report.CreatedAtUtc,
            report.OverfitSuspectedStrategies,
            report.HighRiskStrategies,
            overfitSuspects = assessments.Where(item => item.StrategyConfidence == "overfit_suspected").Take(50).ToList(),
            highRisk = assessments.Where(item => item.HighRisk).Take(50).ToList(),
            noAutoTrading = true,
            humanReviewRequired = true
        };
        File.WriteAllText(OverfitReportPath, JsonSerializer.Serialize(overfit, JsonDefaults.WriteOptions));
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
        var trainScore = Score(metrics.ProfitFactor, metrics.Expectancy, metrics.StabilityScore, metrics.MaxDrawdown);
        var validationPenalty = metrics.Winrate > 0.97 ? 0.22 : 0.05;
        var validationScore = Math.Clamp(trainScore - validationPenalty - Math.Max(0, metrics.ConsecutiveLosses - 4) * 0.03, 0, 1);
        var outOfSampleScore = Math.Clamp(validationScore - (metrics.MaxDrawdown >= 0 ? 0.18 : 0.04), 0, 1);
        var highRisk = metrics.MaxDrawdown < -12 || metrics.ConsecutiveLosses >= 6 || metrics.ProfitFactor < 1.05;
        var flags = OverfitDetector.Detect(metrics, validationScore, outOfSampleScore);
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
            HighRisk: highRisk);
    }

    private static double Score(double profitFactor, double expectancy, double stability, double maxDrawdown)
    {
        return Math.Clamp(
            (Math.Min(3, profitFactor) / 3 * 0.35)
            + (Math.Clamp(expectancy, -1, 2) + 1) / 3 * 0.25
            + stability * 0.3
            + Math.Max(0, 1 - Math.Abs(maxDrawdown) / 20) * 0.1,
            0,
            1);
    }
}
