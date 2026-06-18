namespace HermesPaperBot.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HermesPaperBot.Models;
using HermesPaperBot.Services;

/// <summary>
/// In-memory harness for paper runtime orchestrator checks.
/// </summary>
public static class PaperRuntimeOrchestratorHarness
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>
    /// Runs the harness and returns JSON output.
    /// </summary>
    public static string Run()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ctrader-paper-bot-harness", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            var results = new List<object>();

            results.Add(RunValidCase(tempRoot));
            results.Add(RunInvalidConfigCase(tempRoot));
            results.Add(RunSafetyViolationCase(tempRoot));
            results.Add(RunMissingBundleFileCase(tempRoot));
            results.Add(RunChecksumMismatchCase(tempRoot));
            results.Add(RunValidWithLoggingCase(tempRoot));
            results.Add(RunInvalidConfigWithKillSwitchLoggingCase(tempRoot));

            return JsonSerializer.Serialize(results, JsonOptions);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static object RunValidCase(string tempRoot)
    {
        var bundleDir = Path.Combine(tempRoot, "valid");
        BuildFakeBundle(bundleDir, tamperChecksum: false, removeSchema: false);

        var result = new PaperRuntimeOrchestrator().RunStep(BuildValidConfig(bundleDir));
        return BuildReport("valid_config_valid_bundle", result, result.Success && result.PaperDecision == "would_wait" && !result.KillSwitchActive);
    }

    private static object RunInvalidConfigCase(string tempRoot)
    {
        var bundleDir = Path.Combine(tempRoot, "invalid_config");
        BuildFakeBundle(bundleDir, tamperChecksum: false, removeSchema: false);

        var config = BuildValidConfig(bundleDir);
        config = new BotConfiguration
        {
            ReleaseBundleInboxPath = config.ReleaseBundleInboxPath,
            ActiveReleaseBundlePath = config.ActiveReleaseBundlePath,
            LastValidReleaseBundlePath = config.LastValidReleaseBundlePath,
            LocalRuntimeLogsPath = config.LocalRuntimeLogsPath,
            ReloadIntervalSeconds = 1,
            ImportEnabled = config.ImportEnabled,
            ManualKillSwitch = config.ManualKillSwitch,
            LogVerbosity = config.LogVerbosity,
            NoAutoTrading = config.NoAutoTrading,
            HumanReviewRequired = config.HumanReviewRequired,
            BrokerTradingEnabled = config.BrokerTradingEnabled,
            LiveTradingEnabled = config.LiveTradingEnabled,
            OrderApiEnabled = config.OrderApiEnabled,
            PaperMode = config.PaperMode,
        };
        var result = new PaperRuntimeOrchestrator().RunStep(config);
        return BuildReport("invalid_config", result, !result.ConfigValid && result.KillSwitchActive && result.PaperDecision == "would_block_by_safety");
    }

    private static object RunSafetyViolationCase(string tempRoot)
    {
        var bundleDir = Path.Combine(tempRoot, "safety_violation");
        BuildFakeBundle(bundleDir, tamperChecksum: false, removeSchema: false);

        var config = BuildValidConfig(bundleDir);
        config = new BotConfiguration
        {
            ReleaseBundleInboxPath = config.ReleaseBundleInboxPath,
            ActiveReleaseBundlePath = config.ActiveReleaseBundlePath,
            LastValidReleaseBundlePath = config.LastValidReleaseBundlePath,
            LocalRuntimeLogsPath = config.LocalRuntimeLogsPath,
            ReloadIntervalSeconds = config.ReloadIntervalSeconds,
            ImportEnabled = config.ImportEnabled,
            ManualKillSwitch = config.ManualKillSwitch,
            LogVerbosity = config.LogVerbosity,
            NoAutoTrading = config.NoAutoTrading,
            HumanReviewRequired = config.HumanReviewRequired,
            BrokerTradingEnabled = config.BrokerTradingEnabled,
            LiveTradingEnabled = config.LiveTradingEnabled,
            OrderApiEnabled = config.OrderApiEnabled,
            PaperMode = false,
        };
        var result = new PaperRuntimeOrchestrator().RunStep(config);
        return BuildReport("safety_violation", result, !result.SafetyAllowed && result.KillSwitchActive && result.BrokerAction == "none");
    }

    private static object RunMissingBundleFileCase(string tempRoot)
    {
        var bundleDir = Path.Combine(tempRoot, "missing_bundle");
        BuildFakeBundle(bundleDir, tamperChecksum: false, removeSchema: true);

        var result = new PaperRuntimeOrchestrator().RunStep(BuildValidConfig(bundleDir));
        return BuildReport("missing_bundle_file", result, !result.ImportValid && result.DisabledUntilValidBundle && result.BrokerAction == "none");
    }

    private static object RunChecksumMismatchCase(string tempRoot)
    {
        var bundleDir = Path.Combine(tempRoot, "checksum_mismatch");
        BuildFakeBundle(bundleDir, tamperChecksum: true, removeSchema: false);

        var result = new PaperRuntimeOrchestrator().RunStep(BuildValidConfig(bundleDir));
        return BuildReport("checksum_mismatch", result, !result.ChecksumValid && !result.BundleValid && result.BrokerAction == "none");
    }

    private static object RunValidWithLoggingCase(string tempRoot)
    {
        var bundleDir = Path.Combine(tempRoot, "valid_logging");
        var logsDir = Path.Combine(tempRoot, "logs_valid");
        BuildFakeBundle(bundleDir, tamperChecksum: false, removeSchema: false);

        var config = BuildValidConfig(bundleDir);
        config = new BotConfiguration
        {
            ReleaseBundleInboxPath = config.ReleaseBundleInboxPath,
            ActiveReleaseBundlePath = config.ActiveReleaseBundlePath,
            LastValidReleaseBundlePath = config.LastValidReleaseBundlePath,
            LocalRuntimeLogsPath = logsDir,
            ReloadIntervalSeconds = config.ReloadIntervalSeconds,
            ImportEnabled = config.ImportEnabled,
            ManualKillSwitch = config.ManualKillSwitch,
            LogVerbosity = config.LogVerbosity,
            NoAutoTrading = config.NoAutoTrading,
            HumanReviewRequired = config.HumanReviewRequired,
            BrokerTradingEnabled = config.BrokerTradingEnabled,
            LiveTradingEnabled = config.LiveTradingEnabled,
            OrderApiEnabled = config.OrderApiEnabled,
            PaperMode = config.PaperMode,
        };

        var result = new PaperRuntimeOrchestrator().RunStep(config);
        var stepLogExists = File.Exists(Path.Combine(logsDir, "paper_runtime_step_log.jsonl"));
        var summaryExists = File.Exists(Path.Combine(logsDir, "bot_runtime_summary.json"));
        var killLogExists = File.Exists(Path.Combine(logsDir, "kill_switch_events.jsonl"));

        return new
        {
            test_name = "valid_config_valid_bundle_with_logging",
            passed = result.Success && result.PaperDecision == "would_wait" && !result.KillSwitchActive && stepLogExists && summaryExists && !killLogExists,
            key_fields = new
            {
                result.Success,
                result.State,
                result.ConfigValid,
                result.ImportAttempted,
                result.ImportValid,
                result.BundleValid,
                result.ChecksumValid,
                result.SafetyAllowed,
                result.DriftAllowed,
                result.KillSwitchActive,
                result.FallbackPossible,
                result.DisabledUntilValidBundle,
                result.PaperDecision,
                result.BrokerAction,
                result.LoggingStatus,
                step_log_exists = stepLogExists,
                summary_exists = summaryExists,
                kill_log_exists = killLogExists,
            },
        };
    }

    private static object RunInvalidConfigWithKillSwitchLoggingCase(string tempRoot)
    {
        var bundleDir = Path.Combine(tempRoot, "invalid_config_logging");
        var logsDir = Path.Combine(tempRoot, "logs_invalid");
        BuildFakeBundle(bundleDir, tamperChecksum: false, removeSchema: false);

        var config = BuildValidConfig(bundleDir);
        config = new BotConfiguration
        {
            ReleaseBundleInboxPath = config.ReleaseBundleInboxPath,
            ActiveReleaseBundlePath = config.ActiveReleaseBundlePath,
            LastValidReleaseBundlePath = config.LastValidReleaseBundlePath,
            LocalRuntimeLogsPath = logsDir,
            ReloadIntervalSeconds = 1,
            ImportEnabled = config.ImportEnabled,
            ManualKillSwitch = config.ManualKillSwitch,
            LogVerbosity = config.LogVerbosity,
            NoAutoTrading = config.NoAutoTrading,
            HumanReviewRequired = config.HumanReviewRequired,
            BrokerTradingEnabled = config.BrokerTradingEnabled,
            LiveTradingEnabled = config.LiveTradingEnabled,
            OrderApiEnabled = config.OrderApiEnabled,
            PaperMode = config.PaperMode,
        };

        var result = new PaperRuntimeOrchestrator().RunStep(config);
        var killLogExists = File.Exists(Path.Combine(logsDir, "kill_switch_events.jsonl"));

        return new
        {
            test_name = "invalid_config_writes_kill_switch_event",
            passed = !result.ConfigValid && result.KillSwitchActive && result.PaperDecision == "would_block_by_safety" && killLogExists,
            key_fields = new
            {
                result.Success,
                result.State,
                result.ConfigValid,
                result.ImportAttempted,
                result.ImportValid,
                result.BundleValid,
                result.ChecksumValid,
                result.SafetyAllowed,
                result.DriftAllowed,
                result.KillSwitchActive,
                result.FallbackPossible,
                result.DisabledUntilValidBundle,
                result.PaperDecision,
                result.BrokerAction,
                result.LoggingStatus,
                kill_log_exists = killLogExists,
            },
        };
    }

    private static object BuildReport(string testName, RuntimeStepResult result, bool passed) =>
        new
        {
            test_name = testName,
            passed,
            key_fields = new
            {
                result.Success,
                result.State,
                result.ConfigValid,
                result.ImportAttempted,
                result.ImportValid,
                result.BundleValid,
                result.ChecksumValid,
                result.SafetyAllowed,
                result.DriftAllowed,
                result.KillSwitchActive,
                result.FallbackPossible,
                result.DisabledUntilValidBundle,
                result.PaperDecision,
                result.BrokerAction,
            },
        };

    private static BotConfiguration BuildValidConfig(string bundleDir) =>
        new()
        {
            ReleaseBundleInboxPath = bundleDir,
            ActiveReleaseBundlePath = Path.Combine(bundleDir, "active"),
            LastValidReleaseBundlePath = Path.Combine(bundleDir, "last_valid"),
            LocalRuntimeLogsPath = Path.Combine(bundleDir, "logs"),
            ReloadIntervalSeconds = 30,
            ImportEnabled = true,
            ManualKillSwitch = false,
            LogVerbosity = LogVerbosity.Normal,
            NoAutoTrading = true,
            HumanReviewRequired = true,
            BrokerTradingEnabled = false,
            LiveTradingEnabled = false,
            OrderApiEnabled = false,
            PaperMode = true,
        };

    private static void BuildFakeBundle(string bundleDir, bool tamperChecksum, bool removeSchema)
    {
        Directory.CreateDirectory(bundleDir);

        var manifest = new ReleaseBundleManifest
        {
            BotReleaseId = "release-001",
            BotVersion = "0.1.0-paper",
            StrategyPackageVersion = "1.0.0",
            SchemaVersion = "1.0.0",
            ReleaseMode = ReleaseMode.PaperOnly,
            SafetyFlags = new SafetyFlags
            {
                NoAutoTrading = true,
                HumanReviewRequired = true,
                BrokerTradingEnabled = false,
                LiveTradingEnabled = false,
                OrderApiEnabled = false,
                PaperMode = true,
                BrokerAction = "none",
            },
            ForbiddenCapabilities = new ForbiddenCapabilities
            {
                MarketOrderExecutionForbidden = true,
                LimitOrderPlacementForbidden = true,
                StopOrderPlacementForbidden = true,
                PositionModificationForbidden = true,
                PositionClosingForbidden = true,
                PendingOrderCancellationForbidden = true,
                ExternalNetworkAccessForbidden = true,
            },
        };

        var provenance = new ProvenanceInfo
        {
            ProvenanceId = "prov-001",
            GeneratedAt = "2026-01-01T00:00:00Z",
            SourceSystem = "HermesRuntime",
            PaperMode = true,
            BotReleaseId = "release-001",
            BotVersion = "0.1.0-paper",
            StrategyPackageVersion = "1.0.0",
            SchemaVersion = "1.0.0",
        };

        var signalPackage = "{\"schema_version\":\"1.0.0\",\"paper_mode\":true}";
        var schema = "{\"type\":\"object\"}";

        File.WriteAllText(Path.Combine(bundleDir, "ctrader_bot_release_manifest.json"), JsonSerializer.Serialize(manifest, JsonOptions));
        File.WriteAllText(Path.Combine(bundleDir, "provenance.json"), JsonSerializer.Serialize(provenance, JsonOptions));
        File.WriteAllText(Path.Combine(bundleDir, "ensemble_signal_agent_package.json"), signalPackage);
        File.WriteAllText(Path.Combine(bundleDir, "ensemble_signal_agent_package.schema.json"), schema);

        if (removeSchema)
        {
            File.Delete(Path.Combine(bundleDir, "ensemble_signal_agent_package.schema.json"));
        }

        var checksumEntries = BuildChecksumEntries(bundleDir, removeSchema);
        if (tamperChecksum)
        {
            checksumEntries[0] = new ChecksumEntry
            {
                Path = checksumEntries[0].Path,
                Sha256 = new string('0', 64),
                SizeBytes = checksumEntries[0].SizeBytes,
                GeneratedAt = checksumEntries[0].GeneratedAt,
                Required = checksumEntries[0].Required,
            };
        }

        File.WriteAllText(Path.Combine(bundleDir, "checksums.json"), JsonSerializer.Serialize(checksumEntries, JsonOptions));
    }

    private static ChecksumEntry[] BuildChecksumEntries(string bundleDir, bool removeSchema)
    {
        var entries = new List<ChecksumEntry>
        {
            BuildEntry(bundleDir, "ctrader_bot_release_manifest.json", true),
            BuildEntry(bundleDir, "provenance.json", true),
            BuildEntry(bundleDir, "ensemble_signal_agent_package.json", true),
        };

        if (!removeSchema)
        {
            entries.Add(BuildEntry(bundleDir, "ensemble_signal_agent_package.schema.json", true));
        }

        return entries.ToArray();
    }

    private static ChecksumEntry BuildEntry(string bundleDir, string relativePath, bool required)
    {
        var fullPath = Path.Combine(bundleDir, relativePath);
        var bytes = File.ReadAllBytes(fullPath);
        var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        return new ChecksumEntry
        {
            Path = relativePath,
            Sha256 = sha256,
            SizeBytes = bytes.Length,
            GeneratedAt = "2026-01-01T00:00:00Z",
            Required = required,
        };
    }
}
