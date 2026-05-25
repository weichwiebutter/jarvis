namespace Hermes.Runtime;

public sealed record PositionLifecycle(
    string PositionId,
    string StrategyVariantId,
    string Symbol,
    string Timeframe,
    string Direction,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset ClosedAtUtc,
    double EntryPrice,
    double StopLoss,
    double TakeProfit,
    string ExitReason,
    double GrossR,
    double FeesR,
    double SlippageR,
    double NetR,
    IReadOnlyList<double> EquityCurve,
    TradeExecutionModel ExecutionModel);
