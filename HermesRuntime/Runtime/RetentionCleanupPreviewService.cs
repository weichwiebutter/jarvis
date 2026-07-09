using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record RetentionCleanupPreviewEntry(
    string Path,
    string RetentionClass,
    string Reason,
    double AgeDays,
    string? ProtectedReason,
    bool FromCurrentExport,
    bool FromEmbeddedPackage);

public sealed record RetentionCleanupPreviewReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int KeepCount,
    int Retain30dCount,
    int Retain7dCount,
    int DeletableCount,
    int EstimatedReclaimableFiles,
    long EstimatedReclaimableBytes,
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

    public RetentionCleanupPreviewReport Run()
    {
        Directory.CreateDirectory(Root);

        var plan = _storageHygiene.LoadPlan() ?? _storageHygiene.BuildPlan();
        var entries = new List<RetentionCleanupPreviewEntry>();

        entries.AddRange(plan.Candidates.Select(candidate => BuildEntry(candidate.Path, candidate.Reason, plan.ProtectedPaths, fromCurrentExport: false, fromEmbeddedPackage: false)));
        entries.AddRange(ProtectedPreviewEntries(plan.ProtectedPaths));

        var uniqueEntries = entries
            .GroupBy(entry => Path.GetFullPath(entry.Path), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        var keepCount = uniqueEntries.Count(entry => entry.RetentionClass.Equals("keep", StringComparison.OrdinalIgnoreCase));
        var retain30dCount = uniqueEntries.Count(entry => entry.RetentionClass.Equals("retain_30d", StringComparison.OrdinalIgnoreCase));
        var retain7dCount = uniqueEntries.Count(entry => entry.RetentionClass.Equals("retain_7d", StringComparison.OrdinalIgnoreCase));
        var deletableCount = uniqueEntries.Count(entry => entry.RetentionClass.Equals("deletable", StringComparison.OrdinalIgnoreCase));
        var estimatedReclaimableFiles = deletableCount;
        var estimatedReclaimableBytes = uniqueEntries
            .Where(entry => entry.RetentionClass.Equals("deletable", StringComparison.OrdinalIgnoreCase))
            .Sum(entry => EstimateSizeBytes(entry.Path));
        var reasons = uniqueEntries.Select(entry => entry.Reason).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).Take(20).ToList();
        var protectedPaths = uniqueEntries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ProtectedReason))
            .Select(entry => entry.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(50)
            .ToList();

        var report = new RetentionCleanupPreviewReport(
            ReportVersion: "retention_cleanup_preview_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            KeepCount: keepCount,
            Retain30dCount: retain30dCount,
            Retain7dCount: retain7dCount,
            DeletableCount: deletableCount,
            EstimatedReclaimableFiles: estimatedReclaimableFiles,
            EstimatedReclaimableBytes: estimatedReclaimableBytes,
            CandidatePaths: uniqueEntries,
            Reasons: reasons,
            ProtectedPaths: protectedPaths,
            OperatorSummary: BuildOperatorSummary(keepCount, retain30dCount, retain7dCount, deletableCount, uniqueEntries.Count),
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

    private RetentionCleanupPreviewEntry BuildEntry(string path, string reason, IReadOnlyList<string> protectedPaths, bool fromCurrentExport, bool fromEmbeddedPackage)
    {
        var normalizedPath = Path.GetFullPath(path);
        var ageDays = TryGetAgeDays(normalizedPath);
        var protectedReason = GetProtectedReason(normalizedPath, protectedPaths, fromCurrentExport, fromEmbeddedPackage);
        var retentionClass = DetermineRetentionClass(normalizedPath, reason, ageDays, protectedReason);

        return new RetentionCleanupPreviewEntry(
            Path: normalizedPath,
            RetentionClass: retentionClass,
            Reason: reason,
            AgeDays: ageDays,
            ProtectedReason: protectedReason,
            FromCurrentExport: fromCurrentExport,
            FromEmbeddedPackage: fromEmbeddedPackage);
    }

    private IEnumerable<RetentionCleanupPreviewEntry> ProtectedPreviewEntries(IReadOnlyList<string> protectedPaths)
    {
        var entries = new List<RetentionCleanupPreviewEntry>();

        foreach (var path in GetKnownProtectedPreviewPaths())
        {
            entries.Add(BuildEntry(path, "protected_path", protectedPaths, fromCurrentExport: IsCurrentExportPath(path), fromEmbeddedPackage: IsEmbeddedPackagePath(path)));
        }

        return entries;
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

    private static string GetProtectedReason(string path, IReadOnlyList<string> protectedPaths, bool fromCurrentExport, bool fromEmbeddedPackage)
    {
        if (fromCurrentExport)
        {
            return "current_export";
        }

        if (fromEmbeddedPackage)
        {
            return "current_embedded_package";
        }

        var fileName = Path.GetFileName(path);
        if (fileName.Contains("approved", StringComparison.OrdinalIgnoreCase))
        {
            return "approved";
        }

        if (fileName.Contains("promoted", StringComparison.OrdinalIgnoreCase))
        {
            return "promoted";
        }

        if (fileName.Contains("baseline", StringComparison.OrdinalIgnoreCase))
        {
            return "baseline";
        }

        if (fileName.Contains("audit", StringComparison.OrdinalIgnoreCase))
        {
            return "audit";
        }

        if (fileName.Contains("safety", StringComparison.OrdinalIgnoreCase))
        {
            return "safety";
        }

        if (fileName.Contains("handover", StringComparison.OrdinalIgnoreCase))
        {
            return "handover";
        }

        if (protectedPaths.Any(protectedPath => string.Equals(path, protectedPath, StringComparison.OrdinalIgnoreCase) || path.StartsWith(protectedPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
        {
            return "protected_path";
        }

        return string.Empty;
    }

    private static string DetermineRetentionClass(string path, string reason, double ageDays, string? protectedReason)
    {
        if (!string.IsNullOrWhiteSpace(protectedReason))
        {
            return "keep";
        }

        if (reason.Contains("audit", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("baseline", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("handover", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("approved", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("promoted", StringComparison.OrdinalIgnoreCase))
        {
            return "keep";
        }

        if ((IsSimulationReport(path) || reason.Contains("old_detailed_simulation_report_summary_exists", StringComparison.OrdinalIgnoreCase)) && ageDays > 30)
        {
            return "deletable";
        }

        if (IsTempDiagnostics(path) && ageDays > 14)
        {
            return "deletable";
        }

        if (IsFailedOrStaleIntermediate(path, reason) && ageDays > 7)
        {
            return "deletable";
        }

        if (ageDays > 90)
        {
            return "deletable";
        }

        if (ageDays > 30)
        {
            return "retain_30d";
        }

        if (ageDays > 7)
        {
            return "retain_7d";
        }

        return "keep";
    }

    private static bool IsSimulationReport(string path)
    {
        return path.Contains("simulation", StringComparison.OrdinalIgnoreCase)
            && path.Contains("report", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTempDiagnostics(string path)
    {
        return path.Contains("temp", StringComparison.OrdinalIgnoreCase)
            || path.Contains("diagnostic", StringComparison.OrdinalIgnoreCase)
            || path.Contains("debug", StringComparison.OrdinalIgnoreCase)
            || path.Contains("trace", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFailedOrStaleIntermediate(string path, string reason)
    {
        return reason.Contains("failed", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("stale", StringComparison.OrdinalIgnoreCase)
            || reason.Contains("intermediate", StringComparison.OrdinalIgnoreCase)
            || path.Contains("intermediate", StringComparison.OrdinalIgnoreCase)
            || path.Contains("stale", StringComparison.OrdinalIgnoreCase)
            || path.Contains("failed", StringComparison.OrdinalIgnoreCase);
    }

    private static long EstimateSizeBytes(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                return new FileInfo(path).Length;
            }

            if (Directory.Exists(path))
            {
                return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length);
            }
        }
        catch
        {
        }

        return 0L;
    }

    private static double TryGetAgeDays(string path)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                return 0d;
            }

            if (File.Exists(path))
            {
                return Math.Max(0d, (DateTimeOffset.UtcNow - new FileInfo(path).LastWriteTimeUtc).TotalDays);
            }

            return Math.Max(0d, (DateTimeOffset.UtcNow - new DirectoryInfo(path).LastWriteTimeUtc).TotalDays);
        }
        catch
        {
            return 0d;
        }
    }

    private IReadOnlyList<string> ReadManifestPaths(string manifestPath)
    {
        var paths = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = doc.RootElement;
            foreach (var property in new[] { "source_algo_path", "indexed_algo_path", "indexed_algo_metadata_path", "latest_algo_path", "latest_algo_metadata_path", "readiness_json_path", "readiness_markdown_path" })
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
            return paths;
        }

        return paths;
    }

    private static string BuildOperatorSummary(int keepCount, int retain30dCount, int retain7dCount, int deletableCount, int totalCount)
    {
        return $"Retention preview für {totalCount} bekannte Dateien/Artefakte: keep={keepCount}, retain_30d={retain30dCount}, retain_7d={retain7dCount}, deletable={deletableCount}. Keine Datei wurde gelöscht.";
    }

    private void WriteReport(RetentionCleanupPreviewReport report)
    {
        try
        {
            var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
            var markdown = BuildMarkdown(report);
            File.WriteAllText(ReportPath, json);
            File.WriteAllText(MarkdownPath, markdown);
            _resolvedReportPath = ReportPath;
            _resolvedMarkdownPath = MarkdownPath;
        }
        catch
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "retention_cleanup_preview");
            Directory.CreateDirectory(fallbackRoot);
            var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
            var markdown = BuildMarkdown(report);
            _resolvedReportPath = Path.Combine(fallbackRoot, "retention_cleanup_preview.json");
            _resolvedMarkdownPath = Path.Combine(fallbackRoot, "retention_cleanup_preview.md");
            File.WriteAllText(_resolvedReportPath, json);
            File.WriteAllText(_resolvedMarkdownPath, markdown);
        }
    }

    private static string BuildMarkdown(RetentionCleanupPreviewReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Retention Cleanup Preview");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- keep_count: {report.KeepCount}");
        sb.AppendLine($"- retain_30d_count: {report.Retain30dCount}");
        sb.AppendLine($"- retain_7d_count: {report.Retain7dCount}");
        sb.AppendLine($"- deletable_count: {report.DeletableCount}");
        sb.AppendLine($"- estimated_reclaimable_files: {report.EstimatedReclaimableFiles}");
        sb.AppendLine($"- estimated_reclaimable_bytes: {report.EstimatedReclaimableBytes}");
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

    private string GetExportManifestPath()
    {
        var wsl = Path.Combine(Path.DirectorySeparatorChar.ToString(), "mnt", "d", "Bot", "ctrader_export_manifest.json");
        if (File.Exists(wsl))
        {
            return wsl;
        }

        var local = Path.Combine("D:\\Bot", "ctrader_export_manifest.json");
        return local;
    }
}
