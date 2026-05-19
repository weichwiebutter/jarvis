using System.Text.Json;

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
        var exportDirectory = Path.Combine(_storagePaths.Root, "exports", "features");
        Directory.CreateDirectory(exportDirectory);

        var exportPath = Path.Combine(exportDirectory, $"{job.JobId}.features.jsonl");
        var symbol = ReadStringParameter(job, "symbol", "DEMO_SYMBOL");
        var createdAtUtc = DateTimeOffset.UtcNow;

        var rows = new object[]
        {
            new
            {
                timestamp_utc = createdAtUtc.AddMinutes(-2),
                symbol,
                timeframe = "M1",
                source = "stub",
                feature_set = "demo_feature_export_v1",
                row_index = 1,
                price_stub = 100.00,
                momentum_stub = 0.10,
                volatility_stub = 0.20
            },
            new
            {
                timestamp_utc = createdAtUtc.AddMinutes(-1),
                symbol,
                timeframe = "M1",
                source = "stub",
                feature_set = "demo_feature_export_v1",
                row_index = 2,
                price_stub = 100.25,
                momentum_stub = 0.12,
                volatility_stub = 0.18
            },
            new
            {
                timestamp_utc = createdAtUtc,
                symbol,
                timeframe = "M1",
                source = "stub",
                feature_set = "demo_feature_export_v1",
                row_index = 3,
                price_stub = 100.10,
                momentum_stub = 0.08,
                volatility_stub = 0.22
            }
        };

        File.WriteAllLines(
            exportPath,
            rows.Select(row => JsonSerializer.Serialize(row, JsonDefaults.WriteOptions)));

        return new WorkerExecutionResult(
            OutputPath: exportPath,
            Metrics: new Dictionary<string, object?>
            {
                ["rows_written"] = rows.Length,
                ["format"] = "jsonl",
                ["symbol"] = symbol,
                ["stub"] = true
            });
    }

    private static string ReadStringParameter(JobManifest job, string key, string fallback)
    {
        if (!job.Parameters.TryGetValue(key, out var value) || value is null)
        {
            return fallback;
        }

        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? fallback,
            _ => value.ToString() ?? fallback
        };
    }
}
