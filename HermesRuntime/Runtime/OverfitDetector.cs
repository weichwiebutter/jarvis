namespace Hermes.Runtime;

public static class OverfitDetector
{
    public static IReadOnlyList<string> Detect(
        SimulationPerformanceMetrics metrics,
        double validationScore,
        double outOfSampleScore)
    {
        var flags = new List<string>();

        if (metrics.TooGoodToBeTrue || (metrics.Winrate >= 0.95 && metrics.TradeCount >= 50 && metrics.MaxDrawdown > -2.0))
        {
            flags.Add("too_good_to_be_true");
        }

        if (metrics.Winrate >= 0.95 && metrics.TradeCount >= 50)
        {
            flags.Add("suspicious_winrate");
        }

        if (metrics.Winrate >= 0.88 && metrics.TradeCount >= 100 && metrics.MaxDrawdown > -1.0)
        {
            flags.Add("too_smooth_equity_curve");
        }

        if (metrics.TradeCount >= 80 && metrics.ConsecutiveLosses <= 1 && metrics.LossDistributionQuality < 0.35)
        {
            flags.Add("too_few_losses");
            flags.Add("too_perfect_pattern_penalty");
        }

        if (metrics.TradeCount >= 250 && metrics.MaxDrawdown > -1.0)
        {
            flags.Add("high_trade_count_without_drawdown");
        }

        if (metrics.MaxDrawdown >= -0.25 && metrics.ProfitFactor > 8)
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

        if (metrics.OverfitRisk >= 0.55)
        {
            flags.Add("high_model_overfit_risk");
        }

        if (metrics.RealismPenalty >= 0.38 || metrics.RealismScore < 0.62)
        {
            flags.Add("high_realism_penalty");
        }

        if (metrics.CostSensitivity >= 0.55 || metrics.EstimatedCostR > Math.Max(1, metrics.GrossProfitR) * 0.28)
        {
            flags.Add("cost_sensitive_parameters");
        }

        if (metrics.TradeCount >= 150
            && metrics.EstimatedCostR / Math.Max(1, Math.Abs(metrics.GrossProfitR)) < 0.015)
        {
            flags.Add("cost_impact_suspiciously_low");
        }

        if (metrics.LossDistributionQuality < 0.35 && metrics.TradeCount >= 80)
        {
            flags.Add("poor_loss_distribution");
        }

        return flags;
    }
}
