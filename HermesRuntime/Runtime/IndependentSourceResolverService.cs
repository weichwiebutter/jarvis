using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hermes.Runtime;

public sealed record IndependentSourceResolverCandidate(
    string KnowledgeItemId,
    string Title,
    string Url,
    string Domain,
    string SourceType,
    string ExcerptOrSummary,
    string HumanReviewStatus,
    IReadOnlyList<string> SafetyFlags,
    double SemanticMatchScore,
    double IndependenceScore,
    double EvidenceCoverageScore,
    double ContradictionRisk,
    string EvidenceMatchStatus,
    string SourceStatus,
    bool ReadyForHumanSourceReview,
    string RelationshipStatus,
    int IndependentSourceCandidateCount,
    IReadOnlyList<string> MatchedTerms,
    string? RejectionReason = null);

public sealed record IndependentSourceResolverReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedCandidates,
    int EvaluatedExistingCandidateSources,
    int DuplicateImportCandidates,
    int TrueDuplicates,
    int SameDomainCandidates,
    int IndependentExistingCandidates,
    int IndependentCandidates,
    int RejectedCandidates,
    int ReadyForHumanReview,
    int AffectedKnowledgeItems,
    int AppliedCandidates,
    IReadOnlyList<IndependentSourceResolverCandidate> Candidates,
    IReadOnlyList<IndependentSourceResolverCandidate> Accepted,
    IReadOnlyList<IndependentSourceResolverCandidate> Rejected,
    IReadOnlyList<string> Warnings,
    string SourceConfirmationsPath,
    string ImportCandidatesPath,
    string MatcherReportPath,
    string KnowledgeEvidencePath,
    string EvidenceGraphPath,
    string ReportPath,
    string MarkdownPath,
    bool DryRun,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class IndependentSourceResolverService
{
    private readonly StoragePaths _storagePaths;

    public IndependentSourceResolverService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "independent_source_resolver");
    public string SourceConfirmationsPath => Path.Combine(_storagePaths.Root, "cognitive_core", "source_confirmations.json");
    public string ImportCandidatesPath => Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector", "web_research_import_candidates.json");
    public string MatcherReportPath => Path.Combine(_storagePaths.Root, "reports", "knowledge_evidence_matcher", "knowledge_evidence_matcher_report.json");
    public string KnowledgeEvidencePath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_evidence.json");
    public string EvidenceGraphPath => Path.Combine(_storagePaths.Root, "cognitive_core", "evidence_graph.json");
    public string ReportPath => Path.Combine(Root, "independent_source_resolver_report.json");
    public string MarkdownPath => Path.Combine(Root, "independent_source_resolver_report.md");

    public IndependentSourceResolverReport Run(bool apply)
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var importCandidates = LoadImportCandidates();
        var matcherReport = LoadMatcherReport();
        var evidence = LoadKnowledgeEvidence();
        var graph = LoadEvidenceGraph();
        var sourceRegistry = new KnowledgeSourceRegistry(_storagePaths).LoadOrCreateSources()
            .ToDictionary(source => source.SourceId, source => NormalizeDomain(source.Domain), StringComparer.OrdinalIgnoreCase);
        var confirmations = LoadSourceConfirmations();

        var candidateLookup = importCandidates
            .GroupBy(candidate => MakeKey(candidate.KnowledgeItemId, candidate.Url), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var semanticMatches = matcherReport?.Candidates
            .Where(candidate => candidate.Status is "candidate_relevant" or "candidate_weak" or "needs_human_review")
            .ToList() ?? [];

        var sourceDomainsByKnowledgeId = BuildKnowledgeSourceDomains(confirmations, evidence, sourceRegistry, graph);
        var existingCandidateSources = confirmations.Results
            .SelectMany(result => (result.CandidateSources ?? []).Select(candidate => new ExistingCandidateSourceRecord(result.KnowledgeId, candidate)))
            .ToList();

        var existingCandidateSourceUrls = existingCandidateSources
            .Select(record => record.Candidate.Url)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var importedDuplicateUrls = importCandidates
            .GroupBy(candidate => MakeKey(candidate.KnowledgeItemId, candidate.Url), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = new List<IndependentSourceResolverCandidate>();
        var accepted = new List<IndependentSourceResolverCandidate>();
        var rejected = new List<IndependentSourceResolverCandidate>();
        var warnings = new List<string>();
        var evaluatedExistingCandidateSources = 0;
        var duplicateImportCandidates = 0;
        var trueDuplicates = 0;
        var sameDomainCandidates = 0;
        var independentExistingCandidates = 0;
        var independentCandidates = 0;
        var rejectedCandidates = 0;
        var readyForHumanReview = 0;
        var updates = new List<(string KnowledgeId, SourceCandidate Candidate, bool Independent)>();
        var affectedKnowledgeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var existingCandidate in existingCandidateSources)
        {
            evaluatedExistingCandidateSources++;
            var semanticMatch = FindSemanticMatch(matcherReport, existingCandidate.KnowledgeItemId, existingCandidate.Candidate.Url)
                ?? BuildSemanticMatchFromExisting(existingCandidate);
            var classification = ClassifyExistingCandidate(existingCandidate.KnowledgeItemId, existingCandidate.Candidate, sourceDomainsByKnowledgeId);
            var resolved = BuildResolvedCandidate(existingCandidate.KnowledgeItemId, existingCandidate.Candidate, semanticMatch, classification);

            candidates.Add(resolved);

            switch (classification.Bucket)
            {
                case "same_domain":
                    sameDomainCandidates++;
                    if (classification.Accepted)
                    {
                        accepted.Add(resolved);
                        affectedKnowledgeIds.Add(existingCandidate.KnowledgeItemId);
                        if (classification.ReadyForHumanReview)
                        {
                            readyForHumanReview++;
                        }

                        if (classification.Independent)
                        {
                            independentExistingCandidates++;
                            updates.Add((existingCandidate.KnowledgeItemId, BuildSourceCandidate(existingCandidate.Candidate, resolved, classification), true));
                        }
                    }
                    else
                    {
                        rejectedCandidates++;
                        rejected.Add(resolved);
                    }
                    break;
                case "independent":
                    independentExistingCandidates++;
                    if (classification.Accepted)
                    {
                        accepted.Add(resolved);
                        affectedKnowledgeIds.Add(existingCandidate.KnowledgeItemId);
                        if (classification.ReadyForHumanReview)
                        {
                            readyForHumanReview++;
                        }

                        updates.Add((existingCandidate.KnowledgeItemId, BuildSourceCandidate(existingCandidate.Candidate, resolved, classification), true));
                    }
                    else
                    {
                        rejectedCandidates++;
                        rejected.Add(resolved);
                    }
                    break;
                default:
                    rejectedCandidates++;
                    rejected.Add(resolved);
                    break;
            }
        }

        foreach (var candidate in semanticMatches)
        {
            if (!candidateLookup.TryGetValue(MakeKey(candidate.KnowledgeItemId, candidate.Url), out var rawCandidate))
            {
                continue;
            }

            var duplicateImport = importedDuplicateUrls.Contains(MakeKey(candidate.KnowledgeItemId, candidate.Url));
            var classification = ClassifyCandidate(candidate, rawCandidate, sourceDomainsByKnowledgeId, existingCandidateSourceUrls, duplicateImport);
            var resolved = new IndependentSourceResolverCandidate(
                KnowledgeItemId: candidate.KnowledgeItemId,
                Title: rawCandidate.Title,
                Url: rawCandidate.Url,
                Domain: rawCandidate.Domain,
                SourceType: rawCandidate.SourceType,
                ExcerptOrSummary: rawCandidate.ExcerptOrSummary,
                HumanReviewStatus: "pending",
                SafetyFlags: rawCandidate.SafetyFlags.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                SemanticMatchScore: candidate.SemanticMatchScore,
                IndependenceScore: candidate.IndependenceScore,
                EvidenceCoverageScore: candidate.EvidenceCoverageScore,
                ContradictionRisk: candidate.ContradictionRisk,
                EvidenceMatchStatus: candidate.EvidenceMatchStatus,
                SourceStatus: classification.SourceStatus,
                ReadyForHumanSourceReview: classification.ReadyForHumanReview,
                RelationshipStatus: classification.RelationshipStatus,
                IndependentSourceCandidateCount: classification.Independent ? 1 : 0,
                MatchedTerms: candidate.MatchedTerms,
                RejectionReason: classification.RejectionReason);

            candidates.Add(resolved);

            switch (classification.Bucket)
            {
                case "duplicate":
                    trueDuplicates++;
                    duplicateImportCandidates++;
                    rejectedCandidates++;
                    rejected.Add(resolved);
                    break;
                case "same_domain":
                    sameDomainCandidates++;
                    if (classification.Accepted)
                    {
                        accepted.Add(resolved);
                        affectedKnowledgeIds.Add(candidate.KnowledgeItemId);
                        if (classification.ReadyForHumanReview)
                        {
                            readyForHumanReview++;
                        }
                        updates.Add((candidate.KnowledgeItemId, BuildSourceCandidate(rawCandidate, resolved, classification), classification.Independent));
                    }
                    else
                    {
                        rejectedCandidates++;
                        rejected.Add(resolved);
                    }
                    break;
                case "independent":
                    independentCandidates++;
                    if (classification.Accepted)
                    {
                        accepted.Add(resolved);
                        affectedKnowledgeIds.Add(candidate.KnowledgeItemId);
                        if (classification.ReadyForHumanReview)
                        {
                            readyForHumanReview++;
                        }
                        updates.Add((candidate.KnowledgeItemId, BuildSourceCandidate(rawCandidate, resolved, classification), classification.Independent));
                    }
                    else
                    {
                        rejectedCandidates++;
                        rejected.Add(resolved);
                    }
                    break;
                default:
                    rejectedCandidates++;
                    rejected.Add(resolved);
                    break;
            }
        }

        if (apply && updates.Count > 0)
        {
            var updated = ApplyUpdates(confirmations, updates, now);
            File.WriteAllText(SourceConfirmationsPath, JsonSerializer.Serialize(updated, JsonDefaults.WriteOptions));
        }

        var report = new IndependentSourceResolverReport(
            ReportVersion: "independent_source_resolver_v1",
            UpdatedAtUtc: now,
            Status: candidates.Count == 0 ? "no_matching_candidates" : apply ? "applied" : "dry_run_ready",
            LoadedCandidates: semanticMatches.Count,
            EvaluatedExistingCandidateSources: evaluatedExistingCandidateSources,
            DuplicateImportCandidates: duplicateImportCandidates,
            TrueDuplicates: trueDuplicates,
            SameDomainCandidates: sameDomainCandidates,
            IndependentExistingCandidates: independentExistingCandidates,
            IndependentCandidates: independentCandidates,
            RejectedCandidates: rejectedCandidates,
            ReadyForHumanReview: readyForHumanReview,
            AffectedKnowledgeItems: affectedKnowledgeIds.Count,
            AppliedCandidates: apply ? updates.Count : 0,
            Candidates: candidates,
            Accepted: accepted,
            Rejected: rejected,
            Warnings: warnings.Concat(candidates.Count == 0 ? ["no_independent_candidates_loaded"] : []).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SourceConfirmationsPath: SourceConfirmationsPath,
            ImportCandidatesPath: ImportCandidatesPath,
            MatcherReportPath: MatcherReportPath,
            KnowledgeEvidencePath: KnowledgeEvidencePath,
            EvidenceGraphPath: EvidenceGraphPath,
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

    public IndependentSourceResolverReport LoadStatus()
    {
        if (!File.Exists(ReportPath))
        {
            return Run(apply: false) with { Status = "status_snapshot_generated" };
        }

        try
        {
            return JsonSerializer.Deserialize<IndependentSourceResolverReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions) ?? Run(apply: false) with { Status = "status_snapshot_generated" };
        }
        catch
        {
            return Run(apply: false) with { Status = "status_snapshot_generated" };
        }
    }

    private static SourceCandidate BuildSourceCandidate(
        WebResearchImportCandidateRecord rawCandidate,
        IndependentSourceResolverCandidate resolved,
        CandidateClassification classification) =>
        new(
            Url: rawCandidate.Url,
            Domain: rawCandidate.Domain,
            SourceType: rawCandidate.SourceType,
            ExcerptOrSummary: rawCandidate.ExcerptOrSummary,
            RetrievedAtUtc: rawCandidate.RetrievedAtUtc,
            EvidenceReason: rawCandidate.EvidenceReason,
            IndependenceClaim: rawCandidate.IndependenceClaim,
            HumanReviewStatus: "pending",
            SafetyFlags: rawCandidate.SafetyFlags.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SemanticMatchScore: resolved.SemanticMatchScore,
            IndependenceScore: resolved.IndependenceScore,
            EvidenceCoverageScore: resolved.EvidenceCoverageScore,
            ContradictionRisk: resolved.ContradictionRisk,
            EvidenceMatchStatus: classification.Independent ? "matched_pending_review" : classification.SourceStatus,
            ReadyForHumanSourceReview: classification.ReadyForHumanReview,
            IndependentSourceCandidateCount: classification.Independent ? 1 : 0,
            SourceStatus: classification.SourceStatus);

    private static SourceCandidate BuildSourceCandidate(
        SourceCandidate existingCandidate,
        IndependentSourceResolverCandidate resolved,
        CandidateClassification classification) =>
        existingCandidate with
        {
            SemanticMatchScore = resolved.SemanticMatchScore,
            IndependenceScore = resolved.IndependenceScore,
            EvidenceCoverageScore = resolved.EvidenceCoverageScore,
            ContradictionRisk = resolved.ContradictionRisk,
            EvidenceMatchStatus = classification.Independent ? "matched_pending_review" : classification.SourceStatus,
            ReadyForHumanSourceReview = classification.ReadyForHumanReview,
            IndependentSourceCandidateCount = classification.Independent ? 1 : 0,
            SourceStatus = classification.SourceStatus,
            HumanReviewStatus = "pending"
        };

    private static IndependentSourceResolverCandidate BuildResolvedCandidate(
        string knowledgeItemId,
        SourceCandidate candidate,
        KnowledgeEvidenceSemanticMatchCandidate semanticMatch,
        CandidateClassification classification) =>
        new(
            KnowledgeItemId: knowledgeItemId,
            Title: candidate.ExcerptOrSummary.Length > 0 ? candidate.ExcerptOrSummary : candidate.Url,
            Url: candidate.Url,
            Domain: candidate.Domain,
            SourceType: candidate.SourceType,
            ExcerptOrSummary: candidate.ExcerptOrSummary,
            HumanReviewStatus: candidate.HumanReviewStatus,
            SafetyFlags: candidate.SafetyFlags.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SemanticMatchScore: semanticMatch.SemanticMatchScore,
            IndependenceScore: semanticMatch.IndependenceScore,
            EvidenceCoverageScore: semanticMatch.EvidenceCoverageScore,
            ContradictionRisk: semanticMatch.ContradictionRisk,
            EvidenceMatchStatus: semanticMatch.EvidenceMatchStatus,
            SourceStatus: classification.SourceStatus,
            ReadyForHumanSourceReview: classification.ReadyForHumanReview,
            RelationshipStatus: classification.RelationshipStatus,
            IndependentSourceCandidateCount: classification.Independent ? 1 : 0,
            MatchedTerms: semanticMatch.MatchedTerms,
            RejectionReason: classification.RejectionReason);

    private static KnowledgeEvidenceSemanticMatchCandidate? FindSemanticMatch(
        KnowledgeEvidenceSemanticMatcherReport? matcherReport,
        string knowledgeItemId,
        string url)
    {
        if (matcherReport is null)
        {
            return null;
        }

        return matcherReport.Candidates.FirstOrDefault(candidate =>
            candidate.KnowledgeItemId.Equals(knowledgeItemId, StringComparison.OrdinalIgnoreCase)
            && UrlsMatch(candidate.Url, url));
    }

    private static KnowledgeEvidenceSemanticMatchCandidate BuildSemanticMatchFromExisting(
        ExistingCandidateSourceRecord record) =>
        new(
            KnowledgeItemId: record.KnowledgeItemId,
            Title: record.Candidate.ExcerptOrSummary,
            Url: record.Candidate.Url,
            Domain: record.Candidate.Domain,
            SourceType: record.Candidate.SourceType,
            ExcerptOrSummary: record.Candidate.ExcerptOrSummary,
            EvidenceReason: record.Candidate.EvidenceReason,
            IndependenceClaim: record.Candidate.IndependenceClaim,
            HumanReviewStatus: record.Candidate.HumanReviewStatus,
            SafetyFlags: record.Candidate.SafetyFlags,
            SemanticMatchScore: record.Candidate.SemanticMatchScore,
            IndependenceScore: record.Candidate.IndependenceScore,
            EvidenceCoverageScore: record.Candidate.EvidenceCoverageScore,
            ContradictionRisk: record.Candidate.ContradictionRisk,
            Status: record.Candidate.SourceStatus,
            MatchedTerms: [],
            EvidenceRefs: [],
            ReadyForHumanSourceReview: record.Candidate.ReadyForHumanSourceReview,
            EvidenceMatchStatus: record.Candidate.EvidenceMatchStatus,
            RejectionReason: null);

    private static CandidateClassification ClassifyExistingCandidate(
        string knowledgeItemId,
        SourceCandidate existingCandidate,
        IReadOnlyDictionary<string, IReadOnlyList<string>> sourceDomainsByKnowledgeId)
    {
        var sameDomain = false;
        var primaryDomains = sourceDomainsByKnowledgeId.GetValueOrDefault(knowledgeItemId) ?? [];
        var candidateHost = GetHost(existingCandidate.Url);
        var candidateRoot = GetRootPublisherGroup(candidateHost);
        sameDomain = primaryDomains.Any(domain => DomainsMatch(domain, existingCandidate.Domain) || GetRootPublisherGroup(domain) == candidateRoot);

        var independent = existingCandidate.EvidenceMatchStatus is "matched_pending_review" or "candidate_pending_review"
            && existingCandidate.HumanReviewStatus.Equals("pending", StringComparison.OrdinalIgnoreCase)
            && existingCandidate.ContradictionRisk <= 0.35
            && existingCandidate.IndependenceScore >= 0.55
            && !sameDomain;

        var sourceStatus = independent
            ? "independent_candidate_pending_review"
            : sameDomain
                ? "same_domain_candidate_pending_review"
                : "candidate_rejected";

        var bucket = independent ? "independent" : sameDomain ? "same_domain" : "rejected";
        var accepted = independent || sameDomain;
        var rejectionReason = accepted ? null : "duplicate_or_same_root_group_or_low_independence";
        return new CandidateClassification(bucket, independent, sameDomain, accepted, rejectionReason, sourceStatus)
        {
            RelationshipStatus = independent
                ? "independent_existing_candidate"
                : sameDomain
                    ? "same_domain_existing_candidate"
                    : "rejected_existing_candidate"
        };
    }

    private static CandidateClassification ClassifyCandidate(
        KnowledgeEvidenceSemanticMatchCandidate candidate,
        WebResearchImportCandidateRecord rawCandidate,
        IReadOnlyDictionary<string, IReadOnlyList<string>> sourceDomainsByKnowledgeId,
        ISet<string> existingCandidateSourceUrls,
        bool duplicateImport)
    {
        var sameDomain = false;
        var independent = false;
        var rejectionReason = (string?)null;

        if (string.IsNullOrWhiteSpace(rawCandidate.Url))
        {
            return new CandidateClassification("rejected", false, false, false, "missing_url", "candidate_rejected");
        }

        if (candidate.SemanticMatchScore < 0.45 || candidate.ContradictionRisk > 0.45)
        {
            return new CandidateClassification("rejected", false, false, false, "semantic_or_contradiction_threshold_not_met", "candidate_rejected");
        }

        var primaryDomains = sourceDomainsByKnowledgeId.GetValueOrDefault(candidate.KnowledgeItemId) ?? [];
        var candidateHost = GetHost(rawCandidate.Url);
        var candidateRoot = GetRootPublisherGroup(candidateHost);

        var duplicateUrl = duplicateImport || existingCandidateSourceUrls.Contains(rawCandidate.Url);
        sameDomain = primaryDomains.Any(domain => DomainsMatch(domain, rawCandidate.Domain) || GetRootPublisherGroup(domain) == candidateRoot);
        independent = !duplicateUrl
            && candidate.EvidenceMatchStatus is "matched_pending_review" or "candidate_pending_review"
            && rawCandidate.HumanReviewStatus.Equals("pending", StringComparison.OrdinalIgnoreCase)
            && candidate.ContradictionRisk <= 0.35
            && candidate.IndependenceScore >= 0.55
            && !sameDomain;

        var relationshipStatus = independent
            ? "independent_candidate"
            : sameDomain
                ? "same_domain_candidate"
                : duplicateUrl
                    ? "duplicate_url"
                    : "rejected_candidate";

        var sourceStatus = independent
            ? "independent_candidate_pending_review"
            : sameDomain
                ? "same_domain_candidate_pending_review"
                : "candidate_rejected";

        var bucket = independent ? "independent" : sameDomain ? "same_domain" : duplicateUrl ? "duplicate" : "rejected";
        var accepted = independent || sameDomain;

        if (!accepted)
        {
            rejectionReason = "duplicate_or_same_root_group_or_low_independence";
        }

        return new CandidateClassification(bucket, independent, sameDomain, accepted, rejectionReason, sourceStatus)
        {
            DuplicateUrl = duplicateUrl,
            RelationshipStatus = relationshipStatus
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> BuildKnowledgeSourceDomains(
        SourceConfirmationReport confirmations,
        KnowledgeEvidenceReport evidence,
        IReadOnlyDictionary<string, string> sourceRegistry,
        EvidenceGraph? graph)
    {
        var domains = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in evidence.Evidence)
        {
            if (!domains.TryGetValue(entry.KnowledgeId, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                domains[entry.KnowledgeId] = set;
            }

            foreach (var sourceId in entry.SourceIds)
            {
                if (sourceRegistry.TryGetValue(sourceId, out var domain) && !string.IsNullOrWhiteSpace(domain))
                {
                    set.Add(domain);
                }
            }
        }

        foreach (var source in graph?.Sources ?? [])
        {
            if (string.IsNullOrWhiteSpace(source.Domain))
            {
                continue;
            }

            foreach (var result in confirmations.Results.Where(result => result.EvidenceRefs.Any(refId => refId.Contains(source.SourceId, StringComparison.OrdinalIgnoreCase))))
            {
                if (!domains.TryGetValue(result.KnowledgeId, out var set))
                {
                    set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    domains[result.KnowledgeId] = set;
                }

                set.Add(NormalizeDomain(source.Domain));
            }
        }

        return domains.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)pair.Value.ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static SourceConfirmationReport LoadSourceConfirmations()
    {
        var path = Path.Combine("/mnt/d/HermesData", "cognitive_core", "source_confirmations.json");
        if (!File.Exists(path))
        {
            return EmptySourceConfirmations("source_confirmation_missing");
        }

        try
        {
            return JsonSerializer.Deserialize<SourceConfirmationReport>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions) ?? EmptySourceConfirmations("source_confirmation_empty");
        }
        catch
        {
            return EmptySourceConfirmations("source_confirmation_missing");
        }
    }

    private static KnowledgeEvidenceReport LoadKnowledgeEvidence()
    {
        var path = Path.Combine("/mnt/d/HermesData", "cognitive_core", "knowledge_evidence.json");
        if (!File.Exists(path))
        {
            return new KnowledgeEvidenceReport("knowledge_evidence_v1", DateTimeOffset.UtcNow, [], true, true, true, true);
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeEvidenceReport>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions)
                ?? new KnowledgeEvidenceReport("knowledge_evidence_v1", DateTimeOffset.UtcNow, [], true, true, true, true);
        }
        catch
        {
            return new KnowledgeEvidenceReport("knowledge_evidence_v1", DateTimeOffset.UtcNow, [], true, true, true, true);
        }
    }

    private static EvidenceGraph? LoadEvidenceGraph()
    {
        var path = Path.Combine("/mnt/d/HermesData", "cognitive_core", "evidence_graph.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<EvidenceGraph>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<WebResearchImportCandidateRecord> LoadImportCandidates()
    {
        var path = Path.Combine("/mnt/d/HermesData", "reports", "web_research_source_collector", "web_research_import_candidates.json");
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<WebResearchImportCandidateRecord>>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static KnowledgeEvidenceSemanticMatcherReport? LoadMatcherReport()
    {
        var path = Path.Combine("/mnt/d/HermesData", "reports", "knowledge_evidence_matcher", "knowledge_evidence_matcher_report.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeEvidenceSemanticMatcherReport>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions);
        }
        catch
        {
            return null;
        }
    }

    private static SourceConfirmationReport EmptySourceConfirmations(string warning) =>
        new(
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

    private static SourceConfirmationReport ApplyUpdates(
        SourceConfirmationReport confirmations,
        IReadOnlyList<(string KnowledgeId, SourceCandidate Candidate, bool Independent)> updates,
        DateTimeOffset now)
    {
        var byKnowledge = updates
            .GroupBy(update => update.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        var results = confirmations.Results
            .Select(result =>
            {
                if (!byKnowledge.TryGetValue(result.KnowledgeId, out var candidates))
                {
                    return result;
                }

                var mergedSources = (result.CandidateSources ?? [])
                    .Concat(candidates.Select(update => update.Candidate))
                    .GroupBy(candidate => candidate.Url, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.Last())
                    .ToList();

                var independentCount = mergedSources.Count(candidate => candidate.SourceStatus.Equals("independent_candidate_pending_review", StringComparison.OrdinalIgnoreCase));

                return result with
                {
                    CandidateSources = mergedSources,
                    CandidateSourceCount = mergedSources.Count,
                    IndependentSourceCandidateCount = independentCount,
                    ReviewStatus = independentCount > 0 ? "candidate_second_source" : result.ReviewStatus,
                    Warnings = result.Warnings
                        .Concat(independentCount > 0 ? ["independent_candidate_pending_review"] : [])
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
                .Concat(["independent_source_resolver_applied"])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static string BuildMarkdown(IndependentSourceResolverReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Independent Source Resolver");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Loaded Candidates: {report.LoadedCandidates}");
        sb.AppendLine($"- Evaluated Existing Candidate Sources: {report.EvaluatedExistingCandidateSources}");
        sb.AppendLine($"- Duplicate Import Candidates: {report.DuplicateImportCandidates}");
        sb.AppendLine($"- True Duplicates: {report.TrueDuplicates}");
        sb.AppendLine($"- Same Domain Candidates: {report.SameDomainCandidates}");
        sb.AppendLine($"- Independent Existing Candidates: {report.IndependentExistingCandidates}");
        sb.AppendLine($"- Independent Candidates: {report.IndependentCandidates}");
        sb.AppendLine($"- Rejected Candidates: {report.RejectedCandidates}");
        sb.AppendLine($"- Ready For Human Review: {report.ReadyForHumanReview}");
        sb.AppendLine($"- Affected Knowledge Items: {report.AffectedKnowledgeItems}");
        sb.AppendLine($"- Applied Candidates: {report.AppliedCandidates}");
        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine($"- no_trading_execution: {report.NoTradingExecution}");
        sb.AppendLine($"- no_broker_action: {report.NoBrokerAction}");
        sb.AppendLine($"- no_auto_trading: {report.NoAutoTrading}");
        sb.AppendLine($"- human_review_required: {report.HumanReviewRequired}");
        if (report.Candidates.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Candidates");
            foreach (var candidate in report.Candidates.Take(20))
            {
                sb.AppendLine($"- {candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.RelationshipStatus} | status={candidate.SourceStatus} | sem={candidate.SemanticMatchScore:0.###} | indep={candidate.IndependenceScore:0.###} | contradiction={candidate.ContradictionRisk:0.###}");
            }
        }

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
        }

        return sb.ToString();
    }

    private sealed record CandidateClassification(
        string Bucket,
        bool Independent,
        bool SameDomain,
        bool Accepted,
        string? RejectionReason,
        string SourceStatus)
    {
        public bool DuplicateUrl { get; init; }
        public string RelationshipStatus { get; init; } = "unclassified";
        public bool ReadyForHumanReview => Independent;
    }

    private sealed record ExistingCandidateSourceRecord(string KnowledgeItemId, SourceCandidate Candidate);

    private static string? GetHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Host;
    }

    private static string NormalizeDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[4..];
        }

        return normalized;
    }

    private static string GetRootPublisherGroup(string? host)
    {
        host = NormalizeDomain(host);
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        if (host.EndsWith("babypips.com", StringComparison.OrdinalIgnoreCase))
        {
            return "babypips.com";
        }

        if (host.EndsWith("ctrader.com", StringComparison.OrdinalIgnoreCase))
        {
            return "ctrader.com";
        }

        if (host.EndsWith("spotware.com", StringComparison.OrdinalIgnoreCase))
        {
            return "spotware.com";
        }

        if (host.EndsWith("microsoft.com", StringComparison.OrdinalIgnoreCase))
        {
            return "microsoft.com";
        }

        if (host.EndsWith("github.com", StringComparison.OrdinalIgnoreCase))
        {
            return "github.com";
        }

        if (host.EndsWith("trading.de", StringComparison.OrdinalIgnoreCase))
        {
            return "trading.de";
        }

        var parts = host.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2 ? string.Join('.', parts[^2..]) : host;
    }

    private static bool DomainsMatch(string left, string right) =>
        GetRootPublisherGroup(left).Equals(GetRootPublisherGroup(right), StringComparison.OrdinalIgnoreCase);

    private static bool UrlsMatch(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return NormalizeUrl(left).Equals(NormalizeUrl(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string MakeKey(string knowledgeItemId, string url) =>
        $"{NormalizeDomain(knowledgeItemId)}||{NormalizeDomain(url)}";

    private static string NormalizeUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return NormalizeDomain(value);
        }

        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty,
            Query = string.Empty
        };

        return builder.Uri.ToString().TrimEnd('/').ToLowerInvariant();
    }
}
