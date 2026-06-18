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
    {
        var reasons = new List<string>();
        var configValidation = new ConfigurationValidator().Validate(config);
        reasons.Add(configValidation.Reason);

        if (!configValidation.IsValid)
        {
            var killSwitch = new KillSwitch().Evaluate(config, configValidation);
            reasons.Add(killSwitch.Reason);

            return new RuntimeStepResult
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
            };
        }

        var importResult = new ReleaseBundleImporter().Import(config.ReleaseBundleInboxPath);
        reasons.Add(importResult.Reason);

        var checksumValid = false;
        var bundleValid = false;
        var safetyAllowed = false;
        var driftAllowed = false;
        var killSwitchActive = false;
        var candidateManifest = importResult.Manifest;
        var candidateProvenance = importResult.Provenance;

        if (importResult.Success && candidateManifest is not null && candidateProvenance is not null)
        {
            checksumValid = new ChecksumValidator().Validate(importResult.BundleFiles.BundleRootPath, importResult.ChecksumEntries).IsValid;
            bundleValid = new ReleaseBundleValidator().Validate(candidateManifest, candidateProvenance, importResult.ChecksumEntries).IsValid;
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
            reasons.Add(importResult.DisabledUntilValidBundle ? "disabled_until_valid_bundle" : "fallback_possible");
        }

        var state = killSwitchActive
            ? "blocked_by_safety"
            : (importResult.Success ? "bundle_valid" : "bundle_invalid");

        var paperDecision = new PaperDecisionEngine().Evaluate(
            new BotState
            {
                Status = state,
                KillSwitchActive = killSwitchActive,
                LastBundleValid = importResult.Success,
            },
            new RuntimeMarketContext());

        reasons.Add(paperDecision.Reason);

        return new RuntimeStepResult
        {
            Success = !killSwitchActive && importResult.Success && checksumValid && bundleValid && safetyAllowed && driftAllowed,
            State = state,
            ConfigValid = true,
            ImportAttempted = true,
            ImportValid = importResult.Success,
            BundleValid = bundleValid,
            ChecksumValid = checksumValid,
            SafetyAllowed = safetyAllowed,
            DriftAllowed = driftAllowed,
            KillSwitchActive = killSwitchActive,
            FallbackPossible = importResult.FallbackPossible,
            DisabledUntilValidBundle = importResult.DisabledUntilValidBundle,
            PaperDecision = paperDecision.Decision,
            BrokerAction = "none",
            Reasons = reasons.ToArray(),
        };
    }
}
