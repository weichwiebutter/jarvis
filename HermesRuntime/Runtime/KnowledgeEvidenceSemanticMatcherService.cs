using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hermes.Runtime;

public sealed record KnowledgeEvidenceSemanticMatchCandidate(
    string KnowledgeItemId,
    string Title,
    string Url,
    string Domain,
    string SourceType,
    string ExcerptOrSummary,
    string EvidenceReason,
    string IndependenceClaim,
    string HumanReviewStatus,
    IReadOnlyList<string> SafetyFlags,
    double SemanticMatchScore,
    double IndependenceScore,
    double EvidenceCoverageScore,
    double ContradictionRisk,
    string Status,
    IReadOnlyList<string> MatchedTerms,
    IReadOnlyList<string> EvidenceRefs,
    bool ReadyForHumanSourceReview,
    string EvidenceMatchStatus,
    string? RejectionReason = null);

public sealed record KnowledgeEvidenceSemanticMatcherReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedCandidates,
    int LoadedKnowledgeItems,
    int LoadedQualityItems,
    int LoadedEvidenceItems,
    int LoadedGraphNodes,
    int CandidateRelevant,
    int CandidateWeak,
    int CandidateRejected,
    int NeedsHumanReview,
    int AppliedCandidates,
    IReadOnlyList<KnowledgeEvidenceSemanticMatchCandidate> Candidates,
    IReadOnlyList<KnowledgeEvidenceSemanticMatchCandidate> Accepted,
    IReadOnlyList<KnowledgeEvidenceSemanticMatchCandidate> Rejected,
    IReadOnlyList<string> Warnings,
    string SourceConfirmationsPath,
    string ImportCandidatesPath,
    string KnowledgeQualityPath,
    string KnowledgeEvidencePath,
    string EvidenceGraphPath,
    string ReportPath,
    string MarkdownPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class KnowledgeEvidenceSemanticMatcherService
{
    private readonly StoragePaths _storagePaths;

    public KnowledgeEvidenceSemanticMatcherService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_evidence_matcher");
    public string SourceConfirmationsPath => Path.Combine(_storagePaths.Root, "cognitive_core", "source_confirmations.json");
    public string ImportCandidatesPath => Path.Combine(_storagePaths.Root, "reports", "web_research_source_collector", "web_research_import_candidates.json");
    public string KnowledgeQualityPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_quality.json");
    public string KnowledgeEvidencePath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_evidence.json");
    public string EvidenceGraphPath => Path.Combine(_storagePaths.Root, "cognitive_core", "evidence_graph.json");
    public string ReportPath => Path.Combine(Root, "knowledge_evidence_matcher_report.json");
    public string MarkdownPath => Path.Combine(Root, "knowledge_evidence_matcher_report.md");

    public KnowledgeEvidenceSemanticMatcherReport Run(bool apply)
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var candidates = LoadImportCandidates();
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var quality = LoadKnowledgeQuality();
        var evidence = LoadKnowledgeEvidence();
        var graph = LoadEvidenceGraph();
        var confirmations = LoadSourceConfirmations();
        var candidateByKnowledgeId = candidates
            .GroupBy(candidate => candidate.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);
        var qualityById = quality.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var evidenceById = evidence.Evidence.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var graphSourceByDomain = graph?.Sources
            .GroupBy(source => NormalizeDomain(source.Domain), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, List<EvidenceSourceReference>>(StringComparer.OrdinalIgnoreCase);
        var confirmationsById = confirmations.Results.ToDictionary(result => result.KnowledgeId, StringComparer.OrdinalIgnoreCase);

        var matched = new List<KnowledgeEvidenceSemanticMatchCandidate>();
        var relevant = new List<KnowledgeEvidenceSemanticMatchCandidate>();
        var weak = new List<KnowledgeEvidenceSemanticMatchCandidate>();
        var rejected = new List<KnowledgeEvidenceSemanticMatchCandidate>();
        var needsHumanReview = new List<KnowledgeEvidenceSemanticMatchCandidate>();
        var updates = new List<(string KnowledgeId, SourceCandidate Candidate)>();
        var warnings = new List<string>();

        foreach (var item in catalog)
        {
            if (!candidateByKnowledgeId.TryGetValue(item.Id, out var itemCandidates))
            {
                continue;
            }

            var itemQuality = qualityById.GetValueOrDefault(item.Id);
            var itemEvidence = evidenceById.GetValueOrDefault(item.Id);
            var itemSourceDomains = ResolveItemSourceDomains(item, graph);

            foreach (var candidate in itemCandidates)
            {
                var evaluation = EvaluateCandidate(item, itemQuality, itemEvidence, itemSourceDomains, candidate, graphSourceByDomain, confirmationsById);
                matched.Add(evaluation);
                switch (evaluation.Status)
                {
                    case "candidate_relevant":
                        relevant.Add(evaluation);
                        break;
                    case "candidate_weak":
                        weak.Add(evaluation);
                        break;
                    case "needs_human_review":
                        needsHumanReview.Add(evaluation);
                        break;
                    default:
                        rejected.Add(evaluation);
                        break;
                }

                if (!apply || evaluation.Status == "candidate_rejected")
                {
                    continue;
                }

                if (evaluation.Status is "candidate_relevant" or "candidate_weak" or "needs_human_review")
                {
                    updates.Add((candidate.KnowledgeItemId, new SourceCandidate(
                        Url: candidate.Url,
                        Domain: candidate.Domain,
                        SourceType: candidate.SourceType,
                        ExcerptOrSummary: candidate.ExcerptOrSummary,
                        RetrievedAtUtc: candidate.RetrievedAtUtc,
                        EvidenceReason: candidate.EvidenceReason,
                        IndependenceClaim: candidate.IndependenceClaim,
                        HumanReviewStatus: "pending",
                        SafetyFlags: candidate.SafetyFlags.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                        SemanticMatchScore: evaluation.SemanticMatchScore,
                        IndependenceScore: evaluation.IndependenceScore,
                        EvidenceCoverageScore: evaluation.EvidenceCoverageScore,
                        ContradictionRisk: evaluation.ContradictionRisk,
                        EvidenceMatchStatus: evaluation.Status == "candidate_relevant" ? "matched_pending_review" : "candidate_pending_review",
                        ReadyForHumanSourceReview: evaluation.Status == "candidate_relevant")));
                }
            }
        }

        if (apply && updates.Count > 0)
        {
            var updated = ApplyUpdates(confirmations, updates, now);
            File.WriteAllText(SourceConfirmationsPath, JsonSerializer.Serialize(updated, JsonDefaults.WriteOptions));
        }

        var report = new KnowledgeEvidenceSemanticMatcherReport(
            ReportVersion: "knowledge_evidence_semantic_match_v1",
            UpdatedAtUtc: now,
            Status: matched.Count == 0 ? "no_candidates_loaded" : apply ? "applied" : "dry_run_ready",
            LoadedCandidates: candidates.Count,
            LoadedKnowledgeItems: catalog.Count,
            LoadedQualityItems: quality.Items.Count,
            LoadedEvidenceItems: evidence.Evidence.Count,
            LoadedGraphNodes: graph?.Nodes ?? 0,
            CandidateRelevant: relevant.Count,
            CandidateWeak: weak.Count,
            CandidateRejected: rejected.Count,
            NeedsHumanReview: needsHumanReview.Count,
            AppliedCandidates: apply ? updates.Count : 0,
            Candidates: matched,
            Accepted: relevant.Concat(weak).Concat(needsHumanReview).ToList(),
            Rejected: rejected,
            Warnings: warnings.Concat(matched.Count == 0 ? ["no_matching_candidates"] : []).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SourceConfirmationsPath: SourceConfirmationsPath,
            ImportCandidatesPath: ImportCandidatesPath,
            KnowledgeQualityPath: KnowledgeQualityPath,
            KnowledgeEvidencePath: KnowledgeEvidencePath,
            EvidenceGraphPath: EvidenceGraphPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public KnowledgeEvidenceSemanticMatcherReport LoadStatus()
    {
        if (!File.Exists(ReportPath))
        {
            return Run(apply: false) with { Status = "status_snapshot_generated" };
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeEvidenceSemanticMatcherReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions) ?? Run(apply: false) with { Status = "status_snapshot_generated" };
        }
        catch
        {
            return Run(apply: false) with { Status = "status_snapshot_generated" };
        }
    }

    private static KnowledgeEvidenceSemanticMatchCandidate EvaluateCandidate(
        KnowledgeCatalogItem item,
        KnowledgeQualityItem? quality,
        KnowledgeEvidenceEntry? evidence,
        IReadOnlyList<string> itemSourceDomains,
        WebResearchImportCandidateRecord candidate,
        IReadOnlyDictionary<string, List<EvidenceSourceReference>> graphSourceByDomain,
        IReadOnlyDictionary<string, ConfirmationResult> confirmationsById)
    {
        var knowledgeTerms = BuildKnowledgeTerms(item);
        var candidateText = NormalizeText($"{candidate.Title} {candidate.Url} {candidate.ExcerptOrSummary}");
        var titleTerms = CountMatchedTerms(NormalizeText(candidate.Title), knowledgeTerms);
        var urlTerms = CountMatchedTerms(NormalizeText(candidate.Url), knowledgeTerms);
        var summaryTerms = CountMatchedTerms(NormalizeText(candidate.ExcerptOrSummary), knowledgeTerms);
        var evidenceTypeBoost = EvidenceTypeBoost(candidate.ExcerptOrSummary);
        var queryBoost = ContainsAny(candidateText, new[] { "definition", "strategy", "example", "risk", "application", "use case" }) ? 0.12 : 0;

        var semanticMatch = Math.Clamp(
            titleTerms * 0.22
            + urlTerms * 0.12
            + summaryTerms * 0.26
            + evidenceTypeBoost
            + queryBoost
            + DomainSpecificBoost(candidate.Domain, item.Domain),
            0,
            1);

        var independence = ComputeIndependence(candidate.Domain, itemSourceDomains, graphSourceByDomain);
        var coverage = ComputeEvidenceCoverage(candidate, evidence, quality);
        var contradictionRisk = ComputeContradictionRisk(candidate, quality, itemSourceDomains, confirmationsById.TryGetValue(item.Id, out var confirmation) ? confirmation : null);

        var status = DetermineStatus(semanticMatch, independence, coverage, contradictionRisk);
        var matchedTerms = knowledgeTerms
            .Where(term => ContainsAny(candidateText, term))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        var evidenceRefs = new List<string>();
        if (evidence is not null)
        {
            evidenceRefs.AddRange(evidence.SourceEvidenceRefs.Take(6));
            evidenceRefs.AddRange(evidence.ValidationEvidenceRefs.Take(6));
        }
        if (quality is not null)
        {
            evidenceRefs.AddRange(quality.EvidenceRefs.Take(6));
        }

        var ready = status == "candidate_relevant";
        var evidenceMatchStatus = ready ? "matched_pending_review" : status == "candidate_weak" ? "weak_pending_review" : status == "needs_human_review" ? "needs_human_review" : "rejected";

        return new KnowledgeEvidenceSemanticMatchCandidate(
            KnowledgeItemId: candidate.KnowledgeItemId,
            Title: candidate.Title,
            Url: candidate.Url,
            Domain: candidate.Domain,
            SourceType: candidate.SourceType,
            ExcerptOrSummary: candidate.ExcerptOrSummary,
            EvidenceReason: candidate.EvidenceReason,
            IndependenceClaim: candidate.IndependenceClaim,
            HumanReviewStatus: "pending",
            SafetyFlags: candidate.SafetyFlags,
            SemanticMatchScore: semanticMatch,
            IndependenceScore: independence,
            EvidenceCoverageScore: coverage,
            ContradictionRisk: contradictionRisk,
            Status: status,
            MatchedTerms: matchedTerms,
            EvidenceRefs: evidenceRefs.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReadyForHumanSourceReview: ready,
            EvidenceMatchStatus: evidenceMatchStatus,
            RejectionReason: status == "candidate_rejected" ? "semantic_mismatch_or_high_contradiction_risk" : null);
    }

    private static string DetermineStatus(double semanticMatch, double independence, double coverage, double contradictionRisk)
    {
        if (semanticMatch >= 0.55 && independence >= 0.5 && contradictionRisk <= 0.35)
        {
            return "candidate_relevant";
        }

        if (semanticMatch >= 0.35 && contradictionRisk <= 0.55)
        {
            return "candidate_weak";
        }

        if (semanticMatch >= 0.22 || coverage >= 0.35)
        {
            return "needs_human_review";
        }

        return "candidate_rejected";
    }

    private static double ComputeIndependence(
        string candidateDomain,
        IReadOnlyList<string> itemSourceDomains,
        IReadOnlyDictionary<string, List<EvidenceSourceReference>> graphSourceByDomain)
    {
        var normalizedCandidateDomain = NormalizeDomain(candidateDomain);
        var itemDomainMatch = itemSourceDomains.Any(domain => DomainsMatch(normalizedCandidateDomain, domain));
        var graphDomainMatch = graphSourceByDomain.ContainsKey(normalizedCandidateDomain);
        var score = 1d;
        if (itemDomainMatch)
        {
            score -= 0.5;
        }
        if (graphDomainMatch)
        {
            score -= 0.2;
        }
        return Math.Round(Math.Clamp(score, 0, 1), 4);
    }

    private static double ComputeEvidenceCoverage(WebResearchImportCandidateRecord candidate, KnowledgeEvidenceEntry? evidence, KnowledgeQualityItem? quality)
    {
        var coverage = 0d;
        var text = NormalizeText($"{candidate.Title} {candidate.ExcerptOrSummary}");
        if (ContainsAny(text, new[] { "definition", "what is", "means", "explains" }))
        {
            coverage += 0.24;
        }
        if (ContainsAny(text, new[] { "strategy", "setup", "entry", "exit", "application", "use case" }))
        {
            coverage += 0.26;
        }
        if (ContainsAny(text, new[] { "risk", "invalidat", "stop loss", "take profit", "drawdown" }))
        {
            coverage += 0.22;
        }
        if (evidence is not null)
        {
            coverage += Math.Min(0.18, evidence.SourceIds.Count * 0.04);
            coverage += Math.Min(0.12, evidence.ValidationEvidenceRefs.Count > 0 ? 0.08 : 0);
        }
        if (quality is not null)
        {
            coverage += Math.Min(0.08, quality.EvidenceScore * 0.08);
        }
        return Math.Round(Math.Clamp(coverage, 0, 1), 4);
    }

    private static double EvidenceTypeBoost(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0d;
        }

        var normalized = NormalizeText(text);
        var boost = 0d;
        if (ContainsAny(normalized, new[] { "definition", "what is", "meaning", "explains", "overview" }))
        {
            boost += 0.08;
        }
        if (ContainsAny(normalized, new[] { "strategy", "setup", "entry", "signal", "pattern", "example" }))
        {
            boost += 0.1;
        }
        if (ContainsAny(normalized, new[] { "risk", "invalidat", "stop loss", "take profit", "drawdown", "failure" }))
        {
            boost += 0.08;
        }

        return Math.Round(Math.Clamp(boost, 0, 0.24), 4);
    }

    private static double ComputeContradictionRisk(
        WebResearchImportCandidateRecord candidate,
        KnowledgeQualityItem? quality,
        IReadOnlyList<string> itemSourceDomains,
        ConfirmationResult? confirmation)
    {
        var risk = 0d;
        var text = NormalizeText($"{candidate.Title} {candidate.ExcerptOrSummary}");
        if (ContainsAny(text, new[] { "not", "never", "avoid", "warning", "error", "deprecated", "obsolete" }))
        {
            risk += 0.18;
        }
        if (itemSourceDomains.Any(domain => DomainsMatch(domain, candidate.Domain)))
        {
            risk += 0.24;
        }
        if (quality is not null && quality.TrustScore < 0.5)
        {
            risk += 0.15;
        }
        if (confirmation is not null && confirmation.ValidationEvidenceCount == 0)
        {
            risk += 0.08;
        }
        return Math.Round(Math.Clamp(risk, 0, 1), 4);
    }

    private static double DomainSpecificBoost(string candidateDomain, string itemDomain)
    {
        var cd = NormalizeDomain(candidateDomain);
        var id = NormalizeDomain(itemDomain);
        if (DomainsMatch(cd, id))
        {
            return 0.12;
        }
        if ((id == "trading" || id == "documentation") && (cd.Contains("ctrader", StringComparison.OrdinalIgnoreCase) || cd.Contains("spotware", StringComparison.OrdinalIgnoreCase)))
        {
            return 0.1;
        }
        if (id == "software" && (cd.Contains("github", StringComparison.OrdinalIgnoreCase) || cd.Contains("microsoft", StringComparison.OrdinalIgnoreCase)))
        {
            return 0.1;
        }
        return 0;
    }

    private static int CountMatchedTerms(string text, IReadOnlyList<string> terms) =>
        terms.Count(term => ContainsAny(text, term));

    private static IReadOnlyList<string> BuildKnowledgeTerms(KnowledgeCatalogItem item)
    {
        var terms = new List<string>();
        Add(item.Title);
        Add(item.DescriptionShort);
        Add(item.Id.Replace(':', ' '));
        foreach (var tag in item.Tags)
        {
            Add(tag);
        }

        foreach (var related in item.RelatedItems)
        {
            Add(related);
        }

        if (terms.Count == 0)
        {
            Add(item.Id);
        }

        return terms.Distinct(StringComparer.OrdinalIgnoreCase).Take(24).ToList();

        void Add(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            var normalized = NormalizeText(value);
            foreach (var part in Regex.Split(normalized, "[^a-z0-9]+"))
            {
                if (part.Length >= 3)
                {
                    terms.Add(part);
                }
            }
        }
    }

    private IReadOnlyList<string> ResolveItemSourceDomains(KnowledgeCatalogItem item, EvidenceGraph? graph)
    {
        var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceRegistry = new KnowledgeSourceRegistry(_storagePaths).LoadOrCreateSources()
            .ToDictionary(source => source.SourceId, StringComparer.OrdinalIgnoreCase);

        foreach (var sourceId in item.SourceIds)
        {
            if (sourceRegistry.TryGetValue(sourceId, out var source))
            {
                domains.Add(NormalizeDomain(source.Domain));
            }
        }

        if (graph is not null)
        {
            foreach (var sourceId in item.SourceIds)
            {
                var sourceNode = graph.Sources.FirstOrDefault(source => source.SourceId.Equals(sourceId, StringComparison.OrdinalIgnoreCase));
                if (sourceNode is not null)
                {
                    domains.Add(NormalizeDomain(sourceNode.Domain));
                }
            }
        }

        return domains.ToList();
    }

    private IReadOnlyList<WebResearchImportCandidateRecord> LoadImportCandidates()
    {
        if (!File.Exists(ImportCandidatesPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<WebResearchImportCandidateRecord>>(File.ReadAllText(ImportCandidatesPath), JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private KnowledgeQualityReport LoadKnowledgeQuality()
    {
        if (!File.Exists(KnowledgeQualityPath))
        {
            return EmptyQuality();
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeQualityReport>(File.ReadAllText(KnowledgeQualityPath), JsonDefaults.SnapshotReadOptions) ?? EmptyQuality();
        }
        catch
        {
            return EmptyQuality();
        }
    }

    private KnowledgeEvidenceReport LoadKnowledgeEvidence()
    {
        if (!File.Exists(KnowledgeEvidencePath))
        {
            return new KnowledgeEvidenceReport("knowledge_evidence_v1", DateTimeOffset.UtcNow, [], true, true, true, true);
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeEvidenceReport>(File.ReadAllText(KnowledgeEvidencePath), JsonDefaults.SnapshotReadOptions)
                ?? new KnowledgeEvidenceReport("knowledge_evidence_v1", DateTimeOffset.UtcNow, [], true, true, true, true);
        }
        catch
        {
            return new KnowledgeEvidenceReport("knowledge_evidence_v1", DateTimeOffset.UtcNow, [], true, true, true, true);
        }
    }

    private EvidenceGraph? LoadEvidenceGraph()
    {
        if (!File.Exists(EvidenceGraphPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<EvidenceGraph>(File.ReadAllText(EvidenceGraphPath), JsonDefaults.SnapshotReadOptions);
        }
        catch
        {
            return null;
        }
    }

    private SourceConfirmationReport LoadSourceConfirmations()
    {
        if (!File.Exists(SourceConfirmationsPath))
        {
            return EmptySourceConfirmations("source_confirmation_missing");
        }

        try
        {
            return JsonSerializer.Deserialize<SourceConfirmationReport>(File.ReadAllText(SourceConfirmationsPath), JsonDefaults.SnapshotReadOptions)
                ?? EmptySourceConfirmations("source_confirmation_empty");
        }
        catch
        {
            return EmptySourceConfirmations("source_confirmation_missing");
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

    private static KnowledgeQualityReport EmptyQuality() =>
        new(
            ReportVersion: "knowledge_quality_v2",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TotalKnowledgeItems: 0,
            TrustedKnowledge: 0,
            WeakKnowledge: 0,
            DeprecatedKnowledge: 0,
            AverageQualityScore: 0,
            AverageTrustScore: 0,
            KnowledgeHealth: "unknown",
            KnowledgeTrend: "flat",
            Items: [],
            Warnings: ["knowledge_quality_missing"],
            EvidencePath: "",
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

    private static SourceConfirmationReport ApplyUpdates(
        SourceConfirmationReport confirmations,
        IReadOnlyList<(string KnowledgeId, SourceCandidate Candidate)> updates,
        DateTimeOffset now)
    {
        var grouped = updates
            .GroupBy(update => update.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(update => update.Candidate).ToList(), StringComparer.OrdinalIgnoreCase);

        var results = confirmations.Results
            .Select(result =>
            {
                if (!grouped.TryGetValue(result.KnowledgeId, out var candidateSources))
                {
                    return result;
                }

                var merged = (result.CandidateSources ?? [])
                    .Concat(candidateSources)
                    .GroupBy(candidate => candidate.Url, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group
                        .OrderByDescending(candidate => candidate.SemanticMatchScore)
                        .ThenByDescending(candidate => candidate.IndependenceScore)
                        .ThenByDescending(candidate => candidate.EvidenceCoverageScore)
                        .ThenByDescending(candidate => candidate.PolicyReviewedAtUtc ?? DateTimeOffset.MinValue)
                        .ThenByDescending(candidate => candidate.RetrievedAtUtc)
                        .First())
                    .ToList();

                return result with
                {
                    CandidateSources = merged,
                    CandidateSourceCount = Math.Max(result.CandidateSourceCount, merged.Count),
                    ReviewStatus = merged.Count >= 2 ? "candidate_second_source" : "awaiting_human_review"
                };
            })
            .ToList();

        return confirmations with
        {
            Results = results,
            UpdatedAtUtc = now,
            ItemsAnalyzed = results.Count,
            ConfirmationDistribution = results
                .GroupBy(result => result.ConfirmationLevel, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase)
        };
    }

    private static string BuildMarkdown(KnowledgeEvidenceSemanticMatcherReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Knowledge Evidence Semantic Matcher");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Loaded Candidates: {report.LoadedCandidates}");
        sb.AppendLine($"- Candidate Relevant: {report.CandidateRelevant}");
        sb.AppendLine($"- Candidate Weak: {report.CandidateWeak}");
        sb.AppendLine($"- Candidate Rejected: {report.CandidateRejected}");
        sb.AppendLine($"- Needs Human Review: {report.NeedsHumanReview}");
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
                sb.AppendLine($"- {candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Status} | semantic={candidate.SemanticMatchScore:0.###} | independence={candidate.IndependenceScore:0.###} | coverage={candidate.EvidenceCoverageScore:0.###} | contradiction={candidate.ContradictionRisk:0.###}");
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

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim().ToLowerInvariant(), "\\s+", " ");

    private static string NormalizeDomain(string? value)
    {
        var normalized = NormalizeText(value);
        if (normalized.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[4..];
        }
        return normalized;
    }

    private static bool DomainsMatch(string left, string right) =>
        !string.IsNullOrWhiteSpace(left)
        && !string.IsNullOrWhiteSpace(right)
        && (left.Equals(right, StringComparison.OrdinalIgnoreCase)
            || left.EndsWith($".{right}", StringComparison.OrdinalIgnoreCase)
            || right.EndsWith($".{left}", StringComparison.OrdinalIgnoreCase));

    private static bool ContainsAny(string haystack, IEnumerable<string> terms) =>
        terms.Any(term => ContainsAny(haystack, term));

    private static bool ContainsAny(string haystack, string term)
    {
        haystack = NormalizeText(haystack);
        term = NormalizeText(term);
        return !string.IsNullOrWhiteSpace(haystack) && !string.IsNullOrWhiteSpace(term) && haystack.Contains(term, StringComparison.OrdinalIgnoreCase);
    }
}
