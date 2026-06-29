namespace HermesPaperBot.Services;

using HermesPaperBot.Models;

/// <summary>
/// Guards against strategy drift.
/// </summary>
public sealed class DriftGuard
{
    /// <summary>
    /// Checks drift severity and optional summary state.
    /// </summary>
    public SafetyResult Check(ReleaseBundleManifest manifest, DriftSummary? driftSummary = null)
    {
        if (driftSummary is not null)
        {
            if (driftSummary.BlockingDriftFound ||
                driftSummary.OverallDriftSeverity is DriftSeverity.Blocking or DriftSeverity.High)
            {
                return new SafetyResult
                {
                    Passed = false,
                    Status = "blocked",
                    Reason = "blocking_drift",
                    BrokerAction = "none",
                };
            }
        }

        return new SafetyResult
        {
            Passed = true,
            Status = "passed",
            Reason = "ok",
            BrokerAction = "none",
        };
    }
}
