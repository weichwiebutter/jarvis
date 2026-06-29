namespace HermesPaperBot.Models;

/// <summary>
/// Forbidden capability flags for release bundles.
/// </summary>
public sealed class ForbiddenCapabilities
{
    public bool MarketOrderExecutionForbidden { get; init; } = true;
    public bool LimitOrderPlacementForbidden { get; init; } = true;
    public bool StopOrderPlacementForbidden { get; init; } = true;
    public bool PositionModificationForbidden { get; init; } = true;
    public bool PositionClosingForbidden { get; init; } = true;
    public bool PendingOrderCancellationForbidden { get; init; } = true;
    public bool ExternalNetworkAccessForbidden { get; init; } = true;
}
