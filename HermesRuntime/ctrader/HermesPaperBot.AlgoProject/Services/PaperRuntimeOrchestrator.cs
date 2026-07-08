namespace HermesPaperBot.Services;

using System.Collections.Generic;
using System.Text.Json;
using HermesPaperBot.Models;

/// <summary>
/// Orchestrates the defensive paper runtime validation flow.
/// </summary>
public sealed class PaperRuntimeOrchestrator
{
    /// <summary>
    /// Runs one defensive runtime step.
    /// </summary>
    public RuntimeStepResult RunStep(BotConfiguration config)
        => RunStep(config, null);

    /// <summary>
    /// Runs one defensive runtime step with a supplied market context.
    /// </summary>
    public RuntimeStepResult RunStep(BotConfiguration config, RuntimeMarketContext? marketContext)
    {
        var runtimeMarketContext = marketContext ?? new RuntimeMarketContext();
        var marketContextSeen = marketContext is not null;
        var reasons = new List<string>();
        var restoreResult = new PaperStateStore(config.PaperStateSnapshotPath, config.PaperSnapshotRecoveryMode).Load();
        var configValidation = new ConfigurationValidator().Validate(config);
        reasons.Add(configValidation.Reason);

        if (!configValidation.IsValid)
        {
            var killSwitch = new KillSwitch().Evaluate(config, configValidation);
            reasons.Add(killSwitch.Reason);

            var earlyResult = new RuntimeStepResult
            {
                Success = false,
                State = killSwitch.Active ? "blocked_by_config" : "config_invalid",
                ConfigValid = false,
                ImportAttempted = false,
                ImportValid = false,
                BundleValid = false,
                ChecksumValid = false,
                SafetyAllowed = false,
                DriftAllowed = false,
                KillSwitchActive = killSwitch.Active,
                FallbackPossible = false,
                DisabledUntilValidBundle = true,
                PaperDecision = "would_block_by_safety",
                BrokerAction = "none",
                Reasons = reasons.ToArray(),
                MarketContext = runtimeMarketContext,
                MarketContextSeen = marketContextSeen,
                RestoreState = restoreResult.RestoreState,
                RestoreReason = restoreResult.RestoreReason,
                RestoreSnapshotValid = restoreResult.RestoreSnapshotValid,
                RestoreFreshStateUsed = restoreResult.RestoreFreshStateUsed,
                RestoreActiveTradeCount = restoreResult.RestoreActiveTradeCount,
                RestoreFirstActiveSignalId = restoreResult.RestoreFirstActiveSignalId,
                RestoreFirstActiveEntry = restoreResult.RestoreFirstActiveEntry,
                RestoreFirstActiveSl = restoreResult.RestoreFirstActiveSl,
                RestoreFirstActiveTp = restoreResult.RestoreFirstActiveTp,
            };

            return FinalizeResult(config, earlyResult);
        }

        var importResult = default(ImportResult);
        var embeddedValidation = default(ValidationResult);
        var importAttempted = false;
        var importValid = false;

        var checksumValid = false;
        var bundleValid = false;
        var safetyAllowed = false;
        var driftAllowed = false;
        var killSwitchActive = false;

        ReleaseBundleManifest? candidateManifest = null;
        ProvenanceInfo? candidateProvenance = null;
        CloudEmbeddedReleasePackage? embeddedPackage = null;

        if (config.RuntimeMode == RuntimeMode.CloudEmbeddedBundle)
        {
            embeddedPackage = config.CloudEmbeddedReleasePackage;
            embeddedValidation = new ReleaseBundleValidator().Validate(embeddedPackage);
            importValid = embeddedValidation.IsValid;
            reasons.Add(embeddedValidation.Reason);
            candidateManifest = embeddedPackage is null
                ? null
                : new ReleaseBundleManifest
                {
                    BotReleaseId = embeddedPackage.BotReleaseId,
                    BotVersion = embeddedPackage.BotVersion,
                    StrategyPackageVersion = embeddedPackage.StrategyPackageVersion,
                    SchemaVersion = embeddedPackage.SchemaVersion,
                    ReleaseMode = embeddedPackage.ReleaseMode,
                    SafetyFlags = embeddedPackage.SafetyFlags,
                    ForbiddenCapabilities = embeddedPackage.ForbiddenCapabilities,
                };
            candidateProvenance = embeddedPackage is null
                ? null
                : new ProvenanceInfo
                {
                    ProvenanceId = "embedded",
                    GeneratedAt = "embedded",
                    SourceSystem = "HermesRuntime",
                    PaperMode = true,
                    BotReleaseId = embeddedPackage.BotReleaseId,
                    BotVersion = embeddedPackage.BotVersion,
                    StrategyPackageVersion = embeddedPackage.StrategyPackageVersion,
                    SchemaVersion = embeddedPackage.SchemaVersion,
                };
        }
        else
        {
            importAttempted = true;
            importResult = new ReleaseBundleImporter().Import(config.ReleaseBundleInboxPath);
            reasons.Add(importResult.Reason);
            importValid = importResult.Success;
            candidateManifest = importResult.Manifest;
            candidateProvenance = importResult.Provenance;
        }

        if ((config.RuntimeMode == RuntimeMode.CloudEmbeddedBundle && importValid && candidateManifest is not null) ||
            (config.RuntimeMode == RuntimeMode.LocalFileBundle && importResult is not null && importResult.Success && candidateManifest is not null && candidateProvenance is not null))
        {
            if (config.RuntimeMode == RuntimeMode.LocalFileBundle && importResult is not null)
            {
                checksumValid = new ChecksumValidator().Validate(importResult.BundleFiles.BundleRootPath, importResult.ChecksumEntries).IsValid;
                bundleValid = new ReleaseBundleValidator().Validate(candidateManifest, candidateProvenance, importResult.ChecksumEntries).IsValid;
            }
            else if (config.RuntimeMode == RuntimeMode.CloudEmbeddedBundle && embeddedPackage is not null)
            {
                checksumValid = !string.IsNullOrWhiteSpace(embeddedPackage.EmbeddedChecksum) && embeddedPackage.EmbeddedChecksum.Length == 64;
                bundleValid = embeddedValidation?.IsValid == true;
            }

            safetyAllowed = new SafetyGate().Verify(config, candidateManifest).Passed;
            driftAllowed = new DriftGuard().Check(candidateManifest).Passed;

            var aggregateValidation = new ValidationResult
            {
                IsValid = checksumValid && bundleValid && safetyAllowed && driftAllowed,
                Status = checksumValid && bundleValid && safetyAllowed && driftAllowed ? "valid" : "blocked",
                Reason = checksumValid && bundleValid && safetyAllowed && driftAllowed ? "ok" : "aggregate_validation_failed",
            };

            var killSwitch = new KillSwitch().Evaluate(config, aggregateValidation);
            killSwitchActive = killSwitch.Active;
            reasons.Add(killSwitch.Reason);
        }
        else
        {
            killSwitchActive = true;
            if (config.RuntimeMode == RuntimeMode.LocalFileBundle && importResult is not null)
            {
                reasons.Add(importResult.DisabledUntilValidBundle ? "disabled_until_valid_bundle" : "fallback_possible");
            }
            else
            {
                reasons.Add("embedded_package_invalid");
            }
        }

        var state = killSwitchActive
            ? "blocked_by_safety"
            : (config.RuntimeMode == RuntimeMode.CloudEmbeddedBundle ? (importValid ? "bundle_valid" : "bundle_invalid") : (importResult is not null && importResult.Success ? "bundle_valid" : "bundle_invalid"));

        var paperDecision = new PaperDecisionEngine().Evaluate(
            new BotState
            {
                Status = state,
                KillSwitchActive = killSwitchActive,
                LastBundleValid = config.RuntimeMode == RuntimeMode.CloudEmbeddedBundle ? importValid : (importResult is not null && importResult.Success),
            },
            runtimeMarketContext);

        reasons.Add(paperDecision.Reason);

        var runtimeResult = new RuntimeStepResult
        {
            Success = !killSwitchActive && importValid && checksumValid && bundleValid && safetyAllowed && driftAllowed,
            State = state,
            ConfigValid = true,
            ImportAttempted = importAttempted,
            ImportValid = importValid,
            BundleValid = bundleValid,
            ChecksumValid = checksumValid,
            SafetyAllowed = safetyAllowed,
            DriftAllowed = driftAllowed,
            KillSwitchActive = killSwitchActive,
            FallbackPossible = config.RuntimeMode == RuntimeMode.LocalFileBundle && importResult is not null && importResult.FallbackPossible,
            DisabledUntilValidBundle = config.RuntimeMode == RuntimeMode.LocalFileBundle && importResult is not null && importResult.DisabledUntilValidBundle,
            PaperDecision = paperDecision.Decision,
            BrokerAction = "none",
            Reasons = reasons.ToArray(),
            MarketContext = runtimeMarketContext,
            MarketContextSeen = marketContextSeen,
            PackageLoaded = embeddedPackage is not null,
            SignalPackageLoaded = HasSignalPackageJson(embeddedPackage),
            SignalCount = GetEmbeddedSignalCount(embeddedPackage),
            SignalPackageJsonLength = GetEmbeddedSignalPackageJsonLength(embeddedPackage),
            SignalPackageParseStatus = GetEmbeddedSignalParseStatus(embeddedPackage),
            FirstSignalId = GetFirstEmbeddedSignalId(embeddedPackage),
            ChartAnnotationLoaded = embeddedPackage is not null && !string.IsNullOrWhiteSpace(embeddedPackage.ChartAnnotationSpecJson),
            RestoreState = restoreResult.RestoreState,
            RestoreReason = restoreResult.RestoreReason,
            RestoreSnapshotValid = restoreResult.RestoreSnapshotValid,
            RestoreFreshStateUsed = restoreResult.RestoreFreshStateUsed,
            RestoreActiveTradeCount = restoreResult.RestoreActiveTradeCount,
            RestoreFirstActiveSignalId = restoreResult.RestoreFirstActiveSignalId,
            RestoreFirstActiveEntry = restoreResult.RestoreFirstActiveEntry,
            RestoreFirstActiveSl = restoreResult.RestoreFirstActiveSl,
            RestoreFirstActiveTp = restoreResult.RestoreFirstActiveTp,
        };

        return FinalizeResult(config, runtimeResult);
    }


