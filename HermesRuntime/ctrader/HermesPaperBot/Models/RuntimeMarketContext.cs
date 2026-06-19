using System;

namespace HermesPaperBot.Models;

/// <summary>
/// Runtime market context.
/// </summary>
public sealed class RuntimeMarketContext
{
    /// <summary>
    /// Current symbol.
    /// </summary>
    public string CurrentSymbol { get; init; } = string.Empty;

    /// <summary>
    /// Current timeframe.
    /// </summary>
    public string CurrentTimeframe { get; init; } = string.Empty;

    /// <summary>
    /// Bid price.
    /// </summary>
    public decimal Bid { get; init; } = 0m;

    /// <summary>
    /// Ask price.
    /// </summary>
    public decimal Ask { get; init; } = 0m;

    /// <summary>
    /// Spread value.
    /// </summary>
    public decimal Spread { get; init; } = 0m;

    /// <summary>
    /// Server time associated with the runtime market context.
    /// </summary>
    public DateTimeOffset ServerTime { get; init; } = DateTimeOffset.UtcNow;
}
