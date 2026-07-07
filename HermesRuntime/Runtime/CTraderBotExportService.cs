using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record CTraderBotExportReport(
    string ExportRoot,
    string AlgoSourcePath,
    string AlgoMetadataSourcePath,
    string ReadinessJsonSourcePath,
    string ReadinessMarkdownSourcePath,
    string AlgoTargetPath,
    string AlgoMetadataTargetPath,
    string ReadinessJsonTargetPath,
    string ReadinessMarkdownTargetPath,
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

    public CTraderBotExportService(string runtimeRoot)
    {
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "ctrader_bot_export");
    public string ReportPath => Path.Combine(Root, "ctrader_bot_export.json");
    public string MarkdownPath => Path.Combine(Root, "ctrader_bot_export.md");

    public CTraderBotExportReport Run()
    {
        Directory.CreateDirectory(Root);

        var algoProjectDirectory = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot.AlgoProject", "bin", "Debug", "net6.0");
        var algoSourcePath = Path.Combine(algoProjectDirectory, "HermesPaperBot.algo");
        var algoMetadataSourcePath = Path.Combine(algoProjectDirectory, "HermesPaperBot.algo.metadata");
        var readinessJsonSourcePath = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "ctrader_upload_readiness", "ctrader_upload_readiness.json");
        var readinessMarkdownSourcePath = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "ctrader_upload_readiness", "ctrader_upload_readiness.md");
        var readinessReport = LoadReadinessReport(readinessJsonSourcePath);

        var exportRoot = ResolveExportRoot();
        Directory.CreateDirectory(exportRoot);
        var exportRootCreated = true;

        var algoTargetPath = Path.Combine(exportRoot, "HermesPaperBot.algo");
        var algoMetadataTargetPath = Path.Combine(exportRoot, "HermesPaperBot.algo.metadata");
        var readinessJsonTargetPath = Path.Combine(exportRoot, "ctrader_upload_readiness.json");
        var readinessMarkdownTargetPath = Path.Combine(exportRoot, "ctrader_upload_readiness.md");

        var missingSources = new List<string>();
        var warnings = new List<string>();

        var algoCopied = CopyIfExists(algoSourcePath, algoTargetPath, missingSources, "HermesPaperBot.algo");
        var algoMetadataCopied = CopyIfExists(algoMetadataSourcePath, algoMetadataTargetPath, missingSources, "HermesPaperBot.algo.metadata");
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

        var report = new CTraderBotExportReport(
            ExportRoot: exportRoot,
            AlgoSourcePath: algoSourcePath,
            AlgoMetadataSourcePath: algoMetadataSourcePath,
            ReadinessJsonSourcePath: readinessJsonSourcePath,
            ReadinessMarkdownSourcePath: readinessMarkdownSourcePath,
            AlgoTargetPath: algoTargetPath,
            AlgoMetadataTargetPath: algoMetadataTargetPath,
            ReadinessJsonTargetPath: readinessJsonTargetPath,
            ReadinessMarkdownTargetPath: readinessMarkdownTargetPath,
            ExportRootCreated: exportRootCreated,
            AlgoCopied: algoCopied,
            AlgoMetadataCopied: algoMetadataCopied,
            ReadinessJsonCopied: readinessJsonCopied,
            ReadinessMarkdownCopied: readinessMarkdownCopied,
            MissingSources: missingSources,
            Warnings: warnings,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: status);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
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
        sb.AppendLine($"- algo_source_path: {report.AlgoSourcePath}");
        sb.AppendLine($"- algo_metadata_source_path: {report.AlgoMetadataSourcePath}");
        sb.AppendLine($"- readiness_json_source_path: {report.ReadinessJsonSourcePath}");
        sb.AppendLine($"- readiness_markdown_source_path: {report.ReadinessMarkdownSourcePath}");
        sb.AppendLine($"- algo_target_path: {report.AlgoTargetPath}");
        sb.AppendLine($"- algo_metadata_target_path: {report.AlgoMetadataTargetPath}");
        sb.AppendLine($"- readiness_json_target_path: {report.ReadinessJsonTargetPath}");
        sb.AppendLine($"- readiness_markdown_target_path: {report.ReadinessMarkdownTargetPath}");
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
