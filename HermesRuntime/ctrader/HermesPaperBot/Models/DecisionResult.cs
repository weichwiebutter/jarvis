namespace HermesPaperBot.Models;

/// <summary>
/// Generic paper decision result for the skeleton.
/// </summary>
public sealed class DecisionResult
{
    /// <summary>
    /// Placeholder decision label.
    /// </summary>
    public string Decision { get; init; } = "not_implemented";

    /// <summary>
    /// Placeholder broker action, always none in the skeleton.
    /// </summary>
    public string BrokerAction { get; init; } = "none";

    /// <summary>
    /// Placeholder reason for the decision.
    /// </summary>
    public string Reason { get; init; } = "blocked_by_skeleton";
}
