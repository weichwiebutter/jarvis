namespace HermesPaperBot.Models;

/// <summary>
/// Virtual paper portfolio state.
/// </summary>
public sealed class PaperPortfolioState
{
    public PaperPosition[] ActiveTrades { get; init; } = [];
    public int OpenTradeCountToday { get; init; } = 0;
    public int OpenTradeCountThisHour { get; init; } = 0;
    public int ConsecutiveLosses { get; init; } = 0;
    public decimal DailyPaperLossR { get; init; } = 0m;
    public DateTimeOffset LastUpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
