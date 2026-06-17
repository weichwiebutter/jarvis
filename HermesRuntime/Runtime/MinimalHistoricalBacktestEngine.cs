using System.Text.Json;

namespace Hermes.Runtime;

public sealed class MinimalHistoricalBacktestEngine : IStrategyBacktestEngine
{
    private static readonly string[] SupportedAssets = ["XAUUSD"];
    private static readonly string[] SupportedTimeframes = ["M5"];
    private const string SupportedPattern = "Mean Reversion Rejection";
    private const string SupportedParameterFocus = "Bollinger Band Width";

    private readonly StoragePaths _storagePaths;

    public MinimalHistoricalBacktestEngine(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public bool CanExecute(StrategyBacktestRequest request, StrategyBacktestDatasetDescriptor dataset, StrategyBacktestSafetyContext safetyContext)
        => GetSupportIssues(request, dataset, safetyContext).Count == 0;

    public StrategyBacktestResult Execute(StrategyBacktestRequest request, StrategyBacktestDatasetDescriptor dataset, StrategyBacktestSafetyContext safetyContext)
    {
        var executionId = $"backtest_execution_{NormalizeId(request.BacktestJobId)}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
        var supportIssues = GetSupportIssues(request, dataset, safetyContext).ToList();
        if (supportIssues.Count > 0)
        {
            var unsupported = BuildUnsupportedResult(executionId, request.BacktestJobId, supportIssues);
            return unsupported;
        }

        if (!TryLoadCandles(request.Asset, request.Timeframe, out var candles, out var datasetWarnings, out var datasetErrors))
        {
            var errors = datasetErrors.Count > 0 ? datasetErrors : ["dataset_missing"];
            return new StrategyBacktestResult(
                ExecutionId: executionId,
                BacktestJobId: request.BacktestJobId,
                ExecutionSupported: false,
                Status: "ready_to_execute",
                TradesSimulated: null,
                WinRate: null,
                ProfitFactor: null,
                MaxDrawdown: null,
                Expectancy: null,
                RMultipleAvg: null,
                CostSpreadModelUsed: request.CostSpreadModel.Equals("required", StringComparison.OrdinalIgnoreCase),
                Warnings: datasetWarnings.Count > 0 ? datasetWarnings : ["dataset_missing"],
                Errors: errors,
                RequiresHumanReview: false,
                GeneratedAtUtc: DateTimeOffset.UtcNow);
        }

        var result = RunBacktest(executionId, request, candles, datasetWarnings);
        return result;
    }

    private static StrategyBacktestResult BuildUnsupportedResult(string executionId, string backtestJobId, IReadOnlyList<string> issues)
        => new(
            ExecutionId: executionId,
            BacktestJobId: backtestJobId,
            ExecutionSupported: false,
            Status: "ready_to_execute",
            TradesSimulated: null,
            WinRate: null,
            ProfitFactor: null,
            MaxDrawdown: null,
            Expectancy: null,
            RMultipleAvg: null,
            CostSpreadModelUsed: false,
            Warnings: issues.ToList(),
            Errors: issues.ToList(),
            RequiresHumanReview: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow);

    private IReadOnlyList<string> GetSupportIssues(StrategyBacktestRequest request, StrategyBacktestDatasetDescriptor dataset, StrategyBacktestSafetyContext safetyContext)
    {
        var issues = new List<string>();
        if (!SupportedAssets.Contains(request.Asset, StringComparer.OrdinalIgnoreCase) || !request.Asset.Equals(dataset.Asset, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("unsupported_asset_or_timeframe");
        }

        if (!SupportedTimeframes.Contains(request.Timeframe, StringComparer.OrdinalIgnoreCase) || !request.Timeframe.Equals(dataset.Timeframe, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("unsupported_asset_or_timeframe");
        }

        if (!request.StrategyPattern.Equals(SupportedPattern, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("unsupported_strategy_pattern");
        }

        if (!request.ParametersToTest.Any(parameter => parameter.Contains(SupportedParameterFocus, StringComparison.OrdinalIgnoreCase)))
        {
            issues.Add("invalid_parameters");
        }

        if (!dataset.Available)
        {
            issues.Add("dataset_missing");
        }

        if (!safetyContext.NoAutoTrading || safetyContext.BrokerOrdersEnabled || safetyContext.LiveTradingEnabled || !safetyContext.ResearchOnly)
        {
            issues.Add("safety_gate_failed");
        }

        return issues.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private bool TryLoadCandles(
        string asset,
        string timeframe,
        out IReadOnlyList<MarketDataCandle> candles,
        out List<string> warnings,
        out List<string> errors)
    {
        warnings = [];
        errors = [];
        var directory = Path.Combine(_storagePaths.Root, "market_data", "candles", asset.ToUpperInvariant(), timeframe.ToUpperInvariant());
        if (!Directory.Exists(directory))
        {
            errors.Add("dataset_missing");
            candles = [];
            return false;
        }

        var files = Directory.EnumerateFiles(directory, "*.candles.jsonl", SearchOption.TopDirectoryOnly)
            .OrderBy(File.GetLastWriteTimeUtc)
            .ToList();
        if (files.Count == 0)
        {
            errors.Add("dataset_missing");
            candles = [];
            return false;
        }

        var candleMap = new Dictionary<DateTimeOffset, MarketDataCandle>();
        var invalidRows = 0;
        foreach (var file in files)
        {
            foreach (var line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                MarketDataCandle? candle;
                try
                {
                    candle = JsonSerializer.Deserialize<MarketDataCandle>(line, JsonDefaults.SnapshotReadOptions);
                }
                catch (JsonException)
                {
                    invalidRows++;
                    continue;
                }

                if (candle is null || candle.High < candle.Low || candle.Open <= 0 || candle.High <= 0 || candle.Low <= 0 || candle.Close <= 0)
                {
                    invalidRows++;
                    continue;
                }

                candleMap[candle.TimestampUtc] = candle;
            }
        }

        if (candleMap.Count == 0)
        {
            errors.Add("dataset_missing");
            candles = [];
            return false;
        }

        if (invalidRows > 0)
        {
            warnings.Add("dataset_rows_filtered");
        }

        candles = candleMap.Values.OrderBy(candle => candle.TimestampUtc).ToList();
        return true;
    }

    private static StrategyBacktestResult RunBacktest(
        string executionId,
        StrategyBacktestRequest request,
        IReadOnlyList<MarketDataCandle> candles,
        IReadOnlyList<string> datasetWarnings)
    {
        const int period = 20;
        const double deviation = 2.0;
        var runs = Math.Max(1, request.MaxRuns);
        var trades = new List<TradeOutcome>();
        var equityCurve = new List<double>();
        var equity = 0.0;

        for (var index = period; index < candles.Count && trades.Count < runs; index++)
        {
            var window = candles.Skip(index - period).Take(period).Select(candle => candle.Close).ToArray();
            var mean = window.Average();
            var variance = window.Select(value => Math.Pow(value - mean, 2)).Average();
            var stdDev = Math.Sqrt(variance);
            if (stdDev <= 0)
            {
                continue;
            }

            var upper = mean + deviation * stdDev;
            var lower = mean - deviation * stdDev;
            var current = candles[index];
            var previous = candles[index - 1];
            var bandWidth = upper - lower;
            var longSignal = current.Low <= lower && current.Close > lower && current.Close >= previous.Close;
            var shortSignal = current.High >= upper && current.Close < upper && current.Close <= previous.Close;
            if (!longSignal && !shortSignal)
            {
                continue;
            }

            var direction = longSignal ? "long" : "short";
            var entry = current.Close;
            var stopDistance = Math.Max(bandWidth * 0.5, 0.4);
            var stop = direction == "long" ? entry - stopDistance : entry + stopDistance;
            var target = direction == "long" ? entry + stopDistance : entry - stopDistance;

            var result = ResolveTradeOutcome(candles, index, direction, entry, stop, target, equity);
            trades.Add(result);
            equity = result.EquityAfterTrade;
            equityCurve.Add(equity);
        }

        if (trades.Count == 0)
        {
            return new StrategyBacktestResult(
                ExecutionId: executionId,
                BacktestJobId: request.BacktestJobId,
                ExecutionSupported: true,
                Status: "completed_no_trades",
                TradesSimulated: 0,
                WinRate: null,
                ProfitFactor: null,
                MaxDrawdown: null,
                Expectancy: null,
                RMultipleAvg: null,
                CostSpreadModelUsed: request.CostSpreadModel.Equals("required", StringComparison.OrdinalIgnoreCase),
                Warnings: datasetWarnings.Count > 0 ? datasetWarnings.Concat(["no_trades_generated"]).Distinct(StringComparer.OrdinalIgnoreCase).ToList() : ["no_trades_generated"],
                Errors: ["no_trades_generated"],
                RequiresHumanReview: false,
                GeneratedAtUtc: DateTimeOffset.UtcNow);
        }

        var wins = trades.Count(trade => trade.NetR > 0);
        var losses = trades.Count(trade => trade.NetR < 0);
        var grossProfit = trades.Where(trade => trade.NetR > 0).Sum(trade => trade.NetR);
        var grossLoss = Math.Abs(trades.Where(trade => trade.NetR < 0).Sum(trade => trade.NetR));
        var totalNet = trades.Sum(trade => trade.NetR);
        var expectancy = totalNet / trades.Count;
        var rMultipleAvg = trades.Average(trade => trade.NetR);
        var maxDrawdown = CalculateMaxDrawdown(equityCurve);
        var profitFactor = grossLoss <= 0 ? grossProfit : grossProfit / grossLoss;

        return new StrategyBacktestResult(
            ExecutionId: executionId,
            BacktestJobId: request.BacktestJobId,
            ExecutionSupported: true,
            Status: "completed",
            TradesSimulated: trades.Count,
            WinRate: Math.Round((double)wins / trades.Count, 4),
            ProfitFactor: Math.Round(profitFactor, 4),
            MaxDrawdown: Math.Round(maxDrawdown, 4),
            Expectancy: Math.Round(expectancy, 4),
            RMultipleAvg: Math.Round(rMultipleAvg, 4),
            CostSpreadModelUsed: request.CostSpreadModel.Equals("required", StringComparison.OrdinalIgnoreCase),
            Warnings: datasetWarnings,
            Errors: [],
            RequiresHumanReview: false,
            GeneratedAtUtc: DateTimeOffset.UtcNow);
    }

    private static TradeOutcome ResolveTradeOutcome(
        IReadOnlyList<MarketDataCandle> candles,
        int entryIndex,
        string direction,
        double entry,
        double stop,
        double target,
        double equityBeforeTrade)
    {
        var closeIndex = Math.Min(entryIndex + 12, candles.Count - 1);
        var grossR = 0.0;
        var exitReason = "expired";
        var exitTime = candles[closeIndex].TimestampUtc;
        for (var index = entryIndex + 1; index <= closeIndex; index++)
        {
            var candle = candles[index];
            var stopHit = direction == "long" ? candle.Low <= stop : candle.High >= stop;
            var targetHit = direction == "long" ? candle.High >= target : candle.Low <= target;
            if (stopHit && targetHit)
            {
                grossR = -1.0;
                exitReason = "sl_hit_intracandle_ambiguous";
                exitTime = candle.TimestampUtc;
                break;
            }

            if (stopHit)
            {
                grossR = -1.0;
                exitReason = "sl_hit";
                exitTime = candle.TimestampUtc;
                break;
            }

            if (targetHit)
            {
                grossR = 1.0;
                exitReason = "tp_hit";
                exitTime = candle.TimestampUtc;
                break;
            }
        }

        if (exitReason == "expired")
        {
            var exitClose = candles[closeIndex].Close;
            grossR = direction == "long"
                ? (exitClose - entry) / Math.Abs(entry - stop)
                : (entry - exitClose) / Math.Abs(entry - stop);
            grossR = Math.Clamp(grossR, -1.0, 1.0);
        }

        var session = DetermineSession(candles[entryIndex].TimestampUtc);
        var spreadCost = 0.04;
        var commission = 0.02;
        var slippage = session == "london_new_york_overlap" ? 0.02 : 0.04;
        var net = Math.Round(grossR - spreadCost - commission - slippage, 4);
        var equityAfterTrade = Math.Round(equityBeforeTrade + net, 4);
        return new TradeOutcome(net, equityAfterTrade, exitReason, exitTime);
    }

    private static string DetermineSession(DateTimeOffset timestampUtc)
    {
        var hour = timestampUtc.UtcDateTime.Hour;
        if (hour is >= 7 and < 10)
        {
            return "london";
        }

        if (hour is >= 13 and < 17)
        {
            return "london_new_york_overlap";
        }

        if (hour is >= 13 and < 21)
        {
            return "new_york";
        }

        return "other";
    }

    private static double CalculateMaxDrawdown(IReadOnlyList<double> equityCurve)
    {
        var peak = double.NegativeInfinity;
        var maxDrawdown = 0.0;
        foreach (var value in equityCurve)
        {
            peak = Math.Max(peak, value);
            if (peak <= 0)
            {
                continue;
            }

            var drawdown = value - peak;
            maxDrawdown = Math.Min(maxDrawdown, drawdown);
        }
        return maxDrawdown;
    }

    private sealed record TradeOutcome(double NetR, double EquityAfterTrade, string ExitReason, DateTimeOffset ExitTimeUtc);

    private static string NormalizeId(string value)
    {
        var normalized = value.ToLowerInvariant().Replace(" ", "_").Replace("/", "_").Replace("-", "_");
        return string.Concat(normalized.Where(ch => char.IsLetterOrDigit(ch) || ch == '_'));
    }
}
