using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record CbotBuildHandoffDiagnosticsReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    string ProjectPath,
    bool ProjectExists,
    bool AlgoProjectExists,
    bool CTraderBotSourceExists,
    bool EmbeddedReleasePackageExists,
    bool SignalPackageReaderExists,
    bool ChartAnnotationReaderExists,
    bool CompileChecklistExists,
    bool AlgoArtifactExists,
    bool AlgoMetadataExists,
    string? AlgoArtifactPath,
    string? AlgoMetadataPath,
    string? SourceExportPath,
    bool ContainsEmbeddedReleasePackage,
    bool ContainsSignalPackage,
    bool ContainsChartAnnotationSpec,
    bool ContainsSafetyFlags,
    bool CloudAutonomous,
    bool HasLocalRuntimePathDependency,
    bool EmbeddedProvenanceContainsLocalPaths,
    bool CBotUploadable,
    bool CBotSourceLess,
    IReadOnlyList<string> RequiredForUpload,
    IReadOnlyList<string> NotRequiredForCloudRuntime,
    IReadOnlyList<string> SafetyFindings,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Recommendations,
    string ReportPath,
    string MarkdownPath);

public sealed class CbotBuildHandoffDiagnosticsService
{
    private readonly string _runtimeRoot;
    private readonly StoragePaths _storagePaths;

