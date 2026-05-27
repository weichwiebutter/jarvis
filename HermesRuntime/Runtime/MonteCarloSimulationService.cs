using System.Text.Json;

namespace Hermes.Runtime;

public sealed class MonteCarloSimulationService
{
    private const int DefaultSimulationRuns = 100;
    private const int DefaultMaxCandidates = 100;

    private readonly StoragePaths _storagePaths;

    public MonteCarloSimulationService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string ReportsDirectory => Path.Combine(_storagePaths.Root, "reports", "monte_carlo");

    public string ReportPath => Path.Combine(ReportsDirectory, "monte_carlo_report.json");

    public MonteCarloReport Run(int simulationRuns = DefaultSimulationRuns, int maxCandidates = DefaultMaxCandidates)
    {
        simulationRuns = Math.Clamp(simulationRuns, 20, 2000);
        maxCandidates = Math.Clamp(maxCandidates, 1, 500);
        Directory.CreateDirectory(ReportsDirectory);

        var reports = QualityGateStrategySelector.LoadTopSimulationReports(_storagePaths, maxCandidates);
        var results = reports
            .Select(report => Simulate(report, simulationRuns))
            .OrderBy(result => result.MonteCarloPassed ? 0 : 1)
            .ThenBy(result => result.RuinProbabilityEstimate)
            .ThenByDescending(result => result.PositiveSimulationRatio)
            .ToList();

        var output = new MonteCarloReport(
            ReportId: $"monte_carlo_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            StrategiesEvaluated: results.Count,
            SimulationsPerStrategy: simulationRuns,
            Passed: results.Count(result => result.MonteCarloPassed),
            Failed: results.Count(result => !result.MonteCarloPassed),
            AveragePositiveSimulationRatio: Math.Round(results.Count == 0 ? 0 : results.Average(result => result.PositiveSimulationRatio), 4),
            AverageRuinProbabilityEstimate: Math.Round(results.Count == 0 ? 0 : results.Average(result => result.RuinProbabilityEstimate), 4),
            Results: results,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(output, JsonDefaults.WriteOptions));
        return output;
    }

    public MonteCarloReport? LoadReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MonteCarloReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static MonteCarloResult Simulate(StrategySimulationReport report, int simulationRuns)
    {
        var metrics = report.Metrics;
        var tradeReturns = BuildTradeReturns(report);
        var tradeSampleSize = Math.Clamp(metrics.TradeCount > 0 ? metrics.TradeCount : tradeReturns.Count, 20, 400);
        var scenario = new MonteCarloScenario(
            ScenarioId: "default_monte_carlo_v1",
            SimulationRuns: simulationRuns,
            TradeSampleSize: tradeSampleSize,
            SpreadVariation: 0.35,
            SlippageVariation: 0.22,
            ExecutionDelayProbability: 0.12,
            WorstCaseDrawdownSimulation: true);
        var random = new Random(StableSeed(report.StrategyVariantId));
        var returns = new List<double>(simulationRuns);
        var drawdowns = new List<double>(simulationRuns);
        var ruinEvents = 0;

        for (var simulation = 0; simulation < simulationRuns; simulation++)
        {
            var equity = 0.0;
            var peak = 0.0;
            var worstDrawdown = 0.0;
            var useWorstCaseOrdering = scenario.WorstCaseDrawdownSimulation && simulation % 10 == 0;
            var ordered = useWorstCaseOrdering
                ? tradeReturns.OrderBy(value => value).ToList()
                : tradeReturns.OrderBy(_ => random.Next()).ToList();

            for (var index = 0; index < tradeSampleSize; index++)
            {
                var baseReturn = ordered[index % ordered.Count];
                var spreadPenalty = Math.Abs(baseReturn) * metrics.CostSensitivity * random.NextDouble() * scenario.SpreadVariation;
                var slippagePenalty = metrics.CostSensitivity * random.NextDouble() * scenario.SlippageVariation;
                var delayPenalty = random.NextDouble() < scenario.ExecutionDelayProbability ? 0.04 + random.NextDouble() * 0.08 : 0;
                equity += baseReturn - spreadPenalty - slippagePenalty - delayPenalty;
                peak = Math.Max(peak, equity);
                worstDrawdown = Math.Min(worstDrawdown, equity - peak);
            }

            returns.Add(equity);
            drawdowns.Add(worstDrawdown);
            if (worstDrawdown <= -20 || equity <= -12)
            {
                ruinEvents++;
            }
        }

        var positiveRatio = (double)returns.Count(value => value > 0) / Math.Max(1, returns.Count);
        var medianReturn = Median(returns);
        var worstCaseDrawdown = drawdowns.Count == 0 ? 0 : drawdowns.Min();
        var ruinProbability = (double)ruinEvents / Math.Max(1, simulationRuns);
        if (metrics.TooGoodToBeTrue)
        {
            ruinProbability = Math.Clamp(ruinProbability + 0.08, 0, 1);
        }

        var warnings = new List<string>();
        if (tradeReturns.Count < 20)
        {
            warnings.Add("small_trade_return_sample");
        }

        if (metrics.TooGoodToBeTrue)
        {
            warnings.Add("too_good_to_be_true_penalty_applied");
        }

        var passed = positiveRatio >= 0.9
            && worstCaseDrawdown >= -12
            && ruinProbability <= 0.05
            && metrics.RealismScore >= 0.5
            && metrics.TradeCount >= 30
            && !metrics.TooGoodToBeTrue;

        return new MonteCarloResult(
            StrategyVariantId: report.StrategyVariantId,
            StrategyFamily: report.StrategyFamily,
            PatternId: report.PatternId,
            Symbol: QualityGateStrategySelector.Symbol(report),
            Timeframe: QualityGateStrategySelector.Timeframe(report),
            Scenario: scenario,
            SimulationsRun: simulationRuns,
            PositiveSimulationRatio: Math.Round(positiveRatio, 4),
            MedianReturn: Math.Round(medianReturn, 4),
            WorstCaseDrawdown: Math.Round(worstCaseDrawdown, 4),
            RuinProbabilityEstimate: Math.Round(ruinProbability, 4),
            MonteCarloPassed: passed,
            Warnings: warnings);
    }

    private static IReadOnlyList<double> BuildTradeReturns(StrategySimulationReport report)
    {
        if (report.SampleTrades.Count >= 12)
        {
            return report.SampleTrades.Select(trade => trade.NetR).ToList();
        }

        var metrics = report.Metrics;
        var count = Math.Clamp(metrics.TradeCount > 0 ? metrics.TradeCount : 60, 30, 250);
        var winCount = Math.Clamp((int)Math.Round(count * Math.Clamp(metrics.Winrate, 0.01, 0.99)), 1, count - 1);
        var lossCount = Math.Max(1, count - winCount);
        var averageWin = Math.Max(0.15, metrics.Expectancy + 1.0);
        var averageLoss = -Math.Max(0.3, Math.Abs(metrics.Expectancy - (averageWin * metrics.Winrate)) / Math.Max(0.05, 1 - metrics.Winrate));
        var values = Enumerable.Repeat(averageWin, winCount)
            .Concat(Enumerable.Repeat(averageLoss, lossCount))
            .ToList();
        return values;
    }

    private static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var ordered = values.OrderBy(value => value).ToList();
        var middle = ordered.Count / 2;
        return ordered.Count % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2
            : ordered[middle];
    }

    private static int StableSeed(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value)
            {
                hash = hash * 31 + character;
            }

            return hash;
        }
    }
}
