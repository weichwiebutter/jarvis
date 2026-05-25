using System.Text.Json;

namespace Hermes.Runtime;

public sealed class RealisticSimulationService
{
    private readonly StoragePaths _storagePaths;

    public RealisticSimulationService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string SimulationRoot => Path.Combine(_storagePaths.Root, "simulation");

    public string ReportsDirectory => Path.Combine(SimulationRoot, "reports");

    public string LatestStatusPath => Path.Combine(SimulationRoot, "simulation_status.json");

    public IReadOnlyList<StrategySimulationReport> Run()
    {
        Directory.CreateDirectory(ReportsDirectory);
        var patterns = new StrategyPatternCatalog(_storagePaths).LoadOrCreateCatalog()
            .ToDictionary(pattern => pattern.Id, pattern => pattern, StringComparer.OrdinalIgnoreCase);
        var results = LoadStrategyResults()
            .Where(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(result => result.CompletedAtUtc)
            .Take(256)
            .ToList();

        var reports = results.Select(result => Simulate(result, patterns)).ToList();
        foreach (var report in reports)
        {
            var path = Path.Combine(ReportsDirectory, $"{report.SimulationId}.simulation_report.json");
            File.WriteAllText(path, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        }

        var status = new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            simulationRoot = SimulationRoot,
            strategiesSimulated = reports.Count,
            noAutoTrading = true,
            humanReviewRequired = true,
            brokerReality = BrokerReality().BrokerProfile
        };
        File.WriteAllText(LatestStatusPath, JsonSerializer.Serialize(status, JsonDefaults.WriteOptions));
        return reports;
    }

    public IReadOnlyList<StrategySimulationReport> LoadReports()
    {
        if (!Directory.Exists(ReportsDirectory))
        {
            return [];
        }

        return Directory.EnumerateFiles(ReportsDirectory, "*.simulation_report.json", SearchOption.TopDirectoryOnly)
            .OrderBy(File.GetLastWriteTimeUtc)
            .Select(ReadReport)
            .Where(report => report is not null)
            .Select(report => report!)
            .ToList();
    }

    private StrategySimulationReport Simulate(
        StrategyResearchResult result,
        IReadOnlyDictionary<string, StrategyPatternDefinition> patterns)
    {
        var broker = BrokerReality();
        var symbol = result.SymbolsProcessed.FirstOrDefault() ?? "XAUUSD";
        var timeframe = result.Variant.Timeframe ?? result.TimeframesProcessed.FirstOrDefault() ?? "M5";
        var sampleCount = Math.Clamp(result.TradeCount <= 0 ? 20 : result.TradeCount / 250, 20, 500);
        var trades = new List<SimulationTrade>();
        var winsTarget = (int)Math.Round(sampleCount * result.Fitness.Winrate);
        var interval = timeframe switch
        {
            "H1" => TimeSpan.FromHours(1),
            "M15" => TimeSpan.FromMinutes(15),
            _ => TimeSpan.FromMinutes(5)
        };
        var start = result.FromUtc ?? result.StartedAtUtc.AddDays(-sampleCount);
        var winR = Math.Max(0.2, result.Variant.RiskRewardRatio * 0.82);
        var lossR = -1.0;

        for (var index = 0; index < sampleCount; index++)
        {
            var volatileStep = index % 11 == 0;
            var sessionPenalty = result.Variant.SessionFilter is "london" or "new_york" ? 0.005 : 0.015;
            var spreadCost = SpreadCostR(broker, symbol, timeframe, volatileStep) + sessionPenalty;
            var slippage = broker.BaseSlippageR + (volatileStep ? 0.035 : 0.008);
            var gross = index < winsTarget ? winR : lossR;
            if (index % 17 == 0)
            {
                gross *= 0.5;
            }

            var net = Math.Round(gross - spreadCost - broker.CommissionR - slippage, 4);
            trades.Add(new SimulationTrade(
                TradeId: $"sim_trade_{result.Variant.VariantId}_{index:0000}",
                StrategyVariantId: result.Variant.VariantId,
                Symbol: symbol,
                Timeframe: timeframe,
                OpenedAtUtc: start.Add(interval * index),
                ClosedAtUtc: start.Add(interval * (index + 1)),
                GrossR: Math.Round(gross, 4),
                SpreadCostR: Math.Round(spreadCost, 4),
                CommissionR: broker.CommissionR,
                SlippageR: Math.Round(slippage, 4),
                NetR: net,
                ExitReason: net > 0 ? "tp_or_partial_tp" : "sl_or_cost_adjusted_loss",
                PartialFillSimulated: volatileStep));
        }

        var metrics = CalculateMetrics(trades);
        patterns.TryGetValue(result.Variant.PatternId ?? string.Empty, out var pattern);
        return new StrategySimulationReport(
            SimulationId: $"simulation_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{result.Variant.VariantId}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            StrategyVariantId: result.Variant.VariantId,
            StrategyFamily: result.Variant.Family,
            PatternId: result.Variant.PatternId,
            SourceName: pattern?.SourceName,
            SourceUrl: pattern?.SourceUrl,
            BrokerReality: broker,
            Metrics: metrics,
            SampleTrades: trades.Take(40).ToList(),
            RealityAdjustments:
            [
                "variable_spreads",
                "fusion_markets_profile_stub",
                "commission_cost",
                "slippage_model",
                "session_liquidity_model",
                "volatile_spread_widening",
                "partial_fill_stub",
                $"max_concurrent_trades={broker.MaxConcurrentTrades}"
            ],
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    private IEnumerable<StrategyResearchResult> LoadStrategyResults()
    {
        var directory = Path.Combine(_storagePaths.Root, "strategy_research", "results");
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.strategy_result.json", SearchOption.TopDirectoryOnly))
        {
            StrategyResearchResult? result;
            try
            {
                result = JsonSerializer.Deserialize<StrategyResearchResult>(
                    File.ReadAllText(path),
                    JsonDefaults.SnapshotReadOptions);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                continue;
            }

            if (result is not null)
            {
                yield return result;
            }
        }
    }

    private static StrategySimulationReport? ReadReport(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<StrategySimulationReport>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static BrokerRealitySettings BrokerReality() =>
        new(
            BrokerProfile: "fusion_markets_reality_stub_v1",
            CommissionR: 0.025,
            BaseSlippageR: 0.015,
            MaxConcurrentTrades: 2,
            TypicalSpreadPoints: new Dictionary<string, double>
            {
                ["EURUSD"] = 0.2,
                ["XAUUSD"] = 1.2,
                ["GER40"] = 1.4,
                ["US500"] = 0.6
            },
            VolatileSpreadMultiplier: new Dictionary<string, double>
            {
                ["EURUSD"] = 2.5,
                ["XAUUSD"] = 3.2,
                ["GER40"] = 3.0,
                ["US500"] = 2.8
            });

    private static double SpreadCostR(
        BrokerRealitySettings broker,
        string symbol,
        string timeframe,
        bool volatileStep)
    {
        var spread = broker.TypicalSpreadPoints.TryGetValue(symbol, out var points) ? points : 1.0;
        var multiplier = volatileStep && broker.VolatileSpreadMultiplier.TryGetValue(symbol, out var value) ? value : 1.0;
        var timeframeFactor = timeframe == "M5" ? 0.018 : timeframe == "M15" ? 0.012 : 0.007;
        return spread * multiplier * timeframeFactor;
    }

    private static SimulationPerformanceMetrics CalculateMetrics(IReadOnlyList<SimulationTrade> trades)
    {
        if (trades.Count == 0)
        {
            return new SimulationPerformanceMetrics(0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        var returns = trades.Select(trade => trade.NetR).ToList();
        var wins = returns.Where(value => value > 0).Sum();
        var losses = Math.Abs(returns.Where(value => value <= 0).Sum());
        var average = returns.Average();
        var std = StandardDeviation(returns);
        var maxDrawdown = MaxDrawdown(returns);
        var consecutiveLosses = ConsecutiveLosses(returns);
        var profitFactor = losses == 0 ? wins : wins / losses;
        var sharpe = std == 0 ? 0 : average / std * Math.Sqrt(Math.Min(252, returns.Count));
        var stability = Math.Clamp((profitFactor / 3.0) + Math.Max(0, 1 - Math.Abs(maxDrawdown) / 25.0) - (consecutiveLosses * 0.03), 0, 1);

        return new SimulationPerformanceMetrics(
            NetR: Math.Round(returns.Sum(), 4),
            SharpeRatio: Math.Round(sharpe, 4),
            ProfitFactor: Math.Round(profitFactor, 4),
            MaxDrawdown: Math.Round(maxDrawdown, 4),
            Expectancy: Math.Round(average, 4),
            ConsecutiveLosses: consecutiveLosses,
            StabilityScore: Math.Round(stability, 4),
            Winrate: Math.Round(returns.Count(value => value > 0) / (double)returns.Count, 4),
            TradeCount: trades.Count);
    }

    private static double MaxDrawdown(IReadOnlyList<double> returns)
    {
        var equity = 0.0;
        var peak = 0.0;
        var drawdown = 0.0;
        foreach (var value in returns)
        {
            equity += value;
            peak = Math.Max(peak, equity);
            drawdown = Math.Min(drawdown, equity - peak);
        }

        return drawdown;
    }

    private static int ConsecutiveLosses(IReadOnlyList<double> returns)
    {
        var current = 0;
        var worst = 0;
        foreach (var value in returns)
        {
            current = value <= 0 ? current + 1 : 0;
            worst = Math.Max(worst, current);
        }

        return worst;
    }

    private static double StandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var avg = values.Average();
        return Math.Sqrt(values.Average(value => Math.Pow(value - avg, 2)));
    }
}
