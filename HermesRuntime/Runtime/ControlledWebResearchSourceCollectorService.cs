using System.Text.Json;

namespace Hermes.Runtime;

public sealed record WebResearchSourceRequest(
    string RequestId,
    string KnowledgeItemId,
    string Domain,
    string Query,
    IReadOnlyList<string> RecommendedSourceDomains,
    string Reason,
    int CurrentSourceCount,
    IReadOnlyList<string> RequiredEvidence,
    string Status,
    bool HumanReviewRequired,
    DateTimeOffset CreatedAtUtc);

public sealed record WebResearchSourceCollectorReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalSecondSourceItems,
    int ExportedSearchRequests,
    int AwaitingExternalSearch,
    int AlreadyHasCandidateSource,
    int BlockedNoWebRuntime,
    IReadOnlyList<WebResearchSourceRequest> Requests,
    IReadOnlyList<string> Warnings,
    string QueuePath,
    string ReportPath,
    string MarkdownPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class ControlledWebResearchSourceCollectorService
{
    private readonly StoragePaths _storagePaths;
    private readonly ResearchQueueService _queueService;
    private readonly KnowledgeQualityEngine _qualityEngine;
    private readonly KnowledgeValidationStrategy _validationStrategy;
    private readonly SourceConfirmationEngine _sourceConfirmationEngine;

    public ControlledWebResearchSourceCollectorService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
        _queueService = new ResearchQueueService(storagePaths);
        _qualityEngine = new KnowledgeQualityEngine(storagePaths);
        _validationStrategy = new KnowledgeValidationStrategy(storagePaths);
        _sourceConfirmationEngine = new SourceConfirmationEngine(storagePaths);
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector");

    public string ReportPath => Path.Combine(Root, "web_research_requests.json");

    public string MarkdownPath => Path.Combine(Root, "web_research_requests.md");

    public WebResearchSourceCollectorReport Run(bool apply = false)
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var queue = _queueService.LoadOrCreateQueue();
        var secondSourceItems = queue.Items
            .Where(item => (item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)
                || item.Status.Equals("processed", StringComparison.OrdinalIgnoreCase))
                && item.Type.Equals("collect_second_independent_source", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var quality = _qualityEngine.LoadOrCreateReport();
        var planReport = _validationStrategy.LoadPlanReport() ?? _validationStrategy.GeneratePlans(50);
        var confirmations = _sourceConfirmationEngine.LoadOrBuild();
        var evidence = LoadEvidence();
        var evidenceByKnowledgeId = evidence.Evidence
            .GroupBy(entry => entry.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(entry => entry.UpdatedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var requests = secondSourceItems
            .Select(item => BuildRequest(item, quality, planReport, confirmations, evidenceByKnowledgeId, now))
            .OrderByDescending(request => request.CurrentSourceCount < 2)
            .ThenBy(request => request.Domain, StringComparer.OrdinalIgnoreCase)
            .ThenBy(request => request.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var exported = requests.Count;
        var awaiting = requests.Count(request => request.Status.Equals("awaiting_external_search", StringComparison.OrdinalIgnoreCase));
        var alreadyHasCandidate = requests.Count(request => request.Status.Equals("candidate_source_available", StringComparison.OrdinalIgnoreCase));
        var blockedNoWebRuntime = 0;

        var createdResearchQueueItems = 0;
        if (apply)
        {
            MarkAwaitingExternalSearch(secondSourceItems, requests, now);
            createdResearchQueueItems = requests.Count(request => request.Status.Equals("awaiting_external_search", StringComparison.OrdinalIgnoreCase));
        }

        var report = new WebResearchSourceCollectorReport(
            ReportVersion: "web_research_source_collector_v1",
            UpdatedAtUtc: now,
            TotalSecondSourceItems: secondSourceItems.Count,
            ExportedSearchRequests: exported,
            AwaitingExternalSearch: awaiting,
            AlreadyHasCandidateSource: alreadyHasCandidate,
            BlockedNoWebRuntime: blockedNoWebRuntime,
            Requests: requests,
            Warnings: secondSourceItems.Count == 0 ? ["no_open_second_source_items_found"] : [],
            QueuePath: _queueService.QueuePath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        report = report with
        {
            ExportedSearchRequests = exported,
            AwaitingExternalSearch = apply ? createdResearchQueueItems : awaiting,
            AlreadyHasCandidateSource = alreadyHasCandidate,
            BlockedNoWebRuntime = blockedNoWebRuntime
        };

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    private void MarkAwaitingExternalSearch(
        IReadOnlyList<ResearchQueueItem> items,
        IReadOnlyList<WebResearchSourceRequest> requests,
        DateTimeOffset now)
    {
        if (items.Count == 0)
        {
            return;
        }

        var queueIds = items.Select(item => item.QueueItemId).ToList();
        var noteList = requests
            .Select(request => $"web_research_request:{request.RequestId}")
            .Concat(["controlled_web_research_exported"])
            .ToList();
        _ = _queueService.MarkItemsAwaitingExternalSearch(queueIds, noteList);
    }

    private WebResearchSourceRequest BuildRequest(
        ResearchQueueItem item,
        KnowledgeQualityReport quality,
        KnowledgeValidationPlanReport planReport,
        SourceConfirmationReport confirmations,
        IReadOnlyDictionary<string, KnowledgeEvidenceEntry> evidenceByKnowledgeId,
        DateTimeOffset now)
    {
        var knowledgeId = ExtractKnowledgeId(item);
        var qualityItem = quality.Items.FirstOrDefault(candidate => candidate.KnowledgeId.Equals(knowledgeId, StringComparison.OrdinalIgnoreCase));
        var plan = planReport.Plans.FirstOrDefault(candidate => candidate.KnowledgeItemId.Equals(knowledgeId, StringComparison.OrdinalIgnoreCase));
        var confirmation = confirmations.Results.FirstOrDefault(candidate => candidate.KnowledgeId.Equals(knowledgeId, StringComparison.OrdinalIgnoreCase));
        var sourceCount = confirmation?.SourceCount
            ?? (evidenceByKnowledgeId.TryGetValue(knowledgeId, out var evidenceEntry) ? evidenceEntry.SourceIds.Count : 1);
        var query = ExtractQuery(item)
            ?? BuildRecommendedQuery(qualityItem?.Domain ?? item.Domain, qualityItem?.Title ?? knowledgeId);
        var requiredEvidence = new List<string>();
        if (sourceCount < 2)
        {
            requiredEvidence.Add("second_independent_source");
        }

        if (plan is not null)
        {
            requiredEvidence.AddRange(plan.MissingEvidence);
        }

        var recommendedDomains = RecommendedSourceDomains(qualityItem?.Domain ?? item.Domain);
        var status = confirmation is not null && confirmation.SourceCount >= 2
            ? "candidate_source_available"
            : "awaiting_external_search";
        return new WebResearchSourceRequest(
            RequestId: $"web_research_request_{item.QueueItemId}",
            KnowledgeItemId: knowledgeId,
            Domain: item.Domain,
            Query: query,
            RecommendedSourceDomains: recommendedDomains,
            Reason: BuildReason(qualityItem, plan, confirmation, sourceCount),
            CurrentSourceCount: sourceCount,
            RequiredEvidence: requiredEvidence.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Status: status,
            HumanReviewRequired: true,
            CreatedAtUtc: now);
    }

    private static string? ExtractQuery(ResearchQueueItem item)
    {
        foreach (var note in item.Notes)
        {
            if (note.StartsWith("query:", StringComparison.OrdinalIgnoreCase))
            {
                return note["query:".Length..];
            }
        }

        return null;
    }

    private static string ExtractKnowledgeId(ResearchQueueItem item)
    {
        foreach (var note in item.Notes)
        {
            if (note.StartsWith("multi_source_candidate:", StringComparison.OrdinalIgnoreCase))
            {
                return note["multi_source_candidate:".Length..];
            }
        }

        return item.SourceRefs.FirstOrDefault() ?? item.QueueItemId;
    }

    private static IReadOnlyList<string> RecommendedSourceDomains(string domain) =>
        domain.ToLowerInvariant() switch
        {
            "trading" => ["spotware.com", "github.com/spotware", "ctrader.com"],
            "documentation" => ["docs.microsoft.com", "learn.microsoft.com", "spotware.com"],
            "software" => ["github.com", "learn.microsoft.com", "docs.microsoft.com"],
            "process" => ["internal process docs", "policy docs", "knowledge base"],
            "research" => ["research paper", "official documentation", "vendor docs"],
            _ => ["official docs", "vendor docs", "source repository"]
        };

    private static string BuildReason(
        KnowledgeQualityItem? qualityItem,
        KnowledgeValidationPlan? plan,
        ConfirmationResult? confirmation,
        int sourceCount)
    {
        if (confirmation is not null && confirmation.SourceCount >= 2)
        {
            return "lokale Evidenz zeigt bereits zwei unabhängige Quellen; kontrollierte Aktualisierung möglich.";
        }

        if (plan is not null && plan.MissingEvidence.Count > 0)
        {
            return $"Validation-Plan verlangt weitere Evidenz: {string.Join(", ", plan.MissingEvidence.Take(3))}.";
        }

        return qualityItem is null
            ? "Knowledge Item benötigt eine zweite unabhängige Quelle."
            : $"{qualityItem.Title} benötigt mindestens eine zweite unabhängige Quelle (aktuell {sourceCount}).";
    }

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

    private KnowledgeEvidenceReport LoadEvidence()
    {
        var path = _qualityEngine.EvidencePath;
        if (File.Exists(path))
        {
            try
            {
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
            catch (Exception ex) when (ex is IOException or JsonException)
            {
            }
        }

        if (File.Exists(_qualityEngine.EvidencePath))
        {
            try
            {
                var existing = JsonSerializer.Deserialize<KnowledgeEvidenceReport>(
                    File.ReadAllText(_qualityEngine.EvidencePath),
                    JsonDefaults.SnapshotReadOptions);
                if (existing is not null)
                {
                    return existing;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
            }
        }

        var report = _qualityEngine.LoadOrCreateReport();
        var evidence = report.Items.Select(item => new KnowledgeEvidenceEntry(
            KnowledgeId: item.KnowledgeId,
            Domain: item.Domain,
            SourceIds: item.EvidenceRefs.Where(reference => reference.StartsWith("source:", StringComparison.OrdinalIgnoreCase)).Select(reference => reference["source:".Length..]).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SourceEvidenceRefs: item.EvidenceRefs.Where(reference => reference.StartsWith("source:", StringComparison.OrdinalIgnoreCase)).ToList(),
            ValidationEvidenceRefs: item.EvidenceRefs.Where(reference => reference.StartsWith("validation:", StringComparison.OrdinalIgnoreCase)).ToList(),
            OutcomeRefs: [],
            GoalRefs: [],
            QueueRefs: [],
            RelatedItems: [],
            UpdatedAtUtc: item.LastValidatedUtc ?? DateTimeOffset.UtcNow,
            HumanReviewRequired: true)).ToList();
        var fallback = new KnowledgeEvidenceReport(
            ReportVersion: "knowledge_evidence_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Evidence: evidence,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(_qualityEngine.EvidencePath, JsonSerializer.Serialize(fallback, JsonDefaults.WriteOptions));
        return fallback;
    }

    private static string BuildMarkdown(WebResearchSourceCollectorReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Controlled Web Research Source Collector");
        sb.AppendLine();
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Total Second Source Items: {report.TotalSecondSourceItems}");
        sb.AppendLine($"- Exported Search Requests: {report.ExportedSearchRequests}");
        sb.AppendLine($"- Awaiting External Search: {report.AwaitingExternalSearch}");
        sb.AppendLine($"- Already Has Candidate Source: {report.AlreadyHasCandidateSource}");
        sb.AppendLine($"- Blocked No Web Runtime: {report.BlockedNoWebRuntime}");
        sb.AppendLine();
        foreach (var request in report.Requests.Take(20))
        {
            sb.AppendLine($"## {request.KnowledgeItemId}");
            sb.AppendLine($"- Domain: {request.Domain}");
            sb.AppendLine($"- Query: {request.Query}");
            sb.AppendLine($"- Recommended Source Domains: {string.Join(", ", request.RecommendedSourceDomains)}");
            sb.AppendLine($"- Required Evidence: {string.Join(", ", request.RequiredEvidence)}");
            sb.AppendLine($"- Status: {request.Status}");
        }

        return sb.ToString();
    }
}
