namespace Hermes.Runtime;

public sealed record StrategyVariant(
    string VariantId,
    string Family,
    int FastEma,
    int SlowEma,
    double RiskRewardRatio,
    double StopLossAtrMultiplier,
    bool RequireConfirmationCandle,
    bool UseVolatilityFilter,
    string? PatternId = null,
    string? SessionFilter = null,
    string? Timeframe = null);
