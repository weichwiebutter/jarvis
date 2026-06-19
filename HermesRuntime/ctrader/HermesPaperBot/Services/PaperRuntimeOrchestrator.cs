namespace HermesPaperBot.Services;

using System.Collections.Generic;
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
        var reasons = new List<string>();
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
        };

        return FinalizeResult(config, runtimeResult);
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
        };
    }
}
