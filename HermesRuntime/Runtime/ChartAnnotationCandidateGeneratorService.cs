using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ChartAnnotationCandidate(
    string Asset,
    string SetupId,
    double ConfidenceBaseline,
    string Readiness,
    bool EntrySourceAvailable,
    bool SlSourceAvailable,
    bool TpSourceAvailable,
    bool InvalidationSourceAvailable,
    bool CanGenerateAnnotation,
    IReadOnlyList<string> MissingFields,
    bool RequiresHumanReview,
    string Status);

public sealed record ChartAnnotationCandidateReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    string SourceMode,
    bool EmbeddedSpecAvailable,
    int CandidateCount,
    int ReadyCandidateCount,
    int NeedsPriceReviewCount,
    IReadOnlyList<ChartAnnotationCandidate> Candidates,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled,
    bool ResearchOnly);

public sealed class ChartAnnotationCandidateGeneratorService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ChartAnnotationCandidateGeneratorService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "chart_annotation_candidates");
    public string ReportPath => Path.Combine(Root, "chart_annotation_candidates.json");
    public string MarkdownPath => Path.Combine(Root, "chart_annotation_candidates.md");

    public ChartAnnotationCandidateReport LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return Run();
        }

        try
        {
            return JsonSerializer.Deserialize<ChartAnnotationCandidateReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions) ?? Run();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Run();
        }
    }

    public ChartAnnotationCandidateReport Run()
    {
        var warnings = new List<string>();
        var package = LoadLatestEmbeddedPackageJson(out var packageWarnings);
        warnings.AddRange(packageWarnings);

        var embeddedAnnotations = LoadEmbeddedChartAnnotations(package, out var chartWarnings);
        warnings.AddRange(chartWarnings);

        var assets = LoadCandidateAssets(package, embeddedAnnotations);
        var candidates = assets
            .Select(asset => BuildCandidate(asset, embeddedAnnotations))
            .OrderByDescending(item => item.CanGenerateAnnotation)
            .ThenByDescending(item => item.ConfidenceBaseline)
            .ThenBy(item => item.Asset, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var report = new ChartAnnotationCandidateReport(
            ReportVersion: "chart_annotation_candidate_generator_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: candidates.Count > 0 ? "ready" : "partial",
            SourceMode: embeddedAnnotations.Count > 0 ? "embedded_spec" : "embedded_strategy_only",
            EmbeddedSpecAvailable: embeddedAnnotations.Count > 0,
            CandidateCount: candidates.Count,
            ReadyCandidateCount: candidates.Count(item => item.CanGenerateAnnotation),
            NeedsPriceReviewCount: candidates.Count(item => item.Status == "needs_price_review"),
            Candidates: candidates,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false,
            ResearchOnly: true);

        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    private CloudEmbeddedReleasePackageSnapshot? LoadLatestEmbeddedPackageJson(out List<string> warnings)
    {
        warnings = [];
        try
        {
            var generator = new CloudEmbeddedReleasePackageGeneratorService(_storagePaths, _runtimeRoot);
            var path = generator.OutputJsonPath;
            if (!File.Exists(path))
            {
                warnings.Add("embedded_release_package_missing");
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            var embeddedStrategyJson = root.TryGetProperty("embedded_strategy_json", out var strategyJson) && strategyJson.ValueKind == JsonValueKind.String
                ? strategyJson.GetString()
                : null;
            var chartAnnotationSpecJson = root.TryGetProperty("chart_annotation_spec_json", out var chartSpecJson) && chartSpecJson.ValueKind == JsonValueKind.String
                ? chartSpecJson.GetString()
                : null;

            return new CloudEmbeddedReleasePackageSnapshot(embeddedStrategyJson, chartAnnotationSpecJson);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            warnings.Add($"embedded_release_package_parse_failed:{ex.GetType().Name}");
            return null;
        }
    }

    private static IReadOnlyList<EmbeddedStrategyAsset> LoadCandidateAssets(CloudEmbeddedReleasePackageSnapshot? package, IReadOnlyList<ChartAnnotation> annotations)
    {
        if (package is null || string.IsNullOrWhiteSpace(package.EmbeddedStrategyJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(package.EmbeddedStrategyJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("assets", out var assetsElement) || assetsElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<EmbeddedStrategyAsset>();
            foreach (var assetElement in assetsElement.EnumerateArray())
            {
                if (assetElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var readiness = ReadString(assetElement, "readiness");
                if (!IsCandidateReadiness(readiness))
                {
                    continue;
                }

                var asset = ReadString(assetElement, "asset");
                var setupId = ReadString(assetElement, "setup_id");
                var confidenceBaseline = ReadDouble(assetElement, "confidence_baseline");
                result.Add(new EmbeddedStrategyAsset(asset, setupId, confidenceBaseline, readiness));
            }

            return result;
        }
        catch
        {
            return [];
        }
    }

    private static ChartAnnotationCandidate BuildCandidate(EmbeddedStrategyAsset asset, IReadOnlyList<ChartAnnotation> annotations)
    {
        var annotation = annotations.FirstOrDefault(item =>
            item.Symbol.Equals(asset.Asset, StringComparison.OrdinalIgnoreCase) &&
            item.SetupId.Equals(asset.SetupId, StringComparison.OrdinalIgnoreCase));

        var entrySourceAvailable = annotation is not null && annotation.EntryPrice > 0;
        var slSourceAvailable = annotation is not null && annotation.StopLoss > 0;
        var tpSourceAvailable = annotation is not null && annotation.TakeProfit1 > 0;
        var invalidationSourceAvailable = annotation is not null && annotation.InvalidationLevel > 0;
        var missingFields = new List<string>();

        if (!entrySourceAvailable) missingFields.Add("entry_price");
        if (!slSourceAvailable) missingFields.Add("stop_loss");
        if (!tpSourceAvailable) missingFields.Add("take_profit_1");
        if (!invalidationSourceAvailable) missingFields.Add("invalidation_level");
        if (annotation is null || annotation.RiskReward <= 0) missingFields.Add("risk_reward");

        var canGenerate = entrySourceAvailable && slSourceAvailable && tpSourceAvailable && invalidationSourceAvailable && annotation is not null && annotation.RiskReward > 0;
        var status = canGenerate ? "ready_for_review" : "needs_price_review";

        return new ChartAnnotationCandidate(
            Asset: asset.Asset,
            SetupId: asset.SetupId,
            ConfidenceBaseline: asset.ConfidenceBaseline,
            Readiness: asset.Readiness,
            EntrySourceAvailable: entrySourceAvailable,
            SlSourceAvailable: slSourceAvailable,
            TpSourceAvailable: tpSourceAvailable,
            InvalidationSourceAvailable: invalidationSourceAvailable,
            CanGenerateAnnotation: canGenerate,
            MissingFields: missingFields.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            RequiresHumanReview: true,
            Status: status);
    }

    private static IReadOnlyList<ChartAnnotation> LoadEmbeddedChartAnnotations(CloudEmbeddedReleasePackageSnapshot? package, out List<string> warnings)
    {
        warnings = [];
        if (package is null || string.IsNullOrWhiteSpace(package.ChartAnnotationSpecJson))
        {
            warnings.Add("embedded_chart_annotation_spec_missing");
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(package.ChartAnnotationSpecJson);
            var root = document.RootElement;
            var annotationsElement = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("annotations", out var nested) && nested.ValueKind == JsonValueKind.Array
                ? nested
                : root.ValueKind == JsonValueKind.Array
                    ? root
                    : default;

            if (annotationsElement.ValueKind != JsonValueKind.Array)
            {
                warnings.Add("embedded_chart_annotation_spec_invalid");
                return [];
            }

            var annotations = new List<ChartAnnotation>();
            foreach (var annotationElement in annotationsElement.EnumerateArray())
            {
                if (annotationElement.ValueKind != JsonValueKind.Object)
                {
                    warnings.Add("embedded_chart_annotation_entry_invalid");
                    continue;
                }

                if (!TryReadDouble(annotationElement, "entry_price", out var entryPrice)) continue;
                if (!TryReadDouble(annotationElement, "stop_loss", out var stopLoss)) continue;
                var hasTakeProfit1 = TryReadDouble(annotationElement, "take_profit_1", out var takeProfit1);
                if (!hasTakeProfit1 && !TryReadDouble(annotationElement, "take_profit1", out takeProfit1)) continue;
                if (!TryReadDouble(annotationElement, "invalidation_level", out var invalidationLevel)) continue;
                if (!TryReadDouble(annotationElement, "risk_reward", out var riskReward)) continue;

                annotations.Add(new ChartAnnotation(
                    SignalId: ReadString(annotationElement, "signal_id"),
                    Symbol: ReadString(annotationElement, "symbol"),
                    Timeframe: ReadString(annotationElement, "timeframe"),
                    SetupId: ReadString(annotationElement, "setup_id"),
                    Direction: ReadString(annotationElement, "direction"),
                    EntryPrice: entryPrice,
                    StopLoss: stopLoss,
                    TakeProfit1: takeProfit1,
                    TakeProfit2: TryReadNullableDouble(annotationElement, "take_profit_2") ?? TryReadNullableDouble(annotationElement, "take_profit2"),
                    InvalidationLevel: invalidationLevel,
                    RiskReward: riskReward,
                    AnnotationStyle: ReadString(annotationElement, "annotation_style"),
                    Labels: ReadStringArray(annotationElement, "labels"),
                    CreatedAtUtc: ReadDateTime(annotationElement, "created_at_utc") ?? DateTimeOffset.UtcNow,
                    SignalStatus: ReadString(annotationElement, "signal_status")));
            }

            return annotations;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            warnings.Add($"embedded_chart_annotation_spec_parse_failed:{ex.GetType().Name}");
            return [];
        }
    }

    private static bool IsCandidateReadiness(string readiness)
        => readiness.Equals("bot_ready", StringComparison.OrdinalIgnoreCase)
           || readiness.Equals("signal_ready", StringComparison.OrdinalIgnoreCase);

    private static string ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : string.Empty;

    private static double ReadDouble(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value)
            ? value
            : 0;

    private static double? TryReadNullableDouble(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out var value)
            ? value
            : null;

    private static bool TryReadDouble(JsonElement element, string propertyName, out double value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object
               && element.TryGetProperty(propertyName, out var property)
               && property.ValueKind == JsonValueKind.Number
               && property.TryGetDouble(out value);
    }

    private static DateTimeOffset? ReadDateTime(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            var text = property.GetString();
            if (!string.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array)
        {
            return property.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();
        }

        return [];
    }

    private static string BuildMarkdown(ChartAnnotationCandidateReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Chart Annotation Candidate Generator");
        builder.AppendLine();
        builder.AppendLine($"- report_version: {report.ReportVersion}");
        builder.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        builder.AppendLine($"- status: {report.Status}");
        builder.AppendLine($"- source_mode: {report.SourceMode}");
        builder.AppendLine($"- embedded_spec_available: {report.EmbeddedSpecAvailable.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- candidate_count: {report.CandidateCount}");
        builder.AppendLine($"- ready_candidate_count: {report.ReadyCandidateCount}");
        builder.AppendLine($"- needs_price_review_count: {report.NeedsPriceReviewCount}");
        builder.AppendLine($"- no_auto_trading: {report.NoAutoTrading.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- human_review_required: {report.HumanReviewRequired.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- broker_orders_enabled: {report.BrokerOrdersEnabled.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- live_trading_enabled: {report.LiveTradingEnabled.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- research_only: {report.ResearchOnly.ToString().ToLowerInvariant()}");
        builder.AppendLine();

        foreach (var candidate in report.Candidates)
        {
            builder.AppendLine($"## {candidate.Asset} / {candidate.SetupId}");
            builder.AppendLine($"- asset: {candidate.Asset}");
            builder.AppendLine($"- setup_id: {candidate.SetupId}");
            builder.AppendLine($"- confidence_baseline: {candidate.ConfidenceBaseline:0.####}");
            builder.AppendLine($"- readiness: {candidate.Readiness}");
            builder.AppendLine($"- entry_source_available: {candidate.EntrySourceAvailable.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- sl_source_available: {candidate.SlSourceAvailable.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- tp_source_available: {candidate.TpSourceAvailable.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- invalidation_source_available: {candidate.InvalidationSourceAvailable.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- can_generate_annotation: {candidate.CanGenerateAnnotation.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- missing_fields: {string.Join(", ", candidate.MissingFields)}");
            builder.AppendLine($"- requires_human_review: {candidate.RequiresHumanReview.ToString().ToLowerInvariant()}");
            builder.AppendLine($"- status: {candidate.Status}");
            builder.AppendLine();
        }

        if (report.Warnings.Count > 0)
        {
            builder.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                builder.AppendLine($"- {warning}");
            }
        }

        return builder.ToString();
    }

    private sealed record EmbeddedStrategyAsset(string Asset, string SetupId, double ConfidenceBaseline, string Readiness);

    private sealed record CloudEmbeddedReleasePackageSnapshot(string? EmbeddedStrategyJson, string? ChartAnnotationSpecJson);
}
