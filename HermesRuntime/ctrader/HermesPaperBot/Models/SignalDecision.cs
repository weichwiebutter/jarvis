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
}
