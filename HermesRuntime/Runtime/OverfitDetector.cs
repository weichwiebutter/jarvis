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

        if (metrics.Winrate >= 0.90 && metrics.TradeCount >= 100 && metrics.MaxDrawdown > -1.0)
        {
            flags.Add("too_smooth_equity_curve");
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

        if (metrics.SampleQuality < 0.45)
        {
            flags.Add("small_sample_quality_penalty");
        }

        if (metrics.ParameterStability < 0.55)
        {
            flags.Add("parameter_instability");
        }

        if (metrics.OverfitRisk >= 0.65)
        {
            flags.Add("high_model_overfit_risk");
        }

        if (metrics.RealismPenalty >= 0.45)
        {
            flags.Add("high_realism_penalty");
        }

        if (metrics.EstimatedCostR > Math.Max(1, metrics.GrossProfitR) * 0.35)
        {
            flags.Add("cost_sensitive_parameters");
        }

        return flags;
    }
}
