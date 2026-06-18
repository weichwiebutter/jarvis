namespace HermesPaperBot.Models;

/// <summary>
/// Runtime state.
/// </summary>
public sealed class BotState
{
    /// <summary>
    /// Current lifecycle status.
    /// </summary>
    public string Status { get; init; } = "not_implemented";

    /// <summary>
    /// Whether the kill switch is active.
    /// </summary>
    public bool KillSwitchActive { get; init; } = false;

    /// <summary>
    /// Whether the last bundle was valid.
    /// </summary>
    public bool LastBundleValid { get; init; } = false;
}
