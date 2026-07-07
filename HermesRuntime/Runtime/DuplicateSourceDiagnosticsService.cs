using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record DuplicateSourceFileEntry(
    string RelativePath,
    string RootPath,
    string AlgoProjectPath,
    bool ExistsInRoot,
    bool ExistsInAlgoProject,
    bool SameContent,
    string? RootSha256,
    string? AlgoProjectSha256,
    long? RootSizeBytes,
    long? AlgoProjectSizeBytes,
    bool CompiledByAlgoProject,
    string Risk,
    string Notes);

public sealed record DuplicateSourceDiagnosticsReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    string RootPath,
    string AlgoProjectPath,
    string ProjectPath,
    IReadOnlyList<DuplicateSourceFileEntry> DuplicateFiles,
    IReadOnlyList<DuplicateSourceFileEntry> DifferingFiles,
    IReadOnlyList<DuplicateSourceFileEntry> CompiledByAlgoProject,
    IReadOnlyList<DuplicateSourceFileEntry> NotCompiledByAlgoProject,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> Recommendations,
    string ReportPath,
    string MarkdownPath);

public sealed class DuplicateSourceDiagnosticsService
{
    private readonly string _runtimeRoot;

    public DuplicateSourceDiagnosticsService(string runtimeRoot)
    {
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "duplicate_source_diagnostics");
    public string ReportPath => Path.Combine(Root, "duplicate_source_diagnostics.json");
    public string MarkdownPath => Path.Combine(Root, "duplicate_source_diagnostics.md");

