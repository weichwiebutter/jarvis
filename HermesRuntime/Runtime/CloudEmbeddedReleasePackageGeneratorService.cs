using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record CloudEmbeddedReleasePackageGenerationResult(
    string SourceBundleDirectory,
    string OutputDirectory,
    string OutputJsonPath,
    string OutputMarkdownPath,
    string OutputSourcePath,
    string Status,
    string Reason,
    string BotReleaseId,
    string BotVersion,
    string StrategyPackageVersion,
    string SchemaVersion,
    string ReleaseMode,
    string EmbeddedChecksum,
    bool Success);

public sealed class CloudEmbeddedReleasePackageGeneratorService
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly string[] ForbiddenCapabilities =
    [
        "execute_market_order",
        "place_limit_order",
        "place_stop_order",
        "modify_position",
        "close_position",
        "cancel_pending_order",
        "position_management",
        "pending_order_management",
        "account_risk_mutation",
        "strategy_mutation",
        "backtesting",
        "oos_execution",
        "forward_learning",
        "release_manifest_mutation",
        "safety_flag_mutation",
        "external_network_calls",
        "secrets_access",
    ];

    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedOutputDirectory;

    public CloudEmbeddedReleasePackageGeneratorService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string OutputDirectory => _resolvedOutputDirectory ??= ResolveOutputDirectory();
    public string OutputJsonPath => Path.Combine(OutputDirectory, "cloud_embedded_release_package.json");
    public string OutputMarkdownPath => Path.Combine(OutputDirectory, "cloud_embedded_release_package.md");
    public string OutputSourcePath => Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot", "Generated", "EmbeddedReleasePackage.g.cs");

    public CloudEmbeddedReleasePackageGenerationResult Generate(string? sourceBundleDirectory = null)
    {
        var sourceDirectory = ResolveSourceBundleDirectory(sourceBundleDirectory);
        if (sourceDirectory is null)
        {
            return BuildFailure("source_bundle_missing", "missing_source_bundle", string.Empty);
        }

        var manifestPath = Path.Combine(sourceDirectory, "bundle-manifest.json");
        var packagePath = Path.Combine(sourceDirectory, "ensemble_signal_agent_package.json");
        var schemaPath = Path.Combine(sourceDirectory, "ensemble_signal_agent_package.schema.json");
        var contractPath = Path.Combine(sourceDirectory, "system_b_signal_agent_export_contract.md");
        var signalEvaluation = new PaperSignalEvaluationService(_storagePaths, _runtimeRoot).LoadLatestReport();

        if (!File.Exists(manifestPath) || !File.Exists(packagePath) || !File.Exists(schemaPath))
        {
            return BuildFailure("source_bundle_incomplete", "required_source_files_missing", sourceDirectory);
        }

        var bundleManifest = JsonSerializer.Deserialize<SystemBHandoffBundleManifest>(File.ReadAllText(manifestPath), ReadOptions);
        var sourcePackage = JsonSerializer.Deserialize<EnsembleSignalAgentPortfolioPackage>(File.ReadAllText(packagePath), ReadOptions);
        if (bundleManifest is null || sourcePackage is null)
        {
            return BuildFailure("source_bundle_invalid", "json_parse_failed", sourceDirectory);
        }

        if (!bundleManifest.NoAutoTrading || !bundleManifest.HumanReviewRequired || bundleManifest.BrokerOrdersEnabled || bundleManifest.LiveTradingEnabled || !bundleManifest.ResearchOnly)
        {
            return BuildFailure("source_bundle_safety_invalid", "source_safety_flags_invalid", sourceDirectory);
        }

        if (!sourcePackage.NoAutoTrading || !sourcePackage.HumanReviewRequired || sourcePackage.BrokerOrdersEnabled || sourcePackage.LiveTradingEnabled || !sourcePackage.ResearchOnly)
        {
            return BuildFailure("source_package_safety_invalid", "package_safety_flags_invalid", sourceDirectory);
        }

        var embeddedManifestJson = BuildEmbeddedManifestJson(bundleManifest, sourcePackage, File.Exists(contractPath) ? File.ReadAllText(contractPath) : string.Empty);
        var embeddedStrategyJson = BuildEmbeddedStrategySnapshotJson(bundleManifest, sourcePackage);
        var embeddedChartAnnotationSpecJson = BuildEmbeddedChartAnnotationSpecJson(sourcePackage);
        var signalPackageJson = BuildEmbeddedSignalPackageJson(signalEvaluation);
        var embeddedSchemaJson = File.ReadAllText(schemaPath);
        var embeddedChecksum = ComputeChecksum(embeddedManifestJson, embeddedStrategyJson, embeddedChartAnnotationSpecJson, embeddedSchemaJson, signalPackageJson);

        var payload = new
        {
            bot_release_id = sourcePackage.PackageId,
            bot_version = sourcePackage.PackageVersion,
            strategy_package_version = sourcePackage.PackageVersion,
            schema_version = "ensemble_signal_agent_package.schema_v1",
            release_mode = "paper_only",
            safety_flags = StrictSafetyFlags(),
            forbidden_capabilities = ForbiddenCapabilities,
            embedded_manifest_json = embeddedManifestJson,
            embedded_strategy_json = embeddedStrategyJson,
            chart_annotation_spec_json = embeddedChartAnnotationSpecJson,
            embedded_schema_json = embeddedSchemaJson,
            signal_package_json = signalPackageJson,
            embedded_checksum = embeddedChecksum,
            generated_at_utc = DateTimeOffset.UtcNow,
            generated_by = "HermesRuntime",
            source_bundle_directory = sourceDirectory,
            source_bundle_manifest = manifestPath,
            source_bundle_package = packagePath,
            source_bundle_schema = schemaPath,
        };

        Directory.CreateDirectory(OutputDirectory);
        var json = JsonSerializer.Serialize(payload, JsonDefaults.WriteOptions);
        File.WriteAllText(OutputJsonPath, json);
        File.WriteAllText(OutputMarkdownPath, BuildMarkdown(sourceDirectory, bundleManifest, sourcePackage, embeddedChecksum, embeddedManifestJson, embeddedStrategyJson, embeddedChartAnnotationSpecJson, embeddedSchemaJson, signalPackageJson));
        WriteGeneratedSource(json, signalPackageJson, payload.bot_release_id, payload.bot_version, payload.strategy_package_version, payload.embedded_checksum);

        return new CloudEmbeddedReleasePackageGenerationResult(
            SourceBundleDirectory: sourceDirectory,
            OutputDirectory: OutputDirectory,
            OutputJsonPath: OutputJsonPath,
            OutputMarkdownPath: OutputMarkdownPath,
            OutputSourcePath: OutputSourcePath,
            Status: "generated",
            Reason: "ok",
            BotReleaseId: sourcePackage.PackageId,
            BotVersion: sourcePackage.PackageVersion,
            StrategyPackageVersion: sourcePackage.PackageVersion,
            SchemaVersion: "ensemble_signal_agent_package.schema_v1",
            ReleaseMode: "paper_only",
            EmbeddedChecksum: embeddedChecksum,
            Success: true);
    }

    private CloudEmbeddedReleasePackageGenerationResult BuildFailure(string status, string reason, string sourceDirectory)
        => new(
            SourceBundleDirectory: sourceDirectory,
            OutputDirectory: OutputDirectory,
            OutputJsonPath: OutputJsonPath,
            OutputMarkdownPath: OutputMarkdownPath,
            OutputSourcePath: OutputSourcePath,
            Status: status,
            Reason: reason,
            BotReleaseId: string.Empty,
            BotVersion: string.Empty,
            StrategyPackageVersion: string.Empty,
            SchemaVersion: string.Empty,
            ReleaseMode: "paper_only",
            EmbeddedChecksum: string.Empty,
            Success: false);

    private static object StrictSafetyFlags() => new
    {
        no_auto_trading = true,
        human_review_required = true,
        broker_orders_enabled = false,
        live_trading_enabled = false,
        order_api_enabled = false,
        paper_mode = true,
        broker_action = "none",
    };

    private static string BuildEmbeddedManifestJson(SystemBHandoffBundleManifest bundleManifest, EnsembleSignalAgentPortfolioPackage sourcePackage, string contractMarkdown)
    {
        var manifest = new
        {
            generated_at_utc = DateTimeOffset.UtcNow,
            generated_by = "HermesRuntime",
            bot_release_id = sourcePackage.PackageId,
            bot_version = sourcePackage.PackageVersion,
            strategy_package_version = sourcePackage.PackageVersion,
            schema_version = "ensemble_signal_agent_package.schema_v1",
            release_mode = "paper_only",
            safety_flags = StrictSafetyFlags(),
            forbidden_capabilities = ForbiddenCapabilities,
            source_system = bundleManifest.SourceSystem,
            source_bundle_version = bundleManifest.BundleVersion,
            source_file_count = bundleManifest.FileCount,
            source_contract_present = !string.IsNullOrWhiteSpace(contractMarkdown),
            source_status = sourcePackage.Status,
            source_package_id = sourcePackage.PackageId,
            source_package_assets = sourcePackage.Assets.Select(asset => asset.Asset).ToList(),
            chart_annotation_spec_present = true,
        };

        return JsonSerializer.Serialize(manifest, JsonDefaults.WriteOptions);
    }

    private static string BuildEmbeddedStrategySnapshotJson(SystemBHandoffBundleManifest bundleManifest, EnsembleSignalAgentPortfolioPackage sourcePackage)
    {
        var snapshot = new
        {
            package_id = sourcePackage.PackageId,
            package_version = sourcePackage.PackageVersion,
            source_system = sourcePackage.SourceSystem,
            status = sourcePackage.Status,
            assets = sourcePackage.Assets,
            safety_flags = sourcePackage.SafetyFlags,
            no_auto_trading = sourcePackage.NoAutoTrading,
            human_review_required = sourcePackage.HumanReviewRequired,
            broker_orders_enabled = sourcePackage.BrokerOrdersEnabled,
            live_trading_enabled = sourcePackage.LiveTradingEnabled,
            research_only = sourcePackage.ResearchOnly,
            source_bundle_version = bundleManifest.BundleVersion,
        };

        return JsonSerializer.Serialize(snapshot, JsonDefaults.WriteOptions);
    }

    private static string ComputeChecksum(string embeddedManifestJson, string embeddedStrategyJson, string embeddedChartAnnotationSpecJson, string embeddedSchemaJson, string signalPackageJson)
    {
        var combined = string.Join("\n", [embeddedManifestJson, embeddedStrategyJson, embeddedChartAnnotationSpecJson, embeddedSchemaJson, signalPackageJson]);
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(combined);
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    private string? ResolveSourceBundleDirectory(string? explicitSourceDirectory)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrWhiteSpace(explicitSourceDirectory))
        {
            candidates.Add(explicitSourceDirectory);
        }

        candidates.Add(Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "system_b_handoff", "system_b_handoff_bundle"));
        candidates.Add(Path.Combine(_storagePaths.Root, "reports", "system_b_handoff", "system_b_handoff_bundle"));
        candidates.Add(Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "ctrader_bot_release_bundle"));
        candidates.Add(Path.Combine(_storagePaths.Root, "reports", "ctrader_bot_release_bundle"));

        return candidates.FirstOrDefault(Directory.Exists);
    }

    private string ResolveOutputDirectory()
    {
        var preferred = Path.Combine(_storagePaths.Root, "reports", "cloud_embedded_release_package");
        try
        {
            Directory.CreateDirectory(preferred);
            return preferred;
        }
        catch
        {
            var fallback = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "cloud_embedded_release_package");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    private static string BuildMarkdown(
        string sourceDirectory,
        SystemBHandoffBundleManifest bundleManifest,
        EnsembleSignalAgentPortfolioPackage sourcePackage,
        string embeddedChecksum,
        string embeddedManifestJson,
        string embeddedStrategyJson,
        string embeddedChartAnnotationSpecJson,
        string embeddedSchemaJson,
        string signalPackageJson) => $"""
# Cloud Embedded Release Package

## Status
- source_bundle_directory: {sourceDirectory}
- bot_release_id: {sourcePackage.PackageId}
- bot_version: {sourcePackage.PackageVersion}
- strategy_package_version: {sourcePackage.PackageVersion}
- schema_version: ensemble_signal_agent_package.schema_v1
- release_mode: paper_only
- generated_by: HermesRuntime
- embedded_checksum: {embeddedChecksum}

## Safety
- no_auto_trading=true
- human_review_required=true
- broker_orders_enabled=false
- live_trading_enabled=false
- order_api_enabled=false
- paper_mode=true
- broker_action=none

## Source Bundle
- bundle_version: {bundleManifest.BundleVersion}
- source_system: {bundleManifest.SourceSystem}
- file_count: {bundleManifest.FileCount}

## Embedded Manifest JSON
{embeddedManifestJson}

## Embedded Strategy JSON
{embeddedStrategyJson}

## Embedded Chart Annotation Spec JSON
{embeddedChartAnnotationSpecJson}

## Embedded Schema JSON
{embeddedSchemaJson}

## Signal Package JSON
{signalPackageJson}

## Notes
- cloud_embedded_bundle does not depend on a local release inbox
- HermesRuntime remains the release authority
""";

    private void WriteGeneratedSource(string packageJson, string signalPackageJson, string botReleaseId, string botVersion, string strategyPackageVersion, string embeddedChecksum)
    {
        var generatedPath = OutputSourcePath;
        Directory.CreateDirectory(Path.GetDirectoryName(generatedPath) ?? _runtimeRoot);
        var escapedJson = packageJson.Replace("\"", "\"\"");
        var escapedSignalJson = signalPackageJson.Replace("\"", "\"\"");
        var source = string.Join(Environment.NewLine, new[]
        {
            "// <auto-generated />",
            "// Generated by HermesRuntime. Do not edit manually.",
            string.Empty,
            "namespace HermesPaperBot;",
            string.Empty,
            "/// <summary>",
            "/// Embedded cloud release package snapshot generated by HermesRuntime.",
            "/// </summary>",
            "public static class EmbeddedReleasePackage",
            "{",
            $"    public const string PackageJson = @\"{escapedJson}\";",
            $"    public const string SignalPackageJson = @\"{escapedSignalJson}\";",
            $"    public const string EmbeddedChecksum = \"{embeddedChecksum}\";",
            $"    public const string BotVersion = \"{botVersion}\";",
            $"    public const string BotReleaseId = \"{botReleaseId}\";",
            $"    public const string StrategyPackageVersion = \"{strategyPackageVersion}\";",
            "}",
        });

        File.WriteAllText(generatedPath, source);
    }

    private string BuildEmbeddedChartAnnotationSpecJson(EnsembleSignalAgentPortfolioPackage sourcePackage)
    {
        var chartExport = new ChartAnnotationExportService(_storagePaths, _runtimeRoot).Run(dryRun: true);
        var spec = new
        {
            generated_at_utc = DateTimeOffset.UtcNow,
            generated_by = "HermesRuntime",
            source_package_id = sourcePackage.PackageId,
            source_package_version = sourcePackage.PackageVersion,
            source_system = sourcePackage.SourceSystem,
            source_status = sourcePackage.Status,
            annotation_count = chartExport.AnnotationCount,
            annotation_source_mode = chartExport.SourceMode,
            annotations = chartExport.Annotations,
        };

        return JsonSerializer.Serialize(spec, JsonDefaults.WriteOptions);
    }

    private static string BuildEmbeddedSignalPackageJson(PaperSignalEvaluationReport? signalEvaluation)
    {
        var updatedAtUtc = signalEvaluation?.UpdatedAtUtc ?? DateTimeOffset.UtcNow;
        var signals = signalEvaluation?.Signals ?? [];
        var representativeSignal = signals.FirstOrDefault();
        var signalDecision = representativeSignal is null
            ? new
            {
                direction = "flat",
                confidence = 0m,
                strategy_id = "embedded_signal_missing",
                signal_timestamp_utc = updatedAtUtc,
                expiry_utc = updatedAtUtc,
                reason = "signal_package_missing",
                stop_loss_price = (decimal?)null,
                take_profit_price = (decimal?)null,
                max_holding_seconds = (int?)null,
                risk_r = (decimal?)null,
            }
            : new
            {
                direction = MapSignalDirection(representativeSignal.Direction),
                confidence = representativeSignal.ConfidenceBaseline,
                strategy_id = representativeSignal.SetupId,
                signal_timestamp_utc = updatedAtUtc,
                expiry_utc = updatedAtUtc.AddHours(1),
                reason = representativeSignal.Reason,
                stop_loss_price = (decimal?)null,
                take_profit_price = (decimal?)null,
                max_holding_seconds = (int?)null,
                risk_r = (decimal?)null,
            };

        var payload = new
        {
            report_version = signalEvaluation?.ReportVersion ?? "paper_signal_evaluation_v1",
            updated_at_utc = updatedAtUtc,
            status = signalEvaluation?.Status ?? "missing",
            signal_count = signals.Count,
            signal_decision = signalDecision,
            signals,
            paper_decision_summary = signalEvaluation?.PaperDecisionSummary ?? "signal_package_unavailable",
            warnings = signalEvaluation?.Warnings ?? [],
            recommendations = signalEvaluation?.Recommendations ?? [],
        };

        return JsonSerializer.Serialize(payload, JsonDefaults.WriteOptions);
    }

    private static string MapSignalDirection(string? direction)
    {
        var normalized = direction?.Trim() ?? string.Empty;
        var hasLong = normalized.Contains("long", StringComparison.OrdinalIgnoreCase);
        var hasShort = normalized.Contains("short", StringComparison.OrdinalIgnoreCase);

        if (hasLong && !hasShort)
        {
            return "long";
        }

        if (hasShort && !hasLong)
        {
            return "short";
        }

        return "flat";
    }
}
