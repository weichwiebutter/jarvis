namespace Hermes.Runtime;

public sealed record LongRunResearchJob(
    string JobId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset DeadlineUtc,
    double RequestedHours,
    string RequestedBy,
    bool NoAutoTrading,
    bool HumanReviewRequired);

