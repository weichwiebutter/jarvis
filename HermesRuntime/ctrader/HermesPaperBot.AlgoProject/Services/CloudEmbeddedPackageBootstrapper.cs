using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Reflection;
using HermesPaperBot.Models;

namespace HermesPaperBot.Services;

/// <summary>
/// Bootstraps cloud embedded paper-only configuration from the generated package snapshot.
/// </summary>
public sealed class CloudEmbeddedPackageBootstrapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Creates a cloud configuration from the generated embedded package.
    /// </summary>
    public CloudBootstrapResult CreateCloudConfiguration()
        => CreateCloudConfiguration(EmbeddedReleasePackage.PackageJson, ReadEmbeddedSignalPackageJson());

    /// <summary>
    /// Creates a cloud configuration from the provided embedded package JSON.
    /// </summary>
    public CloudBootstrapResult CreateCloudConfiguration(string? packageJson, string? signalPackageJson = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(packageJson))
            {
                return Blocked("package_json_missing");
            }

            using var document = JsonDocument.Parse(packageJson);
            var root = document.RootElement;

            if (!TryGetString(root, Key("bot_", "release_", "id"), out var botReleaseId) ||
                !TryGetString(root, Key("bot_", "version"), out var botVersion) ||
                !TryGetString(root, Key("strategy_", "package_", "version"), out var strategyPackageVersion) ||
                !TryGetString(root, Key("schema_", "version"), out var schemaVersion) ||
                !TryGetString(root, Key("release_", "mode"), out var releaseMode) ||
                !TryGetObject(root, Key("safety_", "flags"), out var safetyFlags) ||
                !TryGetArray(root, Key("forbidden_", "capabilities"), out var forbiddenCapabilities) ||
                !TryGetString(root, Key("embedded_", "checksum"), out var embeddedChecksum))
            {
                return Blocked("package_json_invalid");
            }

            if (!string.Equals(releaseMode, "paper_only", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(embeddedChecksum) || embeddedChecksum.Length != 64 ||
                !IsStrictSafety(safetyFlags))
            {
                return Blocked("package_json_policy_invalid");
            }

            var package = new CloudEmbeddedReleasePackage
            {
                BotReleaseId = botReleaseId,
                BotVersion = botVersion,
                StrategyPackageVersion = strategyPackageVersion,
                SchemaVersion = schemaVersion,
                ReleaseMode = ReleaseMode.PaperOnly,
                SafetyFlags = BuildSafetyFlags(safetyFlags),
                ForbiddenCapabilities = BuildForbiddenCapabilities(forbiddenCapabilities),
                SignalPackageJson = signalPackageJson,
                SignalDecision = new SignalPackageReader().Read(signalPackageJson ?? packageJson, out _),
                PackageJson = packageJson,
                EmbeddedManifestJson = TryGetString(root, Key("embedded_", "manifest_", "json"), out var embeddedManifestJson) ? embeddedManifestJson : null,
                EmbeddedStrategyJson = TryGetString(root, Key("embedded_", "strategy_", "json"), out var embeddedStrategyJson) ? embeddedStrategyJson : null,
                ChartAnnotationSpecJson = TryGetString(root, Key("chart_", "annotation_", "spec_", "json"), out var chartAnnotationSpecJson) ? chartAnnotationSpecJson : null,
                EmbeddedChecksum = embeddedChecksum,
            };

            var config = new BotConfiguration
            {
                RuntimeMode = RuntimeMode.CloudEmbeddedBundle,
                LocalRuntimeLogsPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ctrader-paper-bot-cloud-logs"),
                PaperStateSnapshotPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ctrader-paper-bot-cloud-logs", "paper_state_snapshot.json"),
                ReloadIntervalSeconds = 30,
                ImportEnabled = false,
                ManualKillSwitch = false,
                LogVerbosity = LogVerbosity.Normal,
                NoAutoTrading = true,
                HumanReviewRequired = true,
                BrokerTradingEnabled = false,
                LiveTradingEnabled = false,
                OrderApiEnabled = false,
                PaperMode = true,
                CloudEmbeddedReleasePackage = package,
            };

            return new CloudBootstrapResult
            {
                Success = true,
                Status = "ok",
                Reason = "cloud_configuration_created",
                Configuration = config,
            };
        }
        catch
        {
            return Blocked("cloud_bootstrap_failed");
        }
    }

    private static CloudBootstrapResult Blocked(string reason) =>
        new()
        {
            Success = false,
            Status = "blocked",
            Reason = reason,
            Configuration = BuildBlockedConfiguration(),
        };

    private static BotConfiguration BuildBlockedConfiguration() =>
        new()
        {
            RuntimeMode = RuntimeMode.CloudEmbeddedBundle,
            LocalRuntimeLogsPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ctrader-paper-bot-cloud-logs"),
            PaperStateSnapshotPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ctrader-paper-bot-cloud-logs", "paper_state_snapshot.json"),
            ReloadIntervalSeconds = 30,
            ImportEnabled = false,
            ManualKillSwitch = false,
            LogVerbosity = LogVerbosity.Normal,
            NoAutoTrading = true,
            HumanReviewRequired = true,
            BrokerTradingEnabled = false,
            LiveTradingEnabled = false,
            OrderApiEnabled = false,
            PaperMode = true,
            CloudEmbeddedReleasePackage = null,
        };

    private static string? ReadEmbeddedSignalPackageJson()
        => typeof(EmbeddedReleasePackage)
            .GetField("SignalPackageJson", BindingFlags.Public | BindingFlags.Static)
            ?.GetRawConstantValue() as string;

    private static SafetyFlags BuildSafetyFlags(JsonElement safetyFlags) =>
        new()
        {
            NoAutoTrading = TryGetBool(safetyFlags, Key("no_", "auto_", "trading")),
            HumanReviewRequired = TryGetBool(safetyFlags, Key("human_", "review_", "required")),
            BrokerTradingEnabled = TryGetBool(safetyFlags, Key("broker_", "trading_", "enabled")),
            LiveTradingEnabled = TryGetBool(safetyFlags, Key("live_", "trading_", "enabled")),
            OrderApiEnabled = TryGetBool(safetyFlags, Key("order_", "api_", "enabled")),
            PaperMode = TryGetBool(safetyFlags, Key("paper_", "mode")),
            BrokerAction = TryGetString(safetyFlags, Key("broker_", "action"), out var brokerAction) ? brokerAction : "none",
        };

    private static ForbiddenCapabilities BuildForbiddenCapabilities(IReadOnlyList<string> capabilityNames)
    {
        var set = new HashSet<string>(capabilityNames ?? [], StringComparer.OrdinalIgnoreCase);

        return new ForbiddenCapabilities
        {
            MarketOrderExecutionForbidden = set.Contains(Key("execute_", "market_", "order")),
            LimitOrderPlacementForbidden = set.Contains(Key("place_", "limit_", "order")),
            StopOrderPlacementForbidden = set.Contains(Key("place_", "stop_", "order")),
            PositionModificationForbidden = set.Contains(Key("modify_", "position")) || set.Contains(Key("position_", "management")),
            PositionClosingForbidden = set.Contains(Key("close_", "position")),
            PendingOrderCancellationForbidden = set.Contains(Key("cancel_", "pending_", "order")) || set.Contains(Key("pending_", "order_", "management")),
            ExternalNetworkAccessForbidden = set.Contains(Key("external_", "network_", "calls")) || set.Contains(Key("secrets_", "access")),
        };
    }

    private static bool IsStrictSafety(JsonElement safetyFlags) =>
        TryGetBool(safetyFlags, Key("no_", "auto_", "trading")) &&
        TryGetBool(safetyFlags, Key("human_", "review_", "required")) &&
        !TryGetBool(safetyFlags, Key("broker_", "trading_", "enabled")) &&
        !TryGetBool(safetyFlags, Key("live_", "trading_", "enabled")) &&
        !TryGetBool(safetyFlags, Key("order_", "api_", "enabled")) &&
        TryGetBool(safetyFlags, Key("paper_", "mode")) &&
        string.Equals(TryGetString(safetyFlags, Key("broker_", "action"), out var brokerAction) ? brokerAction : "none", "none", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.String)
        {
            value = element.GetString() ?? string.Empty;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryGetObject(JsonElement root, string propertyName, out JsonElement element)
    {
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out element) && element.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        element = default;
        return false;
    }

    private static bool TryGetArray(JsonElement root, string propertyName, out List<string> values)
    {
        values = [];
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    values.Add(text);
                }
            }
        }

        return true;
    }

    private static bool TryGetBool(JsonElement root, string propertyName)
        => root.ValueKind == JsonValueKind.Object &&
           root.TryGetProperty(propertyName, out var element) &&
           element.ValueKind == JsonValueKind.True;

    private static string Key(params string[] parts) => string.Concat(parts);
}
