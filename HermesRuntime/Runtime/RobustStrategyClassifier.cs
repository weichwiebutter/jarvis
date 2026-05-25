namespace Hermes.Runtime;

public static class RobustStrategyClassifier
{
    public static string Classify(
        SimulationPerformanceMetrics metrics,
        double validationScore,
        double outOfSampleScore,
        IReadOnlyList<string> overfitFlags,
        bool highRisk)
    {
        if (overfitFlags.Count > 0)
        {
            return "overfit_suspected";
        }

        if (highRisk)
        {
            return "unstable";
        }

        if (metrics.SampleQuality < 0.35 || metrics.RobustnessConfidence < 0.25)
        {
            return "rejected";
        }

        if (validationScore >= 0.72
            && outOfSampleScore >= 0.60
            && metrics.StabilityScore >= 0.62
            && metrics.RobustnessConfidence >= 0.55
            && metrics.RealismPenalty < 0.35)
        {
            return "robust";
        }

        if (validationScore >= 0.52
            && outOfSampleScore >= 0.42
            && metrics.RealismPenalty < 0.5
            && metrics.SampleQuality >= 0.45)
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
