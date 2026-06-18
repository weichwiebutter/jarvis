using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record AutonomousResearchLoopStepResult(
    string StepId,
    string StepType,
    string Status,
    string Result,
    string? SelectedJobId,
    string? SelectedJobMutationType,
    string WhySelected,
    string NextPlannedStep,
    MutationValidationExecutorReport? MutationExecution,
    bool FrankRequired,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> ActionsTaken);

public sealed record AutonomousResearchLoopOrchestratorReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    string StatusLabel,
    bool InWorkWindow,
    bool InLearningWindow,
    bool EvidenceAutoLoopEnabled,
    bool ResearchLoopEnabled,
    bool SafetyEligible,
    string SafetyStatus,
    bool FrankRequired,
    string StepType,
    string StepStatus,
    string StepResult,
    string? SelectedJobId,
    string? SelectedJobMutationType,
    string WhySelected,
    string NextPlannedStep,
    MutationValidationExecutorReport? MutationExecution,
    AttributionHypothesisFeedbackReport? AttributionHypothesisFeedback,
    MutationValidationJobPlannerReport? MutationPlanner,
    MutationCandidateQueueReport? MutationQueue,
    StrategyBacktestFailureLearningReport? FailureLearning,
    StrategyBacktestQualityAuditReport? QualityAudit,
    EvidenceAutoLoopState? EvidenceAutoLoopState,
    ScheduleTimeControlStatus? TimeControl,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class AutonomousResearchLoopOrchestratorService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public AutonomousResearchLoopOrchestratorService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "autonomous_research_loop");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "autonomous_research_loop.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "autonomous_research_loop.md");

    public AutonomousResearchLoopOrchestratorReport Run()
    {
        Directory.CreateDirectory(Root);

        var scheduler = new HermesInternalScheduler(_storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var config = scheduler.LoadConfig();
        var timeControl = scheduler.GetTimeControlStatus();
        var evidenceLoop = new EvidenceAutoLoopService(_storagePaths).GetRuntimeState();
        var failureLearning = new StrategyBacktestFailureLearningService(_storagePaths).Load();
        var qualityAudit = new StrategyBacktestQualityAuditService(_storagePaths).Load();
        var mutationExecution = new MutationValidationExecutorService(_storagePaths, _runtimeRoot).Load();
        var attributionFeedback = new AttributionHypothesisFeedbackService(_storagePaths).Load();
        var mutationQueue = new MutationCandidateQueueService(_storagePaths).Load() ?? new MutationCandidateQueueService(_storagePaths).Run();
        var mutationPlanner = new MutationValidationJobPlannerService(_storagePaths, _runtimeRoot).Load() ?? new MutationValidationJobPlannerService(_storagePaths, _runtimeRoot).Run();

        var enabled = IsEnabled(config, evidenceLoop);
        var inWorkWindow = timeControl.InWorkWindow;
        var inLearningWindow = timeControl.LearningWindow.ActiveNow || timeControl.NightlyWindow.ActiveNow;
        var safetyEligible = enabled && (inWorkWindow || inLearningWindow);

        var step = ExecuteStep(safetyEligible, _storagePaths, _runtimeRoot, mutationQueue, mutationPlanner, failureLearning, qualityAudit, evidenceLoop, attributionFeedback);
        var report = new AutonomousResearchLoopOrchestratorReport(
            ReportVersion: "autonomous_research_loop_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: !safetyEligible ? "waiting_for_window" : step.Status,
            StatusLabel: !safetyEligible ? "wartet auf erlaubtes Zeitfenster" : step.Status == "executed" ? "lief" : "geplant",
            InWorkWindow: inWorkWindow,
            InLearningWindow: inLearningWindow,
            EvidenceAutoLoopEnabled: evidenceLoop.Enabled,
            ResearchLoopEnabled: evidenceLoop.Enabled
                || config.Jobs.Any(job => job.JobId.Equals("validation_backlog_executor", StringComparison.OrdinalIgnoreCase) && job.Enabled),
            SafetyEligible: safetyEligible,
            SafetyStatus: BuildSafetyStatus(timeControl, evidenceLoop),
            FrankRequired: step.FrankRequired,
            StepType: step.StepType,
            StepStatus: step.Status,
            StepResult: step.Result,
            SelectedJobId: step.SelectedJobId,
            SelectedJobMutationType: step.SelectedJobMutationType,
            WhySelected: step.WhySelected,
            NextPlannedStep: step.NextPlannedStep,
            MutationExecution: step.MutationExecution ?? mutationExecution,
            AttributionHypothesisFeedback: attributionFeedback,
            MutationPlanner: mutationPlanner,
            MutationQueue: mutationQueue,
            FailureLearning: failureLearning,
            QualityAudit: qualityAudit,
            EvidenceAutoLoopState: evidenceLoop,
            TimeControl: timeControl,
            Warnings: step.Warnings.ToList(),
            OperatorSummary: BuildOperatorSummary(step, safetyEligible, inLearningWindow, inWorkWindow),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report);
        return report;
    }

    public AutonomousResearchLoopOrchestratorReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AutonomousResearchLoopOrchestratorReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static bool IsEnabled(ScheduleConfig config, EvidenceAutoLoopState evidenceLoop)
        => evidenceLoop.Enabled
            || config.Jobs.Any(job => job.JobId.Equals("validation_backlog_executor", StringComparison.OrdinalIgnoreCase) && job.Enabled)
            || config.Jobs.Any(job => job.JobId.Equals("evidence_auto_loop", StringComparison.OrdinalIgnoreCase) && job.Enabled);

    private static AutonomousResearchLoopStepResult ExecuteStep(
        bool safetyEligible,
        StoragePaths storagePaths,
        string runtimeRoot,
        MutationCandidateQueueReport mutationQueue,
        MutationValidationJobPlannerReport mutationPlanner,
        StrategyBacktestFailureLearningReport? failureLearning,
        StrategyBacktestQualityAuditReport? qualityAudit,
        EvidenceAutoLoopState evidenceLoop,
        AttributionHypothesisFeedbackReport? attributionFeedback)
    {
        if (!safetyEligible)
        {
            return new AutonomousResearchLoopStepResult(
                StepId: "wait_for_window",
                StepType: "wait",
                Status: "waiting_for_allowed_time_window",
                Result: "waiting",
                SelectedJobId: null,
                SelectedJobMutationType: null,
                WhySelected: "Zeitsteuerung oder Safety-Fenster nicht aktiv.",
                NextPlannedStep: "Warten auf erlaubtes Arbeits- oder Lernfenster.",
                MutationExecution: null,
                FrankRequired: false,
                Warnings: ["outside_allowed_window"],
                ActionsTaken: []);
        }

        var readyJob = mutationPlanner.Jobs
            .Where(job => job.ReadinessStatus.Equals("ready_to_execute", StringComparison.OrdinalIgnoreCase))
            .OrderBy(job => PriorityRank(job.Priority))
            .ThenBy(job => MutationTypeRank(job.MutationType))
            .ThenBy(job => job.ValidationJobId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (readyJob is null)
        {
            return new AutonomousResearchLoopStepResult(
                StepId: "prepare_planner",
                StepType: "prepare",
                Status: "planner_needed",
                Result: "planner",
                SelectedJobId: null,
                SelectedJobMutationType: null,
                WhySelected: "Keine ready_to_execute Mutation Jobs vorhanden.",
                NextPlannedStep: "Mutation Planner oder Validation Planner vorbereiten.",
                MutationExecution: null,
                FrankRequired: false,
                Warnings: ["no_ready_mutation_validation_jobs"],
                ActionsTaken: ["mutation_candidate_queue_available", $"mutation_candidates={mutationQueue.QueueSize}"]);
        }

        if (!IsSupportedMutationJob(readyJob))
        {
            return new AutonomousResearchLoopStepResult(
                StepId: "skip_unsupported",
                StepType: "validate",
                Status: "unsupported",
                Result: "unsupported",
                SelectedJobId: readyJob.ValidationJobId,
                SelectedJobMutationType: readyJob.MutationType,
                WhySelected: "Top-Kandidat ist nicht durch die Minimal-Engine unterstützt.",
                NextPlannedStep: "Nächsten kompatiblen Mutation Job vormerken.",
                MutationExecution: null,
                FrankRequired: false,
                Warnings: ["unsupported_mutation_job"],
                ActionsTaken: [$"selected={readyJob.ValidationJobId}"]);
        }

        var executor = new MutationValidationExecutorService(storagePaths, runtimeRoot, readyJob.ValidationJobId, 50);
        var execution = executor.Run();
        var status = execution.Execution?.Status ?? "failed";
        var outcome = execution.Comparison?.Outcome ?? (execution.Execution?.ExecutionSupported == true ? "inconclusive" : "failed");
        var next = outcome == "improved"
            ? "OOS-/Walk-Forward-Plan vorbereiten."
            : outcome == "worse"
                ? "Mutation zurückstufen und nächste Mutation planen."
                : "Nächsten sicheren Mutation-Kandidat prüfen.";

        return new AutonomousResearchLoopStepResult(
            StepId: "mutation_validation",
            StepType: "validate",
            Status: status,
            Result: outcome,
            SelectedJobId: readyJob.ValidationJobId,
            SelectedJobMutationType: readyJob.MutationType,
            WhySelected: $"Höchste Priorität unter unterstützten ready_to_execute Jobs: {readyJob.MutationType}.",
            NextPlannedStep: next,
            MutationExecution: execution,
            FrankRequired: false,
            Warnings: execution.Warnings.Concat(execution.Execution?.Warnings ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ActionsTaken: new[]
            {
                $"mutation_job={readyJob.ValidationJobId}",
                $"execution_status={status}",
                $"comparison_outcome={outcome}",
            });
    }

    private static bool IsSupportedMutationJob(MutationValidationJobPlan job)
        => job.Asset.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase)
            && job.Timeframe.Equals("M5", StringComparison.OrdinalIgnoreCase)
            && job.StrategyPattern.Equals("Mean Reversion Rejection", StringComparison.OrdinalIgnoreCase)
            && job.MutationType is "session_filter_sharpen" or "range_regime_enforce";

    private static string BuildSafetyStatus(ScheduleTimeControlStatus timeControl, EvidenceAutoLoopState evidenceLoop)
        => $"no_auto_trading=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true, evidence_auto_loop_enabled={evidenceLoop.Enabled.ToString().ToLowerInvariant()}, in_work_window={timeControl.InWorkWindow.ToString().ToLowerInvariant()}, learning_window={timeControl.LearningWindow.ActiveNow.ToString().ToLowerInvariant()}";

    private static string BuildOperatorSummary(AutonomousResearchLoopStepResult step, bool safetyEligible, bool inLearningWindow, bool inWorkWindow)
    {
        if (!safetyEligible)
        {
            return "Hermes arbeitet selbstständig an der Research-Queue. Letzter Schritt: wartet auf Zeitfenster. Frank nötig: nein.";
        }

        return step.Status switch
        {
            "completed_improved" => "Hermes arbeitet selbstständig an der Research-Queue. Letzter Schritt: Mutation getestet. Frank nötig: nein.",
            "completed_worse" => "Hermes arbeitet selbstständig an der Research-Queue. Letzter Schritt: Mutation zurückgestuft. Frank nötig: nein.",
            "completed_inconclusive" => "Hermes arbeitet selbstständig an der Research-Queue. Letzter Schritt: Mutation getestet ohne klare Verbesserung. Frank nötig: nein.",
            "unsupported" => "Hermes arbeitet selbstständig an der Research-Queue. Letzter Schritt: Mutation nicht unterstützt. Frank nötig: nein.",
            _ => "Hermes arbeitet selbstständig an der Research-Queue. Letzter Schritt: Research vorbereitet. Frank nötig: nein.",
        };
    }

    private static int PriorityRank(string priority)
        => priority.Equals("high", StringComparison.OrdinalIgnoreCase) ? 0
            : priority.Equals("medium", StringComparison.OrdinalIgnoreCase) ? 1
            : 2;

    private static int MutationTypeRank(string mutationType)
        => mutationType.Equals("session_filter_sharpen", StringComparison.OrdinalIgnoreCase) ? 0
            : mutationType.Equals("range_regime_enforce", StringComparison.OrdinalIgnoreCase) ? 1
            : 2;

    private void WriteArtifacts(AutonomousResearchLoopOrchestratorReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(AutonomousResearchLoopOrchestratorReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Autonomous Research Loop Orchestrator");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Status label: {report.StatusLabel}");
        sb.AppendLine($"- In work window: {report.InWorkWindow}");
        sb.AppendLine($"- In learning window: {report.InLearningWindow}");
        sb.AppendLine($"- Safety eligible: {report.SafetyEligible}");
        sb.AppendLine($"- Selected job: {report.SelectedJobId ?? "-"}");
        sb.AppendLine($"- Selected mutation type: {report.SelectedJobMutationType ?? "-"}");
        sb.AppendLine($"- Next planned step: {report.NextPlannedStep}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine(report.SafetyStatus);
        return sb.ToString();
    }
}
