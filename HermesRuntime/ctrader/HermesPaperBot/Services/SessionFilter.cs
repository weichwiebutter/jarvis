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
        => Evaluate(context, []);

    /// <summary>
    /// Evaluates the current session against candidate session tags.
    /// </summary>
    public FilterResult Evaluate(RuntimeMarketContext context, IReadOnlyList<string>? sessionTags)
    {
        if (context is null)
        {
            return new FilterResult
            {
                Allowed = true,
                Status = "session_context_missing",
                Reason = "session_context_missing",
            };
        }

        var tags = (sessionTags ?? Array.Empty<string>())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim().ToLowerInvariant())
            .ToArray();

        if (tags.Length == 0)
        {
            return new FilterResult
            {
                Allowed = true,
                Status = "no_session_limit",
                Reason = "no_session_limit",
            };
        }

        var utcHour = context.ServerTimeUtc.UtcDateTime.Hour;
        var inLondon = utcHour >= 7 && utcHour < 16;
        var inNewYork = utcHour >= 12 && utcHour < 21;
        var inOverlap = utcHour >= 13 && utcHour < 16;
        var inSession = tags.Any(tag =>
            tag.Contains("overlap", StringComparison.OrdinalIgnoreCase) ? inOverlap :
            tag.Contains("london", StringComparison.OrdinalIgnoreCase) ? inLondon :
            tag.Contains("new_york", StringComparison.OrdinalIgnoreCase) || tag.Contains("newyork", StringComparison.OrdinalIgnoreCase) ? inNewYork :
            tag.Contains("asia", StringComparison.OrdinalIgnoreCase) || tag.Contains("tokyo", StringComparison.OrdinalIgnoreCase) ? utcHour < 9 || utcHour >= 22 :
            tag.Contains("session", StringComparison.OrdinalIgnoreCase));

        if (!inSession)
        {
            return new FilterResult
            {
                Allowed = false,
                Status = "blocked_by_session",
                Reason = "session_too_far_from_preferred_window",
            };
        }

        return new FilterResult
        {
            Allowed = true,
            Status = "session_ok",
            Reason = "session_within_preferred_window",
        };
    }
}
