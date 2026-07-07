using System.Globalization;
using System.Linq;
using System.Text.Json;
using HermesPaperBot.Models;

namespace HermesPaperBot.Services;

/// <summary>
/// Reads embedded chart annotation specs from the generated package JSON only.
/// </summary>
public sealed class EmbeddedChartAnnotationSpecReader
{
    /// <summary>
    /// Reads embedded chart annotations from the package.
    /// </summary>
    public ChartAnnotationSpec[] Read(CloudEmbeddedReleasePackage? package, out string[] warnings)
    {
        if (package is null)
        {
            warnings = ["embedded_package_missing"];
            return [];
        }

        return Read(package.ChartAnnotationSpecJson, out warnings);
    }

    /// <summary>
    /// Reads embedded chart annotations from JSON.
    /// </summary>
    public ChartAnnotationSpec[] Read(string? chartAnnotationSpecJson, out string[] warnings)
    {
        var collectedWarnings = new List<string>();
        var result = new List<ChartAnnotationSpec>();

        if (string.IsNullOrWhiteSpace(chartAnnotationSpecJson))
        {
            warnings = ["embedded_chart_annotation_spec_missing"];
            return [];
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
                warnings = ["embedded_chart_annotation_spec_invalid"];
                return [];
            }

            foreach (var annotationElement in annotationsElement.EnumerateArray())
            {
                if (annotationElement.ValueKind != JsonValueKind.Object)
                {
                    collectedWarnings.Add("embedded_chart_annotation_entry_invalid");
                    continue;
                }

                if (!TryGetString(annotationElement, "signal_id", out var signalId)) signalId = string.Empty;
                if (!TryGetString(annotationElement, "symbol", out var symbol)) symbol = string.Empty;
                if (!TryGetString(annotationElement, "timeframe", out var timeframe)) timeframe = string.Empty;
                if (!TryGetString(annotationElement, "setup_id", out var setupId)) setupId = string.Empty;
                if (!TryGetString(annotationElement, "direction", out var direction)) direction = string.Empty;
                if (!TryGetDecimal(annotationElement, "entry_price", out var entryPrice))
                {
                    collectedWarnings.Add($"chart_annotation_entry_price_missing:{signalId}");
                    continue;
                }
                if (!TryGetDecimal(annotationElement, "stop_loss", out var stopLoss))
                {
                    collectedWarnings.Add($"chart_annotation_stop_loss_missing:{signalId}");
                    continue;
                }
                if (!TryGetDecimal(annotationElement, "take_profit_1", out var takeProfit1) && !TryGetDecimal(annotationElement, "take_profit1", out takeProfit1))
                {
                    collectedWarnings.Add($"chart_annotation_take_profit_1_missing:{signalId}");
                    continue;
                }
                if (!TryGetDecimal(annotationElement, "invalidation_level", out var invalidationLevel))
                {
                    collectedWarnings.Add($"chart_annotation_invalidation_missing:{signalId}");
                    continue;
                }
                if (!TryGetDecimal(annotationElement, "risk_reward", out var riskReward))
                {
                    collectedWarnings.Add($"chart_annotation_risk_reward_missing:{signalId}");
                    continue;
                }

                var takeProfit2 = TryGetNullableDecimal(annotationElement, "take_profit_2") ?? TryGetNullableDecimal(annotationElement, "take_profit2");
                var annotationStyle = TryGetString(annotationElement, "annotation_style", out var style) ? style : "watch_flat";
                var signalStatus = TryGetString(annotationElement, "signal_status", out var status) ? status : "unknown";
                var labels = TryGetStringArray(annotationElement, "labels");
                var createdAtUtc = TryGetDateTime(annotationElement, "created_at_utc") ?? TryGetDateTime(annotationElement, "created_at") ?? DateTimeOffset.UtcNow;

                result.Add(new ChartAnnotationSpec(
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
            warnings = ["embedded_chart_annotation_spec_parse_failed"];
            return [];
        }

        warnings = collectedWarnings.ToArray();
        return result.ToArray();
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetDecimal(JsonElement element, string propertyName, out decimal value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out value))
            {
                return true;
            }

            if (property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
        }

        value = 0m;
        return false;
    }

    private static decimal? TryGetNullableDecimal(JsonElement element, string propertyName)
        => TryGetDecimal(element, propertyName, out var value) ? value : null;

    private static DateTimeOffset? TryGetDateTime(JsonElement element, string propertyName)
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

    private static IReadOnlyList<string> TryGetStringArray(JsonElement element, string propertyName)
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
}
