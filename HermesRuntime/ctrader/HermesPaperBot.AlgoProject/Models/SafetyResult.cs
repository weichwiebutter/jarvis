namespace HermesPaperBot.Models;

/// <summary>
/// Generic safety result for paper-only skeleton services.
/// </summary>
public sealed class SafetyResult
{
    /// <summary>
    /// Indicates whether the safety gate passed.
    /// </summary>
    public bool Passed { get; init; } = false;

    /// <summary>
    /// Placeholder status text.
    /// </summary>
    public string Status { get; init; } = "not_implemented";

    /// <summary>
    /// Placeholder broker action, always none in the skeleton.
    /// </summary>
    public string BrokerAction { get; init; } = "none";

    /// <summary>
    /// Optional reason text.
    /// </summary>
    public string Reason { get; init; } = "blocked_by_skeleton";
}
