namespace Hermes.Runtime;

public sealed record PlannedTaskExecutionResult(
    string TaskId,
    string TaskType,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string Status,
    string Reason,
    string NeedId,
    string GoalId,
    IReadOnlyList<string> OutputPaths,
    IReadOnlyList<string> Warnings,
    string? SkippedReason,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record PlannedTaskExecutionState(
    string StateVersion,
    DateTimeOffset UpdatedAtUtc,
    int PendingTasks,
    int RunningTasks,
    int CompletedTasks,
    int SkippedTasks,
    int FailedTasks,
    string? RunningTaskId,
    string? LastTaskId,
    DateTimeOffset? LastExecutionUtc,
    string LastStatus,
    string ExecutionLogPath,
    IReadOnlyList<PlannedTaskExecutionResult> RecentResults,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);
