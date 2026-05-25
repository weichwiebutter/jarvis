namespace Hermes.Runtime;

public sealed record ResourceSnapshot(
    DateTimeOffset TimestampUtc,
    double CpuUsagePercent,
    double MemoryUsagePercent,
    long TotalMemoryMb,
    long UsedMemoryMb,
    long FreeDiskMb,
    double FreeDiskPercent,
    string StorageRoot,
    string Action,
    IReadOnlyList<string> Warnings,
    bool ShouldPause,
    bool ShouldStop,
    bool NoAutoTrading,
    bool HumanReviewRequired);
