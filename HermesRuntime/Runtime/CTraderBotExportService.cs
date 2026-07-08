using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

namespace Hermes.Runtime;

public sealed record CTraderBotExportReport(
    string ExportId,
    DateTimeOffset TimestampUtc,
    string ExportRoot,
    string AlgoSourcePath,
    string AlgoMetadataSourcePath,
    string ReadinessJsonSourcePath,
    string ReadinessMarkdownSourcePath,
    string AlgoTargetPath,
    string AlgoMetadataTargetPath,
    string IndexedAlgoPath,
    string IndexedAlgoMetadataPath,
    string LatestAlgoPath,
    string LatestAlgoMetadataPath,
    string ReadinessJsonTargetPath,
    string ReadinessMarkdownTargetPath,
    string ManifestPath,
    string BuildStamp,
    long FileSizeBytes,
    string Sha256,
    bool ExportRootCreated,
    bool AlgoCopied,
    bool AlgoMetadataCopied,
    bool ReadinessJsonCopied,
    bool ReadinessMarkdownCopied,
    IReadOnlyList<string> MissingSources,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath,
    DateTimeOffset UpdatedAtUtc,
    string Status);

public sealed class CTraderBotExportService
{
    private readonly string _runtimeRoot;
    private readonly string _storageRoot;

    public CTraderBotExportService(string runtimeRoot, string storageRoot)
    {
        _runtimeRoot = runtimeRoot;
        _storageRoot = storageRoot;
    }

