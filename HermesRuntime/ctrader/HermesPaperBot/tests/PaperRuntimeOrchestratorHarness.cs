namespace HermesPaperBot.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
            results.Add(RunInvalidConfigCase(tempRoot));
            results.Add(RunSafetyViolationCase(tempRoot));
            results.Add(RunMissingBundleFileCase(tempRoot));
            results.Add(RunChecksumMismatchCase(tempRoot));
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

    private static RuntimeMarketContext BuildMarketContext(string symbol, string timeframe, decimal bid, decimal ask) =>
        new()
        {
            CurrentSymbol = symbol,
            CurrentTimeframe = timeframe,
            Bid = bid,
            Ask = ask,
            Spread = ask - bid,
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
