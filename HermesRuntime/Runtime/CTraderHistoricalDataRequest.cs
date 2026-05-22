namespace Hermes.Runtime;

public sealed record CTraderHistoricalDataRequest(
    string Symbol,
    string Timeframe,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc);