    public string Root => Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "ctrader_bot_export");
    public string ReportPath => Path.Combine(Root, "ctrader_bot_export.json");
    public string MarkdownPath => Path.Combine(Root, "ctrader_bot_export.md");

    public CTraderBotExportReport Run()
    {
        Directory.CreateDirectory(Root);

        var embeddedPackageGenerator = new CloudEmbeddedReleasePackageGeneratorService(BuildStoragePaths(_storageRoot), _runtimeRoot);
        var embeddedPackageGeneration = embeddedPackageGenerator.Generate();
        var algoProjectBuild = BuildAlgoProject();
        var timestampUtc = DateTimeOffset.UtcNow;
        var exportId = timestampUtc.ToString("yyyyMMdd_HHmmss");
        var buildStamp = "20260707_timer_diag_v2";

        var algoProjectDirectory = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot.AlgoProject", "bin", "Debug", "net6.0");
        var algoSourcePath = Path.Combine(algoProjectDirectory, "HermesPaperBot.algo");
        var algoMetadataSourcePath = Path.Combine(algoProjectDirectory, "HermesPaperBot.algo.metadata");
        var readinessJsonSourcePath = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "ctrader_upload_readiness", "ctrader_upload_readiness.json");
        var readinessMarkdownSourcePath = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "ctrader_upload_readiness", "ctrader_upload_readiness.md");
        var readinessReport = LoadReadinessReport(readinessJsonSourcePath);

        var exportRoot = ResolveExportRoot();
        Directory.CreateDirectory(exportRoot);
        var exportRootCreated = true;

        var indexedAlgoPath = Path.Combine(exportRoot, $"HermesPaperBot_{exportId}.algo");
        var indexedAlgoMetadataPath = Path.Combine(exportRoot, $"HermesPaperBot_{exportId}.algo.metadata");
        var latestAlgoPath = Path.Combine(exportRoot, "HermesPaperBot_latest.algo");
        var latestAlgoMetadataPath = Path.Combine(exportRoot, "HermesPaperBot_latest.algo.metadata");
        var algoTargetPath = Path.Combine(exportRoot, "HermesPaperBot.algo");
        var algoMetadataTargetPath = Path.Combine(exportRoot, "HermesPaperBot.algo.metadata");
        var readinessJsonTargetPath = Path.Combine(exportRoot, "ctrader_upload_readiness.json");
        var readinessMarkdownTargetPath = Path.Combine(exportRoot, "ctrader_upload_readiness.md");
        var manifestPath = Path.Combine(exportRoot, "ctrader_export_manifest.json");

        var missingSources = new List<string>();
        var warnings = new List<string>();

        if (!string.Equals(embeddedPackageGeneration.Status, "generated", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"embedded_release_package_generation_{embeddedPackageGeneration.Status}");
        }
        if (!algoProjectBuild.Success)
        {
            warnings.AddRange(algoProjectBuild.Warnings);
            warnings.Add("algo_project_build_failed");
        }

        if (!algoProjectBuild.Success)
        {
            var failureReport = new CTraderBotExportReport(
                ExportId: exportId,
                TimestampUtc: timestampUtc,
                ExportRoot: exportRoot,
                AlgoSourcePath: algoSourcePath,
                AlgoMetadataSourcePath: algoMetadataSourcePath,
                ReadinessJsonSourcePath: readinessJsonSourcePath,
                ReadinessMarkdownSourcePath: readinessMarkdownSourcePath,
                AlgoTargetPath: Path.Combine(exportRoot, "HermesPaperBot.algo"),
                AlgoMetadataTargetPath: Path.Combine(exportRoot, "HermesPaperBot.algo.metadata"),
                IndexedAlgoPath: Path.Combine(exportRoot, $"HermesPaperBot_{exportId}.algo"),
                IndexedAlgoMetadataPath: Path.Combine(exportRoot, $"HermesPaperBot_{exportId}.algo.metadata"),
                LatestAlgoPath: Path.Combine(exportRoot, "HermesPaperBot_latest.algo"),
                LatestAlgoMetadataPath: Path.Combine(exportRoot, "HermesPaperBot_latest.algo.metadata"),
                ReadinessJsonTargetPath: Path.Combine(exportRoot, "ctrader_upload_readiness.json"),
                ReadinessMarkdownTargetPath: Path.Combine(exportRoot, "ctrader_upload_readiness.md"),
                ManifestPath: Path.Combine(exportRoot, "ctrader_export_manifest.json"),
                BuildStamp: buildStamp,
                FileSizeBytes: 0L,
                Sha256: string.Empty,
                ExportRootCreated: exportRootCreated,
                AlgoCopied: false,
                AlgoMetadataCopied: false,
                ReadinessJsonCopied: false,
                ReadinessMarkdownCopied: false,
                MissingSources: ["HermesPaperBot.algo_build_failed"],
                Warnings: warnings,
                ReportPath: ReportPath,
                MarkdownPath: MarkdownPath,
                UpdatedAtUtc: timestampUtc,
                Status: "partial");

            File.WriteAllText(ReportPath, JsonSerializer.Serialize(failureReport, JsonDefaults.WriteOptions));
            File.WriteAllText(MarkdownPath, BuildMarkdown(failureReport));
            return failureReport;
        }

        var algoCopied = CopyIfExists(algoSourcePath, algoTargetPath, missingSources, "HermesPaperBot.algo")
            & CopyIfExists(algoSourcePath, indexedAlgoPath, missingSources, $"HermesPaperBot_{exportId}.algo")
            & CopyIfExists(algoSourcePath, latestAlgoPath, missingSources, "HermesPaperBot_latest.algo");
        var algoMetadataCopied = CopyIfExists(algoMetadataSourcePath, algoMetadataTargetPath, missingSources, "HermesPaperBot.algo.metadata")
            & CopyIfExists(algoMetadataSourcePath, indexedAlgoMetadataPath, missingSources, $"HermesPaperBot_{exportId}.algo.metadata")
            & CopyIfExists(algoMetadataSourcePath, latestAlgoMetadataPath, missingSources, "HermesPaperBot_latest.algo.metadata");
        var readinessJsonCopied = CopyIfExists(readinessJsonSourcePath, readinessJsonTargetPath, missingSources, "ctrader_upload_readiness.json");
        var readinessMarkdownCopied = CopyIfExists(readinessMarkdownSourcePath, readinessMarkdownTargetPath, missingSources, "ctrader_upload_readiness.md");

        if (!readinessReport.AlgoExists || !readinessReport.AlgoMetadataExists)
        {
            warnings.Add("readiness_report_indicates_incomplete_algo_artifacts");
        }
        if (!readinessReport.RuntimeSelfCheckReady || !readinessReport.PaperRuntimeWired || !readinessReport.TimerLoopWired)
        {
            warnings.Add("readiness_report_indicates_partial_runtime_readiness");
        }
        if (!readinessReport.BrokerActionNone || !readinessReport.PaperOnly)
        {
            warnings.Add("readiness_report_indicates_safety_flags_issue");
        }

        var status = missingSources.Count == 0 && algoCopied && algoMetadataCopied && readinessJsonCopied && readinessMarkdownCopied
            ? "exported"
            : "partial";

        var fileSizeBytes = File.Exists(indexedAlgoPath) ? new FileInfo(indexedAlgoPath).Length : 0L;
        var sha256 = File.Exists(indexedAlgoPath) ? ComputeSha256(indexedAlgoPath) : string.Empty;
        var manifest = new
        {
            export_id = exportId,
            timestamp = timestampUtc,
            source_algo_path = algoSourcePath,
            indexed_algo_path = indexedAlgoPath,
            indexed_algo_metadata_path = indexedAlgoMetadataPath,
            latest_algo_path = latestAlgoPath,
            latest_algo_metadata_path = latestAlgoMetadataPath,
            build_stamp = buildStamp,
            file_size = fileSizeBytes,
            sha256,
            readiness_json_path = readinessJsonTargetPath,
            readiness_markdown_path = readinessMarkdownTargetPath,
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonDefaults.WriteOptions));

        var report = new CTraderBotExportReport(
            ExportId: exportId,
            TimestampUtc: timestampUtc,
            ExportRoot: exportRoot,
            AlgoSourcePath: algoSourcePath,
            AlgoMetadataSourcePath: algoMetadataSourcePath,
            ReadinessJsonSourcePath: readinessJsonSourcePath,
            ReadinessMarkdownSourcePath: readinessMarkdownSourcePath,
            AlgoTargetPath: algoTargetPath,
            AlgoMetadataTargetPath: algoMetadataTargetPath,
            IndexedAlgoPath: indexedAlgoPath,
            IndexedAlgoMetadataPath: indexedAlgoMetadataPath,
            LatestAlgoPath: latestAlgoPath,
            LatestAlgoMetadataPath: latestAlgoMetadataPath,
            ReadinessJsonTargetPath: readinessJsonTargetPath,
            ReadinessMarkdownTargetPath: readinessMarkdownTargetPath,
            ManifestPath: manifestPath,
            BuildStamp: buildStamp,
            FileSizeBytes: fileSizeBytes,
            Sha256: sha256,
            ExportRootCreated: exportRootCreated,
            AlgoCopied: algoCopied,
            AlgoMetadataCopied: algoMetadataCopied,
            ReadinessJsonCopied: readinessJsonCopied,
            ReadinessMarkdownCopied: readinessMarkdownCopied,
            MissingSources: missingSources,
            Warnings: warnings,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            UpdatedAtUtc: timestampUtc,
            Status: status);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    private BuildOutcome BuildAlgoProject()
    {
        var started = DateTimeOffset.UtcNow;
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build ./ctrader/HermesPaperBot.AlgoProject/HermesPaperBot.AlgoProject.csproj",
            WorkingDirectory = _runtimeRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                return new BuildOutcome(false, 0, ["failed_to_start_algo_project_build"]);
            }

            var output = new StringBuilder();
            var error = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) error.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var timeout = TimeSpan.FromMinutes(2);
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                TryKill(process);
                return new BuildOutcome(false, (long)timeout.TotalMilliseconds, ["algo_project_build_timeout"])
                {
                    OutputPath = WriteBuildOutput(output.ToString(), error.ToString())
                };
            }

            var duration = Math.Max(0, (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds);
            var succeeded = process.ExitCode == 0;
            var warnings = new List<string>();
            if (!succeeded)
            {
                warnings.Add("algo_project_build_failed");
            }

            return new BuildOutcome(succeeded, duration, warnings)
            {
                OutputPath = WriteBuildOutput(output.ToString(), error.ToString())
            };
        }
        catch (Exception ex)
        {
            return new BuildOutcome(false, 0, [$"algo_project_build_exception:{ex.GetType().Name}"]);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // defensive no-op
        }
    }

    private string WriteBuildOutput(string standardOutput, string standardError)
    {
        var outputRoot = Path.Combine(Root, "build-output");
        Directory.CreateDirectory(outputRoot);
        var filePath = Path.Combine(outputRoot, $"algo_project_build_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmssfff}.log");
        File.WriteAllText(filePath, string.Join(Environment.NewLine, new[]
        {
            standardOutput,
            string.Empty,
            standardError,
        }));
        return filePath;
    }

    private sealed record BuildOutcome(bool Success, long DurationMs, IReadOnlyList<string> Warnings)
    {
        public string? OutputPath { get; init; }
    }

    private static bool CopyIfExists(string sourcePath, string targetPath, ICollection<string> missingSources, string label)
    {
        if (!File.Exists(sourcePath))
        {
            missingSources.Add(label);
            return false;
        }

        File.Copy(sourcePath, targetPath, overwrite: true);
        return true;
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ResolveExportRoot()
    {
        var wslDrive = Path.Combine(Path.DirectorySeparatorChar.ToString(), "mnt", "d", "Bot");
        if (Directory.Exists(Path.Combine(Path.DirectorySeparatorChar.ToString(), "mnt", "d")))
        {
            return wslDrive;
        }

        if (OperatingSystem.IsWindows())
        {
            return @"D:\Bot";
        }

        return wslDrive;
    }

    private static StoragePaths BuildStoragePaths(string root)
    {
        var normalizedRoot = Path.GetFullPath(root);
        return new StoragePaths(
            normalizedRoot,
            Path.Combine(normalizedRoot, "events"),
            Path.Combine(normalizedRoot, "snapshots"),
            Path.Combine(normalizedRoot, "logs"),
            Path.Combine(normalizedRoot, "cache"),
            Path.Combine(normalizedRoot, "jobs"),
            Path.Combine(normalizedRoot, "archive"));
    }

    private static CTraderUploadReadinessReport LoadReadinessReport(string readinessJsonSourcePath)
    {
        if (!File.Exists(readinessJsonSourcePath))
        {
            return new CTraderUploadReadinessReport(
                ReportVersion: "ctrader_upload_readiness_v1",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Status: "missing",
                AlgoExists: false,
                AlgoMetadataExists: false,
                AlgoProjectBuildPass: false,
                EmbeddedPackagePresent: false,
                RuntimeSelfCheckReady: false,
                PaperRuntimeWired: false,
                TimerLoopWired: false,
                BrokerActionNone: false,
                PaperOnly: false,
                AlgoPath: string.Empty,
                AlgoMetadataPath: string.Empty,
                AlgoProjectPath: string.Empty,
                RuntimeSelfCheckReportPath: string.Empty,
                PaperRuntimeStepReportPath: string.Empty,
                DiagnosticsReportPath: string.Empty,
                Warnings: ["readiness_report_missing"],
                Recommendations: ["run ctrader-upload-readiness first"],
                ReportPath: readinessJsonSourcePath,
                MarkdownPath: readinessJsonSourcePath.Replace(".json", ".md", StringComparison.OrdinalIgnoreCase));
        }

        try
        {
            var report = JsonSerializer.Deserialize<CTraderUploadReadinessReport>(File.ReadAllText(readinessJsonSourcePath), JsonDefaults.SnapshotReadOptions);
            return report ?? throw new InvalidOperationException("readiness_report_parse_failed");
        }
        catch
        {
            return new CTraderUploadReadinessReport(
                ReportVersion: "ctrader_upload_readiness_v1",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Status: "invalid",
                AlgoExists: false,
                AlgoMetadataExists: false,
                AlgoProjectBuildPass: false,
                EmbeddedPackagePresent: false,
                RuntimeSelfCheckReady: false,
                PaperRuntimeWired: false,
                TimerLoopWired: false,
                BrokerActionNone: false,
                PaperOnly: false,
                AlgoPath: string.Empty,
                AlgoMetadataPath: string.Empty,
                AlgoProjectPath: string.Empty,
                RuntimeSelfCheckReportPath: string.Empty,
                PaperRuntimeStepReportPath: string.Empty,
                DiagnosticsReportPath: string.Empty,
                Warnings: ["readiness_report_parse_failed"],
                Recommendations: ["run ctrader-upload-readiness again"],
                ReportPath: readinessJsonSourcePath,
                MarkdownPath: readinessJsonSourcePath.Replace(".json", ".md", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static string BuildMarkdown(CTraderBotExportReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# cTrader Bot Export");
        sb.AppendLine();
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- export_root: {report.ExportRoot}");
        sb.AppendLine($"- export_id: {report.ExportId}");
        sb.AppendLine($"- timestamp_utc: {report.TimestampUtc:O}");
        sb.AppendLine($"- algo_source_path: {report.AlgoSourcePath}");
        sb.AppendLine($"- algo_metadata_source_path: {report.AlgoMetadataSourcePath}");
        sb.AppendLine($"- readiness_json_source_path: {report.ReadinessJsonSourcePath}");
        sb.AppendLine($"- readiness_markdown_source_path: {report.ReadinessMarkdownSourcePath}");
        sb.AppendLine($"- algo_target_path: {report.AlgoTargetPath}");
        sb.AppendLine($"- algo_metadata_target_path: {report.AlgoMetadataTargetPath}");
        sb.AppendLine($"- indexed_algo_path: {report.IndexedAlgoPath}");
        sb.AppendLine($"- indexed_algo_metadata_path: {report.IndexedAlgoMetadataPath}");
        sb.AppendLine($"- latest_algo_path: {report.LatestAlgoPath}");
        sb.AppendLine($"- latest_algo_metadata_path: {report.LatestAlgoMetadataPath}");
        sb.AppendLine($"- readiness_json_target_path: {report.ReadinessJsonTargetPath}");
        sb.AppendLine($"- readiness_markdown_target_path: {report.ReadinessMarkdownTargetPath}");
        sb.AppendLine($"- manifest_path: {report.ManifestPath}");
        sb.AppendLine($"- build_stamp: {report.BuildStamp}");
        sb.AppendLine($"- file_size_bytes: {report.FileSizeBytes}");
        sb.AppendLine($"- sha256: {report.Sha256}");
        sb.AppendLine($"- export_root_created: {report.ExportRootCreated.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- algo_copied: {report.AlgoCopied.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- algo_metadata_copied: {report.AlgoMetadataCopied.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- readiness_json_copied: {report.ReadinessJsonCopied.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- readiness_markdown_copied: {report.ReadinessMarkdownCopied.ToString().ToLowerInvariant()}");
        if (report.MissingSources.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Missing Sources");
            foreach (var missing in report.MissingSources)
            {
                sb.AppendLine($"- {missing}");
            }
        }
        if (report.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
        }

        return sb.ToString();
    }
}
