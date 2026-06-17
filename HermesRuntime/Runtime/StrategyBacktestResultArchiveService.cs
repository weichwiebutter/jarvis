using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record StrategyBacktestRunHistoryEntry(
    DateTimeOffset AttemptedAtUtc,
    string BacktestJobId,
    string StrategyPattern,
    string Asset,
    string Timeframe,
    bool ExecutionSupported,
    string Status,
    bool Successful,
    string Source,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors);

public static class StrategyBacktestResultArchiveService
{
    public static string Root(StoragePaths storagePaths) => Path.Combine(storagePaths.Root, "reports", "strategy_backtest_execution");
    public static string ResultsRoot(StoragePaths storagePaths) => Path.Combine(Root(storagePaths), "results");
    public static string HistoryRoot(StoragePaths storagePaths) => Path.Combine(Root(storagePaths), "history");
    public static string LastRunReportPath(StoragePaths storagePaths) => Path.Combine(Root(storagePaths), "strategy_backtest_executor_last_run.json");
    public static string LastRunMarkdownPath(StoragePaths storagePaths) => Path.Combine(Root(storagePaths), "strategy_backtest_executor_last_run.md");
    public static string LatestSuccessReportPath(StoragePaths storagePaths) => Path.Combine(Root(storagePaths), "strategy_backtest_latest_success.json");
    public static string LatestSuccessMarkdownPath(StoragePaths storagePaths) => Path.Combine(Root(storagePaths), "strategy_backtest_latest_success.md");
    public static string LegacyReportPath(StoragePaths storagePaths) => Path.Combine(Root(storagePaths), "strategy_backtest_executor.json");
    public static string LegacyMarkdownPath(StoragePaths storagePaths) => Path.Combine(Root(storagePaths), "strategy_backtest_executor.md");
    public static string RunHistoryPath(StoragePaths storagePaths) => Path.Combine(Root(storagePaths), "strategy_backtest_run_history.jsonl");
    public static string HistoryRunHistoryPath(StoragePaths storagePaths) => Path.Combine(HistoryRoot(storagePaths), "strategy_backtest_run_history.jsonl");

    public static void EnsureDirectories(StoragePaths storagePaths)
    {
        Directory.CreateDirectory(Root(storagePaths));
        Directory.CreateDirectory(ResultsRoot(storagePaths));
        Directory.CreateDirectory(HistoryRoot(storagePaths));
    }

    public static void WriteRunHistory(StoragePaths storagePaths, StrategyBacktestRunHistoryEntry entry)
    {
        EnsureDirectories(storagePaths);
        var line = JsonSerializer.Serialize(entry, JsonDefaults.WriteOptions) + Environment.NewLine;
        File.AppendAllText(RunHistoryPath(storagePaths), line);
        File.AppendAllText(HistoryRunHistoryPath(storagePaths), line);
    }

