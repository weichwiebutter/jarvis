namespace Hermes.Runtime;

public sealed record StrategyResearchMemory(
    string MemoryVersion,
    DateTimeOffset UpdatedAtUtc,
    int VariantsTested,
    IReadOnlyList<string> TestedVariantIds,
    IReadOnlyList<StrategyResearchResult> TopVariants,
    IReadOnlyList<StrategyResearchResult> RejectedVariants,
    IReadOnlyList<string> Warnings,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    IReadOnlyList<StrategyResearchMemoryEntry>? ResearchEntries = null);
