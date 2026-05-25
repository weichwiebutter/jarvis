namespace Hermes.Runtime;

public sealed record WalkForwardStrategyAssessment(
    string StrategyVariantId,
    string StrategyFamily,
    string? PatternId,
    double TrainScore,
    double ValidationScore,
    double OutOfSampleScore,
    string StrategyConfidence,
    IReadOnlyList<string> OverfitFlags,
    bool Robust,
    bool HighRisk);
