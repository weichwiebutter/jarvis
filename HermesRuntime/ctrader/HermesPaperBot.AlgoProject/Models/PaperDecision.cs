namespace HermesPaperBot.Models;

/// <summary>
/// Paper decision.
/// </summary>
public sealed class PaperDecision
{
    /// <summary>
    /// Decision label.
    /// </summary>
    public string Decision { get; init; } = "not_implemented";

    /// <summary>
    /// Broker action placeholder.
    /// </summary>
    public string BrokerAction { get; init; } = "none";

    /// <summary>
    /// Decision reason.
    /// </summary>
    public string Reason { get; init; } = "blocked_by_skeleton";
}
