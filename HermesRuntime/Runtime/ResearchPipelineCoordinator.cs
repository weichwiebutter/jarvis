using System.Text.Json;

namespace Hermes.Runtime;

public sealed class ResearchPipelineCoordinator
{
    private const string ResearchSource = "hermes_research_pipeline";

    private readonly StoragePaths _storagePaths;
    private readonly EventBus _eventBus;
    private readonly string _runtimeVersion;

    public ResearchPipelineCoordinator(
        StoragePaths storagePaths,
        EventBus eventBus,
        string runtimeVersion)
    {
        _storagePaths = storagePaths;
        _eventBus = eventBus;
        _runtimeVersion = runtimeVersion;
    }

    public NightlyResearchReport RunNightlyResearch(NightlyResearchJob job)
    {
        PublishNightlyResearchStarted(job);
        var warnings = new List<string>();

        try
        {
            var featureGenerationService = new FeatureGenerationService(_storagePaths, _eventBus, _runtimeVersion);
            var featureResult = featureGenerationService.GenerateFromMarketData();

            var signalGenerationStub = new SignalGenerationStub(_storagePaths);
            var signalResult = signalGenerationStub.GenerateSignalsFromFeatures(
                featureResult.OutputPath,
                job.JobId);
            warnings.AddRange(signalResult.Warnings);

            var outcomeTrackerService = new OutcomeTrackerService(_storagePaths, _eventBus, _runtimeVersion);
            var outcomeResult = outcomeTrackerService.EvaluateDemoOutcomes();

            var backtestResult = RunBacktestStub(job);

            var completedAtUtc = DateTimeOffset.UtcNow;
            var report = new NightlyResearchReport(
                JobId: job.JobId,
                Status: "completed",
                StartedAtUtc: job.StartedAtUtc,
                CompletedAtUtc: completedAtUtc,
                DurationSeconds: Math.Round((completedAtUtc - job.StartedAtUtc).TotalSeconds, 3),
                FeatureCount: featureResult.FeatureCount,
                SignalCount: signalResult.SignalCount,
                OutcomeCount: outcomeResult.OutcomeCount,
                BacktestCount: 1,
                FeatureOutputPath: featureResult.OutputPath,
                SignalOutputPath: signalResult.OutputPath,
                OutcomeReportPath: outcomeResult.ReportPath,
                BacktestReportPath: backtestResult.OutputPath,
                Warnings: warnings,
                NoAutoTrading: job.NoAutoTrading,
                HumanReviewRequired: job.HumanReviewRequired);

            var reportPath = WriteReport(report);
            PublishNightlyResearchCompleted(report, reportPath);
            return report;
        }
        catch (Exception ex)
        {
            warnings.Add(ex.Message);
            var completedAtUtc = DateTimeOffset.UtcNow;
            var failedReport = new NightlyResearchReport(
                JobId: job.JobId,
                Status: "failed",
                StartedAtUtc: job.StartedAtUtc,
                CompletedAtUtc: completedAtUtc,
                DurationSeconds: Math.Round((completedAtUtc - job.StartedAtUtc).TotalSeconds, 3),
                FeatureCount: 0,
                SignalCount: 0,
                OutcomeCount: 0,
                BacktestCount: 0,
                FeatureOutputPath: null,
                SignalOutputPath: null,
                OutcomeReportPath: null,
                BacktestReportPath: null,
                Warnings: warnings,
                NoAutoTrading: job.NoAutoTrading,
                HumanReviewRequired: job.HumanReviewRequired);

            var reportPath = WriteReport(failedReport);
            PublishNightlyResearchFailed(failedReport, reportPath, ex);
            return failedReport;
        }
    }

    private WorkerExecutionResult RunBacktestStub(NightlyResearchJob job)
    {
        var worker = new BacktestWorker(_storagePaths);
        var manifest = new JobManifest(
            JobId: $"job_{job.JobId}_backtest_stub",
            JobType: BacktestWorker.BacktestJobType,
            Priority: 5,
            Status: JobStatus.Pending,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            RequestedBy: "nightly_research_pipeline",
            ResourceProfile: "local_research_stub",
            MaxRuntimeMinutes: 5,
            MaxRetries: 0,
            RetryCount: 0,
            Parameters: new Dictionary<string, object?>
            {
                ["demo"] = true,
                ["symbol"] = "XAUUSD",
                ["timeframe"] = "M5",
                ["period"] = "NightlyResearchDemo",
                ["strategy_name"] = "NightlyResearchBacktestStub",
                ["note"] = "Nightly research backtest stub. No broker, order, replay engine, or optimization was executed."
            });

        return worker.Execute(manifest);
    }

    private string WriteReport(NightlyResearchReport report)
    {
        var reportDirectory = Path.Combine(_storagePaths.Root, "reports", "nightly");
        Directory.CreateDirectory(reportDirectory);
        var reportPath = Path.Combine(reportDirectory, $"{report.JobId}.nightly.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));

        var latestPath = Path.Combine(reportDirectory, "latest_nightly_research.json");
        File.WriteAllText(latestPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return reportPath;
    }

    private void PublishNightlyResearchStarted(NightlyResearchJob job)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.NightlyResearchStarted,
            ResearchSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Nightly research pipeline started. Local research only, no trading execution.",
                job.JobId,
                job.ScheduledForUtc,
                job.StartedAtUtc,
                job.RequestedBy,
                job.Mode,
                noAutoTrading = job.NoAutoTrading,
                humanReviewRequired = job.HumanReviewRequired
            }));
    }

    private void PublishNightlyResearchCompleted(NightlyResearchReport report, string reportPath)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.NightlyResearchCompleted,
            ResearchSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Nightly research pipeline completed. Results are local learning candidates only.",
                report.JobId,
                report.Status,
                report.FeatureCount,
                report.SignalCount,
                report.OutcomeCount,
                report.BacktestCount,
                report.DurationSeconds,
                report.Warnings,
                reportPath,
                noAutoTrading = report.NoAutoTrading,
                humanReviewRequired = report.HumanReviewRequired
            }));
    }

    private void PublishNightlyResearchFailed(
        NightlyResearchReport report,
        string reportPath,
        Exception exception)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.NightlyResearchFailed,
            ResearchSource,
            EventSeverity.Warning,
            _runtimeVersion,
            new
            {
                message = "Nightly research pipeline failed. No trading execution was possible.",
                report.JobId,
                report.Status,
                exceptionType = exception.GetType().Name,
                error = exception.Message,
                report.Warnings,
                reportPath,
                noAutoTrading = report.NoAutoTrading,
                humanReviewRequired = report.HumanReviewRequired
            }));
    }
}
