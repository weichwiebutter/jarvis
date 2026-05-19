namespace Hermes.Runtime;

public sealed record SnapshotLoadResult(
    RuntimeSnapshot? LastValidSnapshot,
    IReadOnlyList<SnapshotValidationResult> ValidationFailures);
