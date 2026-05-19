using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed class SnapshotValidator
{
    public SnapshotValidationResult Validate(string manifestPath)
    {
        try
        {
            var manifest = JsonSerializer.Deserialize<SnapshotManifest>(
                File.ReadAllText(manifestPath),
                JsonDefaults.SnapshotReadOptions);

            if (manifest is null)
            {
                return SnapshotValidationResult.Invalid($"Manifest is empty: {manifestPath}");
            }

            if (!File.Exists(manifest.SnapshotPath))
            {
                return SnapshotValidationResult.Invalid(
                    $"Snapshot file does not exist: {manifest.SnapshotPath}",
                    manifest);
            }

            var snapshot = JsonSerializer.Deserialize<RuntimeSnapshot>(
                File.ReadAllText(manifest.SnapshotPath),
                JsonDefaults.SnapshotReadOptions);

            if (snapshot is null)
            {
                return SnapshotValidationResult.Invalid(
                    $"Snapshot is empty or invalid: {manifest.SnapshotPath}",
                    manifest);
            }

            if (!string.Equals(snapshot.SnapshotId, manifest.SnapshotId, StringComparison.Ordinal))
            {
                return SnapshotValidationResult.Invalid(
                    $"SnapshotId mismatch: {snapshot.SnapshotId} != {manifest.SnapshotId}",
                    manifest);
            }

            var hash = ComputeSnapshotContentHash(snapshot);
            if (!string.Equals(hash, manifest.Sha256Hash, StringComparison.OrdinalIgnoreCase))
            {
                return SnapshotValidationResult.Invalid(
                    $"Manifest hash mismatch for snapshot {manifest.SnapshotId}.",
                    manifest);
            }

            if (!string.Equals(hash, snapshot.Sha256Hash, StringComparison.OrdinalIgnoreCase))
            {
                return SnapshotValidationResult.Invalid(
                    $"Snapshot hash mismatch for snapshot {manifest.SnapshotId}.",
                    manifest);
            }

            return SnapshotValidationResult.Valid(snapshot, manifest);
        }
        catch (Exception ex)
        {
            return SnapshotValidationResult.Invalid($"Snapshot validation failed: {ex.Message}");
        }
    }

    public string ComputeSnapshotContentHash(RuntimeSnapshot snapshot)
    {
        var unsignedSnapshot = snapshot with { Sha256Hash = null };
        var json = JsonSerializer.Serialize(unsignedSnapshot, JsonDefaults.WriteOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));

        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
