namespace HermesPaperBot.Services;

using System;
using HermesPaperBot.Models;

/// <summary>
/// Validates paper-only bot configuration.
/// </summary>
public sealed class ConfigurationValidator
{
    /// <summary>
    /// Validates the provided configuration.
    /// </summary>
    public ValidationResult Validate(BotConfiguration config)
    {
        if (config is null)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "config_null",
            };
        }

        if (config.RuntimeMode == RuntimeMode.LocalFileBundle)
        {
            if (string.IsNullOrWhiteSpace(config.ReleaseBundleInboxPath) ||
                string.IsNullOrWhiteSpace(config.ActiveReleaseBundlePath) ||
                string.IsNullOrWhiteSpace(config.LocalRuntimeLogsPath))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Status = "invalid",
                    Reason = "required_path_missing",
                };
            }

            if (!config.ImportEnabled && string.IsNullOrWhiteSpace(config.LastValidReleaseBundlePath))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Status = "blocked",
                    Reason = "import_disabled_without_last_valid_bundle",
                };
            }
        }
        else if (config.RuntimeMode == RuntimeMode.CloudEmbeddedBundle)
        {
            if (config.CloudEmbeddedReleasePackage is null)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Status = "blocked",
                    Reason = "embedded_package_missing",
                };
            }
        }
        else
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "unknown_runtime_mode",
            };
        }

        if (config.ReloadIntervalSeconds < 5 || config.ReloadIntervalSeconds > 3600)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "reload_interval_out_of_range",
            };
        }

        if (config.ManualKillSwitch)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "blocked",
                Reason = "manual_kill_switch_active",
            };
        }

        if (!Enum.IsDefined(typeof(LogVerbosity), config.LogVerbosity))
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "unknown_log_verbosity",
            };
        }

        if (!config.NoAutoTrading || !config.HumanReviewRequired ||
            config.BrokerTradingEnabled || config.LiveTradingEnabled ||
            config.OrderApiEnabled || !config.PaperMode)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "blocked",
                Reason = "safety_relaxed",
            };
        }

        return new ValidationResult
        {
            IsValid = true,
            Status = "valid",
            Reason = "ok",
        };
    }
}
