namespace Hermes.Runtime;

public sealed record RegimeSummaryReport(
    string ReportVersion,
    DateTimeOffset GeneratedAtUtc,
    string SourceFeatureFile,
    int FeaturesAnalyzed,
    int SnapshotCount,
    IReadOnlyList<string> Symbols,
    IReadOnlyList<string> Timeframes,
    IReadOnlyList<string> DominantRegimes,
    IReadOnlyList<string> DominantSessions,
    IReadOnlyList<MarketRegimeSnapshot> TopSnapshots,
    IReadOnlyList<string> Warnings,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record RegimeDistributionEntry(
    string Symbol,
    string Timeframe,
    string RegimeType,
    string Session,
    int CandleCount,
    double Percentage,
    double AverageConfidence);

public sealed record RegimeDistributionReport(
    string ReportVersion,
    DateTimeOffset GeneratedAtUtc,
    int TotalCandles,
    IReadOnlyList<RegimeDistributionEntry> Entries,
    IReadOnlyList<string> Warnings,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record StrategyRegimePerformanceEntry(
    string StrategyFamily,
    string PatternId,
    string PatternName,
    string RegimeType,
    string Session,
    int VariantCount,
    int TotalTrades,
    double AverageFitness,
    double AverageWinrate,
    double AverageRegimeConfidence,
    double RegimeFitScore,
    string Status);

public sealed record StrategyRegimePerformanceReport(
    string ReportVersion,
    DateTimeOffset GeneratedAtUtc,
    int StrategiesAnalyzed,
    int RegimeSnapshotsAnalyzed,
    IReadOnlyList<StrategyRegimePerformanceEntry> Entries,
    IReadOnlyList<string> StrongRegimeMatches,
    IReadOnlyList<string> WeakRegimeMatches,
    IReadOnlyList<string> PreferredSessions,
    IReadOnlyList<string> AvoidSessions,
    IReadOnlyList<string> VolatilityPreference,
    double RegimeConsistencyScore,
    IReadOnlyList<string>? PreferredRegimes,
    IReadOnlyList<string>? AvoidedRegimes,
    double RegimeSampleQuality,
    IReadOnlyList<string> Warnings,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record MarketRegimeAnalysisResult(
    RegimeSummaryReport Summary,
    RegimeDistributionReport Distribution,
    StrategyRegimePerformanceReport StrategyPerformance,
    string SummaryPath,
    string DistributionPath,
    string StrategyPerformancePath,
    string SnapshotMemoryPath);
