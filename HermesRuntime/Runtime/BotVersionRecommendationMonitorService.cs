using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record BotVersionRecommendationApprovedAnnotation(
    string Asset,
    string SetupId,
    bool Approved,
    bool PromotedToEmbedded,
    string Reviewer,
    DateTimeOffset ReviewTimestampUtc,
    string Comment,
    double? ConfidenceBaseline);

public sealed record BotVersionRecommendationReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    string BotVersionRecommendationStatus,
    decimal? BotEvolutionScore,
    decimal? PreviousBotEvolutionScore,
    decimal? BotEvolutionImprovementDelta,
    string? BotEvolutionRecommendation,
    string? BotEvolutionConfidenceLevel,
    string CurrentExportId,
    DateTimeOffset? CurrentExportTimestampUtc,
    string? CurrentExportPath,
    string? CurrentExportMetadataPath,
    string? CurrentExportSha256,
    string? CurrentEmbeddedChecksum,
    string? CurrentStrategyPackageVersion,
    string? CurrentSignalPackageVersion,
    string? CurrentSignalStrategyId,
    double? CurrentSignalConfidence,
    int ApprovedAnnotationCount,
    int PromotedAnnotationCount,
    int PendingPromotionCount,
    double? BestApprovedConfidence,
    double? BestPromotedConfidence,
    bool RecommendedExportAvailable,
    string RecommendationReason,
    bool ManualActionRequired,
    string SuggestedNextCommand,
    string CurrentCloudEmbeddedPackagePath,
    string CurrentCloudEmbeddedPackageReportPath,
    string CurrentApprovedAnnotationRegistryPath,
    string CurrentExportManifestPath,
    string CurrentBotEvolutionScoreReportPath,
    IReadOnlyList<BotVersionRecommendationApprovedAnnotation> ApprovedAnnotations,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Recommendations,
    string ReportPath,
    string MarkdownPath);

