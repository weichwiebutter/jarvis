namespace HermesPaperBot.Models;

/// <summary>
/// Result of a local bundle import.
/// </summary>
public sealed class ImportResult
{
    public bool Success { get; init; } = false;
    public string Status { get; init; } = "not_implemented";
    public string Reason { get; init; } = "blocked_by_skeleton";
    public BundleFileSet BundleFiles { get; init; } = new BundleFileSet();
    public string ActiveCandidatePath { get; init; } = string.Empty;
    public string LastValidBundlePath { get; init; } = string.Empty;
    public ReleaseBundleManifest? Manifest { get; init; }
    public ProvenanceInfo? Provenance { get; init; }
    public ChecksumEntry[] ChecksumEntries { get; init; } = [];
    public bool FallbackPossible { get; init; } = false;
    public bool DisabledUntilValidBundle { get; init; } = false;
}
