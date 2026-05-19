namespace Hermes.Runtime;

public sealed record QueueStatus(
    int Pending,
    int Running,
    int Completed,
    int Failed,
    int Quarantined)
{
    public int Total => Pending + Running + Completed + Failed + Quarantined;
}
