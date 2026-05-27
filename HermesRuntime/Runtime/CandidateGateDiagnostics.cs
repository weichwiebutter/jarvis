namespace Hermes.Runtime;

public sealed record CandidateGateDiagnostics(
    string CandidateId,
    string StrategyId,
    string StrategyFamily,
    string? PatternId,
    string Symbol,
    string Timeframe,
    string Status,
    string PrimaryRejectionReason,
    IReadOnlyList<string> SecondaryRejectionReasons,
    string WeakestMetric,
    string NearestPassThreshold,
    string ImprovementHint,
    double NearMissScore,
    bool IsNearMiss,
    bool IsCompletelyUnsuitable);

public sealed record BotCandidateRejectionAnalysisReport(
    string ReportId,
    DateTimeOffset CreatedAtUtc,
    int CandidatesAnalyzed,
    int RejectedCandidates,
    int NearMissCount,
    IReadOnlyList<string> WhyNoCandidates,
    IReadOnlyList<RejectionReasonSummary> ReasonSummaries,
    IReadOnlyList<CandidateGateDiagnostics> CandidateDiagnostics,
    IReadOnlyList<CandidateGateDiagnostics> NearMissStrategies,
    IReadOnlyList<CandidateGateDiagnostics> BestRejectedStrategies,
    IReadOnlyList<string> PotentialClusters,
    IReadOnlyList<string> UnsuitableClusters,
    IReadOnlyList<StrategyImprovementSuggestion> RecommendedImprovementExperiments,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);
