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
    public FilterResult Evaluate(RuntimeMarketContext context, decimal? maxSpreadPips = null)
    {
        if (context is null)
        {
            return new FilterResult
            {
                Allowed = true,
                Status = "spread_context_missing",
                Reason = "spread_context_missing",
            };
        }

        if (maxSpreadPips is null)
        {
            return new FilterResult
            {
                Allowed = true,
                Status = "no_spread_limit",
                Reason = "no_spread_limit",
            };
        }

        var effectiveSpreadPips = context.SpreadPips;
        if (!effectiveSpreadPips.HasValue && context.PipSize > 0m)
        {
            effectiveSpreadPips = context.Spread / context.PipSize;
        }

        if (!effectiveSpreadPips.HasValue)
        {
            return new FilterResult
            {
                Allowed = true,
                Status = "spread_pips_missing",
                Reason = "spread_pips_missing",
            };
        }

        if (effectiveSpreadPips.Value > maxSpreadPips.Value)
        {
            return new FilterResult
            {
                Allowed = false,
                Status = "blocked_by_spread",
                Reason = "spread_too_high",
            };
        }

        return new FilterResult
        {
            Allowed = true,
            Status = "spread_ok",
            Reason = "spread_within_limit",
        };
    }
}
