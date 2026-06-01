namespace Hermes.Runtime;

public sealed record MetaObservation(
    string ObservationId,
    string Category,
    string Severity,
    string Title,
    string Summary,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> RecommendedActions);

public sealed record DomainHealthScore(
    string Domain,
    double KnowledgeCoverage,
    double ValidationCoverage,
    double TrustScore,
    double RedundancyScore,
    double LearningVelocity,
    double OverallScore,
    string Classification,
    IReadOnlyList<string> Reasons);

public sealed record DomainHealth(
    string Domain,
    bool Active,
    int SourceCount,
    int KnowledgeItemCount,
    int QueueItems,
    int OpenQueueItems,
    int ProcessedQueueItems,
    int OutcomeCount,
    DomainHealthScore Score,
    IReadOnlyList<string> Warnings);

public sealed record GovernanceRule(
    string RuleId,
    string Description,
    string Severity,
    double Threshold,
    bool Enabled);

public sealed record GovernanceDecision(
    string RuleId,
    string Status,
    string Reason,
    string Action,
    IReadOnlyList<string> EvidenceRefs);

public sealed record LearningStrategy(
    string StrategyVersion,
    DateTimeOffset UpdatedAtUtc,
    string CurrentStrategy,
    string Reason,
    IReadOnlyList<string> PriorityTaskTypes,
    IReadOnlyList<string> DeprioritizedTaskTypes,
    IReadOnlyList<string> DomainFocus,
    string ExpectedEffect,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record MetaReviewResult(
    string ReviewVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int GoalsReviewed,
    int OutcomesReviewed,
    int PlannerTaskTypesReviewed,
    int KnowledgeItems,
    int ResearchQueueItems,
    IReadOnlyList<MetaObservation> Observations,
    IReadOnlyList<string> ActivitiesWithProgress,
    IReadOnlyList<string> ActivitiesGeneratingWork,
    IReadOnlyList<string> StagnantGoals,
    IReadOnlyList<string> RecurringNeeds,
    IReadOnlyList<DomainHealth> DomainHealth,
    IReadOnlyList<GovernanceDecision> GovernanceDecisions,
    LearningStrategy LearningStrategy,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);
