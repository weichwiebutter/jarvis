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

        if (validationScore >= 0.75 && outOfSampleScore >= 0.62 && metrics.StabilityScore >= 0.65)
        {
            return "robust";
        }

        if (validationScore >= 0.55 && outOfSampleScore >= 0.45)
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