public sealed class BotVersionRecommendationMonitorService
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public BotVersionRecommendationMonitorService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "bot_version_recommendation");
    public string ReportPath => Path.Combine(Root, "bot_version_recommendation_report.json");
    public string MarkdownPath => Path.Combine(Root, "bot_version_recommendation_report.md");

    public BotVersionRecommendationReport Run()
    {
        Directory.CreateDirectory(Root);

        var currentCloudEmbeddedPackageReportPath = Path.Combine(_storagePaths.Root, "reports", "cloud_embedded_release_package", "cloud_embedded_release_package.json");
        var currentApprovedRegistryPath = Path.Combine(_storagePaths.Root, "reports", "approved_chart_annotations", "approved_chart_annotations.json");
        var currentBotEvolutionScoreReportPath = Path.Combine(_storagePaths.Root, "reports", "bot_evolution_score", "bot_evolution_score.json");
        var currentExportManifestPath = ResolveCurrentExportManifestPath();
        var currentExportManifest = LoadCurrentExportManifest(currentExportManifestPath);
        var cloudEmbeddedPackage = LoadCloudEmbeddedPackage(currentCloudEmbeddedPackageReportPath);
        var botEvolutionScore = LoadBotEvolutionScore(currentBotEvolutionScoreReportPath) ?? new BotEvolutionScoreReport(
            ReportVersion: "bot_evolution_score_v1",
            UpdatedAtUtc: DateTimeOffset.MinValue,
            Status: "missing",
            EvolutionScore: 0m,
            PreviousScore: null,
            ImprovementDelta: null,
            Recommendation: "do_not_recommend",
            ConfidenceLevel: "low",
            Metrics: new BotEvolutionMetricBreakdown(0m, 0m, 0m, 0m, 0m, 0m, 0, "partial", 0m),
            PaperRuntimeStepReportPath: string.Empty,
            PaperSignalExplainReportPath: string.Empty,
            PaperTradeSummaryReportPath: string.Empty,
            PaperTradeHistoryReportPath: string.Empty,
            PaperForwardSessionReportPath: string.Empty,
            CurrentBotVersionRecommendationReportPath: null,
            Warnings: [],
            ReportPath: currentBotEvolutionScoreReportPath,
            MarkdownPath: Path.ChangeExtension(currentBotEvolutionScoreReportPath, ".md"));
        var approvedAnnotations = LoadApprovedAnnotations(currentApprovedRegistryPath);

        var currentAlgoSourcePath = currentExportManifest?.SourceAlgoPath
            ?? Path.Combine(_runtimeRoot, "ctrader", "HermesPaperBot.AlgoProject", "bin", "Debug", "net6.0", "HermesPaperBot.algo");
        var currentAlgoSha256 = File.Exists(currentAlgoSourcePath) ? ComputeSha256(currentAlgoSourcePath) : null;
        var currentExportId = currentExportManifest?.ExportId ?? "none";
        var currentExportTimestampUtc = currentExportManifest?.TimestampUtc;
        var currentExportSha256 = currentExportManifest?.Sha256;

        var approvedCount = approvedAnnotations.Count;
        var promotedCount = approvedAnnotations.Count(item => item.PromotedToEmbedded);
        var pendingPromotionCount = Math.Max(0, approvedCount - promotedCount);

        var approvedConfidenceValues = approvedAnnotations
            .Where(item => item.Approved && item.ConfidenceBaseline.HasValue)
            .Select(item => item.ConfidenceBaseline!.Value)
            .ToList();
        double? bestApprovedConfidence = approvedConfidenceValues.Count > 0 ? approvedConfidenceValues.Max() : (double?)null;
        var promotedConfidenceValues = approvedAnnotations
            .Where(item => item.Approved && item.PromotedToEmbedded && item.ConfidenceBaseline.HasValue)
            .Select(item => item.ConfidenceBaseline!.Value)
            .ToList();
        double? bestPromotedConfidence = promotedConfidenceValues.Count > 0 ? promotedConfidenceValues.Max() : (double?)null;

        var currentSignalDecision = cloudEmbeddedPackage?.SignalDecision;
        var recommendationReasons = new List<string>();
        var warnings = new List<string>();

        if (approvedCount == 0)
        {
            warnings.Add("no_approved_chart_annotations_found");
        }

        if (cloudEmbeddedPackage is null)
        {
            warnings.Add("cloud_embedded_release_package_report_missing");
        }

        if (currentExportManifest is null)
        {
            warnings.Add("ctrader_export_manifest_missing");
            if (currentAlgoSha256 is not null)
            {
                recommendationReasons.Add("ctrader_export_manifest_missing");
            }
        }

        if (currentAlgoSha256 is null)
        {
            warnings.Add("current_algo_artifact_missing");
        }

        if (currentExportManifest is not null && currentAlgoSha256 is not null && !string.Equals(currentAlgoSha256, currentExportManifest.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            recommendationReasons.Add("embedded_algo_hash_changed_since_last_export");
        }

        if (cloudEmbeddedPackage is not null && currentExportTimestampUtc is not null && cloudEmbeddedPackage.GeneratedAtUtc > currentExportTimestampUtc.Value)
        {
            recommendationReasons.Add("cloud_embedded_package_generated_after_last_export");
        }

        if (pendingPromotionCount > 0)
        {
            recommendationReasons.Add("approved_chart_annotations_pending_promotion");
        }

        if (bestApprovedConfidence is not null && bestPromotedConfidence is not null && bestApprovedConfidence.Value > bestPromotedConfidence.Value)
        {
            recommendationReasons.Add("approved_annotation_confidence_improved");
        }

        var botEvolutionAllowsRecommendation = botEvolutionScore.Recommendation is "recommend_new_version" or "hold_current_version"
            && botEvolutionScore.EvolutionScore >= 60m;
        if (!botEvolutionAllowsRecommendation)
        {
            warnings.Add($"bot_evolution_score_suppressed:{botEvolutionScore.Recommendation}:{botEvolutionScore.EvolutionScore:0.0}");
        }
        else if (botEvolutionScore.ImprovementDelta.HasValue)
        {
            recommendationReasons.Add("bot_evolution_score_supports_export");
        }

        var recommendedExportAvailable = recommendationReasons.Count > 0 && botEvolutionAllowsRecommendation;
        var recommendationReason = recommendedExportAvailable
            ? string.Join("; ", recommendationReasons.Distinct(StringComparer.OrdinalIgnoreCase))
            : botEvolutionAllowsRecommendation
                ? "current_export_is_up_to_date"
                : "bot_evolution_score_does_not_support_new_version";
        var manualActionRequired = recommendedExportAvailable;
        var suggestedNextCommand = pendingPromotionCount > 0
            ? "dotnet run --project ./cli/Hermes.Cli.csproj -- chart-annotation-promote"
            : recommendedExportAvailable
                ? "dotnet run --project ./cli/Hermes.Cli.csproj -- ctrader-export"
                : "dotnet run --project ./cli/Hermes.Cli.csproj -- bot-evolution-score";

        var report = new BotVersionRecommendationReport(
            ReportVersion: "bot_version_recommendation_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: recommendedExportAvailable ? "recommendation_available" : "up_to_date",
            BotVersionRecommendationStatus: recommendedExportAvailable ? "recommended_export_available" : "current_export_up_to_date",
            BotEvolutionScore: botEvolutionScore.EvolutionScore,
            PreviousBotEvolutionScore: botEvolutionScore.PreviousScore,
            BotEvolutionImprovementDelta: botEvolutionScore.ImprovementDelta,
            BotEvolutionRecommendation: botEvolutionScore.Recommendation,
            BotEvolutionConfidenceLevel: botEvolutionScore.ConfidenceLevel,
            CurrentExportId: currentExportId,
            CurrentExportTimestampUtc: currentExportTimestampUtc,
            CurrentExportPath: currentExportManifest?.IndexedAlgoPath ?? currentAlgoSourcePath,
            CurrentExportMetadataPath: currentExportManifest?.IndexedAlgoMetadataPath,
            CurrentExportSha256: currentExportSha256,
            CurrentEmbeddedChecksum: cloudEmbeddedPackage?.EmbeddedChecksum,
            CurrentStrategyPackageVersion: cloudEmbeddedPackage?.StrategyPackageVersion,
            CurrentSignalPackageVersion: cloudEmbeddedPackage?.SignalPackageVersion,
            CurrentSignalStrategyId: currentSignalDecision?.StrategyId,
            CurrentSignalConfidence: currentSignalDecision?.Confidence,
            ApprovedAnnotationCount: approvedCount,
            PromotedAnnotationCount: promotedCount,
            PendingPromotionCount: pendingPromotionCount,
            BestApprovedConfidence: bestApprovedConfidence,
            BestPromotedConfidence: bestPromotedConfidence,
            RecommendedExportAvailable: recommendedExportAvailable,
            RecommendationReason: recommendationReason,
            ManualActionRequired: manualActionRequired,
            SuggestedNextCommand: suggestedNextCommand,
            CurrentCloudEmbeddedPackagePath: currentCloudEmbeddedPackageReportPath,
            CurrentCloudEmbeddedPackageReportPath: currentCloudEmbeddedPackageReportPath,
            CurrentApprovedAnnotationRegistryPath: currentApprovedRegistryPath,
            CurrentExportManifestPath: currentExportManifestPath,
            CurrentBotEvolutionScoreReportPath: currentBotEvolutionScoreReportPath,
            ApprovedAnnotations: approvedAnnotations,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Recommendations: BuildRecommendations(recommendedExportAvailable, pendingPromotionCount, currentExportManifest, currentAlgoSha256, cloudEmbeddedPackage),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    private static IReadOnlyList<string> BuildRecommendations(
        bool recommendedExportAvailable,
        int pendingPromotionCount,
        CurrentExportManifest? currentExportManifest,
        string? currentAlgoSha256,
        CloudEmbeddedPackageReport? cloudEmbeddedPackage)
    {
        var recommendations = new List<string>();
        if (pendingPromotionCount > 0)
        {
            recommendations.Add("run chart-annotation-promote for approved annotation candidates before export");
        }
        if (recommendedExportAvailable)
        {
            recommendations.Add("run ctrader-export to refresh the cTrader handoff package");
        }
        if (currentExportManifest is null)
        {
            recommendations.Add("create or regenerate the cTrader export manifest first");
        }
        if (currentAlgoSha256 is null)
        {
            recommendations.Add("build the AlgoProject so a current HermesPaperBot.algo exists");
        }
        if (cloudEmbeddedPackage is null)
        {
            recommendations.Add("refresh the cloud embedded release package report");
        }

        return recommendations.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static BotEvolutionScoreReport? LoadBotEvolutionScore(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<BotEvolutionScoreReport>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveCurrentExportManifestPath()
    {
        var wslManifest = Path.Combine(Path.DirectorySeparatorChar.ToString(), "mnt", "d", "Bot", "ctrader_export_manifest.json");
        if (File.Exists(wslManifest))
        {
            return wslManifest;
        }

        return Path.Combine(Path.DirectorySeparatorChar.ToString(), "mnt", "d", "Bot", "ctrader_export_manifest.json");
    }

    private static CurrentExportManifest? LoadCurrentExportManifest(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            return new CurrentExportManifest(
                ExportId: ReadString(root, "export_id") ?? "none",
                TimestampUtc: ReadDateTimeOffset(root, "timestamp"),
                SourceAlgoPath: ReadString(root, "source_algo_path"),
                IndexedAlgoPath: ReadString(root, "indexed_algo_path"),
                IndexedAlgoMetadataPath: ReadString(root, "indexed_algo_metadata_path"),
                LatestAlgoPath: ReadString(root, "latest_algo_path"),
                LatestAlgoMetadataPath: ReadString(root, "latest_algo_metadata_path"),
                Sha256: ReadString(root, "sha256"));
        }
        catch
        {
            return null;
        }
    }

    private static CloudEmbeddedPackageReport? LoadCloudEmbeddedPackage(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var signalPackageJson = ReadString(root, "signal_package_json");
            var signalDecision = TryReadSignalDecision(signalPackageJson);
            return new CloudEmbeddedPackageReport(
                GeneratedAtUtc: ReadDateTimeOffset(root, "generated_at_utc") ?? DateTimeOffset.MinValue,
                EmbeddedChecksum: ReadString(root, "embedded_checksum"),
                StrategyPackageVersion: ReadString(root, "strategy_package_version"),
                SignalPackageVersion: ReadSignalPackageVersion(root, signalPackageJson),
                SignalPackageJson: signalPackageJson,
                SignalDecision: signalDecision,
                CurrentForwardNetR: TryReadForwardNetR(root));
        }
        catch
        {
            return null;
        }
    }

    private static SignalDecisionSnapshot? TryReadSignalDecision(string? signalPackageJson)
    {
        if (string.IsNullOrWhiteSpace(signalPackageJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(signalPackageJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("signal_decision", out var signalDecision) || signalDecision.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new SignalDecisionSnapshot(
                StrategyId: ReadString(signalDecision, "strategy_id"),
                Confidence: ReadNullableDouble(signalDecision, "confidence"));
        }
        catch
        {
            return null;
        }
    }

    private static double? TryReadForwardNetR(JsonElement root)
    {
        if (root.TryGetProperty("paper_decision_summary", out var summaryElement) && summaryElement.ValueKind == JsonValueKind.String)
        {
            var summary = summaryElement.GetString();
            if (!string.IsNullOrWhiteSpace(summary))
            {
                foreach (var part in summary.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (part.StartsWith("net_r=", StringComparison.OrdinalIgnoreCase) &&
                        double.TryParse(part["net_r=".Length..], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value))
                    {
                        return value;
                    }
                }
            }
        }

        return null;
    }

    private IReadOnlyList<BotVersionRecommendationApprovedAnnotation> LoadApprovedAnnotations(string path)
    {
        var items = new List<BotVersionRecommendationApprovedAnnotation>();
        if (!File.Exists(path))
        {
            return items;
        }

        try
        {
            var report = JsonSerializer.Deserialize<ApprovedChartAnnotationRegistryReport>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
            if (report?.Items is null)
            {
                return items;
            }

            foreach (var item in report.Items)
            {
                items.Add(new BotVersionRecommendationApprovedAnnotation(
                    Asset: item.Asset,
                    SetupId: item.SetupId,
                    Approved: item.Approved,
                    PromotedToEmbedded: item.PromotedToEmbedded,
                    Reviewer: item.Reviewer,
                    ReviewTimestampUtc: item.ReviewTimestampUtc,
                    Comment: item.Comment,
                    ConfidenceBaseline: ReadArtifactConfidence(item.Asset, item.SetupId)));
            }
        }
        catch
        {
            return items;
        }

        return items;
    }

    private double? ReadArtifactConfidence(string asset, string setupId)
    {
        var docRoot = Path.Combine(_runtimeRoot, "docs", "trading");
        if (!Directory.Exists(docRoot))
        {
            return null;
        }

        foreach (var path in Directory.EnumerateFiles(docRoot, "*chart_annotation_review_artifact.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                var artifactAsset = ReadString(root, "asset");
                var artifactSetup = ReadString(root, "setup_id");
                if (!string.Equals(artifactAsset, asset, StringComparison.OrdinalIgnoreCase) || !string.Equals(artifactSetup, setupId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (root.TryGetProperty("confidence_baseline", out var confidenceElement) && confidenceElement.TryGetDouble(out var confidence))
                {
                    return confidence;
                }
            }
            catch
            {
                // ignore unreadable artifacts
            }
        }

        return null;
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        var hash = SHA256.HashData(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string? ReadString(JsonElement root, string propertyName)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string? ReadSignalPackageVersion(JsonElement root, string? signalPackageJson)
    {
        if (!string.IsNullOrWhiteSpace(signalPackageJson))
        {
            try
            {
                using var document = JsonDocument.Parse(signalPackageJson);
                var signalRoot = document.RootElement;
                if (signalRoot.ValueKind == JsonValueKind.Object && signalRoot.TryGetProperty("report_version", out var reportVersion) && reportVersion.ValueKind == JsonValueKind.String)
                {
                    return reportVersion.GetString();
                }
            }
            catch
            {
                // fall through to root property
            }
        }

        return ReadString(root, "signal_package_version");
    }

    private static double? ReadNullableDouble(JsonElement root, string propertyName)
        => root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var property) && property.TryGetDouble(out var value)
            ? value
            : null;

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement root, string propertyName)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(property.GetString(), out var value))
        {
            return value;
        }

        return null;
    }

    private static string BuildMarkdown(BotVersionRecommendationReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Bot Version Recommendation Monitor");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- bot_version_recommendation_status: {report.BotVersionRecommendationStatus}");
        sb.AppendLine($"- current_export_id: {report.CurrentExportId}");
        sb.AppendLine($"- recommended_export_available: {report.RecommendedExportAvailable.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- recommendation_reason: {report.RecommendationReason}");
        sb.AppendLine($"- manual_action_required: {report.ManualActionRequired.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- suggested_next_command: {report.SuggestedNextCommand}");
        sb.AppendLine($"- current_export_manifest_path: {report.CurrentExportManifestPath}");
        sb.AppendLine($"- current_cloud_embedded_package_path: {report.CurrentCloudEmbeddedPackagePath}");
        sb.AppendLine($"- current_approved_annotation_registry_path: {report.CurrentApprovedAnnotationRegistryPath}");
        sb.AppendLine($"- current_bot_evolution_score_report_path: {report.CurrentBotEvolutionScoreReportPath}");
        sb.AppendLine();
        sb.AppendLine("## Counts");
        sb.AppendLine($"- approved_annotation_count: {report.ApprovedAnnotationCount}");
        sb.AppendLine($"- promoted_annotation_count: {report.PromotedAnnotationCount}");
        sb.AppendLine($"- pending_promotion_count: {report.PendingPromotionCount}");
        sb.AppendLine();
        sb.AppendLine("## Signals");
        sb.AppendLine($"- current_strategy_package_version: {report.CurrentStrategyPackageVersion ?? "-"}");
        sb.AppendLine($"- current_signal_package_version: {report.CurrentSignalPackageVersion ?? "-"}");
        sb.AppendLine($"- current_signal_strategy_id: {report.CurrentSignalStrategyId ?? "-"}");
        sb.AppendLine($"- current_signal_confidence: {(report.CurrentSignalConfidence is null ? "-" : report.CurrentSignalConfidence.Value.ToString("0.####"))}");
        sb.AppendLine($"- bot_evolution_score: {(report.BotEvolutionScore is null ? "-" : report.BotEvolutionScore.Value.ToString("0.0", CultureInfo.InvariantCulture))}");
        sb.AppendLine($"- previous_bot_evolution_score: {(report.PreviousBotEvolutionScore is null ? "-" : report.PreviousBotEvolutionScore.Value.ToString("0.0", CultureInfo.InvariantCulture))}");
        sb.AppendLine($"- bot_evolution_improvement_delta: {(report.BotEvolutionImprovementDelta is null ? "-" : report.BotEvolutionImprovementDelta.Value.ToString("0.0", CultureInfo.InvariantCulture))}");
        sb.AppendLine($"- bot_evolution_recommendation: {report.BotEvolutionRecommendation ?? "-"}");
        sb.AppendLine($"- bot_evolution_confidence_level: {report.BotEvolutionConfidenceLevel ?? "-"}");
        sb.AppendLine($"- current_embedded_checksum: {report.CurrentEmbeddedChecksum ?? "-"}");
        sb.AppendLine($"- current_export_sha256: {report.CurrentExportSha256 ?? "-"}");
        sb.AppendLine();

        if (report.ApprovedAnnotations.Count > 0)
        {
            sb.AppendLine("## Approved Annotations");
            foreach (var item in report.ApprovedAnnotations.OrderByDescending(item => item.ReviewTimestampUtc).ThenBy(item => item.Asset, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"- {item.Asset} / {item.SetupId} | approved={item.Approved.ToString().ToLowerInvariant()} | promoted_to_embedded={item.PromotedToEmbedded.ToString().ToLowerInvariant()} | reviewer={item.Reviewer} | reviewed_at={item.ReviewTimestampUtc:O}");
            }
            sb.AppendLine();
        }

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine("## Warnings");
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

    private sealed record CurrentExportManifest(
        string ExportId,
        DateTimeOffset? TimestampUtc,
        string? SourceAlgoPath,
        string? IndexedAlgoPath,
        string? IndexedAlgoMetadataPath,
        string? LatestAlgoPath,
        string? LatestAlgoMetadataPath,
        string? Sha256);

    private sealed record CloudEmbeddedPackageReport(
        DateTimeOffset GeneratedAtUtc,
        string? EmbeddedChecksum,
        string? StrategyPackageVersion,
        string? SignalPackageVersion,
        string? SignalPackageJson,
        SignalDecisionSnapshot? SignalDecision,
        double? CurrentForwardNetR);

    private sealed record SignalDecisionSnapshot(
        string? StrategyId,
        double? Confidence);
}
