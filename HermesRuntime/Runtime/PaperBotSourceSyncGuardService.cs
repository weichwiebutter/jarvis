using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record PaperBotSourceSyncGuardEntry(
    string RelativePath,
    bool RootExists,
    bool AlgoProjectExists,
    bool SameContent,
    string? RootSha256,
    string? AlgoProjectSha256,
    string Status);

public sealed record PaperBotSourceSyncGuardReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    IReadOnlyList<PaperBotSourceSyncGuardEntry> CriticalFiles,
    IReadOnlyList<string> DriftFiles,
    IReadOnlyList<string> MissingFiles,
    IReadOnlyList<string> Recommendations,
    string ReportPath,
    string MarkdownPath);

public sealed class PaperBotSourceSyncGuardService
{
    private readonly string _runtimeRoot;

    private static readonly string[] CriticalFiles =
    [
        "HermesPaperBot.cs",
        "HermesPaperBotCloudHost.cs",
        Path.Combine("Models", "CloudEmbeddedReleasePackage.cs"),
        Path.Combine("Services", "PaperDecisionEngine.cs"),
        Path.Combine("Services", "SessionFilter.cs"),
    ];

    public PaperBotSourceSyncGuardService(string runtimeRoot)
    {
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "paperbot_source_sync_guard");
    public string ReportPath => Path.Combine(Root, "paperbot_source_sync_guard.json");
    public string MarkdownPath => Path.Combine(Root, "paperbot_source_sync_guard.md");

    public PaperBotSourceSyncGuardReport LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return Run();
        }

        try
        {
            var report = JsonSerializer.Deserialize<PaperBotSourceSyncGuardReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
            return report ?? Run();
        }
        catch
        {
            return Run();
        }
    }

    public PaperBotSourceSyncGuardReport Run()
    {
        var rootPath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot");
        var algoPath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot.AlgoProject");

        var entries = new List<PaperBotSourceSyncGuardEntry>();
        var driftFiles = new List<string>();
        var missingFiles = new List<string>();

        foreach (var relativePath in CriticalFiles)
        {
            var rootFile = Path.Combine(rootPath, relativePath);
            var algoFile = Path.Combine(algoPath, relativePath);
            var rootExists = File.Exists(rootFile);
            var algoExists = File.Exists(algoFile);
            var sameContent = false;
            string? rootHash = null;
            string? algoHash = null;

            if (rootExists)
            {
                rootHash = Sha256(rootFile);
            }

            if (algoExists)
            {
                algoHash = Sha256(algoFile);
            }

            if (rootExists && algoExists)
            {
                sameContent = string.Equals(rootHash, algoHash, StringComparison.OrdinalIgnoreCase);
            }

            var status = !rootExists || !algoExists
                ? "missing"
                : sameContent ? "ready" : "drift";

            if (status == "missing")
            {
                missingFiles.Add(relativePath);
            }
            else if (status == "drift")
            {
                driftFiles.Add(relativePath);
            }

            entries.Add(new PaperBotSourceSyncGuardEntry(
                RelativePath: relativePath,
                RootExists: rootExists,
                AlgoProjectExists: algoExists,
                SameContent: sameContent,
                RootSha256: rootHash,
                AlgoProjectSha256: algoHash,
                Status: status));
        }

        var statusOverall = missingFiles.Count > 0 || driftFiles.Count > 0 ? "fail" : "ready";
        var recommendations = new List<string>();
        if (driftFiles.Count > 0)
        {
            recommendations.Add("Synchronize the critical root and AlgoProject source files before export.");
        }
        if (missingFiles.Count > 0)
        {
            recommendations.Add("Restore missing critical source files in both source trees.");
        }
        if (statusOverall == "ready")
        {
            recommendations.Add("Critical root and AlgoProject sources are aligned.");
        }

        var report = new PaperBotSourceSyncGuardReport(
            ReportVersion: "paperbot_source_sync_guard_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: statusOverall,
            CriticalFiles: entries,
            DriftFiles: driftFiles,
            MissingFiles: missingFiles,
            Recommendations: recommendations,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    private static string Sha256(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch
        {
            return "unavailable";
        }
    }

    private static string BuildMarkdown(PaperBotSourceSyncGuardReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# PaperBot Source Sync Guard");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine();
        sb.AppendLine("## Critical Files");
        foreach (var file in report.CriticalFiles)
        {
            sb.AppendLine($"- {file.RelativePath} | status={file.Status} | root_exists={file.RootExists.ToString().ToLowerInvariant()} | algo_project_exists={file.AlgoProjectExists.ToString().ToLowerInvariant()} | same_content={file.SameContent.ToString().ToLowerInvariant()}");
        }
        if (report.DriftFiles.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Drift Files");
            foreach (var item in report.DriftFiles)
            {
                sb.AppendLine($"- {item}");
            }
        }
        if (report.MissingFiles.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Missing Files");
            foreach (var item in report.MissingFiles)
            {
                sb.AppendLine($"- {item}");
            }
        }
        if (report.Recommendations.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Recommendations");
            foreach (var item in report.Recommendations)
            {
                sb.AppendLine($"- {item}");
            }
        }
        return sb.ToString();
    }
}
