namespace Hermes.Runtime;

public sealed record StrategyDiscoveryReport(
    string ReportId,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<TrustedStrategySource> TrustedSources,
    int SourcesWhitelisted,
    int LocalCsFilesAnalyzed,
    int StrategiesAnalyzed,
    int RiskFlagsDetected,
    IReadOnlyList<StrategyDiscoveryFinding> Findings,
    IReadOnlyList<string> Warnings,
    bool NoForeignCodeExecuted,
    bool NoAutoTrading,
    bool HumanReviewRequired);
