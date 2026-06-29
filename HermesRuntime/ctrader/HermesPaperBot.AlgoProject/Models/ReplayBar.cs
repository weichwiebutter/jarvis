namespace HermesPaperBot.Models;

/// <summary>
/// Single historical OHLC replay bar with spread.
/// </summary>
public sealed class ReplayBar
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public decimal Open { get; init; } = 0m;
    public decimal High { get; init; } = 0m;
    public decimal Low { get; init; } = 0m;
    public decimal Close { get; init; } = 0m;
    public decimal Spread { get; init; } = 0m;
}
