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

    public string SimulationReportsDirectory => Path.Combine(_storagePaths.Root, "reports", "simulation");

    public string LatestStatusPath => Path.Combine(SimulationRoot, "simulation_status.json");

    public string RealismReportPath => Path.Combine(SimulationReportsDirectory, "realism_report.json");

    public IReadOnlyList<StrategySimulationReport> Run()
    {
        Directory.CreateDirectory(ReportsDirectory);
        Directory.CreateDirectory(SimulationReportsDirectory);
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

        var realism = BuildRealismReport(reports);
        File.WriteAllText(RealismReportPath, JsonSerializer.Serialize(realism, JsonDefaults.WriteOptions));
        File.WriteAllText(Path.Combine(SimulationRoot, "realism_report.json"), JsonSerializer.Serialize(realism, JsonDefaults.WriteOptions));
        var status = new
        {
            generatedAtUtc = DateTimeOffset.UtcNow,
            simulationRoot = SimulationRoot,
            simulationReportsRoot = SimulationReportsDirectory,
            strategiesSimulated = reports.Count,
            realismReport = RealismReportPath,
            noAutoTrading = true,
            humanReviewRequired = true,
            brokerReality = BrokerReality().BrokerProfile,
            brokerProfileSource = BrokerRealityProfile().Source
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

    public RealismReport? LoadRealismReport()
    {
        if (!File.Exists(RealismReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<RealismReport>(
                File.ReadAllText(RealismReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private StrategySimulationReport Simulate(
        StrategyResearchResult result,
        IReadOnlyDictionary<string, StrategyPatternDefinition> patterns)
    {
        var broker = BrokerReality();
        var profile = BrokerRealityProfile();
        var symbol = result.SymbolsProcessed.FirstOrDefault() ?? "XAUUSD";
        var timeframe = result.Variant.Timeframe ?? result.TimeframesProcessed.FirstOrDefault() ?? "M5";
        var candles = LoadCandles(symbol, timeframe);
        var lifecycles = new CandleTradeSimulator(BrokerCostModel.FusionMarketsManualDefault)
            .Simulate(result.Variant, candles)
            .ToList();
        var trades = lifecycles.Select(ToSimulationTrade).ToList();
        var metrics = CalculateMetrics(lifecycles, result);
        patterns.TryGetValue(result.Variant.PatternId ?? string.Empty, out var pattern);
        var equityCurve = lifecycles.SelectMany(position => position.EquityCurve).ToList();
        var assumptions = new List<string>
        {
            "variable_spreads",
            "fusion_markets_manual_default",
            "commission_cost",
            "slippage_model",
            "session_liquidity_model",
            "volatile_spread_widening",
            "partial_fill_stub",
            "candle_by_candle_lifecycle",
            "intra_candle_path_conservative_when_ambiguous",
            $"max_concurrent_trades={broker.MaxConcurrentTrades}"
        };
        if (candles.Count == 0)
        {
            assumptions.Add("no_local_candles_found_zero_trade_report");
        }

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
            RealityAdjustments: assumptions,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerRealityProfile: profile,
            CostModel: new SimulationCostModel(
                ModelVersion: "simulation_cost_model_v1",
                SpreadCostR: Math.Round(trades.Sum(trade => trade.SpreadCostR), 4),
                CommissionR: Math.Round(trades.Sum(trade => trade.CommissionR), 4),
                SlippageR: Math.Round(trades.Sum(trade => trade.SlippageR), 4),
                SessionLiquidityPenaltyR: Math.Round(lifecycles.Count(position => position.ExecutionModel.Session == "off_session") * 0.012, 4),
                SpreadWideningPenaltyR: Math.Round(lifecycles.Count(position => position.ExitReason.Contains("ambiguous", StringComparison.OrdinalIgnoreCase)) * 0.02, 4),
                EstimatedCostR: Math.Round(trades.Sum(trade => trade.SpreadCostR + trade.CommissionR + trade.SlippageR), 4)),
            TradeSimulation: new RealisticTradeSimulation(
                SimulationVersion: "realistic_trade_simulation_v1",
                ExecutionModel: "candle_by_candle_sl_tp_cost_model",
                CandleByCandle: true,
                PartialFillsStubbed: true,
                MaxConcurrentTrades: broker.MaxConcurrentTrades,
                Assumptions:
                [
                    "entry_execution_on_candle_close",
                    "sl_tp_execution_from_candle_path_stub",
                    "variable_spread_costs",
                    "commission_and_slippage_subtracted",
                    "no_broker_orders"
                ]),
            PositionLifecycles: lifecycles.Take(40).ToList(),
            EquityCurve: equityCurve.TakeLast(200).ToList());
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

    private static BrokerRealityProfile BrokerRealityProfile() =>
        new(
            ProfileId: "fusion_markets_manual_default_v1",
            BrokerName: "Fusion Markets",
            Source: "manual_default",
            AccountType: "conservative_research_default",
            TypicalSpreadPoints: new Dictionary<string, double>
            {
                ["EURUSD"] = 0.2,
                ["XAUUSD"] = 1.2,
                ["GER40"] = 1.4,
                ["US500"] = 0.6
            },
            TickSize: new Dictionary<string, double>
            {
                ["EURUSD"] = 0.00001,
                ["XAUUSD"] = 0.01,
                ["GER40"] = 0.1,
                ["US500"] = 0.1
            },
            PipSize: new Dictionary<string, double>
            {
                ["EURUSD"] = 0.0001,
                ["XAUUSD"] = 0.1,
                ["GER40"] = 1,
                ["US500"] = 1
            },
            CommissionR: 0.025,
            BaseSlippageR: 0.015,
            MaxConcurrentTrades: 2);

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

    private IReadOnlyList<MarketDataCandle> LoadCandles(string symbol, string timeframe)
    {
        var directory = Path.Combine(_storagePaths.Root, "market_data", "candles", symbol, timeframe);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var files = Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(3)
            .ToList();
        var candles = new Dictionary<DateTimeOffset, MarketDataCandle>();
        foreach (var file in files)
        {
            foreach (var line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var candle = JsonSerializer.Deserialize<MarketDataCandle>(line, JsonDefaults.SnapshotReadOptions);
                    if (candle is not null)
                    {
                        candles[candle.TimestampUtc] = candle;
                    }
                }
                catch (JsonException)
                {
                    continue;
                }
            }
        }

        return candles.Values
            .OrderBy(candle => candle.TimestampUtc)
            .TakeLast(2400)
            .ToList();
    }

    private static SimulationTrade ToSimulationTrade(PositionLifecycle position)
    {
        var commission = BrokerCostModel.FusionMarketsManualDefault.CommissionR;
        return new SimulationTrade(
            TradeId: $"sim_trade_{position.PositionId}",
            StrategyVariantId: position.StrategyVariantId,
            Symbol: position.Symbol,
            Timeframe: position.Timeframe,
            OpenedAtUtc: position.OpenedAtUtc,
            ClosedAtUtc: position.ClosedAtUtc,
            GrossR: position.GrossR,
            SpreadCostR: Math.Max(0, position.FeesR - commission),
            CommissionR: commission,
            SlippageR: position.SlippageR,
            NetR: position.NetR,
            ExitReason: position.ExitReason,
            PartialFillSimulated: position.ExecutionModel.IntraCandlePathApproximated);
    }

    private RealismReport BuildRealismReport(IReadOnlyList<StrategySimulationReport> reports)
    {
        var orderedRealistic = reports
            .OrderBy(report => report.Metrics.RealismPenalty)
            .ThenByDescending(report => report.Metrics.RobustnessConfidence)
            .Take(25)
            .Select(report => $"{report.StrategyFamily}/{report.PatternId ?? "-"}:{report.StrategyVariantId}:realism_penalty={report.Metrics.RealismPenalty:0.####},robustness={report.Metrics.RobustnessConfidence:0.####},trades={report.Metrics.TradeCount}")
            .ToList();
        var suspicious = reports
            .Where(report => report.Metrics.OverfitRisk >= 0.65 || report.Metrics.RealismPenalty >= 0.45)
            .OrderByDescending(report => report.Metrics.OverfitRisk)
            .ThenByDescending(report => report.Metrics.RealismPenalty)
            .Take(50)
            .Select(report => $"{report.StrategyFamily}/{report.PatternId ?? "-"}:{report.StrategyVariantId}:overfit_risk={report.Metrics.OverfitRisk:0.####},realism_penalty={report.Metrics.RealismPenalty:0.####},winrate={report.Metrics.Winrate:P1}")
            .ToList();

        return new RealismReport(
            ReportId: $"realism_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            StrategiesEvaluated: reports.Count,
            RealisticStrategies: reports.Count(report => report.Metrics.RealismPenalty < 0.28 && report.Metrics.SampleQuality >= 0.45),
            SuspiciousStrategies: suspicious.Count,
            MostRealisticStrategies: orderedRealistic,
            SuspiciousStrategiesList: suspicious,
            AverageRealismPenalty: Math.Round(reports.Count == 0 ? 0 : reports.Average(report => report.Metrics.RealismPenalty), 4),
            AverageOverfitRisk: Math.Round(reports.Count == 0 ? 0 : reports.Average(report => report.Metrics.OverfitRisk), 4),
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    private static SimulationPerformanceMetrics CalculateMetrics(
        IReadOnlyList<PositionLifecycle> positions,
        StrategyResearchResult sourceResult)
    {
        if (positions.Count == 0)
        {
            return new SimulationPerformanceMetrics(
                NetR: 0,
                SharpeRatio: 0,
                ProfitFactor: 0,
                MaxDrawdown: 0,
                Expectancy: 0,
                ConsecutiveLosses: 0,
                StabilityScore: 0,
                Winrate: 0,
                TradeCount: 0,
                RealismPenalty: 0.85,
                RobustnessConfidence: 0,
                ParameterStability: ParameterStability(sourceResult.Variant),
                SampleQuality: 0,
                OverfitRisk: 0.85);
        }

        var returns = positions.Select(position => position.NetR).ToList();
        var grossReturns = positions.Select(position => position.GrossR).ToList();
        var wins = returns.Where(value => value > 0).Sum();
        var losses = Math.Abs(returns.Where(value => value <= 0).Sum());
        var average = returns.Average();
        var std = StandardDeviation(returns);
        var maxDrawdown = MaxDrawdown(returns);
        var consecutiveLosses = ConsecutiveLosses(returns);
        var profitFactor = losses == 0 ? wins : wins / losses;
        var sharpe = std == 0 ? 0 : average / std * Math.Sqrt(Math.Min(252, returns.Count));
        var winrate = returns.Count(value => value > 0) / (double)returns.Count;
        var grossProfit = grossReturns.Where(value => value > 0).Sum();
        var gross = grossReturns.Sum();
        var fees = positions.Sum(position => position.FeesR);
        var slippage = positions.Sum(position => position.SlippageR);
        var estimatedCost = fees + slippage;
        var sampleQuality = Math.Clamp(Math.Log10(positions.Count + 1) / Math.Log10(300), 0, 1);
        var parameterStability = ParameterStability(sourceResult.Variant);
        var smoothnessPenalty = maxDrawdown >= -0.01 && profitFactor > 8 ? 0.28 : 0;
        var winratePenalty = winrate > 0.92 ? (winrate - 0.92) * 2.2 : 0;
        var samplePenalty = sampleQuality < 0.5 ? (0.5 - sampleQuality) * 0.75 : 0;
        var costPenalty = estimatedCost / Math.Max(1, Math.Abs(grossProfit)) * 0.35;
        var parameterPenalty = (1 - parameterStability) * 0.2;
        var realismPenalty = Math.Clamp(winratePenalty + smoothnessPenalty + samplePenalty + costPenalty + parameterPenalty, 0, 1);
        var stability = Math.Clamp((Math.Min(3, profitFactor) / 3.0 * 0.35)
            + Math.Max(0, 1 - Math.Abs(maxDrawdown) / 15.0) * 0.28
            + sampleQuality * 0.2
            + parameterStability * 0.17
            - consecutiveLosses * 0.025,
            0,
            1);
        var overfitRisk = Math.Clamp(realismPenalty
            + (consecutiveLosses <= 1 && positions.Count >= 50 ? 0.18 : 0)
            + (winrate >= 0.98 ? 0.25 : 0)
            + (Math.Abs(sourceResult.Fitness.Score - stability) > 0.45 ? 0.16 : 0),
            0,
            1);
        var robustness = Math.Clamp(stability - realismPenalty * 0.55 - overfitRisk * 0.25, 0, 1);

        return new SimulationPerformanceMetrics(
            NetR: Math.Round(returns.Sum(), 4),
            SharpeRatio: Math.Round(sharpe, 4),
            ProfitFactor: Math.Round(profitFactor, 4),
            MaxDrawdown: Math.Round(maxDrawdown, 4),
            Expectancy: Math.Round(average, 4),
            ConsecutiveLosses: consecutiveLosses,
            StabilityScore: Math.Round(stability, 4),
            Winrate: Math.Round(winrate, 4),
            TradeCount: positions.Count,
            GrossProfitR: Math.Round(grossProfit, 4),
            EstimatedCostR: Math.Round(estimatedCost, 4),
            RobustnessScore: Math.Round(robustness, 4),
            GrossR: Math.Round(gross, 4),
            FeesR: Math.Round(fees, 4),
            SlippageR: Math.Round(slippage, 4),
            RealismPenalty: Math.Round(realismPenalty, 4),
            RobustnessConfidence: Math.Round(robustness, 4),
            ParameterStability: Math.Round(parameterStability, 4),
            SampleQuality: Math.Round(sampleQuality, 4),
            OverfitRisk: Math.Round(overfitRisk, 4));
    }

    private static double ParameterStability(StrategyVariant variant)
    {
        var rrPenalty = Math.Abs(variant.RiskRewardRatio - 1.6) * 0.16;
        var slPenalty = Math.Abs(variant.StopLossAtrMultiplier - 1.2) * 0.18;
        var emaSpread = variant.SlowEma - variant.FastEma;
        var emaPenalty = emaSpread is < 8 or > 48 ? 0.2 : 0;
        var filterBonus = variant.RequireConfirmationCandle ? 0.06 : 0;
        return Math.Clamp(1 - rrPenalty - slPenalty - emaPenalty + filterBonus, 0, 1);
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

        var grossProfit = trades.Where(trade => trade.GrossR > 0).Sum(trade => trade.GrossR);
        var estimatedCost = trades.Sum(trade => trade.SpreadCostR + trade.CommissionR + trade.SlippageR);
        var robustness = Math.Clamp(stability - (estimatedCost / Math.Max(1, grossProfit) * 0.15), 0, 1);

        return new SimulationPerformanceMetrics(
            NetR: Math.Round(returns.Sum(), 4),
            SharpeRatio: Math.Round(sharpe, 4),
            ProfitFactor: Math.Round(profitFactor, 4),
            MaxDrawdown: Math.Round(maxDrawdown, 4),
            Expectancy: Math.Round(average, 4),
            ConsecutiveLosses: consecutiveLosses,
            StabilityScore: Math.Round(stability, 4),
            Winrate: Math.Round(returns.Count(value => value > 0) / (double)returns.Count, 4),
            TradeCount: trades.Count,
            GrossProfitR: Math.Round(grossProfit, 4),
            EstimatedCostR: Math.Round(estimatedCost, 4),
            RobustnessScore: Math.Round(robustness, 4));
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
