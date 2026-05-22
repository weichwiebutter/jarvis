namespace Hermes.Runtime;

public sealed record CTraderCsvImportResult(
    string ImportId,
    string Symbol,
    string Timeframe,
    MarketDataImportFormat Format,
    string SourcePath,
    string? OutputPath,
    string? RawImportPath,
    ImportValidationResult Validation);
