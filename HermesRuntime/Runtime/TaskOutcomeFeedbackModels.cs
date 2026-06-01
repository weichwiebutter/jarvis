namespace Hermes.Runtime;

public sealed record TaskOutcomeScore(
    double UsefulnessScore,
    double LearningValue,
    double CostScore,
    double RiskScore,
    double RedundancyScore,
    string Recommendation);

public sealed record TaskOutcomeEvidence(
    bool NeedReduced,
    bool GoalImproved,
    bool NewInsightsGenerated,
    bool ResearchQueueChanged,
    bool OutputEvidenceAvailable,
    int WarningCount,
    bool TaskFailed,
    bool TaskSkipped,
    bool TaskRedundant,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> Notes);

public sealed record TaskOutcomeResult(
    string OutcomeId,
    string TaskId,
    string TaskType,
    string NeedId,
    string GoalId,
    DateTimeOffset ExecutedAtUtc,
    DateTimeOffset EvaluatedAtUtc,
    TaskOutcomeScore OutcomeScore,
    TaskOutcomeEvidence Evidence,
    string Recommendation,
    IReadOnlyList<string> FollowupTaskIds,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record PlannerTaskTypeFeedback(
    string TaskType,
    int Evaluations,
    double AverageUsefulnessScore,
    double AverageLearningValue,
    double AverageCostScore,
    double AverageRiskScore,
    double AverageRedundancyScore,
    double PriorityAdjustment,
    string Recommendation,
    IReadOnlyList<string> RepeatedUnsuccessfulNeeds,
    DateTimeOffset LastEvaluatedUtc);

public sealed record PlannerFeedback(
    string FeedbackVersion,
    DateTimeOffset UpdatedAtUtc,
    int OutcomesEvaluated,
    IReadOnlyList<PlannerTaskTypeFeedback> TaskTypeFeedback,
    IReadOnlyList<string> RetiredTaskTypes,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record GoalFeedbackEntry(
    string GoalId,
    int Evaluations,
    double AverageUsefulnessScore,
    double ProgressDelta,
    IReadOnlyList<string> ImprovedNeeds,
    IReadOnlyList<string> PersistentNeeds,
    IReadOnlyList<string> RecommendedActions,
    DateTimeOffset LastEvaluatedUtc);

public sealed record GoalFeedback(
    string FeedbackVersion,
    DateTimeOffset UpdatedAtUtc,
    int OutcomesEvaluated,
    IReadOnlyList<GoalFeedbackEntry> Goals,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record OutcomeFeedbackStatus(
    string StatusVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalOutcomes,
    DateTimeOffset? LastOutcomeUtc,
    int OutcomesEvaluatedLastRun,
    string TaskOutcomesPath,
    string PlannerFeedbackPath,
    string GoalFeedbackPath,
    IReadOnlyList<string> LatestRecommendations,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);
