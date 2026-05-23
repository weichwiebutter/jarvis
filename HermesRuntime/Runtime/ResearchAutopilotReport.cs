namespace Hermes.Runtime;

public sealed record ResearchAutopilotReport(
    string ReportId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    double RequestedHours,
    IReadOnlyList<string> TargetSymbols,
    IReadOnlyList<string> TargetTimeframes,
    DateTimeOffset TargetFromUtc,
    DateTimeOffset TargetToUtc,
    int DownloadPlans,
    int DownloadsAttempted,
    int CandlesDownloaded,
    int DownloadRequests,
    int StrategyVariantsTested,
    int StrategyResearchEntries,
    string PatternCatalogPath,
    string InsightsPath,
    string Status,
    IReadOnlyList<string> Warnings,
    bool NoAutoTrading,
    bool HumanReviewRequired);
