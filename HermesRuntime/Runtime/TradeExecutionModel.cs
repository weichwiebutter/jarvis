namespace Hermes.Runtime;

public sealed record TradeExecutionModel(
    string ModelVersion,
    double EntryPrice,
    double StopLoss,
    double TakeProfit,
    string Direction,
    string Session,
    int MaxConcurrentTrades,
    bool EntryOnCandleClose,
    bool IntraCandlePathApproximated);
