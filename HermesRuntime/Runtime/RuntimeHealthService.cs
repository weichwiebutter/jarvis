using System.Text.Json;

namespace Hermes.Runtime;

public sealed class RuntimeHealthService
{
    private readonly string _reportsDirectory;

    public RuntimeHealthService(StoragePaths storagePaths)
    {
        _reportsDirectory = Path.Combine(storagePaths.Root, "reports");
        Directory.CreateDirectory(_reportsDirectory);
    }

    public RuntimeHealthWriteResult WriteHealth(
        RuntimeState state,
        DiskSpaceCheck diskSpaceCheck,
        QueueStatus queueStatus,
        SnapshotWriteResult? snapshotResult,
        string? lastError)
    {
        var health = new RuntimeHealth(
            TimestampUtc: DateTimeOffset.UtcNow,
            RuntimeState: state.IsRunning ? "running" : "stopped",
            SafeMode: state.SafeMode,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            FreeDiskGb: Math.Round(diskSpaceCheck.FreeMb / 1024.0, 2),
            PendingJobs: queueStatus.Pending,
            RunningJobs: queueStatus.Running,
            FailedJobs: queueStatus.Failed,
            QuarantinedJobs: queueStatus.Quarantined,
            LastSnapshotId: snapshotResult?.Snapshot.SnapshotId,
            LastError: lastError);

        var reportPath = Path.Combine(_reportsDirectory, "runtime_health.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(health, JsonDefaults.WriteOptions));

        Console.WriteLine($"RuntimeHealth: {health.RuntimeState}, safeMode={health.SafeMode}, pendingJobs={health.PendingJobs}");

        return new RuntimeHealthWriteResult(health, reportPath);
    }
}
