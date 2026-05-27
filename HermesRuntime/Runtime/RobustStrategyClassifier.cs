namespace Hermes.Runtime;

public static class RobustStrategyClassifier
{
    public static string Classify(
        SimulationPerformanceMetrics metrics,
        double validationScore,
        double outOfSampleScore,
        IReadOnlyList<string> overfitFlags,
        bool highRisk,
        bool oosAvailable = false,
        double walkForwardConfidence = 0,
        double regimeConsistencyScore = 0,
        double regimeSampleQuality = 0)
    {
        if (metrics.TooGoodToBeTrue
            || overfitFlags.Contains("too_good_to_be_true", StringComparer.Ordinal)
            || overfitFlags.Contains("suspicious_winrate", StringComparer.Ordinal)
            || overfitFlags.Contains("too_few_losses", StringComparer.Ordinal)
            || overfitFlags.Contains("too_smooth_equity_curve", StringComparer.Ordinal))
        {
            return "overfit_suspected";
        }

        if (metrics.RealismScore < 0.45
            || metrics.OverfitRisk >= 0.72
            || metrics.CostSensitivity >= 0.78
            || metrics.LossDistributionQuality < 0.18)
        {
            return "rejected";
        }

        if (overfitFlags.Count > 0)
        {
            return "overfit_suspected";
        }

        if (highRisk || walkForwardConfidence < 0.28)
        {
            return "unstable";
        }

        if (metrics.SampleQuality < 0.45
            || metrics.RobustnessConfidence < 0.32
            || !oosAvailable
            || regimeSampleQuality < 0.35)
        {
            return "rejected";
        }

        if (validationScore >= 0.72
            && outOfSampleScore >= 0.62
            && metrics.StabilityScore >= 0.62
            && metrics.RobustnessConfidence >= 0.62
            && metrics.RealismScore >= 0.68
            && metrics.RealismPenalty < 0.32
            && metrics.OverfitRisk < 0.38
            && metrics.CostSensitivity < 0.52
            && metrics.LossDistributionQuality >= 0.45
            && oosAvailable
            && walkForwardConfidence >= 0.62
            && regimeConsistencyScore >= 0.52
            && regimeSampleQuality >= 0.5)
        {
            return "robust";
        }

        if (validationScore >= 0.52
            && outOfSampleScore >= 0.42
            && metrics.RealismScore >= 0.55
            && metrics.RealismPenalty < 0.5
            && metrics.CostSensitivity < 0.7
            && metrics.SampleQuality >= 0.5)
        {
            return "promising";
        }

        if (metrics.TradeCount < 30 || validationScore < 0.35)
        {
            return "rejected";
        }

        return "experimental";
    }
}
