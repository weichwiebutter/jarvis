namespace Hermes.Runtime;

public sealed record RealismReport(
    string ReportId,
    DateTimeOffset CreatedAtUtc,
    int StrategiesEvaluated,
    int RealisticStrategies,
    int SuspiciousStrategies,
    IReadOnlyList<string> MostRealisticStrategies,
    IReadOnlyList<string> SuspiciousStrategiesList,
    double AverageRealismPenalty,
    double AverageOverfitRisk,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    int TooGoodToBeTrueStrategies = 0,
    IReadOnlyList<string>? TooGoodToBeTrueStrategiesList = null,
    IReadOnlyList<string>? CostSensitiveStrategies = null,
    double AverageRealismScore = 0,
    double AverageCostSensitivity = 0,
    double AverageLossDistributionQuality = 0);
