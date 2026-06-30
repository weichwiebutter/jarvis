using System.Globalization;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record MultiSourceEvidenceCandidate(
    string KnowledgeId,
    string Domain,
    string Title,
    string CurrentStatus,
    int CurrentSourceCount,
    int SourceTypeCount,
    string SourceTypeNeeded,
    double TrustScore,
    double QualityScore,
    double ValidationScore,
    int OpenValidationPlans,
    IReadOnlyList<string> MissingEvidenceTypes,
    IReadOnlyList<string> RecommendedQueries,
    bool HasLocalAlternativeSources,
    bool WouldUpdateSourceConfirmations,
    bool WouldCreateResearchQueueItem,
    string? ResearchQueueItemId,
    string? Query,
    double PriorityScore);

public sealed record MultiSourceEvidencePlanReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int ItemsNeedingSecondSource,
    int PrioritizedItems,
    int UpdatedSourceConfirmations,
    int CreatedResearchQueueItems,
    IReadOnlyList<MultiSourceEvidenceCandidate> PrioritizedCandidates,
    IReadOnlyDictionary<string, int> SourceTypeNeededDistribution,
    IReadOnlyDictionary<string, int> MissingEvidenceDistribution,
    IReadOnlyList<string> RecommendedQueries,
    IReadOnlyList<string> Warnings,
    string SourceConfirmationsPath,
    string KnowledgeEvidencePath,
    string EvidenceGraphPath,
    string ValidationPlansPath,
    string KnowledgeQualityPath,
    string ResearchQueuePath,
    bool DryRun,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class MultiSourceEvidenceIngestionService
{
    private readonly StoragePaths _storagePaths;
    private readonly KnowledgeQualityEngine _qualityEngine;
    private readonly KnowledgeValidationStrategy _validationStrategy;
    private readonly SourceConfirmationEngine _sourceConfirmationEngine;
    private readonly ResearchQueueService _researchQueueService;

    public MultiSourceEvidenceIngestionService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
        _qualityEngine = new KnowledgeQualityEngine(storagePaths);
        _validationStrategy = new KnowledgeValidationStrategy(storagePaths);
        _sourceConfirmationEngine = new SourceConfirmationEngine(storagePaths);
        _researchQueueService = new ResearchQueueService(storagePaths);
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_trust_promotion");

    public string ReportPath => Path.Combine(Root, "multi_source_evidence_plan.json");

    public string MarkdownPath => Path.Combine(Root, "multi_source_evidence_plan.md");

    public MultiSourceEvidencePlanReport Run(bool apply, bool dryRun)
    {
        if (apply && dryRun)
        {
            throw new InvalidOperationException("Use either dryRun or apply, not both.");
        }

        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var quality = _qualityEngine.LoadOrCreateReport();
        var evidence = LoadOrBuildEvidence();
        var graph = LoadOrBuildGraph();
        var confirmations = _sourceConfirmationEngine.LoadOrBuild();
        var plans = _validationStrategy.LoadPlanReport() ?? _validationStrategy.GeneratePlans(50);
        var planByKnowledgeId = plans.Plans
            .GroupBy(plan => plan.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(plan => plan.Priority).First(), StringComparer.OrdinalIgnoreCase);
        var evidenceByKnowledgeId = evidence.Evidence
            .GroupBy(entry => entry.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(entry => entry.UpdatedAtUtc).First(), StringComparer.OrdinalIgnoreCase);
        var graphByKnowledgeId = graph.EvidenceNodes
            .Where(node => node.NodeType.Equals("knowledge_item", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(node => KnowledgeIdFromNodeId(node.NodeId), node => node, StringComparer.OrdinalIgnoreCase);

        var candidates = quality.Items
            .Where(item => NeedsSecondSource(item, confirmations, evidenceByKnowledgeId, graphByKnowledgeId))
            .Select(item => BuildCandidate(item, planByKnowledgeId.TryGetValue(item.KnowledgeId, out var plan) ? plan : null, evidenceByKnowledgeId.TryGetValue(item.KnowledgeId, out var evidenceEntry) ? evidenceEntry : null, graphByKnowledgeId.TryGetValue(item.KnowledgeId, out var graphNode) ? graphNode : null, confirmations))
            .OrderBy(candidate => DomainRank(candidate.Domain))
            .ThenByDescending(candidate => candidate.OpenValidationPlans > 0)
            .ThenBy(candidate => candidate.PriorityScore)
            .ThenByDescending(candidate => candidate.TrustScore)
            .ThenByDescending(candidate => candidate.QualityScore)
            .ThenBy(candidate => candidate.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var prioritized = candidates.Take(50).ToList();
        var sourceTypeNeededDistribution = prioritized
            .GroupBy(candidate => candidate.SourceTypeNeeded, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var missingEvidenceDistribution = prioritized
            .SelectMany(candidate => candidate.MissingEvidenceTypes)
            .GroupBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        var recommendedQueries = prioritized
            .SelectMany(candidate => candidate.RecommendedQueries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToList();

        var updatedSourceConfirmations = 0;
        var createdResearchQueueItems = 0;
        if (apply && !dryRun)
        {
            var result = ApplyIngestion(prioritized, evidence, graph, confirmations, now);
            updatedSourceConfirmations = result.updatedSourceConfirmations;
            createdResearchQueueItems = result.createdResearchQueueItems;
        }

        var report = new MultiSourceEvidencePlanReport(
            ReportVersion: "multi_source_evidence_plan_v1",
            UpdatedAtUtc: now,
            ItemsNeedingSecondSource: candidates.Count,
            PrioritizedItems: prioritized.Count,
            UpdatedSourceConfirmations: updatedSourceConfirmations,
            CreatedResearchQueueItems: createdResearchQueueItems,
            PrioritizedCandidates: prioritized,
            SourceTypeNeededDistribution: sourceTypeNeededDistribution,
            MissingEvidenceDistribution: missingEvidenceDistribution,
            RecommendedQueries: recommendedQueries,
            Warnings: candidates.Count == 0 ? ["no_items_require_second_independent_source"] : [],
            SourceConfirmationsPath: _sourceConfirmationEngine.ReportPath,
            KnowledgeEvidencePath: _qualityEngine.EvidencePath,
            EvidenceGraphPath: Path.Combine(_storagePaths.Root, "cognitive_core", "evidence_graph.json"),
            ValidationPlansPath: _validationStrategy.PlansPath,
            KnowledgeQualityPath: _qualityEngine.QualityPath,
            ResearchQueuePath: _researchQueueService.QueuePath,
            DryRun: dryRun || !apply,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteReport(report);
        return report;
    }

    private (int updatedSourceConfirmations, int createdResearchQueueItems) ApplyIngestion(
        IReadOnlyList<MultiSourceEvidenceCandidate> candidates,
        KnowledgeEvidenceReport evidence,
        EvidenceGraph graph,
        SourceConfirmationReport confirmations,
        DateTimeOffset now)
    {
        var updates = candidates
            .Where(candidate => candidate.HasLocalAlternativeSources)
            .ToList();

        if (updates.Count > 0)
        {
            var updated = BuildUpdatedSourceConfirmations(confirmations, evidence, graph, updates, now);
            File.WriteAllText(_sourceConfirmationEngine.ReportPath, JsonSerializer.Serialize(updated, JsonDefaults.WriteOptions));
        }

        var queued = 0;
        foreach (var candidate in candidates.Where(candidate => !candidate.HasLocalAlternativeSources))
        {
            var query = candidate.Query ?? candidate.RecommendedQueries.FirstOrDefault() ?? BuildRecommendedQuery(candidate.Domain, candidate.Title);
            _ = _researchQueueService.EnqueueResearchTask(
                domain: candidate.Domain,
                type: "collect_second_independent_source",
                priority: candidate.PriorityScore >= 0.75 ? ResearchPriority.High : ResearchPriority.Normal,
                sourceRefs: [candidate.KnowledgeId],
                requestedBy: "multi_source_evidence_ingestion",
                notes:
                [
                    $"multi_source_candidate:{candidate.KnowledgeId}",
                    $"query:{query}",
                    $"source_type_needed:{candidate.SourceTypeNeeded}",
                    $"current_source_count:{candidate.CurrentSourceCount}",
                    $"missing_evidence:{string.Join('|', candidate.MissingEvidenceTypes)}",
                    "no_trading_execution",
                    "human_review_required"
                ]);
            queued++;
        }

        return (updates.Count, queued);
    }

    private static SourceConfirmationReport BuildUpdatedSourceConfirmations(
        SourceConfirmationReport confirmations,
        KnowledgeEvidenceReport evidence,
        EvidenceGraph graph,
        IReadOnlyList<MultiSourceEvidenceCandidate> updates,
        DateTimeOffset now)
    {
        var evidenceByKnowledge = evidence.Evidence.ToDictionary(entry => entry.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var graphByKnowledge = graph.EvidenceNodes
            .Where(node => node.NodeType.Equals("knowledge_item", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(node => KnowledgeIdFromNodeId(node.NodeId), node => node, StringComparer.OrdinalIgnoreCase);

        var results = confirmations.Results
            .Select(result =>
            {
                if (!updates.Any(candidate => candidate.KnowledgeId.Equals(result.KnowledgeId, StringComparison.OrdinalIgnoreCase)))
                {
                    return result;
                }

                var evidenceEntry = evidenceByKnowledge.GetValueOrDefault(result.KnowledgeId);
                var graphNode = graphByKnowledge.GetValueOrDefault(result.KnowledgeId);
                var combinedSources = evidenceEntry?.SourceIds
                    .Concat(graphNode?.SourceRefs.Select(sourceRef => sourceRef.StartsWith("source:", StringComparison.OrdinalIgnoreCase)
                        ? sourceRef["source:".Length..]
                        : sourceRef) ?? [])
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (combinedSources is null || combinedSources.Count == 0)
                {
                    combinedSources = result.SourceCount > 1 ? [result.KnowledgeId] : [];
                }
                var sourceCount = Math.Max(result.SourceCount, combinedSources.Count);
                var sourceTypeCount = Math.Max(result.SourceTypeCount, combinedSources.Count);
                var level = sourceCount >= 2 ? "multi_source" : result.ConfirmationLevel;
                return result with
                {
                    ConfirmationLevel = level,
                    SourceCount = sourceCount,
                    SourceTypeCount = sourceTypeCount,
                    ConfirmationScore = Math.Min(1, Math.Max(result.ConfirmationScore, 0.35 + sourceCount * 0.1)),
                    EvidenceRefs = result.EvidenceRefs.Concat(combinedSources.Select(source => $"source:{source}")).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    Warnings = result.Warnings.Where(warning => !warning.Equals("second_independent_source_missing", StringComparison.OrdinalIgnoreCase)).ToList()
                };
            })
            .ToList();

        return confirmations with
        {
            UpdatedAtUtc = now,
            ItemsAnalyzed = results.Count,
            Results = results,
            ConfirmationDistribution = results
                .GroupBy(result => result.ConfirmationLevel, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            Warnings = confirmations.Warnings
        };
    }

    private static bool NeedsSecondSource(
        KnowledgeQualityItem item,
        SourceConfirmationReport confirmations,
        IReadOnlyDictionary<string, KnowledgeEvidenceEntry> evidenceByKnowledgeId,
        IReadOnlyDictionary<string, EvidenceNode> graphByKnowledgeId)
    {
        var confirmation = confirmations.Results.FirstOrDefault(result => result.KnowledgeId.Equals(item.KnowledgeId, StringComparison.OrdinalIgnoreCase));
        if (confirmation is not null && confirmation.SourceCount >= 2)
        {
            return false;
        }

        var evidenceSourceCount = evidenceByKnowledgeId.TryGetValue(item.KnowledgeId, out var evidenceEntry)
            ? evidenceEntry.SourceIds.Distinct(StringComparer.OrdinalIgnoreCase).Count()
            : 0;
        if (evidenceSourceCount >= 2)
        {
            return false;
        }

        var graphSourceCount = graphByKnowledgeId.TryGetValue(item.KnowledgeId, out var graphNode)
            ? graphNode.SourceRefs
                .Select(sourceRef => sourceRef.StartsWith("source:", StringComparison.OrdinalIgnoreCase) ? sourceRef["source:".Length..] : sourceRef)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count()
            : 0;

        return graphSourceCount < 2;
    }

    private static MultiSourceEvidenceCandidate BuildCandidate(
        KnowledgeQualityItem item,
        KnowledgeValidationPlan? plan,
        KnowledgeEvidenceEntry? evidenceEntry,
        EvidenceNode? graphNode,
        SourceConfirmationReport confirmations)
    {
        var confirmation = confirmations.Results.FirstOrDefault(result => result.KnowledgeId.Equals(item.KnowledgeId, StringComparison.OrdinalIgnoreCase));
        var sourceCount = evidenceEntry?.SourceIds.Distinct(StringComparer.OrdinalIgnoreCase).Count()
            ?? graphNode?.SourceRefs.Count
            ?? confirmation?.SourceCount
            ?? item.EvidenceRefs.Count(reference => reference.StartsWith("source:", StringComparison.OrdinalIgnoreCase));
        var sourceTypeCount = graphNode?.SourceRefs.Count
            ?? confirmation?.SourceTypeCount
            ?? 1;
        var openValidationPlans = plan is null
            ? 0
            : plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase)
                || plan.Status.Equals("in_progress", StringComparison.OrdinalIgnoreCase)
                    ? 1
                    : 0;
        var missing = new List<string>();
        if (sourceCount < 2)
        {
            missing.Add("second_independent_source_missing");
        }

        if (plan is not null && plan.MissingEvidence.Count > 0)
        {
            missing.AddRange(plan.MissingEvidence);
        }

        if (item.LastValidatedUtc is null)
        {
            missing.Add("fresh_validation_timestamp_missing");
        }

        var recommendedQuery = BuildRecommendedQuery(item);
        var queryList = new List<string> { recommendedQuery };
        if (plan is not null)
        {
            queryList.Add($"validation_plan:{plan.PlanId}");
        }

        var localAlternativeSources = sourceCount >= 2;
        var priorityScore = PriorityScore(item, openValidationPlans);

        return new MultiSourceEvidenceCandidate(
            KnowledgeId: item.KnowledgeId,
            Domain: item.Domain,
            Title: item.Title,
            CurrentStatus: item.LifecycleStatus,
            CurrentSourceCount: sourceCount,
            SourceTypeCount: sourceTypeCount,
            SourceTypeNeeded: SourceTypeNeeded(item.Domain),
            TrustScore: item.TrustScore,
            QualityScore: item.QualityScore,
            ValidationScore: item.ValidationScore,
            OpenValidationPlans: openValidationPlans,
            MissingEvidenceTypes: missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            RecommendedQueries: queryList.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            HasLocalAlternativeSources: localAlternativeSources,
            WouldUpdateSourceConfirmations: localAlternativeSources,
            WouldCreateResearchQueueItem: !localAlternativeSources,
            ResearchQueueItemId: null,
            Query: recommendedQuery,
            PriorityScore: priorityScore);
    }

    private static double PriorityScore(KnowledgeQualityItem item, int openValidationPlans)
    {
        var domainBias = item.Domain.Equals("trading", StringComparison.OrdinalIgnoreCase) ? 0.0 : 0.2;
        var qualityGap = Math.Max(0, 0.64 - item.QualityScore);
        var trustGap = Math.Max(0, 0.64 - item.TrustScore);
        var validationGap = Math.Max(0, 0.6 - item.ValidationScore);
        var planBias = openValidationPlans > 0 ? -0.12 : 0.08;
        return Math.Round(Math.Clamp(domainBias + planBias + qualityGap + trustGap + validationGap, 0, 2), 4);
    }

    private static int DomainRank(string domain) =>
        domain.Equals("trading", StringComparison.OrdinalIgnoreCase) ? 0 : 1;

    private static string SourceTypeNeeded(string domain) =>
        domain.ToLowerInvariant() switch
        {
            "trading" => "independent_trading_source",
            "software" => "independent_software_source",
            "documentation" => "independent_documentation_source",
            "process" => "independent_process_source",
            "research" => "independent_research_source",
            _ => "independent_external_source"
        };

    private static string BuildRecommendedQuery(KnowledgeQualityItem item) =>
        BuildRecommendedQuery(item.Domain, item.Title);

    private static string BuildRecommendedQuery(string domain, string title) =>
        domain.ToLowerInvariant() switch
        {
            "trading" => $"\"{title}\" second independent source site:spotware.com OR site:github.com/spotware",
            "software" => $"\"{title}\" second independent source official docs or upstream repository",
            "documentation" => $"\"{title}\" second independent source official documentation",
            "process" => $"\"{title}\" second independent source process evidence",
            "research" => $"\"{title}\" second independent source research evidence",
            _ => $"\"{title}\" second independent source"
        };

    private static string KnowledgeIdFromNodeId(string nodeId) =>
        nodeId.StartsWith("knowledge:", StringComparison.OrdinalIgnoreCase)
            ? nodeId["knowledge:".Length..]
            : nodeId;

    private KnowledgeEvidenceReport LoadOrBuildEvidence()
    {
        var path = _qualityEngine.EvidencePath;
        if (File.Exists(path))
        {
            try
            {
                var report = JsonSerializer.Deserialize<KnowledgeEvidenceReport>(
                    File.ReadAllText(path),
                    JsonDefaults.SnapshotReadOptions);
                if (report is not null)
                {
                    return report;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
            }
        }

        _ = _qualityEngine.Run();
        return JsonSerializer.Deserialize<KnowledgeEvidenceReport>(
            File.ReadAllText(path),
            JsonDefaults.SnapshotReadOptions) ?? new KnowledgeEvidenceReport(
                ReportVersion: "knowledge_evidence_v1",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Evidence: [],
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true);
    }

    private EvidenceGraph LoadOrBuildGraph()
    {
        var path = Path.Combine(_storagePaths.Root, "cognitive_core", "evidence_graph.json");
        if (File.Exists(path))
        {
            try
            {
                var report = JsonSerializer.Deserialize<EvidenceGraph>(
                    File.ReadAllText(path),
                    JsonDefaults.SnapshotReadOptions);
                if (report is not null)
                {
                    return report;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
            }
        }

        return new EvidenceGraphBuilder(_storagePaths).Build();
    }

    private void WriteReport(MultiSourceEvidencePlanReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(MultiSourceEvidencePlanReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Multi-Source Evidence Plan");
        sb.AppendLine();
        sb.AppendLine($"- Report Version: {report.ReportVersion}");
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Items Needing Second Source: {report.ItemsNeedingSecondSource}");
        sb.AppendLine($"- Prioritized Items: {report.PrioritizedItems}");
        sb.AppendLine($"- Updated Source Confirmations: {report.UpdatedSourceConfirmations}");
        sb.AppendLine($"- Created Research Queue Items: {report.CreatedResearchQueueItems}");
        sb.AppendLine();
        sb.AppendLine("## Source Types Needed");
        foreach (var entry in report.SourceTypeNeededDistribution.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"- {entry.Key}: {entry.Value}");
        }
        sb.AppendLine();
        sb.AppendLine("## Missing Evidence Types");
        foreach (var entry in report.MissingEvidenceDistribution.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
        {
            sb.AppendLine($"- {entry.Key}: {entry.Value}");
        }
        sb.AppendLine();
        sb.AppendLine("## Recommended Queries");
        foreach (var query in report.RecommendedQueries)
        {
            sb.AppendLine($"- {query}");
        }
        sb.AppendLine();
        foreach (var candidate in report.PrioritizedCandidates.Take(20))
        {
            sb.AppendLine($"### {candidate.Title} ({candidate.KnowledgeId})");
            sb.AppendLine($"- Domain: {candidate.Domain}");
            sb.AppendLine($"- Current Source Count: {candidate.CurrentSourceCount}");
            sb.AppendLine($"- Source Type Needed: {candidate.SourceTypeNeeded}");
            sb.AppendLine($"- Open Validation Plans: {candidate.OpenValidationPlans}");
            sb.AppendLine($"- Missing Evidence: {string.Join(", ", candidate.MissingEvidenceTypes)}");
            sb.AppendLine($"- Query: {candidate.Query}");
        }

        return sb.ToString();
    }
}
