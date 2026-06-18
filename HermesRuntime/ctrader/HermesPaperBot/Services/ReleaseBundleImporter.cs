namespace HermesPaperBot.Services;

using HermesPaperBot.Models;

/// <summary>
/// Imports release bundles in a paper-only flow.
/// </summary>
public sealed class ReleaseBundleImporter
{
    /// <summary>
    /// Imports a bundle from the inbox.
    /// </summary>
    public BotState Import()
    {
        return new BotState
        {
            Status = "not_implemented",
            KillSwitchActive = false,
            LastBundleValid = false,
        };
    }
}
