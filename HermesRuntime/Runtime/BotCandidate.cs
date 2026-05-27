namespace Hermes.Runtime;

public sealed record BotCandidate(
    string CandidateId,
    string StrategyId,
    string StrategyFamily,
    string? PatternId,
    string Symbol,
    string Timeframe,
    BotCandidateStatus Status,
    BotCandidateCriteria Criteria,
    IReadOnlyList<string> RejectionReasons,
    string NextValidationRecommendation,
    IReadOnlyList<string> OverfitFlags,
    bool NoBotCreated,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    MonteCarloResult? MonteCarlo = null,
    CostStressResult? CostStress = null,
    RiskOfRuinEntry? RiskOfRuin = null);
