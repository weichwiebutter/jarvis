namespace Hermes.Runtime;

public sealed record RetentionPolicy(
    int KeepCheckpointDays,
    int KeepLatestSimulationReports,
    bool AllowTempCleanup,
    bool AllowCacheCleanup)
{
    public static RetentionPolicy Default =>
        new(
            KeepCheckpointDays: 14,
            KeepLatestSimulationReports: 1024,
            AllowTempCleanup: true,
            AllowCacheCleanup: true);
}