    public DuplicateSourceDiagnosticsReport LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return Run();
        }

        try
        {
            var report = JsonSerializer.Deserialize<DuplicateSourceDiagnosticsReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
            return report ?? Run();
        }
        catch
        {
            return Run();
        }
    }

    public DuplicateSourceDiagnosticsReport Run()
    {
        var rootPath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot");
        var algoProjectPath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot.AlgoProject");
        var projectPath = Path.Combine(algoProjectPath, "HermesPaperBot.AlgoProject.csproj");

        var rootFiles = EnumerateFiles(rootPath).ToDictionary(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase);
        var algoFiles = EnumerateFiles(algoProjectPath).ToDictionary(entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase);

        var duplicateFiles = new List<DuplicateSourceFileEntry>();
        var differingFiles = new List<DuplicateSourceFileEntry>();
        var compiledByAlgoProject = new List<DuplicateSourceFileEntry>();
        var notCompiledByAlgoProject = new List<DuplicateSourceFileEntry>();

        foreach (var relativePath in rootFiles.Keys.Intersect(algoFiles.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var rootFile = rootFiles[relativePath];
            var algoFile = algoFiles[relativePath];
            var sameContent = string.Equals(rootFile.Hash, algoFile.Hash, StringComparison.OrdinalIgnoreCase);
            var entry = new DuplicateSourceFileEntry(
                RelativePath: relativePath,
                RootPath: rootFile.FullPath,
                AlgoProjectPath: algoFile.FullPath,
                ExistsInRoot: true,
                ExistsInAlgoProject: true,
                SameContent: sameContent,
                RootSha256: rootFile.Hash,
                AlgoProjectSha256: algoFile.Hash,
                RootSizeBytes: rootFile.SizeBytes,
                AlgoProjectSizeBytes: algoFile.SizeBytes,
                CompiledByAlgoProject: true,
                Risk: sameContent ? "low" : "medium",
                Notes: sameContent ? "identical_source_copy" : "divergent_source_copy");
            duplicateFiles.Add(entry);
            compiledByAlgoProject.Add(entry);
            if (!sameContent)
            {
                differingFiles.Add(entry);
            }
        }

        foreach (var relativePath in rootFiles.Keys.Except(algoFiles.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var rootFile = rootFiles[relativePath];
            notCompiledByAlgoProject.Add(new DuplicateSourceFileEntry(
                RelativePath: relativePath,
                RootPath: rootFile.FullPath,
                AlgoProjectPath: string.Empty,
                ExistsInRoot: true,
                ExistsInAlgoProject: false,
                SameContent: false,
                RootSha256: rootFile.Hash,
                AlgoProjectSha256: null,
                RootSizeBytes: rootFile.SizeBytes,
                AlgoProjectSizeBytes: null,
                CompiledByAlgoProject: false,
                Risk: "low",
                Notes: "root_only_source_file"));
        }

        var risks = new List<string>();
        if (differingFiles.Count > 0)
        {
            risks.Add("duplicate_sources_with_different_contents");
        }
        if (duplicateFiles.Count > 0)
        {
            risks.Add("duplicate_source_tree_present");
        }
        if (notCompiledByAlgoProject.Count > 0)
        {
            risks.Add("root_sources_not_compiled_by_algo_project");
        }

        var recommendations = new List<string>
        {
            "Treat ctrader/HermesPaperBot.AlgoProject as the compiled source of truth for the cTrader export.",
            "Keep ctrader/HermesPaperBot as the editable source mirror only if synchronization is intentional.",
            "Prefer a single source tree or explicit generation step to reduce drift risk.",
        };
        if (differingFiles.Count > 0)
        {
            recommendations.Add("Resolve divergent duplicate files before future wrapper/runtime changes.");
        }

        var report = new DuplicateSourceDiagnosticsReport(
            ReportVersion: "duplicate_source_diagnostics_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: differingFiles.Count > 0 ? "warning" : duplicateFiles.Count > 0 ? "ok" : "partial",
            RootPath: rootPath,
            AlgoProjectPath: algoProjectPath,
            ProjectPath: projectPath,
            DuplicateFiles: duplicateFiles,
            DifferingFiles: differingFiles,
            CompiledByAlgoProject: compiledByAlgoProject,
            NotCompiledByAlgoProject: notCompiledByAlgoProject,
            Risks: risks,
            Recommendations: recommendations,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    private static IEnumerable<(string RelativePath, string FullPath, string Hash, long SizeBytes)> EnumerateFiles(string root)
    {
        if (!Directory.Exists(root))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string hash;
            long size;
            try
            {
                using var stream = File.OpenRead(file);
                hash = Convert.ToHexString(SHA256.HashData(stream));
                size = new FileInfo(file).Length;
            }
            catch
            {
                hash = "unavailable";
                size = 0;
            }

            yield return (Path.GetRelativePath(root, file).Replace('\\', '/'), file, hash, size);
        }
    }

    private static string BuildMarkdown(DuplicateSourceDiagnosticsReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# cTrader Duplicate Source Diagnostics");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- root_path: {report.RootPath}");
        sb.AppendLine($"- algo_project_path: {report.AlgoProjectPath}");
        sb.AppendLine($"- project_path: {report.ProjectPath}");
        sb.AppendLine($"- duplicate_files: {report.DuplicateFiles.Count}");
        sb.AppendLine($"- differing_files: {report.DifferingFiles.Count}");
        sb.AppendLine($"- compiled_by_algo_project: {report.CompiledByAlgoProject.Count}");
        sb.AppendLine($"- not_compiled_by_algo_project: {report.NotCompiledByAlgoProject.Count}");
        sb.AppendLine();
        sb.AppendLine("## Duplicate Files");
        foreach (var file in report.DuplicateFiles)
        {
            sb.AppendLine($"- {file.RelativePath} | same_content={file.SameContent.ToString().ToLowerInvariant()} | compiled_by_algo_project={file.CompiledByAlgoProject.ToString().ToLowerInvariant()}");
        }
        if (report.DifferingFiles.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Differing Files");
            foreach (var file in report.DifferingFiles)
            {
                sb.AppendLine($"- {file.RelativePath} | root_sha256={file.RootSha256} | algo_sha256={file.AlgoProjectSha256}");
            }
        }
        if (report.NotCompiledByAlgoProject.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Root-Only Files");
            foreach (var file in report.NotCompiledByAlgoProject)
            {
                sb.AppendLine($"- {file.RelativePath}");
            }
        }
        if (report.Risks.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Risks");
            foreach (var risk in report.Risks)
            {
                sb.AppendLine($"- {risk}");
            }
        }
        if (report.Recommendations.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Recommendations");
            foreach (var recommendation in report.Recommendations)
            {
                sb.AppendLine($"- {recommendation}");
            }
        }
        return sb.ToString();
    }
}
