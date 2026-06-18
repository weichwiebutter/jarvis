namespace HermesPaperBot.Models;

/// <summary>
/// Release bundle manifest.
/// </summary>
public sealed class ReleaseBundleManifest
{
    /// <summary>
    /// Release mode.
    /// </summary>
    public ReleaseMode ReleaseMode { get; init; } = ReleaseMode.PaperOnly;

    /// <summary>
    /// Bot version.
    /// </summary>
    public string BotVersion { get; init; } = string.Empty;

    /// <summary>
    /// Bot release identifier.
    /// </summary>
    public string BotReleaseId { get; init; } = string.Empty;

    /// <summary>
    /// Strategy package version.
    /// </summary>
    public string StrategyPackageVersion { get; init; } = string.Empty;

    /// <summary>
    /// Schema version.
    /// </summary>
    public string SchemaVersion { get; init; } = string.Empty;

    /// <summary>
    /// Safety flags snapshot.
    /// </summary>
    public SafetyFlags SafetyFlags { get; init; } = new SafetyFlags();

    /// <summary>
    /// Forbidden capabilities snapshot.
    /// </summary>
    public ForbiddenCapabilities ForbiddenCapabilities { get; init; } = new ForbiddenCapabilities();
}
