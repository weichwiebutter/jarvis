namespace HermesPaperBot.Models;

/// <summary>
/// Result of activating a validated bundle.
/// </summary>
public sealed class BundleActivationResult
{
    public bool Activated { get; init; } = false;
    public string Status { get; init; } = "not_implemented";
    public string Reason { get; init; } = "blocked_by_skeleton";
    public string ActiveCandidatePath { get; init; } = string.Empty;
    public string LastValidBundlePath { get; init; } = string.Empty;
    public bool FallbackPossible { get; init; } = false;
}
