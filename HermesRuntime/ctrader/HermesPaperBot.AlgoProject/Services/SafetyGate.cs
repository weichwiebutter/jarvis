namespace HermesPaperBot.Services;

using System;
using HermesPaperBot.Models;

/// <summary>
/// Enforces paper-only safety rules.
/// </summary>
public sealed class SafetyGate
{
    /// <summary>
    /// Verifies safety state.
    /// </summary>
    public SafetyResult Verify(BotConfiguration config, ReleaseBundleManifest manifest)
    {
        if (config is null || manifest is null)
        {
            return new SafetyResult
            {
                Passed = false,
                Status = "invalid",
                Reason = "missing_config_or_manifest",
                BrokerAction = "none",
            };
        }

        if (!config.NoAutoTrading || !config.HumanReviewRequired ||
            config.BrokerTradingEnabled || config.LiveTradingEnabled ||
            config.OrderApiEnabled || !config.PaperMode)
        {
            return new SafetyResult
            {
                Passed = false,
                Status = "blocked",
                Reason = "config_safety_failed",
                BrokerAction = "none",
            };
        }

        if (manifest.SafetyFlags is null)
        {
            return new SafetyResult
            {
                Passed = false,
                Status = "invalid",
                Reason = "missing_safety_flags",
                BrokerAction = "none",
            };
        }

        if (!manifest.SafetyFlags.NoAutoTrading ||
            !manifest.SafetyFlags.HumanReviewRequired ||
            manifest.SafetyFlags.BrokerTradingEnabled ||
            manifest.SafetyFlags.LiveTradingEnabled ||
            manifest.SafetyFlags.OrderApiEnabled ||
            !manifest.SafetyFlags.PaperMode ||
            !string.Equals(manifest.SafetyFlags.BrokerAction, "none", StringComparison.OrdinalIgnoreCase))
        {
            return new SafetyResult
            {
                Passed = false,
                Status = "blocked",
                Reason = "manifest_safety_failed",
                BrokerAction = "none",
            };
        }

        return new SafetyResult
        {
            Passed = true,
            Status = "passed",
            BrokerAction = "none",
            Reason = "ok",
        };
    }
}
