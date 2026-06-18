using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record AutonomousOosExecutionGateResult(
    string OosExecutionId,
    string OosJobId,
    string HypothesisId,
    string Asset,
    string Timeframe,
    string StrategyPattern,
    string OosPeriod,
    string RequiredDataset,
    int MaxRuns,
    string GateStatus,
    string ExecutionStatus,
    string Outcome,
    string NextPlannedStep,
    bool FrankRequired,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings,
    StrategyBacktestResult? Execution,
    MutationValidationComparison? Comparison);

public sealed record AutonomousOosExecutionGateReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string WindowStatus,
    string GateStatus,
    int PlansSeen,
    int PlansReady,
    int PlansWaiting,
    int PlansBlocked,
    AutonomousOosPlan? SelectedPlan,
    AutonomousOosExecutionGateResult? Result,
    IReadOnlyList<string> SourceReports,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string NextSafeStep,
    string SafetySummary,
    bool FrankRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class AutonomousOosExecutionGateService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public AutonomousOosExecutionGateService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "autonomous_oos_execution_gate");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "autonomous_oos_execution_gate.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "autonomous_oos_execution_gate.md");

    public AutonomousOosExecutionGateReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AutonomousOosExecutionGateReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public AutonomousOosExecutionGateReport Run()
    {
        Directory.CreateDirectory(Root);

        var planner = new AutonomousOosPlanningService(_storagePaths).Load()
            ?? new AutonomousOosPlanningService(_storagePaths).Run();
        var timeControl = new HermesInternalScheduler(_storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json")).GetTimeControlStatus();
        var windowStatus = BuildWindowStatus(timeControl);
        var readyPlans = planner.Plans
            .Where(plan => plan.ReadinessStatus.Equals("ready_to_execute", StringComparison.OrdinalIgnoreCase))
            .OrderBy(plan => plan.HypothesisId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var selectedPlan = readyPlans.FirstOrDefault();
        var warnings = new List<string>();
        AutonomousOosExecutionGateResult? result = null;
        var gateStatus = "waiting";

        if (selectedPlan is null)
        {
            warnings.Add("no_ready_oos_plan_found");
            gateStatus = "waiting";
        }
        else if (!timeControl.InWorkWindow && !timeControl.LearningWindow.ActiveNow && !timeControl.NightlyWindow.ActiveNow)
        {
            warnings.Add("outside_allowed_window");
            gateStatus = "waiting";
        }
        else
        {
            result = ExecuteSelectedPlan(selectedPlan, warnings);
            gateStatus = result.GateStatus;
        }

        var report = new AutonomousOosExecutionGateReport(
            ReportVersion: "autonomous_oos_execution_gate_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            WindowStatus: windowStatus,
            GateStatus: gateStatus,
            PlansSeen: planner.Plans.Count,
            PlansReady: readyPlans.Count,
            PlansWaiting: planner.Plans.Count(plan => plan.ReadinessStatus.Equals("waiting_for_data", StringComparison.OrdinalIgnoreCase) || plan.ReadinessStatus.Equals("waiting_for_specification", StringComparison.OrdinalIgnoreCase)),
            PlansBlocked: planner.Plans.Count(plan => plan.ReadinessStatus.Equals("blocked", StringComparison.OrdinalIgnoreCase)),
            SelectedPlan: selectedPlan,
            Result: result,
            SourceReports: BuildSourceReports(),
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            OperatorSummary: BuildOperatorSummary(result, selectedPlan, windowStatus),
            NextSafeStep: result?.NextPlannedStep ?? "Warten auf aktives Zeitfenster oder weiteren ready_to_execute OOS-Plan.",
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true, no_broker_api=true, no_demo_orders=true",
            FrankRequired: result?.FrankRequired ?? false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report);
        return report;
    }

    private AutonomousOosExecutionGateResult ExecuteSelectedPlan(AutonomousOosPlan plan, List<string> warnings)
    {
        var maxRuns = Math.Min(50, Math.Max(1, plan.MaxRuns));
        if (!plan.SafetyFlags.Contains("no_oos_execution=true", StringComparer.OrdinalIgnoreCase))
        {
            warnings.Add("oos_execution_flag_missing");
            return new AutonomousOosExecutionGateResult(
                OosExecutionId: $"oos_execution_{NormalizeId(plan.OosJobId)}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
                OosJobId: plan.OosJobId,
                HypothesisId: plan.HypothesisId,
                Asset: plan.Asset,
                Timeframe: plan.Timeframe,
                StrategyPattern: plan.StrategyPattern,
                OosPeriod: plan.OosPeriod,
                RequiredDataset: plan.RequiredDataset,
                MaxRuns: maxRuns,
                GateStatus: "blocked",
                ExecutionStatus: "blocked",
                Outcome: "inconclusive",
                NextPlannedStep: "OOS-Ausführung blockiert; Safety-Flags korrigieren.",
                FrankRequired: false,
                Blockers: ["safety_flag_missing"],
                Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Execution: null,
                Comparison: null);
        }

        var latestSuccess = StrategyBacktestResultArchiveService.LoadLatestSuccess(_storagePaths);
        if (latestSuccess is null)
        {
            warnings.Add("no_successful_backtest_found");
            return new AutonomousOosExecutionGateResult(
                OosExecutionId: $"oos_execution_{NormalizeId(plan.OosJobId)}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
                OosJobId: plan.OosJobId,
                HypothesisId: plan.HypothesisId,
                Asset: plan.Asset,
                Timeframe: plan.Timeframe,
                StrategyPattern: plan.StrategyPattern,
                OosPeriod: plan.OosPeriod,
                RequiredDataset: plan.RequiredDataset,
                MaxRuns: maxRuns,
                GateStatus: "waiting",
                ExecutionStatus: "waiting_for_data",
                Outcome: "inconclusive",
                NextPlannedStep: "Warten auf OOS-/Baseline-Daten.",
                FrankRequired: false,
                Blockers: ["baseline_missing"],
                Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Execution: null,
                Comparison: null);
        }

        var engine = new MinimalHistoricalBacktestEngine(_storagePaths);
        var datasetAvailable = TryDatasetAvailable(plan.Asset, plan.Timeframe, out var datasetWarnings);
        var safety = new StrategyBacktestSafetyContext(
            NoAutoTrading: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false,
            HumanReviewRequired: true,
            ResearchOnly: true,
            SafetyMode: "no_auto_trading=true; human_review_required=true; broker_orders_enabled=false; live_trading_enabled=false; research_only=true; no_oos_execution=true",
            SafetyFlags: plan.SafetyFlags);
        var request = new StrategyBacktestRequest(
            BacktestJobId: plan.OosJobId,
            StrategyPattern: plan.StrategyPattern,
            Asset: plan.Asset,
            Timeframe: plan.Timeframe,
            ParametersToTest: latestSuccess.Job.ParametersToTest.Count > 0
                ? latestSuccess.Job.ParametersToTest
                : [plan.CausalFactor, "OOS validation"],
            DatasetPath: $"historical_data:{plan.Asset}:{plan.Timeframe}",
            DatasetId: $"historical_data:{plan.Asset}:{plan.Timeframe}",
            BacktestPeriod: latestSuccess.Job.OosPeriod,
            OosPeriod: plan.OosPeriod,
            CostSpreadModel: "required",
            MaxRuns: maxRuns,
            TimeoutSeconds: 1800,
            SafetyMode: safety.SafetyMode);

        if (!datasetAvailable)
        {
            warnings.AddRange(datasetWarnings);
            return new AutonomousOosExecutionGateResult(
                OosExecutionId: $"oos_execution_{NormalizeId(plan.OosJobId)}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
                OosJobId: plan.OosJobId,
                HypothesisId: plan.HypothesisId,
                Asset: plan.Asset,
                Timeframe: plan.Timeframe,
                StrategyPattern: plan.StrategyPattern,
                OosPeriod: plan.OosPeriod,
                RequiredDataset: plan.RequiredDataset,
                MaxRuns: maxRuns,
                GateStatus: "waiting",
                ExecutionStatus: "waiting_for_data",
                Outcome: "inconclusive",
                NextPlannedStep: "OOS-Daten vervollständigen oder ableiten.",
                FrankRequired: false,
                Blockers: ["oos_dataset_missing"],
                Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Execution: null,
                Comparison: null);
        }

        var dataset = new StrategyBacktestDatasetDescriptor(
            DatasetPath: request.DatasetPath,
            DatasetId: request.DatasetId,
            Asset: plan.Asset,
            Timeframe: plan.Timeframe,
            Period: plan.OosPeriod,
            Available: true,
            Warnings: datasetWarnings);
        if (!engine.CanExecute(request, dataset, safety))
        {
            warnings.Add("engine_gate_failed");
            return new AutonomousOosExecutionGateResult(
                OosExecutionId: $"oos_execution_{NormalizeId(plan.OosJobId)}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
                OosJobId: plan.OosJobId,
                HypothesisId: plan.HypothesisId,
                Asset: plan.Asset,
                Timeframe: plan.Timeframe,
                StrategyPattern: plan.StrategyPattern,
                OosPeriod: plan.OosPeriod,
                RequiredDataset: plan.RequiredDataset,
                MaxRuns: maxRuns,
                GateStatus: "blocked",
                ExecutionStatus: "blocked",
                Outcome: "inconclusive",
                NextPlannedStep: "OOS-Execution-Gates korrigieren.",
                FrankRequired: false,
                Blockers: ["engine_gate_failed"],
                Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Execution: null,
                Comparison: null);
        }

        var execution = engine.Execute(request, dataset, safety) with
        {
            Status = "completed"
        };
        var comparison = BuildComparison(latestSuccess, execution);
        var outcome = comparison.Outcome;
        var nextStep = outcome switch
        {
            "improved" => "OOS-Plan abgeschlossen; nächste Research-/Absicherungsableitung vorbereiten.",
            "worse" => "OOS-Ergebnis verschlechtert; Hypothese zurückstufen und nächste sichere Hypothese planen.",
            _ => "OOS-Ergebnis unklar; zusätzliche Evidenz oder Spezifikation vorbereiten.",
        };

        return new AutonomousOosExecutionGateResult(
            OosExecutionId: $"oos_execution_{NormalizeId(plan.OosJobId)}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            OosJobId: plan.OosJobId,
            HypothesisId: plan.HypothesisId,
            Asset: plan.Asset,
            Timeframe: plan.Timeframe,
            StrategyPattern: plan.StrategyPattern,
            OosPeriod: plan.OosPeriod,
            RequiredDataset: plan.RequiredDataset,
            MaxRuns: maxRuns,
            GateStatus: "executed",
            ExecutionStatus: execution.Status,
            Outcome: outcome,
            NextPlannedStep: nextStep,
            FrankRequired: false,
            Blockers: [],
            Warnings: warnings.Concat(datasetWarnings).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Execution: execution,
            Comparison: comparison);
    }

    private static MutationValidationComparison BuildComparison(StrategyBacktestExecutorResultArtifact latestSuccess, StrategyBacktestResult execution)
    {
        var baselineWinRate = latestSuccess.Execution.WinRate ?? 0;
        var baselineProfitFactor = latestSuccess.Execution.ProfitFactor ?? 0;
        var baselineMaxDrawdown = latestSuccess.Execution.MaxDrawdown ?? 0;
        var baselineExpectancy = latestSuccess.Execution.Expectancy ?? 0;
        var mutationWinRate = execution.WinRate ?? 0;
        var mutationProfitFactor = execution.ProfitFactor ?? 0;
        var mutationMaxDrawdown = execution.MaxDrawdown ?? 0;
        var mutationExpectancy = execution.Expectancy ?? 0;

        var winRateDelta = Math.Round(mutationWinRate - baselineWinRate, 4);
        var profitFactorDelta = Math.Round(mutationProfitFactor - baselineProfitFactor, 4);
        var maxDrawdownDelta = Math.Round(mutationMaxDrawdown - baselineMaxDrawdown, 4);
        var expectancyDelta = Math.Round(mutationExpectancy - baselineExpectancy, 4);
        var outcome = DetermineOutcome(latestSuccess, execution);

        return new MutationValidationComparison(
            BaselineBacktestJobId: latestSuccess.Job.BacktestJobId,
            BaselineTradesSimulated: latestSuccess.Execution.TradesSimulated ?? 0,
            BaselineWinRate: baselineWinRate,
            BaselineProfitFactor: baselineProfitFactor,
            BaselineMaxDrawdown: baselineMaxDrawdown,
            BaselineExpectancy: baselineExpectancy,
            BaselineQualityClass: latestSuccess.Execution.Status,
            BaselineCertificationReady: true,
            MutationTradesSimulated: execution.TradesSimulated ?? 0,
            MutationWinRate: mutationWinRate,
            MutationProfitFactor: mutationProfitFactor,
            MutationMaxDrawdown: mutationMaxDrawdown,
            MutationExpectancy: mutationExpectancy,
            MutationQualityClass: execution.Status,
            MutationCertificationReady: false,
            WinRateDelta: winRateDelta,
            ProfitFactorDelta: profitFactorDelta,
            MaxDrawdownDelta: maxDrawdownDelta,
            ExpectancyDelta: expectancyDelta,
            Outcome: outcome);
    }

    private static string DetermineOutcome(StrategyBacktestExecutorResultArtifact latestSuccess, StrategyBacktestResult execution)
    {
        if (execution.Status.Equals("completed_no_trades", StringComparison.OrdinalIgnoreCase))
        {
            return "inconclusive";
        }

        var trades = execution.TradesSimulated ?? 0;
        if (trades == 0)
        {
            return "inconclusive";
        }

        var baselineWinRate = latestSuccess.Execution.WinRate ?? 0;
        var baselineProfitFactor = latestSuccess.Execution.ProfitFactor ?? 0;
        var baselineMaxDrawdown = latestSuccess.Execution.MaxDrawdown ?? 0;
        var baselineExpectancy = latestSuccess.Execution.Expectancy ?? 0;

        if ((execution.ProfitFactor ?? 0) > baselineProfitFactor
            && (execution.Expectancy ?? 0) > baselineExpectancy
            && (execution.MaxDrawdown ?? 0) >= baselineMaxDrawdown
            && (execution.WinRate ?? 0) >= baselineWinRate)
        {
            return "improved";
        }

        if ((execution.ProfitFactor ?? 0) < baselineProfitFactor
            || (execution.Expectancy ?? 0) < baselineExpectancy
            || (execution.MaxDrawdown ?? 0) < baselineMaxDrawdown
            || (execution.WinRate ?? 0) < baselineWinRate)
        {
            return "worse";
        }

        return "inconclusive";
    }

    private static string BuildWindowStatus(ScheduleTimeControlStatus timeControl)
        => $"work_window={timeControl.InWorkWindow.ToString().ToLowerInvariant()}, learning_window={timeControl.LearningWindow.ActiveNow.ToString().ToLowerInvariant()}, nightly_window={timeControl.NightlyWindow.ActiveNow.ToString().ToLowerInvariant()}";

    private static bool TryDatasetAvailable(string asset, string timeframe, out IReadOnlyList<string> warnings)
    {
        var root = Path.Combine("/mnt/d/HermesData", "market_data", "candles", asset.ToUpperInvariant(), timeframe.ToUpperInvariant());
        if (Directory.Exists(root) && Directory.EnumerateFiles(root, "*.candles.jsonl", SearchOption.TopDirectoryOnly).Any())
        {
            warnings = [];
            return true;
        }

        warnings = ["oos_dataset_missing"];
        return false;
    }

    private static IReadOnlyList<string> BuildSourceReports()
    {
        return
        [
            "/mnt/d/HermesData/reports/autonomous_oos_planning/autonomous_oos_planning.json",
            "/mnt/d/HermesData/reports/attribution_hypothesis_feedback/attribution_hypothesis_feedback.json",
            "/mnt/d/HermesData/reports/mutation_validation_execution/mutation_validation_execution.json",
            "/mnt/d/HermesData/reports/strategy_backtest_execution/strategy_backtest_latest_success.json"
        ];
    }

    private static string BuildOperatorSummary(AutonomousOosExecutionGateResult? result, AutonomousOosPlan? plan, string windowStatus)
    {
        if (plan is null)
        {
            return $"Hermes hat keinen ausführbaren OOS-Plan gefunden. {windowStatus}. Frank nötig: nein.";
        }

        if (result is null)
        {
            return $"Hermes wartet auf aktives Zeitfenster für OOS-Plan {plan.OosJobId}. {windowStatus}. Frank nötig: nein.";
        }

        return $"Hermes hat genau einen OOS-Job ausgeführt. Ergebnis={result.Outcome}. Nächster Schritt: {result.NextPlannedStep}. Frank nötig: nein.";
    }

    private void WriteArtifacts(AutonomousOosExecutionGateReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(AutonomousOosExecutionGateReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Autonomous OOS Execution Gate");
        sb.AppendLine();
        sb.AppendLine(report.OperatorSummary);
        if (report.Result is not null)
        {
            sb.AppendLine();
            sb.AppendLine($"- Outcome: {report.Result.Outcome}");
            sb.AppendLine($"- Next step: {report.Result.NextPlannedStep}");
            sb.AppendLine($"- Frank required: {(report.Result.FrankRequired ? "yes" : "no")}");
        }

        return sb.ToString();
    }

    private static string NormalizeId(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_').ToArray();
        return new string(chars).Trim('_');
    }
}
