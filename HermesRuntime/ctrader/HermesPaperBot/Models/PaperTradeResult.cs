namespace HermesPaperBot.Models;

/// <summary>
/// Result of a paper trade lifecycle step.
/// </summary>
public sealed class PaperTr\u0061deResult
{
    public string SignalId { get; init; } = string.Empty;
    public string Asset { get; init; } = string.Empty;
    public string Timeframe { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public string Decision { get; init; } = "would_wait";
    public string BrokerAction { get; init; } = "none";
    public PaperTradeLifecycle Lifecycle { get; init; } = PaperTradeLifecycle.Active;
    public string Reason { get; init; } = "ok";
    public decimal EntryPrice { get; init; } = 0m;
    public decimal ExitPrice { get; init; } = 0m;
    public decimal ProfitR { get; init; } = 0m;
}
