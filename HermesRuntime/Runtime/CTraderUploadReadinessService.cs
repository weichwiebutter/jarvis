using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record CTraderUploadReadinessReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    bool AlgoExists,
    bool AlgoMetadataExists,
    bool AlgoProjectBuildPass,
    bool EmbeddedPackagePresent,
    bool RuntimeSelfCheckReady,
    bool PaperRuntimeWired,
    bool TimerLoopWired,
    bool BrokerActionNone,
    bool PaperOnly,
    string AlgoPath,
    string AlgoMetadataPath,
    string AlgoProjectPath,
    string RuntimeSelfCheckReportPath,
    string PaperRuntimeStepReportPath,
    string DiagnosticsReportPath,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Recommendations,
    string ReportPath,
    string MarkdownPath);

public sealed class CTraderUploadReadinessService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public CTraderUploadReadinessService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "ctrader_upload_readiness");
    public string ReportPath => Path.Combine(Root, "ctrader_upload_readiness.json");
    public string MarkdownPath => Path.Combine(Root, "ctrader_upload_readiness.md");

    public CTraderUploadReadinessReport Run()
    {
        Directory.CreateDirectory(Root);

        var algoProjectPath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot.AlgoProject", "HermesPaperBot.AlgoProject.csproj");
        var algoDir = Path.GetDirectoryName(algoProjectPath) ?? string.Empty;
        var algoPath = Path.Combine(algoDir, "bin", "Debug", "net6.0", "HermesPaperBot.algo");
        var algoMetadataPath = Path.Combine(algoDir, "bin", "Debug", "net6.0", "HermesPaperBot.algo.metadata");
        var diagnosticsService = new CbotBuildHandoffDiagnosticsService(_storagePaths, _runtimeRoot);
        var diagnostics = diagnosticsService.LoadLatestReport();
        var selfCheckService = new PaperBotRuntimeSelfCheckService(_storagePaths, _runtimeRoot);
        var selfCheck = selfCheckService.LoadLatestReport();
        var stepService = new PaperRuntimeStepService(_storagePaths, _runtimeRoot);
        var stepReport = stepService.LoadLatestReport();

        var algoExists = File.Exists(algoPath);
        var algoMetadataExists = File.Exists(algoMetadataPath);
        var algoProjectBuildPass = diagnostics.ProjectExists && diagnostics.AlgoArtifactExists && diagnostics.AlgoMetadataExists;
        var embeddedPackagePresent = selfCheck.EmbeddedReleasePackagePresent && selfCheck.EmbeddedReleasePackageParseable;
        var runtimeSelfCheckReady = selfCheck.RuntimeReady;
        var paperRuntimeWired = stepReport.RuntimeReady
            && stepReport.EmbeddedPackageLoaded
            && stepReport.SignalPackageLoaded
            && stepReport.ChartAnnotationSpecLoaded
            && stepReport.SafetyFlagsActive
            && stepReport.BrokerActionNone;
        var timerLoopWired = paperRuntimeWired && string.Equals(stepReport.Status, "ready", StringComparison.OrdinalIgnoreCase);
        var brokerActionNone = stepReport.BrokerActionNone;
        var paperOnly = stepReport.CloudMode;

        var warnings = new List<string>();
        if (!algoExists) warnings.Add("algo_artifact_missing");
        if (!algoMetadataExists) warnings.Add("algo_metadata_missing");
        if (!algoProjectBuildPass) warnings.Add("algo_project_build_not_confirmed");
        if (!embeddedPackagePresent) warnings.Add("embedded_package_not_present");
        if (!runtimeSelfCheckReady) warnings.Add("runtime_self_check_not_ready");
        if (!paperRuntimeWired) warnings.Add("paper_runtime_not_wired");
        if (!timerLoopWired) warnings.Add("timer_loop_not_wired");
        if (!brokerActionNone) warnings.Add("broker_action_not_none");
        if (!paperOnly) warnings.Add("paper_only_not_confirmed");

        var recommendations = new List<string>();
        if (!algoExists || !algoMetadataExists)
        {
            recommendations.Add("build HermesPaperBot.AlgoProject to produce HermesPaperBot.algo and HermesPaperBot.algo.metadata");
        }
        if (!runtimeSelfCheckReady)
        {
            recommendations.Add("run paperbot-runtime-self-check to refresh runtime readiness evidence");
        }
        if (!paperRuntimeWired || !timerLoopWired)
        {
            recommendations.Add("run paper-runtime-step to refresh the runtime step and timer loop evidence");
        }

        var ready = algoExists && algoMetadataExists && algoProjectBuildPass && embeddedPackagePresent && runtimeSelfCheckReady && paperRuntimeWired && timerLoopWired && brokerActionNone && paperOnly;
        var report = new CTraderUploadReadinessReport(
            ReportVersion: "ctrader_upload_readiness_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: ready ? "ready" : "partial",
            AlgoExists: algoExists,
            AlgoMetadataExists: algoMetadataExists,
            AlgoProjectBuildPass: algoProjectBuildPass,
            EmbeddedPackagePresent: embeddedPackagePresent,
            RuntimeSelfCheckReady: runtimeSelfCheckReady,
            PaperRuntimeWired: paperRuntimeWired,
            TimerLoopWired: timerLoopWired,
            BrokerActionNone: brokerActionNone,
            PaperOnly: paperOnly,
            AlgoPath: algoPath,
            AlgoMetadataPath: algoMetadataPath,
            AlgoProjectPath: algoProjectPath,
            RuntimeSelfCheckReportPath: selfCheckService.ReportPath,
            PaperRuntimeStepReportPath: stepService.ReportPath,
            DiagnosticsReportPath: diagnosticsService.ReportPath,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Recommendations: recommendations.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public CTraderUploadReadinessReport LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return Run();
        }

        try
        {
            var report = JsonSerializer.Deserialize<CTraderUploadReadinessReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
            return report ?? Run();
        }
        catch
        {
            return Run();
        }
    }

    private static string BuildMarkdown(CTraderUploadReadinessReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# cTrader Upload Readiness");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- algo_exists: {report.AlgoExists.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- algo_metadata_exists: {report.AlgoMetadataExists.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- algo_project_build_pass: {report.AlgoProjectBuildPass.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- embedded_package_present: {report.EmbeddedPackagePresent.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- runtime_self_check_ready: {report.RuntimeSelfCheckReady.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- paper_runtime_wired: {report.PaperRuntimeWired.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- timer_loop_wired: {report.TimerLoopWired.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- broker_action_none: {report.BrokerActionNone.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- paper_only: {report.PaperOnly.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- algo_path: {report.AlgoPath}");
        sb.AppendLine($"- algo_metadata_path: {report.AlgoMetadataPath}");
        sb.AppendLine($"- algo_project_path: {report.AlgoProjectPath}");
        sb.AppendLine($"- runtime_self_check_report_path: {report.RuntimeSelfCheckReportPath}");
        sb.AppendLine($"- paper_runtime_step_report_path: {report.PaperRuntimeStepReportPath}");
        sb.AppendLine($"- diagnostics_report_path: {report.DiagnosticsReportPath}");
        if (report.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
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
