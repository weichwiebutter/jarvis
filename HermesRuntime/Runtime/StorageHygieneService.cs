using System.Text.Json;

namespace Hermes.Runtime;

public sealed class StorageHygieneService
{
    private readonly StoragePaths _storagePaths;
    private readonly RetentionPolicy _policy;
    private string? _resolvedReportDirectory;

    public StorageHygieneService(StoragePaths storagePaths, RetentionPolicy? policy = null)
    {
        _storagePaths = storagePaths;
        _policy = policy ?? RetentionPolicy.Default;
    }

    public string ReportDirectory => _resolvedReportDirectory ??= ResolveReportDirectory();

    public string CleanupPlanPath => Path.Combine(ReportDirectory, "cleanup_plan.json");

    public string CleanupReportPath => Path.Combine(ReportDirectory, "cleanup_report.json");

    public string CleanupAuditLogPath => Path.Combine(ReportDirectory, "cleanup_audit_log.jsonl");

    public string StatusPath => Path.Combine(ReportDirectory, "storage_status.json");

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

        var filteredCandidates = candidates
            .Where(candidate => !IsProtected(candidate.Path, protectedPaths))
            .ToList();
        var safeCandidates = filteredCandidates.Where(candidate => candidate.SafeToDelete).ToList();
        var policyStatus = EvaluatePolicy(safeCandidates, protectedPaths);
        var plan = new CleanupPlan(
            PlanId: $"cleanup_plan_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            StorageRoot: _storagePaths.Root,
            ProtectedPaths: protectedPaths,
            Candidates: filteredCandidates,
            EstimatedBytesToFree: safeCandidates.Sum(candidate => candidate.EstimatedBytes),
            PolicyStatus: policyStatus,
            SafeToApply: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteTextWithFallback(CleanupPlanPath, JsonSerializer.Serialize(plan, JsonDefaults.WriteOptions));
        WriteStatus(plan);
        return plan;
    }

