using System.Linq;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ChartAnnotation(
    string SignalId,
    string Symbol,
    string Timeframe,
    string SetupId,
    string Direction,
    double EntryPrice,
    double StopLoss,
    double TakeProfit1,
    double? TakeProfit2,
    double InvalidationLevel,
    double RiskReward,
    string AnnotationStyle,
    IReadOnlyList<string> Labels,
    DateTimeOffset CreatedAtUtc,
    string SignalStatus);

public sealed record ChartAnnotationExportReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    string SourceMode,
    bool EmbeddedSpecAvailable,
    int LoadedDemoSignals,
    int LoadedForwardTestObservations,
    int AnnotationCount,
    IReadOnlyList<ChartAnnotation> Annotations,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed class ChartAnnotationExportService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ChartAnnotationExportService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "chart_annotations");
    public string ReportPath => Path.Combine(Root, "chart_annotations.json");
    public string MarkdownPath => Path.Combine(Root, "chart_annotations.md");

    public ChartAnnotationExportReport LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return Run(dryRun: true);
        }

        try
        {
            var report = JsonSerializer.Deserialize<ChartAnnotationExportReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
            return report ?? Run(dryRun: true);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Run(dryRun: true);
        }
    }

    public ChartAnnotationExportReport Run(bool dryRun)
    {
        var embeddedChartAnnotationSpecJson = LoadEmbeddedChartAnnotationSpecJson();
        var embeddedAnnotations = TryBuildAnnotationsFromEmbeddedPackage(embeddedChartAnnotationSpecJson, out var embeddedWarnings);
        var demoFeed = new DemoSignalFeedService(_storagePaths, _runtimeRoot);
        var forwardTest = new ForwardTestService(_storagePaths, _runtimeRoot);
        var signals = demoFeed.LoadLatestSignals();
        var observations = forwardTest.LoadLatestObservations();
        var observationBySignalId = observations
            .GroupBy(item => item.SignalId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CreatedUtc).First(), StringComparer.OrdinalIgnoreCase);
        var annotations = new List<ChartAnnotation>();
        var warnings = new List<string>();

        if (embeddedAnnotations.Count > 0)
        {
            annotations.AddRange(embeddedAnnotations);
            warnings.AddRange(embeddedWarnings);
        }
        else
        {
            foreach (var signal in signals.OrderByDescending(item => item.CreatedUtc))
            {
                var observation = observationBySignalId.TryGetValue(signal.SignalId, out var value) ? value : null;
                var annotation = BuildAnnotation(signal, observation, warnings);
                annotations.Add(annotation);
            }

            if (signals.Count == 0)
            {
                warnings.Add("demo_signal_feed_empty");
            }

            if (observations.Count == 0)
            {
                warnings.Add("forward_test_observations_empty");
            }
        }

        var report = new ChartAnnotationExportReport(
            ReportVersion: "chart_annotation_export_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: dryRun ? "dry_run" : "exported",
            SourceMode: embeddedAnnotations.Count > 0 ? "embedded_spec" : "local_demo_forward_test",
            EmbeddedSpecAvailable: embeddedAnnotations.Count > 0,
            LoadedDemoSignals: signals.Count,
            LoadedForwardTestObservations: observations.Count,
            AnnotationCount: annotations.Count,
            Annotations: annotations,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);

        if (!dryRun)
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
            File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        }

        return report;
    }

    private string? LoadEmbeddedChartAnnotationSpecJson()
    {
        try
        {
            var generator = new CloudEmbeddedReleasePackageGeneratorService(_storagePaths, _runtimeRoot);
            var path = generator.OutputJsonPath;
            if (!File.Exists(path))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            return root.TryGetProperty("chart_annotation_spec_json", out var chartAnnotationSpec) && chartAnnotationSpec.ValueKind == JsonValueKind.String
                ? chartAnnotationSpec.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static List<ChartAnnotation> TryBuildAnnotationsFromEmbeddedPackage(string? chartAnnotationSpecJson, out List<string> warnings)
    {
        warnings = [];
        var annotations = new List<ChartAnnotation>();

        if (string.IsNullOrWhiteSpace(chartAnnotationSpecJson))
        {
            return annotations;
        }

        try
        {
            using var document = JsonDocument.Parse(chartAnnotationSpecJson);
            var root = document.RootElement;
            var annotationsElement = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("annotations", out var nested) && nested.ValueKind == JsonValueKind.Array
                ? nested
                : root.ValueKind == JsonValueKind.Array
                    ? root
                    : default;

            if (annotationsElement.ValueKind != JsonValueKind.Array)
            {
                warnings.Add("embedded_chart_annotation_spec_invalid");
                return annotations;
            }

            foreach (var annotationElement in annotationsElement.EnumerateArray())
            {
                if (annotationElement.ValueKind != JsonValueKind.Object)
                {
                    warnings.Add("embedded_chart_annotation_entry_invalid");
                    continue;
                }

                var signalId = ReadString(annotationElement, "signal_id") ?? string.Empty;
                var symbol = ReadString(annotationElement, "symbol") ?? string.Empty;
                var timeframe = ReadString(annotationElement, "timeframe") ?? string.Empty;
                var setupId = ReadString(annotationElement, "setup_id") ?? string.Empty;
                var direction = ReadString(annotationElement, "direction") ?? string.Empty;
                var annotationStyle = ReadString(annotationElement, "annotation_style") ?? "watch_flat";
                var signalStatus = ReadString(annotationElement, "signal_status") ?? "unknown";
                var labels = ReadStringArray(annotationElement, "labels");
                var createdAtUtc = ReadDateTime(annotationElement, "created_at_utc") ?? ReadDateTime(annotationElement, "created_at") ?? DateTimeOffset.UtcNow;

                if (!TryReadDouble(annotationElement, "entry_price", out var entryPrice)) continue;
                if (!TryReadDouble(annotationElement, "stop_loss", out var stopLoss)) continue;
                if (!TryReadDouble(annotationElement, "take_profit_1", out var takeProfit1) && !TryReadDouble(annotationElement, "take_profit1", out takeProfit1)) continue;
                var takeProfit2 = TryReadNullableDouble(annotationElement, "take_profit_2") ?? TryReadNullableDouble(annotationElement, "take_profit2");
                if (!TryReadDouble(annotationElement, "invalidation_level", out var invalidationLevel)) continue;
                if (!TryReadDouble(annotationElement, "risk_reward", out var riskReward)) continue;

                annotations.Add(new ChartAnnotation(
                    SignalId: signalId,
                    Symbol: symbol,
                    Timeframe: timeframe,
                    SetupId: setupId,
                    Direction: direction,
                    EntryPrice: entryPrice,
                    StopLoss: stopLoss,
                    TakeProfit1: takeProfit1,
                    TakeProfit2: takeProfit2,
                    InvalidationLevel: invalidationLevel,
                    RiskReward: riskReward,
                    AnnotationStyle: annotationStyle,
                    Labels: labels,
                    CreatedAtUtc: createdAtUtc,
                    SignalStatus: signalStatus));
            }
        }
        catch (JsonException)
        {
            warnings.Add("embedded_chart_annotation_spec_parse_failed");
        }

        return annotations;
    }

    private static string? ReadString(JsonElement element, string propertyName)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Array)
        {
            return property.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).Where(text => !string.IsNullOrWhiteSpace(text)).ToList();
        }

        return [];
    }

    private static DateTimeOffset? ReadDateTime(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            var text = property.GetString();
            if (!string.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static bool TryReadDouble(JsonElement element, string propertyName, out double value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.Number && property.TryGetDouble(out value))
            {
                return true;
            }

            if (property.ValueKind == JsonValueKind.String && double.TryParse(property.GetString(), out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static double? TryReadNullableDouble(JsonElement element, string propertyName)
        => TryReadDouble(element, propertyName, out var value) ? value : null;

    private static ChartAnnotation BuildAnnotation(DemoSignalFeedItem signal, ForwardTestObservation? observation, List<string> warnings)
    {
        var signalStatus = observation?.ObservedStatus ?? signal.Status;
        var direction = NormalizeDirection(signal.Direction);
        var entry = signal.EntryLevel;
        var stop = signal.StopLoss;
        var tp1 = signal.TakeProfit;
        var tp2 = CalculateTakeProfit2(direction, entry, stop, tp1, signal);
        var riskReward = CalculateRiskReward(direction, entry, stop, tp1);
        var style = BuildAnnotationStyle(signalStatus, direction);
        var labels = BuildLabels(signal, observation, riskReward);

        if (string.IsNullOrWhiteSpace(signal.SetupType))
        {
            warnings.Add($"setup_type_missing:{signal.SignalId}");
        }

        return new ChartAnnotation(
            SignalId: signal.SignalId,
            Symbol: signal.Asset,
            Timeframe: signal.Timeframe,
            SetupId: signal.CandidateId,
            Direction: direction,
            EntryPrice: entry,
            StopLoss: stop,
            TakeProfit1: tp1,
            TakeProfit2: tp2,
            InvalidationLevel: signal.InvalidationLevel,
            RiskReward: riskReward,
            AnnotationStyle: style,
            Labels: labels,
            CreatedAtUtc: signal.CreatedUtc,
            SignalStatus: signalStatus);
    }

    private static string NormalizeDirection(string direction)
    {
        var normalized = direction.Trim().ToLowerInvariant();
        return normalized switch
        {
            "long" => "long",
            "short" => "short",
            "long_short" => "long_short",
            _ => "flat"
        };
    }

    private static double? CalculateTakeProfit2(string direction, double entry, double stop, double tp1, DemoSignalFeedItem signal)
    {
        var isLong = direction.StartsWith("long", StringComparison.OrdinalIgnoreCase);
        var risk = Math.Abs(entry - stop);
        if (risk <= 0)
        {
            return null;
        }

        var tp2 = isLong ? entry + (risk * 2.0) : entry - (risk * 2.0);
        if (Math.Abs(tp2 - tp1) < risk * 0.05)
        {
            return null;
        }

        return Math.Round(tp2, signal.Asset.Equals("EURUSD", StringComparison.OrdinalIgnoreCase) ? 5 : 2);
    }

    private static double CalculateRiskReward(string direction, double entry, double stop, double tp1)
    {
        var risk = Math.Abs(entry - stop);
        var reward = Math.Abs(tp1 - entry);
        if (risk <= 0)
        {
            return 0;
        }

        return Math.Round(reward / risk, 4);
    }

    private static string BuildAnnotationStyle(string signalStatus, string direction)
    {
        var baseStyle = signalStatus switch
        {
            "waiting_for_trigger" => "pending",
            "triggered" or "active" => "active",
            "invalidated" => "invalidated",
            "expired" => "expired",
            "completed" => "completed",
            _ => "watch"
        };

        return $"{baseStyle}_{direction}";
    }

    private static IReadOnlyList<string> BuildLabels(DemoSignalFeedItem signal, ForwardTestObservation? observation, double riskReward)
    {
        var labels = new List<string>
        {
            signal.Asset,
            signal.Timeframe,
            signal.CandidateId,
            signal.SetupType,
            signal.Direction,
            $"confidence:{signal.Confidence:0.###}",
            $"rr:{riskReward:0.###}",
            $"status:{observation?.ObservedStatus ?? signal.Status}"
        };

        if (observation is not null)
        {
            labels.Add($"forward_test:{observation.HypotheticalResult}");
            labels.Add($"r_multiple:{observation.RMultiple?.ToString("0.###") ?? "n/a"}");
        }

        return labels;
    }

    private static string BuildMarkdown(ChartAnnotationExportReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Chart Annotation Export");
        builder.AppendLine();
        builder.AppendLine($"- report_version: {report.ReportVersion}");
        builder.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        builder.AppendLine($"- status: {report.Status}");
        builder.AppendLine($"- source_mode: {report.SourceMode}");
        builder.AppendLine($"- embedded_spec_available: {report.EmbeddedSpecAvailable.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- annotations: {report.AnnotationCount}");
        builder.AppendLine($"- no_auto_trading: {report.NoAutoTrading.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- human_review_required: {report.HumanReviewRequired.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- broker_orders_enabled: {report.BrokerOrdersEnabled.ToString().ToLowerInvariant()}");
        builder.AppendLine($"- live_trading_enabled: {report.LiveTradingEnabled.ToString().ToLowerInvariant()}");
        builder.AppendLine();

        foreach (var annotation in report.Annotations)
        {
            builder.AppendLine($"## {annotation.SignalId}");
            builder.AppendLine($"- symbol: {annotation.Symbol}");
            builder.AppendLine($"- timeframe: {annotation.Timeframe}");
            builder.AppendLine($"- setup_id: {annotation.SetupId}");
            builder.AppendLine($"- direction: {annotation.Direction}");
            builder.AppendLine($"- entry_price: {annotation.EntryPrice:0.#####}");
            builder.AppendLine($"- stop_loss: {annotation.StopLoss:0.#####}");
            builder.AppendLine($"- take_profit_1: {annotation.TakeProfit1:0.#####}");
            builder.AppendLine($"- take_profit_2: {(annotation.TakeProfit2.HasValue ? annotation.TakeProfit2.Value.ToString("0.#####") : "n/a")}");
            builder.AppendLine($"- invalidation_level: {annotation.InvalidationLevel:0.#####}");
            builder.AppendLine($"- risk_reward: {annotation.RiskReward:0.###}");
            builder.AppendLine($"- annotation_style: {annotation.AnnotationStyle}");
            builder.AppendLine($"- created_at: {annotation.CreatedAtUtc:O}");
            builder.AppendLine($"- signal_status: {annotation.SignalStatus}");
            builder.AppendLine($"- labels: {string.Join(", ", annotation.Labels)}");
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
}
