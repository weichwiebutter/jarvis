namespace Hermes.Runtime;

public sealed record ResearchSummaryReport(
    string RunId,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<string> SymbolsProcessed,
    int CandlesProcessed,
    int FeaturesGenerated,
    int SignalsGenerated,
    int OutcomesGenerated,
    int BacktestsGenerated,
    int ReportsGenerated,
    IReadOnlyList<string> Warnings,
    double DurationSeconds,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string? FeatureOutputPath,
    string? SignalOutputPath,
    string? OutcomeReportPath,
    string? BacktestReportPath,
    string? NightlyReportPath,
    string? ResearchReportPath);
