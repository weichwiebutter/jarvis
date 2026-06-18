namespace HermesPaperBot.Services;

using HermesPaperBot.Models;

/// <summary>
/// Computes paper-only decisions.
/// </summary>
public sealed class PaperDecisionEngine
{
    /// <summary>
    /// Evaluates a paper-only decision.
    /// </summary>
    public PaperDecision Evaluate(BotState state, RuntimeMarketContext context)
    {
        return new PaperDecision
        {
            Decision = "not_implemented",
            BrokerAction = "none",
            Reason = "blocked_by_skeleton",
        };
    }
}
