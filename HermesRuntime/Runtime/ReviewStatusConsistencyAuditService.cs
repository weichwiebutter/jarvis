using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ReviewStatusConsistencySnapshot(
    string Source,
    string Path,
    DateTimeOffset LastUpdatedUtc,
    int PendingReviews,
    int NeedsMoreEvidenceReviews,
    IReadOnlyList<string> TopReviewPriorities);

public sealed record ReviewStatusConsistencyEntry(
    string ReviewId,
    string KnowledgeItemId,
    string Title,
    string Domain,
    string QueueStatus,
    string QueueRecommendation,
    DateTimeOffset? QueueUpdatedAtUtc,
    string? AssistantRecommendation,
    double? AssistantTrust,
    double? AssistantEvidenceQuality,
    double? AssistantValidationScore,
    string? PrioritizationPriority,
    string? PrioritizationSummary,
    string Source,
    DateTimeOffset? LastUpdatedUtc);

public sealed record ReviewStatusConsistencyAuditReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalReviews,
    int PendingReviewsQueue,
    int PendingReviewsMaster,
    int NeedsMoreEvidenceQueue,
    int NeedsMoreEvidenceMaster,
    int AbnormalReviewCount,
    int SameCount,
    int DifferentCount,
    string SourceOfTruth,
    string LeadingQueueSource,
    string LeadingMasterSource,
    string MasterSnapshotSource,
    double MasterSnapshotAgeHours,
    bool MasterSnapshotIsFallback,
    bool LegacySnapshotsIgnored,
    IReadOnlyList<string> LegacySnapshotCandidates,
    IReadOnlyList<ReviewStatusConsistencySnapshot> MasterSnapshots,
    IReadOnlyList<ReviewStatusConsistencyEntry> Reviews,
    IReadOnlyList<string> Deviations,
    string Cause,
    string RecommendedCorrection,
    string OperatorSummary,
    string QueuePath,
    string ReviewQueuePath,
    string ReviewDecisionAssistantPath,
    string ReviewPrioritizationPath,
    string ReviewEvidenceRefreshPath,
    string MasterStatusPath,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class ReviewStatusConsistencyAuditService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public ReviewStatusConsistencyAuditService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "review_status_consistency_audit");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "review_status_consistency_audit.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "review_status_consistency_audit.md");

    public ReviewStatusConsistencyAuditReport Run()
    {
        var (reportPath, markdownPath, root) = ResolveOutputPaths();
        _resolvedReportPath = reportPath;
        _resolvedMarkdownPath = markdownPath;

        var workflow = new HumanReviewWorkflow(_storagePaths);
        var queue = workflow.LoadOrCreateQueue();
        var assistant = new ReviewDecisionAssistantService(_storagePaths).Load() ?? new ReviewDecisionAssistantService(_storagePaths).Run();
        var prioritization = new ReviewPrioritizationAuditService(_storagePaths).Load() ?? new ReviewPrioritizationAuditService(_storagePaths).Run();
        var refresh = new ReviewEvidenceRefreshService(_storagePaths).Load();
        var masterSnapshots = LoadMasterSnapshots().ToList();
        var leadingMaster = masterSnapshots.OrderByDescending(snapshot => snapshot.LastUpdatedUtc).FirstOrDefault()
            ?? new ReviewStatusConsistencySnapshot(
                Source: "unavailable",
                Path: "-",
                LastUpdatedUtc: DateTimeOffset.MinValue,
                PendingReviews: 0,
                NeedsMoreEvidenceReviews: 0,
                TopReviewPriorities: []);
        var leadingQueueSource = "HumanReviewQueue";
        var sourceOfTruth = "HumanReviewQueue";

        var masterSnapshot = masterSnapshots.FirstOrDefault(snapshot => snapshot.Source.Equals("master-status", StringComparison.OrdinalIgnoreCase))
            ?? leadingMaster;
        var legacySnapshotCandidates = masterSnapshots
            .Where(snapshot => snapshot.Source.Contains("local", StringComparison.OrdinalIgnoreCase))
            .Select(snapshot => snapshot.Path)
            .ToList();

        var reviewById = queue.Items
            .ToDictionary(item => item.ReviewId, StringComparer.OrdinalIgnoreCase);
        var assistantByReviewId = assistant.Entries
            .GroupBy(item => item.ReviewId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var prioritizationByReviewId = prioritization.TopPriorityReviews
            .Concat(prioritization.DomainGroups.SelectMany(group => group.Reviews))
            .GroupBy(item => item.ReviewId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var refreshByReviewId = refresh?.Reviews
            .GroupBy(item => item.ReviewId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, ReviewEvidenceRefreshEntry>(StringComparer.OrdinalIgnoreCase);

        var reviews = reviewById.Values
            .Select(item =>
            {
                assistantByReviewId.TryGetValue(item.ReviewId, out var assistantEntry);
                prioritizationByReviewId.TryGetValue(item.ReviewId, out var prioritizationEntry);
                refreshByReviewId.TryGetValue(item.ReviewId, out var refreshEntry);

                var source = assistantEntry is not null ? "review-decision-assistant" : "review-queue";
                var lastUpdated = item.UpdatedAtUtc
                    ?? refresh?.UpdatedAtUtc
                    ?? assistant?.UpdatedAtUtc
                    ?? (DateTimeOffset?)prioritization.UpdatedAtUtc
                    ?? item.CreatedAtUtc;
                return new ReviewStatusConsistencyEntry(
                    ReviewId: item.ReviewId,
                    KnowledgeItemId: item.KnowledgeItemId,
                    Title: item.Title,
                    Domain: item.Domain,
                    QueueStatus: item.Status,
                    QueueRecommendation: item.Recommendation,
                    QueueUpdatedAtUtc: item.UpdatedAtUtc,
                    AssistantRecommendation: assistantEntry?.RecommendationLabel,
                    AssistantTrust: assistantEntry?.TrustBefore,
                    AssistantEvidenceQuality: assistantEntry?.EvidenceQuality,
                    AssistantValidationScore: assistantEntry?.ValidationScore,
                    PrioritizationPriority: prioritizationEntry?.Priority,
                    PrioritizationSummary: prioritizationEntry?.PriorityReason,
                    Source: source,
                    LastUpdatedUtc: lastUpdated);
            })
            .OrderByDescending(item => item.QueueStatus.Equals("pending", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => item.QueueUpdatedAtUtc ?? item.LastUpdatedUtc)
            .ThenBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var queuePending = queue.PendingReviews;
        var queueNeedsMoreEvidence = queue.NeedsMoreEvidenceReviews;
        var masterPending = leadingMaster.PendingReviews;
        var masterNeedsMoreEvidence = leadingMaster.NeedsMoreEvidenceReviews;
        var deviations = BuildDeviations(queue, leadingMaster, reviews);
        var abnormalReviewCount = reviews.Count(item =>
            !string.Equals(item.QueueStatus, item.QueueRecommendation, StringComparison.OrdinalIgnoreCase)
            && item.QueueStatus.Equals("pending", StringComparison.OrdinalIgnoreCase));
        var report = new ReviewStatusConsistencyAuditReport(
            ReportVersion: "review_status_consistency_audit_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TotalReviews: queue.Items.Count,
            PendingReviewsQueue: queuePending,
            PendingReviewsMaster: masterPending,
            NeedsMoreEvidenceQueue: queueNeedsMoreEvidence,
            NeedsMoreEvidenceMaster: masterNeedsMoreEvidence,
            AbnormalReviewCount: abnormalReviewCount,
            SameCount: queuePending == masterPending && queueNeedsMoreEvidence == masterNeedsMoreEvidence ? queue.Items.Count : 0,
            DifferentCount: queuePending == masterPending && queueNeedsMoreEvidence == masterNeedsMoreEvidence ? 0 : queue.Items.Count,
            SourceOfTruth: sourceOfTruth,
            LeadingQueueSource: leadingQueueSource,
            LeadingMasterSource: leadingMaster.Source,
            MasterSnapshotSource: masterSnapshot.Source,
            MasterSnapshotAgeHours: masterSnapshot.LastUpdatedUtc == DateTimeOffset.MinValue
                ? double.NaN
                : Math.Round((DateTimeOffset.UtcNow - masterSnapshot.LastUpdatedUtc).TotalHours, 2),
            MasterSnapshotIsFallback: masterSnapshot.Source.Contains("local", StringComparison.OrdinalIgnoreCase),
            LegacySnapshotsIgnored: legacySnapshotCandidates.Count > 0 && !masterSnapshot.Source.Contains("local", StringComparison.OrdinalIgnoreCase),
            LegacySnapshotCandidates: legacySnapshotCandidates,
            MasterSnapshots: masterSnapshots,
            Reviews: reviews,
            Deviations: deviations,
            Cause: BuildCause(queue, leadingMaster, masterSnapshots, deviations),
            RecommendedCorrection: "HumanReviewQueue als führende Quelle verwenden; Master-Status nur als Snapshot/Anzeige verstehen; Dashboard direkt aus der Queue lesen; Legacy .codex_artifacts-Snapshots ignorieren.",
            OperatorSummary: BuildOperatorSummary(queue, leadingMaster, masterSnapshots, deviations),
            QueuePath: workflow.QueuePath,
            ReviewQueuePath: workflow.QueuePath,
            ReviewDecisionAssistantPath: assistant.ReportPath,
            ReviewPrioritizationPath: prioritization.ReportPath,
            ReviewEvidenceRefreshPath: refresh?.ReportPath ?? Path.Combine(_storagePaths.Root, "reports", "review_evidence_refresh", "review_evidence_refresh.json"),
            MasterStatusPath: leadingMaster.Path,
            Warnings: BuildWarnings(queue, leadingMaster, masterSnapshots),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteReport(reportPath, markdownPath, report);
        return report;
    }

    public ReviewStatusConsistencyAuditReport? Load()
    {
        var readablePath = ResolveReadableReportPath();
        _resolvedReportPath = readablePath;
        if (!File.Exists(readablePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ReviewStatusConsistencyAuditReport>(File.ReadAllText(readablePath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private IReadOnlyList<ReviewStatusConsistencySnapshot> LoadMasterSnapshots()
    {
        var candidates = new List<(string Source, string Path)>
        {
            ("master-status", Path.Combine(_storagePaths.Root, "reports", "master-status", "master_status.json")),
            ("master-status-local", Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "master-status", "master_status.json")),
        };

        var snapshots = new List<ReviewStatusConsistencySnapshot>();
        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate.Path))
            {
                continue;
            }

            try
            {
                var raw = JsonSerializer.Deserialize<MasterStatusSnapshot>(File.ReadAllText(candidate.Path), JsonDefaults.SnapshotReadOptions);
                if (raw is null)
                {
                    continue;
                }

                snapshots.Add(new ReviewStatusConsistencySnapshot(
                    Source: candidate.Source,
                    Path: candidate.Path,
                    LastUpdatedUtc: raw.LastUpdatedUtc,
                    PendingReviews: raw.PendingReviews,
                    NeedsMoreEvidenceReviews: raw.NeedsMoreEvidenceReviews,
                    TopReviewPriorities: raw.TopReviewPriorities));
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                snapshots.Add(new ReviewStatusConsistencySnapshot(
                    Source: candidate.Source,
                    Path: candidate.Path,
                    LastUpdatedUtc: DateTimeOffset.MinValue,
                    PendingReviews: -1,
                    NeedsMoreEvidenceReviews: -1,
                    TopReviewPriorities: []));
            }
        }

        return snapshots
            .OrderByDescending(snapshot => snapshot.LastUpdatedUtc)
            .ToList();
    }

    private static IReadOnlyList<string> BuildDeviations(HumanReviewQueue queue, ReviewStatusConsistencySnapshot master, IReadOnlyList<ReviewStatusConsistencyEntry> reviews)
    {
        var deviations = new List<string>();
        if (queue.PendingReviews != master.PendingReviews)
        {
            deviations.Add($"pending_reviews_mismatch: queue={queue.PendingReviews}, master={master.PendingReviews}");
        }

        if (queue.NeedsMoreEvidenceReviews != master.NeedsMoreEvidenceReviews)
        {
            deviations.Add($"needs_more_evidence_mismatch: queue={queue.NeedsMoreEvidenceReviews}, master={master.NeedsMoreEvidenceReviews}");
        }

        if (reviews.Any(item => item.QueueStatus.Equals("pending", StringComparison.OrdinalIgnoreCase) && item.QueueRecommendation.Equals("needs_more_evidence", StringComparison.OrdinalIgnoreCase)))
        {
            deviations.Add("pending_reviews_with_more_evidence_recommendations");
        }

        if (master.TopReviewPriorities.Count == 0)
        {
            deviations.Add("master_snapshot_without_top_review_priorities");
        }

        return deviations;
    }

    private static string BuildCause(HumanReviewQueue queue, ReviewStatusConsistencySnapshot master, IReadOnlyList<ReviewStatusConsistencySnapshot> snapshots, IReadOnlyList<string> deviations)
    {
        if (deviations.Count == 0 && queue.PendingReviews == master.PendingReviews && queue.NeedsMoreEvidenceReviews == master.NeedsMoreEvidenceReviews)
        {
            return "Die Statusquellen sind aktuell konsistent. HumanReviewQueue und Master-Status zeigen denselben Zählstand.";
        }

        if (snapshots.Count > 1 && snapshots.Select(snapshot => (snapshot.PendingReviews, snapshot.NeedsMoreEvidenceReviews)).Distinct().Count() > 1)
        {
            return "Master Status verwendet unterschiedliche Snapshot-Varianten; ein älterer Snapshot kann von der Live-Queue abweichen.";
        }

        return "Die Statusquellen verwenden unterschiedliche Bewertungsstände.";
    }

    private static string BuildOperatorSummary(HumanReviewQueue queue, ReviewStatusConsistencySnapshot master, IReadOnlyList<ReviewStatusConsistencySnapshot> snapshots, IReadOnlyList<string> deviations)
    {
        if (deviations.Count == 0 && queue.PendingReviews == master.PendingReviews && queue.NeedsMoreEvidenceReviews == master.NeedsMoreEvidenceReviews)
        {
            return "Die Statusquellen sind aktuell konsistent. Frank sieht dieselbe Review-Wahrheit im Prüfzentrum und im Master Status.";
        }

        return snapshots.Any(snapshot => snapshot.Source.Contains("local", StringComparison.OrdinalIgnoreCase))
            ? "Master Status nutzt noch einen älteren Snapshot. Die Live-Queue ist die führende Quelle."
            : "Die Statusquellen verwenden unterschiedliche Bewertungsstände.";
    }

    private static IReadOnlyList<string> BuildWarnings(HumanReviewQueue queue, ReviewStatusConsistencySnapshot master, IReadOnlyList<ReviewStatusConsistencySnapshot> snapshots)
    {
        var warnings = new List<string>();
        if (queue.PendingReviews != master.PendingReviews)
        {
            warnings.Add("queue_master_pending_mismatch");
        }

        if (queue.NeedsMoreEvidenceReviews != master.NeedsMoreEvidenceReviews)
        {
            warnings.Add("queue_master_needs_more_evidence_mismatch");
        }

        if (snapshots.Any(snapshot => snapshot.Source.Contains("local", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("legacy_master_snapshot_candidate_present");
        }

        return warnings;
    }

    private (string ReportPath, string MarkdownPath, string Root) ResolveOutputPaths()
    {
        try
        {
            Directory.CreateDirectory(Root);
            return (Path.Combine(Root, "review_status_consistency_audit.json"), Path.Combine(Root, "review_status_consistency_audit.md"), Root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), ".codex_artifacts", "reports", "review_status_consistency_audit");
            Directory.CreateDirectory(fallbackRoot);
            return (Path.Combine(fallbackRoot, "review_status_consistency_audit.json"), Path.Combine(fallbackRoot, "review_status_consistency_audit.md"), fallbackRoot);
        }
    }

    private string ResolveReadableReportPath()
    {
        var primary = ReportPath;
        if (File.Exists(primary))
        {
            return primary;
        }

        var fallback = Path.Combine(Directory.GetCurrentDirectory(), ".codex_artifacts", "reports", "review_status_consistency_audit", "review_status_consistency_audit.json");
        return File.Exists(fallback) ? fallback : primary;
    }

    private static void WriteReport(string reportPath, string markdownPath, ReviewStatusConsistencyAuditReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);

        try
        {
            File.WriteAllText(reportPath, json);
            File.WriteAllText(markdownPath, markdown);
            return;
        }
        catch
        {
            // Fall through to safe local fallbacks.
        }

        var fallbackRoots = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), ".codex_artifacts", "reports", "review_status_consistency_audit"),
            Path.Combine(Path.GetTempPath(), "hermes", "reports", "review_status_consistency_audit"),
        };

        foreach (var fallbackRoot in fallbackRoots)
        {
            try
            {
                Directory.CreateDirectory(fallbackRoot);
                var fallbackReportPath = Path.Combine(fallbackRoot, "review_status_consistency_audit.json");
                var fallbackMarkdownPath = Path.Combine(fallbackRoot, "review_status_consistency_audit.md");
                File.WriteAllText(fallbackReportPath, json);
                File.WriteAllText(fallbackMarkdownPath, markdown);
                return;
            }
            catch
            {
                // Try next fallback root.
            }
        }

        throw new IOException("Unable to write review status consistency audit report.");
    }

    private static string BuildMarkdown(ReviewStatusConsistencyAuditReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Review / Master Status Consistency Audit");
        sb.AppendLine();
        sb.AppendLine($"- Report Version: {report.ReportVersion}");
        sb.AppendLine($"- Updated At UTC: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Reviews gesamt: {report.TotalReviews}");
        sb.AppendLine($"- Pending laut Queue: {report.PendingReviewsQueue}");
        sb.AppendLine($"- Pending laut Master: {report.PendingReviewsMaster}");
        sb.AppendLine($"- Needs More Evidence laut Queue: {report.NeedsMoreEvidenceQueue}");
        sb.AppendLine($"- Needs More Evidence laut Master: {report.NeedsMoreEvidenceMaster}");
        sb.AppendLine($"- Source of Truth: {report.SourceOfTruth}");
        sb.AppendLine($"- Snapshot-Quelle: {report.MasterSnapshotSource}");
        sb.AppendLine($"- Snapshot-Alter (h): {(double.IsNaN(report.MasterSnapshotAgeHours) ? "-" : report.MasterSnapshotAgeHours.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))}");
        sb.AppendLine($"- Fallback verwendet: {report.MasterSnapshotIsFallback}");
        sb.AppendLine($"- Legacy-Snapshots ignoriert: {report.LegacySnapshotsIgnored}");
        sb.AppendLine($"- Ursache: {report.Cause}");
        sb.AppendLine($"- Korrektur: {report.RecommendedCorrection}");
        sb.AppendLine();
        sb.AppendLine("## Abweichungen");
        foreach (var deviation in report.Deviations)
        {
            sb.AppendLine($"- {deviation}");
        }
        if (report.Deviations.Count == 0)
        {
            sb.AppendLine("- keine");
        }
        sb.AppendLine();
        sb.AppendLine("## Snapshots");
        foreach (var snapshot in report.MasterSnapshots)
        {
            sb.AppendLine($"- {snapshot.Source}: {snapshot.Path} | pending={snapshot.PendingReviews} | needs_more_evidence={snapshot.NeedsMoreEvidenceReviews} | updated={snapshot.LastUpdatedUtc:O}");
        }
        if (report.LegacySnapshotCandidates.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Legacy Snapshot Candidates");
            foreach (var path in report.LegacySnapshotCandidates)
            {
                sb.AppendLine($"- {path}");
            }
            sb.AppendLine("- empfohlener manueller Cleanup-Befehl: `rm -f HermesRuntime/.codex_artifacts/reports/master-status/master_status.json`");
        }
        return sb.ToString();
    }
}
