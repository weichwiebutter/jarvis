namespace HermesPaperBot.Models;

/// <summary>
/// Snapshot of the virtual paper runtime state.
/// </summary>
public sealed class PaperStateSnapshot
{
    /// <summary>
    /// Snapshot schema version.
    /// </summary>
    public string SchemaVersion { get; init; } = "paper_state_snapshot_v1";

    /// <summary>
    /// Generation timestamp in UTC.
    /// </summary>
    public string GeneratedAtUtc { get; init; } = string.Empty;

    /// <summary>
    /// Paper portfolio state snapshot.
    /// </summary>
    public PaperPortfolioState PaperPortfolioState { get; init; } = new();

    /// <summary>
    /// Last runtime state.
    /// </summary>
    public string LastState { get; init; } = "unknown";

    /// <summary>
    /// Last paper decision.
    /// </summary>
    public string LastPaperDecision { get; init; } = "would_wait";

    /// <summary>
    /// Last broker action.
    /// </summary>
    public string BrokerAction { get; init; } = "none";
}
