namespace HermesPaperBot.Models;

/// <summary>
/// Bundle provenance.
/// </summary>
public sealed class ProvenanceInfo
{
    /// <summary>
    /// Provenance identifier.
    /// </summary>
    public string ProvenanceId { get; init; } = string.Empty;

    /// <summary>
    /// Generated timestamp.
    /// </summary>
    public string GeneratedAt { get; init; } = string.Empty;

    /// <summary>
    /// Source system.
    /// </summary>
    public string SourceSystem { get; init; } = "HermesRuntime";

    /// <summary>
    /// Safety flag marker.
    /// </summary>
    public bool PaperMode { get; init; } = true;

    /// <summary>
    /// Bot release identifier.
    /// </summary>
    public string BotReleaseId { get; init; } = string.Empty;

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
