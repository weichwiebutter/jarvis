namespace HermesPaperBot.Services;

using HermesPaperBot.Models;

/// <summary>
/// Validates release bundle structure and content.
/// </summary>
public sealed class ReleaseBundleValidator
{
    /// <summary>
    /// Validates the manifest and provenance.
    /// </summary>
    public ValidationResult Validate(ReleaseBundleManifest manifest, ProvenanceInfo provenance)
    {
        return new ValidationResult
        {
            IsValid = false,
            Status = "not_implemented",
            Reason = "blocked_by_skeleton",
        };
    }
}
