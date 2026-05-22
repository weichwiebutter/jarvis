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

    public ResearchSummaryReport RunNightlyResearch(NightlyResearchJob job)
    {
        PublishNightlyResearchStarted(job);
        var warnings = new List<string>();

        try
        {
            var featureGenerationService = new FeatureGenerationService(_storagePaths, _eventBus, _runtimeVersion);
            var featureResult = featureGenerationService.GenerateFromMarketData();

            if (featureResult.CandleCount == 0)
            {
                warnings.Add("No historical candles were found under data/market_data/candles.");
            }

            var signalGenerationStub = new SignalGenerationStub(_storagePaths, _eventBus, _runtimeVersion);
            var signalResult = signalGenerationStub.GenerateSignalsFromFeatures(
                featureResult.OutputPath,
                job.JobId);
            warnings.AddRange(signalResult.Warnings);

            var outcomeTrackerService = new OutcomeTrackerService(_storagePaths, _eventBus, _runtimeVersion);
            var outcomeResult = outcomeTrackerService.EvaluateDemoOutcomes(signalResult.OutputPath);

            PublishBacktestStarted(job);
            var backtestResult = RunBacktestStub(job);
            PublishBacktestCompleted(job, backtestResult);

            var completedAtUtc = DateTimeOffset.UtcNow;
            var report = new ResearchSummaryReport(
                RunId: job.JobId,
                Status: "completed",
                StartedAtUtc: job.StartedAtUtc,
                CompletedAtUtc: completedAtUtc,
                SymbolsProcessed: featureResult.SymbolsProcessed,
                CandlesProcessed: featureResult.CandleCount,
                DurationSeconds: Math.Round((completedAtUtc - job.StartedAtUtc).TotalSeconds, 3),
                FeaturesGenerated: featureResult.FeatureCount,
                SignalsGenerated: signalResult.SignalCount,
                OutcomesGenerated: outcomeResult.OutcomeCount,
                BacktestsGenerated: 1,
                ReportsGenerated: 4,
                Warnings: warnings,
                NoAutoTrading: job.NoAutoTrading,
                HumanReviewRequired: job.HumanReviewRequired,
                FeatureOutputPath: featureResult.OutputPath,
                SignalOutputPath: signalResult.OutputPath,
                OutcomeReportPath: outcomeResult.ReportPath,
                BacktestReportPath: backtestResult.OutputPath,
                NightlyReportPath: null,
                ResearchReportPath: null);

            var writtenReport = WriteReport(report);
            PublishNightlyResearchCompleted(writtenReport);
            return writtenReport;
        }
        catch (Exception ex)
        {
            warnings.Add(ex.Message);
            var completedAtUtc = DateTimeOffset.UtcNow;
            var failedReport = new ResearchSummaryReport(
                RunId: job.JobId,
                Status: "failed",
                StartedAtUtc: job.StartedAtUtc,
                CompletedAtUtc: completedAtUtc,
                SymbolsProcessed: [],
                CandlesProcessed: 0,
                DurationSeconds: Math.Round((completedAtUtc - job.StartedAtUtc).TotalSeconds, 3),
                FeaturesGenerated: 0,
                SignalsGenerated: 0,
                OutcomesGenerated: 0,
                BacktestsGenerated: 0,
                ReportsGenerated: 2,
                Warnings: warnings,
                NoAutoTrading: job.NoAutoTrading,
                HumanReviewRequired: job.HumanReviewRequired,
                FeatureOutputPath: null,
                SignalOutputPath: null,
                OutcomeReportPath: null,
                BacktestReportPath: null,
                NightlyReportPath: null,
                ResearchReportPath: null);

            var writtenFailedReport = WriteReport(failedReport);
            PublishNightlyResearchFailed(writtenFailedReport, ex);
            return writtenFailedReport;
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

    private ResearchSummaryReport WriteReport(ResearchSummaryReport report)
    {
        var nightlyDirectory = Path.Combine(_storagePaths.Root, "reports", "nightly");
        var researchDirectory = Path.Combine(_storagePaths.Root, "reports", "research");
        Directory.CreateDirectory(nightlyDirectory);
        Directory.CreateDirectory(researchDirectory);

        var nightlyPath = Path.Combine(nightlyDirectory, $"{report.RunId}.nightly.json");
        var latestNightlyPath = Path.Combine(nightlyDirectory, "latest_nightly_research.json");
        var researchPath = Path.Combine(researchDirectory, $"{report.RunId}.research_summary.json");
        var latestResearchPath = Path.Combine(researchDirectory, "latest_research_summary.json");

        var writtenReport = report with
        {
            NightlyReportPath = nightlyPath,
            ResearchReportPath = researchPath
        };

        var json = JsonSerializer.Serialize(writtenReport, JsonDefaults.WriteOptions);
        File.WriteAllText(nightlyPath, json);
        File.WriteAllText(latestNightlyPath, json);
        File.WriteAllText(researchPath, json);
        File.WriteAllText(latestResearchPath, json);
        return writtenReport;
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
                job.Symbols,
                job.Timeframes,
                noAutoTrading = job.NoAutoTrading,
                humanReviewRequired = job.HumanReviewRequired
            }));
    }

    private void PublishNightlyResearchCompleted(ResearchSummaryReport report)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.NightlyResearchCompleted,
            ResearchSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Nightly research pipeline completed. Results are local learning candidates only.",
                report.RunId,
                report.Status,
                report.SymbolsProcessed,
                report.CandlesProcessed,
                report.FeaturesGenerated,
                report.SignalsGenerated,
                report.OutcomesGenerated,
                report.BacktestsGenerated,
                report.ReportsGenerated,
                report.DurationSeconds,
                report.Warnings,
                report.NightlyReportPath,
                report.ResearchReportPath,
                noAutoTrading = report.NoAutoTrading,
                humanReviewRequired = report.HumanReviewRequired
            }));
    }

    private void PublishNightlyResearchFailed(ResearchSummaryReport report, Exception exception)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.NightlyResearchFailed,
            ResearchSource,
            EventSeverity.Warning,
            _runtimeVersion,
            new
            {
                message = "Nightly research pipeline failed. No trading execution was possible.",
                report.RunId,
                report.Status,
                exceptionType = exception.GetType().Name,
                error = exception.Message,
                report.Warnings,
                report.NightlyReportPath,
                report.ResearchReportPath,
                noAutoTrading = report.NoAutoTrading,
                humanReviewRequired = report.HumanReviewRequired
            }));
    }

    private void PublishBacktestStarted(NightlyResearchJob job)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.BacktestStarted,
            ResearchSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Backtest stub started inside nightly research. Demo-only, no execution.",
                runId = job.JobId,
                strategy = "NightlyResearchBacktestStub",
                noAutoTrading = job.NoAutoTrading,
                humanReviewRequired = job.HumanReviewRequired
            }));
    }

    private void PublishBacktestCompleted(NightlyResearchJob job, WorkerExecutionResult result)
    {
        _eventBus.Publish(EventEnvelope.Create(
            EventType.BacktestCompleted,
            ResearchSource,
            EventSeverity.Info,
            _runtimeVersion,
            new
            {
                message = "Backtest stub completed inside nightly research. Report is local research data only.",
                runId = job.JobId,
                outputPath = result.OutputPath,
                result.Metrics,
                noAutoTrading = job.NoAutoTrading,
                humanReviewRequired = job.HumanReviewRequired
            }));
    }
}
