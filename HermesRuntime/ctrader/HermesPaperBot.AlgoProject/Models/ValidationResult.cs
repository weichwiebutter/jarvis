namespace HermesPaperBot.Models;

/// <summary>
/// Generic validation result for paper-only skeleton services.
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Indicates whether validation passed.
    /// </summary>
    public bool IsValid { get; init; } = false;

    /// <summary>
    /// Placeholder status text.
    /// </summary>
    public string Status { get; init; } = "not_implemented";

    /// <summary>
    /// Optional reason for a blocked or invalid result.
    /// </summary>
    public string Reason { get; init; } = "blocked_by_skeleton";
}
