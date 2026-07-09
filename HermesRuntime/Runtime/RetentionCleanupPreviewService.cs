using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record RetentionCleanupPreviewEntry(
    string Path,
    string RetentionClass,
    string Reason,
    double AgeDays,
    string? ProtectedReason);

public sealed record RetentionCleanupPreviewReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int KeepCount,
    int Retain30dCount,
    int Retain7dCount,
    int DeletableCount,
    int EstimatedReclaimableFiles,
    long EstimatedReclaimableBytes,
    bool PreviewLimited,
    IReadOnlyList<RetentionCleanupPreviewEntry> CandidatePaths,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<string> ProtectedPaths,
    string OperatorSummary,
    bool NoDeletionPerformed,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class RetentionCleanupPreviewService
{
    private const int DefaultCandidateLimit = 500;

    private readonly StoragePaths _storagePaths;
    private readonly StorageHygieneService _storageHygiene;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public RetentionCleanupPreviewService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
        _storageHygiene = new StorageHygieneService(storagePaths);
    }

    public string Root => Path.Combine(_storagePaths.Root, ".codex_artifacts", "reports", "retention_cleanup_preview");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "retention_cleanup_preview.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "retention_cleanup_preview.md");

    public RetentionCleanupPreviewReport Run(bool full = false)
    {
        Directory.CreateDirectory(Root);

        var planPath = _storageHygiene.CleanupPlanPath;
        var previewLimit = full ? int.MaxValue : DefaultCandidateLimit;
        var data = full ? LoadFullData(planPath, previewLimit) : LoadFastData(planPath, previewLimit);

        var report = new RetentionCleanupPreviewReport(
            ReportVersion: "retention_cleanup_preview_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            KeepCount: data.Summary.KeepCount,
            Retain30dCount: data.Summary.Retain30dCount,
            Retain7dCount: data.Summary.Retain7dCount,
            DeletableCount: data.Summary.DeletableCount,
            EstimatedReclaimableFiles: data.Summary.EstimatedReclaimableFiles,
            EstimatedReclaimableBytes: data.Summary.EstimatedReclaimableBytes,
            PreviewLimited: !full,
            CandidatePaths: data.Candidates,
            Reasons: data.Summary.Reasons,
            ProtectedPaths: data.Summary.ProtectedPaths,
            OperatorSummary: BuildOperatorSummary(data.Summary, !full),
            NoDeletionPerformed: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteReport(report);
        return report;
    }

    private sealed record RetentionSummary(
        int KeepCount,
        int Retain30dCount,
        int Retain7dCount,
        int DeletableCount,
        int EstimatedReclaimableFiles,
        long EstimatedReclaimableBytes,
        int TotalCount,
        IReadOnlyList<string> Reasons,
        IReadOnlyList<string> ProtectedPaths);

    private sealed record RetentionPreviewData(RetentionSummary Summary, IReadOnlyList<RetentionCleanupPreviewEntry> Candidates);

    private RetentionPreviewData LoadFastData(string planPath, int limit)
    {
        if (!File.Exists(planPath))
        {
            return new RetentionPreviewData(
                new RetentionSummary(0, 0, 0, 0, 0, 0, 0, Array.Empty<string>(), Array.Empty<string>()),
                Array.Empty<RetentionCleanupPreviewEntry>());
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(planPath));
            var root = doc.RootElement;
            var protectedPaths = ReadStringArray(root, "protected_paths").Take(50).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var candidates = root.TryGetProperty("candidates", out var candidatesElement) && candidatesElement.ValueKind == JsonValueKind.Array
                ? candidatesElement.EnumerateArray()
                : Enumerable.Empty<JsonElement>();

            var previewItems = new List<RetentionCleanupPreviewEntry>();
            var reasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var totalCount = ReadInt(root, "policy_status", "cleanup_candidates");
            if (totalCount <= 0)
            {
                totalCount = ReadInt(root, "candidates", "count");
            }

            var deletableCount = totalCount;
            var estimatedBytes = ReadLong(root, "estimated_bytes_to_free") ?? ReadLong(root, "policy_status", "estimated_free_bytes") ?? 0L;

            foreach (var candidate in candidates)
            {
                if (previewItems.Count >= limit)
                {
                    break;
                }

                var path = ReadString(candidate, "path");
                var reason = ReadString(candidate, "reason");
                var safeToDelete = ReadBoolean(candidate, "safe_to_delete");
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    reasons.Add(reason);
                }

                var protectedReason = GetProtectedReason(path, protectedPaths);
                var retentionClass = DetermineRetentionClass(path, reason, safeToDelete, protectedReason);
                previewItems.Add(new RetentionCleanupPreviewEntry(
                    Path: Path.GetFullPath(path),
                    RetentionClass: retentionClass,
                    Reason: reason,
                    AgeDays: TryGetAgeDays(path),
                    ProtectedReason: protectedReason));
            }

            foreach (var path in GetKnownProtectedPreviewPaths())
            {
                if (previewItems.Count >= limit)
                {
                    break;
                }

                previewItems.Add(new RetentionCleanupPreviewEntry(
                    Path: Path.GetFullPath(path),
                    RetentionClass: "keep",
                    Reason: "protected_path",
                    AgeDays: TryGetAgeDays(path),
                    ProtectedReason: GetProtectedReason(path, protectedPaths)));
            }

            return new RetentionPreviewData(
                new RetentionSummary(
                    KeepCount: 0,
                    Retain30dCount: 0,
                    Retain7dCount: 0,
                    DeletableCount: deletableCount,
                    EstimatedReclaimableFiles: deletableCount,
                    EstimatedReclaimableBytes: estimatedBytes,
                    TotalCount: totalCount,
                    Reasons: reasons.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).Take(20).ToList(),
                    ProtectedPaths: protectedPaths.ToList()),
                previewItems);
        }
        catch
        {
            return new RetentionPreviewData(
                new RetentionSummary(0, 0, 0, 0, 0, 0, 0, Array.Empty<string>(), Array.Empty<string>()),
                Array.Empty<RetentionCleanupPreviewEntry>());
        }
    }

    private RetentionPreviewData LoadFullData(string planPath, int limit)
    {
        if (!File.Exists(planPath))
        {
            return new RetentionPreviewData(
                new RetentionSummary(0, 0, 0, 0, 0, 0, 0, Array.Empty<string>(), Array.Empty<string>()),
                Array.Empty<RetentionCleanupPreviewEntry>());
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(planPath));
            var root = doc.RootElement;
            var protectedPaths = ReadStringArray(root, "protected_paths").Take(50).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var candidates = root.TryGetProperty("candidates", out var candidatesElement) && candidatesElement.ValueKind == JsonValueKind.Array
                ? candidatesElement.EnumerateArray()
                : Enumerable.Empty<JsonElement>();

            var keepCount = 0;
            var retain30dCount = 0;
            var retain7dCount = 0;
            var deletableCount = 0;
            var reclaimableFiles = 0;
            var reclaimableBytes = 0L;
            var totalCount = 0;
            var reasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var previewItems = new List<RetentionCleanupPreviewEntry>();

            foreach (var candidate in candidates)
            {
                totalCount++;
                var path = ReadString(candidate, "path");
                var reason = ReadString(candidate, "reason");
                var safeToDelete = ReadBoolean(candidate, "safe_to_delete");
                if (!string.IsNullOrWhiteSpace(reason))
                {
                    reasons.Add(reason);
                }

                var protectedReason = GetProtectedReason(path, protectedPaths);
                var retentionClass = DetermineRetentionClass(path, reason, safeToDelete, protectedReason);

                switch (retentionClass)
                {
                    case "deletable":
                        deletableCount++;
                        reclaimableFiles++;
                        reclaimableBytes += ReadLong(candidate, "estimated_bytes") ?? 0L;
                        break;
                    case "retain_30d":
                        retain30dCount++;
                        break;
                    case "retain_7d":
                        retain7dCount++;
                        break;
                    default:
                        keepCount++;
                        break;
                }

                if (previewItems.Count < limit)
                {
                    var ageDays = TryGetAgeDays(path);
                    previewItems.Add(new RetentionCleanupPreviewEntry(
                        Path: Path.GetFullPath(path),
                        RetentionClass: retentionClass,
                        Reason: reason,
                        AgeDays: ageDays,
                        ProtectedReason: protectedReason));
                }
            }

            foreach (var path in GetKnownProtectedPreviewPaths())
            {
                if (previewItems.Count >= limit)
                {
                    break;
                }

                previewItems.Add(new RetentionCleanupPreviewEntry(
                    Path: Path.GetFullPath(path),
                    RetentionClass: "keep",
                    Reason: "protected_path",
                    AgeDays: TryGetAgeDays(path),
                    ProtectedReason: GetProtectedReason(path, protectedPaths)));
            }

            return new RetentionPreviewData(
                new RetentionSummary(
                    keepCount,
                    retain30dCount,
                    retain7dCount,
                    deletableCount,
                    reclaimableFiles,
                    reclaimableBytes,
                    totalCount,
                    reasons.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).Take(20).ToList(),
                    protectedPaths.ToList()),
                previewItems);
        }
        catch
        {
            return new RetentionPreviewData(
                new RetentionSummary(0, 0, 0, 0, 0, 0, 0, Array.Empty<string>(), Array.Empty<string>()),
                Array.Empty<RetentionCleanupPreviewEntry>());
        }
    }

    private static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static long? ReadLong(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static long? ReadLong(JsonElement root, params string[] path)
    {
        if (!TryGetNestedProperty(root, path, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static int ReadInt(JsonElement root, params string[] path)
    {
        if (!TryGetNestedProperty(root, path, out var value))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    private static bool ReadBoolean(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(value.GetString(), out var parsed) => parsed,
            _ => false
        };
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    private static bool TryGetNestedProperty(JsonElement root, IReadOnlyList<string> path, out JsonElement value)
    {
        value = root;
        foreach (var segment in path)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
            {
                value = default;
                return false;
            }
        }

        return true;
    }

    private static string GetProtectedReason(string path, ISet<string> protectedPaths)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        if (IsCurrentExportPath(path))
        {
            return "current_export";
        }

        if (IsEmbeddedPackagePath(path))
        {
            return "current_embedded_package";
        }

        var fileName = Path.GetFileName(path);
        if (fileName.Contains("approved", StringComparison.OrdinalIgnoreCase)) return "approved";
        if (fileName.Contains("promoted", StringComparison.OrdinalIgnoreCase)) return "promoted";
        if (fileName.Contains("baseline", StringComparison.OrdinalIgnoreCase)) return "baseline";
        if (fileName.Contains("audit", StringComparison.OrdinalIgnoreCase)) return "audit";
        if (fileName.Contains("safety", StringComparison.OrdinalIgnoreCase)) return "safety";
        if (fileName.Contains("handover", StringComparison.OrdinalIgnoreCase)) return "handover";

        if (protectedPaths.Contains(path))
        {
            return "protected_path";
        }

        foreach (var protectedPath in protectedPaths)
        {
            if (path.StartsWith(protectedPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                return "protected_path";
            }
        }

        if (protectedPaths.Any(protectedPath =>
                string.Equals(path, protectedPath, StringComparison.OrdinalIgnoreCase)))
        {
            return "protected_path";
        }

        return string.Empty;
    }

    private static bool IsCurrentExportPath(string path)
    {
        return path.Contains("HermesPaperBot_latest.algo", StringComparison.OrdinalIgnoreCase)
            || path.Contains("HermesPaperBot_latest.algo.metadata", StringComparison.OrdinalIgnoreCase)
            || path.Contains("HermesPaperBot_", StringComparison.OrdinalIgnoreCase)
            || path.Contains("ctrader_export_manifest.json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEmbeddedPackagePath(string path)
    {
        return path.Contains("EmbeddedReleasePackage.g.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static string DetermineRetentionClass(string path, string reason, bool safeToDelete, string? protectedReason)
    {
        if (!string.IsNullOrWhiteSpace(protectedReason))
        {
            return "keep";
        }

        if (reason.Contains("approved", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("promoted", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("baseline", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("audit", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("handover", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("safety", StringComparison.OrdinalIgnoreCase))
        {
            return "keep";
        }

        if ((path.Contains("simulation", StringComparison.OrdinalIgnoreCase) && path.Contains("report", StringComparison.OrdinalIgnoreCase))
            && safeToDelete)
        {
            return "deletable";
        }

        if ((path.Contains("temp", StringComparison.OrdinalIgnoreCase)
             || path.Contains("diagnostic", StringComparison.OrdinalIgnoreCase)
             || path.Contains("debug", StringComparison.OrdinalIgnoreCase)
             || path.Contains("trace", StringComparison.OrdinalIgnoreCase))
            && safeToDelete)
        {
            return "deletable";
        }

        if ((reason.Contains("failed", StringComparison.OrdinalIgnoreCase)
             || reason.Contains("stale", StringComparison.OrdinalIgnoreCase)
             || reason.Contains("intermediate", StringComparison.OrdinalIgnoreCase)
             || path.Contains("intermediate", StringComparison.OrdinalIgnoreCase)
             || path.Contains("stale", StringComparison.OrdinalIgnoreCase)
             || path.Contains("failed", StringComparison.OrdinalIgnoreCase))
            && safeToDelete)
        {
            return "deletable";
        }

        return safeToDelete ? "retain_30d" : "keep";
    }

    private static double TryGetAgeDays(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return Math.Max(0d, (DateTimeOffset.UtcNow - new FileInfo(path).LastWriteTimeUtc).TotalDays);
            }

            if (Directory.Exists(path))
            {
                return Math.Max(0d, (DateTimeOffset.UtcNow - new DirectoryInfo(path).LastWriteTimeUtc).TotalDays);
            }
        }
        catch
        {
        }

        return 0d;
    }

    private IEnumerable<string> GetKnownProtectedPreviewPaths()
    {
        var exportManifestPath = GetExportManifestPath();
        if (File.Exists(exportManifestPath))
        {
            yield return exportManifestPath;
            foreach (var path in ReadManifestPaths(exportManifestPath))
            {
                yield return path;
            }
        }

        yield return Path.Combine(_storagePaths.Root, "ctrader", "HermesPaperBot", "Generated", "EmbeddedReleasePackage.g.cs");
        yield return Path.Combine(_storagePaths.Root, "ctrader", "HermesPaperBot.AlgoProject", "Generated", "EmbeddedReleasePackage.g.cs");
        yield return Path.Combine(_storagePaths.Root, "reports", "storage_cleanup", "storage_cleanup_safety_audit.json");
        yield return Path.Combine(_storagePaths.Root, "reports", "storage_cleanup", "storage_cleanup_safety_audit.md");
        yield return Path.Combine(_storagePaths.Root, "reports", "autonomous_improvement_queue", "planned_task_executor_diagnosis.json");
        yield return Path.Combine(_storagePaths.Root, "reports", "autonomous_improvement_queue", "planned_task_executor_diagnosis.md");
        yield return Path.Combine(_storagePaths.Root, "reports", "bot_evolution_baseline", "bot_evolution_baseline.json");
        yield return Path.Combine(_storagePaths.Root, "reports", "bot_evolution_baseline", "bot_evolution_baseline.md");
        yield return Path.Combine(_storagePaths.Root, "reports", "bot_evolution_history", "bot_evolution_history.json");
        yield return Path.Combine(_storagePaths.Root, "reports", "bot_evolution_history", "bot_evolution_history.md");
        yield return Path.Combine(_storagePaths.Root, "reports", "approved_chart_annotations", "approved_chart_annotations.json");
        yield return Path.Combine(_storagePaths.Root, "reports", "approved_chart_annotations", "approved_chart_annotations.md");
    }

    private IReadOnlyList<string> ReadManifestPaths(string manifestPath)
    {
        var paths = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = doc.RootElement;
            foreach (var property in new[]
            {
                "source_algo_path",
                "indexed_algo_path",
                "indexed_algo_metadata_path",
                "latest_algo_path",
                "latest_algo_metadata_path",
                "readiness_json_path",
                "readiness_markdown_path"
            })
            {
                if (root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    var path = value.GetString();
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        paths.Add(path);
                    }
                }
            }
        }
        catch
        {
        }

        return paths;
    }

    private string GetExportManifestPath()
    {
        var wsl = Path.Combine(Path.DirectorySeparatorChar.ToString(), "mnt", "d", "Bot", "ctrader_export_manifest.json");
        if (File.Exists(wsl))
        {
            return wsl;
        }

        return Path.Combine("D:\\Bot", "ctrader_export_manifest.json");
    }

    private static string BuildOperatorSummary(RetentionSummary summary, bool previewLimited)
    {
        var suffix = previewLimited ? " Preview begrenzt auf 500 Kandidaten." : string.Empty;
        return $"Retention preview für {summary.TotalCount} bekannte Dateien/Artefakte: keep={summary.KeepCount}, retain_30d={summary.Retain30dCount}, retain_7d={summary.Retain7dCount}, deletable={summary.DeletableCount}. Keine Datei wurde gelöscht.{suffix}";
    }

    private void WriteReport(RetentionCleanupPreviewReport report)
    {
        try
        {
            Directory.CreateDirectory(Root);
            var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
            File.WriteAllText(ReportPath, json);
            File.WriteAllText(MarkdownPath, BuildMarkdown(report));
            _resolvedReportPath = ReportPath;
            _resolvedMarkdownPath = MarkdownPath;
        }
        catch
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "retention_cleanup_preview");
            Directory.CreateDirectory(fallbackRoot);
            _resolvedReportPath = Path.Combine(fallbackRoot, "retention_cleanup_preview.json");
            _resolvedMarkdownPath = Path.Combine(fallbackRoot, "retention_cleanup_preview.md");
            File.WriteAllText(_resolvedReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
            File.WriteAllText(_resolvedMarkdownPath, BuildMarkdown(report));
        }
    }

    private static string BuildMarkdown(RetentionCleanupPreviewReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Retention Cleanup Preview");
        sb.AppendLine();
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- keep_count: {report.KeepCount}");
        sb.AppendLine($"- retain_30d_count: {report.Retain30dCount}");
        sb.AppendLine($"- retain_7d_count: {report.Retain7dCount}");
        sb.AppendLine($"- deletable_count: {report.DeletableCount}");
        sb.AppendLine($"- estimated_reclaimable_files: {report.EstimatedReclaimableFiles}");
        sb.AppendLine($"- estimated_reclaimable_bytes: {report.EstimatedReclaimableBytes}");
        sb.AppendLine($"- preview_limited: {report.PreviewLimited.ToString().ToLowerInvariant()}");
        sb.AppendLine();
        sb.AppendLine("## Candidate Paths");
        foreach (var item in report.CandidatePaths)
        {
            sb.AppendLine($"- {item.Path} | retention={item.RetentionClass} | age_days={item.AgeDays:0.##} | reason={item.Reason} | protected_reason={item.ProtectedReason ?? "-"}");
        }
        sb.AppendLine();
        sb.AppendLine("## Protected Paths");
        foreach (var path in report.ProtectedPaths)
        {
            sb.AppendLine($"- {path}");
        }
        sb.AppendLine();
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("Safety: no deletion performed; no trading execution; no broker action; no auto trading; human review required.");
        return sb.ToString();
    }
}
