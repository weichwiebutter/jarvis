namespace Hermes.Runtime;

public sealed record RiskOfRuinProfile(
    double RiskPerTradePercent,
    double ExpectedDrawdownPercent,
    double LosingStreakRisk,
    double AccountRuinProbabilityEstimate);

public sealed record RiskOfRuinEntry(
    string StrategyVariantId,
    string StrategyFamily,
    string? PatternId,
    string Symbol,
    string Timeframe,
    double ExpectedDrawdown,
    double LosingStreakRisk,
    double AccountRuinProbabilityEstimate,
    double RecommendedMaxRiskPerTrade,
    bool RiskOfRuinPassed,
    IReadOnlyList<RiskOfRuinProfile> Profiles);

public sealed record RiskOfRuinReport(
    string ReportId,
    DateTimeOffset CreatedAtUtc,
    int StrategiesEvaluated,
    int Passed,
    int Failed,
    double AverageRuinProbabilityEstimate,
    double AverageRecommendedMaxRiskPerTrade,
    IReadOnlyList<RiskOfRuinEntry> Entries,
    bool NoAutoTrading,
    bool HumanReviewRequired);
