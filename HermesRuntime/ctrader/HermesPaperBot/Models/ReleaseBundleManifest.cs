namespace HermesPaperBot.Models;

/// <summary>
/// Release bundle manifest.
/// </summary>
public sealed class ReleaseBundleManifest
{
    /// <summary>
    /// Release mode.
    /// </summary>
    public string ReleaseMode { get; init; } = "paper_only";

    /// <summary>
    /// Bot version.
    /// </summary>
    public string BotVersion { get; init; } = string.Empty;

    /// <summary>
    /// Strategy package version.
    /// </summary>
    public string StrategyPackageVersion { get; init; } = string.Empty;

    /// <summary>
    /// Schema version.
    /// </summary>
    public string SchemaVersion { get; init; } = string.Empty;
}
