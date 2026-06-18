namespace HermesPaperBot.Services;

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
        return new SafetyResult
        {
            Passed = false,
            Status = "not_implemented",
            BrokerAction = "none",
        };
    }
}
