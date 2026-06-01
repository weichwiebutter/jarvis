namespace Hermes.Runtime;

public sealed record DomainProfile(
    string DomainId,
    string Name,
    bool Active,
    string Description,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastScannedAtUtc,
    string Status,
    IReadOnlyList<string> Tags,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record DomainGoal(
    string GoalId,
    string Domain,
    string Description,
    int Priority,
    string Status);

public sealed record DomainGoals(
    string Domain,
    IReadOnlyList<DomainGoal> Goals);

public sealed record DomainKnowledgeSource(
    string SourceId,
    string Domain,
    string SourceType,
    string PathOrUrl,
    string Description,
    string TrustLevel,
    DateTimeOffset? LastScannedAtUtc,
    IReadOnlyList<string> RiskFlags);

public sealed record DomainKnowledgeSources(
    string Domain,
    IReadOnlyList<DomainKnowledgeSource> Sources);

public sealed record DomainQueueRule(
    string RuleId,
    string Domain,
    string Queue,
    string TaskType,
    string PriorityHint,
    string SafetyRule);

public sealed record DomainQueueRules(
    string Domain,
    IReadOnlyList<DomainQueueRule> Rules);

public sealed record DomainScanResult(
    string Domain,
    DateTimeOffset ScannedAtUtc,
    int SourcesScanned,
    int KnowledgeItems,
    IReadOnlyList<string> OutputPaths,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record DomainStatusEntry(
    string Domain,
    bool Active,
    DateTimeOffset? LastScannedAtUtc,
    int SourceCount,
    int KnowledgeItemCount,
    int OpenNeeds,
    int OpenQueueItems,
    IReadOnlyList<string> NextRecommendedTasks,
    IReadOnlyList<string> Warnings);

public sealed record DomainStatusReport(
    string StatusVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string> ActiveDomains,
    IReadOnlyList<DomainStatusEntry> Domains,
    IReadOnlyList<string> WeakDomains,
    IReadOnlyList<string> StrongDomains,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record DomainInsight(
    string InsightId,
    string Domain,
    string Severity,
    string Title,
    string Summary,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> RecommendedTasks);

public sealed record DomainInsightsReport(
    string StatusVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<DomainInsight> Insights,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);
