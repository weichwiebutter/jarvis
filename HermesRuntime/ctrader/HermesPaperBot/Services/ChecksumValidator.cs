namespace HermesPaperBot.Services;

using HermesPaperBot.Models;

/// <summary>
/// Validates bundle checksums.
/// </summary>
public sealed class ChecksumValidator
{
    /// <summary>
    /// Validates checksum entries.
    /// </summary>
    public ValidationResult Validate(ChecksumEntry[] entries)
    {
        return new ValidationResult
        {
            IsValid = false,
            Status = "not_implemented",
            Reason = "blocked_by_skeleton",
        };
    }
}
