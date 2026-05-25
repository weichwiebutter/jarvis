namespace Hermes.Runtime;

public sealed record SimulationTrade(
    string TradeId,
    string StrategyVariantId,
    string Symbol,
    string Timeframe,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset ClosedAtUtc,
    double GrossR,
    double SpreadCostR,
    double CommissionR,
    double SlippageR,
    double NetR,
    string ExitReason,
    bool PartialFillSimulated);
