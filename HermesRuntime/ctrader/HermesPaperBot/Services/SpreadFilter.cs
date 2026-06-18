namespace HermesPaperBot.Services;

using HermesPaperBot.Models;

/// <summary>
/// Evaluates spread allowance in paper-only mode.
/// </summary>
public sealed class SpreadFilter
{
    /// <summary>
    /// Evaluates spread against the configured threshold.
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
