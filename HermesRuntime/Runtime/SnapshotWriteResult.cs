namespace Hermes.Runtime;

public sealed record SnapshotWriteResult(
    RuntimeSnapshot Snapshot,
    SnapshotManifest Manifest,
    string SnapshotPath,
    string ManifestPath,
    SnapshotValidationResult Validation);
