namespace Hermes.Runtime;

public sealed record BacktestJobRequest(
    string Symbol,
    string Timeframe,
    string Period,
    string StrategyName);
