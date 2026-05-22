namespace Hermes.Runtime;

public sealed record FeatureGenerationJob(
    string GenerationId,
    IReadOnlyList<string> Symbols,
    IReadOnlyList<string> Timeframes,
    string SourceRoot,
    DateTimeOffset RequestedAtUtc,
    bool DemoData);
