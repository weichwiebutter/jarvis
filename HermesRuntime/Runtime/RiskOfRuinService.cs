using System.Text.Json;

namespace Hermes.Runtime;

public sealed class RiskOfRuinService
{
    private const int DefaultMaxCandidates = 100;

    private static readonly IReadOnlyList<double> RiskLevels = [0.25, 0.5, 1.0];

    private readonly StoragePaths _storagePaths;

    public RiskOfRuinService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string ReportsDirectory => Path.Combine(_storagePaths.Root, "reports", "risk");

    public string ReportPath => Path.Combine(ReportsDirectory, "risk_of_ruin_report.json");

    public RiskOfRuinReport Run(int maxCandidates = DefaultMaxCandidates)
    {
        maxCandidates = Math.Clamp(maxCandidates, 1, 500);
        Directory.CreateDirectory(ReportsDirectory);

        var entries = QualityGateStrategySelector.LoadTopSimulationReports(_storagePaths, maxCandidates)
            .Select(Estimate)
            .OrderBy(entry => entry.RiskOfRuinPassed ? 0 : 1)
            .ThenBy(entry => entry.AccountRuinProbabilityEstimate)
            .ThenByDescending(entry => entry.RecommendedMaxRiskPerTrade)
            .ToList();

        var report = new RiskOfRuinReport(
            ReportId: $"risk_of_ruin_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            StrategiesEvaluated: entries.Count,
            Passed: entries.Count(entry => entry.RiskOfRuinPassed),
            Failed: entries.Count(entry => !entry.RiskOfRuinPassed),
            AverageRuinProbabilityEstimate: Math.Round(entries.Count == 0 ? 0 : entries.Average(entry => entry.AccountRuinProbabilityEstimate), 4),
            AverageRecommendedMaxRiskPerTrade: Math.Round(entries.Count == 0 ? 0 : entries.Average(entry => entry.RecommendedMaxRiskPerTrade), 4),
            Entries: entries,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    public RiskOfRuinReport? LoadReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RiskOfRuinReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static RiskOfRuinEntry Estimate(StrategySimulationReport report)
    {
        var metrics = report.Metrics;
        var profiles = RiskLevels
            .Select(risk => EstimateProfile(metrics, risk))
            .ToList();
        var recommended = profiles
            .Where(profile => profile.AccountRuinProbabilityEstimate <= 0.03
                && profile.ExpectedDrawdownPercent <= 12
                && profile.LosingStreakRisk <= 0.35)
            .OrderByDescending(profile => profile.RiskPerTradePercent)
            .FirstOrDefault();
        var conservative = recommended ?? profiles.OrderBy(profile => profile.RiskPerTradePercent).First();
        var riskPassed = recommended is not null
            && conservative.AccountRuinProbabilityEstimate <= 0.05
            && metrics.TradeCount >= 30
            && metrics.RealismScore >= 0.5
            && !metrics.TooGoodToBeTrue;

        return new RiskOfRuinEntry(
            StrategyVariantId: report.StrategyVariantId,
            StrategyFamily: report.StrategyFamily,
            PatternId: report.PatternId,
            Symbol: QualityGateStrategySelector.Symbol(report),
            Timeframe: QualityGateStrategySelector.Timeframe(report),
            ExpectedDrawdown: Math.Round(conservative.ExpectedDrawdownPercent, 4),
            LosingStreakRisk: Math.Round(conservative.LosingStreakRisk, 4),
            AccountRuinProbabilityEstimate: Math.Round(conservative.AccountRuinProbabilityEstimate, 4),
            RecommendedMaxRiskPerTrade: Math.Round(recommended?.RiskPerTradePercent ?? 0, 4),
            RiskOfRuinPassed: riskPassed,
            Profiles: profiles);
    }

    private static RiskOfRuinProfile EstimateProfile(SimulationPerformanceMetrics metrics, double riskPerTradePercent)
    {
        var tradeCount = Math.Clamp(metrics.TradeCount, 1, 10000);
        var winrate = Math.Clamp(metrics.Winrate, 0.01, 0.99);
        var lossProbability = 1 - winrate;
        var streakLength = riskPerTradePercent >= 1.0 ? 8 : riskPerTradePercent >= 0.5 ? 10 : 12;
        var streakWindows = Math.Max(1, tradeCount - streakLength + 1);
        var losingStreakRisk = 1 - Math.Pow(1 - Math.Pow(lossProbability, streakLength), streakWindows);
        var expectedDrawdown = Math.Abs(metrics.MaxDrawdown) * riskPerTradePercent;
        var qualityPenalty = (1 - Math.Clamp(metrics.SampleQuality, 0, 1)) * 0.18
            + Math.Clamp(metrics.OverfitRisk, 0, 1) * 0.16
            + (metrics.TooGoodToBeTrue ? 0.18 : 0);
        var costPenalty = Math.Clamp(metrics.CostSensitivity, 0, 1) * 0.08;
        var ruinProbability = Math.Clamp(
            losingStreakRisk * (riskPerTradePercent / 2.0)
            + Math.Max(0, expectedDrawdown - 18) / 100.0
            + qualityPenalty
            + costPenalty,
            0,
            1);

        return new RiskOfRuinProfile(
            RiskPerTradePercent: riskPerTradePercent,
            ExpectedDrawdownPercent: Math.Round(expectedDrawdown, 4),
            LosingStreakRisk: Math.Round(losingStreakRisk, 4),
            AccountRuinProbabilityEstimate: Math.Round(ruinProbability, 4));
    }
}
