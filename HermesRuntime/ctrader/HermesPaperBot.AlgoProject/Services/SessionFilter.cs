namespace HermesPaperBot.Services;

using HermesPaperBot.Models;

/// <summary>
/// Evaluates session allowance in paper-only mode.
/// </summary>
public sealed class SessionFilter
{
    /// <summary>
    /// Evaluates the current session.
    /// </summary>
    public FilterResult Evaluate(RuntimeMarketContext context)
    {
        return new FilterResult
        {
            Allowed = false,
            Status = "not_implemented",
            Reason = "blocked_by_skeleton",
        };
    }
}
