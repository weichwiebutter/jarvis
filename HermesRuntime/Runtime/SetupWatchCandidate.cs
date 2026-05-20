namespace Hermes.Runtime;

public sealed record SetupWatchCandidate(
    string SetupId,
    string Symbol,
    string Bias,
    SetupWatchStatus Status,
    decimal Confidence,
    string EntryZone,
    string SuggestedStopLoss,
    string SuggestedTarget,
    string TriggerCondition,
    string InvalidationLevel,
    int TimeWindowMinutes,
    string Notes,
    DateTimeOffset CreatedAtUtc);
