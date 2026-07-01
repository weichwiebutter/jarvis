using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hermes.Runtime;

public sealed record AutoSourceReviewCandidate(
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
    double ContradictionRisk,
    bool DomainAllowed,
    bool InTrustedCatalog,
    bool IsForumOrCommunity,
    bool IsNavigationOrBrokerLike,
    bool DuplicateUrl,
    bool AutoApprovedByPolicy,
    bool HumanReviewRequired,
    bool Rejected,
    string SourceStatus,
    string ReviewStatus,
    string PolicyDecision,
    string PolicyReason,
    int SourceCountBeforePolicy,
    int SourceCountAfterPolicy,
    IReadOnlyList<string> MatchedTerms);

public sealed record AutoSourceReviewReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedCandidateSources,
    int EvaluatedCandidateSources,
    int AutoApprovedCandidates,
    int HumanReviewCandidates,
    int RejectedCandidates,
    int AppliedCandidates,
    int DuplicateCandidates,
    int PolicyApprovedKnowledgeItems,
    int SourceCountIncreasedKnowledgeItems,
    IReadOnlyList<AutoSourceReviewCandidate> Candidates,
    IReadOnlyList<AutoSourceReviewCandidate> AutoApproved,
    IReadOnlyList<AutoSourceReviewCandidate> HumanReview,
    IReadOnlyList<AutoSourceReviewCandidate> Rejected,
    IReadOnlyList<string> Warnings,
    string SourceConfirmationsPath,
    string MatcherReportPath,
    string TrustedSourceCatalogPath,
    string ReportPath,
    string MarkdownPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool ResearchOnly);

