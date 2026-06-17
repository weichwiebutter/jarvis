using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ValidationBacklogExecutorStep(
    string StepId,
    string Title,
    string AreaId,
    string AreaTitle,
    string Priority,
    string Status,
    string Result,
    int PlannedCount,
    int ExecutedCount,
    int SkippedCount,
    string NextAction,
    bool FrankRequired,
    bool AutomaticallyAllowed,
    bool SafeToExecute,
    string WindowHint,
    string? OutputReportPath,
    DateTimeOffset? ExecutedAtUtc,
    IReadOnlyList<string> Warnings);

public sealed record ValidationBacklogExecutorReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    bool Configured,
    bool Enabled,
    string Mode,
    string StatusLabel,
    string WindowLabel,
    bool InWorkWindow,
    bool InNightlyWindow,
    bool ResourceHealthy,
    int MaxTasksPerRun,
    DateTimeOffset? LastRunUtc,
    DateTimeOffset? NextRunUtc,
    string NextRunHint,
    int BacklogItemsAnalyzed,
    int PlannedWorkItems,
    int ExecutedWorkItems,
    int SkippedWorkItems,
    int PlannedSteps,
    int ExecutedSteps,
    int SkippedSteps,
    int ValidationTasksCreated,
    int EvidenceTasksExecuted,
    int ReviewsRefreshed,
    int FrankRequired,
    IReadOnlyList<ValidationBacklogPriorityArea> PriorityAreas,
    IReadOnlyList<ValidationBacklogExecutorStep> Steps,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string AnalyzerPath,
    string QueuePath,
    string ReportPath,
    string MarkdownPath);

