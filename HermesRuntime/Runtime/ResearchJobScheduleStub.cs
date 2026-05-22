namespace Hermes.Runtime;

public sealed class ResearchJobScheduleStub
{
    public NightlyResearchJob CreateDemoNightlyRun(string requestedBy)
    {
        return CreateResearchRun(requestedBy, "nightly_research", "demo_nightly_run");
    }

    public NightlyResearchJob CreateBetaLearningRun(string requestedBy)
    {
        return CreateResearchRun(requestedBy, "beta_learning", "trading_learning_beta_1");
    }

    private static NightlyResearchJob CreateResearchRun(string requestedBy, string idPrefix, string mode)
    {
        var startedAtUtc = DateTimeOffset.UtcNow;
        return new NightlyResearchJob(
            JobId: $"{idPrefix}_{startedAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            ScheduledForUtc: startedAtUtc.Date.AddDays(1).AddHours(2),
            StartedAtUtc: startedAtUtc,
            RequestedBy: requestedBy,
            Mode: mode,
            Symbols: ["XAUUSD", "EURUSD", "GER40"],
            Timeframes: ["M5", "M15", "H1", "H4"],
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }
}
