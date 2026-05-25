namespace Hermes.Runtime;

public sealed record WalkForwardValidationReport(
    string ReportId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset TrainFromUtc,
    DateTimeOffset TrainToUtc,
    DateTimeOffset ValidationFromUtc,
    DateTimeOffset ValidationToUtc,
    int StrategiesEvaluated,
    int RobustStrategies,
    int OverfitSuspectedStrategies,
    int HighRiskStrategies,
    IReadOnlyList<WalkForwardStrategyAssessment> Assessments,
    bool NoAutoTrading,
    bool HumanReviewRequired);
