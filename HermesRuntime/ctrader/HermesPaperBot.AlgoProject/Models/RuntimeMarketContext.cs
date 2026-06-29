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
    public string Symbol { get; init; } = string.Empty;

    /// <summary>
    /// Current symbol.
    /// </summary>
    public string CurrentSymbol
    {
        get => Symbol;
        init => Symbol = value;
    }

    /// <summary>
    /// Current timeframe.
    /// </summary>
    public string Timeframe { get; init; } = string.Empty;

    /// <summary>
    /// Current timeframe.
    /// </summary>
    public string CurrentTimeframe
    {
        get => Timeframe;
        init => Timeframe = value;
    }

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
    /// Spread in pips if available.
    /// </summary>
    public decimal? SpreadPips { get; init; }

    /// <summary>
    /// Tick size.
    /// </summary>
    public decimal TickSize { get; init; } = 0m;

    /// <summary>
    /// Pip size.
    /// </summary>
    public decimal PipSize { get; init; } = 0m;

    /// <summary>
    /// Server time associated with the runtime market context.
    /// </summary>
    public DateTimeOffset ServerTime { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Server time associated with the runtime market context.
    /// </summary>
    public DateTimeOffset ServerTimeUtc
    {
        get => ServerTime;
        init => ServerTime = value;
    }

    /// <summary>
    /// Source of the market context.
    /// </summary>
    public string Source { get; init; } = "unknown";
}
