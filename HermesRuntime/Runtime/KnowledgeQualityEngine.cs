using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeTrustScore(
    double Value,
    string Classification,
    IReadOnlyList<string> Reasons);

public sealed record KnowledgeEvidenceScore(
    double Value,
    string Classification,
    IReadOnlyList<string> EvidenceRefs);

public sealed record KnowledgeReuseScore(
    double Value,
    string Classification,
    IReadOnlyList<string> ReuseRefs);

public sealed record KnowledgeValidationScore(
    double Value,
    string Status,
    IReadOnlyList<string> ValidationRefs);

public sealed record KnowledgeLifetimeScore(
    double Value,
    string Classification,
    int AgeDays,
    IReadOnlyList<string> Reasons);

public sealed record KnowledgeQualityItem(
    string KnowledgeId,
    string Domain,
    string Title,
    string LifecycleStatus,
    string RetentionState,
    double TrustScore,
    double EvidenceScore,
    double ReuseScore,
    double ValidationScore,
    double AgeScore,
    double QualityScore,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> SupportingGoals,
    IReadOnlyList<string> SupportingOutcomes,
    IReadOnlyList<string> Reasons,
    DateTimeOffset? LastValidatedUtc);

public sealed record KnowledgeQualityReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalKnowledgeItems,
    int TrustedKnowledge,
    int WeakKnowledge,
    int DeprecatedKnowledge,
    double AverageQualityScore,
    double AverageTrustScore,
    string KnowledgeHealth,
    string KnowledgeTrend,
    IReadOnlyList<KnowledgeQualityItem> Items,
    IReadOnlyList<string> Warnings,
    string EvidencePath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record KnowledgeEvidenceEntry(
    string KnowledgeId,
    string Domain,
    IReadOnlyList<string> SourceIds,
    IReadOnlyList<string> SourceEvidenceRefs,
    IReadOnlyList<string> ValidationEvidenceRefs,
    IReadOnlyList<string> OutcomeRefs,
    IReadOnlyList<string> GoalRefs,
    IReadOnlyList<string> QueueRefs,
    IReadOnlyList<string> RelatedItems,
    DateTimeOffset UpdatedAtUtc,
    bool HumanReviewRequired);

public sealed record KnowledgeEvidenceReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<KnowledgeEvidenceEntry> Evidence,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record KnowledgeRetentionPolicy(
    double TrustedQualityThreshold,
    double WeakQualityThreshold,
    double DeprecatedAgeThreshold,
    int DeprecationAgeDays,
    int ArchiveAgeDays)
{
    public static KnowledgeRetentionPolicy Default => new(
        TrustedQualityThreshold: 0.82,
        WeakQualityThreshold: 0.38,
        DeprecatedAgeThreshold: 0.25,
        DeprecationAgeDays: 180,
        ArchiveAgeDays: 365);
}

public sealed record MemoryConsolidationEntry(
    string KnowledgeId,
    string Domain,
    string Action,
    string Reason,
    IReadOnlyList<string> RelatedKnowledgeIds,
    double QualityScore,
    string LifecycleStatus);

