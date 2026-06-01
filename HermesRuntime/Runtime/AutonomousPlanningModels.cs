namespace Hermes.Runtime;

public enum NeedSeverity
{
    low,
    medium,
    high,
    critical
}

public enum NeedCategory
{
    knowledge_gap,
    validation_gap,
    data_gap,
    quality_risk,
    resource_risk,
    domain_gap,
    maintenance
}

public sealed record DetectedNeed(
    string NeedId,
    NeedCategory Category,
    NeedSeverity Severity,
    string Domain,
    string Title,
    string Description,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> SuggestedTaskTypes,
    DateTimeOffset DetectedAtUtc,
    bool NoTradingExecution,
    bool HumanReviewRequired);

public sealed record GoalProgress(
    string GoalId,
    double ProgressScore,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> NextActions,
    DateTimeOffset UpdatedAtUtc);

public sealed record HermesGoal(
    string GoalId,
    string Description,
    int Priority,
    bool Active,
    double ProgressScore,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> NextActions);

public sealed record PriorityScore(
    double Impact,
    double Urgency,
    double Confidence,
    double Cost,
    double Risk,
    double ExpectedLearningValue,
    double TotalScore);

public sealed record PlannedTask(
    string TaskId,
    string TaskType,
    string Domain,
    string GoalId,
    string NeedId,
    string QueueType,
    PriorityScore Priority,
    string Reason,
    string ExpectedOutcome,
    IReadOnlyList<string> SourceRefs,
    DateTimeOffset CreatedAtUtc,
    string Status,
    bool NoTradingExecution,
    bool HumanReviewRequired);

public sealed record PlanningDecision(
    string DecisionId,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<DetectedNeed> Needs,
    IReadOnlyList<HermesGoal> Goals,
    IReadOnlyList<PlannedTask> PlannedTasks,
    IReadOnlyList<string> Explanations,
    bool NoTradingExecution,
    bool HumanReviewRequired);

public sealed record AutonomousPlanningStatus(
    string StatusVersion,
    DateTimeOffset UpdatedAtUtc,
    int NeedsDetected,
    int ActiveGoals,
    int PlannedTasks,
    int QueuedResearchItems,
    IReadOnlyList<string> ActiveDomains,
    string LastDecisionId,
    string NextAction,
    IReadOnlyList<string> TopNeeds,
    IReadOnlyList<string> TopTasks,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);
