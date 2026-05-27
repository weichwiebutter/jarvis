namespace Hermes.Runtime;

public sealed record BotCandidateCriteria(
    string Confidence,
    bool ConfidenceRobust,
    bool OosAvailable,
    double WalkForwardConfidence,
    bool WalkForwardConfidencePassed,
    double RealismScore,
    bool RealismScorePassed,
    double OverfitRisk,
    bool OverfitRiskPassed,
    double CostSensitivity,
    bool CostSensitivityPassed,
    double RegimeConsistencyScore,
    bool RegimeConsistencyPassed,
    double MaxDrawdown,
    bool MaxDrawdownPassed,
    double ProfitFactor,
    bool ProfitFactorPassed,
    double SampleQuality,
    bool SampleQualityPassed,
    bool TooGoodToBeTrue,
    bool TooGoodToBeTruePassed)
{
    public bool Passed =>
        ConfidenceRobust
        && OosAvailable
        && WalkForwardConfidencePassed
        && RealismScorePassed
        && OverfitRiskPassed
        && CostSensitivityPassed
        && RegimeConsistencyPassed
        && MaxDrawdownPassed
        && ProfitFactorPassed
        && SampleQualityPassed
        && TooGoodToBeTruePassed;
}
