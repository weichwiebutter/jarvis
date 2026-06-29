namespace HermesPaperBot.Services;

using System;
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
        return Validate(manifest, provenance, []);
    }

    /// <summary>
    /// Validates the manifest, provenance, and checksums.
    /// </summary>
    public ValidationResult Validate(ReleaseBundleManifest manifest, ProvenanceInfo provenance, ChecksumEntry[] checksumEntries)
    {
        if (manifest is null || provenance is null)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "missing_manifest_or_provenance",
            };
        }

        if (string.IsNullOrWhiteSpace(manifest.BotReleaseId) ||
            string.IsNullOrWhiteSpace(manifest.BotVersion) ||
            string.IsNullOrWhiteSpace(manifest.StrategyPackageVersion) ||
            string.IsNullOrWhiteSpace(manifest.SchemaVersion))
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "missing_manifest_identity",
            };
        }

        if (manifest.ReleaseMode != ReleaseMode.PaperOnly)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "blocked",
                Reason = "rejected_release_mode",
            };
        }

        if (manifest.SafetyFlags is null || manifest.ForbiddenCapabilities is null)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "missing_manifest_policy_sections",
            };
        }

        if (!manifest.SafetyFlags.NoAutoTrading ||
            !manifest.SafetyFlags.HumanReviewRequired ||
            manifest.SafetyFlags.BrokerTradingEnabled ||
            manifest.SafetyFlags.LiveTradingEnabled ||
            manifest.SafetyFlags.OrderApiEnabled ||
            !manifest.SafetyFlags.PaperMode ||
            !string.Equals(manifest.SafetyFlags.BrokerAction, "none", StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "blocked",
                Reason = "safety_flags_invalid",
            };
        }

        if (!manifest.ForbiddenCapabilities.MarketOrderExecutionForbidden ||
            !manifest.ForbiddenCapabilities.LimitOrderPlacementForbidden ||
            !manifest.ForbiddenCapabilities.StopOrderPlacementForbidden ||
            !manifest.ForbiddenCapabilities.PositionModificationForbidden ||
            !manifest.ForbiddenCapabilities.PositionClosingForbidden ||
            !manifest.ForbiddenCapabilities.PendingOrderCancellationForbidden ||
            !manifest.ForbiddenCapabilities.ExternalNetworkAccessForbidden)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "blocked",
                Reason = "forbidden_capabilities_incomplete",
            };
        }

        if (checksumEntries is null || checksumEntries.Length == 0)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "missing_checksum_entries",
            };
        }

        if (string.IsNullOrWhiteSpace(provenance.BotReleaseId) ||
            string.IsNullOrWhiteSpace(provenance.BotVersion) ||
            string.IsNullOrWhiteSpace(provenance.StrategyPackageVersion) ||
            string.IsNullOrWhiteSpace(provenance.SchemaVersion))
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "missing_provenance_identity",
            };
        }

        if (!string.Equals(manifest.BotReleaseId, provenance.BotReleaseId, StringComparison.Ordinal) ||
            !string.Equals(manifest.BotVersion, provenance.BotVersion, StringComparison.Ordinal) ||
            !string.Equals(manifest.StrategyPackageVersion, provenance.StrategyPackageVersion, StringComparison.Ordinal) ||
            !string.Equals(manifest.SchemaVersion, provenance.SchemaVersion, StringComparison.Ordinal))
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "blocked",
                Reason = "manifest_provenance_mismatch",
            };
        }

        if (!provenance.PaperMode)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "blocked",
                Reason = "provenance_not_paper_only",
            };
        }

        return new ValidationResult
        {
            IsValid = true,
            Status = "valid",
            Reason = "ok",
        };
    }

    /// <summary>
    /// Validates an embedded cloud package.
    /// </summary>
    public ValidationResult Validate(CloudEmbeddedReleasePackage package)
    {
        if (package is null)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "embedded_package_missing",
            };
        }

        if (string.IsNullOrWhiteSpace(package.BotReleaseId) ||
            string.IsNullOrWhiteSpace(package.BotVersion) ||
            string.IsNullOrWhiteSpace(package.StrategyPackageVersion) ||
            string.IsNullOrWhiteSpace(package.SchemaVersion))
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "embedded_identity_missing",
            };
        }

        if (package.ReleaseMode != ReleaseMode.PaperOnly)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "blocked",
                Reason = "rejected_release_mode",
            };
        }

        if (package.SafetyFlags is null || package.ForbiddenCapabilities is null)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "invalid",
                Reason = "embedded_policy_sections_missing",
            };
        }

        if (!package.SafetyFlags.NoAutoTrading ||
            !package.SafetyFlags.HumanReviewRequired ||
            package.SafetyFlags.BrokerTradingEnabled ||
            package.SafetyFlags.LiveTradingEnabled ||
            package.SafetyFlags.OrderApiEnabled ||
            !package.SafetyFlags.PaperMode ||
            !string.Equals(package.SafetyFlags.BrokerAction, "none", StringComparison.OrdinalIgnoreCase))
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "blocked",
                Reason = "embedded_safety_flags_invalid",
            };
        }

        if (!package.ForbiddenCapabilities.MarketOrderExecutionForbidden ||
            !package.ForbiddenCapabilities.LimitOrderPlacementForbidden ||
            !package.ForbiddenCapabilities.StopOrderPlacementForbidden ||
            !package.ForbiddenCapabilities.PositionModificationForbidden ||
            !package.ForbiddenCapabilities.PositionClosingForbidden ||
            !package.ForbiddenCapabilities.PendingOrderCancellationForbidden ||
            !package.ForbiddenCapabilities.ExternalNetworkAccessForbidden)
        {
            return new ValidationResult
            {
                IsValid = false,
                Status = "blocked",
                Reason = "embedded_forbidden_capabilities_incomplete",
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
