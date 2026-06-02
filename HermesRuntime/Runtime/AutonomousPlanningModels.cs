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
    string Title,
    string Domain,
    int Priority,
    string TargetState,
    string CurrentState,
    double ProgressScore,
    int BlockerCount,
    IReadOnlyList<string> RelatedNeeds,
    IReadOnlyList<string> RelatedTasks,
    IReadOnlyList<string> RecentOutcomes,
    IReadOnlyList<string> NextRecommendedActions,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> NextActions,
    DateTimeOffset UpdatedAtUtc);

public sealed record HermesGoal(
    string GoalId,
    string Title,
    string Domain,
    string Description,
    int Priority,
    bool Active,
    string TargetState,
    string CurrentState,
    double ProgressScore,
    int BlockerCount,
    DateTimeOffset LastUpdatedUtc,
    IReadOnlyList<string> NextRecommendedActions,
    IReadOnlyList<string> RelatedNeeds,
    IReadOnlyList<string> RelatedTasks,
    IReadOnlyList<string> RecentOutcomes,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> NextActions);

public sealed record GoalState(
    string StateVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<HermesGoal> Goals,
    int ActiveGoals,
    string TopGoalId,
    IReadOnlyList<string> BlockedGoals,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record GoalProgressReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<GoalProgress> Goals,
    IReadOnlyDictionary<string, double> ProgressSummary,
    IReadOnlyList<string> BlockedGoals,
    IReadOnlyList<string> TopNextActions,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record GoalOutcomeEvaluation(
    string OutcomeId,
    string GoalId,
    string TaskId,
    string NeedId,
    DateTimeOffset EvaluatedAtUtc,
    double GoalDelta,
    string Recommendation,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> Notes,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record PriorityScore(
    double Impact,
    double Urgency,
    double Confidence,
    double Cost,
    double Risk,
    double ExpectedLearningValue,
    double GoalPriority,
    double RedundancyPenalty,
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
    string SupportingGoalId,
    string GoalReason,
    double ExpectedGoalDelta,
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
