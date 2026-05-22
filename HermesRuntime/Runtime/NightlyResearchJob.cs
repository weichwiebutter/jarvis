namespace Hermes.Runtime;

public sealed record NightlyResearchJob(
    string JobId,
    DateTimeOffset ScheduledForUtc,
    DateTimeOffset StartedAtUtc,
    string RequestedBy,
    string Mode,
    IReadOnlyList<string> Symbols,
    IReadOnlyList<string> Timeframes,
    bool NoAutoTrading,
    bool HumanReviewRequired);
