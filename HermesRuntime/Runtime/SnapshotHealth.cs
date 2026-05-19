namespace Hermes.Runtime;

public sealed record SnapshotHealth(
    string Status,
    bool SafeMode,
    string? SafeModeReason,
    string? DiskSpaceWarning,
    long FreeDiskMb,
    long MinimumFreeDiskMb);
