namespace HermesPaperBot.Models;

/// <summary>
/// Virtual paper trade position.
/// </summary>
public sealed class PaperPosition
{
    public string SignalId { get; init; } = string.Empty;
    public string Asset { get; init; } = string.Empty;
    public string Timeframe { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public decimal EntryPrice { get; init; } = 0m;
    public decimal StopLossPrice { get; init; } = 0m;
    public decimal TakeProfitPrice { get; init; } = 0m;
    public decimal ProfitR { get; init; } = 0m;
    public PaperTradeLifecycle Lifecycle { get; init; } = PaperTradeLifecycle.Open;
    public DateTimeOffset? ExpiresAtUtc { get; init; }
    public DateTimeOffset OpenedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ClosedAtUtc { get; init; }
    public string CloseReason { get; init; } = string.Empty;
}
