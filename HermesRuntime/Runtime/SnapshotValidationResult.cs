namespace Hermes.Runtime;

public sealed record SnapshotValidationResult(
    bool IsValid,
    string? Error,
    RuntimeSnapshot? Snapshot,
    SnapshotManifest? Manifest)
{
    public static SnapshotValidationResult Valid(RuntimeSnapshot snapshot, SnapshotManifest manifest) =>
        new(IsValid: true, Error: null, Snapshot: snapshot, Manifest: manifest);

    public static SnapshotValidationResult Invalid(string error, SnapshotManifest? manifest = null) =>
        new(IsValid: false, Error: error, Snapshot: null, Manifest: manifest);
}
