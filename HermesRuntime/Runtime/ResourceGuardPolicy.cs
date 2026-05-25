namespace Hermes.Runtime;

public sealed record ResourceGuardPolicy(
    double CpuPauseThresholdPercent,
    int CpuSustainedMinutes,
    double MemoryPauseThresholdPercent,
    double DiskCleanupFreePercent,
    double DiskStopFreePercent,
    int MaxProcessRuntimeMinutes)
{
    public static ResourceGuardPolicy Default =>
        new(
            CpuPauseThresholdPercent: 85,
            CpuSustainedMinutes: 5,
            MemoryPauseThresholdPercent: 85,
            DiskCleanupFreePercent: 15,
            DiskStopFreePercent: 8,
            MaxProcessRuntimeMinutes: 360);
}
