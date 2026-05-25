using System.Text.Json;

namespace Hermes.Runtime;

public sealed class StorageHygieneService
{
    private readonly StoragePaths _storagePaths;
    private readonly RetentionPolicy _policy;

    public StorageHygieneService(StoragePaths storagePaths, RetentionPolicy? policy = null)
    {
        _storagePaths = storagePaths;
        _policy = policy ?? RetentionPolicy.Default;
    }

    public string ReportDirectory => Path.Combine(_storagePaths.Root, "reports", "storage");

    public string CleanupPlanPath => Path.Combine(ReportDirectory, "cleanup_plan.json");

    public string CleanupReportPath => Path.Combine(ReportDirectory, "cleanup_report.json");

    public CleanupPlan BuildPlan()
    {
        Directory.CreateDirectory(ReportDirectory);
        var protectedPaths = ProtectedPaths().ToList();
        var candidates = new List<CleanupCandidate>();

        if (_policy.AllowCacheCleanup)
        {
            candidates.AddRange(EnumerateFiles(Path.Combine(_storagePaths.Root, "cache"), "cache_cleanup"));
        }

        if (_policy.AllowTempCleanup)
        {
            candidates.AddRange(EnumerateFiles(Path.Combine(_storagePaths.Root, "temp"), "temp_cleanup"));
        }

        candidates.AddRange(OldCheckpointCandidates());
        candidates.AddRange(ObsoleteSimulationCandidates());

        var safeCandidates = candidates
            .Where(candidate => candidate.SafeToDelete && !IsProtected(candidate.Path, protectedPaths))
            .ToList();
        var plan = new CleanupPlan(
            PlanId: $"cleanup_plan_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            StorageRoot: _storagePaths.Root,
            ProtectedPaths: protectedPaths,
            Candidates: safeCandidates,
            EstimatedBytesToFree: safeCandidates.Sum(candidate => candidate.EstimatedBytes),
            SafeToApply: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(CleanupPlanPath, JsonSerializer.Serialize(plan, JsonDefaults.WriteOptions));
        return plan;
    }

    public CleanupReport ApplySafeCleanup()
    {
        var plan = LoadPlan() ?? BuildPlan();
        var deleted = new List<string>();
        var skipped = new List<string>();
        var bytes = 0L;

        foreach (var candidate in plan.Candidates.Where(candidate => candidate.SafeToDelete))
        {
            try
            {
                if (!File.Exists(candidate.Path))
                {
                    skipped.Add(candidate.Path);
                    continue;
                }

                var info = new FileInfo(candidate.Path);
                bytes += info.Length;
                File.Delete(candidate.Path);
                deleted.Add(candidate.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                skipped.Add($"{candidate.Path}: {ex.Message}");
            }
        }

        var report = new CleanupReport(
            ReportId: $"cleanup_report_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            PlanId: plan.PlanId,
            FilesDeleted: deleted.Count,
            BytesFreed: bytes,
            DeletedPaths: deleted,
            SkippedPaths: skipped,
            SafeMode: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(CleanupReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    public CleanupPlan? LoadPlan()
    {
        if (!File.Exists(CleanupPlanPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CleanupPlan>(
                File.ReadAllText(CleanupPlanPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private IEnumerable<CleanupCandidate> OldCheckpointCandidates()
    {
        var checkpointRoot = Path.Combine(_storagePaths.Root, "research_memory", "checkpoints");
        if (!Directory.Exists(checkpointRoot))
        {
            yield break;
        }

        var cutoff = DateTime.UtcNow.AddDays(-_policy.KeepCheckpointDays);
        foreach (var file in Directory.EnumerateFiles(checkpointRoot, "*.checkpoint.json", SearchOption.TopDirectoryOnly))
        {
            var info = new FileInfo(file);
            if (info.LastWriteTimeUtc < cutoff)
            {
                yield return new CleanupCandidate(file, "old_checkpoint_after_retention", info.Length, SafeToDelete: true);
            }
        }
    }

    private IEnumerable<CleanupCandidate> ObsoleteSimulationCandidates()
    {
        var root = Path.Combine(_storagePaths.Root, "simulation", "reports");
        var status = Path.Combine(_storagePaths.Root, "simulation", "simulation_status.json");
        if (!Directory.Exists(root) || !File.Exists(status))
        {
            yield break;
        }

        var files = Directory.EnumerateFiles(root, "*.simulation_report.json", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .Skip(_policy.KeepLatestSimulationReports)
            .ToList();
        foreach (var info in files)
        {
            yield return new CleanupCandidate(info.FullName, "old_detailed_simulation_report_summary_exists", info.Length, SafeToDelete: true);
        }
    }

    private static IEnumerable<CleanupCandidate> EnumerateFiles(string root, string reason)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var info = new FileInfo(file);
            yield return new CleanupCandidate(file, reason, info.Length, SafeToDelete: true);
        }
    }

    private IEnumerable<string> ProtectedPaths()
    {
        yield return Path.Combine(_storagePaths.Root, "research_memory");
        yield return Path.Combine(_storagePaths.Root, "strategy_research", "strategy_research_memory.json");
        yield return Path.Combine(_storagePaths.Root, "strategy_research", "research_insights.json");
        yield return Path.Combine(_storagePaths.Root, "strategy_research", "robust_strategies.json");
        yield return Path.Combine(_storagePaths.Root, "auth");
        yield return Path.Combine(_storagePaths.Root, "market_data", "candles");
        yield return Path.Combine(_storagePaths.Root, "config");
    }

    private static bool IsProtected(string path, IReadOnlyList<string> protectedPaths)
    {
        var full = Path.GetFullPath(path);
        return protectedPaths.Any(protectedPath =>
            full.StartsWith(Path.GetFullPath(protectedPath), StringComparison.OrdinalIgnoreCase));
    }
}