public sealed record MemoryConsolidationReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalKnowledgeItems,
    int ActiveKnowledge,
    int ArchivedKnowledge,
    int DeprecatedKnowledge,
    int DuplicateGroups,
    int WeakKnowledge,
    int PrioritizedKnowledge,
    IReadOnlyList<MemoryConsolidationEntry> Entries,
    IReadOnlyList<string> Warnings,
    string KnowledgeQualityPath,
    string KnowledgeEvidencePath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class KnowledgeQualityEngine
{
    private readonly StoragePaths _storagePaths;
    private readonly KnowledgeRetentionPolicy _policy;

    public KnowledgeQualityEngine(StoragePaths storagePaths, KnowledgeRetentionPolicy? policy = null)
    {
        _storagePaths = storagePaths;
        _policy = policy ?? KnowledgeRetentionPolicy.Default;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string QualityPath => Path.Combine(Root, "knowledge_quality.json");

    public string EvidencePath => Path.Combine(Root, "knowledge_evidence.json");

    public KnowledgeQualityReport Run()
    {
        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;
        var sources = new KnowledgeSourceRegistry(_storagePaths).LoadOrCreateSources();
        var sourcesById = sources.ToDictionary(source => source.SourceId, StringComparer.OrdinalIgnoreCase);
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var queue = new ResearchQueueService(_storagePaths).LoadOrCreateQueue();
        var outcomes = new TaskOutcomeEvaluator(_storagePaths).LoadOutcomes(5000);
        var goals = LoadGoalState()?.Goals ?? [];
        var insights = new HypothesisGenerator(_storagePaths).LoadInsights();
        var warnings = new List<string>();
        var items = catalog
            .Select(item => ScoreItem(item, sourcesById, queue, outcomes, goals, insights, now))
            .OrderByDescending(item => item.QualityScore)
            .ThenBy(item => item.Domain, StringComparer.Ordinal)
            .ThenBy(item => item.KnowledgeId, StringComparer.Ordinal)
            .ToList();

        if (items.Count == 0)
        {
            warnings.Add("knowledge_catalog_empty");
        }

        var averageQuality = Average(items, item => item.QualityScore);
        var averageTrust = Average(items, item => item.TrustScore);
        var trusted = items.Count(item => item.LifecycleStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase));
        var weak = items.Count(item => item.QualityScore < _policy.WeakQualityThreshold
            || item.LifecycleStatus is "untested" or "experimental" or "rejected");
        var deprecated = items.Count(item => item.LifecycleStatus.Equals("deprecated", StringComparison.OrdinalIgnoreCase)
            || item.RetentionState.Equals("deprecated", StringComparison.OrdinalIgnoreCase));
        var health = KnowledgeHealth(items.Count, trusted, weak, deprecated, averageQuality, averageTrust);
        var trend = KnowledgeTrend(LoadReport(), averageQuality, averageTrust, weak, deprecated);
        var report = new KnowledgeQualityReport(
            ReportVersion: "knowledge_quality_v1",
            UpdatedAtUtc: now,
            TotalKnowledgeItems: items.Count,
            TrustedKnowledge: trusted,
            WeakKnowledge: weak,
            DeprecatedKnowledge: deprecated,
            AverageQualityScore: averageQuality,
            AverageTrustScore: averageTrust,
            KnowledgeHealth: health,
            KnowledgeTrend: trend,
            Items: items,
            Warnings: warnings,
            EvidencePath: EvidencePath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        var evidence = new KnowledgeEvidenceReport(
            ReportVersion: "knowledge_evidence_v1",
            UpdatedAtUtc: now,
            Evidence: items
                .Select(item => new KnowledgeEvidenceEntry(
                    KnowledgeId: item.KnowledgeId,
                    Domain: item.Domain,
                    SourceIds: catalog.FirstOrDefault(catalogItem => catalogItem.Id.Equals(item.KnowledgeId, StringComparison.OrdinalIgnoreCase))?.SourceIds ?? [],
                    SourceEvidenceRefs: item.EvidenceRefs.Where(reference => reference.StartsWith("source:", StringComparison.OrdinalIgnoreCase)).ToList(),
                    ValidationEvidenceRefs: item.EvidenceRefs.Where(reference => reference.StartsWith("validation:", StringComparison.OrdinalIgnoreCase)).ToList(),
                    OutcomeRefs: item.SupportingOutcomes,
                    GoalRefs: item.SupportingGoals,
                    QueueRefs: item.EvidenceRefs.Where(reference => reference.StartsWith("queue:", StringComparison.OrdinalIgnoreCase)).ToList(),
                    RelatedItems: catalog.FirstOrDefault(catalogItem => catalogItem.Id.Equals(item.KnowledgeId, StringComparison.OrdinalIgnoreCase))?.RelatedItems ?? [],
                    UpdatedAtUtc: now,
                    HumanReviewRequired: true))
                .ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(EvidencePath, JsonSerializer.Serialize(evidence, JsonDefaults.WriteOptions));
        File.WriteAllText(QualityPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    public KnowledgeQualityReport? LoadReport()
    {
        if (!File.Exists(QualityPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeQualityReport>(
                File.ReadAllText(QualityPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public KnowledgeQualityReport LoadOrCreateReport() => LoadReport() ?? Run();

    private KnowledgeQualityItem ScoreItem(
        KnowledgeCatalogItem item,
        IReadOnlyDictionary<string, CognitiveSource> sourcesById,
        ResearchQueue queue,
        IReadOnlyList<TaskOutcomeResult> outcomes,
        IReadOnlyList<HermesGoal> goals,
        IReadOnlyList<CognitiveInsight> insights,
        DateTimeOffset now)
    {
        var sourceScores = item.SourceIds
            .Select(sourceId => sourcesById.TryGetValue(sourceId, out var source) ? source.TrustProfile.TrustScore : 0.45)
            .DefaultIfEmpty(0.35)
            .ToList();
        var sourceTrust = sourceScores.Average();
        var sourceRefs = item.SourceIds.Select(sourceId => $"source:{sourceId}").ToList();
        var queueRefs = queue.Items
            .Where(queueItem => queueItem.SourceRefs.Contains(item.Id, StringComparer.OrdinalIgnoreCase)
                || queueItem.Notes.Any(note => note.Contains(item.Id, StringComparison.OrdinalIgnoreCase)))
            .Select(queueItem => $"queue:{queueItem.QueueItemId}:{queueItem.Status}")
            .Take(12)
            .ToList();
        var relatedOutcomes = outcomes
            .Where(outcome =>
                outcome.Evidence.EvidenceRefs.Any(reference => reference.Contains(item.Id, StringComparison.OrdinalIgnoreCase))
                || outcome.TaskType.Contains("knowledge", StringComparison.OrdinalIgnoreCase)
                || outcome.TaskType.Contains("domain", StringComparison.OrdinalIgnoreCase)
                || outcome.GoalId.Contains("knowledge", StringComparison.OrdinalIgnoreCase))
            .Take(12)
            .ToList();
        var supportingOutcomes = relatedOutcomes
            .Select(outcome => $"outcome:{outcome.OutcomeId}:{outcome.Recommendation}")
            .ToList();
        var supportingGoals = goals
            .Where(goal => goal.Domain.Equals(item.Domain, StringComparison.OrdinalIgnoreCase)
                || goal.GoalId.Contains("knowledge", StringComparison.OrdinalIgnoreCase)
                || goal.GoalId.Contains("cognitive", StringComparison.OrdinalIgnoreCase))
            .OrderBy(goal => goal.Priority)
            .Take(8)
            .Select(goal => $"goal:{goal.GoalId}:progress={goal.ProgressScore:0.####}")
            .ToList();
        var insightRefs = insights
            .Where(insight => insight.Domain.Equals(item.Domain, StringComparison.OrdinalIgnoreCase)
                && (insight.EvidenceRefs.Contains(item.Id, StringComparer.OrdinalIgnoreCase)
                    || insight.Summary.Contains(item.Title, StringComparison.OrdinalIgnoreCase)))
            .Select(insight => $"insight:{insight.InsightId}")
            .Take(8)
            .ToList();

        var validation = ValidationScoreFor(item, relatedOutcomes, queueRefs);
        var evidence = EvidenceScoreFor(item, sourceRefs, queueRefs, supportingOutcomes, supportingGoals, insightRefs);
        var reuse = ReuseScoreFor(item, queueRefs, supportingOutcomes, insightRefs);
        var age = LifetimeScoreFor(item, now);
        var trust = Math.Round(Math.Clamp(sourceTrust * 0.45 + evidence.Value * 0.25 + validation.Value * 0.25 + age.Value * 0.05, 0, 1), 4);
        var quality = Math.Round(Math.Clamp(
            trust * 0.25
            + evidence.Value * 0.22
            + reuse.Value * 0.12
            + validation.Value * 0.25
            + age.Value * 0.16,
            0,
            1), 4);
        var lifecycle = LifecycleStatus(item, quality, trust, validation.Value, age.Value);
        var retention = RetentionState(lifecycle, quality, age.Value);
        var reasons = new List<string>
        {
            $"source_trust:{sourceTrust:0.####}",
            $"validation:{validation.Status}:{validation.Value:0.####}",
            $"evidence:{evidence.Classification}:{evidence.Value:0.####}",
            $"reuse:{reuse.Classification}:{reuse.Value:0.####}",
            $"age:{age.Classification}:{age.Value:0.####}"
        };

        return new KnowledgeQualityItem(
            KnowledgeId: item.Id,
            Domain: item.Domain,
            Title: item.Title,
            LifecycleStatus: lifecycle,
            RetentionState: retention,
            TrustScore: trust,
            EvidenceScore: evidence.Value,
            ReuseScore: reuse.Value,
            ValidationScore: validation.Value,
            AgeScore: age.Value,
            QualityScore: quality,
            EvidenceRefs: sourceRefs
                .Concat(validation.ValidationRefs)
                .Concat(queueRefs)
                .Concat(supportingOutcomes)
                .Concat(supportingGoals)
                .Concat(insightRefs)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(40)
                .ToList(),
            SupportingGoals: supportingGoals,
            SupportingOutcomes: supportingOutcomes,
            Reasons: reasons,
            LastValidatedUtc: item.LastValidatedUtc);
    }

    private static KnowledgeEvidenceScore EvidenceScoreFor(
        KnowledgeCatalogItem item,
        IReadOnlyList<string> sourceRefs,
        IReadOnlyList<string> queueRefs,
        IReadOnlyList<string> outcomeRefs,
        IReadOnlyList<string> goalRefs,
        IReadOnlyList<string> insightRefs)
    {
        var raw = 0.12
            + Math.Min(0.28, sourceRefs.Count * 0.08)
            + Math.Min(0.18, queueRefs.Count * 0.03)
            + Math.Min(0.18, outcomeRefs.Count * 0.04)
            + Math.Min(0.12, goalRefs.Count * 0.025)
            + Math.Min(0.12, insightRefs.Count * 0.03)
            + Math.Min(0.08, item.Tags.Count * 0.01);
        var value = Math.Round(Math.Clamp(raw, 0, 1), 4);
        return new KnowledgeEvidenceScore(
            Value: value,
            Classification: value >= 0.75 ? "strong" : value >= 0.48 ? "moderate" : "weak",
            EvidenceRefs: sourceRefs
                .Concat(queueRefs)
                .Concat(outcomeRefs)
                .Concat(goalRefs)
                .Concat(insightRefs)
                .ToList());
    }

    private static KnowledgeReuseScore ReuseScoreFor(
        KnowledgeCatalogItem item,
        IReadOnlyList<string> queueRefs,
        IReadOnlyList<string> outcomeRefs,
        IReadOnlyList<string> insightRefs)
    {
        var raw = 0.08
            + Math.Min(0.32, queueRefs.Count * 0.06)
            + Math.Min(0.28, outcomeRefs.Count * 0.06)
            + Math.Min(0.2, insightRefs.Count * 0.05)
            + Math.Min(0.12, item.RelatedItems.Count * 0.02);
        var value = Math.Round(Math.Clamp(raw, 0, 1), 4);
        return new KnowledgeReuseScore(
            Value: value,
            Classification: value >= 0.68 ? "reused" : value >= 0.35 ? "limited_reuse" : "low_reuse",
            ReuseRefs: queueRefs.Concat(outcomeRefs).Concat(insightRefs).ToList());
    }

    private static KnowledgeValidationScore ValidationScoreFor(
        KnowledgeCatalogItem item,
        IReadOnlyList<TaskOutcomeResult> outcomes,
        IReadOnlyList<string> queueRefs)
    {
        var statusScore = item.ValidationStatus.ToLowerInvariant() switch
        {
            "trusted" => 0.95,
            "robust" => 0.86,
            "promising" => 0.68,
            "experimental" => 0.48,
            "needs_more_data" => 0.32,
            "untested" => 0.22,
            "overfit_suspected" => 0.18,
            "rejected" => 0.08,
            _ => 0.28
        };
        var outcomeBoost = outcomes.Count == 0
            ? 0
            : Math.Clamp(outcomes.Average(outcome => outcome.OutcomeScore.UsefulnessScore) * 0.22, 0, 0.22);
        var queueBoost = queueRefs.Any(reference => reference.Contains("processed", StringComparison.OrdinalIgnoreCase))
            ? 0.08
            : 0;
        var value = Math.Round(Math.Clamp(statusScore + outcomeBoost + queueBoost, 0, 1), 4);
        return new KnowledgeValidationScore(
            Value: value,
            Status: item.ValidationStatus,
            ValidationRefs: outcomes
                .Select(outcome => $"validation:{outcome.OutcomeId}:{outcome.Recommendation}")
                .Concat(queueRefs.Where(reference => reference.Contains("processed", StringComparison.OrdinalIgnoreCase)).Select(reference => $"validation:{reference}"))
                .Take(16)
                .ToList());
    }

    private KnowledgeLifetimeScore LifetimeScoreFor(KnowledgeCatalogItem item, DateTimeOffset now)
    {
        if (item.LastValidatedUtc is null)
        {
            return new KnowledgeLifetimeScore(
                Value: 0.52,
                Classification: "unvalidated_age_unknown",
                AgeDays: 0,
                Reasons: ["last_validated_utc_missing"]);
        }

        var ageDays = Math.Max(0, (int)Math.Floor((now - item.LastValidatedUtc.Value).TotalDays));
        var score = ageDays switch
        {
            <= 30 => 0.95,
            <= 90 => 0.78,
            <= 180 => 0.58,
            <= 365 => 0.34,
            _ => 0.16
        };
        return new KnowledgeLifetimeScore(
            Value: Math.Round(score, 4),
            Classification: ageDays >= _policy.DeprecationAgeDays ? "aging" : "current",
            AgeDays: ageDays,
            Reasons: [$"age_days:{ageDays}"]);
    }

    private string LifecycleStatus(KnowledgeCatalogItem item, double quality, double trust, double validation, double age)
    {
        if (item.ValidationStatus.Equals("rejected", StringComparison.OrdinalIgnoreCase))
        {
            return "rejected";
        }

        if (age <= _policy.DeprecatedAgeThreshold && quality < 0.58)
        {
            return "deprecated";
        }

        if (quality >= _policy.TrustedQualityThreshold && trust >= 0.76 && validation >= 0.68)
        {
            return "trusted";
        }

        if (quality >= 0.72 && validation >= 0.58)
        {
            return "robust";
        }

        if (quality >= 0.56)
        {
            return "promising";
        }

        if (quality >= _policy.WeakQualityThreshold)
        {
            return "experimental";
        }

        return validation <= 0.24 ? "untested" : "experimental";
    }

    private static string RetentionState(string lifecycle, double quality, double ageScore)
    {
        if (lifecycle.Equals("deprecated", StringComparison.OrdinalIgnoreCase))
        {
            return "deprecated";
        }

        if (lifecycle.Equals("rejected", StringComparison.OrdinalIgnoreCase) && quality < 0.28 && ageScore < 0.35)
        {
            return "archived";
        }

        return "active";
    }

    private static string KnowledgeHealth(
        int total,
        int trusted,
        int weak,
        int deprecated,
        double averageQuality,
        double averageTrust)
    {
        if (total == 0)
        {
            return "empty";
        }

        if (deprecated > total * 0.35 || weak > total * 0.6 || averageQuality < 0.36)
        {
            return "critical";
        }

        if (weak > total * 0.35 || averageQuality < 0.55 || averageTrust < 0.55)
        {
            return "needs_consolidation";
        }

        return trusted > 0 || averageQuality >= 0.68 ? "healthy" : "developing";
    }

    private static string KnowledgeTrend(
        KnowledgeQualityReport? previous,
        double quality,
        double trust,
        int weak,
        int deprecated)
    {
        if (previous is null)
        {
            return "baseline";
        }

        var qualityDelta = quality - previous.AverageQualityScore;
        var trustDelta = trust - previous.AverageTrustScore;
        if (qualityDelta > 0.03 || trustDelta > 0.03 || weak < previous.WeakKnowledge)
        {
            return "improving";
        }

        if (qualityDelta < -0.03 || trustDelta < -0.03 || deprecated > previous.DeprecatedKnowledge)
        {
            return "declining";
        }

        return "stable";
    }

    private GoalState? LoadGoalState()
    {
        var path = new GoalManager(_storagePaths).GoalStatePath;
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<GoalState>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static double Average(IReadOnlyList<KnowledgeQualityItem> items, Func<KnowledgeQualityItem, double> selector) =>
        Math.Round(items.Count == 0 ? 0 : items.Average(selector), 4);
}

public sealed class MemoryConsolidationService
{
    private readonly StoragePaths _storagePaths;

    public MemoryConsolidationService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string ConsolidationPath => Path.Combine(Root, "memory_consolidation.json");

    public MemoryConsolidationReport Run()
    {
        Directory.CreateDirectory(Root);
        var qualityEngine = new KnowledgeQualityEngine(_storagePaths);
        var quality = qualityEngine.Run();
        var duplicateGroups = quality.Items
            .GroupBy(item => DuplicateKey(item), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .ToList();
        var duplicateIds = duplicateGroups
            .SelectMany(group => group.Select(item => item.KnowledgeId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var entries = quality.Items
            .Select(item =>
            {
                var duplicateGroup = duplicateGroups.FirstOrDefault(group =>
                    group.Any(candidate => candidate.KnowledgeId.Equals(item.KnowledgeId, StringComparison.OrdinalIgnoreCase)));
                var related = duplicateGroup?
                    .Where(candidate => !candidate.KnowledgeId.Equals(item.KnowledgeId, StringComparison.OrdinalIgnoreCase))
                    .Select(candidate => candidate.KnowledgeId)
                    .ToList() ?? [];
                var action = item.RetentionState switch
                {
                    "deprecated" => "mark_deprecated",
                    "archived" => "archive_reference_only",
                    _ when item.QualityScore >= KnowledgeRetentionPolicy.Default.TrustedQualityThreshold => "prioritize_active",
                    _ when duplicateIds.Contains(item.KnowledgeId) => "deduplicate_review",
                    _ when item.QualityScore < KnowledgeRetentionPolicy.Default.WeakQualityThreshold => "mark_weak",
                    _ => "keep_active"
                };
                var reason = action switch
                {
                    "mark_deprecated" => "knowledge_lifecycle_deprecated_no_delete",
                    "archive_reference_only" => "weak_rejected_or_aging_reference_only_no_delete",
                    "prioritize_active" => "high_quality_trusted_or_robust_knowledge",
                    "deduplicate_review" => "similar_domain_title_detected",
                    "mark_weak" => "quality_below_retention_threshold",
                    _ => "knowledge_active"
                };
                return new MemoryConsolidationEntry(
                    KnowledgeId: item.KnowledgeId,
                    Domain: item.Domain,
                    Action: action,
                    Reason: reason,
                    RelatedKnowledgeIds: related,
                    QualityScore: item.QualityScore,
                    LifecycleStatus: item.LifecycleStatus);
            })
            .OrderBy(entry => entry.Action, StringComparer.Ordinal)
            .ThenBy(entry => entry.Domain, StringComparer.Ordinal)
            .ThenBy(entry => entry.KnowledgeId, StringComparer.Ordinal)
            .ToList();

        var report = new MemoryConsolidationReport(
            ReportVersion: "memory_consolidation_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TotalKnowledgeItems: quality.TotalKnowledgeItems,
            ActiveKnowledge: quality.Items.Count(item => item.RetentionState.Equals("active", StringComparison.OrdinalIgnoreCase)),
            ArchivedKnowledge: quality.Items.Count(item => item.RetentionState.Equals("archived", StringComparison.OrdinalIgnoreCase)),
            DeprecatedKnowledge: quality.DeprecatedKnowledge,
            DuplicateGroups: duplicateGroups.Count,
            WeakKnowledge: quality.WeakKnowledge,
            PrioritizedKnowledge: entries.Count(entry => entry.Action.Equals("prioritize_active", StringComparison.OrdinalIgnoreCase)),
            Entries: entries,
            Warnings: quality.Warnings
                .Concat(duplicateGroups.Count > 0 ? [$"duplicate_groups:{duplicateGroups.Count}"] : [])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            KnowledgeQualityPath: qualityEngine.QualityPath,
            KnowledgeEvidencePath: qualityEngine.EvidencePath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        File.WriteAllText(ConsolidationPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    public MemoryConsolidationReport? LoadReport()
    {
        if (!File.Exists(ConsolidationPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MemoryConsolidationReport>(
                File.ReadAllText(ConsolidationPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static string DuplicateKey(KnowledgeQualityItem item) =>
        $"{item.Domain}:{Normalize(item.Title)}";

    private static string Normalize(string value)
    {
        var chars = value
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray();
        return chars.Length == 0 ? "unknown" : new string(chars);
    }
}
