namespace Hermes.Runtime;

public sealed class ResearchJobScheduleStub
{
    public NightlyResearchJob CreateDemoNightlyRun(string requestedBy)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        return new NightlyResearchJob(
            JobId: $"nightly_research_{startedAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            ScheduledForUtc: startedAtUtc.Date.AddDays(1).AddHours(2),
            StartedAtUtc: startedAtUtc,
            RequestedBy: requestedBy,
            Mode: "demo_nightly_run",
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }
}
