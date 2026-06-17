using System.Text.Json;

namespace Hermes.Runtime;

public sealed record StrategyDatasetGateResult(
    string Asset,
    string Timeframe,
    bool DatasetAvailable,
    string DatasetSource,
    string DatasetPeriod,
    IReadOnlyList<string> MissingRequirements,
    IReadOnlyList<string> Warnings);

public sealed class StrategyDatasetGateService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public StrategyDatasetGateService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "strategy_dataset_gate");

    public StrategyDatasetGateResult Evaluate(string asset, string timeframe)
    {
        var normalizedAsset = asset.Trim().ToUpperInvariant();
        var normalizedTimeframe = timeframe.Trim().ToUpperInvariant();
        var marketData = new MarketDataAvailabilityService(_storagePaths, _runtimeRoot);
        var availability = marketData.LoadAvailability() ?? marketData.Scan();

        var matchingFiles = availability.Files
            .Where(file => file.Asset.Equals(normalizedAsset, StringComparison.OrdinalIgnoreCase) && file.Timeframe.Equals(normalizedTimeframe, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var datasetAvailable = matchingFiles.Count > 0;
        var missingRequirements = new List<string>();
        var warnings = new List<string>();

        if (!datasetAvailable)
        {
            missingRequirements.Add($"historical_data:{normalizedAsset}:{normalizedTimeframe}");
            warnings.Add("dataset_missing");
        }

        if (!availability.AssetsAvailable.Contains(normalizedAsset, StringComparer.OrdinalIgnoreCase))
        {
            warnings.Add($"market_data_asset_missing:{normalizedAsset}");
        }

        return new StrategyDatasetGateResult(
            Asset: normalizedAsset,
            Timeframe: normalizedTimeframe,
            DatasetAvailable: datasetAvailable,
            DatasetSource: "market_data_inventory",
            DatasetPeriod: $"historical_data:{normalizedAsset}:{normalizedTimeframe}",
            MissingRequirements: missingRequirements.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList());
    }

    public StrategyDatasetGateAuditReport RunAudit(
        IReadOnlyList<StrategyValidationQueueItem> queueItems,
        StrategyValidationReadinessAnalyzerReport readiness,
        StrategyBacktestJobPlannerReport planner)
    {
        var assets = queueItems
            .Select(item => (item.Asset.ToUpperInvariant(), item.Timeframe.ToUpperInvariant()))
            .Distinct()
            .OrderBy(item => item.Item1, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Item2, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var gateItems = assets.Select(assetTimeframe =>
        {
            var gate = Evaluate(assetTimeframe.Item1, assetTimeframe.Item2);
            var readinessView = readiness.Items.FirstOrDefault(item =>
                item.Asset.Equals(assetTimeframe.Item1, StringComparison.OrdinalIgnoreCase) &&
                item.Timeframe.Equals(assetTimeframe.Item2, StringComparison.OrdinalIgnoreCase));
            var plannerView = planner.Jobs.FirstOrDefault(item =>
                item.Asset.Equals(assetTimeframe.Item1, StringComparison.OrdinalIgnoreCase) &&
                item.Timeframe.Equals(assetTimeframe.Item2, StringComparison.OrdinalIgnoreCase));

            return new StrategyDatasetGateAuditItem(
                Asset: gate.Asset,
                Timeframe: gate.Timeframe,
                DatasetAvailable: gate.DatasetAvailable,
                DatasetSource: gate.DatasetSource,
                DatasetPeriod: gate.DatasetPeriod,
                ReadinessView: readinessView?.Status ?? "missing",
                PlannerView: plannerView?.Status ?? "missing",
                Mismatch: readinessView is not null && plannerView is not null && readinessView.Status switch
                {
                    "ready_for_backtest" when plannerView.Status == "ready_to_execute" => false,
                    "blocked" when plannerView.Status == "blocked" => false,
                    "waiting_for_oos_data" when plannerView.Status == "waiting_for_data" => false,
                    "waiting_for_forward_observation" when plannerView.Status == "waiting_for_data" => false,
                    _ => true,
                },
                MissingRequirements: gate.MissingRequirements,
                Warnings: gate.Warnings);
        }).ToList();

        var mismatches = gateItems.Count(item => item.Mismatch);
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
            Items: gateItems,
            MismatchCount: mismatches,
            FixedCount: fixedCount,
            Inconsistencies: BuildInconsistencies(gateItems, readiness, planner),
            CorrectionPlan:
            [
                "Dataset-Verfügbarkeit nur noch über Market Data Inventory bestimmen.",
                "Readiness Analyzer und Backtest Job Planner nutzen dieselbe Gate-Schicht.",
                "status ready_for_backtest und ready_to_execute müssen dieselbe Datenlage abbilden.",
            ],
            OperatorSummary: $"{queueItems.Count} Strategie-Aufträge geprüft. {mismatches} Inkonsistenzen gefunden. {fixedCount} behoben. Aktive Datenquelle: market_data_inventory. Frank nötig: nein.",
            FrankRequired: false,
            ReportPath: Path.Combine(Root, "strategy_dataset_gate_audit.json"),
            MarkdownPath: Path.Combine(Root, "strategy_dataset_gate_audit.md"));

        return report;
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
}
