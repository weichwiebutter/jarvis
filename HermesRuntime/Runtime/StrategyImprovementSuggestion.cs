namespace Hermes.Runtime;

public sealed record StrategyImprovementSuggestion(
    string SuggestionId,
    string Priority,
    string Title,
    string Description,
    string TargetMetric,
    IReadOnlyList<string> RelatedRejectionReasons,
    string ExpectedImpact);
