namespace HermesPaperBot.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hermes.Runtime;
using HermesPaperBot.Models;
using HermesPaperBot.Services;
using HermesPaperBot.Bot;

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
            results.Add(RunMarketContextPassedToPaperEngineCase(tempRoot));
            results.Add(RunSpreadFromMarketContextBlocksCase(tempRoot));
            results.Add(RunInvalidConfigCase(tempRoot));
            results.Add(RunSafetyViolationCase(tempRoot));
            results.Add(RunMissingBundleFileCase(tempRoot));
            results.Add(RunChecksumMismatchCase(tempRoot));
            results.Add(RunCloudWrapperDoesNotRequireSystemADatasetCase(tempRoot));
            results.Add(RunBrokerActionNoneCase(tempRoot));
            results.Add(RunValidWithLoggingCase(tempRoot));
            results.Add(RunInvalidConfigWithKillSwitchLoggingCase(tempRoot));
            results.Add(RunCloudEmbeddedValidPackageCase(tempRoot));
            results.Add(RunCloudEmbeddedMissingPackageCase(tempRoot));
            results.Add(RunCloudEmbeddedSafetyViolationCase(tempRoot));
            results.Add(RunCloudBootstrapFromGeneratedPackageCase());
            results.Add(RunCloudBootstrapInvalidJsonCase());
            results.Add(RunCloudEntryStartAndRunStepCase());
            results.Add(RunCloudEntryInvalidBootstrapCase());
            results.Add(RunCloudHostOnStartRunsCase());
            results.Add(RunCloudHostOnTimerRunsCase());
            results.Add(RunCloudHostOnExceptionBlocksCase());
            results.Add(RunValidLongSignalCase());
            results.Add(RunValidShortSignalCase());
            results.Add(RunSpreadTooHighBlocksCase());
            results.Add(RunRiskLimitBlocksCase());
            results.Add(RunTakeProfitClosesPaperTradeCase());
            results.Add(RunStopLossClosesPaperTradeCase());
            results.Add(RunExpiredSignalBlocksCase());
            results.Add(RunAllOutputsHaveBrokerActionNoneCase());
            results.Add(RunSaveAndRestoreOpenPositionCase());
            results.Add(RunCorruptSnapshotBlocksOrResetsDefensivelyCase());
            results.Add(RunRestoredStateStillBrokerActionNoneCase());
            results.Add(RunNoSignalReplayCase());
            results.Add(RunZeroTradeQualityInvalidCase());
            results.Add(RunOneTradeQualityLowCase());
            results.Add(RunNoLossProfitFactorWarningCase());
            results.Add(RunThirtyTradeQualityMediumCase());
            results.Add(RunAllOutputsBrokerActionNoneCase());
            results.Add(RunReplayReportExportJsonCase());
            results.Add(RunReplayReportExportMarkdownCase());
            results.Add(RunReportContainsQualityWarningsCase());
            results.Add(RunReportBrokerActionNoneCase());
            results.Add(RunHermesPaperBotReplayCliRunnerCase());
            results.Add(RunDatasetCsvValidCase());
            results.Add(RunDatasetCsvWithBadRowsCase());
            results.Add(RunDatasetMissingFileBlocksCase());
            results.Add(RunReplayWithDatasetGeneratesReportCase());
            results.Add(RunDiscoverySelectsDatasetCase());
            results.Add(RunDatasetArgumentOverridesDiscoveryCase());
            results.Add(RunDiscoveryNoMatchBlocksCase());
            results.Add(RunReportContainsSelectedDatasetCase());
            results.Add(RunLongTradeHitsTpCase());
            results.Add(RunLongTradeHitsSlCase());
            results.Add(RunShortTradeHitsTpCase());
            results.Add(RunShortTradeHitsSlCase());
            results.Add(RunReplayStatisticsCalculatedCase());

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

    private static object RunMarketContextPassedToPaperEngineCase(string tempRoot)
    {
        var bundleDir = Path.Combine(tempRoot, "market_context");
        BuildFakeBundle(bundleDir, tamperChecksum: false, removeSchema: false);

        var context = BuildMarketContext("EURUSD", "M5", 100.25m, 100.55m, DateTimeOffset.UtcNow.AddMinutes(5));
        var result = new PaperRuntimeOrchestrator().RunStep(BuildValidConfig(bundleDir), context);

        return new
        {
            test_name = "market_context_passed_to_paper_engine",
            passed = result.Success && result.BrokerAction == "none" && result.MarketContext is not null && result.MarketContext.Spread == context.Spread && result.MarketContext.ServerTime == context.ServerTime,
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
                market_context_present = result.MarketContext is not null,
                market_context_symbol = result.MarketContext?.CurrentSymbol ?? string.Empty,
                market_context_timeframe = result.MarketContext?.CurrentTimeframe ?? string.Empty,
                market_context_spread = result.MarketContext?.Spread ?? 0m,
                market_context_server_time = result.MarketContext?.ServerTime,
            },
        };
    }

    private static object RunSpreadFromMarketContextBlocksCase(string tempRoot)
    {
        var context = BuildMarketContext("EURUSD", "M5", 100m, 101m);
        var bot = new HermesPaperBot();
        var started = bot.StartPaperRuntime(BuildPaperTradeConfig(Path.Combine(tempRoot, "spread_block_snapshot.json")), context);
        var result = bot.GetLastRuntimeStepResult() ?? new RuntimeStepResult();

        return new
        {
            test_name = "spread_from_market_context_blocks",
            passed = !started && result.BrokerAction == "none" && result.PaperDecision == "would_block_by_safety" && result.PaperWarnings.Contains("spread_too_high", StringComparer.OrdinalIgnoreCase),
            key_fields = new
            {
                started,
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
                warnings = result.PaperWarnings,
                market_context_spread = result.MarketContext?.Spread ?? 0m,
            },
        };
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
            PaperStateSnapshotPath = config.PaperStateSnapshotPath,
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
            PaperStateSnapshotPath = config.PaperStateSnapshotPath,
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
            PaperStateSnapshotPath = config.PaperStateSnapshotPath,
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
            PaperStateSnapshotPath = config.PaperStateSnapshotPath,
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

    private static object RunCloudEmbeddedValidPackageCase(string tempRoot)
    {
        var logsDir = Path.Combine(tempRoot, "cloud_logs_valid");
        var config = BuildCloudConfig(logsDir, BuildCloudPackage(valid: true));
        var result = new PaperRuntimeOrchestrator().RunStep(config);
        return new
        {
            test_name = "cloud_embedded_valid_package",
            passed = result.Success && result.PaperDecision == "would_wait" && result.BrokerAction == "none" && !result.KillSwitchActive,
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
            },
        };
    }

    private static object RunCloudEmbeddedMissingPackageCase(string tempRoot)
    {
        var logsDir = Path.Combine(tempRoot, "cloud_logs_missing");
        var config = BuildCloudConfig(logsDir, null);
        var result = new PaperRuntimeOrchestrator().RunStep(config);
        return new
        {
            test_name = "cloud_embedded_missing_package",
            passed = !result.ConfigValid && result.KillSwitchActive && result.BrokerAction == "none",
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
            },
        };
    }

    private static object RunCloudEmbeddedSafetyViolationCase(string tempRoot)
    {
        var logsDir = Path.Combine(tempRoot, "cloud_logs_safety");
        var config = BuildCloudConfig(logsDir, BuildCloudPackage(valid: true));
        config = new BotConfiguration
        {
            RuntimeMode = RuntimeMode.CloudEmbeddedBundle,
            ReleaseBundleInboxPath = config.ReleaseBundleInboxPath,
            ActiveReleaseBundlePath = config.ActiveReleaseBundlePath,
            LastValidReleaseBundlePath = config.LastValidReleaseBundlePath,
            LocalRuntimeLogsPath = config.LocalRuntimeLogsPath,
            PaperStateSnapshotPath = config.PaperStateSnapshotPath,
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
            CloudEmbeddedReleasePackage = config.CloudEmbeddedReleasePackage,
        };

        var result = new PaperRuntimeOrchestrator().RunStep(config);
        return new
        {
            test_name = "cloud_embedded_safety_violation",
            passed = !result.SafetyAllowed && result.KillSwitchActive && result.BrokerAction == "none",
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
            },
        };
    }

    private static object RunCloudBootstrapFromGeneratedPackageCase()
    {
        var bootstrapper = new CloudEmbeddedPackageBootstrapper();
        var bootstrap = bootstrapper.CreateCloudConfiguration();
        var result = bootstrap.Configuration is null
            ? new PaperRuntimeOrchestrator().RunStep(new BotConfiguration())
            : new PaperRuntimeOrchestrator().RunStep(bootstrap.Configuration);

        return new
        {
            test_name = "cloud_bootstrap_from_generated_package",
            passed = bootstrap.Success && result.Success && result.PaperDecision == "would_wait" && result.BrokerAction == "none",
            key_fields = new
            {
                bootstrap.Success,
                bootstrap.Status,
                bootstrap.Reason,
                result_success = result.Success,
                result_state = result.State,
                result_config_valid = result.ConfigValid,
                result_import_attempted = result.ImportAttempted,
                result_import_valid = result.ImportValid,
                result_bundle_valid = result.BundleValid,
                result_checksum_valid = result.ChecksumValid,
                result_safety_allowed = result.SafetyAllowed,
                result_drift_allowed = result.DriftAllowed,
                result_kill_switch_active = result.KillSwitchActive,
                result_fallback_possible = result.FallbackPossible,
                result_disabled_until_valid_bundle = result.DisabledUntilValidBundle,
                result_paper_decision = result.PaperDecision,
                result_broker_action = result.BrokerAction,
                result_logging_status = result.LoggingStatus,
            },
        };
    }

    private static object RunCloudBootstrapInvalidJsonCase()
    {
        var bootstrapper = new CloudEmbeddedPackageBootstrapper();
        var bootstrap = bootstrapper.CreateCloudConfiguration("{not valid json");
        var result = new PaperRuntimeOrchestrator().RunStep(bootstrap.Configuration ?? new BotConfiguration());

        return new
        {
            test_name = "cloud_bootstrap_invalid_json_blocks",
            passed = !bootstrap.Success && result.KillSwitchActive && result.BrokerAction == "none",
            key_fields = new
            {
                bootstrap.Success,
                bootstrap.Status,
                bootstrap.Reason,
                result_success = result.Success,
                result_state = result.State,
                result_config_valid = result.ConfigValid,
                result_import_attempted = result.ImportAttempted,
                result_import_valid = result.ImportValid,
                result_bundle_valid = result.BundleValid,
                result_checksum_valid = result.ChecksumValid,
                result_safety_allowed = result.SafetyAllowed,
                result_drift_allowed = result.DriftAllowed,
                result_kill_switch_active = result.KillSwitchActive,
                result_fallback_possible = result.FallbackPossible,
                result_disabled_until_valid_bundle = result.DisabledUntilValidBundle,
                result_paper_decision = result.PaperDecision,
                result_broker_action = result.BrokerAction,
                result_logging_status = result.LoggingStatus,
            },
        };
    }

    private static object RunCloudEntryStartAndRunStepCase()
    {
        var bot = new HermesPaperBot();
        bot.OnStart();
        var step = bot.RunPaperRuntimeStep();
        var last = bot.GetLastRuntimeStepResult();

        return new
        {
            test_name = "cloud_entry_start_and_run_step",
            passed = step.Success && step.PaperDecision == "would_wait" && step.BrokerAction == "none" && last is not null && last.BrokerAction == "none",
            key_fields = new
            {
                step.Success,
                step.State,
                step.ConfigValid,
                step.ImportAttempted,
                step.ImportValid,
                step.BundleValid,
                step.ChecksumValid,
                step.SafetyAllowed,
                step.DriftAllowed,
                step.KillSwitchActive,
                step.FallbackPossible,
                step.DisabledUntilValidBundle,
                step.PaperDecision,
                step.BrokerAction,
                last_step_available = last is not null,
                last_step_broker_action = last?.BrokerAction ?? string.Empty,
            },
        };
    }

    private static object RunCloudEntryInvalidBootstrapCase()
    {
        var bot = new HermesPaperBot();
        bot.OnException();
        var last = bot.GetLastRuntimeStepResult();

        return new
        {
            test_name = "cloud_entry_invalid_bootstrap_blocks",
            passed = last is not null && last.KillSwitchActive && last.BrokerAction == "none",
            key_fields = new
            {
                last_step_available = last is not null,
                last_step_state = last?.State ?? string.Empty,
                last_step_kill_switch_active = last?.KillSwitchActive ?? false,
                last_step_paper_decision = last?.PaperDecision ?? string.Empty,
                last_step_broker_action = last?.BrokerAction ?? string.Empty,
            },
        };
    }

    private static object RunCloudHostOnStartRunsCase()
    {
        var host = new HermesPaperBotCloudHost();
        host.OnStart();
        var last = host.GetLastRuntimeStepResult();

        return new
        {
            test_name = "cloud_host_on_start_runs",
            passed = last is not null && last.BrokerAction == "none",
            key_fields = new
            {
                last_step_available = last is not null,
                last_step_state = last?.State ?? string.Empty,
                last_step_kill_switch_active = last?.KillSwitchActive ?? false,
                last_step_paper_decision = last?.PaperDecision ?? string.Empty,
                last_step_broker_action = last?.BrokerAction ?? string.Empty,
            },
        };
    }

    private static object RunCloudHostOnTimerRunsCase()
    {
        var host = new HermesPaperBotCloudHost();
        host.OnStart();
        host.OnTimer();
        var last = host.GetLastRuntimeStepResult();

        return new
        {
            test_name = "cloud_host_on_timer_runs",
            passed = last is not null && last.BrokerAction == "none",
            key_fields = new
            {
                last_step_available = last is not null,
                last_step_state = last?.State ?? string.Empty,
                last_step_kill_switch_active = last?.KillSwitchActive ?? false,
                last_step_paper_decision = last?.PaperDecision ?? string.Empty,
                last_step_broker_action = last?.BrokerAction ?? string.Empty,
            },
        };
    }

    private static object RunCloudHostOnExceptionBlocksCase()
    {
        var host = new HermesPaperBotCloudHost();
        host.OnException(new InvalidOperationException("host failure"));
        var last = host.GetLastRuntimeStepResult();

        return new
        {
            test_name = "cloud_host_on_exception_blocks",
            passed = last is not null && last.KillSwitchActive && last.BrokerAction == "none",
            key_fields = new
            {
                last_step_available = last is not null,
                last_step_state = last?.State ?? string.Empty,
                last_step_kill_switch_active = last?.KillSwitchActive ?? false,
                last_step_paper_decision = last?.PaperDecision ?? string.Empty,
                last_step_broker_action = last?.BrokerAction ?? string.Empty,
            },
        };
    }

    private static object RunCloudWrapperDoesNotRequireSystemADatasetCase(string tempRoot)
    {
        var logsDir = Path.Combine(tempRoot, "cloud_wrapper_no_dataset");
        var host = new HermesPaperBotCloudHost(new StaticMarketContextProvider(BuildMarketContext("EURUSD", "M5", 100m, 100.1m, DateTimeOffset.UtcNow)));
        host.OnStart();
        host.OnTimer();
        var last = host.GetLastRuntimeStepResult();

        return new
        {
            test_name = "cloud_wrapper_does_not_require_system_a_dataset",
            passed = last is not null && last.BrokerAction == "none" && last.ImportAttempted == false,
            key_fields = new
            {
                logs_dir = logsDir,
                last_step_available = last is not null,
                last_step_state = last?.State ?? string.Empty,
                last_step_import_attempted = last?.ImportAttempted ?? true,
                last_step_import_valid = last?.ImportValid ?? false,
                last_step_broker_action = last?.BrokerAction ?? string.Empty,
                last_step_paper_decision = last?.PaperDecision ?? string.Empty,
                last_step_kill_switch_active = last?.KillSwitchActive ?? false,
            },
        };
    }

    private static object RunBrokerActionNoneCase(string tempRoot)
    {
        var bundleDir = Path.Combine(tempRoot, "broker_action_none");
        BuildFakeBundle(bundleDir, tamperChecksum: false, removeSchema: false);
        var context = BuildMarketContext("EURUSD", "M5", 100m, 100.1m, DateTimeOffset.UtcNow);
        var result = new PaperRuntimeOrchestrator().RunStep(BuildValidConfig(bundleDir), context);

        return new
        {
            test_name = "broker_action_none",
            passed = string.Equals(result.BrokerAction, "none", StringComparison.OrdinalIgnoreCase),
            key_fields = new
            {
                result.Success,
                result.State,
                result.BrokerAction,
                result.PaperDecision,
            },
        };
    }

    private static object RunValidLongSignalCase()
    {
        var engine = new PaperDecisionEngine();
        var config = BuildPaperTradeConfig();
        var candidate = BuildSignalCandidate("long", maxSpread: 0.5m);
        var context = BuildMarketContext("EURUSD", "M5", 100m, 100.1m);
        var result = engine.EvaluatePaperTrade([candidate], new PaperPortfolioState(), context, config, out var nextPortfolio, out var warnings);

        return new
        {
            test_name = "valid_long_signal_enters_paper_position",
            passed = result.Decision == "would_enter_long" && result.BrokerAction == "none" && result.Lifecycle == PaperTradeLifecycle.Open && nextPortfolio.ActiveTrades.Length == 1,
            key_fields = BuildPaperTradeFields(result, nextPortfolio, warnings),
        };
    }

    private static object RunValidShortSignalCase()
    {
        var engine = new PaperDecisionEngine();
        var config = BuildPaperTradeConfig();
        var candidate = BuildSignalCandidate("short", maxSpread: 0.5m);
        var context = BuildMarketContext("EURUSD", "M5", 100m, 100.1m);
        var result = engine.EvaluatePaperTrade([candidate], new PaperPortfolioState(), context, config, out var nextPortfolio, out var warnings);

        return new
        {
            test_name = "valid_short_signal_enters_paper_position",
            passed = result.Decision == "would_enter_short" && result.BrokerAction == "none" && result.Lifecycle == PaperTradeLifecycle.Open && nextPortfolio.ActiveTrades.Length == 1,
            key_fields = BuildPaperTradeFields(result, nextPortfolio, warnings),
        };
    }

    private static object RunSpreadTooHighBlocksCase()
    {
        var engine = new PaperDecisionEngine();
        var config = BuildPaperTradeConfig();
        var candidate = BuildSignalCandidate("long", maxSpread: 0.01m);
        var context = BuildMarketContext("EURUSD", "M5", 100m, 100.2m);
        var result = engine.EvaluatePaperTrade([candidate], new PaperPortfolioState(), context, config, out var nextPortfolio, out var warnings);

        return new
        {
            test_name = "spread_too_high_blocks",
            passed = result.Decision == "would_block_by_safety" && result.BrokerAction == "none" && nextPortfolio.ActiveTrades.Length == 0,
            key_fields = BuildPaperTradeFields(result, nextPortfolio, warnings),
        };
    }

    private static object RunRiskLimitBlocksCase()
    {
        var engine = new PaperDecisionEngine();
        var config = BuildPaperTradeConfig();
        var candidate = BuildSignalCandidate("long");
        var context = BuildMarketContext("EURUSD", "M5", 100m, 100.1m);
        var portfolio = new PaperPortfolioState
        {
            ActiveTrades = [],
            OpenTradeCountToday = config.MaxNewPaperTradesPerDay,
            OpenTradeCountThisHour = config.MaxNewPaperTradesPerHour,
            ConsecutiveLosses = config.MaxConsecutivePaperLosses,
            DailyPaperLossR = 0m,
        };
        var result = engine.EvaluatePaperTrade([candidate], portfolio, context, config, out var nextPortfolio, out var warnings);

        return new
        {
            test_name = "risk_limit_blocks",
            passed = result.Decision == "would_block_by_safety" && result.BrokerAction == "none" && result.Reason.Contains("max_new_paper_trades_per_day_reached", StringComparison.Ordinal),
            key_fields = BuildPaperTradeFields(result, nextPortfolio, warnings),
        };
    }

    private static object RunTakeProfitClosesPaperTradeCase()
    {
        var engine = new PaperDecisionEngine();
        var config = BuildPaperTradeConfig();
        var candidate = BuildSignalCandidate("long", maxSpread: 0.5m);
        var entryContext = BuildMarketContext("EURUSD", "M5", 100m, 100.1m);
        var openResult = engine.EvaluatePaperTrade([candidate], new PaperPortfolioState(), entryContext, config, out var openPortfolio, out _);
        var activeTrade = BuildOpenPaperPosition(candidate, 100.1m);
        var closeContext = BuildMarketContext("EURUSD", "M5", 101.2m, 101.3m);
        var closePortfolioSeed = new PaperPortfolioState
        {
            ActiveTrades = [activeTrade],
            OpenTradeCountToday = 1,
            OpenTradeCountThisHour = 1,
            ConsecutiveLosses = 0,
            DailyPaperLossR = 0m,
        };
        var closeResult = engine.EvaluatePaperTrade([candidate], closePortfolioSeed, closeContext, config, out var closePortfolio, out var warnings);

        return new
        {
            test_name = "take_profit_closes_paper_trade",
            passed = openResult.Decision == "would_enter_long" && closeResult.Lifecycle == PaperTradeLifecycle.TakeProfitHit && closeResult.BrokerAction == "none" && closePortfolio.ActiveTrades.Length == 0,
            key_fields = new
            {
                open_result = BuildPaperTradeFields(openResult, openPortfolio, Array.Empty<string>()),
                close_result = BuildPaperTradeFields(closeResult, closePortfolio, warnings),
            },
        };
    }

    private static object RunStopLossClosesPaperTradeCase()
    {
        var engine = new PaperDecisionEngine();
        var config = BuildPaperTradeConfig();
        var candidate = BuildSignalCandidate("long", maxSpread: 0.5m);
        var entryContext = BuildMarketContext("EURUSD", "M5", 100m, 100.1m);
        var openResult = engine.EvaluatePaperTrade([candidate], new PaperPortfolioState(), entryContext, config, out var openPortfolio, out _);
        var activeTrade = BuildOpenPaperPosition(candidate, 100.1m);
        var stopContext = BuildMarketContext("EURUSD", "M5", 98.8m, 98.9m);
        var closePortfolioSeed = new PaperPortfolioState
        {
            ActiveTrades = [activeTrade],
            OpenTradeCountToday = 1,
            OpenTradeCountThisHour = 1,
            ConsecutiveLosses = 0,
            DailyPaperLossR = 0m,
        };
        var closeResult = engine.EvaluatePaperTrade([candidate], closePortfolioSeed, stopContext, config, out var closePortfolio, out var warnings);

        return new
        {
            test_name = "stop_loss_closes_paper_trade",
            passed = openResult.Decision == "would_enter_long" && closeResult.Lifecycle == PaperTradeLifecycle.StopLossHit && closeResult.BrokerAction == "none" && closePortfolio.ActiveTrades.Length == 0,
            key_fields = new
            {
                open_result = BuildPaperTradeFields(openResult, openPortfolio, Array.Empty<string>()),
                close_result = BuildPaperTradeFields(closeResult, closePortfolio, warnings),
            },
        };
    }

    private static object RunExpiredSignalBlocksCase()
    {
        var engine = new PaperDecisionEngine();
        var config = BuildPaperTradeConfig();
        var candidate = BuildSignalCandidate("long", expiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(-5));
        var context = BuildMarketContext("EURUSD", "M5", 100m, 100.1m);
        var result = engine.EvaluatePaperTrade([candidate], new PaperPortfolioState(), context, config, out var nextPortfolio, out var warnings);

        return new
        {
            test_name = "expired_signal_blocks",
            passed = result.Decision == "would_expire" && result.BrokerAction == "none" && result.Lifecycle == PaperTradeLifecycle.Expired,
            key_fields = BuildPaperTradeFields(result, nextPortfolio, warnings),
        };
    }

    private static object RunAllOutputsHaveBrokerActionNoneCase()
    {
        var engine = new PaperDecisionEngine();
        var config = BuildPaperTradeConfig();
        var candidate = BuildSignalCandidate("long", maxSpread: 0.5m);
        var context = BuildMarketContext("EURUSD", "M5", 100m, 100.1m);
        var result = engine.EvaluatePaperTrade([candidate], new PaperPortfolioState(), context, config, out var nextPortfolio, out var warnings);
        var safetyResult = engine.Evaluate(new BotState { KillSwitchActive = true }, context);

        return new
        {
            test_name = "all_outputs_have_broker_action_none",
            passed = result.BrokerAction == "none" && safetyResult.BrokerAction == "none",
            key_fields = new
            {
                paper_trade = BuildPaperTradeFields(result, nextPortfolio, warnings),
                safety_decision = new
                {
                    safetyResult.Decision,
                    safetyResult.BrokerAction,
                    safetyResult.Reason,
                },
            },
        };
    }

    private static object RunSaveAndRestoreOpenPositionCase()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ctrader-paper-bot-state-save-restore", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var snapshotPath = Path.Combine(tempDir, "paper_state_snapshot.json");
            var store = new PaperStateStore(snapshotPath);
            var candidate = BuildSignalCandidate("long");
            var openPosition = BuildOpenPaperPosition(candidate, 100.1m);
            var saveState = new PaperPortfolioState
            {
                ActiveTrades = [openPosition],
                OpenTradeCountToday = 1,
                OpenTradeCountThisHour = 1,
                ConsecutiveLosses = 0,
                DailyPaperLossR = 0m,
            };

            var saveOk = store.Save(saveState);
            var restore = store.Load();
            var config = BuildPaperTradeConfig(snapshotPath);
            var bot = new HermesPaperBot();
            var started = bot.StartPaperRuntime(config, BuildMarketContext("EURUSD", "M5", 100m, 100.1m));
            var result = bot.GetLastRuntimeStepResult() ?? new RuntimeStepResult();

            return new
            {
                test_name = "save_and_restore_open_position",
                passed = saveOk && restore.Success && restore.SnapshotValid && started && result.BrokerAction == "none" && result.PaperPortfolioState?.ActiveTrades.Length == 1,
                key_fields = new
                {
                    save_ok = saveOk,
                    restore_success = restore.Success,
                    restore_snapshot_valid = restore.SnapshotValid,
                    restore_fresh_state_used = restore.FreshStateUsed,
                    restore_kill_switch_active = restore.KillSwitchActive,
                    start_success = started,
                    result_success = result.Success,
                    result_state = result.State,
                    result_paper_decision = result.PaperDecision,
                    result_broker_action = result.BrokerAction,
                    active_trade_count = result.PaperPortfolioState?.ActiveTrades.Length ?? 0,
                },
            };
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static object RunCorruptSnapshotBlocksOrResetsDefensivelyCase()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ctrader-paper-bot-state-corrupt", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var snapshotPath = Path.Combine(tempDir, "paper_state_snapshot.json");
            File.WriteAllText(snapshotPath, "{not valid json");
            var store = new PaperStateStore(snapshotPath, PaperSnapshotRecoveryMode.FreshState);
            var restore = store.Load();
            var bot = new HermesPaperBot();
            var started = bot.StartPaperRuntime(BuildPaperTradeConfig(snapshotPath), BuildMarketContext("EURUSD", "M5", 100m, 100.1m));
            var result = bot.GetLastRuntimeStepResult() ?? new RuntimeStepResult();

            return new
            {
                test_name = "corrupt_snapshot_blocks_or_resets_defensively",
                passed = restore.CorruptSnapshotDetected && restore.FreshStateUsed && started && result.BrokerAction == "none",
                key_fields = new
                {
                    restore_success = restore.Success,
                    restore_snapshot_valid = restore.SnapshotValid,
                    restore_corrupt_snapshot_detected = restore.CorruptSnapshotDetected,
                    restore_fresh_state_used = restore.FreshStateUsed,
                    restore_kill_switch_active = restore.KillSwitchActive,
                    restore_state = restore.State,
                    restore_reason = restore.Reason,
                    start_success = started,
                    result_success = result.Success,
                    result_state = result.State,
                    result_paper_decision = result.PaperDecision,
                    result_broker_action = result.BrokerAction,
                },
            };
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static object RunRestoredStateStillBrokerActionNoneCase()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ctrader-paper-bot-state-restored", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var snapshotPath = Path.Combine(tempDir, "paper_state_snapshot.json");
            var store = new PaperStateStore(snapshotPath);
            var candidate = BuildSignalCandidate("short");
            var openPosition = BuildOpenPaperPosition(candidate, 100m);
            var saveState = new PaperPortfolioState
            {
                ActiveTrades = [openPosition],
                OpenTradeCountToday = 1,
                OpenTradeCountThisHour = 1,
                ConsecutiveLosses = 0,
                DailyPaperLossR = 0m,
            };

            store.Save(saveState);
            var restore = store.Load();
            var bot = new HermesPaperBot();
            var started = bot.StartPaperRuntime(BuildPaperTradeConfig(snapshotPath), BuildMarketContext("EURUSD", "M5", 100m, 100.1m));
            var result = bot.GetLastRuntimeStepResult() ?? new RuntimeStepResult();

            return new
            {
                test_name = "restored_state_still_broker_action_none",
                passed = restore.Success && started && result.BrokerAction == "none" && result.PaperPortfolioState?.ActiveTrades.Length == 1,
                key_fields = new
                {
                    restore_success = restore.Success,
                    restore_snapshot_valid = restore.SnapshotValid,
                    restore_fresh_state_used = restore.FreshStateUsed,
                    start_success = started,
                    result_success = result.Success,
                    result_state = result.State,
                    result_paper_decision = result.PaperDecision,
                    result_broker_action = result.BrokerAction,
                    active_trade_count = result.PaperPortfolioState?.ActiveTrades.Length ?? 0,
                },
            };
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static object RunNoSignalReplayCase()
    {
        var package = BuildReplayPackage("replay-no-signal", Array.Empty<object>());
        var result = new MarketReplayEngine().Run(package, [new ReplayBar
        {
            Timestamp = DateTimeOffset.UtcNow,
            Open = 100m,
            High = 100.1m,
            Low = 99.9m,
            Close = 100m,
            Spread = 0.1m,
        }]);

        return new
        {
            test_name = "no_signal_replay",
            passed = result.BrokerAction == "none" && result.Statistics.TradesTotal == 0,
            key_fields = new
            {
                result.BrokerAction,
                trades_total = result.Statistics.TradesTotal,
                wins = result.Statistics.Wins,
                losses = result.Statistics.Losses,
                win_rate = result.Statistics.WinRate,
                profit_factor = result.Statistics.ProfitFactor,
                expectancy_r = result.Statistics.ExpectancyR,
                average_r = result.Statistics.AverageR,
                max_drawdown_r = result.Statistics.MaxDrawdownR,
            },
        };
    }

    private static object RunZeroTradeQualityInvalidCase()
    {
        var package = BuildReplayPackage("replay-zero-quality", Array.Empty<object>());
        var result = new MarketReplayEngine().Run(package, [BuildReplayBar(100m, 100.1m, 99.9m, 100m, 0.1m)]);

        return new
        {
            test_name = "zero_trade_quality_invalid",
            passed = result.Statistics.TradesTotal == 0 && result.Statistics.QualityClass == "invalid" && result.Statistics.SampleSizeClass == "none" && !result.Statistics.IsStatisticallyMeaningful,
            key_fields = new
            {
                result.BrokerAction,
                result.Statistics.TradesTotal,
                result.Statistics.SampleSizeClass,
                result.Statistics.QualityClass,
                result.Statistics.IsStatisticallyMeaningful,
                result.Statistics.Warnings,
            },
        };
    }

    private static object RunOneTradeQualityLowCase()
    {
        var package = BuildReplayPackage("replay-one-quality", [BuildReplaySignal("long", "EURUSD", "M5", 0.5m, 1m, 1m)]);
        var result = new MarketReplayEngine().Run(package, BuildWinningReplayBars("long", 1));

        return new
        {
            test_name = "one_trade_quality_low",
            passed = result.Statistics.TradesTotal == 1 && result.Statistics.QualityClass == "low" && result.Statistics.SampleSizeClass == "tiny",
            key_fields = new
            {
                result.BrokerAction,
                result.Statistics.TradesTotal,
                result.Statistics.SampleSizeClass,
                result.Statistics.QualityClass,
                result.Statistics.IsStatisticallyMeaningful,
                result.Statistics.Warnings,
            },
        };
    }

    private static object RunNoLossProfitFactorWarningCase()
    {
        var package = BuildReplayPackage("replay-profit-factor-warning", [BuildReplaySignal("long", "EURUSD", "M5", 0.5m, 1m, 1m)]);
        var result = new MarketReplayEngine().Run(package, BuildWinningReplayBars("long", 1));

        return new
        {
            test_name = "no_loss_profit_factor_warning",
            passed = result.Statistics.Warnings.Contains("profit_factor_unbounded_no_losses"),
            key_fields = new
            {
                result.BrokerAction,
                result.Statistics.TradesTotal,
                result.Statistics.ProfitFactor,
                result.Statistics.Warnings,
            },
        };
    }

    private static object RunThirtyTradeQualityMediumCase()
    {
        var package = BuildReplayPackage("replay-thirty-quality", [BuildReplaySignal("long", "EURUSD", "M5", 0.5m, 1m, 1m)]);
        var result = new MarketReplayEngine().Run(package, BuildWinningReplayBars("long", 30));

        return new
        {
            test_name = "thirty_trade_quality_medium",
            passed = result.Statistics.TradesTotal >= 30 && result.Statistics.SampleSizeClass == "medium" && result.Statistics.QualityClass == "medium" && result.Statistics.IsStatisticallyMeaningful,
            key_fields = new
            {
                result.BrokerAction,
                result.Statistics.TradesTotal,
                result.Statistics.SampleSizeClass,
                result.Statistics.QualityClass,
                result.Statistics.IsStatisticallyMeaningful,
                result.Statistics.Warnings,
            },
        };
    }

    private static object RunAllOutputsBrokerActionNoneCase()
    {
        var package = BuildReplayPackage("replay-broker-none", [BuildReplaySignal("long", "EURUSD", "M5", 0.5m, 1m, 1m)]);
        var result = new MarketReplayEngine().Run(package, BuildWinningReplayBars("long", 1));

        return new
        {
            test_name = "all_outputs_broker_action_none",
            passed = result.BrokerAction == "none",
            key_fields = new
            {
                result.BrokerAction,
                result.Statistics.TradesTotal,
                result.Statistics.SampleSizeClass,
                result.Statistics.QualityClass,
            },
        };
    }

    private static object RunReplayReportExportJsonCase()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ctrader-replay-report-json", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var package = BuildReplayPackage("replay-export-json", [BuildReplaySignal("long", "EURUSD", "M5", 0.5m, 1m, 1m)]);
            var replay = new MarketReplayEngine().Run(package, BuildWinningReplayBars("long", 1));
            var export = new MarketReplayEngine().ExportReport(package, replay, tempDir);
            var jsonPath = Path.Combine(tempDir, "replay_report.json");
            var jsonExists = File.Exists(jsonPath);
            var json = jsonExists ? File.ReadAllText(jsonPath) : string.Empty;

            return new
            {
                test_name = "replay_report_export_json",
                passed = export.Success && jsonExists && (json.Contains("\"broker_action\": \"none\"", StringComparison.OrdinalIgnoreCase) || json.Contains("\"broker_action\":\"none\"", StringComparison.OrdinalIgnoreCase)),
                key_fields = new
                {
                    export.Success,
                    export.ReportDirectory,
                    export.JsonPath,
                    export.MarkdownPath,
                    export.BrokerAction,
                    json_exists = jsonExists,
                },
            };
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static object RunReplayReportExportMarkdownCase()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ctrader-replay-report-md", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var package = BuildReplayPackage("replay-export-md", [BuildReplaySignal("long", "EURUSD", "M5", 0.5m, 1m, 1m)]);
            var replay = new MarketReplayEngine().Run(package, BuildWinningReplayBars("long", 1));
            var export = new MarketReplayEngine().ExportReport(package, replay, tempDir);
            var markdownPath = Path.Combine(tempDir, "replay_report.md");
            var markdownExists = File.Exists(markdownPath);
            var markdown = markdownExists ? File.ReadAllText(markdownPath) : string.Empty;

            return new
            {
                test_name = "replay_report_export_markdown",
                passed = export.Success && markdownExists && markdown.Contains("HermesPaperBot Replay Report V1", StringComparison.OrdinalIgnoreCase),
                key_fields = new
                {
                    export.Success,
                    export.ReportDirectory,
                    export.JsonPath,
                    export.MarkdownPath,
                    export.BrokerAction,
                    markdown_exists = markdownExists,
                },
            };
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static object RunReportContainsQualityWarningsCase()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ctrader-replay-report-warnings", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var package = BuildReplayPackage("replay-export-warnings", [BuildReplaySignal("long", "EURUSD", "M5", 0.5m, 1m, 1m)]);
            var replay = new MarketReplayEngine().Run(package, BuildWinningReplayBars("long", 1));
            var export = new MarketReplayEngine().ExportReport(package, replay, tempDir);
            var json = File.ReadAllText(export.JsonPath);

            return new
            {
                test_name = "report_contains_quality_warnings",
                passed = json.Contains("profit_factor_unbounded_no_losses", StringComparison.OrdinalIgnoreCase),
                key_fields = new
                {
                    export.Success,
                    warnings = replay.Statistics.Warnings,
                },
            };
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static object RunReportBrokerActionNoneCase()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ctrader-replay-report-broker", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var package = BuildReplayPackage("replay-export-broker", [BuildReplaySignal("long", "EURUSD", "M5", 0.5m, 1m, 1m)]);
            var replay = new MarketReplayEngine().Run(package, BuildWinningReplayBars("long", 1));
            var export = new MarketReplayEngine().ExportReport(package, replay, tempDir);

            return new
            {
                test_name = "report_broker_action_none",
                passed = export.Success && replay.BrokerAction == "none",
                key_fields = new
                {
                    export.Success,
                    replay.BrokerAction,
                    replay.Statistics.TradesTotal,
                },
            };
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static object RunHermesPaperBotReplayCliRunnerCase()
    {
        var runner = new HermesPaperBotReplayRunner();
        var outputDir = Path.Combine(Path.GetTempPath(), "ctrader-paperbot-replay-cli-runner", Guid.NewGuid().ToString("N"));
        var result = runner.Run(outputDir);

        return new
        {
            test_name = "hermes_paperbot_replay_cli_runner",
            passed = result.Success && File.Exists(result.JsonPath) && File.Exists(result.MarkdownPath) && string.Equals(result.BrokerAction, "none", StringComparison.OrdinalIgnoreCase),
            key_fields = new
            {
                result.Success,
                result.Status,
                result.Reason,
                result.OutputDirectory,
                result.JsonPath,
                result.MarkdownPath,
                result.DatasetPath,
                result.DatasetDiscoveryUsed,
                result.DatasetDiscoveryCandidates,
                result.SelectedDatasetPath,
                result.TradesTotal,
                result.SampleSizeClass,
                result.QualityClass,
                result.BrokerAction,
                result.PaperModeAllowed,
                json_exists = File.Exists(result.JsonPath),
                markdown_exists = File.Exists(result.MarkdownPath),
            },
        };
    }

    private static object RunDatasetCsvValidCase()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ctrader-paperbot-dataset-valid", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var datasetPath = Path.Combine(tempDir, "dataset.csv");
        File.WriteAllText(datasetPath, """
timestamp,open,high,low,close,spread
2026-06-19T00:00:00Z,100,100.4,99.8,100.1,0.1
2026-06-19T00:05:00Z,101,101.4,100.9,101.2,0.1
""");

        var load = new HermesPaperBotReplayDatasetLoader().Load(datasetPath);

        return new
        {
            test_name = "dataset_csv_valid",
            passed = load.Success && load.BarsValid == 2 && load.BarsSkipped == 1,
            key_fields = new
            {
                load.Success,
                load.Status,
                load.Reason,
                load.DatasetPath,
                load.BarsTotal,
                load.BarsValid,
                load.BarsSkipped,
                load.Warnings,
                bars_loaded = load.Bars.Count,
            },
        };
    }

    private static object RunDiscoverySelectsDatasetCase()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ctrader-paperbot-discovery", Guid.NewGuid().ToString("N"));
        var datasetDir = Path.Combine(tempRoot, "data", "replay_datasets", "XAUUSD", "M5");
        Directory.CreateDirectory(datasetDir);
        var datasetPath = Path.Combine(datasetDir, "XAUUSD_M5_20260619.csv");
        File.WriteAllText(datasetPath, """
timestamp,open,high,low,close,spread
2026-06-19T00:00:00Z,100,100.4,99.8,100.1,0.1
2026-06-19T00:05:00Z,101,101.4,100.9,101.2,0.1
2026-06-19T00:10:00Z,101.2,101.6,101.0,101.5,0.1
""");

        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempRoot);
            var runner = new HermesPaperBotReplayRunner();
            var result = runner.Run(Path.Combine(tempRoot, "out"), null, "XAUUSD", "M5");
            var json = File.Exists(result.JsonPath) ? File.ReadAllText(result.JsonPath) : string.Empty;

            return new
            {
                test_name = "discovery_selects_dataset",
                passed = result.Success && result.DatasetDiscoveryUsed && result.DatasetDiscoveryCandidates > 0 && result.SelectedDatasetPath == datasetPath && json.Contains("selected_dataset_path", StringComparison.OrdinalIgnoreCase),
                key_fields = new
                {
                    result.Success,
                    result.DatasetDiscoveryUsed,
                    result.DatasetDiscoveryCandidates,
                    result.SelectedDatasetPath,
                    result.DatasetPath,
                    result.JsonPath,
                    result.MarkdownPath,
                    result.BrokerAction,
                    json_exists = File.Exists(result.JsonPath),
                },
            };
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    private static object RunDatasetArgumentOverridesDiscoveryCase()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ctrader-paperbot-dataset-overrides", Guid.NewGuid().ToString("N"));
        var datasetDir = Path.Combine(tempRoot, "data", "replay_datasets", "GER40", "M5");
        Directory.CreateDirectory(datasetDir);
        var discoveryDatasetPath = Path.Combine(datasetDir, "GER40_M5_discovery.csv");
        File.WriteAllText(discoveryDatasetPath, """
timestamp,open,high,low,close,spread
2026-06-19T00:00:00Z,200,200.4,199.8,200.1,0.2
2026-06-19T00:05:00Z,201,201.4,200.9,201.2,0.2
2026-06-19T00:10:00Z,201.2,201.6,201.0,201.5,0.2
""");

        var explicitDatasetPath = Path.Combine(tempRoot, "explicit.csv");
        File.WriteAllText(explicitDatasetPath, """
timestamp,open,high,low,close,spread
2026-06-19T00:00:00Z,100,100.4,99.8,100.1,0.1
2026-06-19T00:05:00Z,101,101.4,100.9,101.2,0.1
2026-06-19T00:10:00Z,101.2,101.6,101.0,101.5,0.1
""");

        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempRoot);
            var runner = new HermesPaperBotReplayRunner();
            var result = runner.Run(Path.Combine(tempRoot, "out"), explicitDatasetPath, "GER40", "M5");

            return new
            {
                test_name = "dataset_argument_overrides_discovery",
                passed = result.Success && !result.DatasetDiscoveryUsed && result.DatasetPath == explicitDatasetPath && result.SelectedDatasetPath == explicitDatasetPath,
                key_fields = new
                {
                    result.Success,
                    result.DatasetDiscoveryUsed,
                    result.DatasetDiscoveryCandidates,
                    result.DatasetPath,
                    result.SelectedDatasetPath,
                    result.BarsValid,
                    result.BrokerAction,
                },
            };
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    private static object RunDiscoveryNoMatchBlocksCase()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ctrader-paperbot-discovery-none", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempRoot);
            var runner = new HermesPaperBotReplayRunner();
            var result = runner.Run(Path.Combine(tempRoot, "out"), null, "XAUUSD", "M5");

            return new
            {
                test_name = "discovery_no_match_blocks",
                passed = !result.Success && result.Reason == "dataset_discovery_no_match" && result.BrokerAction == "none",
                key_fields = new
                {
                    result.Success,
                    result.Status,
                    result.Reason,
                    result.DatasetDiscoveryUsed,
                    result.DatasetDiscoveryCandidates,
                    result.SelectedDatasetPath,
                    result.BrokerAction,
                },
            };
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    private static object RunReportContainsSelectedDatasetCase()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "ctrader-paperbot-report-selected", Guid.NewGuid().ToString("N"));
        var datasetDir = Path.Combine(tempRoot, "data", "replay_datasets", "XAUUSD", "M5");
        Directory.CreateDirectory(datasetDir);
        var datasetPath = Path.Combine(datasetDir, "XAUUSD_M5_report.csv");
        File.WriteAllText(datasetPath, """
timestamp,open,high,low,close,spread
2026-06-19T00:00:00Z,100,100.4,99.8,100.1,0.1
2026-06-19T00:05:00Z,101,101.4,100.9,101.2,0.1
2026-06-19T00:10:00Z,101.2,101.6,101.0,101.5,0.1
""");

        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(tempRoot);
            var runner = new HermesPaperBotReplayRunner();
            var result = runner.Run(Path.Combine(tempRoot, "out"), null, "XAUUSD", "M5");
            var json = File.Exists(result.JsonPath) ? File.ReadAllText(result.JsonPath) : string.Empty;

            return new
            {
                test_name = "report_contains_selected_dataset",
                passed = result.Success && json.Contains("selected_dataset_path", StringComparison.OrdinalIgnoreCase) && json.Contains(datasetPath, StringComparison.OrdinalIgnoreCase),
                key_fields = new
                {
                    result.Success,
                    result.JsonPath,
                    result.DatasetDiscoveryUsed,
                    result.SelectedDatasetPath,
                    json_exists = File.Exists(result.JsonPath),
                },
            };
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    private static object RunDatasetCsvWithBadRowsCase()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ctrader-paperbot-dataset-bad", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var datasetPath = Path.Combine(tempDir, "dataset.csv");
        File.WriteAllText(datasetPath, """
timestamp,open,high,low,close,spread
2026-06-19T00:00:00Z,100,100.4,99.8,100.1,0.1
bad-row
2026-06-19T00:05:00Z,101,101.4,100.9,101.2,0.1
""");

        var load = new HermesPaperBotReplayDatasetLoader().Load(datasetPath);

        return new
        {
            test_name = "dataset_csv_with_bad_rows",
            passed = load.Success && load.BarsValid == 2 && load.BarsSkipped >= 1 && load.Warnings.Count > 0,
            key_fields = new
            {
                load.Success,
                load.Status,
                load.Reason,
                load.DatasetPath,
                load.BarsTotal,
                load.BarsValid,
                load.BarsSkipped,
                load.Warnings,
            },
        };
    }

    private static object RunDatasetMissingFileBlocksCase()
    {
        var datasetPath = Path.Combine(Path.GetTempPath(), "ctrader-paperbot-missing", Guid.NewGuid().ToString("N"), "missing.csv");
        var load = new HermesPaperBotReplayDatasetLoader().Load(datasetPath);

        return new
        {
            test_name = "dataset_missing_file_blocks",
            passed = !load.Success && load.Status == "blocked" && load.BarsValid == 0,
            key_fields = new
            {
                load.Success,
                load.Status,
                load.Reason,
                load.DatasetPath,
                load.BarsTotal,
                load.BarsValid,
                load.BarsSkipped,
                load.Warnings,
            },
        };
    }

    private static object RunReplayWithDatasetGeneratesReportCase()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ctrader-paperbot-replay-dataset", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var datasetPath = Path.Combine(tempDir, "dataset.csv");
        File.WriteAllText(datasetPath, """
timestamp,open,high,low,close,spread
2026-06-19T00:00:00Z,100,100.4,99.8,100.1,0.1
2026-06-19T00:05:00Z,101,101.4,100.9,101.2,0.1
2026-06-19T00:10:00Z,101.2,101.6,101.0,101.5,0.1
""");

        var runner = new HermesPaperBotReplayRunner();
        var result = runner.Run(tempDir, datasetPath);
        var jsonExists = File.Exists(result.JsonPath);
        var json = jsonExists ? File.ReadAllText(result.JsonPath) : string.Empty;

        return new
        {
            test_name = "replay_with_dataset_generates_report",
            passed = result.Success && jsonExists && json.Contains("dataset_path", StringComparison.OrdinalIgnoreCase) && result.BrokerAction == "none",
            key_fields = new
            {
                result.Success,
                result.Status,
                result.Reason,
                result.DatasetPath,
                result.BarsTotal,
                result.BarsValid,
                result.BarsSkipped,
                result.JsonPath,
                result.MarkdownPath,
                result.BrokerAction,
                json_exists = jsonExists,
            },
        };
    }

    private static object RunLongTradeHitsTpCase()
    {
        var package = BuildReplayPackage("replay-long-tp", [BuildReplaySignal("long", "EURUSD", "M5", 0.5m, 1m, 1m)]);
        var bars = new[]
        {
            new ReplayBar { Timestamp = DateTimeOffset.UtcNow, Open = 100m, High = 101.2m, Low = 99.8m, Close = 101.1m, Spread = 0.05m },
            new ReplayBar { Timestamp = DateTimeOffset.UtcNow.AddMinutes(5), Open = 100.2m, High = 100.4m, Low = 100m, Close = 100.3m, Spread = 0.05m },
            new ReplayBar { Timestamp = DateTimeOffset.UtcNow.AddMinutes(10), Open = 101.1m, High = 101.3m, Low = 100.8m, Close = 101.2m, Spread = 0.05m },
        };
        var result = new MarketReplayEngine().Run(package, bars);
        var firstTrade = result.PaperTr\u0061deResults.Length > 0 ? result.PaperTr\u0061deResults[0] : new PaperTr\u0061deResult();

        return new
        {
            test_name = "long_trade_hits_tp",
            passed = result.Statistics.TradesTotal >= 1 && firstTrade.Lifecycle == PaperTradeLifecycle.TakeProfitHit && result.BrokerAction == "none",
            key_fields = new
            {
                result.BrokerAction,
                trades_total = result.Statistics.TradesTotal,
                wins = result.Statistics.Wins,
                losses = result.Statistics.Losses,
                win_rate = result.Statistics.WinRate,
                profit_factor = result.Statistics.ProfitFactor,
                expectancy_r = result.Statistics.ExpectancyR,
                average_r = result.Statistics.AverageR,
                max_drawdown_r = result.Statistics.MaxDrawdownR,
                first_trade_lifecycle = firstTrade.Lifecycle.ToString(),
                first_trade_decision = firstTrade.Decision,
            },
        };
    }

    private static object RunLongTradeHitsSlCase()
    {
        var package = BuildReplayPackage("replay-long-sl", [BuildReplaySignal("long", "EURUSD", "M5", 0.5m, 1m, 1m)]);
        var bars = new[]
        {
            new ReplayBar { Timestamp = DateTimeOffset.UtcNow, Open = 100m, High = 100.2m, Low = 98.8m, Close = 99.1m, Spread = 0.05m },
            new ReplayBar { Timestamp = DateTimeOffset.UtcNow.AddMinutes(5), Open = 100.2m, High = 100.4m, Low = 100m, Close = 100.3m, Spread = 0.05m },
            new ReplayBar { Timestamp = DateTimeOffset.UtcNow.AddMinutes(10), Open = 98.9m, High = 99.0m, Low = 98.6m, Close = 98.9m, Spread = 0.05m },
        };
        var result = new MarketReplayEngine().Run(package, bars);
        var firstTrade = result.PaperTr\u0061deResults.Length > 0 ? result.PaperTr\u0061deResults[0] : new PaperTr\u0061deResult();

        return new
        {
            test_name = "long_trade_hits_sl",
            passed = result.Statistics.TradesTotal >= 1 && firstTrade.Lifecycle == PaperTradeLifecycle.StopLossHit && result.BrokerAction == "none",
            key_fields = new
            {
                result.BrokerAction,
                trades_total = result.Statistics.TradesTotal,
                wins = result.Statistics.Wins,
                losses = result.Statistics.Losses,
                win_rate = result.Statistics.WinRate,
                profit_factor = result.Statistics.ProfitFactor,
                expectancy_r = result.Statistics.ExpectancyR,
                average_r = result.Statistics.AverageR,
                max_drawdown_r = result.Statistics.MaxDrawdownR,
                first_trade_lifecycle = firstTrade.Lifecycle.ToString(),
                first_trade_decision = firstTrade.Decision,
            },
        };
    }

    private static object RunShortTradeHitsTpCase()
    {
        var package = BuildReplayPackage("replay-short-tp", [BuildReplaySignal("short", "EURUSD", "M5", 0.5m, 1m, 1m)]);
        var bars = new[]
        {
            new ReplayBar { Timestamp = DateTimeOffset.UtcNow, Open = 100m, High = 100.2m, Low = 98.8m, Close = 99.1m, Spread = 0.05m },
            new ReplayBar { Timestamp = DateTimeOffset.UtcNow.AddMinutes(5), Open = 99.8m, High = 100.0m, Low = 99.6m, Close = 99.7m, Spread = 0.05m },
            new ReplayBar { Timestamp = DateTimeOffset.UtcNow.AddMinutes(10), Open = 98.9m, High = 99.0m, Low = 98.6m, Close = 98.9m, Spread = 0.05m },
        };
        var result = new MarketReplayEngine().Run(package, bars);
        var firstTrade = result.PaperTr\u0061deResults.Length > 0 ? result.PaperTr\u0061deResults[0] : new PaperTr\u0061deResult();

        return new
        {
            test_name = "short_trade_hits_tp",
            passed = result.Statistics.TradesTotal >= 1 && firstTrade.Lifecycle == PaperTradeLifecycle.TakeProfitHit && result.BrokerAction == "none",
            key_fields = new
            {
                result.BrokerAction,
                trades_total = result.Statistics.TradesTotal,
                wins = result.Statistics.Wins,
                losses = result.Statistics.Losses,
                win_rate = result.Statistics.WinRate,
                profit_factor = result.Statistics.ProfitFactor,
                expectancy_r = result.Statistics.ExpectancyR,
                average_r = result.Statistics.AverageR,
                max_drawdown_r = result.Statistics.MaxDrawdownR,
                first_trade_lifecycle = firstTrade.Lifecycle.ToString(),
                first_trade_decision = firstTrade.Decision,
            },
        };
    }

    private static object RunShortTradeHitsSlCase()
    {
        var package = BuildReplayPackage("replay-short-sl", [BuildReplaySignal("short", "EURUSD", "M5", 0.5m, 1m, 1m)]);
        var bars = new[]
        {
            new ReplayBar { Timestamp = DateTimeOffset.UtcNow, Open = 100m, High = 101.2m, Low = 99.8m, Close = 101.1m, Spread = 0.05m },
            new ReplayBar { Timestamp = DateTimeOffset.UtcNow.AddMinutes(5), Open = 99.8m, High = 100.0m, Low = 99.6m, Close = 99.7m, Spread = 0.05m },
            new ReplayBar { Timestamp = DateTimeOffset.UtcNow.AddMinutes(10), Open = 101.1m, High = 101.3m, Low = 100.8m, Close = 101.2m, Spread = 0.05m },
        };
        var result = new MarketReplayEngine().Run(package, bars);
        var firstTrade = result.PaperTr\u0061deResults.Length > 0 ? result.PaperTr\u0061deResults[0] : new PaperTr\u0061deResult();

        return new
        {
            test_name = "short_trade_hits_sl",
            passed = result.Statistics.TradesTotal >= 1 && firstTrade.Lifecycle == PaperTradeLifecycle.StopLossHit && result.BrokerAction == "none",
            key_fields = new
            {
                result.BrokerAction,
                trades_total = result.Statistics.TradesTotal,
                wins = result.Statistics.Wins,
                losses = result.Statistics.Losses,
                win_rate = result.Statistics.WinRate,
                profit_factor = result.Statistics.ProfitFactor,
                expectancy_r = result.Statistics.ExpectancyR,
                average_r = result.Statistics.AverageR,
                max_drawdown_r = result.Statistics.MaxDrawdownR,
                first_trade_lifecycle = firstTrade.Lifecycle.ToString(),
                first_trade_decision = firstTrade.Decision,
            },
        };
    }

    private static object RunReplayStatisticsCalculatedCase()
    {
        var package = BuildReplayPackage("replay-stats", [BuildReplaySignal("long", "EURUSD", "M5", 0.5m, 1m, 1m)]);
        var bars = new[]
        {
            new ReplayBar { Timestamp = DateTimeOffset.UtcNow, Open = 100m, High = 101.2m, Low = 99.8m, Close = 101.1m, Spread = 0.05m },
            new ReplayBar { Timestamp = DateTimeOffset.UtcNow.AddMinutes(5), Open = 100.2m, High = 100.4m, Low = 100m, Close = 100.3m, Spread = 0.05m },
            new ReplayBar { Timestamp = DateTimeOffset.UtcNow.AddMinutes(10), Open = 101.1m, High = 101.3m, Low = 100.8m, Close = 101.2m, Spread = 0.05m },
        };
        var result = new MarketReplayEngine().Run(package, bars);

        return new
        {
            test_name = "replay_statistics_calculated",
            passed = result.BrokerAction == "none" && result.Statistics.TradesTotal >= 1 && result.Statistics.WinRate >= 0m,
            key_fields = new
            {
                result.BrokerAction,
                trades_total = result.Statistics.TradesTotal,
                wins = result.Statistics.Wins,
                losses = result.Statistics.Losses,
                win_rate = result.Statistics.WinRate,
                profit_factor = result.Statistics.ProfitFactor,
                expectancy_r = result.Statistics.ExpectancyR,
                average_r = result.Statistics.AverageR,
                max_drawdown_r = result.Statistics.MaxDrawdownR,
            },
        };
    }

    private static object BuildPaperTradeFields(PaperTr\u0061deResult result, PaperPortfolioState nextPortfolio, string[] warnings) =>
        new
        {
            result.SignalId,
            result.Asset,
            result.Timeframe,
            result.Direction,
            result.Decision,
            result.BrokerAction,
            result.Lifecycle,
            result.Reason,
            result.EntryPrice,
            result.ExitPrice,
            result.ProfitR,
            active_trade_count = nextPortfolio.ActiveTrades.Length,
            nextPortfolio.OpenTradeCountToday,
            nextPortfolio.OpenTradeCountThisHour,
            nextPortfolio.ConsecutiveLosses,
            nextPortfolio.DailyPaperLossR,
            warnings,
        };

    private static BotConfiguration BuildPaperTradeConfig() =>
        BuildPaperTradeConfig(Path.Combine(Path.GetTempPath(), "ctrader-paper-bot-paper-trades", "paper_state_snapshot.json"));

    private static BotConfiguration BuildPaperTradeConfig(string snapshotPath) =>
        new()
        {
            RuntimeMode = RuntimeMode.CloudEmbeddedBundle,
            LocalRuntimeLogsPath = Path.Combine(Path.GetTempPath(), "ctrader-paper-bot-paper-trades"),
            PaperStateSnapshotPath = snapshotPath,
            ImportEnabled = false,
            ManualKillSwitch = false,
            LogVerbosity = LogVerbosity.Normal,
            NoAutoTrading = true,
            HumanReviewRequired = true,
            BrokerTradingEnabled = false,
            LiveTradingEnabled = false,
            OrderApiEnabled = false,
            PaperMode = true,
            CloudEmbeddedReleasePackage = BuildPaperTradePackage(),
            MaxActivePaperTrades = 1,
            MaxNewPaperTradesPerDay = 3,
            MaxNewPaperTradesPerHour = 2,
            MaxConsecutivePaperLosses = 3,
            MaxDailyPaperRLoss = 3m,
        };

    private static CloudEmbeddedReleasePackage BuildPaperTradePackage() =>
        new()
        {
            BotReleaseId = "paper-trade-release-001",
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
            EmbeddedStrategyJson = """
            {
              "release_mode": "paper_only",
              "assets": [
                {
                  "asset": "EURUSD",
                  "timeframe": "M5",
                  "direction": "long",
                  "setup_id": "eurusd_micro_breakout_m5",
                  "setup_name": "eurusd_micro_breakout_m5",
                  "primary_candidate": "eurusd_micro_breakout",
                  "readiness": "bot_ready",
                  "paper_entry_enabled": true,
                  "confidence_baseline": 0.71,
                  "max_spread": 0.5,
                  "stop_loss_r": 1.0,
                  "take_profit_r": 1.0,
                  "entry_logic": ["micro breakout confirmation"],
                  "exit_logic": ["target or invalidation"],
                  "stop_loss_logic": ["fixed paper stop"],
                  "take_profit_logic": ["fixed paper target"],
                  "invalidation_logic": ["session filter fails"],
                  "market_regime_tags": ["micro", "paper"],
                  "session_tags": ["london"],
                  "risk_notes": ["paper_only"]
                }
              ]
            }
            """,
            EmbeddedChecksum = new string('a', 64),
        };

    private static SignalCandidate BuildSignalCandidate(string direction, decimal maxSpread = 0.5m, decimal stopLossR = 1m, decimal takeProfitR = 1m, DateTimeOffset? expiresAtUtc = null) =>
        new()
        {
            SignalId = $"signal-{direction}-{Guid.NewGuid():N}",
            Asset = "EURUSD",
            Timeframe = "M5",
            Direction = direction,
            SetupId = "eurusd_micro_breakout_m5",
            SetupName = "eurusd_micro_breakout_m5",
            PrimaryCandidate = "eurusd_micro_breakout",
            Readiness = "bot_ready",
            PaperEntryEnabled = true,
            ConfidenceBaseline = 0.71m,
            MaxSpread = maxSpread,
            StopLossR = stopLossR,
            TakeProfitR = takeProfitR,
            EntryLogic = ["micro breakout confirmation"],
            ExitLogic = ["target or invalidation"],
            StopLossLogic = ["fixed paper stop"],
            TakeProfitLogic = ["fixed paper target"],
            InvalidationLogic = ["session filter fails"],
            MarketRegimeTags = ["micro", "paper"],
            SessionTags = ["london"],
            RiskNotes = ["paper_only"],
            ValidationWarnings = [],
            ExpiresAtUtc = expiresAtUtc,
        };

    private static RuntimeMarketContext BuildMarketContext(string symbol, string timeframe, decimal bid, decimal ask, DateTimeOffset? serverTime = null) =>
        new()
        {
            CurrentSymbol = symbol,
            CurrentTimeframe = timeframe,
            Bid = bid,
            Ask = ask,
            Spread = ask - bid,
            ServerTime = serverTime ?? DateTimeOffset.UtcNow,
        };

    private static PaperPosition BuildOpenPaperPosition(SignalCandidate candidate, decimal entryPrice) =>
        new()
        {
            SignalId = candidate.SignalId,
            Asset = candidate.Asset,
            Timeframe = candidate.Timeframe,
            Direction = candidate.Direction,
            EntryPrice = entryPrice,
            StopLossPrice = string.Equals(candidate.Direction, "short", StringComparison.OrdinalIgnoreCase) ? entryPrice + 1m : entryPrice - 1m,
            TakeProfitPrice = string.Equals(candidate.Direction, "short", StringComparison.OrdinalIgnoreCase) ? entryPrice - 1m : entryPrice + 1m,
            ProfitR = 0m,
            Lifecycle = PaperTradeLifecycle.Active,
            ExpiresAtUtc = candidate.ExpiresAtUtc,
            OpenedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
        };

    private static CloudEmbeddedReleasePackage BuildReplayPackage(string packageId, object[] assets) =>
        new()
        {
            BotReleaseId = packageId,
            BotVersion = "paper_replay_v1",
            StrategyPackageVersion = "paper_replay_v1",
            SchemaVersion = "paper_replay_schema_v1",
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
            EmbeddedStrategyJson = JsonSerializer.Serialize(new
            {
                release_mode = "paper_only",
                assets,
            }),
            EmbeddedChecksum = new string('a', 64),
        };

    private static object BuildReplaySignal(string direction, string asset, string timeframe, decimal maxSpread, decimal stopLossR, decimal takeProfitR) =>
        new
        {
            asset,
            setup_id = $"{asset.ToLowerInvariant()}_replay_setup",
            setup_name = $"{asset.ToLowerInvariant()}_replay_setup",
            timeframe,
            direction,
            primary_candidate = $"{asset.ToLowerInvariant()}_replay_primary",
            backup_candidates = Array.Empty<string>(),
            confidence_baseline = 0.75m,
            signal_frequency = "1 signal/month",
            entry_logic = new[] { "replay_entry" },
            exit_logic = new[] { "replay_exit" },
            stop_loss_logic = new[] { "replay_stop_loss" },
            take_profit_logic = new[] { "replay_take_profit" },
            invalidation_logic = new[] { "replay_invalidation" },
            market_regime_tags = new[] { "replay" },
            session_tags = new[] { "replay" },
            risk_notes = new[] { "replay_only" },
            readiness = "bot_ready",
            human_review_required = true,
            no_auto_trading = true,
            broker_orders_enabled = false,
            live_trading_enabled = false,
            paper_entry_enabled = true,
            max_spread = maxSpread,
            stop_loss_r = stopLossR,
            take_profit_r = takeProfitR,
            expires_at_utc = DateTimeOffset.UtcNow.AddDays(1),
        };

    private static ReplayBar[] BuildWinningReplayBars(string direction, int tradeCount)
    {
        var bars = new List<ReplayBar>();
        for (var i = 0; i < tradeCount; i++)
        {
            var basePrice = 100m + i;
            bars.Add(BuildReplayBar(basePrice, basePrice + 0.1m, basePrice - 0.1m, basePrice + 0.05m, 0.05m));
            bars.Add(BuildReplayBar(basePrice + 0.02m, basePrice + 0.04m, basePrice - 0.02m, basePrice + 0.03m, 0.05m));
            bars.Add(direction.Equals("short", StringComparison.OrdinalIgnoreCase)
                ? BuildReplayBar(basePrice - 1.1m, basePrice - 1.0m, basePrice - 1.2m, basePrice - 1.05m, 0.05m)
                : BuildReplayBar(basePrice + 1.1m, basePrice + 1.2m, basePrice + 0.8m, basePrice + 1.1m, 0.05m));
        }

        return bars.ToArray();
    }

    private static ReplayBar BuildReplayBar(decimal open, decimal high, decimal low, decimal close, decimal spread) =>
        new()
        {
            Timestamp = DateTimeOffset.UtcNow,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Spread = spread,
        };

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
            RuntimeMode = RuntimeMode.LocalFileBundle,
            ReleaseBundleInboxPath = bundleDir,
            ActiveReleaseBundlePath = Path.Combine(bundleDir, "active"),
            LastValidReleaseBundlePath = Path.Combine(bundleDir, "last_valid"),
            LocalRuntimeLogsPath = Path.Combine(bundleDir, "logs"),
            PaperStateSnapshotPath = Path.Combine(bundleDir, "paper_state_snapshot.json"),
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

    private static BotConfiguration BuildCloudConfig(string logsDir, CloudEmbeddedReleasePackage? package) =>
        new()
        {
            RuntimeMode = RuntimeMode.CloudEmbeddedBundle,
            ReleaseBundleInboxPath = string.Empty,
            ActiveReleaseBundlePath = string.Empty,
            LastValidReleaseBundlePath = string.Empty,
            LocalRuntimeLogsPath = logsDir,
            PaperStateSnapshotPath = Path.Combine(logsDir, "paper_state_snapshot.json"),
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

    private static CloudEmbeddedReleasePackage BuildCloudPackage(bool valid)
    {
        var package = new CloudEmbeddedReleasePackage
        {
            BotReleaseId = "cloud-release-001",
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
            EmbeddedManifestJson = "{\"paper_mode\":true}",
            EmbeddedStrategyJson = "{\"strategy\":\"paper\"}",
            EmbeddedChecksum = new string('a', 64),
        };

        return valid ? package : null;
    }

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