    public CleanupReport ApplySafeCleanup()
    {
        var plan = LoadPlan() ?? BuildPlan();
        var deleted = new List<string>();
        var skipped = new List<string>();
        var bytes = 0L;
        var unsafeSkipped = 0;
        var protectedSkipped = 0;
        var protectedPaths = plan.ProtectedPaths ?? [];

        foreach (var candidate in plan.Candidates)
        {
            if (!candidate.SafeToDelete)
            {
                unsafeSkipped++;
                skipped.Add($"{candidate.Path}: unsafe_candidate_skipped");
                continue;
            }

            if (IsProtected(candidate.Path, protectedPaths))
            {
                protectedSkipped++;
                skipped.Add($"{candidate.Path}: protected_path_skipped");
                continue;
            }

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
            UnsafeCandidatesSkipped: unsafeSkipped,
            ProtectedCandidatesSkipped: protectedSkipped,
            DeletedPaths: deleted,
            SkippedPaths: skipped,
            AuditLogPath: CleanupAuditLogPath,
            SafeMode: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteTextWithFallback(CleanupReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        AppendTextWithFallback(CleanupAuditLogPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions) + Environment.NewLine);
        WriteStatus(BuildPlan());
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

    public StorageStatusSnapshot BuildStatus()
    {
        var plan = LoadPlan() ?? BuildPlan();
        return WriteStatus(plan);
    }

    public StorageStatusSnapshot? LoadStatus()
    {
        if (!File.Exists(StatusPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StorageStatusSnapshot>(File.ReadAllText(StatusPath), JsonDefaults.SnapshotReadOptions);
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
        yield return Path.Combine(_storagePaths.Root, "reports", "signal_agent_specs");
        yield return Path.Combine(_storagePaths.Root, "reports", "scalping_bot_specs");
        yield return Path.Combine(_storagePaths.Root, "reports", "scalping_portfolio", "ensemble_export");
        yield return Path.Combine(_storagePaths.Root, "reports", "scalping_portfolio", "ensemble_review");
        yield return Path.Combine(_storagePaths.Root, "reports", "forward_test");
        yield return Path.Combine(_storagePaths.Root, "reports", "signal_watch");
        yield return Path.Combine(_storagePaths.Root, "auth");
        yield return Path.Combine(_storagePaths.Root, "market_data", "candles");
        yield return Path.Combine(_storagePaths.Root, "config");
    }

    private StoragePolicyStatus EvaluatePolicy(IReadOnlyList<CleanupCandidate> safeCandidates, IReadOnlyList<string> protectedPaths)
    {
        var (freeMb, freePercent) = ReadDisk();
        var usagePercent = Math.Round(100 - freePercent, 2);
        var warnings = new List<string>();
        var safetyMode = "monitor_only";
        var policyAction = "monitor";
        var autoCleanupAllowed = false;

        if (usagePercent >= 95)
        {
            safetyMode = "auto_safe_cleanup_allowed";
            policyAction = "auto_safe_cleanup_allowed";
            autoCleanupAllowed = safeCandidates.Count > 0;
            warnings.Add("disk_usage_above_95_percent_safe_cleanup_allowed_for_safe_candidates_only");
        }
        else if (usagePercent >= 85)
        {
            safetyMode = "safe_cleanup_recommended";
            policyAction = "recommend_safe_cleanup";
            warnings.Add("disk_usage_between_85_and_95_percent_safe_cleanup_recommended");
        }
        else if (usagePercent >= 70)
        {
            safetyMode = "warning_cleanup_plan";
            policyAction = "generate_cleanup_plan";
            warnings.Add("disk_usage_between_70_and_85_percent_cleanup_plan_required");
        }

        var lastReport = LoadCleanupReport();
        return new StoragePolicyStatus(
            PolicyVersion: "auto_storage_hygiene_policy_v1",
            AutoCleanupPolicyEnabled: true,
            AutoCleanupAllowed: autoCleanupAllowed,
            SafetyMode: safetyMode,
            DiskUsagePercent: usagePercent,
            FreeDiskPercent: Math.Round(freePercent, 2),
            PolicyAction: policyAction,
            AutoCleanupLastRun: lastReport?.CreatedAtUtc,
            AutoCleanupLastResult: lastReport is null ? "never_run" : $"deleted={lastReport.FilesDeleted};bytes_freed={lastReport.BytesFreed}",
            CleanupCandidates: safeCandidates.Count,
            EstimatedFreeBytes: safeCandidates.Sum(candidate => candidate.EstimatedBytes),
            ProtectedPathsCount: protectedPaths.Count,
            Warnings: warnings);
    }

    private StorageStatusSnapshot WriteStatus(CleanupPlan plan)
    {
        var status = new StorageStatusSnapshot(
            StatusVersion: "storage_status_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            StorageRoot: plan.StorageRoot,
            FreeDiskPercent: plan.PolicyStatus.FreeDiskPercent,
            DiskUsagePercent: plan.PolicyStatus.DiskUsagePercent,
            CleanupCandidates: plan.PolicyStatus.CleanupCandidates,
            EstimatedFreeBytes: plan.PolicyStatus.EstimatedFreeBytes,
            ProtectedPathsCount: plan.PolicyStatus.ProtectedPathsCount,
            AutoCleanupPolicyEnabled: plan.PolicyStatus.AutoCleanupPolicyEnabled,
            AutoCleanupAllowed: plan.PolicyStatus.AutoCleanupAllowed,
            AutoCleanupLastRun: plan.PolicyStatus.AutoCleanupLastRun,
            AutoCleanupLastResult: plan.PolicyStatus.AutoCleanupLastResult,
            SafetyMode: plan.PolicyStatus.SafetyMode,
            PolicyAction: plan.PolicyStatus.PolicyAction,
            Warnings: plan.PolicyStatus.Warnings,
            SafeToApply: plan.SafeToApply,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
        WriteTextWithFallback(StatusPath, JsonSerializer.Serialize(status, JsonDefaults.WriteOptions));
        return status;
    }

    private CleanupReport? LoadCleanupReport()
    {
        if (!File.Exists(CleanupReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<CleanupReport>(File.ReadAllText(CleanupReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private (long FreeMb, double FreePercent) ReadDisk()
    {
        try
        {
            var root = Path.GetPathRoot(_storagePaths.Root);
            if (!string.IsNullOrWhiteSpace(root))
            {
                var drive = new DriveInfo(root);
                var free = drive.AvailableFreeSpace / 1024 / 1024;
                var total = Math.Max(1, drive.TotalSize / 1024 / 1024);
                return (free, free / (double)total * 100);
            }
        }
        catch (IOException)
        {
        }

        return (0, 0);
    }

    private string ResolveReportDirectory()
    {
        var preferred = Path.Combine(_storagePaths.Root, "reports", "storage");
        try
        {
            Directory.CreateDirectory(preferred);
            return preferred;
        }
        catch (IOException)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), ".codex_artifacts", "reports", "storage");
        }
        catch (UnauthorizedAccessException)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), ".codex_artifacts", "reports", "storage");
        }
    }

    private void WriteTextWithFallback(string path, string content)
    {
        try
        {
            File.WriteAllText(path, content);
        }
        catch (IOException)
        {
            _resolvedReportDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".codex_artifacts", "reports", "storage");
            Directory.CreateDirectory(ReportDirectory);
            File.WriteAllText(Path.Combine(ReportDirectory, Path.GetFileName(path)), content);
        }
        catch (UnauthorizedAccessException)
        {
            _resolvedReportDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".codex_artifacts", "reports", "storage");
            Directory.CreateDirectory(ReportDirectory);
            File.WriteAllText(Path.Combine(ReportDirectory, Path.GetFileName(path)), content);
        }
    }

    private void AppendTextWithFallback(string path, string content)
    {
        try
        {
            File.AppendAllText(path, content);
        }
        catch (IOException)
        {
            _resolvedReportDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".codex_artifacts", "reports", "storage");
            Directory.CreateDirectory(ReportDirectory);
            File.AppendAllText(Path.Combine(ReportDirectory, Path.GetFileName(path)), content);
        }
        catch (UnauthorizedAccessException)
        {
            _resolvedReportDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".codex_artifacts", "reports", "storage");
            Directory.CreateDirectory(ReportDirectory);
            File.AppendAllText(Path.Combine(ReportDirectory, Path.GetFileName(path)), content);
        }
    }

    private static bool IsProtected(string path, IReadOnlyList<string> protectedPaths)
    {
        var full = Path.GetFullPath(path);
        return protectedPaths.Any(protectedPath =>
            full.StartsWith(Path.GetFullPath(protectedPath), StringComparison.OrdinalIgnoreCase));
    }
}
