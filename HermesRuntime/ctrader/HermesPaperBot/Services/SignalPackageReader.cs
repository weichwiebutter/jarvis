using System;
using System.Globalization;
using System.Text.Json;
using HermesPaperBot.Models;

namespace HermesPaperBot.Services;

/// <summary>
/// Reads an embedded signal decision from the generated package JSON only.
/// </summary>
public sealed class SignalPackageReader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Reads the embedded signal decision from the package JSON.
    /// </summary>
    public SignalDecision? Read(CloudEmbeddedReleasePackage? package, out string[] warnings)
    {
        if (package is null)
        {
            warnings = ["embedded_package_missing"];
            return null;
        }

        return Read(package.PackageJson, out warnings);
    }

    /// <summary>
    /// Reads the embedded signal decision from package JSON.
    /// </summary>
    public SignalDecision? Read(string? packageJson, out string[] warnings)
    {
        var collectedWarnings = new System.Collections.Generic.List<string>();

        if (string.IsNullOrWhiteSpace(packageJson))
        {
            warnings = ["embedded_package_json_missing"];
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(packageJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("signal_decision", out var signalElement) || signalElement.ValueKind != JsonValueKind.Object)
            {
                warnings = [];
                return null;
            }

            if (!TryGetDirection(signalElement, "direction", out var direction))
            {
                collectedWarnings.Add("signal_direction_missing");
            }

            if (!TryGetDecimal(signalElement, "confidence", out var confidence))
            {
                collectedWarnings.Add("signal_confidence_missing");
            }

            if (!TryGetString(signalElement, "strategy_id", out var strategyId))
            {
                collectedWarnings.Add("signal_strategy_id_missing");
            }

            if (!TryGetDateTime(signalElement, "signal_timestamp_utc", out var signalTimestampUtc))
            {
                collectedWarnings.Add("signal_timestamp_missing");
            }

            if (!TryGetDateTime(signalElement, "expiry_utc", out var expiryUtc))
            {
                collectedWarnings.Add("signal_expiry_missing");
            }

            if (!TryGetString(signalElement, "reason", out var reason))
            {
                collectedWarnings.Add("signal_reason_missing");
            }

            if (collectedWarnings.Count > 0)
            {
                warnings = collectedWarnings.ToArray();
                return null;
            }

            warnings = [];
            return new SignalDecision
            {
                Direction = direction,
                Confidence = confidence,
                StrategyId = strategyId,
                SignalTimestampUtc = signalTimestampUtc,
                ExpiryUtc = expiryUtc,
                Reason = reason,
            };
        }
        catch (JsonException)
        {
            warnings = ["signal_package_json_invalid"];
            return null;
        }
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

    private static bool TryGetDateTime(JsonElement element, string propertyName, out DateTimeOffset value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
        {
            var text = property.GetString();
            if (!string.IsNullOrWhiteSpace(text) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                value = parsed;
                return true;
            }
        }

        value = DateTimeOffset.MinValue;
        return false;
    }

    private static bool TryGetDirection(JsonElement element, string propertyName, out SignalDirection direction)
    {
        direction = SignalDirection.Flat;
        if (!TryGetString(element, propertyName, out var text))
        {
            return false;
        }

        if (Enum.TryParse<SignalDirection>(text, ignoreCase: true, out var parsed))
        {
            direction = parsed;
            return true;
        }

        if (string.Equals(text, "long", StringComparison.OrdinalIgnoreCase))
        {
            direction = SignalDirection.Long;
            return true;
        }

        if (string.Equals(text, "short", StringComparison.OrdinalIgnoreCase))
        {
            direction = SignalDirection.Short;
            return true;
        }

        if (string.Equals(text, "flat", StringComparison.OrdinalIgnoreCase))
        {
            direction = SignalDirection.Flat;
            return true;
        }

        return false;
    }
}
