using System.Text.Json;

namespace Hermes.Runtime;

public sealed class MasterStatusWriter
{
    private readonly MasterStatusService _service;
    private string? _resolvedSnapshotPath;

    public MasterStatusWriter(MasterStatusService service)
    {
        _service = service;
    }

    public string SnapshotPath => _resolvedSnapshotPath ?? _service.SnapshotPath;

    public MasterStatusSnapshot? LoadSnapshot()
    {
        var primary = _service.SnapshotPath;
        if (!File.Exists(primary))
        {
            return null;
        }

        var snapshot = JsonSerializer.Deserialize<MasterStatusSnapshot>(File.ReadAllText(primary), JsonDefaults.SnapshotReadOptions);
        if (snapshot is not null)
        {
            _resolvedSnapshotPath = primary;
            return snapshot;
        }

        return null;
    }

    public MasterStatusSnapshot WriteSnapshot()
    {
        var snapshot = _service.BuildSnapshot();
        try
        {
            Directory.CreateDirectory(_service.SnapshotDirectory);
            File.WriteAllText(_service.SnapshotPath, JsonSerializer.Serialize(snapshot, JsonDefaults.WriteOptions));
            _resolvedSnapshotPath = _service.SnapshotPath;
        }
        catch (IOException)
        {
            _resolvedSnapshotPath = WriteFallbackSnapshot(snapshot);
        }
        catch (UnauthorizedAccessException)
        {
            _resolvedSnapshotPath = WriteFallbackSnapshot(snapshot);
        }

        return snapshot;
    }

    private string WriteFallbackSnapshot(MasterStatusSnapshot snapshot)
    {
        var fallbackDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".codex_artifacts", "reports", "master-status");
        Directory.CreateDirectory(fallbackDirectory);
        var fallbackPath = Path.Combine(fallbackDirectory, "master_status.json");
        File.WriteAllText(fallbackPath, JsonSerializer.Serialize(snapshot, JsonDefaults.WriteOptions));
        return fallbackPath;
    }


    public MasterStatusSnapshot WriteKnowledgeOnlySnapshot(KnowledgeQualityReport qualityReport)
    {
        var snapshot = LoadSnapshot() ?? _service.BuildSnapshot();
        var updated = ApplyKnowledgeOnlyUpdate(snapshot, qualityReport);

        try
        {
            Directory.CreateDirectory(_service.SnapshotDirectory);
            File.WriteAllText(_service.SnapshotPath, JsonSerializer.Serialize(updated, JsonDefaults.WriteOptions));
            _resolvedSnapshotPath = _service.SnapshotPath;
        }
        catch (IOException)
        {
            _resolvedSnapshotPath = WriteFallbackSnapshot(updated);
        }
        catch (UnauthorizedAccessException)
        {
            _resolvedSnapshotPath = WriteFallbackSnapshot(updated);
        }

        return updated;
    }

    private static MasterStatusSnapshot ApplyKnowledgeOnlyUpdate(MasterStatusSnapshot snapshot, KnowledgeQualityReport qualityReport)
    {
        var metrics = new Dictionary<string, object?>(snapshot.CognitiveStatus.Metrics, StringComparer.OrdinalIgnoreCase)
        {
            ["trusted_knowledge"] = qualityReport.TrustedKnowledge,
            ["weak_knowledge"] = qualityReport.WeakKnowledge,
            ["deprecated_knowledge"] = qualityReport.DeprecatedKnowledge,
            ["average_quality_score"] = qualityReport.AverageQualityScore,
            ["average_trust_score"] = qualityReport.AverageTrustScore,
            ["knowledge_health"] = qualityReport.KnowledgeHealth,
            ["evidence_coverage"] = qualityReport.EvidenceCoverage,
            ["contradiction_count"] = qualityReport.ContradictionCount,
            ["human_reviewed_items"] = qualityReport.HumanReviewedItems,
            ["validation_coverage"] = qualityReport.ValidationCoverage,
            ["trust_distribution"] = qualityReport.TrustDistribution ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        };

        var warnings = snapshot.CognitiveStatus.Warnings
            .Where(warning =>
                !warning.StartsWith("trusted_knowledge:", StringComparison.OrdinalIgnoreCase)
                && !warning.StartsWith("average_trust_score:", StringComparison.OrdinalIgnoreCase)
                && !warning.StartsWith("average_quality_score:", StringComparison.OrdinalIgnoreCase)
                && !warning.StartsWith("knowledge_health:", StringComparison.OrdinalIgnoreCase))
            .Concat(qualityReport.Warnings ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return snapshot with
        {
            LastUpdatedUtc = DateTimeOffset.UtcNow,
            TrustedKnowledge = qualityReport.TrustedKnowledge,
            WeakKnowledge = qualityReport.WeakKnowledge,
            DeprecatedKnowledge = qualityReport.DeprecatedKnowledge,
            AverageQualityScore = qualityReport.AverageQualityScore,
            AverageTrustScore = qualityReport.AverageTrustScore,
            KnowledgeHealth = qualityReport.KnowledgeHealth,
            KnowledgeTrend = qualityReport.KnowledgeTrend,
            EvidenceCoverage = qualityReport.EvidenceCoverage,
            ContradictionCount = qualityReport.ContradictionCount,
            HumanReviewedItems = qualityReport.HumanReviewedItems,
            ValidationCoverage = qualityReport.ValidationCoverage,
            TrustDistribution = qualityReport.TrustDistribution ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase),
            CognitiveStatus = snapshot.CognitiveStatus with
            {
                Metrics = metrics,
                Warnings = warnings
            },
            Warnings = snapshot.Warnings
                .Where(warning => !warning.StartsWith("trusted_knowledge:", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

}
