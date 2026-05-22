namespace Hermes.Runtime;

public sealed record TradingLearningBetaReport(
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
    IReadOnlyList<string> Warnings,
    double DurationSeconds,
    bool LearningReady,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string? BetaReportPath,
    string? ResearchReportPath,
    string? FeatureOutputPath,
    string? SignalOutputPath,
    string? OutcomeReportPath,
    string? BacktestReportPath);
