namespace Hermes.Runtime;

public sealed record MarketDataImportJob(
    string ImportId,
    IReadOnlyList<string> Symbols,
    IReadOnlyList<string> Timeframes,
    string Source,
    DateTimeOffset RequestedAtUtc,
    bool DemoData);
