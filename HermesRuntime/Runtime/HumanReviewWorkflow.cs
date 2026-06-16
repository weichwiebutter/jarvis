using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public enum HumanReviewPriority
{
    low,
    medium,
    high
}

public sealed record HumanReviewItem(
    string ReviewId,
    string KnowledgeItemId,
    string Domain,
    string Title,
    string Reason,
    string EvidenceSummary,
    double TrustBefore,
    string Recommendation,
    string RequestedByTaskId,
    HumanReviewPriority Priority,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    string Status,
    IReadOnlyList<string> EvidenceRefs,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record HumanReviewQueue(
    string QueueVersion,
    DateTimeOffset UpdatedAtUtc,
    int PendingReviews,
    int ApprovedReviews,
    int RejectedReviews,
    int NeedsMoreEvidenceReviews,
    int DeferredReviews,
    IReadOnlyList<HumanReviewItem> Items,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record HumanReviewDecision(
    string DecisionId,
    string ReviewId,
    string KnowledgeItemId,
    string Domain,
    string Decision,
    string Note,
    string DecidedBy,
    DateTimeOffset DecidedAtUtc,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> FollowupTasks,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record HumanReviewSummary(
    string SummaryVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalReviewItems,
    int PendingReviews,
    int ApprovedReviews,
    int RejectedReviews,
    int NeedsMoreEvidenceReviews,
    int DeferredReviews,
    int HumanReviewedItems,
    double ReviewCoverage,
    IReadOnlyList<string> TopReviewPriorities,
    string QueuePath,
    string DecisionsPath,
    string EvidencePath,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record HumanReviewLearningFeedback(
    string FeedbackId,
    string ReviewId,
    string Decision,
    string Note,
    DateTimeOffset TimestampUtc,
    string KnowledgeItem,
    string Domain,
    double PreviousTrust,
    string Result,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class HumanReviewWorkflow
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedQueuePath;
    private string? _resolvedDecisionsPath;
    private string? _resolvedLearningFeedbackPath;

    public HumanReviewWorkflow(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string QueuePath => _resolvedQueuePath ?? Path.Combine(Root, "human_review_queue.json");

    public string DecisionsPath => _resolvedDecisionsPath ?? Path.Combine(Root, "human_review_decisions.jsonl");

    public string LearningFeedbackPath => _resolvedLearningFeedbackPath ?? Path.Combine(Root, "human_review_learning_feedback.jsonl");

    public HumanReviewQueue GenerateQueue(string requestedByTaskId, int maxItems)
    {
        maxItems = Math.Clamp(maxItems, 1, 200);
        Directory.CreateDirectory(Root);
        var existing = LoadQueueOrEmpty();
        var existingKnowledge = existing.Items
            .Where(item => item.Status is "pending" or "deferred" or "approved" or "rejected" or "needs_more_evidence")
            .Select(item => item.KnowledgeItemId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var latestReviews = new HumanReviewEvidenceStore(_storagePaths).LoadOrCreateReport().Reviews
            .GroupBy(review => review.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(review => review.ReviewedAtUtc).First(), StringComparer.OrdinalIgnoreCase);
        var contradictions = new ContradictionDetector(_storagePaths).LoadOrRun();
        var contradictionIds = contradictions.Contradictions
            .Select(record => record.KnowledgeId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var quality = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        var candidates = quality.Items
            .Where(item => !existingKnowledge.Contains(item.KnowledgeId))
            .Where(item => !latestReviews.TryGetValue(item.KnowledgeId, out var review)
                || review.Result.Equals("needs_review", StringComparison.OrdinalIgnoreCase))
            .Select(item => BuildItem(item, contradictionIds.Contains(item.KnowledgeId), requestedByTaskId))
            .Where(item => item is not null)
            .Cast<HumanReviewItem>()
            .OrderByDescending(item => PriorityRank(item.Priority))
            .ThenByDescending(item => item.TrustBefore)
            .ThenBy(item => item.Domain, StringComparer.Ordinal)
            .ThenBy(item => item.KnowledgeItemId, StringComparer.Ordinal)
            .Take(maxItems)
            .ToList();

        var merged = existing.Items
            .Concat(candidates)
            .GroupBy(item => item.ReviewId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(item => item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(item => PriorityRank(item.Priority))
            .ThenByDescending(item => item.CreatedAtUtc)
            .ToList();
        return WriteQueue(merged, candidates.Count == 0 ? ["no_new_human_review_items_generated"] : []);
    }

    public HumanReviewQueue LoadOrCreateQueue()
    {
        var queuePath = ResolveReadableQueuePath();
        _resolvedQueuePath = queuePath;
        _resolvedDecisionsPath = ResolveDecisionPath();
        _resolvedLearningFeedbackPath = ResolveLearningFeedbackPath();
        if (File.Exists(queuePath))
        {
            return LoadQueueOrEmpty();
        }

        return WriteQueue([], []);
    }


    public void PersistQueue(HumanReviewQueue queue)
    {
        WriteQueue(queue.Items, queue.Warnings);
    }

    public HumanReviewItem? FindItem(string reviewId) =>
        LoadOrCreateQueue().Items.FirstOrDefault(item =>
            item.ReviewId.Equals(reviewId, StringComparison.OrdinalIgnoreCase));

    public HumanReviewDecision Decide(string reviewId, string decision, string note, string decidedBy = "human")
    {
        var normalized = NormalizeDecision(decision);
        var queue = LoadOrCreateQueue();
        var item = queue.Items.FirstOrDefault(candidate =>
            candidate.ReviewId.Equals(reviewId, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            throw new InvalidOperationException($"Human review item not found: {reviewId}");
        }

        var now = DateTimeOffset.UtcNow;
        var updatedItem = item with
        {
            Status = normalized,
            UpdatedAtUtc = now
        };
        var items = queue.Items
            .Select(candidate => candidate.ReviewId.Equals(reviewId, StringComparison.OrdinalIgnoreCase) ? updatedItem : candidate)
            .ToList();
        var followups = new List<string>();
        if (normalized.Equals("needs_more_evidence", StringComparison.OrdinalIgnoreCase))
        {
            new ResearchQueueService(_storagePaths).Enqueue(
                item.Domain,
                "collect_evidence",
                ResearchPriority.High,
                [item.KnowledgeItemId, item.ReviewId, "human_review_needs_more_evidence"]);
            followups.Add("collect_evidence");
        }

        var evidenceRefs = item.EvidenceRefs
            .Concat([$"human_review_item:{item.ReviewId}", $"human_review_decision:{normalized}"])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToList();
        var decisionRecord = new HumanReviewDecision(
            DecisionId: $"human_review_decision_{now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            ReviewId: item.ReviewId,
            KnowledgeItemId: item.KnowledgeItemId,
            Domain: item.Domain,
            Decision: normalized,
            Note: string.IsNullOrWhiteSpace(note) ? "no_note" : note.Trim(),
            DecidedBy: string.IsNullOrWhiteSpace(decidedBy) ? "human" : decidedBy.Trim(),
            DecidedAtUtc: now,
            EvidenceRefs: evidenceRefs,
            FollowupTasks: followups,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        AppendDecision(decisionRecord);
        AppendLearningFeedback(new HumanReviewLearningFeedback(
            FeedbackId: $"human_review_feedback_{now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            ReviewId: item.ReviewId,
            Decision: normalized,
            Note: string.IsNullOrWhiteSpace(note) ? "no_note" : note.Trim(),
            TimestampUtc: now,
            KnowledgeItem: item.KnowledgeItemId,
            Domain: item.Domain,
            PreviousTrust: item.TrustBefore,
            Result: normalized switch
            {
                "approved" => "trusted",
                "rejected" => "rejected",
                "needs_more_evidence" => "more_evidence_requested",
                _ => "deferred"
            },
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true));
        WriteQueue(items, []);

        if (normalized is "approved" or "rejected" or "needs_more_evidence")
        {
            var evidenceResult = normalized switch
            {
                "approved" => "approved",
                "rejected" => "rejected",
                _ => "needs_review"
            };
            new HumanReviewEvidenceStore(_storagePaths).AddReview(
                item.KnowledgeItemId,
                evidenceResult,
                decisionRecord.DecidedBy,
                $"{normalized}: {decisionRecord.Note}");
        }

        new ContradictionDetector(_storagePaths).Run();
        new SourceConfirmationEngine(_storagePaths).Build();
        new KnowledgeQualityEngine(_storagePaths).Run();
        var needs = new NeedDetectionEngine(_storagePaths).Detect();
        new GoalProgressTracker(_storagePaths).Update();
        new CognitiveCoreService(_storagePaths).BuildStatus();
        new MasterStatusWriter(new MasterStatusService(_storagePaths, Directory.GetCurrentDirectory())).WriteSnapshot();
        return decisionRecord;
    }

    public HumanReviewSummary BuildSummary()
    {
        var queue = LoadOrCreateQueue();
        var evidence = new HumanReviewEvidenceStore(_storagePaths).LoadOrCreateReport();
        var knowledgeCatalog = new KnowledgeCatalog(_storagePaths).LoadItems();
        var totalKnowledge = knowledgeCatalog.Count;
        var activeItems = queue.Items;
        return new HumanReviewSummary(
            SummaryVersion: "human_review_summary_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TotalReviewItems: activeItems.Count,
            PendingReviews: activeItems.Count(item => item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase)),
            ApprovedReviews: activeItems.Count(item => item.Status.Equals("approved", StringComparison.OrdinalIgnoreCase)),
            RejectedReviews: activeItems.Count(item => item.Status.Equals("rejected", StringComparison.OrdinalIgnoreCase)),
            NeedsMoreEvidenceReviews: activeItems.Count(item => item.Status.Equals("needs_more_evidence", StringComparison.OrdinalIgnoreCase)),
            DeferredReviews: activeItems.Count(item => item.Status.Equals("deferred", StringComparison.OrdinalIgnoreCase)),
            HumanReviewedItems: evidence.ReviewedKnowledgeItems,
            ReviewCoverage: Math.Round(totalKnowledge == 0 ? 0 : evidence.ReviewedKnowledgeItems / (double)totalKnowledge, 4),
            TopReviewPriorities: activeItems
                .Where(item => item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => PriorityRank(item.Priority))
                .ThenByDescending(item => item.TrustBefore)
                .Take(10)
                .Select(item => $"{item.Priority}:{item.Domain}:{item.KnowledgeItemId}:trust={item.TrustBefore:0.####}:{item.Recommendation}")
                .ToList(),
            QueuePath: QueuePath,
            DecisionsPath: DecisionsPath,
            EvidencePath: new HumanReviewEvidenceStore(_storagePaths).ReviewPath,
            Warnings: activeItems.Count == 0 ? ["human_review_queue_empty"] : [],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    public IReadOnlyList<HumanReviewDecision> LoadDecisions(int limit)
    {
        if (!File.Exists(DecisionsPath))
        {
            return [];
        }

        var decisions = new List<HumanReviewDecision>();
        foreach (var line in File.ReadLines(DecisionsPath).Reverse())
        {
            if (decisions.Count >= Math.Clamp(limit, 1, 5000))
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var decision = JsonSerializer.Deserialize<HumanReviewDecision>(
                    line,
                    JsonDefaults.SnapshotReadOptions);
                if (decision is not null)
                {
                    decisions.Add(decision);
                }
            }
            catch (JsonException)
            {
                // Keep append-only decisions resilient to malformed historical rows.
            }
        }

        return decisions;
    }

    private HumanReviewQueue LoadQueueOrEmpty()
    {
        if (!File.Exists(QueuePath))
        {
            return EmptyQueue();
        }

        try
        {
            return JsonSerializer.Deserialize<HumanReviewQueue>(
                File.ReadAllText(QueuePath),
                JsonDefaults.SnapshotReadOptions) ?? EmptyQueue();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return EmptyQueue(["human_review_queue_unreadable"]);
        }
    }

    private HumanReviewQueue WriteQueue(IReadOnlyList<HumanReviewItem> items, IReadOnlyList<string> warnings)
    {
        var queue = new HumanReviewQueue(
            QueueVersion: "human_review_queue_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            PendingReviews: items.Count(item => item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase)),
            ApprovedReviews: items.Count(item => item.Status.Equals("approved", StringComparison.OrdinalIgnoreCase)),
            RejectedReviews: items.Count(item => item.Status.Equals("rejected", StringComparison.OrdinalIgnoreCase)),
            NeedsMoreEvidenceReviews: items.Count(item => item.Status.Equals("needs_more_evidence", StringComparison.OrdinalIgnoreCase)),
            DeferredReviews: items.Count(item => item.Status.Equals("deferred", StringComparison.OrdinalIgnoreCase)),
            Items: items,
            Warnings: warnings,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        var path = EnsureWritableQueuePath();
        File.WriteAllText(path, JsonSerializer.Serialize(queue, JsonDefaults.WriteOptions));
        return queue;
    }

    private void AppendDecision(HumanReviewDecision decision)
    {
        var path = EnsureWritableDecisionPath();
        File.AppendAllText(path, JsonSerializer.Serialize(decision, JsonDefaults.WriteOptions) + Environment.NewLine);
    }

    private void AppendLearningFeedback(HumanReviewLearningFeedback feedback)
    {
        var path = EnsureWritableLearningFeedbackPath();
        File.AppendAllText(path, JsonSerializer.Serialize(feedback, JsonDefaults.WriteOptions) + Environment.NewLine);
    }

    private string ResolveQueuePath() => ResolveWritablePath("human_review_queue.json");

    private string ResolveReadableQueuePath()
    {
        var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "cognitive_core");
        var fallbackPath = Path.Combine(fallbackRoot, "human_review_queue.json");
        if (File.Exists(fallbackPath))
        {
            return fallbackPath;
        }

        var primaryPath = Path.Combine(Root, "human_review_queue.json");
        return primaryPath;
    }

    private string ResolveDecisionPath() => ResolveWritablePath("human_review_decisions.jsonl");

    private string ResolveLearningFeedbackPath() => ResolveWritablePath("human_review_learning_feedback.jsonl");

    private string ResolveWritablePath(string fileName)
    {
        var primaryRoot = Root;
        try
        {
            Directory.CreateDirectory(primaryRoot);
            return Path.Combine(primaryRoot, fileName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "cognitive_core");
            Directory.CreateDirectory(fallbackRoot);
            return Path.Combine(fallbackRoot, fileName);
        }
    }

    private string EnsureWritableQueuePath() => _resolvedQueuePath ?? ResolveQueuePath();

    private string EnsureWritableDecisionPath() => _resolvedDecisionsPath ?? ResolveDecisionPath();

    private string EnsureWritableLearningFeedbackPath() => _resolvedLearningFeedbackPath ?? ResolveLearningFeedbackPath();

    private HumanReviewQueue EmptyQueue(IReadOnlyList<string>? warnings = null) =>
        new(
            QueueVersion: "human_review_queue_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            PendingReviews: 0,
            ApprovedReviews: 0,
            RejectedReviews: 0,
            NeedsMoreEvidenceReviews: 0,
            DeferredReviews: 0,
            Items: [],
            Warnings: warnings ?? [],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

    private static HumanReviewItem? BuildItem(KnowledgeQualityItem item, bool hasContradiction, string requestedByTaskId)
    {
        var hasHumanReview = item.EvidenceRefs.Any(reference => reference.StartsWith("human_review:", StringComparison.OrdinalIgnoreCase));
        if (hasHumanReview && !hasContradiction)
        {
            return null;
        }

        var priority = hasContradiction || item.ReuseScore >= 0.55 || item.QualityScore >= 0.54
            ? HumanReviewPriority.high
            : item.LifecycleStatus.Equals("promising", StringComparison.OrdinalIgnoreCase) || item.TrustScore >= 0.45
                ? HumanReviewPriority.medium
                : HumanReviewPriority.low;
        var recommendation = hasContradiction
            ? "resolve_contradiction_before_trust_promotion"
            : item.TrustScore >= 0.55
                ? "human_review_can_unlock_validated_trust"
                : item.EvidenceScore < 0.45
                    ? "request_more_evidence_before_approval"
                    : "review_for_quality_gate";
        var reviewId = StableReviewId(item.KnowledgeId);
        return new HumanReviewItem(
            ReviewId: reviewId,
            KnowledgeItemId: item.KnowledgeId,
            Domain: item.Domain,
            Title: item.Title,
            Reason: hasContradiction
                ? "Contradiction risk requires human review."
                : "Trust v2 requires human review before higher trust promotion.",
            EvidenceSummary: $"trust={item.TrustScore:0.####}; quality={item.QualityScore:0.####}; evidence={item.EvidenceScore:0.####}; validation={item.ValidationScore:0.####}; lifecycle={item.LifecycleStatus}",
            TrustBefore: item.TrustScore,
            Recommendation: recommendation,
            RequestedByTaskId: string.IsNullOrWhiteSpace(requestedByTaskId) ? "request_human_review" : requestedByTaskId,
            Priority: priority,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            UpdatedAtUtc: null,
            Status: "pending",
            EvidenceRefs: item.EvidenceRefs.Take(24).ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    private static string StableReviewId(string knowledgeItemId)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(knowledgeItemId))).ToLowerInvariant()[..12];
        return $"review_{hash}";
    }

    private static int PriorityRank(HumanReviewPriority priority) =>
        priority switch
        {
            HumanReviewPriority.high => 3,
            HumanReviewPriority.medium => 2,
            _ => 1
        };

    private static string NormalizeDecision(string decision) =>
        decision.Trim().ToLowerInvariant() switch
        {
            "approved" or "approve" => "approved",
            "rejected" or "reject" => "rejected",
            "needs_more_evidence" or "more_evidence" => "needs_more_evidence",
            "deferred" or "defer" => "deferred",
            _ => "deferred"
        };
}
