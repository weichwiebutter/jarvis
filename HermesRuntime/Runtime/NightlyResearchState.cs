namespace Hermes.Runtime;

public sealed record NightlyResearchState(
    string StateVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    string RunId,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? DeadlineUtc,
    int IterationsCompleted,
    int IdleIterations,
    int WorkPerformed,
    string NextAction,
    string? LastCheckpointPath,
    string? LastAutopilotReportPath,
    string? LastError,
    bool NoAutoTrading,
    bool HumanReviewRequired);