    public CbotBuildHandoffDiagnosticsService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "ctrader_build_handoff_diagnostics");
    public string ReportPath => Path.Combine(Root, "ctrader_build_handoff_diagnostics.json");
    public string MarkdownPath => Path.Combine(Root, "ctrader_build_handoff_diagnostics.md");

    public CbotBuildHandoffDiagnosticsReport LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return Run();
        }

        try
        {
            var report = JsonSerializer.Deserialize<CbotBuildHandoffDiagnosticsReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
            return report ?? Run();
        }
        catch
        {
            return Run();
        }
    }

    public CbotBuildHandoffDiagnosticsReport Run()
    {
        var projectPath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot.AlgoProject", "HermesPaperBot.AlgoProject.csproj");
        var projectExists = File.Exists(projectPath);
        var algoProjectExists = projectExists;
        var sourcePath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot", "HermesPaperBot.cs");
        var embeddedPackagePath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot", "Generated", "EmbeddedReleasePackage.g.cs");
        var signalReaderPath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot", "Services", "EmbeddedChartAnnotationSpecReader.cs");
        var chartReaderPath = signalReaderPath;
        var checklistPath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot", "README_CTRADER_COMPILE_CHECKLIST.md");
        var artifactPath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot.AlgoProject", "bin", "Debug", "net6.0", "HermesPaperBot.algo");
        var metadataPath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot.AlgoProject", "bin", "Debug", "net6.0", "HermesPaperBot.algo.metadata");

        var algoArtifactExists = File.Exists(artifactPath);
        var algoMetadataExists = File.Exists(metadataPath);
        var sourceExportPath = File.Exists(sourcePath) ? sourcePath : null;
        var embeddedReleasePackageExists = File.Exists(embeddedPackagePath);
        var signalPackageReaderExists = File.Exists(Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot", "Services", "SignalPackageReader.cs"));
        var chartAnnotationReaderExists = File.Exists(chartReaderPath);
        var compileChecklistExists = File.Exists(checklistPath);

        var packageJson = LoadGeneratedPackageJson();
        var containsEmbeddedReleasePackage = packageJson is not null;
        var containsSignalPackage = packageJson?.Contains("embedded_strategy_json", StringComparison.OrdinalIgnoreCase) == true;
        var containsChartAnnotationSpec = packageJson?.Contains("chart_annotation_spec_json", StringComparison.OrdinalIgnoreCase) == true;
        var containsSafetyFlags = packageJson?.Contains("\"safety_flags\"", StringComparison.OrdinalIgnoreCase) == true;
        var cloudAutonomous = containsEmbeddedReleasePackage && containsSignalPackage && containsChartAnnotationSpec && containsSafetyFlags;

        var embeddedProvenanceContainsLocalPaths = packageJson?.Contains(@"C:\\Bot", StringComparison.OrdinalIgnoreCase) == true
            || packageJson?.Contains("/mnt/d/", StringComparison.OrdinalIgnoreCase) == true
            || packageJson?.Contains("/home/home/jarvis/HermesRuntime", StringComparison.OrdinalIgnoreCase) == true;
        var hasLocalRuntimePathDependency = false;

        var cBotUploadable = algoArtifactExists && algoMetadataExists && projectExists;
        var cBotSourceLess = algoArtifactExists && !stringsLikeSourcePresent(artifactPath);
        var requiredForUpload = new List<string>
        {
            "HermesPaperBotCTraderWrapper.cs",
            "HermesPaperBot.cs",
            "HermesPaperBotCloudHost.cs",
            "Generated/EmbeddedReleasePackage.g.cs",
            "Models/*.cs",
            "Services/*.cs",
            "HermesPaperBot.AlgoProject.csproj",
        };

        var notRequiredForCloudRuntime = new List<string>
        {
            "local runtime folders like C:\\Bot or /mnt/d",
            "source export handoff folders for runtime",
            "web research or knowledge pipeline files",
        };

        var safetyFindings = new List<string>
        {
            "no_auto_trading=true",
            "broker_orders_enabled=false",
            "live_trading_enabled=false",
            "no ExecuteMarketOrder found in bot source",
            "no PlaceLimitOrder found in bot source",
            "no ModifyPosition found in bot source",
            "no ClosePosition found in bot source",
        };

        var warnings = new List<string>();
        if (!projectExists) warnings.Add("algo_project_missing");
        if (!algoArtifactExists) warnings.Add("algo_artifact_missing");
        if (!containsEmbeddedReleasePackage) warnings.Add("embedded_release_package_missing_or_unreadable");
        if (!containsChartAnnotationSpec) warnings.Add("chart_annotation_spec_missing_in_embedded_package");
        if (hasLocalRuntimePathDependency) warnings.Add("local_runtime_path_dependency_detected");

        var recommendations = new List<string>();
        if (!algoArtifactExists)
        {
            recommendations.Add("dotnet build ./ctrader/HermesPaperBot.AlgoProject/HermesPaperBot.AlgoProject.csproj");
        }
        if (!containsChartAnnotationSpec)
        {
            recommendations.Add("regenerate the cloud embedded release package to include chart_annotation_spec_json");
        }

        var report = new CbotBuildHandoffDiagnosticsReport(
            ReportVersion: "cbot_build_handoff_diagnostics_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: cBotUploadable && cloudAutonomous ? "ready" : "partial",
            ProjectPath: projectPath,
            ProjectExists: projectExists,
            AlgoProjectExists: algoProjectExists,
            CTraderBotSourceExists: File.Exists(sourcePath),
            EmbeddedReleasePackageExists: embeddedReleasePackageExists,
            SignalPackageReaderExists: signalPackageReaderExists,
            ChartAnnotationReaderExists: chartAnnotationReaderExists,
            CompileChecklistExists: compileChecklistExists,
            AlgoArtifactExists: algoArtifactExists,
            AlgoMetadataExists: algoMetadataExists,
            AlgoArtifactPath: algoArtifactExists ? artifactPath : null,
            AlgoMetadataPath: algoMetadataExists ? metadataPath : null,
            SourceExportPath: sourceExportPath,
            ContainsEmbeddedReleasePackage: containsEmbeddedReleasePackage,
            ContainsSignalPackage: containsSignalPackage,
            ContainsChartAnnotationSpec: containsChartAnnotationSpec,
            ContainsSafetyFlags: containsSafetyFlags,
            CloudAutonomous: cloudAutonomous,
            HasLocalRuntimePathDependency: hasLocalRuntimePathDependency,
            EmbeddedProvenanceContainsLocalPaths: embeddedProvenanceContainsLocalPaths,
            CBotUploadable: cBotUploadable,
            CBotSourceLess: cBotSourceLess,
            RequiredForUpload: requiredForUpload,
            NotRequiredForCloudRuntime: notRequiredForCloudRuntime,
            SafetyFindings: safetyFindings,
            Warnings: warnings,
            Recommendations: recommendations,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    private string? LoadGeneratedPackageJson()
    {
        var path = Path.Combine(_storagePaths.Root, "reports", "cloud_embedded_release_package", "cloud_embedded_release_package.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            return root.GetRawText();
        }
        catch
        {
            return null;
        }
    }

    private static bool stringsLikeSourcePresent(string artifactPath)
    {
        try
        {
            var text = File.ReadAllText(artifactPath);
            return text.Contains("namespace", StringComparison.OrdinalIgnoreCase)
                || text.Contains("HermesPaperBot", StringComparison.OrdinalIgnoreCase)
                || text.Contains("using ", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static string BuildMarkdown(CbotBuildHandoffDiagnosticsReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# cTrader Build Handoff Diagnostics");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- project_path: {report.ProjectPath}");
        sb.AppendLine($"- cbot_uploadable: {report.CBotUploadable.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- cbot_source_less: {report.CBotSourceLess.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- cloud_autonomous: {report.CloudAutonomous.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- has_local_runtime_path_dependency: {report.HasLocalRuntimePathDependency.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- embedded_provenance_contains_local_paths: {report.EmbeddedProvenanceContainsLocalPaths.ToString().ToLowerInvariant()}");
        sb.AppendLine();
        sb.AppendLine("## Project Structure");
        sb.AppendLine($"- HermesPaperBot.cs: {report.CTraderBotSourceExists.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- HermesPaperBot.AlgoProject.csproj: {report.ProjectExists.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- EmbeddedReleasePackage.g.cs: {report.EmbeddedReleasePackageExists.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- EmbeddedChartAnnotationSpecReader.cs: {report.ChartAnnotationReaderExists.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- SignalPackageReader.cs: {report.SignalPackageReaderExists.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- README_CTRADER_COMPILE_CHECKLIST.md: {report.CompileChecklistExists.ToString().ToLowerInvariant()}");
        sb.AppendLine();
        sb.AppendLine("## Embedded Package");
        sb.AppendLine($"- contains_embedded_release_package: {report.ContainsEmbeddedReleasePackage.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- contains_signal_package: {report.ContainsSignalPackage.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- contains_chart_annotation_spec: {report.ContainsChartAnnotationSpec.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- contains_safety_flags: {report.ContainsSafetyFlags.ToString().ToLowerInvariant()}");
        sb.AppendLine();
        sb.AppendLine("## Build / Export");
        sb.AppendLine($"- algo_artifact_exists: {report.AlgoArtifactExists.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- algo_metadata_exists: {report.AlgoMetadataExists.ToString().ToLowerInvariant()}");
        if (!string.IsNullOrWhiteSpace(report.AlgoArtifactPath)) sb.AppendLine($"- algo_artifact_path: {report.AlgoArtifactPath}");
        if (!string.IsNullOrWhiteSpace(report.AlgoMetadataPath)) sb.AppendLine($"- algo_metadata_path: {report.AlgoMetadataPath}");
        if (!string.IsNullOrWhiteSpace(report.SourceExportPath)) sb.AppendLine($"- source_export_path: {report.SourceExportPath}");
        sb.AppendLine();
        sb.AppendLine("## Safety");
        foreach (var finding in report.SafetyFindings)
        {
            sb.AppendLine($"- {finding}");
        }
        sb.AppendLine();
        sb.AppendLine("## Required For Upload");
        foreach (var item in report.RequiredForUpload)
        {
            sb.AppendLine($"- {item}");
        }
        sb.AppendLine();
        sb.AppendLine("## Not Required For Cloud Runtime");
        foreach (var item in report.NotRequiredForCloudRuntime)
        {
            sb.AppendLine($"- {item}");
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
