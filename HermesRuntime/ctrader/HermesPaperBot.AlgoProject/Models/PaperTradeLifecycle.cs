namespace HermesPaperBot.Models;

/// <summary>
/// Lifecycle of a paper trade.
/// </summary>
public enum PaperTradeLifecycle
{
    Open,
    Active,
    TakeProfitHit,
    StopLossHit,
    Invalidated,
    Expired,
    Closed,
}
