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
    public SafetyResult Evaluate(BotConfiguration config, ValidationResult validation)
    {
        return new SafetyResult
        {
            Passed = false,
            Status = "not_implemented",
            BrokerAction = "none",
        };
    }
}