    public static void WriteResult(StoragePaths storagePaths, StrategyBacktestJobPlan job, StrategyBacktestResult execution)
    {
        EnsureDirectories(storagePaths);
        var payload = new StrategyBacktestExecutorResultArtifact(
            ReportRole: "latest_success",
            Job: job,
            Execution: execution,
            GeneratedAtUtc: DateTimeOffset.UtcNow);

        var json = JsonSerializer.Serialize(payload, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(payload);
        var resultPath = Path.Combine(ResultsRoot(storagePaths), $"backtest_result_{job.BacktestJobId}.json");
        File.WriteAllText(resultPath, json);
        File.WriteAllText(LatestSuccessReportPath(storagePaths), json);
        File.WriteAllText(LatestSuccessMarkdownPath(storagePaths), markdown);
    }

    public static StrategyBacktestExecutorResultArtifact? LoadLatestSuccess(StoragePaths storagePaths)
    {
        EnsureDirectories(storagePaths);
        var resultFiles = Directory.Exists(ResultsRoot(storagePaths))
            ? Directory.EnumerateFiles(ResultsRoot(storagePaths), "backtest_result_*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ToList()
            : [];
        foreach (var path in resultFiles)
        {
            var artifact = ReadArtifact(path);
            if (artifact is not null && IsTerminalSuccess(artifact.Execution.Status))
            {
                return artifact;
            }
        }

        var latestSuccessPath = LatestSuccessReportPath(storagePaths);
        if (File.Exists(latestSuccessPath))
        {
            var artifact = ReadArtifact(latestSuccessPath);
            if (artifact is not null && IsTerminalSuccess(artifact.Execution.Status))
            {
                return artifact;
            }
        }

        var legacyPath = LegacyReportPath(storagePaths);
        if (File.Exists(legacyPath))
        {
            var legacy = ReadLegacyExecutor(legacyPath);
            if (legacy?.Execution is not null && IsTerminalSuccess(legacy.Execution.Status))
            {
                return new StrategyBacktestExecutorResultArtifact("latest_success", legacy.SelectedJob!, legacy.Execution, legacy.UpdatedAtUtc);
            }
        }

        return null;
    }

    public static StrategyBacktestExecutorReport? LoadLastRun(StoragePaths storagePaths)
    {
        var lastRunPath = LastRunReportPath(storagePaths);
        if (File.Exists(lastRunPath))
        {
            return ReadExecutorReport(lastRunPath);
        }

        var legacyPath = LegacyReportPath(storagePaths);
        return ReadExecutorReport(legacyPath);
    }

    public static void WriteLastRun(StoragePaths storagePaths, StrategyBacktestExecutorReport report)
    {
        EnsureDirectories(storagePaths);
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        File.WriteAllText(LastRunReportPath(storagePaths), json);
        File.WriteAllText(LastRunMarkdownPath(storagePaths), markdown);
        File.WriteAllText(LegacyReportPath(storagePaths), json);
        File.WriteAllText(LegacyMarkdownPath(storagePaths), markdown);
    }

    private static bool IsTerminalSuccess(string status)
        => status.Equals("completed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("completed_no_trades", StringComparison.OrdinalIgnoreCase);

    private static StrategyBacktestExecutorResultArtifact? ReadArtifact(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<StrategyBacktestExecutorResultArtifact>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static StrategyBacktestExecutorReport? ReadExecutorReport(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StrategyBacktestExecutorReport>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static StrategyBacktestExecutorReport? ReadLegacyExecutor(string path)
    {
        return ReadExecutorReport(path);
    }

    private static string BuildMarkdown(StrategyBacktestExecutorResultArtifact artifact)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Strategy Backtest Latest Success");
        sb.AppendLine();
        sb.AppendLine($"- Generated at: {artifact.GeneratedAtUtc:O}");
        sb.AppendLine($"- Job: {artifact.Job.BacktestJobId}");
        sb.AppendLine($"- Strategy: {artifact.Job.StrategyPattern}");
        sb.AppendLine($"- Asset: {artifact.Job.Asset}");
        sb.AppendLine($"- Timeframe: {artifact.Job.Timeframe}");
        sb.AppendLine($"- Status: {artifact.Execution.Status}");
        sb.AppendLine($"- Trades simulated: {artifact.Execution.TradesSimulated ?? 0}");
        return sb.ToString();
    }

    private static string BuildMarkdown(StrategyBacktestExecutorReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Strategy Backtest Executor Last Run");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Queue items loaded: {report.QueueItemsLoaded}");
        sb.AppendLine($"- Ready jobs found: {report.ReadyJobsFound}");
        sb.AppendLine($"- Jobs attempted: {report.JobsAttempted}");
        sb.AppendLine($"- Jobs executed: {report.JobsExecuted}");
        sb.AppendLine($"- Jobs skipped: {report.JobsSkipped}");
        sb.AppendLine($"- Latest success available: {report.LatestSuccessAvailable}");
        sb.AppendLine($"- Latest success path: {report.LatestSuccessPath}");
        return sb.ToString();
    }
}

public sealed record StrategyBacktestExecutorResultArtifact(
    string ReportRole,
    StrategyBacktestJobPlan Job,
    StrategyBacktestResult Execution,
    DateTimeOffset GeneratedAtUtc);
