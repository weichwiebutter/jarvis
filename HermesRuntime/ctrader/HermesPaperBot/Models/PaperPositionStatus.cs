namespace HermesPaperBot.Models;

/// <summary>
/// Status of a virtual paper position.
/// </summary>
public enum PaperPositionStatus
{
    Open = 0,
    Active = 1,
    TakeProfitHit = 2,
    StopLossHit = 3,
    Expired = 4,
    Closed = 5,
    Invalidated = 6,
}
