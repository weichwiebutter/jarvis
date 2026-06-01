using System.Text.Json;

namespace Hermes.Runtime;

public sealed record AutonomousLoopConfig(
    bool Enabled,
    int MaxTasksPerIteration,
    int MaxOutcomesPerIteration,
    int SleepSecondsBetweenIterations,
    int MaxIdleIterations,
    double MinLearningValueToContinue)
{
    public static AutonomousLoopConfig Default =>
        new(
            Enabled: true,
            MaxTasksPerIteration: 10,
            MaxOutcomesPerIteration: 50,
            SleepSecondsBetweenIterations: 1,
            MaxIdleIterations: 3,
            MinLearningValueToContinue: 0.05);

    public static AutonomousLoopConfig LoadOrDefault(string path)
    {
        if (!File.Exists(path))
        {
            return Default;
        }

        try
        {
            return JsonSerializer.Deserialize<AutonomousLoopConfig>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions) ?? Default;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Default;
        }
    }
}

public sealed record AutonomousLoopIterationSummary(
    string IterationId,
    string RunId,
    int IterationNumber,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string Status,
    string ResourceAction,
    IReadOnlyList<string> ResourceWarnings,
    int CleanupCandidates,
    int NeedsDetected,
    int TasksPlanned,
    int TasksExecuted,
    int TasksCompleted,
    int TasksSkipped,
    int TasksFailed,
    int OutcomesEvaluated,
    double AverageOutcomeUsefulness,
    double AverageOutcomeLearningValue,
    int CognitiveInsights,
    bool WorkPerformed,
    bool Idle,
    string NextAction,
    string? StopReason,
    string? CheckpointPath,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> FeedbackChanges,
    string PlanningStatusPath,
    string PlannedTasksPath,
    string TaskExecutionStatePath,
    string PlannerFeedbackPath,
    string GoalFeedbackPath,
    string CognitiveInsightsPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record AutonomousLoopState(
    string StateVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    string RunId,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? DeadlineUtc,
    int IterationsCompleted,
    int IdleIterations,
    int WorkPerformed,
    double AverageLearningValue,
    string NextAction,
    string? LastIterationId,
    string? LastCheckpointPath,
    string? LastStopReason,
    string StatePath,
    string SummaryPath,
    string LogPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record AutonomousLoopSummary(
    string SummaryVersion,
    DateTimeOffset UpdatedAtUtc,
    string RunId,
    string Status,
    int RequestedIterations,
    double MaxMinutes,
    int IterationsCompleted,
    int IdleIterations,
    int WorkPerformed,
    double AverageLearningValue,
    string NextAction,
    string? StopReason,
    AutonomousLoopIterationSummary? LastIteration,
    IReadOnlyList<AutonomousLoopIterationSummary> RecentIterations,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);
