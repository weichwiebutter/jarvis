namespace HermesPaperBot.Services;

using System;
using System.Collections.Generic;
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
        => Evaluate(context, null);

    /// <summary>
    /// Evaluates the current session for a specific signal candidate.
    /// </summary>
    public FilterResult Evaluate(RuntimeMarketContext context, SignalCandidate? candidate)
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

        var sessionTags = candidate?.SessionTags ?? Array.Empty<string>();
        if (sessionTags.Length == 0)
        {
            return new FilterResult
            {
                Allowed = true,
                Status = "allowed",
                Reason = "no_session_rule",
            };
        }

        var currentSession = DetermineSession(context.ServerTimeUtc);
        if (IsSessionAllowed(currentSession, sessionTags))
        {
            return new FilterResult
            {
                Allowed = true,
                Status = "allowed",
                Reason = $"session_allowed:{currentSession}",
            };
        }

        return new FilterResult
        {
            Allowed = false,
            Status = "blocked",
            Reason = $"session_not_allowed:{currentSession}",
        };
    }

    private static bool IsSessionAllowed(string currentSession, IReadOnlyList<string> sessionTags)
    {
        foreach (var tag in sessionTags)
        {
            if (IsSessionTagMatch(currentSession, tag))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsSessionTagMatch(string currentSession, string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        var normalizedTag = tag.Trim().Replace("session_", string.Empty, StringComparison.OrdinalIgnoreCase).Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase);
        return currentSession.Equals(normalizedTag, StringComparison.OrdinalIgnoreCase)
               || (normalizedTag.Equals("overlap", StringComparison.OrdinalIgnoreCase) && currentSession.Equals("london_new_york_overlap", StringComparison.OrdinalIgnoreCase))
               || (normalizedTag.Equals("londonnewyorkoverlap", StringComparison.OrdinalIgnoreCase) && currentSession.Equals("london_new_york_overlap", StringComparison.OrdinalIgnoreCase));
    }

    private static string DetermineSession(DateTimeOffset timestampUtc)
    {
        var hour = timestampUtc.UtcDateTime.Hour;
        if (hour is >= 7 and < 10)
        {
            return "london";
        }

        if (hour is >= 13 and < 17)
        {
            return "london_new_york_overlap";
        }

        if (hour is >= 13 and < 21)
        {
            return "new_york";
        }

        return "other";
    }
}
