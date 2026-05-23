namespace Hermes.Runtime;

public sealed record StrategyPatternDefinition(
    string Id,
    string Name,
    string DirectionBias,
    string Description,
    IReadOnlyList<string> RequiredTimeframes,
    IReadOnlyList<string> PreferredSessions,
    IReadOnlyList<string> MarketRegimes,
    IReadOnlyList<PatternRuleStub> TriggerRules,
    IReadOnlyList<PatternRuleStub> InvalidationRules,
    string RiskModelHint,
    IReadOnlyList<PatternTag> Tags);
