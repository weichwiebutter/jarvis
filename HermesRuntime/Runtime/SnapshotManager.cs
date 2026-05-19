using System.Text.Json;

namespace Hermes.Runtime;

public sealed class SnapshotManager
{
    private const string ManifestVersion = "hermes.snapshot_manifest.v1";

    private readonly string _runtimeSnapshotDirectory;
    private readonly string _quarantineDirectory;
    private readonly SnapshotValidator _validator = new();

    public SnapshotManager(StoragePaths storagePaths)
    {
        _runtimeSnapshotDirectory = Path.Combine(storagePaths.Snapshots, "runtime");
        _quarantineDirectory = Path.Combine(_runtimeSnapshotDirectory, "quarantine");

        Directory.CreateDirectory(_runtimeSnapshotDirectory);
        Directory.CreateDirectory(_quarantineDirectory);
    }

    public SnapshotLoadResult LoadLastValidSnapshot()
    {
        var failures = new List<SnapshotValidationResult>();
        var manifests = Directory
            .EnumerateFiles(_runtimeSnapshotDirectory, "*.manifest.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToList();

        foreach (var manifestPath in manifests)
        {
            var result = _validator.Validate(manifestPath);
            if (result.IsValid)
            {
                return new SnapshotLoadResult(result.Snapshot, failures);
            }

            failures.Add(result);
            Quarantine(manifestPath, result.Manifest?.SnapshotPath);
        }

        return new SnapshotLoadResult(LastValidSnapshot: null, failures);
    }

    public SnapshotWriteResult WriteRuntimeSnapshot(
        RuntimeState state,
        DiskSpaceCheck diskSpaceCheck,
        string runtimeVersion,
        string runtimeMode,
        string? lastEventId)
    {
        var snapshotId = $"snap_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
        var health = new SnapshotHealth(
            Status: state.SafeMode ? "safe_mode" : "healthy",
            SafeMode: state.SafeMode,
            SafeModeReason: state.SafeModeReason,
            DiskSpaceWarning: state.DiskSpaceWarning,
            FreeDiskMb: diskSpaceCheck.FreeMb,
            MinimumFreeDiskMb: diskSpaceCheck.MinimumFreeMb);

        var unsignedSnapshot = new RuntimeSnapshot(
            SnapshotId: snapshotId,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            RuntimeVersion: runtimeVersion,
            RuntimeMode: runtimeMode,
            State: state,
            Health: health,
            LastEventId: lastEventId,
            Sha256Hash: null);

        var hash = _validator.ComputeSnapshotContentHash(unsignedSnapshot);
        var snapshot = unsignedSnapshot with { Sha256Hash = hash };

        var snapshotPath = Path.Combine(_runtimeSnapshotDirectory, $"{snapshotId}.snapshot.json");
        File.WriteAllText(snapshotPath, JsonSerializer.Serialize(snapshot, JsonDefaults.WriteOptions));

        var manifest = new SnapshotManifest(
            ManifestVersion: ManifestVersion,
            SnapshotId: snapshotId,
            CreatedAtUtc: snapshot.CreatedAtUtc,
            RuntimeVersion: runtimeVersion,
            RuntimeMode: runtimeMode,
            SnapshotPath: snapshotPath,
            SnapshotBytes: new FileInfo(snapshotPath).Length,
            Sha256Hash: hash);

        var manifestPath = Path.Combine(_runtimeSnapshotDirectory, $"{snapshotId}.manifest.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonDefaults.WriteOptions));

        var validation = _validator.Validate(manifestPath);

        if (!validation.IsValid)
        {
            Quarantine(manifestPath, snapshotPath);
        }

        return new SnapshotWriteResult(snapshot, manifest, snapshotPath, manifestPath, validation);
    }

    private void Quarantine(string manifestPath, string? snapshotPath)
    {
        Directory.CreateDirectory(_quarantineDirectory);
        var prefix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");

        MoveIfExists(manifestPath, Path.Combine(_quarantineDirectory, $"{prefix}_{Path.GetFileName(manifestPath)}"));

        if (!string.IsNullOrWhiteSpace(snapshotPath))
        {
            MoveIfExists(snapshotPath, Path.Combine(_quarantineDirectory, $"{prefix}_{Path.GetFileName(snapshotPath)}"));
        }
    }

    private static void MoveIfExists(string source, string destination)
    {
        if (!File.Exists(source))
        {
            return;
        }

        if (File.Exists(destination))
        {
            File.Delete(destination);
        }

        File.Move(source, destination);
    }
}
