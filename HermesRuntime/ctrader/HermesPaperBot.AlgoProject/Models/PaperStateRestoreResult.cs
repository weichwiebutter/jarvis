namespace HermesPaperBot.Models;

/// <summary>
/// Defensive result of loading a paper state snapshot.
/// </summary>
public sealed class PaperStateRestoreResult
{
    /// <summary>
    /// Whether loading succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Whether the snapshot was read successfully.
    /// </summary>
    public bool SnapshotValid { get; init; }

    /// <summary>
    /// Whether a corrupt snapshot was detected.
    /// </summary>
    public bool CorruptSnapshotDetected { get; init; }

    /// <summary>
    /// Whether a fresh state was used instead of the snapshot.
    /// </summary>
    public bool FreshStateUsed { get; init; }

    /// <summary>
    /// Whether the kill switch was activated.
    /// </summary>
    public bool KillSwitchActive { get; init; }

    /// <summary>
    /// Whether broker action stays none.
    /// </summary>
    public string BrokerAction { get; init; } = "none";

    /// <summary>
    /// Result state label.
    /// </summary>
    public string State { get; init; } = "unknown";

    /// <summary>
    /// Restore reason.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Restored portfolio state if available.
    /// </summary>
    public PaperPortfolioState? PaperPortfolioState { get; init; }
}
