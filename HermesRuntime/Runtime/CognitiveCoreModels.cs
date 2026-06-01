namespace Hermes.Runtime;

public sealed record CognitiveDomain(
    string DomainId,
    string Name,
    bool Active,
    string Status);

public sealed record TrustScore(
    double Value,
    string Classification,
    IReadOnlyList<string> Reasons);

public sealed record EvidenceScore(
    double Value,
    string Classification,
    IReadOnlyList<string> EvidenceRefs);

public sealed record ValidationScore(
    double Value,
    string Status,
    IReadOnlyList<string> ValidationRefs);

public sealed record ReuseScore(
    double Value,
    string Classification,
    IReadOnlyList<string> ReuseHints);

public sealed record SourceTrustProfile(
    string TrustLevel,
    double TrustScore,
    string LicenseHint,
    IReadOnlyList<string> RiskFlags);

public sealed record CognitiveSource(
    string SourceId,
    string SourceName,
    string UrlOrPath,
    string Domain,
    string SourceType,
    SourceTrustProfile TrustProfile,
    DateTimeOffset LastCheckedUtc,
    string ExtractionStatus,
    IReadOnlyList<string> ExtractedConcepts,
    IReadOnlyList<string> RiskFlags);

public sealed record CognitiveKnowledgeItem(
    string ItemId,
    string Domain,
    string Title,
    string DescriptionShort,
    IReadOnlyList<string> SourceIds,
    TrustScore Trust,
    EvidenceScore Evidence,
    ValidationScore Validation,
    ReuseScore Reuse,
    IReadOnlyList<string> Tags,
    DateTimeOffset? LastValidatedUtc,
    IReadOnlyList<string> RelatedItems);

public sealed record KnowledgeCatalogItem(
    string Id,
    string Domain,
    string Title,
    string DescriptionShort,
    IReadOnlyList<string> SourceIds,
    double Confidence,
    string ValidationStatus,
    IReadOnlyList<string> Tags,
    DateTimeOffset? LastValidatedUtc,
    IReadOnlyList<string> RelatedItems);

public sealed record CognitiveHypothesis(
    string HypothesisId,
    string Domain,
    string Title,
    string Description,
    IReadOnlyList<string> SourceItemIds,
    string ProposedValidation,
    string Status,
    TrustScore Trust,
    EvidenceScore Evidence,
    bool HumanReviewRequired);

public sealed record CognitiveValidationResult(
    string ValidationId,
    string Domain,
    string ItemOrHypothesisId,
    string Status,
    ValidationScore Validation,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> RiskFlags,
    DateTimeOffset CreatedAtUtc);

public sealed record CognitiveMemoryEntry(
    string EntryId,
    string Domain,
    string EntryType,
    string Summary,
    IReadOnlyList<string> SourceRefs,
    string Status,
    DateTimeOffset CreatedAtUtc,
    bool HumanReviewRequired);

public sealed record CognitiveTask(
    string TaskId,
    string Domain,
    string TaskType,
    string Status,
    string Priority,
    IReadOnlyList<string> SourceRefs,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc);

public sealed record CognitiveInsight(
    string InsightId,
    string Domain,
    string Title,
    string Summary,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> RecommendedActions,
    string Status,
    DateTimeOffset CreatedAtUtc,
    bool NoTradingExecution,
    bool HumanReviewRequired);

public enum ResearchPriority
{
    Low,
    Normal,
    High,
    Critical
}

public sealed record ResearchQueueItem(
    string QueueItemId,
    string Domain,
    string Queue,
    string Type,
    ResearchPriority Priority,
    string Status,
    IReadOnlyList<string> SourceRefs,
    string RequestedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<string> Notes,
    bool NoTradingExecution,
    bool HumanReviewRequired);

public sealed record ResearchQueue(
    string QueueVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ResearchQueueItem> Items,
    bool NoTradingExecution,
    bool HumanReviewRequired);

public sealed record RoleOutput(
    string OutputId,
    string Role,
    string Domain,
    string Status,
    IReadOnlyList<string> StructuredFindings,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> RiskFlags,
    DateTimeOffset CreatedAtUtc);

public sealed record CrossKnowledgeCandidate(
    string CandidateId,
    string Domain,
    IReadOnlyList<string> ItemIds,
    string Combination,
    string HypothesisTitle,
    string ValidationPlan,
    double ExpectedReuseScore);

public sealed record CognitiveStatus(
    string StatusVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<CognitiveDomain> Domains,
    int SourceCount,
    int KnowledgeItemCount,
    int QueueItemCount,
    int InsightCount,
    int MemoryEntryCount,
    IReadOnlyList<string> ActiveDomains,
    IReadOnlyList<string> NextActions,
    string CognitiveRoot,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);
