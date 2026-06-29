namespace HermesPaperBot.Models;

/// <summary>
/// Generic filter result for paper-only skeleton services.
/// </summary>
public sealed class FilterResult
{
    /// <summary>
    /// Indicates whether the filter allows continuation.
    /// </summary>
    public bool Allowed { get; init; } = false;

    /// <summary>
    /// Placeholder status text.
    /// </summary>
    public string Status { get; init; } = "not_implemented";

    /// <summary>
    /// Optional reason for a denied filter result.
    /// </summary>
    public string Reason { get; init; } = "blocked_by_skeleton";
}
