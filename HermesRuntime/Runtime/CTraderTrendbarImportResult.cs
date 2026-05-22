namespace Hermes.Runtime;

public sealed record CTraderTrendbarImportResult(
    string DownloadId,
    string Symbol,
    string Timeframe,
    string OutputPath,
    int CandleCount,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    bool StubData);
