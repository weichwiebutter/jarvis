namespace Hermes.Runtime;

public sealed class FeatureExportWorker : IWorker
{
    public const string FeatureExportJobType = "feature_export.demo";

    private readonly StoragePaths _storagePaths;

    public FeatureExportWorker(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string WorkerName => "feature_export_worker_stub";

    public string JobType => FeatureExportJobType;

    public WorkerExecutionResult Execute(JobManifest job)
    {
        var service = new FeatureExportService(_storagePaths);
        var result = service.CreateDemoFeatureExport(job.JobId);

        return new WorkerExecutionResult(
            OutputPath: result.FeatureOutputPath,
            Metrics: new Dictionary<string, object?>
            {
                ["feature_rows_written"] = result.FeatureRowsWritten,
                ["signal_rows_written"] = result.SignalRowsWritten,
                ["format"] = "jsonl",
                ["symbols"] = result.Symbols,
                ["feature_output_path"] = result.FeatureOutputPath,
                ["signal_output_path"] = result.SignalOutputPath,
                ["stub"] = true
            });
    }
}
