namespace Hermes.Runtime;

public sealed record StrategySimulationReport(
    string SimulationId,
    DateTimeOffset CreatedAtUtc,
    string StrategyVariantId,
    string StrategyFamily,
    string? PatternId,
    string? SourceName,
    string? SourceUrl,
    BrokerRealitySettings BrokerReality,
    SimulationPerformanceMetrics Metrics,
    IReadOnlyList<SimulationTrade> SampleTrades,
    IReadOnlyList<string> RealityAdjustments,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    BrokerRealityProfile? BrokerRealityProfile = null,
    SimulationCostModel? CostModel = null,
    RealisticTradeSimulation? TradeSimulation = null,
    IReadOnlyList<PositionLifecycle>? PositionLifecycles = null,
    IReadOnlyList<double>? EquityCurve = null);
