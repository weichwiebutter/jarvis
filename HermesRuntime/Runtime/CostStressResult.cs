namespace Hermes.Runtime;

public sealed record CostStressResult(
    string StrategyVariantId,
    string StrategyFamily,
    string? PatternId,
    string Symbol,
    string Timeframe,
    bool SurvivesNormalCost,
    bool SurvivesSpreadX2,
    bool SurvivesSpreadX3,
    bool SurvivesStressCost,
    string CostFailureReason,
    IReadOnlyList<CostStressScenarioResult> ScenarioResults);

public sealed record CostStressReport(
    string ReportId,
    DateTimeOffset CreatedAtUtc,
    int StrategiesEvaluated,
    int SurvivesNormalCost,
    int SurvivesSpreadX2,
    int SurvivesSpreadX3,
    int SurvivesStressCost,
    int StressCostFailures,
    IReadOnlyList<CostStressResult> Results,
    bool NoAutoTrading,
    bool HumanReviewRequired);
