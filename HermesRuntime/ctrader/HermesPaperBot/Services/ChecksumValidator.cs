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
        if (entries is null || entries.Length == 0)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "entries_empty",
            };
        }

        var requiredEntryCount = 0;

        foreach (var entry in entries)
        {
            if (entry is null)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Status = "invalid",
                    Reason = "entry_null",
                };
            }

            if (string.IsNullOrWhiteSpace(entry.Path) ||
                string.IsNullOrWhiteSpace(entry.Sha256) ||
                entry.Sha256.Length != 64 ||
                entry.SizeBytes < 0)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Status = "invalid",
                    Reason = "checksum_structure_invalid",
                };
            }

            if (entry.Required)
            {
                requiredEntryCount++;
            }
        }

        if (requiredEntryCount == 0)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "required_entries_missing",
            };
        }

        return new ValidationResult
        {
            IsValid = true,
            Status = "valid",
            Reason = "ok",
        };
    }
}
