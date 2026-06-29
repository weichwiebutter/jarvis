namespace HermesPaperBot.Models;

/// <summary>
/// Parsed paper-only signal candidate.
/// </summary>
public sealed class SignalCandidate
{
    public string SignalId { get; init; } = string.Empty;
    public string Asset { get; init; } = string.Empty;
    public string Timeframe { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty;
    public string SetupId { get; init; } = string.Empty;
    public string SetupName { get; init; } = string.Empty;
    public string PrimaryCandidate { get; init; } = string.Empty;
    public string Readiness { get; init; } = string.Empty;
    public bool PaperEntryEnabled { get; init; } = false;
    public decimal ConfidenceBaseline { get; init; } = 0m;
    public decimal MaxSpread { get; init; } = 0.25m;
    public decimal StopLossR { get; init; } = 1m;
    public decimal TakeProfitR { get; init; } = 1m;
    public string[] EntryLogic { get; init; } = [];
    public string[] ExitLogic { get; init; } = [];
    public string[] StopLossLogic { get; init; } = [];
    public string[] TakeProfitLogic { get; init; } = [];
    public string[] InvalidationLogic { get; init; } = [];
    public string[] MarketRegimeTags { get; init; } = [];
    public string[] SessionTags { get; init; } = [];
    public string[] RiskNotes { get; init; } = [];
    public string[] ValidationWarnings { get; init; } = [];
    public DateTimeOffset? ExpiresAtUtc { get; init; }
}
