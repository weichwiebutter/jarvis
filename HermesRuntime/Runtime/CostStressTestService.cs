using System.Text.Json;

namespace Hermes.Runtime;

public sealed class CostStressTestService
{
    private const int DefaultMaxCandidates = 100;

    private static readonly IReadOnlyList<CostStressScenario> Scenarios =
    [
        new("normal_cost", "normal_cost", 1.0, 0.0, 1.0, 0.0),
        new("spread_x2", "spread_x2", 2.0, 0.0, 1.0, 0.02),
        new("spread_x3", "spread_x3", 3.0, 0.0, 1.0, 0.04),
        new("slippage_0_1_pip", "slippage_0_1_pip", 1.0, 0.1, 1.0, 0.02),
        new("slippage_0_3_pip", "slippage_0_3_pip", 1.0, 0.3, 1.0, 0.05),
        new("slippage_0_5_pip", "slippage_0_5_pip", 1.0, 0.5, 1.0, 0.08),
        new("stress_cost", "stress_cost", 3.0, 0.5, 1.35, 0.12)
    ];

    private readonly StoragePaths _storagePaths;

    public CostStressTestService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string ReportsDirectory => Path.Combine(_storagePaths.Root, "reports", "stress_tests");

    public string ReportPath => Path.Combine(ReportsDirectory, "cost_stress_report.json");

    public CostStressReport Run(int maxCandidates = DefaultMaxCandidates)
    {
        maxCandidates = Math.Clamp(maxCandidates, 1, 500);
        Directory.CreateDirectory(ReportsDirectory);

        var results = QualityGateStrategySelector.LoadTopSimulationReports(_storagePaths, maxCandidates)
            .Select(Stress)
            .OrderBy(result => result.SurvivesStressCost ? 0 : 1)
            .ThenBy(result => result.SurvivesSpreadX2 ? 0 : 1)
            .ThenBy(result => result.CostFailureReason, StringComparer.Ordinal)
            .ToList();

        var report = new CostStressReport(
            ReportId: $"cost_stress_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            StrategiesEvaluated: results.Count,
            SurvivesNormalCost: results.Count(result => result.SurvivesNormalCost),
            SurvivesSpreadX2: results.Count(result => result.SurvivesSpreadX2),
            SurvivesSpreadX3: results.Count(result => result.SurvivesSpreadX3),
            SurvivesStressCost: results.Count(result => result.SurvivesStressCost),
            StressCostFailures: results.Count(result => !result.SurvivesStressCost),
            Results: results,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    public CostStressReport? LoadReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CostStressReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static CostStressResult Stress(StrategySimulationReport report)
    {
        var scenarioResults = Scenarios
            .Select(scenario => StressScenario(report.Metrics, scenario))
            .ToList();
        var normal = scenarioResults.First(result => result.Scenario.ScenarioId == "normal_cost");
        var spreadX2 = scenarioResults.First(result => result.Scenario.ScenarioId == "spread_x2");
        var spreadX3 = scenarioResults.First(result => result.Scenario.ScenarioId == "spread_x3");
        var stress = scenarioResults.First(result => result.Scenario.ScenarioId == "stress_cost");
        var reason = BuildFailureReason(normal, spreadX2, spreadX3, stress);

        return new CostStressResult(
            StrategyVariantId: report.StrategyVariantId,
            StrategyFamily: report.StrategyFamily,
            PatternId: report.PatternId,
            Symbol: QualityGateStrategySelector.Symbol(report),
            Timeframe: QualityGateStrategySelector.Timeframe(report),
            SurvivesNormalCost: normal.Survived,
            SurvivesSpreadX2: spreadX2.Survived,
            SurvivesSpreadX3: spreadX3.Survived,
            SurvivesStressCost: stress.Survived,
            CostFailureReason: reason,
            ScenarioResults: scenarioResults);
    }

    private static CostStressScenarioResult StressScenario(
        SimulationPerformanceMetrics metrics,
        CostStressScenario scenario)
    {
        var tradeCount = Math.Max(1, metrics.TradeCount);
        var costPressure = metrics.CostSensitivity
            + Math.Max(0, scenario.SpreadMultiplier - 1) * 0.14
            + scenario.SlippagePips * 0.22
            + Math.Max(0, scenario.CommissionMultiplier - 1) * 0.08
            + scenario.ExecutionDelayPenaltyR;
        var adjustedProfitFactor = Math.Max(0, metrics.ProfitFactor - costPressure * 1.15);
        var adjustedNetR = metrics.NetR
            - metrics.EstimatedCostR * Math.Max(1, scenario.SpreadMultiplier)
            - tradeCount * (scenario.SlippagePips * 0.015 + scenario.ExecutionDelayPenaltyR * 0.03);
        var survivalScore = Math.Clamp(
            metrics.RobustnessConfidence
            + metrics.RealismScore * 0.25
            - costPressure * 0.35
            - (metrics.TooGoodToBeTrue ? 0.2 : 0),
            0,
            1);
        var survived = adjustedProfitFactor >= 1.1
            && adjustedNetR > 0
            && survivalScore >= 0.35
            && metrics.TradeCount >= 30;

        return new CostStressScenarioResult(
            Scenario: scenario,
            AdjustedProfitFactor: Math.Round(adjustedProfitFactor, 4),
            AdjustedNetR: Math.Round(adjustedNetR, 4),
            SurvivalScore: Math.Round(survivalScore, 4),
            Survived: survived);
    }

    private static string BuildFailureReason(
        CostStressScenarioResult normal,
        CostStressScenarioResult spreadX2,
        CostStressScenarioResult spreadX3,
        CostStressScenarioResult stress)
    {
        if (!normal.Survived)
        {
            return "fails_normal_cost";
        }

        if (!spreadX2.Survived)
        {
            return "fails_spread_x2";
        }

        if (!spreadX3.Survived)
        {
            return "fails_spread_x3";
        }

        if (!stress.Survived && (stress.AdjustedProfitFactor < 0.75 || stress.AdjustedNetR <= 0))
        {
            return "total_failure_under_stress_cost";
        }

        if (!stress.Survived)
        {
            return "weak_under_stress_cost";
        }

        return "passed";
    }
}
