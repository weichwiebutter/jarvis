namespace Hermes.Runtime;

public sealed record CTraderConnectionHealth(
    DateTimeOffset TimestampUtc,
    string Status,
    string Environment,
    bool StubActive,
    bool AuthConfigured,
    bool ClientIdConfigured,
    bool AccountIdConfigured,
    bool NoOrders,
    bool ReadOnlyMarketData,
    IReadOnlyList<string> Warnings);
