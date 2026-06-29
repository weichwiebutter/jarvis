namespace HermesPaperBot.Models;

/// <summary>
/// Embedded signal decision extracted from the generated package JSON.
/// </summary>
public sealed class SignalDecision
{
    /// <summary>
    /// Direction to act on.
    /// </summary>
    public SignalDirection Direction { get; init; } = SignalDirection.Flat;

    /// <summary>
    /// Signal confidence.
    /// </summary>
    public decimal Confidence { get; init; } = 0m;

    /// <summary>
    /// Strategy identifier.
    /// </summary>
    public string StrategyId { get; init; } = string.Empty;

    /// <summary>
    /// Signal timestamp in UTC.
    /// </summary>
    public DateTimeOffset SignalTimestampUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Signal expiry in UTC.
    /// </summary>
    public DateTimeOffset ExpiryUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Decision reason.
    /// </summary>
    public string Reason { get; init; } = string.Empty;

    /// <summary>
    /// Optional stop loss price.
    /// </summary>
    public decimal? StopLossPrice { get; init; }

    /// <summary>
    /// Optional take profit price.
    /// </summary>
    public decimal? TakeProfitPrice { get; init; }

    /// <summary>
    /// Optional maximum holding seconds.
    /// </summary>
    public int? MaxHoldingSeconds { get; init; }

    /// <summary>
    /// Optional risk in R.
    /// </summary>
    public decimal? RiskR { get; init; }
}
