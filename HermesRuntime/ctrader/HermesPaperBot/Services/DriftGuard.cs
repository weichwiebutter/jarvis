namespace HermesPaperBot.Services;

using HermesPaperBot.Models;

/// <summary>
/// Guards against strategy drift.
/// </summary>
public sealed class DriftGuard
{
    /// <summary>
    /// Checks drift severity.
    /// </summary>
    public SafetyResult Check(ReleaseBundleManifest manifest)
    {
        return new SafetyResult
        {
            Passed = false,
            Status = "not_implemented",
            BrokerAction = "none",
        };
    }
}
