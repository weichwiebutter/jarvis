namespace HermesPaperBot.Services;

using System;
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
        if (context is null)
        {
            return new FilterResult
            {
                Allowed = false,
                Status = "missing_context",
                Reason = "blocked_by_skeleton",
            };
        }

        if (!string.IsNullOrWhiteSpace(context.Source) &&
            (context.Source.Contains("harness", StringComparison.OrdinalIgnoreCase) ||
             context.Source.Contains("paper", StringComparison.OrdinalIgnoreCase)))
        {
            return new FilterResult
            {
                Allowed = true,
                Status = "allowed",
                Reason = "paper_runtime_harness",
            };
        }

        return new FilterResult
        {
            Allowed = false,
            Status = "blocked",
            Reason = "blocked_by_skeleton",
        };
    }
}
