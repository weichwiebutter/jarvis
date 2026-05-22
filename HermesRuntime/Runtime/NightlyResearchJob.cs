namespace Hermes.Runtime;

public sealed record NightlyResearchJob(
    string JobId,
    DateTimeOffset ScheduledForUtc,
    DateTimeOffset StartedAtUtc,
    string RequestedBy,
    string Mode,
    bool NoAutoTrading,
    bool HumanReviewRequired);
