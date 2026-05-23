namespace Hermes.Runtime;

public sealed record ResearchMemoryIndex(
    string IndexVersion,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? LastRunAt,
    IReadOnlyList<string> SymbolsProcessed,
    IReadOnlyList<string> TimeframesProcessed,
    int CandlesProcessed,
    int FeaturesGenerated,
    int SignalsGenerated,
    int OutcomesGenerated,
    int BacktestsGenerated,
    IReadOnlyList<ResearchProcessedRange> ProcessedRanges,
    IReadOnlyList<string> Warnings,
    bool LearningReady,
    IReadOnlyList<string> IndexedRunIds,
    int RunCount);

