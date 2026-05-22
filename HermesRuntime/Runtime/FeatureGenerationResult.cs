namespace Hermes.Runtime;

public sealed record FeatureGenerationResult(
    FeatureGenerationJob Job,
    int CandleCount,
    int FeatureCount,
    IReadOnlyList<string> SymbolsProcessed,
    string OutputPath);
