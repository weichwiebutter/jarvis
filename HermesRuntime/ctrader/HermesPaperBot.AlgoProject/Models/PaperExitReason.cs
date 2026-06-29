namespace HermesPaperBot.Models;

/// <summary>
/// Exit reason for a virtual paper position.
/// </summary>
public enum PaperExitReason
{
    None = 0,
    TakeProfitHit = 1,
    StopLossHit = 2,
    Expired = 3,
    MissingRiskBounds = 4,
    SignalMissing = 5,
    Invalidated = 6,
}
