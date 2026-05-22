namespace Hermes.Runtime;

public sealed record NightlyResearchReport(
    string JobId,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    double DurationSeconds,
    int FeatureCount,
    int SignalCount,
    int OutcomeCount,
    int BacktestCount,
    string? FeatureOutputPath,
    string? SignalOutputPath,
    string? OutcomeReportPath,
    string? BacktestReportPath,
    IReadOnlyList<string> Warnings,
    bool NoAutoTrading,
    bool HumanReviewRequired);
