namespace HermesPaperBot.Models;

/// <summary>
/// Kill-switch evaluation result.
/// </summary>
public sealed class KillSwitchResult
{
    public bool Active { get; init; } = false;
    public string Status { get; init; } = "not_implemented";
    public string Reason { get; init; } = "blocked_by_skeleton";
    public string BrokerAction { get; init; } = "none";
}
