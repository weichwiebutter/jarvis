namespace Hermes.Runtime;

public sealed class CandleTradeSimulator
{
    private readonly BrokerCostModel _costModel;

    public CandleTradeSimulator(BrokerCostModel costModel)
    {
        _costModel = costModel;
    }

    public IReadOnlyList<PositionLifecycle> Simulate(
        StrategyVariant variant,
        IReadOnlyList<MarketDataCandle> candles,
        int maxTrades = 500)
    {
        if (candles.Count < 4)
        {
            return [];
        }

        var ordered = candles
            .OrderBy(candle => candle.TimestampUtc)
            .TakeLast(1600)
            .ToList();
        var positions = new List<PositionLifecycle>();
        var equity = 0.0;
        var openUntil = DateTimeOffset.MinValue;

        for (var index = 2; index < ordered.Count - 2 && positions.Count < maxTrades; index++)
        {
            var current = ordered[index];
            if (current.TimestampUtc < openUntil)
            {
                continue;
            }

            if (!PassesSessionFilter(variant, current.TimestampUtc))
            {
                continue;
            }

            var direction = ResolveDirection(variant, ordered[index - 2], ordered[index - 1], current);
            if (direction == "none")
            {
                continue;
            }

            var atr = AverageRange(ordered, index, 14);
            if (atr <= 0)
            {
                continue;
            }

            if (variant.UseVolatilityFilter && current.High - current.Low < atr * 0.65)
            {
                continue;
            }

            var entry = current.Close;
            var stopDistance = Math.Max(atr * variant.StopLossAtrMultiplier, TickSize(current.Symbol) * 4);
            var stop = direction == "long" ? entry - stopDistance : entry + stopDistance;
            var target = direction == "long"
                ? entry + stopDistance * variant.RiskRewardRatio
                : entry - stopDistance * variant.RiskRewardRatio;

            var closeIndex = Math.Min(index + 12, ordered.Count - 1);
            var exitReason = "expired";
            var grossR = 0.0;
            var closedAt = ordered[closeIndex].TimestampUtc;
            for (var forward = index + 1; forward <= closeIndex; forward++)
            {
                var candle = ordered[forward];
                var stopHit = direction == "long" ? candle.Low <= stop : candle.High >= stop;
                var targetHit = direction == "long" ? candle.High >= target : candle.Low <= target;

                if (stopHit && targetHit)
                {
                    // Conservative path approximation: if both levels are touched, assume the stop first.
                    exitReason = "sl_hit_intracandle_ambiguous";
                    grossR = -1.0;
                    closedAt = candle.TimestampUtc;
                    break;
                }

                if (stopHit)
                {
                    exitReason = "sl_hit";
                    grossR = -1.0;
                    closedAt = candle.TimestampUtc;
                    break;
                }

                if (targetHit)
                {
                    exitReason = "tp_hit";
                    grossR = variant.RiskRewardRatio;
                    closedAt = candle.TimestampUtc;
                    break;
                }
            }

            if (exitReason == "expired")
            {
                var exitClose = ordered[closeIndex].Close;
                grossR = direction == "long"
                    ? (exitClose - entry) / stopDistance
                    : (entry - exitClose) / stopDistance;
                grossR = Math.Clamp(grossR, -1.0, variant.RiskRewardRatio);
            }

            var session = SessionFor(current.TimestampUtc);
            var fees = FeeR(current.Symbol, session);
            var slippage = SlippageR(current.Symbol, session, current.High - current.Low, atr);
            var net = Math.Round(grossR - fees - slippage, 4);
            equity = Math.Round(equity + net, 4);
            positions.Add(new PositionLifecycle(
                PositionId: $"position_{variant.VariantId}_{positions.Count:0000}",
                StrategyVariantId: variant.VariantId,
                Symbol: current.Symbol,
                Timeframe: current.Timeframe,
                Direction: direction,
                OpenedAtUtc: current.TimestampUtc,
                ClosedAtUtc: closedAt,
                EntryPrice: Math.Round(entry, 6),
                StopLoss: Math.Round(stop, 6),
                TakeProfit: Math.Round(target, 6),
                ExitReason: exitReason,
                GrossR: Math.Round(grossR, 4),
                FeesR: fees,
                SlippageR: slippage,
                NetR: net,
                EquityCurve: [equity],
                ExecutionModel: new TradeExecutionModel(
                    ModelVersion: "trade_execution_model_v1",
                    EntryPrice: Math.Round(entry, 6),
                    StopLoss: Math.Round(stop, 6),
                    TakeProfit: Math.Round(target, 6),
                    Direction: direction,
                    Session: session,
                    MaxConcurrentTrades: 2,
                    EntryOnCandleClose: true,
                    IntraCandlePathApproximated: true)));

            openUntil = closedAt;
        }

        return positions;
    }

