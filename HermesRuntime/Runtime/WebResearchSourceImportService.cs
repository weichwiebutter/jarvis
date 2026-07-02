using System.Text.Json;

namespace Hermes.Runtime;

public sealed record WebResearchImportCandidateRecord(
    string KnowledgeItemId,
    string Title,
    string Url,
    string Domain,
    string SourceType,
    string ExcerptOrSummary,
    DateTimeOffset RetrievedAtUtc,
    string EvidenceReason,
    string IndependenceClaim,
    string HumanReviewStatus,
    IReadOnlyList<string> SafetyFlags,
    double RelevanceScore = 0,
    IReadOnlyList<string>? MatchedTerms = null,
    string? RejectionReason = null,
    string? SourceRelevanceStatus = null);

public sealed record WebResearchImportReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int ImportCandidates,
    int AcceptedCandidates,
    int RejectedCandidates,
    int DuplicateSources,
    int BlockedSameDomain,
    int AwaitingHumanReview,
    int CandidateSourcesAdded,
    IReadOnlyList<WebResearchImportCandidateRecord> Accepted,
    IReadOnlyList<WebResearchImportCandidateRecord> Rejected,
    IReadOnlyList<string> Warnings,
    string ImportCandidatesPath,
    string ImportExamplePath,
    string SourceConfirmationsPath,
    string ReportPath,
    string MarkdownPath,
    bool DryRun,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class WebResearchSourceImportService
{
    private readonly StoragePaths _storagePaths;
    private readonly KnowledgeSourceRegistry _sourceRegistry;

    public WebResearchSourceImportService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
        _sourceRegistry = new KnowledgeSourceRegistry(storagePaths);
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector");

    public string ImportCandidatesPath => Path.Combine(Root, "web_research_import_candidates.json");

    public string ExamplePath => Path.Combine(Root, "web_research_import_candidates.example.json");

    public string ReportPath => Path.Combine(Root, "web_research_import_report.json");

    public string MarkdownPath => Path.Combine(Root, "web_research_import_report.md");

    public SourceConfirmationReport LoadSourceConfirmations()
    {
        var path = new SourceConfirmationEngine(_storagePaths).ReportPath;
        if (!File.Exists(path))
        {
            return EmptySourceConfirmations("source_confirmation_missing");
        }

        try
        {
            return JsonSerializer.Deserialize<SourceConfirmationReport>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions)
                ?? EmptySourceConfirmations("source_confirmation_empty");
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return EmptySourceConfirmations("source_confirmation_missing");
        }
    }

    private static SourceConfirmationReport EmptySourceConfirmations(string warning)
    {
        return new SourceConfirmationReport(
            ReportVersion: "source_confirmation_v2",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ItemsAnalyzed: 0,
            ConfirmationDistribution: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            Results: [],
            Warnings: [warning],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    public WebResearchImportReport Run(bool apply)
    {
        Directory.CreateDirectory(Root);
        EnsureExampleFile();
        var now = DateTimeOffset.UtcNow;
        var candidates = LoadImportCandidates();
        var confirmations = LoadSourceConfirmations();
        var sources = _sourceRegistry.LoadOrCreateSources();
        var sourceByDomain = sources
            .GroupBy(source => source.Domain, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var importedUrlsByKnowledgeItem = confirmations.Results
            .ToDictionary(
                result => result.KnowledgeId,
                result => (result.CandidateSources ?? [])
                    .Select(candidate => candidate.Url)
                    .Where(url => !string.IsNullOrWhiteSpace(url))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        var knownKnowledgeIds = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems()
            .Select(item => item.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var accepted = new List<WebResearchImportCandidateRecord>();
        var rejected = new List<WebResearchImportCandidateRecord>();
        var duplicateSources = 0;
        var blockedSameDomain = 0;
        var awaitingHumanReview = 0;
        var updates = new List<(string KnowledgeId, SourceCandidate Candidate)>();

        foreach (var candidate in candidates)
        {
            var reason = ValidateCandidate(candidate, knownKnowledgeIds, confirmations, importedUrlsByKnowledgeItem, sourceByDomain);
            if (reason is not null)
            {
                rejected.Add(candidate);
                if (reason.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
                {
                    duplicateSources++;
                }
                if (reason.Contains("same_domain", StringComparison.OrdinalIgnoreCase))
                {
                    blockedSameDomain++;
                }
                continue;
            }

            accepted.Add(candidate);
            awaitingHumanReview++;
            updates.Add((candidate.KnowledgeItemId, new SourceCandidate(
                Url: candidate.Url,
                Domain: candidate.Domain,
                SourceType: candidate.SourceType,
                ExcerptOrSummary: candidate.ExcerptOrSummary,
                RetrievedAtUtc: candidate.RetrievedAtUtc,
                EvidenceReason: candidate.EvidenceReason,
                IndependenceClaim: candidate.IndependenceClaim,
                HumanReviewStatus: candidate.HumanReviewStatus,
                SafetyFlags: candidate.SafetyFlags.Distinct(StringComparer.OrdinalIgnoreCase).ToList())));
            if (!importedUrlsByKnowledgeItem.TryGetValue(candidate.KnowledgeItemId, out var itemUrls))
            {
                itemUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                importedUrlsByKnowledgeItem[candidate.KnowledgeItemId] = itemUrls;
            }

            itemUrls.Add(candidate.Url);
        }

        if (apply && updates.Count > 0)
        {
            var updated = ApplyUpdates(confirmations, updates, now);
            File.WriteAllText(new SourceConfirmationEngine(_storagePaths).ReportPath, JsonSerializer.Serialize(updated, JsonDefaults.WriteOptions));
        }

        var report = new WebResearchImportReport(
            ReportVersion: "web_research_import_v1",
            UpdatedAtUtc: now,
            ImportCandidates: candidates.Count,
            AcceptedCandidates: accepted.Count,
            RejectedCandidates: rejected.Count,
            DuplicateSources: duplicateSources,
            BlockedSameDomain: blockedSameDomain,
            AwaitingHumanReview: awaitingHumanReview,
            CandidateSourcesAdded: apply ? accepted.Count : 0,
            Accepted: accepted,
            Rejected: rejected,
            Warnings: candidates.Count == 0 ? ["no_import_candidates_found"] : [],
            ImportCandidatesPath: ImportCandidatesPath,
            ImportExamplePath: ExamplePath,
            SourceConfirmationsPath: new SourceConfirmationEngine(_storagePaths).ReportPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            DryRun: !apply,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    private void EnsureExampleFile()
    {
        if (File.Exists(ExamplePath))
        {
            return;
        }

        var example = new[]
        {
            new WebResearchImportCandidateRecord(
                KnowledgeItemId: "trading:example_breakout",
                Title: "Example Breakout Source",
                Url: "https://example.com/trading/breakout",
                Domain: "trading",
                SourceType: "manual_web_reference",
                ExcerptOrSummary: "Example-only candidate. Replace with a real external source.",
                RetrievedAtUtc: DateTimeOffset.UtcNow,
                EvidenceReason: "example_only",
                IndependenceClaim: "example_independent_source",
                HumanReviewStatus: "pending",
                SafetyFlags: ["example_only", "no_trading_execution", "human_review_required"]),
            new WebResearchImportCandidateRecord(
                KnowledgeItemId: "documentation:doc_example",
                Title: "Example Documentation Source",
                Url: "https://example.com/docs/example",
                Domain: "documentation",
                SourceType: "manual_web_reference",
                ExcerptOrSummary: "Example-only candidate. Replace with a real external source.",
                RetrievedAtUtc: DateTimeOffset.UtcNow,
                EvidenceReason: "example_only",
                IndependenceClaim: "example_independent_source",
                HumanReviewStatus: "pending",
                SafetyFlags: ["example_only", "no_trading_execution", "human_review_required"])
        };
        File.WriteAllText(ExamplePath, JsonSerializer.Serialize(example, JsonDefaults.WriteOptions));
    }

    private IReadOnlyList<WebResearchImportCandidateRecord> LoadImportCandidates()
    {
        if (!File.Exists(ImportCandidatesPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<WebResearchImportCandidateRecord>>(
                File.ReadAllText(ImportCandidatesPath),
                JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    private static string? ValidateCandidate(
        WebResearchImportCandidateRecord candidate,
        ISet<string> knownKnowledgeIds,
        SourceConfirmationReport confirmations,
        IReadOnlyDictionary<string, HashSet<string>> importedUrlsByKnowledgeItem,
        IReadOnlyDictionary<string, List<CognitiveSource>> sourceByDomain)
    {
        if (string.IsNullOrWhiteSpace(candidate.KnowledgeItemId))
        {
            return "missing_knowledge_item_id";
        }

        if (string.IsNullOrWhiteSpace(candidate.Title))
        {
            return "missing_title";
        }

        if (string.IsNullOrWhiteSpace(candidate.Url))
        {
            return "missing_url";
        }

        if (string.IsNullOrWhiteSpace(candidate.Domain))
        {
            return "missing_domain";
        }

        if (string.IsNullOrWhiteSpace(candidate.SourceType))
        {
            return "missing_source_type";
        }

        if (string.IsNullOrWhiteSpace(candidate.ExcerptOrSummary))
        {
            return "missing_excerpt_or_summary";
        }

        if (candidate.SafetyFlags is null || candidate.SafetyFlags.Count == 0)
        {
            return "missing_safety_flags";
        }

        var requiredFlags = new[] { "no_trading_execution", "human_review_required" };
        if (requiredFlags.Any(required => !candidate.SafetyFlags.Any(flag => flag.Equals(required, StringComparison.OrdinalIgnoreCase))))
        {
            return "safety_flags_missing_required_terms";
        }

        if (!knownKnowledgeIds.Contains(candidate.KnowledgeItemId))
        {
            return "unknown_knowledge_item_id";
        }

        if (!candidate.HumanReviewStatus.Equals("pending", StringComparison.OrdinalIgnoreCase)
            && !candidate.HumanReviewStatus.Equals("awaiting_human_review", StringComparison.OrdinalIgnoreCase))
        {
            return "human_review_status_not_pending";
        }

        if (importedUrlsByKnowledgeItem.TryGetValue(candidate.KnowledgeItemId, out var itemUrls)
            && itemUrls.Contains(candidate.Url))
        {
            return "duplicate_url";
        }

        var confirmation = confirmations.Results.FirstOrDefault(result =>
            result.KnowledgeId.Equals(candidate.KnowledgeItemId, StringComparison.OrdinalIgnoreCase));
        var primaryDomain = confirmation?.Domain ?? candidate.Domain;
        if (!string.IsNullOrWhiteSpace(primaryDomain)
            && candidate.Domain.Equals(primaryDomain, StringComparison.OrdinalIgnoreCase))
        {
            return "same_domain_as_primary_source";
        }

        return null;
    }

    private static SourceConfirmationReport ApplyUpdates(
        SourceConfirmationReport confirmations,
        IReadOnlyList<(string KnowledgeId, SourceCandidate Candidate)> updates,
        DateTimeOffset now)
    {
        var byKnowledge = updates
            .GroupBy(update => update.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(update => update.Candidate).ToList(), StringComparer.OrdinalIgnoreCase);

        var results = confirmations.Results
            .Select(result =>
            {
                if (!byKnowledge.TryGetValue(result.KnowledgeId, out var candidateSources))
                {
                    return result;
                }

                var mergedSources = (result.CandidateSources ?? [])
                    .Concat(candidateSources)
                    .GroupBy(candidate => candidate.Url, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .ToList();

                return result with
                {
                    CandidateSources = mergedSources,
                    CandidateSourceCount = mergedSources.Count,
                    ReviewStatus = "awaiting_human_review",
                    Warnings = result.Warnings
                        .Concat(mergedSources.Count > 0 ? ["candidate_source_imported"] : [])
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()
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
                .Concat(["controlled_web_research_import_applied"])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static string BuildMarkdown(WebResearchImportReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Web Research Import Report");
        sb.AppendLine();
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Import Candidates: {report.ImportCandidates}");
        sb.AppendLine($"- Accepted Candidates: {report.AcceptedCandidates}");
        sb.AppendLine($"- Rejected Candidates: {report.RejectedCandidates}");
        sb.AppendLine($"- Duplicate Sources: {report.DuplicateSources}");
        sb.AppendLine($"- Blocked Same Domain: {report.BlockedSameDomain}");
        sb.AppendLine($"- Awaiting Human Review: {report.AwaitingHumanReview}");
        sb.AppendLine($"- Candidate Sources Added: {report.CandidateSourcesAdded}");
        sb.AppendLine();
        sb.AppendLine("## Accepted");
        foreach (var candidate in report.Accepted.Take(20))
        {
            sb.AppendLine($"- {candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Url}");
        }
        sb.AppendLine();
        sb.AppendLine("## Rejected");
        foreach (var candidate in report.Rejected.Take(20))
        {
            sb.AppendLine($"- {candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Url}");
        }
        return sb.ToString();
    }
}
