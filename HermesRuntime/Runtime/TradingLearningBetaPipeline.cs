using System.Text.Json;

namespace Hermes.Runtime;

public sealed class TradingLearningBetaPipeline
{
    private readonly StoragePaths _storagePaths;
    private readonly EventBus _eventBus;
    private readonly string _runtimeVersion;

    public TradingLearningBetaPipeline(
        StoragePaths storagePaths,
        EventBus eventBus,
        string runtimeVersion)
    {
        _storagePaths = storagePaths;
        _eventBus = eventBus;
        _runtimeVersion = runtimeVersion;
    }

    public TradingLearningBetaReport Run()
    {
        var schedule = new ResearchJobScheduleStub();
        var job = schedule.CreateBetaLearningRun("hermes_beta_learning_cli");
        var coordinator = new ResearchPipelineCoordinator(_storagePaths, _eventBus, _runtimeVersion);
        var summary = coordinator.RunNightlyResearch(job);

        var report = CreateBetaReport(summary);
        return WriteBetaReport(report);
    }

    private static TradingLearningBetaReport CreateBetaReport(ResearchSummaryReport summary)
    {
        var learningReady = summary.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)
            && summary.CandlesProcessed > 0
            && summary.FeaturesGenerated > 0
            && summary.SignalsGenerated > 0
            && summary.OutcomesGenerated > 0
            && summary.BacktestsGenerated > 0
            && summary.NoAutoTrading;

        return new TradingLearningBetaReport(
            RunId: summary.RunId,
            Status: summary.Status,
            StartedAtUtc: summary.StartedAtUtc,
            CompletedAtUtc: summary.CompletedAtUtc,
            SymbolsProcessed: summary.SymbolsProcessed,
            CandlesProcessed: summary.CandlesProcessed,
            FeaturesGenerated: summary.FeaturesGenerated,
            SignalsGenerated: summary.SignalsGenerated,
            OutcomesGenerated: summary.OutcomesGenerated,
            BacktestsGenerated: summary.BacktestsGenerated,
            Warnings: summary.Warnings,
            DurationSeconds: summary.DurationSeconds,
            LearningReady: learningReady,
            NoAutoTrading: summary.NoAutoTrading,
            HumanReviewRequired: summary.HumanReviewRequired,
            BetaReportPath: null,
            ResearchReportPath: summary.ResearchReportPath,
            FeatureOutputPath: summary.FeatureOutputPath,
            SignalOutputPath: summary.SignalOutputPath,
            OutcomeReportPath: summary.OutcomeReportPath,
            BacktestReportPath: summary.BacktestReportPath);
    }

    private TradingLearningBetaReport WriteBetaReport(TradingLearningBetaReport report)
    {
        var reportDirectory = Path.Combine(_storagePaths.Root, "reports", "beta");
        Directory.CreateDirectory(reportDirectory);

        var reportPath = Path.Combine(reportDirectory, $"{report.RunId}.beta_report.json");
        var latestPath = Path.Combine(reportDirectory, "latest_beta_learning.json");
        var writtenReport = report with { BetaReportPath = reportPath };
        var json = JsonSerializer.Serialize(writtenReport, JsonDefaults.WriteOptions);

        File.WriteAllText(reportPath, json);
        File.WriteAllText(latestPath, json);
        return writtenReport;
    }
}
