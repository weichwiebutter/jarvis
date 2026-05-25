namespace Hermes.Runtime;

public static class OverfitDetector
{
    public static IReadOnlyList<string> Detect(
        SimulationPerformanceMetrics metrics,
        double validationScore,
        double outOfSampleScore)
    {
        var flags = new List<string>();

        if (metrics.Winrate >= 0.98 && metrics.TradeCount >= 50)
        {
            flags.Add("suspicious_winrate");
        }

        if (metrics.TradeCount >= 50 && metrics.Winrate >= 0.995 && metrics.ConsecutiveLosses <= 1)
        {
            flags.Add("too_few_losses");
            flags.Add("too_perfect_pattern_penalty");
        }

        if (metrics.MaxDrawdown >= 0 && metrics.ProfitFactor > 10)
        {
            flags.Add("unrealistic_equity_curve");
        }

        if (metrics.StabilityScore < 0.45)
        {
            flags.Add("low_robustness_penalty");
        }

        if (validationScore - outOfSampleScore > 0.25)
        {
            flags.Add("out_of_sample_decay");
        }

        if (metrics.TradeCount < 30)
        {
            flags.Add("too_few_trades");
        }

        if (metrics.EstimatedCostR > Math.Max(1, metrics.GrossProfitR) * 0.35)
        {
            flags.Add("cost_sensitive_parameters");
        }

        return flags;
    }
}
