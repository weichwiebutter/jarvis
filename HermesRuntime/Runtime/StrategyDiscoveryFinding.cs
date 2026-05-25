namespace Hermes.Runtime;

public sealed record StrategyDiscoveryFinding(
    string FindingId,
    string SourceId,
    string SourceUrl,
    string? LocalFile,
    IReadOnlyList<string> IndicatorsUsed,
    IReadOnlyList<string> EntryLogicHints,
    IReadOnlyList<string> ExitLogicHints,
    IReadOnlyList<string> RiskLogicHints,
    IReadOnlyList<string> RiskFlags);
