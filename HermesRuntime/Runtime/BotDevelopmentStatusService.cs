using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record BotDevelopmentComponentStatus(
    string Name,
    string Status,
    string Readiness,
    string Summary,
    string? JsonPath,
    string? MarkdownPath,
    IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Recommendations);

public sealed record BotDevelopmentStatusReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string OverallStatus,
    IReadOnlyList<BotDevelopmentComponentStatus> Components,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Recommendations,
    bool ResearchOnly,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled,
    string ReportPath,
    string MarkdownPath);

public sealed class BotDevelopmentStatusService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public BotDevelopmentStatusService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "bot_development_status");
    public string ReportPath => Path.Combine(Root, "bot_development_status.json");
    public string MarkdownPath => Path.Combine(Root, "bot_development_status.md");

    public BotDevelopmentStatusReport LoadLatestReport()
    {
        if (File.Exists(ReportPath))
        {
            try
            {
                var report = JsonSerializer.Deserialize<BotDevelopmentStatusReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
                if (report is not null)
                {
                    return report;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
            }
        }

        return Run();
    }

    public BotDevelopmentStatusReport Run()
    {
        var components = new List<BotDevelopmentComponentStatus>
        {
            BuildCTraderBotExportStatus(),
            BuildChartAnnotationReadinessStatus(),
            BuildPaperBotRuntimeSelfCheckStatus(),
            BuildPaperBotStatus(),
            BuildForwardTestStatus(),
            BuildCurrentMarketSnapshotStatus(),
            BuildSignalAgentSpecsStatus(),
            BuildDemoSignalFeedStatus(),
            BuildEnsemblePackageStatus(),
            BuildHumanReviewGateStatus(),
        };

        var warnings = components.SelectMany(component => component.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var recommendations = BuildRecommendations(components).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var overallStatus = DetermineOverallStatus(components);

        var report = new BotDevelopmentStatusReport(
            ReportVersion: "bot_development_status_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            OverallStatus: overallStatus,
            Components: components,
            Warnings: warnings,
            Recommendations: recommendations,
            ResearchOnly: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    private BotDevelopmentComponentStatus BuildCTraderBotExportStatus()
    {
        var projectPath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot.AlgoProject", "HermesPaperBot.AlgoProject.csproj");
        var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;
        var artifactPath = Path.Combine(projectDir, "bin", "Debug", "net6.0", "HermesPaperBot.algo");
        var metadataPath = Path.Combine(projectDir, "bin", "Debug", "net6.0", "HermesPaperBot.algo.metadata");
        var sourcePath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot", "HermesPaperBotCTraderWrapper.cs");

        var projectExists = File.Exists(projectPath);
        var artifactExists = File.Exists(artifactPath);
        var metadataExists = File.Exists(metadataPath);
        var sourceExists = File.Exists(sourcePath);
        var readiness = projectExists && sourceExists && artifactExists ? "ready" : "needs_build";
        var status = artifactExists ? "export_ready" : projectExists ? "export_configured" : "missing";
        var warnings = new List<string>();
        if (!projectExists) warnings.Add("ctrader_algo_project_missing");
        if (!sourceExists) warnings.Add("ctrader_wrapper_missing");
        if (!artifactExists) warnings.Add("ctrader_algo_artifact_missing");

        var recommendations = new List<string>();
        if (!artifactExists)
        {
            recommendations.Add("dotnet build ./ctrader/HermesPaperBot.AlgoProject/HermesPaperBot.AlgoProject.csproj");
        }

        return new BotDevelopmentComponentStatus(
            Name: "cTrader Bot Export",
            Status: status,
            Readiness: readiness,
            Summary: artifactExists
                ? "HermesPaperBot.algo ist vorhanden und das Export-Projekt ist konfiguriert."
                : "Das Export-Projekt ist vorhanden, aber der .algo-Artefaktstatus ist nicht vollständig.",
            JsonPath: projectPath,
            MarkdownPath: artifactExists ? artifactPath : metadataPath,
            Evidence: new[]
            {
                $"project_exists={projectExists.ToString().ToLowerInvariant()}",
                $"wrapper_exists={sourceExists.ToString().ToLowerInvariant()}",
                $"algo_artifact_exists={artifactExists.ToString().ToLowerInvariant()}",
                $"algo_metadata_exists={metadataExists.ToString().ToLowerInvariant()}",
                $"artifact_path={artifactPath}",
            },
            Warnings: warnings,
            Recommendations: recommendations);
    }

    private BotDevelopmentComponentStatus BuildPaperBotStatus()
    {
        var root = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot");
        var algoProjectPath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot.AlgoProject", "HermesPaperBot.AlgoProject.csproj");
        var replayReportPath = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "hermes_paper_bot_replay", "replay_report.json");
        var replayReportMarkdownPath = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "hermes_paper_bot_replay", "replay_report.md");
        var keyFiles = new[]
        {
            Path.Combine(root, "HermesPaperBot.cs"),
            Path.Combine(root, "HermesPaperBotCloudHost.cs"),
            Path.Combine(root, "HermesPaperBotCTraderWrapper.cs"),
            Path.Combine(root, "Generated", "EmbeddedReleasePackage.g.cs"),
            Path.Combine(root, "Services", "PaperRuntimeOrchestrator.cs"),
            Path.Combine(root, "tests", "PaperRuntimeOrchestratorHarness.cs"),
            Path.Combine(root, "README.md"),
        };

        var presentFiles = keyFiles.Where(File.Exists).ToList();
        var missingFiles = keyFiles.Except(presentFiles).ToList();
        var algoArtifactPath = Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot.AlgoProject", "bin", "Debug", "net6.0", "HermesPaperBot.algo");
        var algoArtifactExists = File.Exists(algoArtifactPath);
        var replayReportExists = File.Exists(replayReportPath);
        var status = presentFiles.Count == keyFiles.Length && algoArtifactExists
            ? "ready"
            : presentFiles.Count > 0
                ? "partial"
                : "missing";
        var readiness = algoArtifactExists && presentFiles.Count == keyFiles.Length ? "paper_bot_ready" : "paper_bot_needs_attention";
        var warnings = new List<string>();
        if (missingFiles.Count > 0) warnings.Add($"missing_files:{string.Join(",", missingFiles.Select(Path.GetFileName))}");
        if (!algoArtifactExists) warnings.Add("paperbot_algo_artifact_missing");
        if (!replayReportExists) warnings.Add("paperbot_replay_report_missing");

        var recommendations = new List<string>();
        if (!algoArtifactExists)
        {
            recommendations.Add("build HermesPaperBot.AlgoProject to refresh the cTrader export");
        }

        return new BotDevelopmentComponentStatus(
            Name: "PaperBot",
            Status: status,
            Readiness: readiness,
            Summary: algoArtifactExists && missingFiles.Count == 0
                ? "PaperBot-Quellpfad, Wrapper, Cloud-Host, Generated Package und Replay-Report sind vorhanden."
                : "PaperBot-Artefakte sind teilweise vorhanden; mindestens ein Kernartefakt fehlt oder der .algo-Export fehlt.",
            JsonPath: algoProjectPath,
            MarkdownPath: replayReportExists ? replayReportMarkdownPath : null,
            Evidence: new[]
            {
                $"source_files_present={presentFiles.Count}/{keyFiles.Length}",
                $"algo_project_exists={File.Exists(algoProjectPath).ToString().ToLowerInvariant()}",
                $"algo_artifact_exists={algoArtifactExists.ToString().ToLowerInvariant()}",
                $"replay_report_exists={replayReportExists.ToString().ToLowerInvariant()}",
            }
            .Concat(presentFiles.Select(file => $"file_present={Path.GetFileName(file)}"))
            .ToList(),
            Warnings: warnings,
            Recommendations: recommendations);
    }

    private BotDevelopmentComponentStatus BuildPaperBotRuntimeSelfCheckStatus()
    {
        var selfCheckService = new PaperBotRuntimeSelfCheckService(_storagePaths, _runtimeRoot);
        var report = selfCheckService.LoadLatestReport();

        return new BotDevelopmentComponentStatus(
            Name: "PaperBot Runtime Self Check",
            Status: report.RuntimeReady ? "ready" : "not_ready",
            Readiness: report.RuntimeReady ? "embedded_ready" : "missing_embedded_spec",
            Summary: report.RuntimeReady
                ? "Embedded Release Package, Signal Package, Chart Annotation Spec, Safety Flags und Cloud Mode sind bestätigt."
                : "Der PaperBot Runtime Self Check meldet fehlende oder unvollständige Embedded-Artefakte.",
            JsonPath: selfCheckService.ReportPath,
            MarkdownPath: selfCheckService.MarkdownPath,
            Evidence: new[]
            {
                $"embedded_release_package_present={report.EmbeddedReleasePackagePresent.ToString().ToLowerInvariant()}",
                $"embedded_release_package_parseable={report.EmbeddedReleasePackageParseable.ToString().ToLowerInvariant()}",
                $"signal_package_loaded={report.SignalPackageLoaded.ToString().ToLowerInvariant()}",
                $"chart_annotation_spec_loaded={report.ChartAnnotationSpecLoaded.ToString().ToLowerInvariant()}",
                $"safety_flags_active={report.SafetyFlagsActive.ToString().ToLowerInvariant()}",
                $"cloud_mode={report.CloudMode.ToString().ToLowerInvariant()}",
                $"broker_action_none={report.BrokerActionNone.ToString().ToLowerInvariant()}",
                $"runtime_ready={report.RuntimeReady.ToString().ToLowerInvariant()}",
            },
            Warnings: report.Warnings.ToList(),
            Recommendations: report.Recommendations.ToList());
    }

    private BotDevelopmentComponentStatus BuildChartAnnotationReadinessStatus()
    {
        var chartService = new ChartAnnotationExportService(_storagePaths, _runtimeRoot);
        var report = chartService.LoadLatestReport();
        var embeddedPackagePath = Path.Combine(_storagePaths.Root, "reports", "cloud_embedded_release_package", "cloud_embedded_release_package.json");
        var embeddedPackageContainsSpec = false;
        string? localExportPath = File.Exists(chartService.ReportPath) ? chartService.ReportPath : null;
        string? localExportMarkdownPath = File.Exists(chartService.MarkdownPath) ? chartService.MarkdownPath : null;

        try
        {
            if (File.Exists(embeddedPackagePath))
            {
                using var document = JsonDocument.Parse(File.ReadAllText(embeddedPackagePath));
                var root = document.RootElement;
                embeddedPackageContainsSpec = root.TryGetProperty("chart_annotation_spec_json", out var spec) && spec.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(spec.GetString());
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
        }

        var readerAvailable = File.Exists(Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot", "Services", "EmbeddedChartAnnotationSpecReader.cs"));
        var sourceMode = report.SourceMode;
        var embeddedSpecAvailable = report.EmbeddedSpecAvailable || embeddedPackageContainsSpec;

        var readiness = embeddedSpecAvailable && readerAvailable
            ? "embedded_ready"
            : embeddedSpecAvailable
                ? "local_only"
                : localExportPath is not null
                    ? "missing_embedded_spec"
                    : "not_ready";

        var status = readiness switch
        {
            "embedded_ready" => "ready",
            "local_only" => "partial",
            "missing_embedded_spec" => "partial",
            _ => "missing",
        };

        var warnings = new List<string>();
        if (!embeddedSpecAvailable)
        {
            warnings.Add("chart_annotation_spec_missing");
        }
        if (!readerAvailable)
        {
            warnings.Add("chart_annotation_reader_missing");
        }
        if (string.Equals(readiness, "local_only", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add("chart_annotation_embedded_spec_missing_but_local_export_exists");
        }

        var recommendations = new List<string>();
        if (!embeddedSpecAvailable)
        {
            recommendations.Add("regenerate the cloud embedded release package with chart annotation spec embedded");
        }

        return new BotDevelopmentComponentStatus(
            Name: "Chart Annotation Readiness",
            Status: status,
            Readiness: readiness,
            Summary: embeddedSpecAvailable && readerAvailable
                ? "Chart Annotation Spec ist im Embedded Release Package enthalten und der Cloud-Reader ist vorhanden."
                : localExportPath is not null
                    ? "Lokaler Chart-Annotation-Export existiert, aber die eingebettete Spec ist nicht vollständig nachweisbar."
                    : "Chart Annotation Spec ist noch nicht vollständig eingebettet oder nicht nachweisbar.",
            JsonPath: embeddedPackagePath,
            MarkdownPath: localExportMarkdownPath,
            Evidence: new[]
            {
                $"chart_annotation_spec_status={readiness}",
                $"source_mode={sourceMode}",
                $"embedded_spec_available={embeddedSpecAvailable.ToString().ToLowerInvariant()}",
                $"embedded_release_package_contains_chart_annotations={embeddedPackageContainsSpec.ToString().ToLowerInvariant()}",
                $"cloud_bot_annotation_reader_available={readerAvailable.ToString().ToLowerInvariant()}",
                $"local_export_path={(localExportPath ?? "-")}",
            },
            Warnings: warnings,
            Recommendations: recommendations);
    }

    private BotDevelopmentComponentStatus BuildForwardTestStatus()
    {
        var service = new ForwardTestService(_storagePaths, _runtimeRoot);
        var status = service.LoadStatus();
        var warnings = new List<string>();
        var recommendations = new List<string>();

        if (status is null)
        {
            warnings.Add("forward_test_status_missing");
            recommendations.Add("generate or load the forward test status for a current snapshot");
            return new BotDevelopmentComponentStatus(
                Name: "ForwardTestService",
                Status: "missing",
                Readiness: "forward_test_missing",
                Summary: "Kein gespeicherter Forward-Test-Status gefunden.",
                JsonPath: service.StatusPath,
                MarkdownPath: service.PlanMarkdownPath,
                Evidence: new[]
                {
                    $"status_path_exists={File.Exists(service.StatusPath).ToString().ToLowerInvariant()}",
                    $"plan_path_exists={File.Exists(service.PlanPath).ToString().ToLowerInvariant()}",
                    $"log_path_exists={File.Exists(service.LogPath).ToString().ToLowerInvariant()}",
                },
                Warnings: warnings,
                Recommendations: recommendations);
        }

        if (status.Blockers.Count > 0)
        {
            warnings.AddRange(status.Blockers.Select(blocker => $"forward_test_blocker:{blocker}"));
            recommendations.Add("review forward test blockers before using the feed in bot development");
        }

        return new BotDevelopmentComponentStatus(
            Name: "ForwardTestService",
            Status: status.ForwardTestStatus,
            Readiness: status.ForwardTestStatus,
            Summary: $"Forward-Test-Modus={status.ForwardTestMode}; Health={status.ForwardTestHealth}; Beobachtungen={status.ForwardTestObservationsTotal}.",
            JsonPath: service.StatusPath,
            MarkdownPath: service.PlanMarkdownPath,
            Evidence: new[]
            {
                $"status={status.ForwardTestStatus}",
                $"mode={status.ForwardTestMode}",
                $"health={status.ForwardTestHealth}",
                $"signals_observed={status.ForwardTestSignalsObserved}",
                $"observations_total={status.ForwardTestObservationsTotal}",
                $"triggered_count={status.ForwardTestTriggeredCount}",
                $"invalidated_count={status.ForwardTestInvalidatedCount}",
            },
            Warnings: warnings.Concat(status.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Recommendations: recommendations);
    }

    private BotDevelopmentComponentStatus BuildCurrentMarketSnapshotStatus()
    {
        var service = new CurrentMarketSnapshotService(_storagePaths, _runtimeRoot);
        var status = service.LoadStatus();
        if (status is null)
        {
            return new BotDevelopmentComponentStatus(
                Name: "CurrentMarketSnapshotService",
                Status: "missing",
                Readiness: "market_snapshot_missing",
                Summary: "Kein gespeicherter Market Snapshot vorhanden.",
                JsonPath: service.StatusPath,
                MarkdownPath: service.SnapshotMarkdownPath,
                Evidence: new[]
                {
                    $"status_path_exists={File.Exists(service.StatusPath).ToString().ToLowerInvariant()}",
                    $"snapshot_path_exists={File.Exists(service.SnapshotJsonPath).ToString().ToLowerInvariant()}",
                    $"markdown_path_exists={File.Exists(service.SnapshotMarkdownPath).ToString().ToLowerInvariant()}",
                },
                Warnings: new[] { "current_market_snapshot_missing" },
                Recommendations: new[] { "refresh the current market snapshot if current read-only quotes are required" }
        );
        }

        var warnings = status.Warnings.ToList();
        if (!string.Equals(status.SnapshotStatus, "available", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"market_snapshot_status:{status.SnapshotStatus}");
        }

        return new BotDevelopmentComponentStatus(
            Name: "CurrentMarketSnapshotService",
            Status: status.SnapshotStatus,
            Readiness: status.SnapshotHealth,
            Summary: $"Assets verfügbar: {string.Join(", ", status.AssetsAvailable)}; Health={status.SnapshotHealth}.",
            JsonPath: service.StatusPath,
            MarkdownPath: service.SnapshotMarkdownPath,
            Evidence: new[]
            {
                $"snapshot_status={status.SnapshotStatus}",
                $"snapshot_health={status.SnapshotHealth}",
                $"assets_requested={string.Join(",", status.AssetsRequested)}",
                $"assets_available={string.Join(",", status.AssetsAvailable)}",
                $"latest_update_utc={status.LatestUpdateUtc?.ToString("O") ?? "-"}",
            },
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Recommendations: status.SnapshotStatus == "available"
                ? []
                : ["refresh the current market snapshot"]);
    }

    private BotDevelopmentComponentStatus BuildSignalAgentSpecsStatus()
    {
        var researchService = new ScalpingResearchService(_storagePaths, _runtimeRoot);
        var roots = new[]
        {
            researchService.SignalSpecDirectory,
            Path.Combine(_storagePaths.Root, "reports", "signal_agent_specs"),
            Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "signal_agent_specs")
        }
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Where(Directory.Exists)
        .ToList();

        var specs = roots
            .SelectMany(root => Directory.GetFiles(root, "signal_agent_spec.json", SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        var latest = specs.FirstOrDefault();
        var warnings = new List<string>();
        var recommendations = new List<string>();
        if (specs.Count == 0)
        {
            warnings.Add("signal_agent_specs_missing");
            recommendations.Add("export signal agent specs for the active research candidates");
        }

        return new BotDevelopmentComponentStatus(
            Name: "Signal Agent Specs",
            Status: specs.Count > 0 ? "ready" : "missing",
            Readiness: specs.Count > 0 ? "signal_agent_specs_ready" : "signal_agent_specs_missing",
            Summary: $"Gefundene Signal-Agent-Specs: {specs.Count}.",
            JsonPath: latest,
            MarkdownPath: latest is null ? null : Path.ChangeExtension(latest, ".md"),
            Evidence: new[]
            {
                $"spec_count={specs.Count}",
                $"latest_spec={latest ?? "-"}",
                $"signal_spec_directory={researchService.SignalSpecDirectory}",
            }
            .Concat(roots.Select(root => $"search_root={root}"))
            .ToList(),
            Warnings: warnings,
            Recommendations: recommendations);
    }

    private BotDevelopmentComponentStatus BuildDemoSignalFeedStatus()
    {
        var service = new DemoSignalFeedService(_storagePaths, _runtimeRoot);
        var status = service.LoadStatus();
        if (status is null)
        {
            return new BotDevelopmentComponentStatus(
                Name: "Demo Signal Feed",
                Status: "missing",
                Readiness: "demo_signal_feed_missing",
                Summary: "Kein gespeicherter Demo-Signal-Feed-Status vorhanden.",
                JsonPath: service.StatusPath,
                MarkdownPath: service.LatestSignalsMarkdownPath,
                Evidence: new[]
                {
                    $"status_path_exists={File.Exists(service.StatusPath).ToString().ToLowerInvariant()}",
                    $"signals_path_exists={File.Exists(service.LatestSignalsJsonPath).ToString().ToLowerInvariant()}",
                },
                Warnings: ["demo_signal_feed_missing"],
                Recommendations: ["generate the demo signal feed once the ensemble review gate is satisfied"]);
        }

        var warnings = status.Warnings.ToList();
        if (status.Blockers.Count > 0)
        {
            warnings.AddRange(status.Blockers.Select(blocker => $"demo_signal_feed_blocker:{blocker}"));
        }

        return new BotDevelopmentComponentStatus(
            Name: "Demo Signal Feed",
            Status: status.FeedStatus,
            Readiness: status.FeedMode,
            Summary: $"FeedMode={status.FeedMode}; Signale={status.SignalCount}; Verfügbar={status.DemoSignalsAvailable}.",
            JsonPath: service.StatusPath,
            MarkdownPath: service.LatestSignalsMarkdownPath,
            Evidence: new[]
            {
                $"feed_status={status.FeedStatus}",
                $"feed_mode={status.FeedMode}",
                $"signal_count={status.SignalCount}",
                $"demo_signals_available={status.DemoSignalsAvailable.ToString().ToLowerInvariant()}",
                $"ensemble_review_status={status.EnsembleReviewStatus}",
            },
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Recommendations: status.DemoSignalsAvailable ? [] : ["resolve the ensemble review gate before regenerating demo signals"]);
    }

    private BotDevelopmentComponentStatus BuildEnsemblePackageStatus()
    {
        var service = new ScalpingEnsemblePortfolioService(_storagePaths, _runtimeRoot);
        var package = service.LoadPackage();
        if (package is null)
        {
            return new BotDevelopmentComponentStatus(
                Name: "Ensemble Package",
                Status: "missing",
                Readiness: "ensemble_package_missing",
                Summary: "Kein Ensemble-Package gefunden.",
                JsonPath: service.PackagePath,
                MarkdownPath: service.PackageMarkdownPath,
                Evidence: new[]
                {
                    $"package_path_exists={File.Exists(service.PackagePath).ToString().ToLowerInvariant()}",
                    $"markdown_path_exists={File.Exists(service.PackageMarkdownPath).ToString().ToLowerInvariant()}",
                },
                Warnings: ["ensemble_package_missing"],
                Recommendations: ["build the ensemble package from the current portfolio report"]);
        }

        return new BotDevelopmentComponentStatus(
            Name: "Ensemble Package",
            Status: package.Status,
            Readiness: package.Status,
            Summary: $"PackageId={package.PackageId}; Assets={package.Assets.Count}; Status={package.Status}.",
            JsonPath: service.PackagePath,
            MarkdownPath: service.PackageMarkdownPath,
            Evidence: new[]
            {
                $"package_id={package.PackageId}",
                $"package_version={package.PackageVersion}",
                $"status={package.Status}",
                $"assets={package.Assets.Count}",
                $"research_only={package.ResearchOnly.ToString().ToLowerInvariant()}",
            },
            Warnings: package.Assets.Count == 0 ? ["ensemble_package_without_assets"] : [],
            Recommendations: package.Status.Equals("portfolio_ready", StringComparison.OrdinalIgnoreCase)
                ? []
                : ["review the ensemble package readiness before demo or forward-test usage"]);
    }

    private BotDevelopmentComponentStatus BuildHumanReviewGateStatus()
    {
        var service = new ScalpingEnsembleReviewService(_storagePaths, _runtimeRoot);
        var state = service.LoadState();
        if (state is null)
        {
            return new BotDevelopmentComponentStatus(
                Name: "Human Review Gate",
                Status: "missing",
                Readiness: "human_review_missing",
                Summary: "Kein Ensemble-Review-Status vorhanden.",
                JsonPath: service.StatusPath,
                MarkdownPath: service.StatusMarkdownPath,
                Evidence: new[]
                {
                    $"status_path_exists={File.Exists(service.StatusPath).ToString().ToLowerInvariant()}",
                    $"review_log_exists={File.Exists(service.LogPath).ToString().ToLowerInvariant()}",
                },
                Warnings: ["human_review_gate_missing"],
                Recommendations: ["load or create the ensemble review state before any demo signal use"]);
        }

        var status = state.ReviewStatus.ToString();
        var readiness = state.ReviewStatus == ScalpingEnsembleReviewStatus.approved_for_demo_signal_use
            || state.ReviewStatus == ScalpingEnsembleReviewStatus.approved_for_forward_test_preparation
            ? "approved"
            : "needs_review";

        var recommendations = new List<string>();
        if (state.ReviewStatus == ScalpingEnsembleReviewStatus.pending_human_review)
        {
            recommendations.Add("perform human review on the ensemble package before demo/forward-test use");
        }

        return new BotDevelopmentComponentStatus(
            Name: "Human Review Gate",
            Status: status,
            Readiness: readiness,
            Summary: $"ReviewMode={state.ReviewMode ?? "-"}; ReviewStatus={state.ReviewStatus}; PackageStatus={state.PackageStatus}.",
            JsonPath: service.StatusPath,
            MarkdownPath: service.StatusMarkdownPath,
            Evidence: new[]
            {
                $"review_status={state.ReviewStatus}",
                $"review_mode={state.ReviewMode ?? "-"}",
                $"package_id={state.PackageId}",
                $"package_status={state.PackageStatus}",
                $"blockers={string.Join(",", state.Blockers)}",
            },
            Warnings: state.Blockers.ToList(),
            Recommendations: recommendations);
    }

    private static string DetermineOverallStatus(IReadOnlyList<BotDevelopmentComponentStatus> components)
    {
        if (components.Count == 0)
        {
            return "missing";
        }

        if (components.All(component => component.Status is "ready" or "export_ready" or "approved"))
        {
            return "ready";
        }

        if (components.Any(component => component.Status is "missing" or "blocked"))
        {
            return "blocked";
        }

        return "partial";
    }

    private static IReadOnlyList<string> BuildRecommendations(IReadOnlyList<BotDevelopmentComponentStatus> components)
    {
        var recommendations = new List<string>();
        foreach (var component in components)
        {
            recommendations.AddRange(component.Recommendations);
        }

        if (components.Any(component => component.Name == "Human Review Gate" && component.Readiness != "approved"))
        {
            recommendations.Add("complete human review for the ensemble package before any demo or forward-test usage");
        }

        if (components.Any(component => component.Name == "ForwardTestService" && component.Status == "missing"))
        {
            recommendations.Add("initialize the forward test plan/observations if forward-test diagnostics are needed");
        }

        return recommendations;
    }

    private static string BuildMarkdown(BotDevelopmentStatusReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Hermes Bot Development Status");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- overall_status: {report.OverallStatus}");
        sb.AppendLine($"- research_only: {report.ResearchOnly.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- no_auto_trading: {report.NoAutoTrading.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- human_review_required: {report.HumanReviewRequired.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- broker_orders_enabled: {report.BrokerOrdersEnabled.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- live_trading_enabled: {report.LiveTradingEnabled.ToString().ToLowerInvariant()}");
        sb.AppendLine();

        foreach (var component in report.Components)
        {
            sb.AppendLine($"## {component.Name}");
            sb.AppendLine($"- status: {component.Status}");
            sb.AppendLine($"- readiness: {component.Readiness}");
            sb.AppendLine($"- summary: {component.Summary}");
            if (!string.IsNullOrWhiteSpace(component.JsonPath))
            {
                sb.AppendLine($"- json_path: {component.JsonPath}");
            }
            if (!string.IsNullOrWhiteSpace(component.MarkdownPath))
            {
                sb.AppendLine($"- markdown_path: {component.MarkdownPath}");
            }
            sb.AppendLine("- evidence:");
            foreach (var item in component.Evidence)
            {
                sb.AppendLine($"  - {item}");
            }

            sb.AppendLine("- warnings:");
            foreach (var warning in component.Warnings.DefaultIfEmpty("none"))
            {
                sb.AppendLine($"  - {warning}");
            }

            sb.AppendLine("- recommendations:");
            foreach (var recommendation in component.Recommendations.DefaultIfEmpty("none"))
            {
                sb.AppendLine($"  - {recommendation}");
            }

            sb.AppendLine();
        }

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine("## Global Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }

            sb.AppendLine();
        }

        if (report.Recommendations.Count > 0)
        {
            sb.AppendLine("## Recommendations");
            foreach (var recommendation in report.Recommendations)
            {
                sb.AppendLine($"- {recommendation}");
            }
        }

        return sb.ToString();
    }
}
