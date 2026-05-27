namespace Hermes.Runtime;

public sealed record BotCandidateReport(
    string ReportId,
    DateTimeOffset CreatedAtUtc,
    int StrategiesEvaluated,
    int BotCandidateCount,
    int DemoBotCandidateCount,
    int PromisingCandidateCount,
    int RobustCandidateCount,
    int RejectedCandidateCount,
    IReadOnlyList<BotCandidate> Candidates,
    IReadOnlyList<BotCandidate> RejectedCandidates,
    IReadOnlyList<string> TopDemoBotCandidates,
    IReadOnlyList<string> NextValidationRecommendations,
    IReadOnlyDictionary<string, int> RejectionReasonCounts,
    string BotCandidatesPath,
    string RejectedCandidatesPath,
    bool NoBotCreated,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);