public sealed class AutoSourceReviewPolicyService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public AutoSourceReviewPolicyService(StoragePaths storagePaths, string? runtimeRoot = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot ?? Directory.GetCurrentDirectory();
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "auto_source_review");
    public string SourceConfirmationsPath => Path.Combine(_storagePaths.Root, "cognitive_core", "source_confirmations.json");
    public string MatcherReportPath => Path.Combine(_storagePaths.Root, "reports", "knowledge_evidence_matcher", "knowledge_evidence_matcher_report.json");
    public string TrustedSourceCatalogPath => Path.Combine(_runtimeRoot, "config", "trusted_source_catalog.json");
    public string ReportPath => Path.Combine(Root, "auto_source_review_report.json");
    public string MarkdownPath => Path.Combine(Root, "auto_source_review_report.md");

    public AutoSourceReviewReport Run(bool apply)
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var confirmations = LoadSourceConfirmations();
        var matcherReport = LoadMatcherReport();
        var catalogEntries = new TrustedSourceCatalogService(_storagePaths, _runtimeRoot).LoadCatalog();
        var allowedDomains = catalogEntries
            .Where(entry => entry.Allowed)
            .Select(entry => NormalizeDomain(entry.Domain))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var catalogByDomain = catalogEntries
            .GroupBy(entry => NormalizeDomain(entry.Domain), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var matcherByKey = matcherReport?.Candidates
            .GroupBy(candidate => MakeKey(candidate.KnowledgeItemId, candidate.Url), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeEvidenceSemanticMatchCandidate>(StringComparer.OrdinalIgnoreCase);

        var candidates = new List<AutoSourceReviewCandidate>();
        var autoApproved = new List<AutoSourceReviewCandidate>();
        var humanReview = new List<AutoSourceReviewCandidate>();
        var rejected = new List<AutoSourceReviewCandidate>();
        var warnings = new List<string>();
        var seenUrlsByKnowledge = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var updates = new List<(string KnowledgeId, SourceCandidate Candidate)>();
        var policyApprovedKnowledgeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sourceCountIncreasedKnowledgeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in confirmations.Results)
        {
            if (!seenUrlsByKnowledge.TryGetValue(result.KnowledgeId, out var seen))
            {
                seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                seenUrlsByKnowledge[result.KnowledgeId] = seen;
            }

            foreach (var candidate in result.CandidateSources ?? [])
            {
                var normalizedUrl = NormalizeUrl(candidate.Url);
                var duplicateUrl = !seen.Add(normalizedUrl);
                var matcherCandidate = matcherByKey.GetValueOrDefault(MakeKey(result.KnowledgeId, candidate.Url));
                var evaluation = EvaluateCandidate(result.KnowledgeId, result.SourceCount, candidate, matcherCandidate, catalogByDomain, allowedDomains, duplicateUrl);
                candidates.Add(evaluation);

                switch (evaluation.PolicyDecision)
                {
                    case "auto_approved":
                        autoApproved.Add(evaluation);
                        policyApprovedKnowledgeIds.Add(result.KnowledgeId);
                        sourceCountIncreasedKnowledgeIds.Add(result.KnowledgeId);
                        updates.Add((result.KnowledgeId, candidate with
                        {
                            AutoApprovedByPolicy = true,
                            PolicyReviewStatus = "approved",
                            PolicyApprovalReason = evaluation.PolicyReason,
                            PolicyReviewedAtUtc = now,
                            HumanReviewStatus = "policy_approved",
                            ReadyForHumanSourceReview = false,
                            EvidenceMatchStatus = "policy_approved_second_source",
                            SourceStatus = "policy_approved_second_source",
                            IndependentSourceCandidateCount = Math.Max(candidate.IndependentSourceCandidateCount, 1)
                        }));
                        break;
                    case "human_review":
                        humanReview.Add(evaluation);
                        updates.Add((result.KnowledgeId, candidate with
                        {
                            AutoApprovedByPolicy = false,
                            PolicyReviewStatus = "human_review_required",
                            PolicyApprovalReason = evaluation.PolicyReason,
                            PolicyReviewedAtUtc = now,
                            HumanReviewStatus = "pending",
                            ReadyForHumanSourceReview = true,
                            EvidenceMatchStatus = candidate.EvidenceMatchStatus is "matched_pending_review" or "candidate_pending_review"
                                ? candidate.EvidenceMatchStatus
                                : "matched_pending_review",
                            SourceStatus = "policy_human_review_required"
                        }));
                        break;
                    default:
                        rejected.Add(evaluation);
                        updates.Add((result.KnowledgeId, candidate with
                        {
                            AutoApprovedByPolicy = false,
                            PolicyReviewStatus = "rejected",
                            PolicyApprovalReason = evaluation.PolicyReason,
                            PolicyReviewedAtUtc = now,
                            HumanReviewStatus = "rejected",
                            ReadyForHumanSourceReview = false,
                            SourceStatus = "policy_rejected",
                            EvidenceMatchStatus = "policy_rejected"
                        }));
                        break;
                }
            }
        }

        if (apply && updates.Count > 0)
        {
            var updated = ApplyUpdates(confirmations, updates, now);
            File.WriteAllText(SourceConfirmationsPath, JsonSerializer.Serialize(updated, JsonDefaults.WriteOptions));
        }

        var report = new AutoSourceReviewReport(
            ReportVersion: "auto_source_review_v1",
            UpdatedAtUtc: now,
            Status: candidates.Count == 0 ? "no_candidate_sources_loaded" : apply ? "applied" : "dry_run_ready",
            LoadedCandidateSources: confirmations.Results.Sum(result => (result.CandidateSources ?? []).Count),
            EvaluatedCandidateSources: candidates.Count,
            AutoApprovedCandidates: autoApproved.Count,
            HumanReviewCandidates: humanReview.Count,
            RejectedCandidates: rejected.Count,
            AppliedCandidates: apply ? updates.Count : 0,
            DuplicateCandidates: candidates.Count(candidate => candidate.DuplicateUrl),
            PolicyApprovedKnowledgeItems: policyApprovedKnowledgeIds.Count,
            SourceCountIncreasedKnowledgeItems: sourceCountIncreasedKnowledgeIds.Count,
            Candidates: candidates,
            AutoApproved: autoApproved,
            HumanReview: humanReview,
            Rejected: rejected,
            Warnings: warnings.Concat(candidates.Count == 0 ? ["no_candidate_sources_loaded"] : []).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SourceConfirmationsPath: SourceConfirmationsPath,
            MatcherReportPath: MatcherReportPath,
            TrustedSourceCatalogPath: TrustedSourceCatalogPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ResearchOnly: true);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public AutoSourceReviewReport LoadStatus()
    {
        if (!File.Exists(ReportPath))
        {
            return Run(apply: false) with { Status = "status_snapshot_generated" };
        }

        try
        {
            return JsonSerializer.Deserialize<AutoSourceReviewReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions)
                ?? Run(apply: false) with { Status = "status_snapshot_generated" };
        }
        catch
        {
            return Run(apply: false) with { Status = "status_snapshot_generated" };
        }
    }

    private static AutoSourceReviewCandidate EvaluateCandidate(
        string knowledgeItemId,
        int sourceCountBeforePolicy,
        SourceCandidate candidate,
        KnowledgeEvidenceSemanticMatchCandidate? matcherCandidate,
        IReadOnlyDictionary<string, TrustedSourceCatalogEntry> catalogByDomain,
        ISet<string> allowedDomains,
        bool duplicateUrl)
    {
        var semantic = candidate.SemanticMatchScore > 0 ? candidate.SemanticMatchScore : matcherCandidate?.SemanticMatchScore ?? 0;
        var independence = candidate.IndependenceScore > 0 ? candidate.IndependenceScore : matcherCandidate?.IndependenceScore ?? 0;
        var contradiction = candidate.ContradictionRisk > 0 ? candidate.ContradictionRisk : matcherCandidate?.ContradictionRisk ?? 1;
        var sourceType = candidate.SourceType ?? string.Empty;
        var domain = NormalizeDomain(candidate.Domain);
        var inCatalog = catalogByDomain.ContainsKey(domain);
        var domainAllowed = inCatalog && allowedDomains.Contains(domain);
        var catalogEntry = catalogByDomain.GetValueOrDefault(domain);
        var isForumOrCommunity = IsForumLike(domain) || IsForumLike(candidate.Url) || IsForumLike(candidate.ExcerptOrSummary);
        var isNavigationOrBrokerLike = IsNavigationOrBrokerLike(candidate, catalogEntry);
        var hasSafetyFlags = candidate.SafetyFlags.Any(flag => flag.Equals("no_trading_execution", StringComparison.OrdinalIgnoreCase))
            && candidate.SafetyFlags.Any(flag => flag.Equals("human_review_required", StringComparison.OrdinalIgnoreCase));
        var autoApproved = candidate.SourceStatus.Equals("independent_candidate_pending_review", StringComparison.OrdinalIgnoreCase)
            && semantic >= 0.85
            && independence >= 0.80
            && contradiction <= 0.15
            && domainAllowed
            && !isForumOrCommunity
            && !isNavigationOrBrokerLike
            && !duplicateUrl
            && hasSafetyFlags;

        var humanReview = !autoApproved && !duplicateUrl && !isNavigationOrBrokerLike && hasSafetyFlags
            && (isForumOrCommunity
                || semantic < 0.85
                || contradiction > 0.15
                || !domainAllowed
                || candidate.SourceStatus.Equals("independent_candidate_pending_review", StringComparison.OrdinalIgnoreCase));

        var rejected = duplicateUrl
            || semantic < 0.35
            || contradiction > 0.5
            || isNavigationOrBrokerLike
            || (!domainAllowed && !humanReview);

        var policyDecision = autoApproved
            ? "auto_approved"
            : rejected
                ? "rejected"
                : "human_review";

        var policyReason = autoApproved
            ? "policy_rules_met"
            : duplicateUrl
                ? "duplicate_url"
                : isNavigationOrBrokerLike
                    ? "navigation_download_broker_or_bots_source"
                    : !domainAllowed
                        ? "domain_not_allowed_or_missing_from_trusted_catalog"
                        : semantic < 0.35
                            ? "semantic_score_too_low"
                            : contradiction > 0.5
                                ? "contradiction_risk_too_high"
                                : isForumOrCommunity
                                    ? "forum_or_community_source_requires_human_review"
                                    : semantic < 0.85 || contradiction > 0.15
                                        ? "borderline_source_requires_human_review"
                                        : "policy_rules_not_fully_met";

        var sourceStatus = autoApproved
            ? "policy_approved_second_source"
            : humanReview
                ? "policy_human_review_required"
                : "policy_rejected";

        var reviewStatus = autoApproved
            ? "policy_approved_second_source"
            : humanReview
                ? "policy_review_required"
                : "policy_rejected";

        var sourceCountAfterPolicy = autoApproved ? Math.Max(sourceCountBeforePolicy, 2) : sourceCountBeforePolicy;

        return new AutoSourceReviewCandidate(
            KnowledgeItemId: knowledgeItemId,
            Title: candidate.ExcerptOrSummary.Length > 0 ? candidate.ExcerptOrSummary : candidate.Url,
            Url: candidate.Url,
            Domain: candidate.Domain,
            SourceType: sourceType,
            ExcerptOrSummary: candidate.ExcerptOrSummary,
            HumanReviewStatus: candidate.HumanReviewStatus,
            SafetyFlags: candidate.SafetyFlags.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SemanticMatchScore: semantic,
            IndependenceScore: independence,
            ContradictionRisk: contradiction,
            DomainAllowed: domainAllowed,
            InTrustedCatalog: inCatalog,
            IsForumOrCommunity: isForumOrCommunity,
            IsNavigationOrBrokerLike: isNavigationOrBrokerLike,
            DuplicateUrl: duplicateUrl,
            AutoApprovedByPolicy: autoApproved,
            HumanReviewRequired: humanReview,
            Rejected: rejected,
            SourceStatus: sourceStatus,
            ReviewStatus: reviewStatus,
            PolicyDecision: policyDecision,
            PolicyReason: policyReason,
            SourceCountBeforePolicy: sourceCountBeforePolicy,
            SourceCountAfterPolicy: sourceCountAfterPolicy,
            MatchedTerms: matcherCandidate?.MatchedTerms ?? []);
    }

    private static bool IsNavigationOrBrokerLike(SourceCandidate candidate, TrustedSourceCatalogEntry? catalogEntry)
    {
        var sourceType = Normalize(candidate.SourceType);
        var url = Normalize(candidate.Url);
        var title = Normalize(candidate.ExcerptOrSummary);
        var blockedTerms = new[] { "navigation", "download", "broker", "bots", "bot", "pricing", "signup", "login", "account" };

        if (sourceType.Equals("known_article_seed_candidate", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (blockedTerms.Any(term => sourceType.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (catalogEntry is not null)
        {
            foreach (var blockedPath in catalogEntry.BlockedPaths)
            {
                if (string.IsNullOrWhiteSpace(blockedPath))
                {
                    continue;
                }

                var blocked = NormalizePath(blockedPath);
                if (url.Contains(blocked, StringComparison.OrdinalIgnoreCase) || title.Contains(blocked, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return blockedTerms.Any(term => url.Contains(term, StringComparison.OrdinalIgnoreCase) || title.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsForumLike(string value)
    {
        value = Normalize(value);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("forums.", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/forum", StringComparison.OrdinalIgnoreCase)
            || value.Contains("community", StringComparison.OrdinalIgnoreCase)
            || value.Contains("reddit.com", StringComparison.OrdinalIgnoreCase)
            || value.Contains("stackexchange", StringComparison.OrdinalIgnoreCase)
            || value.Contains("comments", StringComparison.OrdinalIgnoreCase)
            || value.Contains("discuss", StringComparison.OrdinalIgnoreCase);
    }

    private SourceConfirmationReport LoadSourceConfirmations()
    {
        var engine = new SourceConfirmationEngine(_storagePaths);
        if (!File.Exists(engine.ReportPath))
        {
            return new SourceConfirmationReport(
                ReportVersion: "source_confirmation_v2",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                ItemsAnalyzed: 0,
                ConfirmationDistribution: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                Results: [],
                Warnings: ["source_confirmations_missing"],
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true);
        }

        return engine.LoadReport()
            ?? new SourceConfirmationReport(
                ReportVersion: "source_confirmation_v2",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                ItemsAnalyzed: 0,
                ConfirmationDistribution: new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
                Results: [],
                Warnings: ["source_confirmations_missing"],
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true);
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
            return JsonSerializer.Deserialize<KnowledgeEvidenceSemanticMatcherReport>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
        }
        catch
        {
            return null;
        }
    }

    private static SourceConfirmationReport ApplyUpdates(
        SourceConfirmationReport confirmations,
        IReadOnlyList<(string KnowledgeId, SourceCandidate Candidate)> updates,
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

                var approvedCount = mergedSources.Count(candidate => candidate.AutoApprovedByPolicy);
                var candidateCount = mergedSources.Count;
                var independentCount = mergedSources.Count(candidate => candidate.AutoApprovedByPolicy || candidate.SourceStatus.Equals("policy_human_review_required", StringComparison.OrdinalIgnoreCase) || candidate.SourceStatus.Equals("independent_candidate_pending_review", StringComparison.OrdinalIgnoreCase));
                var existingApprovedCount = SourceConfirmationEngine.ApprovedSourceCount(result);
                var baseSourceCount = Math.Max(0, result.SourceCount - existingApprovedCount);
                var sourceCount = Math.Max(result.SourceCount, baseSourceCount + approvedCount);

                return result with
                {
                    SourceCount = sourceCount,
                    CandidateSourceCount = candidateCount,
                    IndependentSourceCandidateCount = independentCount,
                    PolicyApprovedSourceCount = approvedCount,
                    ReviewStatus = approvedCount > 0
                        ? "policy_approved_second_source"
                        : mergedSources.Any(candidate => candidate.SourceStatus.Equals("policy_human_review_required", StringComparison.OrdinalIgnoreCase))
                            ? "policy_review_required"
                            : result.ReviewStatus,
                    CandidateSources = mergedSources,
                    Warnings = result.Warnings
                        .Concat(approvedCount > 0 ? ["policy_approved_second_source"] : [])
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
                .Concat(["auto_source_review_applied"])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static string BuildMarkdown(AutoSourceReviewReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Auto Source Review Policy");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Updated At: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Loaded Candidate Sources: {report.LoadedCandidateSources}");
        sb.AppendLine($"- Evaluated Candidate Sources: {report.EvaluatedCandidateSources}");
        sb.AppendLine($"- Auto Approved Candidates: {report.AutoApprovedCandidates}");
        sb.AppendLine($"- Human Review Candidates: {report.HumanReviewCandidates}");
        sb.AppendLine($"- Rejected Candidates: {report.RejectedCandidates}");
        sb.AppendLine($"- Duplicate Candidates: {report.DuplicateCandidates}");
        sb.AppendLine($"- Policy Approved Knowledge Items: {report.PolicyApprovedKnowledgeItems}");
        sb.AppendLine($"- Source Count Increased Knowledge Items: {report.SourceCountIncreasedKnowledgeItems}");
        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine($"- no_trading_execution: {report.NoTradingExecution}");
        sb.AppendLine($"- no_broker_action: {report.NoBrokerAction}");
        sb.AppendLine($"- no_auto_trading: {report.NoAutoTrading}");
        sb.AppendLine($"- human_review_required: {report.HumanReviewRequired}");
        sb.AppendLine($"- research_only: {report.ResearchOnly}");
        if (report.AutoApproved.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Auto Approved");
            foreach (var candidate in report.AutoApproved.Take(20))
            {
                sb.AppendLine($"- {candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Url} | sem={candidate.SemanticMatchScore:0.###} | indep={candidate.IndependenceScore:0.###} | contradiction={candidate.ContradictionRisk:0.###}");
            }
        }

        if (report.HumanReview.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Human Review");
            foreach (var candidate in report.HumanReview.Take(20))
            {
                sb.AppendLine($"- {candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Url} | reason={candidate.PolicyReason}");
            }
        }

        if (report.Rejected.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Rejected");
            foreach (var candidate in report.Rejected.Take(20))
            {
                sb.AppendLine($"- {candidate.KnowledgeItemId} | {candidate.Domain} | {candidate.Url} | reason={candidate.PolicyReason}");
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

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim().ToLowerInvariant(), "\\s+", " ");

    private static string NormalizeDomain(string? value)
    {
        var normalized = Normalize(value);
        if (normalized.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[4..];
        }

        return normalized;
    }

    private static string NormalizePath(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().TrimEnd('/');

    private static string MakeKey(string knowledgeItemId, string url) => $"{Normalize(knowledgeItemId)}||{NormalizeUrl(url)}";

    private static string NormalizeUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            return Normalize(value);
        }

        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty,
            Query = string.Empty
        };

        return builder.Uri.ToString().TrimEnd('/').ToLowerInvariant();
    }
}
