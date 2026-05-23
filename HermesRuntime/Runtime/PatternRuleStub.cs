namespace Hermes.Runtime;

public sealed record PatternRuleStub(
    string RuleId,
    string Description,
    IReadOnlyList<string> Inputs,
    bool StubOnly);