    private static bool HasSignalPackageJson(CloudEmbeddedReleasePackage? package)
        => !string.IsNullOrWhiteSpace(package?.SignalPackageJson);

    private static int GetEmbeddedSignalCount(CloudEmbeddedReleasePackage? package)
    {
        var signalPackageJson = package?.SignalPackageJson;
        if (string.IsNullOrWhiteSpace(signalPackageJson))
        {
            return 0;
        }

        try
        {
            using var document = JsonDocument.Parse(signalPackageJson);
            var root = document.RootElement;
            if (root.TryGetProperty("signal_count", out var signalCount) && signalCount.ValueKind == JsonValueKind.Number && signalCount.TryGetInt32(out var parsedCount))
            {
                return parsedCount;
            }

            if (root.TryGetProperty("signals", out var signals) && signals.ValueKind == JsonValueKind.Array)
            {
                return signals.GetArrayLength();
            }

            return root.TryGetProperty("signal_decision", out var signalDecision) && signalDecision.ValueKind == JsonValueKind.Object ? 1 : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static string GetEmbeddedSignalPackageJsonLength(CloudEmbeddedReleasePackage? package)
        => (package?.SignalPackageJson?.Length ?? 0).ToString();

    private static string GetEmbeddedSignalParseStatus(CloudEmbeddedReleasePackage? package)
        => package is null
            ? "package_missing"
            : HasSignalPackageJson(package) ? "ok" : "signal_missing";

    private static string GetFirstEmbeddedSignalId(CloudEmbeddedReleasePackage? package)
    {
        var signalPackageJson = package?.SignalPackageJson;
        if (string.IsNullOrWhiteSpace(signalPackageJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(signalPackageJson);
            var root = document.RootElement;
            if (root.TryGetProperty("signals", out var signals) && signals.ValueKind == JsonValueKind.Array && signals.GetArrayLength() > 0)
            {
                var first = signals[0];
                if (first.TryGetProperty("signal_id", out var signalId) && signalId.ValueKind == JsonValueKind.String)
                {
                    return signalId.GetString() ?? string.Empty;
                }
            }

            if (root.TryGetProperty("signal_decision", out var signalDecision) && signalDecision.ValueKind == JsonValueKind.Object &&
                signalDecision.TryGetProperty("strategy_id", out var strategyId) && strategyId.ValueKind == JsonValueKind.String)
            {
                return strategyId.GetString() ?? string.Empty;
            }
        }
        catch
        {
        }

        return string.Empty;
    }

    private static RuntimeStepResult FinalizeResult(BotConfiguration config, RuntimeStepResult runtimeResult)
    {
        var logsPath = config.LocalRuntimeLogsPathOverride ?? config.LocalRuntimeLogsPath;
        var logger = new PaperLogger();
        var summaryWriter = new RuntimeSummaryWriter();
        var loggingOk = logger.Write(logsPath, runtimeResult);
        var summaryOk = summaryWriter.Write(logsPath, runtimeResult, config);

        if (!loggingOk || !summaryOk)
        {
            var loggingReasons = new List<string>(runtimeResult.Reasons)
            {
                "logging_failed",
            };

            return new RuntimeStepResult
            {
                Success = runtimeResult.Success,
                State = runtimeResult.State,
                ConfigValid = runtimeResult.ConfigValid,
                ImportAttempted = runtimeResult.ImportAttempted,
                ImportValid = runtimeResult.ImportValid,
                BundleValid = runtimeResult.BundleValid,
                ChecksumValid = runtimeResult.ChecksumValid,
                SafetyAllowed = runtimeResult.SafetyAllowed,
                DriftAllowed = runtimeResult.DriftAllowed,
                KillSwitchActive = runtimeResult.KillSwitchActive,
                FallbackPossible = runtimeResult.FallbackPossible,
                DisabledUntilValidBundle = runtimeResult.DisabledUntilValidBundle,
                PaperDecision = runtimeResult.PaperDecision,
                BrokerAction = "none",
                Reasons = loggingReasons.ToArray(),
                LoggingStatus = "logging_failed",
                MarketContext = runtimeResult.MarketContext,
                MarketContextSeen = runtimeResult.MarketContextSeen,
                PackageLoaded = runtimeResult.PackageLoaded,
                SignalPackageLoaded = runtimeResult.SignalPackageLoaded,
                SignalCount = runtimeResult.SignalCount,
                SignalPackageJsonLength = runtimeResult.SignalPackageJsonLength,
                SignalPackageParseStatus = runtimeResult.SignalPackageParseStatus,
                FirstSignalId = runtimeResult.FirstSignalId,
                ChartAnnotationLoaded = runtimeResult.ChartAnnotationLoaded,
                RestoreState = runtimeResult.RestoreState,
                RestoreReason = runtimeResult.RestoreReason,
                RestoreSnapshotValid = runtimeResult.RestoreSnapshotValid,
                RestoreFreshStateUsed = runtimeResult.RestoreFreshStateUsed,
                RestoreActiveTradeCount = runtimeResult.RestoreActiveTradeCount,
                RestoreFirstActiveSignalId = runtimeResult.RestoreFirstActiveSignalId,
                RestoreFirstActiveEntry = runtimeResult.RestoreFirstActiveEntry,
                RestoreFirstActiveSl = runtimeResult.RestoreFirstActiveSl,
                RestoreFirstActiveTp = runtimeResult.RestoreFirstActiveTp,
            };
        }

        return new RuntimeStepResult
        {
            Success = runtimeResult.Success,
            State = runtimeResult.State,
            ConfigValid = runtimeResult.ConfigValid,
            ImportAttempted = runtimeResult.ImportAttempted,
            ImportValid = runtimeResult.ImportValid,
            BundleValid = runtimeResult.BundleValid,
            ChecksumValid = runtimeResult.ChecksumValid,
            SafetyAllowed = runtimeResult.SafetyAllowed,
            DriftAllowed = runtimeResult.DriftAllowed,
            KillSwitchActive = runtimeResult.KillSwitchActive,
            FallbackPossible = runtimeResult.FallbackPossible,
            DisabledUntilValidBundle = runtimeResult.DisabledUntilValidBundle,
            PaperDecision = runtimeResult.PaperDecision,
            BrokerAction = "none",
            Reasons = runtimeResult.Reasons,
            LoggingStatus = "ok",
            MarketContext = runtimeResult.MarketContext,
            MarketContextSeen = runtimeResult.MarketContextSeen,
            PackageLoaded = runtimeResult.PackageLoaded,
            SignalPackageLoaded = runtimeResult.SignalPackageLoaded,
            SignalCount = runtimeResult.SignalCount,
            SignalPackageJsonLength = runtimeResult.SignalPackageJsonLength,
            SignalPackageParseStatus = runtimeResult.SignalPackageParseStatus,
            FirstSignalId = runtimeResult.FirstSignalId,
            ChartAnnotationLoaded = runtimeResult.ChartAnnotationLoaded,
            RestoreState = runtimeResult.RestoreState,
            RestoreReason = runtimeResult.RestoreReason,
            RestoreSnapshotValid = runtimeResult.RestoreSnapshotValid,
            RestoreFreshStateUsed = runtimeResult.RestoreFreshStateUsed,
            RestoreActiveTradeCount = runtimeResult.RestoreActiveTradeCount,
            RestoreFirstActiveSignalId = runtimeResult.RestoreFirstActiveSignalId,
            RestoreFirstActiveEntry = runtimeResult.RestoreFirstActiveEntry,
            RestoreFirstActiveSl = runtimeResult.RestoreFirstActiveSl,
            RestoreFirstActiveTp = runtimeResult.RestoreFirstActiveTp,
        };
    }
}
