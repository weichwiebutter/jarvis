namespace Hermes.Runtime;

public sealed record MarketDataCandle(
    DateTimeOffset TimestampUtc,
    double Open,
    double High,
    double Low,
    double Close,
    double Volume,
    string Symbol,
    string Timeframe);
