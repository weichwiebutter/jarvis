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
    IReadOnlyList<PatternTag> Tags,
    string? SourceUrl = null,
    string? SourceName = null,
    string? Category = null,
    string? DescriptionShort = null,
    string? MarketContext = null,
    IReadOnlyList<string>? PossibleTimeframes = null,
    string? TriggerRuleStub = null,
    string? InvalidationRuleStub = null,
    string? TestPriority = null,
    string? SourceTrust = null);
