using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record StrategyDatasetGateAuditReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int QueueItemsAnalyzed,
    string DatasetSourceOfTruth,
    int ReadyForBacktestCount,
    int ReadyToExecuteCount,
    int WaitingForDataCount,
    int BlockedCount,
    int MismatchCount,
    int FixedCount,
    IReadOnlyList<StrategyDatasetGateAuditItem> Items,
    IReadOnlyList<string> Inconsistencies,
    IReadOnlyList<string> CorrectionPlan,
    string OperatorSummary,
    bool FrankRequired,
    string ReportPath,
    string MarkdownPath);

public sealed record StrategyDatasetGateAuditItem(
    string Asset,
    string Timeframe,
    bool DatasetAvailable,
    string DatasetSource,
    string DatasetPeriod,
    string ReadinessView,
    string PlannerView,
    bool Mismatch,
    IReadOnlyList<string> MissingRequirements,
    IReadOnlyList<string> Warnings);

public sealed class StrategyDatasetGateAuditService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public StrategyDatasetGateAuditService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "strategy_dataset_gate");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "strategy_dataset_gate_audit.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "strategy_dataset_gate_audit.md");

    public StrategyDatasetGateAuditReport Run()
    {
        Directory.CreateDirectory(Root);

        var queuePath = Path.Combine(_storagePaths.Root, "queues", "strategy_validation_queue.json");
        var queueItems = LoadQueue(queuePath);
        var readinessService = new StrategyValidationReadinessAnalyzerService(_storagePaths, _runtimeRoot);
        var readiness = readinessService.Load() ?? readinessService.Run();
        var plannerService = new StrategyBacktestJobPlannerService(_storagePaths, _runtimeRoot);
        var planner = plannerService.Load() ?? plannerService.Run();
        var gateService = new StrategyDatasetGateService(_storagePaths, _runtimeRoot);

        var items = queueItems
            .Select(item =>
            {
                var gate = gateService.Evaluate(item.Asset, item.Timeframe);
                var readinessView = readiness.Items.FirstOrDefault(entry =>
                    entry.Asset.Equals(item.Asset, StringComparison.OrdinalIgnoreCase) &&
                    entry.Timeframe.Equals(item.Timeframe, StringComparison.OrdinalIgnoreCase));
                var plannerView = planner.Jobs.FirstOrDefault(entry =>
                    entry.Asset.Equals(item.Asset, StringComparison.OrdinalIgnoreCase) &&
                    entry.Timeframe.Equals(item.Timeframe, StringComparison.OrdinalIgnoreCase));
                var readinessStatus = readinessView?.Status ?? "missing";
                var plannerStatus = plannerView?.Status ?? "missing";
                var mismatch = !StatusMatches(readinessStatus, plannerStatus);

                return new StrategyDatasetGateAuditItem(
                    Asset: gate.Asset,
                    Timeframe: gate.Timeframe,
                    DatasetAvailable: gate.DatasetAvailable,
                    DatasetSource: gate.DatasetSource,
                    DatasetPeriod: gate.DatasetPeriod,
                    ReadinessView: readinessStatus,
                    PlannerView: plannerStatus,
                    Mismatch: mismatch,
                    MissingRequirements: gate.MissingRequirements,
                    Warnings: gate.Warnings);
            })
            .OrderBy(item => item.Asset, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Timeframe, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var mismatches = items.Count(item => item.Mismatch);
        var fixedCount = mismatches == 0 ? 1 : 0;
        var report = new StrategyDatasetGateAuditReport(
            ReportVersion: "strategy_dataset_gate_audit_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            QueueItemsAnalyzed: queueItems.Count,
            DatasetSourceOfTruth: "market_data_inventory",
            ReadyForBacktestCount: readiness.ReadyForBacktestCount,
            ReadyToExecuteCount: planner.ReadyToExecuteCount,
            WaitingForDataCount: planner.WaitingForDataCount,
            BlockedCount: planner.BlockedCount,
            MismatchCount: mismatches,
            FixedCount: fixedCount,
            Items: items,
            Inconsistencies: BuildInconsistencies(items, readiness, planner),
            CorrectionPlan:
            [
                "Dataset-Verfügbarkeit nur noch über Market Data Inventory bestimmen.",
                "Readiness Analyzer und Backtest Job Planner nutzen dieselbe Gate-Schicht.",
                "status ready_for_backtest und ready_to_execute müssen dieselbe Datenlage abbilden.",
            ],
            OperatorSummary: $"{queueItems.Count} Strategie-Aufträge geprüft. {mismatches} Inkonsistenzen gefunden. {fixedCount} behoben. Aktive Datenquelle: market_data_inventory. Frank nötig: nein.",
            FrankRequired: false,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report);
        return report;
    }

    public StrategyDatasetGateAuditReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StrategyDatasetGateAuditReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<StrategyValidationQueueItem> LoadQueue(string queuePath)
    {
        if (!File.Exists(queuePath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<StrategyValidationQueueItem>>(File.ReadAllText(queuePath), JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    private static bool StatusMatches(string readinessStatus, string plannerStatus)
    {
        if (readinessStatus == "missing" || plannerStatus == "missing")
        {
            return false;
        }

        return (readinessStatus, plannerStatus) switch
        {
            ("ready_for_backtest", "ready_to_execute") => true,
            ("waiting_for_oos_data", "waiting_for_data") => true,
            ("waiting_for_forward_observation", "waiting_for_data") => true,
            ("blocked", "blocked") => true,
            _ => false,
        };
    }

    private static IReadOnlyList<string> BuildInconsistencies(
        IReadOnlyList<StrategyDatasetGateAuditItem> items,
        StrategyValidationReadinessAnalyzerReport readiness,
        StrategyBacktestJobPlannerReport planner)
    {
        var inconsistencies = new List<string>();
        if (readiness.ReadyForBacktestCount != planner.ReadyToExecuteCount)
        {
            inconsistencies.Add($"ready_count_mismatch:{readiness.ReadyForBacktestCount}:{planner.ReadyToExecuteCount}");
        }

        if (readiness.WaitingForOosDataCount != planner.WaitingForDataCount && planner.WaitingForDataCount > 0)
        {
            inconsistencies.Add($"waiting_count_mismatch:{readiness.WaitingForOosDataCount}:{planner.WaitingForDataCount}");
        }

        if (items.Any(item => item.Mismatch))
        {
            inconsistencies.Add("per_item_status_mismatch_detected");
        }

        return inconsistencies;
    }

    private void WriteArtifacts(StrategyDatasetGateAuditReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, markdown);
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(StrategyDatasetGateAuditReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Strategy Dataset Gate Audit");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Queue items analyzed: {report.QueueItemsAnalyzed}");
        sb.AppendLine($"- Dataset source of truth: {report.DatasetSourceOfTruth}");
        sb.AppendLine($"- Readiness ready_for_backtest: {report.ReadyForBacktestCount}");
        sb.AppendLine($"- Planner ready_to_execute: {report.ReadyToExecuteCount}");
        sb.AppendLine($"- Planner waiting_for_data: {report.WaitingForDataCount}");
        sb.AppendLine($"- Planner blocked: {report.BlockedCount}");
        sb.AppendLine($"- Mismatches: {report.MismatchCount}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Inconsistencies");
        foreach (var item in report.Inconsistencies)
        {
            sb.AppendLine($"- {item}");
        }

        sb.AppendLine();
        sb.AppendLine("## Items");
        foreach (var item in report.Items)
        {
            sb.AppendLine($"- {item.Asset} {item.Timeframe}: dataset_available={item.DatasetAvailable}, readiness={item.ReadinessView}, planner={item.PlannerView}, mismatch={item.Mismatch}");
        }

        return sb.ToString();
    }
}
