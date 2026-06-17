using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record StrategyValidationQueueItem(
    string QueueItemId,
    string ValidationPlanId,
    string StrategyPattern,
    string Asset,
    string Timeframe,
    IReadOnlyList<string> ParametersToValidate,
    string Priority,
    string Status,
    bool RequiredBacktest,
    bool RequiredOosTest,
    bool RequiredWalkForward,
    bool RequiredMonteCarlo,
    bool RequiredCostSpreadTest,
    bool RequiredForwardObservation,
    IReadOnlyList<string> SafetyFlags,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string NextAction);

public sealed record StrategyValidationQueueReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string SourcePlannerPath,
    string? QueuePath,
    IReadOnlyList<StrategyValidationQueueItem> QueueItems,
    int PlannedCount,
    int ReadyForBacktestCount,
    int WaitingForOosDataCount,
    int WaitingForForwardObservationCount,
    int BlockedCount,
    int CompletedCount,
    IReadOnlyList<string> StatusDistribution,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string SafetySummary,
    bool FrankRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class StrategyValidationQueueExportService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;
    private string? _resolvedQueuePath;

    public StrategyValidationQueueExportService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "strategy_validation_queue");

    public string QueueDirectory => Path.Combine(_storagePaths.Root, "queues");

    public string QueuePath => _resolvedQueuePath ?? Path.Combine(QueueDirectory, "strategy_validation_queue.json");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "strategy_validation_queue.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "strategy_validation_queue.md");

    public StrategyValidationQueueReport Run()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(QueueDirectory);

        var plannerService = new StrategyMutationValidationPlannerService(_storagePaths, Directory.GetCurrentDirectory());
        var plannerPath = plannerService.ReportPath;
        var planner = plannerService.Load() ?? plannerService.Run();
        var queueItems = planner.ValidationPlans
            .Select((plan, index) => new StrategyValidationQueueItem(
                QueueItemId: $"strategy_validation_queue_{NormalizeId(plan.ValidationPlanId)}_{index + 1:00}",
                ValidationPlanId: plan.ValidationPlanId,
                StrategyPattern: plan.StrategyPattern,
                Asset: plan.Asset,
                Timeframe: plan.Timeframe,
                ParametersToValidate: plan.ParametersToValidate,
                Priority: plan.Priority,
                Status: "planned",
                RequiredBacktest: plan.RequiredBacktest,
                RequiredOosTest: plan.RequiredOosTest,
                RequiredWalkForward: plan.RequiredWalkForward,
                RequiredMonteCarlo: plan.RequiredMonteCarlo,
                RequiredCostSpreadTest: plan.RequiredCostSpreadTest,
                RequiredForwardObservation: plan.RequiredForwardObservation,
                SafetyFlags: plan.SafetyFlags,
                CreatedAtUtc: DateTimeOffset.UtcNow,
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                NextAction: "Planned validation queue entry ready for scheduler or manual validation planning."))
            .ToList();
        var plannedCount = queueItems.Count;

        var report = new StrategyValidationQueueReport(
            ReportVersion: "strategy_validation_queue_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            SourcePlannerPath: plannerPath,
            QueuePath: QueuePath,
            QueueItems: queueItems,
            PlannedCount: plannedCount,
            ReadyForBacktestCount: 0,
            WaitingForOosDataCount: 0,
            WaitingForForwardObservationCount: 0,
            BlockedCount: 0,
            CompletedCount: 0,
            StatusDistribution: new[] { $"planned:{plannedCount}" },
            Warnings: new[] { "validation_queue_exported", "no_backtests_started", "no_broker_action", "no_auto_trading" },
            OperatorSummary: $"{plannedCount} Validierungsaufträge in Queue übernommen. {plannedCount} geplant. 0 laufend. 0 abgeschlossen. Frank nötig: nein. Keine Backtests gestartet.",
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true",
            FrankRequired: false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report);
        return report;
    }

    private void WriteArtifacts(StrategyValidationQueueReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, markdown);
        File.WriteAllText(QueuePath, JsonSerializer.Serialize(report.QueueItems, JsonDefaults.WriteOptions));
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
        _resolvedQueuePath = QueuePath;
    }

    private static string BuildMarkdown(StrategyValidationQueueReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Strategy Validation Queue Export");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Planned: {report.PlannedCount}");
        sb.AppendLine($"- Ready for backtest: {report.ReadyForBacktestCount}");
        sb.AppendLine($"- Waiting for OOS: {report.WaitingForOosDataCount}");
        sb.AppendLine($"- Waiting for forward observation: {report.WaitingForForwardObservationCount}");
        sb.AppendLine($"- Blocked: {report.BlockedCount}");
        sb.AppendLine($"- Completed: {report.CompletedCount}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Queue Items");
        foreach (var item in report.QueueItems)
        {
            sb.AppendLine($"- {item.StrategyPattern} · {item.Asset} {item.Timeframe} · priority={item.Priority} · status={item.Status}");
        }
        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine(report.SafetySummary);
        return sb.ToString();
    }

    private static string NormalizeId(string value)
    {
        var normalized = value.ToLowerInvariant().Replace(" ", "_").Replace("/", "_").Replace("-", "_");
        return string.Concat(normalized.Where(ch => char.IsLetterOrDigit(ch) || ch == '_'));
    }
}
