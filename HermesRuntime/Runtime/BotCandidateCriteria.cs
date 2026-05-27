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
    bool TooGoodToBeTruePassed,
    bool MonteCarloPassed = false,
    double PositiveSimulationRatio = 0,
    bool PositiveSimulationRatioPassed = false,
    bool SurvivesSpreadX2 = false,
    bool SurvivesStressCost = false,
    bool CostStressPassed = false,
    double RiskOfRuinProbabilityEstimate = 1,
    bool RiskOfRuinPassed = false,
    double RecommendedMaxRiskPerTrade = 0,
    bool RecommendedRiskAvailable = false)
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
        && TooGoodToBeTruePassed
        && MonteCarloPassed
        && PositiveSimulationRatioPassed
        && SurvivesSpreadX2
        && CostStressPassed
        && RiskOfRuinPassed
        && RecommendedRiskAvailable;
}
