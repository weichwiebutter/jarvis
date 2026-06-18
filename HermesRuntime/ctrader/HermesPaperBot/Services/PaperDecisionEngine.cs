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
    public DecisionResult Evaluate(BotState state, RuntimeMarketContext context)
    {
        if (state is not null && state.KillSwitchActive)
        {
            return new DecisionResult
            {
                Decision = "would_block_by_safety",
                BrokerAction = "none",
                Reason = "kill_switch_active",
            };
        }

        return new DecisionResult
        {
            Decision = "would_wait",
            BrokerAction = "none",
            Reason = "ok",
        };
    }
}
