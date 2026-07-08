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

        return Read(package.SignalPackageJson ?? package.PackageJson, null, out warnings);
    }

    /// <summary>
    /// Reads the embedded signal decision from the package JSON for a specific symbol.
    /// </summary>
    public SignalDecision? Read(CloudEmbeddedReleasePackage? package, string? symbol, out string[] warnings)
    {
        if (package is null)
        {
            warnings = ["embedded_package_missing"];
            return null;
        }

        return Read(package.SignalPackageJson ?? package.PackageJson, symbol, out warnings);
    }

    /// <summary>
    /// Reads the embedded signal decision from package JSON.
    /// </summary>
    public SignalDecision? Read(string? packageJson, out string[] warnings)
        => Read(packageJson, null, out warnings);

    /// <summary>
    /// Reads the embedded signal decision from package JSON for a specific symbol.
    /// </summary>
    public SignalDecision? Read(string? packageJson, string? symbol, out string[] warnings)
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
            if (!TrySelectSignalElement(root, symbol, out var signalElement))
            {
                warnings = ["no_matching_signal"];
                return null;
            }

            if (!TryGetDirection(signalElement, "direction", out var direction))
            {
                collectedWarnings.Add("signal_direction_missing");
            }

            if (!TryGetDecimal(signalElement, "confidence", out var confidence) &&
                !TryGetDecimal(signalElement, "confidence_baseline", out confidence))
            {
                collectedWarnings.Add("signal_confidence_missing");
            }

            if (!TryGetString(signalElement, "strategy_id", out var strategyId) &&
                !TryGetString(signalElement, "setup_id", out strategyId) &&
                !TryGetString(signalElement, "signal_id", out strategyId))
            {
                collectedWarnings.Add("signal_strategy_id_missing");
            }

            if (!TryGetDateTime(signalElement, "signal_timestamp_utc", out var signalTimestampUtc) &&
                !TryGetDateTime(root, "updated_at_utc", out signalTimestampUtc) &&
                !TryGetDateTime(root, "generated_at_utc", out signalTimestampUtc))
            {
                signalTimestampUtc = DateTimeOffset.UtcNow;
            }

            var expiryUtc = TryGetDateTime(signalElement, "expiry_utc", out var expiry) ? expiry : signalTimestampUtc.AddHours(1);

            if (!TryGetString(signalElement, "reason", out var reason) &&
                !TryGetString(signalElement, "paper_decision", out reason))
            {
                reason = "signal_package_loaded";
            }

            var stopLossPrice = TryGetOptionalDecimal(signalElement, "stop_loss_price");
            var takeProfitPrice = TryGetOptionalDecimal(signalElement, "take_profit_price");
            var maxHoldingSeconds = TryGetOptionalInt(signalElement, "max_holding_seconds");
            var riskR = TryGetOptionalDecimal(signalElement, "risk_r");

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
                StopLossPrice = stopLossPrice,
                TakeProfitPrice = takeProfitPrice,
                MaxHoldingSeconds = maxHoldingSeconds,
                RiskR = riskR,
            };
        }
        catch (JsonException)
        {
            warnings = ["signal_package_json_invalid"];
            return null;
        }
    }

    private static bool TrySelectSignalElement(JsonElement root, string? symbol, out JsonElement signalElement)
    {
        signalElement = default;

        if (string.IsNullOrWhiteSpace(symbol))
        {
            if (root.TryGetProperty("signal_decision", out var defaultSignal) && defaultSignal.ValueKind == JsonValueKind.Object)
            {
                signalElement = defaultSignal;
                return true;
            }

            if (root.TryGetProperty("signals", out var defaultSignals) && defaultSignals.ValueKind == JsonValueKind.Array)
            {
                foreach (var candidate in defaultSignals.EnumerateArray())
                {
                    if (candidate.ValueKind == JsonValueKind.Object)
                    {
                        signalElement = candidate;
                        return true;
                    }
                }
            }

            return false;
        }

        if (root.TryGetProperty("signal_decision", out var signalDecision) &&
            signalDecision.ValueKind == JsonValueKind.Object &&
            IsSymbolMatch(signalDecision, symbol))
        {
            signalElement = signalDecision;
            return true;
        }

        if (root.TryGetProperty("signals", out var signalsElement) && signalsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var candidate in signalsElement.EnumerateArray())
            {
                if (candidate.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (IsSymbolMatch(candidate, symbol))
                {
                    signalElement = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsSymbolMatch(JsonElement signalElement, string? symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return true;
        }

        if (TryGetString(signalElement, "strategy_id", out var strategyId) &&
            strategyId.Contains(symbol, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TryGetString(signalElement, "asset", out var asset) &&
            asset.Equals(symbol, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TryGetString(signalElement, "setup_id", out var setupId) &&
            setupId.Contains(symbol, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (TryGetString(signalElement, "signal_id", out var signalId) &&
            signalId.Contains(symbol, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
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

    private static decimal? TryGetOptionalDecimal(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String && decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static int? TryGetOptionalInt(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property))
        {
            if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value))
            {
                return value;
            }

            if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
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

        if (text.Contains("long", StringComparison.OrdinalIgnoreCase) && text.Contains("short", StringComparison.OrdinalIgnoreCase))
        {
            direction = SignalDirection.Flat;
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
