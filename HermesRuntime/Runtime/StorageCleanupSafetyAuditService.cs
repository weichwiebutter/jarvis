using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record StorageCleanupSafetyGroup(
    string GroupId,
    string Title,
    int FileCount,
    long EstimatedBytes,
    string Risk,
    bool AutomaticallySafe,
    bool ManuallyRecommended,
    IReadOnlyList<string> ExamplePaths);

public sealed record StorageCleanupSafetyAuditReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    double FreeDiskGb,
    double FreeDiskPercent,
    double DiskUsagePercent,
    int CleanupCandidates,
    long EstimatedFreeBytes,
    int ProtectedPathsCount,
    bool AutoCleanupPolicyEnabled,
    bool AutoCleanupAllowed,
    IReadOnlyList<StorageCleanupSafetyGroup> Groups,
    IReadOnlyList<string> ProtectedPaths,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string SafetySummary,
    bool FrankRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class StorageCleanupSafetyAuditService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public StorageCleanupSafetyAuditService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "storage_cleanup");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "storage_cleanup_safety_audit.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "storage_cleanup_safety_audit.md");

    public StorageCleanupSafetyAuditReport Run()
    {
        Directory.CreateDirectory(Root);
        var hygiene = new StorageHygieneService(_storagePaths);
        var plan = hygiene.LoadPlan() ?? hygiene.BuildPlan();
        var status = hygiene.LoadStatus() ?? hygiene.BuildStatus();
        var resource = new ResourceGuard(_storagePaths).Check();
        var groups = GroupCandidates(plan);
        var report = new StorageCleanupSafetyAuditReport(
            ReportVersion: "storage_cleanup_safety_audit_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            FreeDiskGb: Math.Round(resource.FreeDiskMb / 1024.0, 2),
            FreeDiskPercent: status.FreeDiskPercent,
            DiskUsagePercent: status.DiskUsagePercent,
            CleanupCandidates: status.CleanupCandidates,
            EstimatedFreeBytes: status.EstimatedFreeBytes,
            ProtectedPathsCount: status.ProtectedPathsCount,
            AutoCleanupPolicyEnabled: status.AutoCleanupPolicyEnabled,
            AutoCleanupAllowed: status.AutoCleanupAllowed,
            Groups: groups,
            ProtectedPaths: plan.ProtectedPaths,
            Warnings: status.Warnings
                .Concat(groups.Count > 0 ? ["cleanup_candidates_grouped"] : [])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList(),
            OperatorSummary: BuildOperatorSummary(groups, status),
            SafetySummary: "Speicher knapp; Kandidaten wurden gruppiert; keine Datei wurde geloescht oder archiviert.",
            FrankRequired: false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteReport(report);
        return report;
    }

    private static IReadOnlyList<StorageCleanupSafetyGroup> GroupCandidates(CleanupPlan plan)
    {
        var groups = new[]
        {
            BuildGroup("logs", "Logs", plan.Candidates, ["log", ".log", "trace", "debug"], "low", automaticallySafe: true, manuallyRecommended: true),
            BuildGroup("reports", "Alte Reports", plan.Candidates, ["report", ".json", ".md"], "low", automaticallySafe: true, manuallyRecommended: true),
            BuildGroup("snapshots", "Alte Snapshots", plan.Candidates, ["snapshot", "checkpoint"], "medium", automaticallySafe: false, manuallyRecommended: true),
            BuildGroup("temp", "Temporäre Dateien", plan.Candidates, ["temp", "tmp", ".tmp"], "low", automaticallySafe: true, manuallyRecommended: true),
            BuildGroup("build", "Build-Artefakte", plan.Candidates, ["bin", "obj", "build"], "medium", automaticallySafe: true, manuallyRecommended: true),
            BuildGroup("codex", "Alte .codex_artifacts", plan.Candidates, [".codex_artifacts"], "medium", automaticallySafe: false, manuallyRecommended: true),
            new StorageCleanupSafetyGroup("protected", "Sicher geschützte Pfade", plan.ProtectedPaths.Count, 0, "high", false, true, plan.ProtectedPaths.Take(10).ToList()),
        };

        return groups
            .OrderByDescending(group => group.FileCount)
            .ThenBy(group => group.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static StorageCleanupSafetyGroup BuildGroup(
        string groupId,
        string title,
        IReadOnlyList<CleanupCandidate> candidates,
        IReadOnlyList<string> tokens,
        string risk,
        bool automaticallySafe,
        bool manuallyRecommended)
    {
        var matched = candidates.Where(candidate =>
                tokens.Any(token =>
                    candidate.Path.Contains(token, StringComparison.OrdinalIgnoreCase)
                    || candidate.Reason.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var bytes = matched.Sum(candidate => candidate.EstimatedBytes);
        var examples = matched.Select(candidate => candidate.Path).Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToList();

        return new StorageCleanupSafetyGroup(
            GroupId: groupId,
            Title: title,
            FileCount: matched.Count,
            EstimatedBytes: bytes,
            Risk: risk,
            AutomaticallySafe: automaticallySafe && matched.Count > 0,
            ManuallyRecommended: manuallyRecommended,
            ExamplePaths: examples);
    }

    private static string BuildOperatorSummary(IReadOnlyList<StorageCleanupSafetyGroup> groups, StorageStatusSnapshot status)
    {
        var removable = groups.Where(group => group.AutomaticallySafe).Sum(group => group.FileCount);
        var protectedCount = groups.FirstOrDefault(group => group.GroupId == "protected")?.FileCount ?? status.ProtectedPathsCount;
        return $"Speicher knapp. {removable} Dateien könnten später sicher entfernt werden. {protectedCount} Dateien brauchen Schutz. Frank nötig: optional.";
    }

    private void WriteReport(StorageCleanupSafetyAuditReport report)
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
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "storage_cleanup");
            Directory.CreateDirectory(fallbackRoot);
            var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
            var markdown = BuildMarkdown(report);
            _resolvedReportPath = Path.Combine(fallbackRoot, "storage_cleanup_safety_audit.json");
            _resolvedMarkdownPath = Path.Combine(fallbackRoot, "storage_cleanup_safety_audit.md");
            File.WriteAllText(_resolvedReportPath, json);
            File.WriteAllText(_resolvedMarkdownPath, markdown);
        }
    }

    private static string BuildMarkdown(StorageCleanupSafetyAuditReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Storage Cleanup Safety Audit");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Cleanup candidates: {report.CleanupCandidates}");
        sb.AppendLine($"- Estimated free bytes: {report.EstimatedFreeBytes}");
        sb.AppendLine($"- Protected paths: {report.ProtectedPathsCount}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Groups");
        foreach (var group in report.Groups)
        {
            sb.AppendLine($"- {group.Title}: {group.FileCount} files · {group.EstimatedBytes} bytes · risk={group.Risk} · safe={group.AutomaticallySafe}");
        }
        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine(report.SafetySummary);
        return sb.ToString();
    }
}
