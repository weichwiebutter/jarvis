namespace HermesPaperBot.Services;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using HermesPaperBot.Models;

/// <summary>
/// Imports release bundles in a paper-only flow.
/// </summary>
public sealed class ReleaseBundleImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Imports a bundle from the inbox.
    /// </summary>
    public ImportResult Import(string releaseBundleInboxPath)
    {
        if (string.IsNullOrWhiteSpace(releaseBundleInboxPath) || !Directory.Exists(releaseBundleInboxPath))
        {
            return new ImportResult
            {
                Success = false,
                Status = "bundle_missing",
                Reason = "inbox_missing",
                ActiveCandidatePath = string.Empty,
                LastValidBundlePath = string.Empty,
                DisabledUntilValidBundle = true,
            };
        }

        var manifestPath = Path.Combine(releaseBundleInboxPath, "ctrader_bot_release_manifest.json");
        var provenancePath = Path.Combine(releaseBundleInboxPath, "provenance.json");
        var checksumsPath = Path.Combine(releaseBundleInboxPath, "checksums.json");
        var signalPackagePath = Path.Combine(releaseBundleInboxPath, "ensemble_signal_agent_package.json");
        var signalSchemaPath = Path.Combine(releaseBundleInboxPath, "ensemble_signal_agent_package.schema.json");

        var bundleFiles = new BundleFileSet
        {
            BundleRootPath = releaseBundleInboxPath,
            ManifestPath = manifestPath,
            ProvenancePath = provenancePath,
            ChecksumsPath = checksumsPath,
            SignalPackagePath = signalPackagePath,
            SignalSchemaPath = signalSchemaPath,
        };

        if (!File.Exists(manifestPath) ||
            !File.Exists(provenancePath) ||
            !File.Exists(checksumsPath) ||
            !File.Exists(signalPackagePath) ||
            !File.Exists(signalSchemaPath))
        {
            return new ImportResult
            {
                Success = false,
                Status = "bundle_missing_required_files",
                Reason = "required_files_missing",
                BundleFiles = bundleFiles,
                ActiveCandidatePath = releaseBundleInboxPath,
                LastValidBundlePath = string.Empty,
                DisabledUntilValidBundle = true,
            };
        }

        var manifest = ReadJson<ReleaseBundleManifest>(manifestPath);
        var provenance = ReadJson<ProvenanceInfo>(provenancePath);
        var checksumEntries = ReadJson<ChecksumEntry[]>(checksumsPath) ?? [];

        if (manifest is null || provenance is null)
        {
            return new ImportResult
            {
                Success = false,
                Status = "bundle_invalid",
                Reason = "json_parse_failed",
                BundleFiles = bundleFiles,
                ActiveCandidatePath = releaseBundleInboxPath,
                LastValidBundlePath = string.Empty,
                Manifest = manifest,
                Provenance = provenance,
                ChecksumEntries = checksumEntries,
                DisabledUntilValidBundle = true,
            };
        }

        var checksumValidation = new ChecksumValidator().Validate(releaseBundleInboxPath, checksumEntries);
        var manifestValidation = new ReleaseBundleValidator().Validate(manifest, provenance, checksumEntries);
        var validationPassed = checksumValidation.IsValid && manifestValidation.IsValid;

        return new ImportResult
        {
            Success = validationPassed,
            Status = validationPassed ? "bundle_valid" : "bundle_invalid",
            Reason = validationPassed ? "ok" : "validation_failed",
            BundleFiles = bundleFiles,
            ActiveCandidatePath = releaseBundleInboxPath,
            LastValidBundlePath = string.Empty,
            Manifest = manifest,
            Provenance = provenance,
            ChecksumEntries = checksumEntries,
            FallbackPossible = !validationPassed,
            DisabledUntilValidBundle = !validationPassed,
        };
    }

    private static T? ReadJson<T>(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
