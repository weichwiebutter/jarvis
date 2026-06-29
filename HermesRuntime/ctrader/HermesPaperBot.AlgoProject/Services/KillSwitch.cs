namespace HermesPaperBot.Services;

using HermesPaperBot.Models;

/// <summary>
/// Manages the paper-only kill-switch state.
/// </summary>
public sealed class KillSwitch
{
    /// <summary>
    /// Evaluates whether the kill switch should be active.
    /// </summary>
    public KillSwitchResult Evaluate(BotConfiguration config, ValidationResult validation)
    {
        if (config is not null && config.ManualKillSwitch)
        {
            return new KillSwitchResult
            {
                Active = true,
                Status = "blocked",
                Reason = "manual_kill_switch_active",
                BrokerAction = "none",
            };
        }

        if (validation is null || !validation.IsValid)
        {
            return new KillSwitchResult
            {
                Active = true,
                Status = "blocked",
                Reason = "validation_failed",
                BrokerAction = "none",
            };
        }

        return new KillSwitchResult
        {
            Active = false,
            Status = "passed",
            Reason = "ok",
            BrokerAction = "none",
        };
    }
}
