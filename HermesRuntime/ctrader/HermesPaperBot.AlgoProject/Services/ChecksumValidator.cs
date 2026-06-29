namespace HermesPaperBot.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HermesPaperBot.Models;

/// <summary>
/// Validates bundle checksums.
/// </summary>
public sealed class ChecksumValidator
{
    /// <summary>
    /// Validates checksum entries.
    /// </summary>
    public ValidationResult Validate(string bundleRootPath, ChecksumEntry[] entries)
    {
        if (string.IsNullOrWhiteSpace(bundleRootPath) || !Directory.Exists(bundleRootPath))
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "bundle_root_missing",
            };
        }

        if (entries is null || entries.Length == 0)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "entries_empty",
            };
        }

        var requiredEntries = new List<ChecksumEntry>();
        var entryMap = new Dictionary<string, ChecksumEntry>(StringComparer.OrdinalIgnoreCase);

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
                requiredEntries.Add(entry);
            }

            entryMap[Normalize(entry.Path)] = entry;
        }

        if (requiredEntries.Count == 0)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "required_entries_missing",
            };
        }

        foreach (var entry in requiredEntries)
        {
            var normalizedPath = Normalize(entry.Path);
            if (normalizedPath.EndsWith("checksums.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fullPath = Path.IsPathRooted(entry.Path)
                ? entry.Path
                : Path.Combine(bundleRootPath, entry.Path);

            if (!File.Exists(fullPath))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Status = "invalid",
                    Reason = "required_file_missing",
                };
            }

            var actualSha256 = ComputeSha256(fullPath);
            var actualSize = new FileInfo(fullPath).Length;

            if (!string.Equals(actualSha256, entry.Sha256, StringComparison.OrdinalIgnoreCase) ||
                actualSize != entry.SizeBytes)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Status = "invalid",
                    Reason = "checksum_mismatch",
                };
            }
        }

        return new ValidationResult
        {
            IsValid = true,
            Status = "valid",
            Reason = "ok",
        };
    }

    private static string Normalize(string path) =>
        path.Replace('\\', '/').Trim();

    private static string ComputeSha256(string path)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(path);
        var hash = sha256.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
