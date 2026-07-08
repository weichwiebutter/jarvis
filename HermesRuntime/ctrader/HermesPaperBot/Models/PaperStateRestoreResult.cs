using System.Linq;

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

    /// <summary>
    /// Restored state label for diagnostics.
    /// </summary>
    public string RestoreState => State;

    /// <summary>
    /// Restored reason label for diagnostics.
    /// </summary>
    public string RestoreReason => Reason;

    /// <summary>
    /// Whether the restored snapshot was valid.
    /// </summary>
    public bool RestoreSnapshotValid => SnapshotValid;

    /// <summary>
    /// Whether a fresh state was used instead of restoring the snapshot.
    /// </summary>
    public bool RestoreFreshStateUsed => FreshStateUsed;

    /// <summary>
    /// Number of active trades in the restored snapshot.
    /// </summary>
    public int RestoreActiveTradeCount => PaperPortfolioState?.ActiveTrades.Length ?? 0;

    /// <summary>
    /// First active trade signal identifier in the restored snapshot.
    /// </summary>
    public string RestoreFirstActiveSignalId => PaperPortfolioState?.ActiveTrades.FirstOrDefault()?.SignalId ?? string.Empty;

    /// <summary>
    /// First active trade entry price in the restored snapshot.
    /// </summary>
    public decimal? RestoreFirstActiveEntry => PaperPortfolioState?.ActiveTrades.FirstOrDefault()?.EntryPrice;

    /// <summary>
    /// First active trade stop loss in the restored snapshot.
    /// </summary>
    public decimal? RestoreFirstActiveSl => PaperPortfolioState?.ActiveTrades.FirstOrDefault()?.StopLossPrice;

    /// <summary>
    /// First active trade take profit in the restored snapshot.
    /// </summary>
    public decimal? RestoreFirstActiveTp => PaperPortfolioState?.ActiveTrades.FirstOrDefault()?.TakeProfitPrice;
}
