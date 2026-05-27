namespace Hermes.Runtime;

public sealed record CostSensitivityEntry(
    string StrategyVariantId,
    string StrategyFamily,
    string? PatternId,
    double NormalCostScore,
    double HighCostScore,
    double StressCostScore,
    double CostSensitivity,
    string Status,
    bool WorksOnlyWithoutCosts,
    bool TooGoodToBeTrue,
    int TradeCount = 0);

public sealed record CostSensitivityReport(
    string ReportId,
    DateTimeOffset CreatedAtUtc,
    int StrategiesEvaluated,
    int CostSensitiveStrategies,
    int StressCostFailures,
    double AverageCostSensitivity,
    IReadOnlyList<CostSensitivityEntry> Entries,
    bool NoAutoTrading,
    bool HumanReviewRequired);
