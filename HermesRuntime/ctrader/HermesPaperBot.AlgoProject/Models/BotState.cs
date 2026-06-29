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

    /// <summary>
    /// Current active paper position.
    /// </summary>
    public PaperPosition? ActivePaperPosition { get; init; }

    /// <summary>
    /// Completed paper positions count.
    /// </summary>
    public int CompletedPaperPositionsCount { get; init; } = 0;

    /// <summary>
    /// Last paper exit reason.
    /// </summary>
    public PaperExitReason LastPaperExitReason { get; init; } = PaperExitReason.None;
}
