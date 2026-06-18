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
}
