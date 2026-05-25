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
    bool HumanReviewRequired);
