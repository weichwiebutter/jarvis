namespace HermesPaperBot.Models;

/// <summary>
/// Summary of bundle validation results.
/// </summary>
public sealed class BundleValidationSummary
{
    public bool IsValid { get; init; } = false;
    public string Status { get; init; } = "not_implemented";
    public string Reason { get; init; } = "blocked_by_skeleton";
    public ValidationResult ManifestValidation { get; init; } = new ValidationResult();
    public ValidationResult ChecksumValidation { get; init; } = new ValidationResult();
    public SafetyResult SafetyValidation { get; init; } = new SafetyResult();
    public SafetyResult DriftValidation { get; init; } = new SafetyResult();
    public bool FallbackPossible { get; init; } = false;
    public bool DisabledUntilValidBundle { get; init; } = false;
}
