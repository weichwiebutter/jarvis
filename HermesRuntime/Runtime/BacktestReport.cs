namespace Hermes.Runtime;

public sealed record BacktestReport(
    string RunId,
    string Symbol,
    string Timeframe,
    string StrategyName,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    int TradeCount,
    double Winrate,
    double ProfitFactor,
    double MaxDrawdown,
    double Expectancy,
    string Notes,
    bool NoAutoTrading);
