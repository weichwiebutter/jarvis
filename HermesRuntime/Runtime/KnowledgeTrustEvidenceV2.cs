using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record EvidenceSourceReference(
    string SourceId,
    string SourceName,
    string UrlOrPath,
    string Domain,
    string SourceType,
    string TrustLevel,
    double TrustScore,
    DateTimeOffset? LastCheckedUtc);

public sealed record EvidenceNode(
    string NodeId,
    string NodeType,
    string Domain,
    string Label,
    string Status,
    double Weight,
    IReadOnlyList<string> SourceRefs,
    IReadOnlyList<string> EvidenceRefs);

public sealed record EvidenceLink(
    string LinkId,
    string FromNodeId,
    string ToNodeId,
    string LinkType,
    double Weight,
    IReadOnlyList<string> EvidenceRefs);

public sealed record EvidenceGraph(
    string GraphVersion,
    DateTimeOffset UpdatedAtUtc,
    int KnowledgeItems,
    int SourceNodes,
    int ValidationNodes,
    int Nodes,
    int Links,
    IReadOnlyList<EvidenceNode> EvidenceNodes,
    IReadOnlyList<EvidenceLink> EvidenceLinks,
    IReadOnlyList<EvidenceSourceReference> Sources,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record ConfirmationResult(
    string KnowledgeId,
    string Domain,
    string ConfirmationLevel,
    double ConfirmationScore,
    int SourceCount,
    int SourceTypeCount,
    int SourceTimeBucketCount,
    int ValidationEvidenceCount,
    bool HumanApproved,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> Warnings,
    int CandidateSourceCount = 0,
    string ReviewStatus = "trusted_ready",
    IReadOnlyList<SourceCandidate>? CandidateSources = null);

public sealed record SourceCandidate(
    string Url,
    string Domain,
    string SourceType,
    string ExcerptOrSummary,
    DateTimeOffset RetrievedAtUtc,
    string EvidenceReason,
    string IndependenceClaim,
    string HumanReviewStatus,
    IReadOnlyList<string> SafetyFlags,
    double SemanticMatchScore = 0,
    double IndependenceScore = 0,
    double EvidenceCoverageScore = 0,
    double ContradictionRisk = 0,
    string EvidenceMatchStatus = "unmatched",
    bool ReadyForHumanSourceReview = false);

public sealed record SourceConfirmationReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int ItemsAnalyzed,
    IReadOnlyDictionary<string, int> ConfirmationDistribution,
    IReadOnlyList<ConfirmationResult> Results,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record ContradictionRecord(
    string ContradictionId,
    string KnowledgeId,
    string Domain,
    string Title,
    string ContradictionType,
    string Severity,
    IReadOnlyList<string> ConflictingValues,
    IReadOnlyList<string> EvidenceRefs,
    string Recommendation,
    DateTimeOffset DetectedAtUtc);

public sealed record ContradictionReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int ContradictionCount,
    IReadOnlyDictionary<string, int> ContradictionsBySeverity,
    IReadOnlyList<ContradictionRecord> Contradictions,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record HumanReviewEvidence(
    string ReviewId,
    string KnowledgeId,
    string Domain,
    string Result,
    string Reviewer,
    string Notes,
    DateTimeOffset ReviewedAtUtc,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record HumanReviewResult(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalReviews,
    int Approved,
    int Rejected,
    int NeedsReview,
    int ReviewedKnowledgeItems,
    IReadOnlyList<HumanReviewEvidence> Reviews,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class EvidenceGraphBuilder
{
    private readonly StoragePaths _storagePaths;

    public EvidenceGraphBuilder(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string GraphPath => Path.Combine(Root, "evidence_graph.json");

    public EvidenceGraph Build()
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var sources = new KnowledgeSourceRegistry(_storagePaths).LoadOrCreateSources();
        var sourcesById = sources.ToDictionary(source => source.SourceId, StringComparer.OrdinalIgnoreCase);
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var validations = new KnowledgeValidationExecutor(_storagePaths).LoadResults(5000);
        var validationByKnowledge = validations
            .Where(result => !string.IsNullOrWhiteSpace(result.KnowledgeItemId))
            .GroupBy(result => result.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Take(24).ToList(), StringComparer.OrdinalIgnoreCase);
        var nodes = new List<EvidenceNode>();
        var links = new List<EvidenceLink>();

        foreach (var item in catalog)
        {
            var knowledgeNodeId = KnowledgeNodeId(item.Id);
            var validationRefs = validationByKnowledge.TryGetValue(item.Id, out var itemValidations)
                ? itemValidations.Select(result => $"validation:{result.ExecutionId}:{result.OutcomeStatus}").ToList()
                : [];
            nodes.Add(new EvidenceNode(
                NodeId: knowledgeNodeId,
                NodeType: "knowledge_item",
                Domain: item.Domain,
                Label: item.Title,
                Status: item.ValidationStatus,
                Weight: Math.Round(Math.Clamp(item.Confidence, 0, 1), 4),
                SourceRefs: item.SourceIds.Select(sourceId => $"source:{sourceId}").ToList(),
                EvidenceRefs: validationRefs.Take(12).ToList()));

            foreach (var sourceId in item.SourceIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var sourceNodeId = SourceNodeId(sourceId);
                if (!nodes.Any(node => node.NodeId.Equals(sourceNodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    var source = sourcesById.GetValueOrDefault(sourceId);
                    nodes.Add(new EvidenceNode(
                        NodeId: sourceNodeId,
                        NodeType: "source",
                        Domain: source?.Domain ?? item.Domain,
                        Label: source?.SourceName ?? sourceId,
                        Status: source?.ExtractionStatus ?? "unknown",
                        Weight: Math.Round(Math.Clamp(source?.TrustProfile.TrustScore ?? 0.35, 0, 1), 4),
                        SourceRefs: [$"source:{sourceId}"],
                        EvidenceRefs: source is null ? ["source_metadata_missing"] : [$"source_type:{source.SourceType}", $"trust:{source.TrustProfile.TrustLevel}:{source.TrustProfile.TrustScore:0.####}"]));
                }

                links.Add(Link(knowledgeNodeId, sourceNodeId, "supported_by_source", sourceId, sourceRefs: [$"source:{sourceId}"]));
            }

            foreach (var relatedId in item.RelatedItems.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                links.Add(Link(knowledgeNodeId, KnowledgeNodeId(relatedId), "related_knowledge", relatedId, sourceRefs: [$"related:{relatedId}"]));
            }

            if (validationByKnowledge.TryGetValue(item.Id, out var results))
            {
                foreach (var result in results.Take(12))
                {
                    var validationNodeId = ValidationNodeId(result.ExecutionId);
                    nodes.Add(new EvidenceNode(
                        NodeId: validationNodeId,
                        NodeType: "validation",
                        Domain: result.Domain,
                        Label: result.RequirementType,
                        Status: $"{result.Status}:{result.OutcomeStatus}",
                        Weight: ValidationWeight(result),
                        SourceRefs: [$"queue:{result.QueueItemId}", $"plan:{result.PlanId}"],
                        EvidenceRefs: result.EvidenceRefs.Take(16).ToList()));
                    links.Add(Link(knowledgeNodeId, validationNodeId, "validated_by", result.ExecutionId, result.EvidenceRefs));
                }
            }
        }

        var graph = new EvidenceGraph(
            GraphVersion: "evidence_graph_v2",
            UpdatedAtUtc: now,
            KnowledgeItems: catalog.Count,
            SourceNodes: nodes.Count(node => node.NodeType.Equals("source", StringComparison.OrdinalIgnoreCase)),
            ValidationNodes: nodes.Count(node => node.NodeType.Equals("validation", StringComparison.OrdinalIgnoreCase)),
            Nodes: nodes.GroupBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase).Count(),
            Links: links.GroupBy(link => link.LinkId, StringComparer.OrdinalIgnoreCase).Count(),
            EvidenceNodes: nodes
                .GroupBy(node => node.NodeId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.OrderByDescending(node => node.Weight).First())
                .OrderBy(node => node.NodeType, StringComparer.Ordinal)
                .ThenBy(node => node.Domain, StringComparer.Ordinal)
                .ThenBy(node => node.NodeId, StringComparer.Ordinal)
                .ToList(),
            EvidenceLinks: links
                .GroupBy(link => link.LinkId, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(link => link.LinkType, StringComparer.Ordinal)
                .ThenBy(link => link.FromNodeId, StringComparer.Ordinal)
                .ToList(),
            Sources: sources
                .Select(source => new EvidenceSourceReference(
                    SourceId: source.SourceId,
                    SourceName: source.SourceName,
                    UrlOrPath: source.UrlOrPath,
                    Domain: source.Domain,
                    SourceType: source.SourceType,
                    TrustLevel: source.TrustProfile.TrustLevel,
                    TrustScore: source.TrustProfile.TrustScore,
                    LastCheckedUtc: source.LastCheckedUtc))
                .ToList(),
            Warnings: catalog.Count == 0 ? ["knowledge_catalog_empty"] : [],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(GraphPath, JsonSerializer.Serialize(graph, JsonDefaults.WriteOptions));
        return graph;
    }

    public EvidenceGraph? LoadGraph()
    {
        if (!File.Exists(GraphPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<EvidenceGraph>(
                File.ReadAllText(GraphPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static string KnowledgeNodeId(string id) => $"knowledge:{id}";

    private static string SourceNodeId(string id) => $"source:{id}";

    private static string ValidationNodeId(string id) => $"validation:{id}";

    private static EvidenceLink Link(string from, string to, string type, string seed, IReadOnlyList<string> sourceRefs)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{from}|{to}|{type}|{seed}"))).ToLowerInvariant()[..12];
        return new EvidenceLink(
            LinkId: $"evidence_link_{hash}",
            FromNodeId: from,
            ToNodeId: to,
            LinkType: type,
            Weight: type.Equals("validated_by", StringComparison.OrdinalIgnoreCase) ? 0.78 : 0.52,
            EvidenceRefs: sourceRefs.Take(16).ToList());
    }

    private static double ValidationWeight(KnowledgeValidationExecutionResult result)
    {
        if (result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
        {
            return result.OutcomeStatus.Contains("confirmed", StringComparison.OrdinalIgnoreCase)
                || result.OutcomeStatus.Contains("available", StringComparison.OrdinalIgnoreCase)
                || result.OutcomeStatus.Contains("validated", StringComparison.OrdinalIgnoreCase)
                ? 0.82
                : 0.62;
        }

        if (result.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase))
        {
            return 0.34;
        }

        return result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase) ? 0.12 : 0.22;
    }
}

public sealed class SourceConfirmationEngine
{
    private readonly StoragePaths _storagePaths;

    public SourceConfirmationEngine(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string ReportPath => Path.Combine(Root, "source_confirmations.json");

    public SourceConfirmationReport Build()
    {
        Directory.CreateDirectory(Root);
        var existing = LoadReport();
        var existingById = existing?.Results.ToDictionary(result => result.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, ConfirmationResult>(StringComparer.OrdinalIgnoreCase);
        var sources = new KnowledgeSourceRegistry(_storagePaths).LoadOrCreateSources();
        var sourcesById = sources.ToDictionary(source => source.SourceId, StringComparer.OrdinalIgnoreCase);
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var validations = new KnowledgeValidationExecutor(_storagePaths).LoadResults(5000);
        var reviews = new HumanReviewEvidenceStore(_storagePaths).LoadOrCreateReport().Reviews;
        var results = catalog
            .Select(item => BuildResult(item, sourcesById, validations, reviews, existingById.GetValueOrDefault(item.Id)))
            .OrderByDescending(item => item.ConfirmationScore)
            .ThenBy(item => item.Domain, StringComparer.Ordinal)
            .ThenBy(item => item.KnowledgeId, StringComparer.Ordinal)
            .ToList();
        var distribution = results
            .GroupBy(result => result.ConfirmationLevel, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var report = new SourceConfirmationReport(
            ReportVersion: "source_confirmation_v2",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ItemsAnalyzed: results.Count,
            ConfirmationDistribution: distribution,
            Results: results,
            Warnings: results.Count == 0 ? ["knowledge_catalog_empty"] : [],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    public SourceConfirmationReport? LoadReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SourceConfirmationReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public SourceConfirmationReport LoadOrBuild() => LoadReport() ?? Build();

    private static ConfirmationResult BuildResult(
        KnowledgeCatalogItem item,
        IReadOnlyDictionary<string, CognitiveSource> sourcesById,
        IReadOnlyList<KnowledgeValidationExecutionResult> validations,
        IReadOnlyList<HumanReviewEvidence> reviews,
        ConfirmationResult? existing = null)
    {
        var itemSources = item.SourceIds
            .Select(sourceId => sourcesById.TryGetValue(sourceId, out var source) ? source : null)
            .Where(source => source is not null)
            .Cast<CognitiveSource>()
            .ToList();
        var sourceTypes = itemSources
            .Select(source => source.SourceType)
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var sourceTimeBuckets = itemSources
            .Select(source => $"{source.LastCheckedUtc:yyyy-MM}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var itemValidations = validations
            .Where(result => result.KnowledgeItemId.Equals(item.Id, StringComparison.OrdinalIgnoreCase)
                && !result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var validationEvidence = itemValidations.Count(result =>
            result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)
            || result.Status.Equals("needs_more_data", StringComparison.OrdinalIgnoreCase));
        var humanApproved = reviews
            .Where(review => review.KnowledgeId.Equals(item.Id, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(review => review.ReviewedAtUtc)
            .FirstOrDefault()
            ?.Result.Equals("approved", StringComparison.OrdinalIgnoreCase) == true;
        var sourceCount = item.SourceIds.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var score = Math.Round(Math.Clamp(
            Math.Min(0.28, sourceCount * 0.09)
            + Math.Min(0.18, sourceTypes * 0.08)
            + Math.Min(0.12, sourceTimeBuckets * 0.05)
            + Math.Min(0.28, validationEvidence * 0.05)
            + (humanApproved ? 0.14 : 0),
            0,
            1), 4);
        var level = score switch
        {
            >= 0.82 when humanApproved && sourceTypes >= 2 && validationEvidence >= 2 => "trusted",
            >= 0.68 when validationEvidence > 0 => "validated",
            >= 0.52 when sourceTypes >= 2 => "cross_source",
            >= 0.36 when sourceCount >= 2 => "multi_source",
            _ => "single_source"
        };
        var evidenceRefs = item.SourceIds
            .Select(sourceId => $"source:{sourceId}")
            .Concat(itemValidations.Take(12).Select(result => $"validation:{result.ExecutionId}:{result.OutcomeStatus}"))
            .Concat(humanApproved ? [$"human_review:approved:{item.Id}"] : [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToList();
        var warnings = new List<string>();
        if (sourceCount < 2)
        {
            warnings.Add("second_independent_source_missing");
        }

        if (validationEvidence == 0)
        {
            warnings.Add("validation_evidence_missing");
        }

        return new ConfirmationResult(
            KnowledgeId: item.Id,
            Domain: item.Domain,
            ConfirmationLevel: level,
            ConfirmationScore: score,
            SourceCount: sourceCount,
            SourceTypeCount: sourceTypes,
            SourceTimeBucketCount: sourceTimeBuckets,
            ValidationEvidenceCount: validationEvidence,
            HumanApproved: humanApproved,
            EvidenceRefs: evidenceRefs,
            Warnings: warnings,
            CandidateSourceCount: existing?.CandidateSourceCount ?? 0,
            ReviewStatus: existing?.ReviewStatus ?? (sourceCount >= 2 ? "candidate_second_source" : "awaiting_human_review"),
            CandidateSources: existing?.CandidateSources ?? []);
    }
}

public sealed class ContradictionDetector
{
    private readonly StoragePaths _storagePaths;

    public ContradictionDetector(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string ContradictionsPath => Path.Combine(Root, "contradictions.json");

    public ContradictionReport Run()
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var validations = new KnowledgeValidationExecutor(_storagePaths).LoadResults(5000);
        var reviews = new HumanReviewEvidenceStore(_storagePaths).LoadOrCreateReport().Reviews;
        var records = new List<ContradictionRecord>();

        records.AddRange(DetectDuplicateClaimConflicts(catalog, now));
        records.AddRange(DetectValidationConflicts(catalog, validations, now));
        records.AddRange(DetectHumanReviewConflicts(catalog, reviews, validations, now));

        var distinct = records
            .GroupBy(record => record.ContradictionId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(record => record.Domain, StringComparer.Ordinal)
            .ThenBy(record => record.Title, StringComparer.Ordinal)
            .Take(500)
            .ToList();
        var report = new ContradictionReport(
            ReportVersion: "contradictions_v2",
            UpdatedAtUtc: now,
            ContradictionCount: distinct.Count,
            ContradictionsBySeverity: distinct
                .GroupBy(record => record.Severity, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            Contradictions: distinct,
            Warnings: distinct.Count == 0 ? [] : [$"contradictions_detected:{distinct.Count}"],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(ContradictionsPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    public ContradictionReport? LoadReport()
    {
        if (!File.Exists(ContradictionsPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ContradictionReport>(
                File.ReadAllText(ContradictionsPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public ContradictionReport LoadOrRun() => LoadReport() ?? Run();

    private static IReadOnlyList<ContradictionRecord> DetectDuplicateClaimConflicts(
        IReadOnlyList<KnowledgeCatalogItem> catalog,
        DateTimeOffset now)
    {
        return catalog
            .GroupBy(item => $"{item.Domain}|{Normalize(item.Title)}", StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Where(group => group.Select(item => item.ValidationStatus).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(group =>
            {
                var first = group.First();
                var values = group
                    .Select(item => $"{item.Id}:{item.ValidationStatus}:confidence={item.Confidence:0.####}")
                    .ToList();
                return Record(
                    first.Id,
                    first.Domain,
                    first.Title,
                    "duplicate_claim_validation_conflict",
                    values.Any(value => value.Contains("rejected", StringComparison.OrdinalIgnoreCase)) ? "high" : "medium",
                    values,
                    group.Select(item => $"knowledge:{item.Id}").ToList(),
                    "review_duplicate_claims_and_resolve_validation_status",
                    now);
            })
            .ToList();
    }

    private static IReadOnlyList<ContradictionRecord> DetectValidationConflicts(
        IReadOnlyList<KnowledgeCatalogItem> catalog,
        IReadOnlyList<KnowledgeValidationExecutionResult> validations,
        DateTimeOffset now)
    {
        var catalogById = catalog.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        return validations
            .Where(result => !string.IsNullOrWhiteSpace(result.KnowledgeItemId))
            .GroupBy(result => result.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .Where(group =>
                group.Any(result => result.OutcomeStatus.Contains("confirmed", StringComparison.OrdinalIgnoreCase)
                    || result.OutcomeStatus.Contains("available", StringComparison.OrdinalIgnoreCase)
                    || result.OutcomeStatus.Contains("validated", StringComparison.OrdinalIgnoreCase))
                && group.Any(result => result.OutcomeStatus.Contains("missing", StringComparison.OrdinalIgnoreCase)
                    || result.OutcomeStatus.Contains("failed", StringComparison.OrdinalIgnoreCase)
                    || result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)))
            .Select(group =>
            {
                var item = catalogById.GetValueOrDefault(group.Key);
                var values = group
                    .OrderByDescending(result => result.CompletedAtUtc)
                    .Take(8)
                    .Select(result => $"{result.RequirementType}:{result.Status}:{result.OutcomeStatus}")
                    .ToList();
                return Record(
                    group.Key,
                    item?.Domain ?? group.First().Domain,
                    item?.Title ?? group.Key,
                    "validation_outcome_conflict",
                    values.Any(value => value.Contains("failed", StringComparison.OrdinalIgnoreCase)) ? "medium" : "low",
                    values,
                    group.Take(12).Select(result => $"validation:{result.ExecutionId}:{result.OutcomeStatus}").ToList(),
                    "prioritize_revalidation_or_human_review",
                    now);
            })
            .ToList();
    }

    private static IReadOnlyList<ContradictionRecord> DetectHumanReviewConflicts(
        IReadOnlyList<KnowledgeCatalogItem> catalog,
        IReadOnlyList<HumanReviewEvidence> reviews,
        IReadOnlyList<KnowledgeValidationExecutionResult> validations,
        DateTimeOffset now)
    {
        var validationById = validations
            .GroupBy(result => result.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        return catalog
            .Select(item =>
            {
                var latestReview = reviews
                    .Where(review => review.KnowledgeId.Equals(item.Id, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(review => review.ReviewedAtUtc)
                    .FirstOrDefault();
                if (latestReview is null)
                {
                    return null;
                }

                var relatedValidation = validationById.GetValueOrDefault(item.Id) ?? [];
                var hasPositiveValidation = relatedValidation.Any(result =>
                    result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)
                    && (result.OutcomeStatus.Contains("confirmed", StringComparison.OrdinalIgnoreCase)
                        || result.OutcomeStatus.Contains("available", StringComparison.OrdinalIgnoreCase)
                        || result.OutcomeStatus.Contains("validated", StringComparison.OrdinalIgnoreCase)));
                var hasNegativeValidation = relatedValidation.Any(result =>
                    result.OutcomeStatus.Contains("missing", StringComparison.OrdinalIgnoreCase)
                    || result.OutcomeStatus.Contains("failed", StringComparison.OrdinalIgnoreCase)
                    || result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase));
                var reviewRejectedPositive = latestReview.Result.Equals("rejected", StringComparison.OrdinalIgnoreCase) && hasPositiveValidation;
                var reviewApprovedNegative = latestReview.Result.Equals("approved", StringComparison.OrdinalIgnoreCase) && hasNegativeValidation;
                if (!reviewRejectedPositive && !reviewApprovedNegative)
                {
                    return null;
                }

                var values = new List<string> { $"human_review:{latestReview.Result}:{latestReview.ReviewedAtUtc:O}" };
                values.AddRange(relatedValidation.Take(8).Select(result => $"{result.RequirementType}:{result.Status}:{result.OutcomeStatus}"));
                return Record(
                    item.Id,
                    item.Domain,
                    item.Title,
                    "human_review_validation_conflict",
                    "high",
                    values,
                    [$"human_review:{latestReview.ReviewId}", .. relatedValidation.Take(8).Select(result => $"validation:{result.ExecutionId}:{result.OutcomeStatus}")],
                    "resolve_with_human_review_before_promoting_trust",
                    now);
            })
            .Where(record => record is not null)
            .Cast<ContradictionRecord>()
            .ToList();
    }

    private static ContradictionRecord Record(
        string knowledgeId,
        string domain,
        string title,
        string type,
        string severity,
        IReadOnlyList<string> values,
        IReadOnlyList<string> evidence,
        string recommendation,
        DateTimeOffset now)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{knowledgeId}|{type}|{string.Join('|', values)}"))).ToLowerInvariant()[..12];
        return new ContradictionRecord(
            ContradictionId: $"contradiction_{hash}",
            KnowledgeId: knowledgeId,
            Domain: domain,
            Title: title,
            ContradictionType: type,
            Severity: severity,
            ConflictingValues: values,
            EvidenceRefs: evidence,
            Recommendation: recommendation,
            DetectedAtUtc: now);
    }

    private static string Normalize(string value)
    {
        var chars = value.ToLowerInvariant()
            .Where(character => char.IsLetterOrDigit(character) || char.IsWhiteSpace(character))
            .ToArray();
        return string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}

public sealed class HumanReviewEvidenceStore
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReviewPath;

    public HumanReviewEvidenceStore(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string ReviewPath => _resolvedReviewPath ?? Path.Combine(Root, "human_review_evidence.json");

    public HumanReviewResult AddReview(string knowledgeId, string result, string reviewer, string notes)
    {
        _resolvedReviewPath = ResolveWritablePath();
        var catalogItem = new KnowledgeCatalog(_storagePaths).FindById(knowledgeId);
        var normalizedResult = NormalizeReviewResult(result);
        var current = LoadOrCreateReport();
        var review = new HumanReviewEvidence(
            ReviewId: $"human_review_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            KnowledgeId: knowledgeId,
            Domain: catalogItem?.Domain ?? "unknown",
            Result: normalizedResult,
            Reviewer: string.IsNullOrWhiteSpace(reviewer) ? "human" : reviewer.Trim(),
            Notes: string.IsNullOrWhiteSpace(notes) ? "cli_review_recorded" : notes.Trim(),
            ReviewedAtUtc: DateTimeOffset.UtcNow,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        return Write(current.Reviews.Concat([review]).ToList(), catalogItem is null ? [$"knowledge_item_not_found:{knowledgeId}"] : []);
    }

    public HumanReviewResult LoadOrCreateReport()
    {
        var path = ResolveWritablePath();
        _resolvedReviewPath = path;
        if (File.Exists(path))
        {
            try
            {
                return JsonSerializer.Deserialize<HumanReviewResult>(
                    File.ReadAllText(path),
                    JsonDefaults.SnapshotReadOptions) ?? Empty();
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                return Empty(["human_review_report_unreadable"]);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var empty = Empty();
        File.WriteAllText(path, JsonSerializer.Serialize(empty, JsonDefaults.WriteOptions));
        return empty;
    }

    public HumanReviewEvidence? LatestFor(string knowledgeId) =>
        LoadOrCreateReport().Reviews
            .Where(review => review.KnowledgeId.Equals(knowledgeId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(review => review.ReviewedAtUtc)
            .FirstOrDefault();

    private HumanReviewResult Write(IReadOnlyList<HumanReviewEvidence> reviews, IReadOnlyList<string> warnings)
    {
        var report = new HumanReviewResult(
            ReportVersion: "human_review_evidence_v2",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TotalReviews: reviews.Count,
            Approved: reviews.Count(review => review.Result.Equals("approved", StringComparison.OrdinalIgnoreCase)),
            Rejected: reviews.Count(review => review.Result.Equals("rejected", StringComparison.OrdinalIgnoreCase)),
            NeedsReview: reviews.Count(review => review.Result.Equals("needs_review", StringComparison.OrdinalIgnoreCase)),
            ReviewedKnowledgeItems: reviews.Select(review => review.KnowledgeId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            Reviews: reviews
                .OrderByDescending(review => review.ReviewedAtUtc)
                .Take(5000)
                .ToList(),
            Warnings: warnings,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        var path = _resolvedReviewPath ?? ResolveWritablePath();
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    private HumanReviewResult Empty(IReadOnlyList<string>? warnings = null) =>
        new(
            ReportVersion: "human_review_evidence_v2",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TotalReviews: 0,
            Approved: 0,
            Rejected: 0,
            NeedsReview: 0,
            ReviewedKnowledgeItems: 0,
            Reviews: [],
            Warnings: warnings ?? [],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

    private static string NormalizeReviewResult(string result) =>
        result.Trim().ToLowerInvariant() switch
        {
            "approved" => "approved",
            "rejected" => "rejected",
            _ => "needs_review"
        };

    private string ResolveWritablePath()
    {
        var primaryRoot = Root;
        try
        {
            Directory.CreateDirectory(primaryRoot);
            return Path.Combine(primaryRoot, "human_review_evidence.json");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "cognitive_core");
            Directory.CreateDirectory(fallbackRoot);
            return Path.Combine(fallbackRoot, "human_review_evidence.json");
        }
    }
}
