namespace Hermes.Runtime;

public sealed record SnapshotManifest(
    string ManifestVersion,
    string SnapshotId,
    DateTimeOffset CreatedAtUtc,
    string RuntimeVersion,
    string RuntimeMode,
    string SnapshotPath,
    long SnapshotBytes,
    string Sha256Hash);
