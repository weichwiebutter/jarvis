namespace HermesPaperBot.Models;

/// <summary>
/// Embedded chart annotation specification for cloud and review exports.
/// </summary>
public sealed record ChartAnnotationSpec(
    string SignalId,
    string Symbol,
    string Timeframe,
    string SetupId,
    string Direction,
    decimal EntryPrice,
    decimal StopLoss,
    decimal TakeProfit1,
    decimal? TakeProfit2,
    decimal InvalidationLevel,
    decimal RiskReward,
    string AnnotationStyle,
    IReadOnlyList<string> Labels,
    DateTimeOffset CreatedAtUtc,
    string SignalStatus);