    private static string ResolveDirection(
        StrategyVariant variant,
        MarketDataCandle previous2,
        MarketDataCandle previous,
        MarketDataCandle current)
    {
        var pattern = variant.PatternId ?? string.Empty;
        if (pattern == "bullish_engulfing" && BullishEngulfing(previous, current))
        {
            return "long";
        }

        if (pattern == "bearish_engulfing" && BearishEngulfing(previous, current))
        {
            return "short";
        }

        if (pattern is "pin_bar" or "hammer" && LowerWickRatio(current) > 0.55 && BodyRatio(current) < 0.35)
        {
            return "long";
        }

        if (pattern == "shooting_star" && UpperWickRatio(current) > 0.55 && BodyRatio(current) < 0.35)
        {
            return "short";
        }

        if (pattern is "inside_bar" or "inside_bar_breakout" && InsideBar(previous, current))
        {
            return current.Close >= previous.Close ? "long" : "short";
        }

        if (pattern is "breakout_continuation" or "support_resistance_breakout" or "triangle_breakout"
            && BreakoutContinuation(previous2, previous, current))
        {
            return current.Close > current.Open ? "long" : "short";
        }

        if (pattern == "liquidity_sweep_reversal" && LiquiditySweepReversal(previous, current, out var reversal))
        {
            return reversal;
        }

        if (variant.Family.Contains("mean_reversion", StringComparison.OrdinalIgnoreCase))
        {
            var range = current.High - current.Low;
            if (range > 0 && current.Close < current.Low + range * 0.28)
            {
                return "long";
            }

            if (range > 0 && current.Close > current.High - range * 0.28)
            {
                return "short";
            }
        }

        if (variant.Family.Contains("trend", StringComparison.OrdinalIgnoreCase)
            || variant.Family.Contains("pullback", StringComparison.OrdinalIgnoreCase))
        {
            return current.Close > previous.Close && previous.Close > previous2.Close
                ? "long"
                : current.Close < previous.Close && previous.Close < previous2.Close
                    ? "short"
                    : "none";
        }

        return "none";
    }

    private double FeeR(string symbol, string session)
    {
        var spread = _costModel.SpreadDefaults.TryGetValue(symbol, out var value) ? value : 1.0;
        var sessionMultiplier = _costModel.SessionVolatilityMultipliers.TryGetValue(session, out var multiplier) ? multiplier : 1.4;
        return Math.Round(_costModel.CommissionR + spread * sessionMultiplier * 0.012, 4);
    }

    private double SlippageR(string symbol, string session, double candleRange, double atr)
    {
        var volatilityMultiplier = atr <= 0 ? 1.0 : Math.Clamp(candleRange / atr, 0.75, 3.0);
        var sessionMultiplier = _costModel.SessionVolatilityMultipliers.TryGetValue(session, out var multiplier) ? multiplier : 1.4;
        var symbolMultiplier = symbol is "XAUUSD" or "GER40" ? 1.35 : 1.0;
        return Math.Round(_costModel.BaseSlippageR * volatilityMultiplier * sessionMultiplier * symbolMultiplier, 4);
    }

    private static bool PassesSessionFilter(StrategyVariant variant, DateTimeOffset timestamp)
    {
        if (string.IsNullOrWhiteSpace(variant.SessionFilter) || variant.SessionFilter == "any")
        {
            return true;
        }

        return SessionFor(timestamp).Equals(variant.SessionFilter, StringComparison.OrdinalIgnoreCase);
    }

    private static string SessionFor(DateTimeOffset timestamp)
    {
        var hour = timestamp.UtcDateTime.Hour;
        return hour switch
        {
            >= 13 and <= 16 => "london_new_york_overlap",
            >= 7 and < 13 => "london",
            > 16 and <= 21 => "new_york",
            _ => "off_session"
        };
    }

    private static double AverageRange(IReadOnlyList<MarketDataCandle> candles, int index, int lookback)
    {
        var start = Math.Max(0, index - lookback);
        var window = candles.Skip(start).Take(index - start + 1).ToList();
        return window.Count == 0 ? 0 : window.Average(candle => candle.High - candle.Low);
    }

    private static bool BullishEngulfing(MarketDataCandle previous, MarketDataCandle current) =>
        previous.Close < previous.Open
        && current.Close > current.Open
        && current.Open <= previous.Close
        && current.Close >= previous.Open
        && BodyRatio(current) >= 0.45;

    private static bool BearishEngulfing(MarketDataCandle previous, MarketDataCandle current) =>
        previous.Close > previous.Open
        && current.Close < current.Open
        && current.Open >= previous.Close
        && current.Close <= previous.Open
        && BodyRatio(current) >= 0.45;

    private static bool InsideBar(MarketDataCandle previous, MarketDataCandle current) =>
        current.High < previous.High && current.Low > previous.Low;

    private static bool BreakoutContinuation(MarketDataCandle previous2, MarketDataCandle previous, MarketDataCandle current)
    {
        var priorHigh = Math.Max(previous2.High, previous.High);
        var priorLow = Math.Min(previous2.Low, previous.Low);
        return current.Close > priorHigh || current.Close < priorLow;
    }

    private static bool LiquiditySweepReversal(MarketDataCandle previous, MarketDataCandle current, out string direction)
    {
        if (current.Low < previous.Low && current.Close > previous.Low && LowerWickRatio(current) > 0.45)
        {
            direction = "long";
            return true;
        }

        if (current.High > previous.High && current.Close < previous.High && UpperWickRatio(current) > 0.45)
        {
            direction = "short";
            return true;
        }

        direction = "none";
        return false;
    }

    private static double BodyRatio(MarketDataCandle candle)
    {
        var range = candle.High - candle.Low;
        return range <= 0 ? 0 : Math.Abs(candle.Close - candle.Open) / range;
    }

    private static double LowerWickRatio(MarketDataCandle candle)
    {
        var range = candle.High - candle.Low;
        return range <= 0 ? 0 : (Math.Min(candle.Open, candle.Close) - candle.Low) / range;
    }

    private static double UpperWickRatio(MarketDataCandle candle)
    {
        var range = candle.High - candle.Low;
        return range <= 0 ? 0 : (candle.High - Math.Max(candle.Open, candle.Close)) / range;
    }

    private static double TickSize(string symbol) =>
        symbol switch
        {
            "EURUSD" => 0.00001,
            "XAUUSD" => 0.01,
            "GER40" or "US500" => 0.1,
            _ => 0.0001
        };
}
