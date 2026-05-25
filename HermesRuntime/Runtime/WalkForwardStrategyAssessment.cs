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
    bool HighRisk,
    double TrainPerformance = 0,
    double ValidationPerformance = 0,
    double DegradationScore = 0,
    double RobustnessGap = 0,
    double RealismPenalty = 0,
    double RobustnessConfidence = 0,
    double ParameterStability = 0,
    double SampleQuality = 0,
    double OverfitRisk = 0);
