namespace Hermes.Runtime;

public sealed class RuntimeState
{
    public string RuntimeName { get; init; } = "Hermes Minimal Runtime";

    public string Environment { get; init; } = "local";

    public string StorageProfile { get; set; } = "unknown";

    public string StorageRoot { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? StoppedAtUtc { get; set; }

    public bool IsRunning { get; set; }

    public bool SafeMode { get; set; }

    public string? SafeModeReason { get; set; }

    public string? DiskSpaceWarning { get; set; }

    public string? LastSnapshotPath { get; set; }
}