public sealed class ValidationBacklogExecutorService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _configPath;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public ValidationBacklogExecutorService(StoragePaths storagePaths, string? configPath = null)
    {
        _storagePaths = storagePaths;
        _configPath = configPath ?? Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", "config", "schedules.json");
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "validation_backlog");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "validation_backlog_executor.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "validation_backlog_executor.md");

    public ValidationBacklogExecutorReport Execute(int maxTasksPerRun = 20) =>
        Evaluate(Math.Clamp(maxTasksPerRun, 1, 200), scheduledMode: false);

    public ValidationBacklogExecutorReport RunScheduled(int maxTasksPerRun = 20) =>
        Evaluate(Math.Clamp(maxTasksPerRun, 1, 200), scheduledMode: true);

    public ValidationBacklogExecutorReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ValidationBacklogExecutorReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private ValidationBacklogExecutorReport Evaluate(int maxTasksPerRun, bool scheduledMode)
    {
        Directory.CreateDirectory(Root);

        var scheduler = new HermesInternalScheduler(_storagePaths, _configPath);
        var config = scheduler.LoadConfig();
        var timeControl = scheduler.GetTimeControlStatus();
        var resourceGuard = new ResourceGuard(_storagePaths);
        var resource = resourceGuard.Check();
        var analyzerService = new ValidationBacklogAnalyzerService(_storagePaths, Directory.GetCurrentDirectory());
        var analyzer = analyzerService.Load() ?? analyzerService.Build();
        var trustPlan = new KnowledgeTrustImprovementPlannerService(_storagePaths).Load()
            ?? new KnowledgeTrustImprovementPlannerService(_storagePaths).Run();
        var priorityEngine = new ValidationBacklogPriorityEngineService(_storagePaths);
        var priorityAreas = priorityEngine.BuildAreas(analyzer, trustPlan);

        var configured = config.Jobs.Any(job => job.JobId.Equals("validation_backlog_executor", StringComparison.OrdinalIgnoreCase))
            || config.ValidationBacklogExecutorEnabled;
        var enabled = config.ValidationBacklogExecutorEnabled
            || config.Jobs.Any(job => job.JobId.Equals("validation_backlog_executor", StringComparison.OrdinalIgnoreCase) && job.Enabled);
        var inWindow = timeControl.LearningWindow.ActiveNow || timeControl.NightlyWindow.ActiveNow;
        var resourceHealthy = !resource.ShouldPause && !resource.ShouldStop;
        var canAutoRun = scheduledMode ? enabled && inWindow && resourceHealthy : true;

        var steps = new List<ValidationBacklogExecutorStep>();
        var warnings = new List<string>();
        var plannedBacklogItems = analyzer.OpenValidationsByDomain.Sum(item => item.PendingCount);
        var plannedWorkItems = Math.Min(plannedBacklogItems, maxTasksPerRun);
        var executedWorkItems = 0;
        var validationTasksCreated = 0;
        var evidenceTasksExecuted = 0;
        var reviewsRefreshed = 0;

        void AddStep(
            string stepId,
            string title,
            string areaId,
            string areaTitle,
            string priority,
            int plannedCount,
            bool automaticallyAllowed,
            bool safeToExecute,
            string windowHint,
            string nextAction,
            Func<(string Result, string? ReportPath, int ExecutedCount, IReadOnlyList<string> StepWarnings, string Status)> action)
        {
            if (!canAutoRun && scheduledMode)
            {
                steps.Add(new ValidationBacklogExecutorStep(
                    StepId: stepId,
                    Title: title,
                    AreaId: areaId,
                    AreaTitle: areaTitle,
                    Priority: priority,
                    Status: "geplant",
                    Result: "geplant",
                    PlannedCount: plannedCount,
                    ExecutedCount: 0,
                    SkippedCount: plannedCount,
                    NextAction: nextAction,
                    FrankRequired: areaId == "contradiction_analysis",
                    AutomaticallyAllowed: automaticallyAllowed,
                    SafeToExecute: safeToExecute,
                    WindowHint: windowHint,
                    OutputReportPath: null,
                    ExecutedAtUtc: null,
                    Warnings: [scheduledMode ? "scheduler_window_not_open" : "manual_run"]));
                return;
            }

            var outcome = action();
            steps.Add(new ValidationBacklogExecutorStep(
                StepId: stepId,
                Title: title,
                AreaId: areaId,
                AreaTitle: areaTitle,
                Priority: priority,
                Status: outcome.Status,
                Result: outcome.Result,
                PlannedCount: plannedCount,
                ExecutedCount: outcome.ExecutedCount,
                SkippedCount: Math.Max(0, plannedCount - outcome.ExecutedCount),
                NextAction: nextAction,
                FrankRequired: areaId == "contradiction_analysis",
                AutomaticallyAllowed: automaticallyAllowed,
                SafeToExecute: safeToExecute,
                WindowHint: windowHint,
                OutputReportPath: outcome.ReportPath,
                ExecutedAtUtc: DateTimeOffset.UtcNow,
                Warnings: outcome.StepWarnings));
        }

        AddStep(
            "validation_queue_refill",
            "Validation Queue nachfüllen",
            "schedule_revalidation",
            "Re-Validierung",
            "high",
            analyzer.ValidationPlansOpen,
            true,
            true,
            "Nightly",
            "Offene Validierungspläne in Tasks überführen.",
            () =>
            {
                var refill = new ValidationQueueRefillService(_storagePaths).Refill();
                validationTasksCreated += refill.TasksCreated;
                return ("executed", refill.ReportPath, refill.TasksCreated, Array.Empty<string>(), "executed");
            });

        AddStep(
            "evidence_auto_loop",
            "Evidence Auto-Loop ausführen",
            "gather_more_evidence",
            "Evidenz sammeln",
            "high",
            priorityAreas.FirstOrDefault(area => area.AreaId == "gather_more_evidence")?.ItemCount ?? 0,
            true,
            true,
            "Arbeitsfenster",
            "Weitere Evidenzläufe planen.",
            () =>
            {
                var autoLoop = new EvidenceAutoLoopService(_storagePaths);
                var report = autoLoop.Run();
                return ("executed", autoLoop.ReportPath, report.PlannedTasks, report.Warnings, "executed");
            });

        AddStep(
            "run_evidence_tasks",
            "Evidenzaufgaben abarbeiten",
            "schedule_revalidation",
            "Re-Validierung",
            "high",
            plannedWorkItems,
            true,
            true,
            "Nightly",
            "Sichere Evidenz- und Validierungsaufgaben ausführen.",
            () =>
            {
                var runner = new EvidenceValidationRunnerService(_storagePaths);
                var perDomain = Math.Max(1, plannedWorkItems / 5);
                var report = runner.Run(maxDomains: 5, maxItemsPerDomain: perDomain);
                evidenceTasksExecuted += report.EvidenceTasksExecuted;
                executedWorkItems += report.EvidenceTasksExecuted;
                return ("executed", runner.ReportPath, report.EvidenceTasksExecuted, report.Warnings, "executed");
            });

        AddStep(
            "review_evidence_refresh",
            "Review Evidence Refresh",
            "contradiction_analysis",
            "Widersprüche prüfen",
            "high",
            priorityAreas.FirstOrDefault(area => area.AreaId == "contradiction_analysis")?.ItemCount ?? 0,
            true,
            true,
            "Arbeitsfenster",
            "Reviews mit neuer Evidenz aktualisieren.",
            () =>
            {
                var refresh = new ReviewEvidenceRefreshService(_storagePaths).Run();
                reviewsRefreshed += refresh.ReviewsUpdated;
                return ("executed", refresh.ReportPath, refresh.ReviewsUpdated, refresh.Warnings, "executed");
            });

        AddStep(
            "review_decision_assistant",
            "Review Decision Assistant aktualisieren",
            "contradiction_analysis",
            "Widersprüche prüfen",
            "high",
            20,
            true,
            true,
            "Arbeitsfenster",
            "Empfehlungen für Frank aktualisieren.",
            () =>
            {
                var assistant = new ReviewDecisionAssistantService(_storagePaths).Run();
                return ("executed", assistant.ReportPath, assistant.ReviewCount, assistant.Warnings, "executed");
            });

        AddStep(
            "knowledge_validation_audit",
            "Knowledge Validation Audit aktualisieren",
            "schedule_revalidation",
            "Re-Validierung",
            "high",
            analyzer.OpenValidationsByDomain.Sum(item => item.PendingCount),
            true,
            true,
            "Nightly",
            "Audit und Konsistenz neu schreiben.",
            () =>
            {
                var audit = new KnowledgeValidationAuditService(_storagePaths).Run();
                return ("executed", audit.AuditPath, audit.ValidationTasksPending, audit.Warnings, "executed");
            });

        AddStep(
            "validation_backlog_analyzer",
            "Validation Backlog Analyzer aktualisieren",
            "systempflege",
            "Systempflege",
            "low",
            analyzer.OpenValidationsByDomain.Sum(item => item.PendingCount),
            true,
            true,
            "bei Bedarf",
            "Validierungsstau neu analysieren.",
            () =>
            {
                var updated = analyzerService.Build();
                return ("executed", analyzerService.ReportPath, updated.OpenValidationsByDomain.Sum(item => item.PendingCount), updated.Warnings, "executed");
            });

        var runStartedAtUtc = DateTimeOffset.UtcNow;
        var nextRunUtc = CalculateNextRunUtc(timeControl, config, runStartedAtUtc, enabled);
        if (scheduledMode)
        {
            scheduler.UpdateValidationBacklogExecutorRunState(runStartedAtUtc, nextRunUtc);
        }
        else
        {
            scheduler.UpdateValidationBacklogExecutorRunState(runStartedAtUtc, nextRunUtc);
        }

        var report = new ValidationBacklogExecutorReport(
            ReportVersion: scheduledMode ? "validation_backlog_executor_v1" : "validation_backlog_executor_manual_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Configured: configured,
            Enabled: enabled,
            Mode: scheduledMode
                ? (enabled ? (canAutoRun ? "läuft" : "wartet auf Lernfenster") : "deaktiviert")
                : "manuell ausgeführt",
            StatusLabel: scheduledMode
                ? (enabled ? (canAutoRun ? "Aktiv" : "Aktiviert – wartet auf Lernfenster") : "Deaktiviert")
                : "Manuell ausgeführt",
            WindowLabel: timeControl.LearningWindow.ActiveNow
                ? "Lernfenster"
                : timeControl.NightlyWindow.ActiveNow
                    ? "Nightly"
                    : "außerhalb des Fensters",
            InWorkWindow: timeControl.InWorkWindow,
            InNightlyWindow: timeControl.NightlyWindow.ActiveNow,
            ResourceHealthy: resourceHealthy,
            MaxTasksPerRun: maxTasksPerRun,
            LastRunUtc: runStartedAtUtc,
            NextRunUtc: nextRunUtc,
            NextRunHint: BuildNextRunHint(scheduledMode, enabled, timeControl, nextRunUtc),
            BacklogItemsAnalyzed: analyzer.OpenValidationsByDomain.Sum(item => item.PendingCount),
            PlannedWorkItems: plannedWorkItems,
            ExecutedWorkItems: executedWorkItems,
            SkippedWorkItems: Math.Max(0, analyzer.OpenValidationsByDomain.Sum(item => item.PendingCount) - plannedWorkItems),
            PlannedSteps: steps.Count,
            ExecutedSteps: steps.Count(step => step.Status.Equals("executed", StringComparison.OrdinalIgnoreCase)),
            SkippedSteps: steps.Count(step => step.Status.Equals("geplant", StringComparison.OrdinalIgnoreCase) || step.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase)),
            ValidationTasksCreated: validationTasksCreated,
            EvidenceTasksExecuted: evidenceTasksExecuted,
            ReviewsRefreshed: reviewsRefreshed,
            FrankRequired: 0,
            PriorityAreas: priorityAreas,
            Steps: steps,
            Warnings: warnings
                .Concat(steps.SelectMany(step => step.Warnings))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            AnalyzerPath: analyzerService.ReportPath,
            QueuePath: new AutonomousImprovementQueueService(_storagePaths).QueuePath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        TryWriteReport(report);
        TryWriteMasterStatusSnapshot();
        return report;
    }

    private static string BuildNextRunHint(bool scheduledMode, bool enabled, ScheduleTimeControlStatus timeControl, DateTimeOffset? nextRunUtc)
    {
        if (!enabled)
        {
            return "Deaktiviert";
        }

        if (!scheduledMode)
        {
            return nextRunUtc?.ToString("O") ?? "Nächster Lauf wird beim Scheduler-Lauf berechnet.";
        }

        if (timeControl.LearningWindow.ActiveNow || timeControl.NightlyWindow.ActiveNow)
        {
            return "Aktiv – wartet auf Ausführung oder läuft";
        }

        if (nextRunUtc is not null)
        {
            return nextRunUtc.Value.ToString("O");
        }

        return "Nächster Lauf wird beim Scheduler-Lauf berechnet.";
    }

    private static DateTimeOffset? CalculateNextRunUtc(ScheduleTimeControlStatus timeControl, ScheduleConfig config, DateTimeOffset nowUtc, bool enabled)
    {
        if (!enabled)
        {
            return null;
        }

        if (timeControl.LearningWindow.ActiveNow || timeControl.NightlyWindow.ActiveNow)
        {
            return nowUtc;
        }

        var zone = ResolveTimeZone(timeControl.TimeZone);
        var currentLocal = TimeZoneInfo.ConvertTime(nowUtc, zone);
        if (TimeOnly.TryParse(config.LearningWindow.Start, out var learningStart))
        {
            var candidate = currentLocal.Date + learningStart.ToTimeSpan();
            if (candidate <= currentLocal.DateTime)
            {
                candidate = candidate.AddDays(1);
            }

            return new DateTimeOffset(candidate, zone.GetUtcOffset(candidate));
        }

        return null;
    }

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return string.IsNullOrWhiteSpace(timeZoneId)
                ? TimeZoneInfo.Local
                : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Local;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }

    private void TryWriteReport(ValidationBacklogExecutorReport report)
    {
        try
        {
            var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
            var markdown = BuildMarkdown(report);
            Directory.CreateDirectory(Root);
            File.WriteAllText(ReportPath, json);
            File.WriteAllText(MarkdownPath, markdown);
            _resolvedReportPath = ReportPath;
            _resolvedMarkdownPath = MarkdownPath;
        }
        catch (Exception)
        {
            var fallbackRoots = new[]
            {
                Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "validation_backlog"),
                Path.Combine(Path.GetTempPath(), "hermes", "reports", "validation_backlog"),
            };

            var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
            var markdown = BuildMarkdown(report);
            foreach (var fallbackRoot in fallbackRoots)
            {
                try
                {
                    Directory.CreateDirectory(fallbackRoot);
                    var fallbackReportPath = Path.Combine(fallbackRoot, "validation_backlog_executor.json");
                    var fallbackMarkdownPath = Path.Combine(fallbackRoot, "validation_backlog_executor.md");
                    File.WriteAllText(fallbackReportPath, json);
                    File.WriteAllText(fallbackMarkdownPath, markdown);
                    _resolvedReportPath = fallbackReportPath;
                    _resolvedMarkdownPath = fallbackMarkdownPath;
                    return;
                }
                catch
                {
                    // Try next fallback root.
                }
            }

            throw;
        }
    }

    private static string BuildMarkdown(ValidationBacklogExecutorReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Validation Backlog Executor");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Configured: {report.Configured.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- Enabled: {report.Enabled.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- Mode: {report.Mode}");
        sb.AppendLine($"- Status: {report.StatusLabel}");
        sb.AppendLine($"- Window: {report.WindowLabel}");
        sb.AppendLine($"- Max tasks per run: {report.MaxTasksPerRun}");
        sb.AppendLine($"- Last run: {report.LastRunUtc?.ToString("O") ?? "-"}");
        sb.AppendLine($"- Next run: {report.NextRunUtc?.ToString("O") ?? report.NextRunHint}");
        sb.AppendLine($"- Backlog items analyzed: {report.BacklogItemsAnalyzed}");
        sb.AppendLine($"- Planned work items: {report.PlannedWorkItems}");
        sb.AppendLine($"- Executed work items: {report.ExecutedWorkItems}");
        sb.AppendLine($"- Skipped work items: {report.SkippedWorkItems}");
        sb.AppendLine($"- Frank required: {report.FrankRequired > 0}");
        sb.AppendLine();
        sb.AppendLine("## Work Areas");
        foreach (var area in report.PriorityAreas)
        {
            sb.AppendLine($"- {area.AreaTitle}: {area.ItemCount} · {area.Priority} · {area.Status} · {area.NextAction}");
        }
        sb.AppendLine();
        sb.AppendLine("## Steps");
        foreach (var step in report.Steps)
        {
            sb.AppendLine($"- {step.Title}: {step.Status} · {step.Result} · planned={step.PlannedCount} · executed={step.ExecutedCount}");
        }
        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine("- no_auto_trading=true");
        sb.AppendLine("- human_review_required=true");
        sb.AppendLine("- broker_orders_enabled=false");
        sb.AppendLine("- live_trading_enabled=false");
        sb.AppendLine("- research_only=true");
        return sb.ToString();
    }

    private void TryWriteMasterStatusSnapshot()
    {
        try
        {
            new MasterStatusWriter(new MasterStatusService(_storagePaths, Directory.GetCurrentDirectory())).WriteSnapshot();
        }
        catch
        {
        }
    }
}
