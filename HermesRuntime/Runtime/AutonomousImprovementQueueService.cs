using System.Text.Json;

namespace Hermes.Runtime;

public sealed record AutonomousImprovementTask(
    string TaskId,
    string SourceWarning,
    string Title,
    string Domain,
    string Priority,
    string Reason,
    string SuggestedAction,
    string Status,
    DateTimeOffset CreatedAtUtc,
    string DueHint,
    bool RequiresHumanReview,
    bool AutoFixable,
    bool SafeToExecute);

public sealed record AutonomousImprovementQueueReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int ActiveImprovements,
    string HighestPriority,
    int HermesCanHandle,
    int FrankItems,
    IReadOnlyList<AutonomousImprovementGroup> GroupedImprovementAreas,
    IReadOnlyList<AutonomousImprovementGroup> TopPriorityGroups,
    IReadOnlyList<AutonomousImprovementTask> Tasks,
    IReadOnlyList<string> SourceWarnings,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string AuditPath,
    string QueuePath,
    string SummaryPath,
    string MarkdownPath);

public sealed record AutonomousImprovementGroup(
    string GroupId,
    string GroupTitle,
    string ActionType,
    string Domain,
    string Priority,
    string SourceWarning,
    int ItemCount,
    int CompletedCount,
    int FailedCount,
    string Status,
    string NextAction);

public sealed record AutonomousImprovementQueueSummaryReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int ActiveAreas,
    int ActiveItems,
    int HermesCanHandle,
    int FrankItems,
    IReadOnlyList<AutonomousImprovementGroup> GroupedImprovementAreas,
    IReadOnlyList<AutonomousImprovementGroup> TopPriorityGroups,
    IReadOnlyList<string> SourceWarnings,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string QueuePath,
    string SummaryPath,
    string MarkdownPath);

public sealed record AutonomousImprovementWorkArea(
    string AreaId,
    string AreaTitle,
    int ItemCount,
    string Status,
    string HighestPriority,
    bool FrankRequired,
    string NextAction);

public sealed record AutonomousImprovementWorkAreasReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int ActiveAreas,
    int ActiveItems,
    int HermesCanHandle,
    int FrankItems,
    IReadOnlyList<AutonomousImprovementWorkArea> WorkAreas,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string QueuePath,
    string SummaryPath,
    string MarkdownPath);

public sealed class AutonomousImprovementQueueService
{
    private readonly StoragePaths _storagePaths;

    public AutonomousImprovementQueueService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "autonomous_improvement_queue");

    public string QueuePath => Path.Combine(Root, "autonomous_improvement_queue.json");

    public string SummaryPath => Path.Combine(Root, "autonomous_improvement_queue_summary.json");

    public string WorkAreasPath => Path.Combine(Root, "autonomous_improvement_work_areas.json");

    public string MarkdownPath => Path.Combine(Root, "autonomous_improvement_queue.md");

    public AutonomousImprovementQueueReport Generate()
    {
        Directory.CreateDirectory(Root);
        var auditService = new KnowledgeValidationAuditService(_storagePaths);
        var audit = auditService.Load() ?? auditService.Run();
        var trustPlanner = new KnowledgeTrustImprovementPlannerService(_storagePaths);
        var trustPlan = trustPlanner.Load() ?? trustPlanner.Run();
        var warnings = audit.Warnings
            .Concat(trustPlan.BlockerCounts.Keys)
            .Concat(new[] { "storage_cleanup_candidates" })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var tasks = BuildTasks(warnings, audit, trustPlan);
        var groupedAreas = BuildGroups(tasks);
        var workAreas = BuildWorkAreas(tasks);
        var report = new AutonomousImprovementQueueReport(
            ReportVersion: "autonomous_improvement_queue_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ActiveImprovements: tasks.Count(task => task.Status.Equals("open", StringComparison.OrdinalIgnoreCase)),
            HighestPriority: HighestPriority(tasks),
            HermesCanHandle: tasks.Count(task => !task.RequiresHumanReview),
            FrankItems: tasks.Count(task => task.RequiresHumanReview),
            GroupedImprovementAreas: groupedAreas,
            TopPriorityGroups: groupedAreas
                .OrderBy(group => PriorityRank(group.Priority))
                .ThenByDescending(group => group.ItemCount)
                .Take(5)
                .ToList(),
            Tasks: tasks,
            SourceWarnings: warnings,
            Warnings: tasks.Count == 0 ? ["autonomous_improvement_queue_empty"] : [],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            AuditPath: audit.AuditPath,
            QueuePath: QueuePath,
            SummaryPath: SummaryPath,
            MarkdownPath: MarkdownPath);

        var summary = new AutonomousImprovementQueueSummaryReport(
            ReportVersion: "autonomous_improvement_queue_summary_v1",
            UpdatedAtUtc: report.UpdatedAtUtc,
            ActiveAreas: groupedAreas.Count,
            ActiveItems: report.ActiveImprovements,
            HermesCanHandle: report.HermesCanHandle,
            FrankItems: report.FrankItems,
            GroupedImprovementAreas: groupedAreas,
            TopPriorityGroups: report.TopPriorityGroups,
            SourceWarnings: report.SourceWarnings,
            Warnings: report.Warnings,
            NoTradingExecution: report.NoTradingExecution,
            NoBrokerAction: report.NoBrokerAction,
            NoAutoTrading: report.NoAutoTrading,
            HumanReviewRequired: report.HumanReviewRequired,
            QueuePath: QueuePath,
            SummaryPath: SummaryPath,
            MarkdownPath: MarkdownPath);
        var workAreasReport = new AutonomousImprovementWorkAreasReport(
            ReportVersion: "autonomous_improvement_work_areas_v1",
            UpdatedAtUtc: report.UpdatedAtUtc,
            ActiveAreas: workAreas.Count,
            ActiveItems: report.ActiveImprovements,
            HermesCanHandle: report.HermesCanHandle,
            FrankItems: report.FrankItems,
            WorkAreas: workAreas,
            Warnings: report.Warnings,
            NoTradingExecution: report.NoTradingExecution,
            NoBrokerAction: report.NoBrokerAction,
            NoAutoTrading: report.NoAutoTrading,
            HumanReviewRequired: report.HumanReviewRequired,
            QueuePath: QueuePath,
            SummaryPath: SummaryPath,
            MarkdownPath: MarkdownPath);
        WriteQueueOutputs(report, summary, workAreasReport);
        return report;
    }

    public AutonomousImprovementQueueReport? Load()
    {
        if (!File.Exists(QueuePath))
        {
            return null;
        }

        try
        {
            var report = JsonSerializer.Deserialize<AutonomousImprovementQueueReport>(
                File.ReadAllText(QueuePath),
                JsonDefaults.SnapshotReadOptions);
            return HasLegacyQueueShape(report) ? null : report;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static bool HasLegacyQueueShape(AutonomousImprovementQueueReport? report)
    {
        return report is null
            || report.GroupedImprovementAreas is null
            || report.TopPriorityGroups is null;
    }

    private static IReadOnlyList<AutonomousImprovementTask> BuildTasks(
        IReadOnlyList<string> warnings,
        KnowledgeValidationAuditReport audit,
        KnowledgeTrustImprovementPlanReport trustPlan)
    {
        var tasks = new List<AutonomousImprovementTask>();
        var now = DateTimeOffset.UtcNow;

        foreach (var warning in warnings.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var mapping = warning switch
            {
                "oos_data_missing" => NewTask(warning, "OOS-/Walk-Forward-Validierung planen", "trading", "high", "Die Audit-Kette meldet fehlende OOS-Absicherung.", "Weitere Walk-Forward-/OOS-Läufe planen.", false, true, true, "Innerhalb des nächsten Validierungszyklus"),
                "knowledge_validation_queue_missing" => NewTask(warning, "Validation Queue prüfen/reparieren", "research", "high", "Offene Validation Plans werden nicht in Queue-Arbeit überführt.", "Validation Queue befüllen oder Routing reparieren.", false, true, true, "Sofort"),
                "hypotheses_without_validation_queue" => NewTask(warning, "Hypothesen in Queue überführen", "research", "high", "Hypothesen hängen ohne Queue-Arbeit.", "Offene Hypothesen in die Validierungswarteschlange überführen.", false, true, true, "Sofort"),
                "no_robust_strategies" => NewTask(warning, "Research-/Robustness-Lauf planen", "trading", "high", "Es gibt noch keine robuste Strategie.", "Nächsten Robustness-/Research-Lauf planen.", false, true, true, "Beim nächsten Research-Fenster"),
                "storage_cleanup_candidates" => NewTask(warning, "Cleanup-Plan aktualisieren", "process", "low", "Speicherbereinigung wäre sinnvoll.", "Cleanup-Plan bei Bedarf aktualisieren.", false, true, true, "Bei Speicherbedarf"),
                _ => null
            };

            if (mapping is not null)
            {
                tasks.Add(mapping with { CreatedAtUtc = now });
            }
        }

        if (audit.ValidationQueueExists && audit.ValidationQueueFilled && audit.ValidationQueueProcessed && !tasks.Any(task => task.SourceWarning.Equals("knowledge_validation_queue_missing", StringComparison.OrdinalIgnoreCase)))
        {
            tasks.Add(NewTask(
                "knowledge_validation_audit",
                "Audit-Ergebnisse weiterverfolgen",
                "research",
                "info",
                "Die Audit-Ergebnisse sind dokumentiert und müssen nur nachverfolgt werden.",
                "Keine Aktion erforderlich, nur Fortschritt beobachten.",
                false,
                false,
                false,
                "Im nächsten Status-Check") with { CreatedAtUtc = now });
        }

        foreach (var plannedAction in trustPlan.PlannedActions)
        {
            var mapping = plannedAction.ActionId.StartsWith("gather_more_evidence", StringComparison.OrdinalIgnoreCase)
                ? NewTask(plannedAction.Blocker, plannedAction.Title, plannedAction.Domain, plannedAction.Priority, "Trusted-Kandidaten brauchen mehr Evidenz.", plannedAction.SuggestedAction, plannedAction.RequiresHumanReview, plannedAction.AutoFixable, true, "Im nächsten Validierungsfenster")
                : plannedAction.ActionId.StartsWith("source_expansion", StringComparison.OrdinalIgnoreCase)
                    ? NewTask(plannedAction.Blocker, plannedAction.Title, plannedAction.Domain, plannedAction.Priority, "Trusted-Kandidaten brauchen zusätzliche Quellen.", plannedAction.SuggestedAction, plannedAction.RequiresHumanReview, plannedAction.AutoFixable, true, "Im nächsten Validierungsfenster")
                    : plannedAction.ActionId.StartsWith("schedule_revalidation", StringComparison.OrdinalIgnoreCase)
                        ? NewTask(plannedAction.Blocker, plannedAction.Title, plannedAction.Domain, plannedAction.Priority, "Trusted-Kandidaten brauchen Re-Validierung.", plannedAction.SuggestedAction, plannedAction.RequiresHumanReview, plannedAction.AutoFixable, true, "Beim nächsten Validation Cycle")
                        : plannedAction.ActionId.StartsWith("contradiction_analysis", StringComparison.OrdinalIgnoreCase)
                            ? NewTask(plannedAction.Blocker, plannedAction.Title, plannedAction.Domain, plannedAction.Priority, "Aktive Widersprüche müssen analysiert werden.", plannedAction.SuggestedAction, plannedAction.RequiresHumanReview, plannedAction.AutoFixable, true, "Sofort")
                            : null;

            if (mapping is not null)
            {
                tasks.Add(mapping with { CreatedAtUtc = now, TaskId = $"improvement_{plannedAction.ActionId}" });
            }
        }

        return tasks
            .OrderByDescending(task => PriorityRank(task.Priority))
            .ThenBy(task => task.TaskId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<AutonomousImprovementGroup> BuildGroups(IReadOnlyList<AutonomousImprovementTask> tasks)
    {
        return tasks
            .GroupBy(task => new
            {
                ActionType = ActionTypeFromTask(task),
                Domain = NormalizeGroupValue(task.Domain, "allgemein"),
                Priority = NormalizeGroupValue(task.Priority, "low"),
                SourceWarning = NormalizeGroupValue(task.SourceWarning, "unknown"),
            })
            .Select(group =>
            {
                var ordered = group.OrderBy(task => task.Status.Equals("executed", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ToList();
                return new AutonomousImprovementGroup(
                    GroupId: $"group_{group.Key.ActionType}_{group.Key.Domain}_{group.Key.Priority}_{group.Key.SourceWarning}",
                    GroupTitle: GroupTitleFromActionType(group.Key.ActionType),
                    ActionType: group.Key.ActionType,
                    Domain: group.Key.Domain,
                    Priority: group.Key.Priority,
                    SourceWarning: group.Key.SourceWarning,
                    ItemCount: group.Count(),
                    CompletedCount: ordered.Count(task => task.Status.Equals("executed", StringComparison.OrdinalIgnoreCase)),
                    FailedCount: ordered.Count(task => task.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)),
                    Status: GroupStatus(group),
                    NextAction: NextActionFromGroup(group.Key.ActionType));
            })
            .OrderBy(group => PriorityRank(group.Priority))
            .ThenByDescending(group => group.ItemCount)
            .ThenBy(group => group.GroupTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public IReadOnlyList<AutonomousImprovementWorkArea> BuildWorkAreas(IReadOnlyList<AutonomousImprovementTask> tasks)
    {
        var areas = new[]
        {
            BuildWorkArea(tasks, "gather_more_evidence", "Evidenz sammeln"),
            BuildWorkArea(tasks, "source_expansion", "Quellen erweitern"),
            BuildWorkArea(tasks, "schedule_revalidation", "Re-Validierung"),
            BuildWorkArea(tasks, "contradiction_analysis", "Widersprüche prüfen"),
            BuildWorkArea(tasks, "systempflege", "Systempflege"),
        };

        return areas
            .OrderBy(area => WorkAreaOrder(area.AreaId))
            .ToList();
    }

    private static AutonomousImprovementWorkArea BuildWorkArea(
        IReadOnlyList<AutonomousImprovementTask> tasks,
        string areaId,
        string areaTitle)
    {
        var items = tasks.Where(task => WorkAreaFromTask(task).Equals(areaId, StringComparison.OrdinalIgnoreCase)).ToList();
        var highestPriority = items.Count == 0 ? "niedrig" : items.OrderBy(task => PriorityRank(task.Priority)).First().Priority;
        return new AutonomousImprovementWorkArea(
            AreaId: areaId,
            AreaTitle: areaTitle,
            ItemCount: items.Count,
            Status: items.Any(task => task.Status.Equals("failed", StringComparison.OrdinalIgnoreCase))
                ? "failed"
                : items.Any(task => task.Status.Equals("executed", StringComparison.OrdinalIgnoreCase))
                    ? "completed"
                    : "open",
            HighestPriority: highestPriority,
            FrankRequired: items.Any(task => task.RequiresHumanReview),
            NextAction: NextActionFromWorkArea(areaId));
    }

    private static string NormalizeGroupValue(string? value, string fallback)
    {
        var text = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return text.ToLowerInvariant();
    }

    private static string ActionTypeFromTask(AutonomousImprovementTask task)
    {
        var source = task.SourceWarning.ToLowerInvariant();
        var title = task.Title.ToLowerInvariant();

        if (source.Contains("cleanup") || title.Contains("cleanup"))
        {
            return "cleanup_plan_update";
        }

        if (source.Contains("contradiction") || title.Contains("widerspruch"))
        {
            return "contradiction_analysis";
        }

        if (source.Contains("validation_queue_missing") || source.Contains("knowledge_validation_queue_missing"))
        {
            return "validation_queue_repair";
        }

        if (source.Contains("hypotheses_without_validation_queue"))
        {
            return "validation_queue_repair";
        }

        if (source.Contains("schedule_revalidation") || source.Contains("validation_score_too_low") || source.Contains("not_recently_validated"))
        {
            return "schedule_revalidation";
        }

        if (source.Contains("source_expansion") || source.Contains("insufficient_sources") || source.Contains("quality_score_too_low"))
        {
            return "source_expansion";
        }

        if (source.Contains("gather_more_evidence") || source.Contains("trust_score_too_low"))
        {
            return "gather_more_evidence";
        }

        if (source.Contains("robust") || source.Contains("no_robust_strategies"))
        {
            return "robustness_planning";
        }

        if (source.Contains("audit"))
        {
            return "audit_followup";
        }

        return "general_improvement";
    }

    private static string GroupTitleFromActionType(string actionType)
    {
        return actionType switch
        {
            "gather_more_evidence" => "Mehr Evidenz sammeln",
            "source_expansion" => "Quellen erweitern",
            "schedule_revalidation" => "Re-Validierung planen",
            "contradiction_analysis" => "Widersprüche prüfen",
            "validation_queue_repair" => "Validation Queue reparieren",
            "cleanup_plan_update" => "Systempflege",
            "robustness_planning" => "Robustheit planen",
            "audit_followup" => "Audit-Ergebnisse verfolgen",
            _ => "Allgemeine Verbesserungen",
        };
    }

    private static string WorkAreaFromTask(AutonomousImprovementTask task)
    {
        var source = task.SourceWarning.ToLowerInvariant();
        var title = task.Title.ToLowerInvariant();

        if (source.Contains("trust_score") || source.Contains("evidence") || title.Contains("evidenz"))
        {
            return "gather_more_evidence";
        }

        if (source.Contains("source") || source.Contains("insufficient_sources") || source.Contains("quality"))
        {
            return "source_expansion";
        }

        if (source.Contains("validation") || source.Contains("oos") || source.Contains("revalidation") || source.Contains("re-") || title.Contains("valid"))
        {
            return "schedule_revalidation";
        }

        if (source.Contains("contradiction") || title.Contains("widerspruch"))
        {
            return "contradiction_analysis";
        }

        return "systempflege";
    }

    private static string WorkAreaTitle(string areaId)
    {
        return areaId switch
        {
            "gather_more_evidence" => "Evidenz sammeln",
            "source_expansion" => "Quellen erweitern",
            "schedule_revalidation" => "Re-Validierung",
            "contradiction_analysis" => "Widersprüche prüfen",
            "systempflege" => "Systempflege",
            _ => "Verbesserungen",
        };
    }

    private static int WorkAreaOrder(string areaId)
    {
        return areaId switch
        {
            "gather_more_evidence" => 0,
            "source_expansion" => 1,
            "schedule_revalidation" => 2,
            "contradiction_analysis" => 3,
            "systempflege" => 4,
            _ => 5,
        };
    }

    private static string NextActionFromWorkArea(string areaId)
    {
        return areaId switch
        {
            "gather_more_evidence" => "Mehr Evidenz sammeln",
            "source_expansion" => "Quellen erweitern",
            "schedule_revalidation" => "Re-Validierung planen",
            "contradiction_analysis" => "Widersprüche prüfen",
            "systempflege" => "Systempflege aktualisieren",
            _ => "Verbesserung fortsetzen",
        };
    }

    private static string NextActionFromGroup(string actionType)
    {
        return actionType switch
        {
            "gather_more_evidence" => "Mehr Evidenz sammeln",
            "source_expansion" => "Quellen erweitern",
            "schedule_revalidation" => "Re-Validierung planen",
            "contradiction_analysis" => "Widerspruchsanalyse ausführen",
            "validation_queue_repair" => "Validation Queue prüfen/reparieren",
            "cleanup_plan_update" => "Cleanup-Plan aktualisieren",
            "robustness_planning" => "Research-/Robustness-Lauf planen",
            "audit_followup" => "Audit weiterverfolgen",
            _ => "Verbesserung weiterverfolgen",
        };
    }

    private static string GroupStatus(IEnumerable<AutonomousImprovementTask> group)
    {
        var items = group.ToList();
        var completed = items.Count(task => task.Status.Equals("executed", StringComparison.OrdinalIgnoreCase));
        var failed = items.Count(task => task.Status.Equals("failed", StringComparison.OrdinalIgnoreCase));

        if (failed > 0)
        {
            return "failed";
        }

        if (completed > 0 && completed >= items.Count)
        {
            return "completed";
        }

        if (completed > 0)
        {
            return "in_progress";
        }

        return "open";
    }

    private static AutonomousImprovementTask NewTask(
        string sourceWarning,
        string title,
        string domain,
        string priority,
        string reason,
        string suggestedAction,
        bool requiresHumanReview,
        bool autoFixable,
        bool safeToExecute,
        string dueHint)
    {
        return new AutonomousImprovementTask(
            TaskId: $"improvement_{sourceWarning}",
            SourceWarning: sourceWarning,
            Title: title,
            Domain: domain,
            Priority: priority,
            Reason: reason,
            SuggestedAction: suggestedAction,
            Status: "open",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            DueHint: dueHint,
            RequiresHumanReview: requiresHumanReview,
            AutoFixable: autoFixable,
            SafeToExecute: safeToExecute);
    }

    private static string HighestPriority(IReadOnlyList<AutonomousImprovementTask> tasks)
    {
        return tasks.Count == 0
            ? "niedrig"
            : tasks.OrderBy(task => PriorityRank(task.Priority)).First().Priority;
    }

    private static int PriorityRank(string priority) => StringComparer.OrdinalIgnoreCase.Equals(priority, "high")
        ? 0
        : StringComparer.OrdinalIgnoreCase.Equals(priority, "medium")
            ? 1
            : 2;

    private static string BuildMarkdown(AutonomousImprovementQueueReport report)
    {
        var lines = new List<string>
        {
            "# Autonomous Improvement Queue",
            string.Empty,
            $"- Updated UTC: {report.UpdatedAtUtc:O}",
            $"- Aktive Verbesserungen: {report.ActiveImprovements}",
            $"- Arbeitsbereiche: {report.GroupedImprovementAreas.Count}",
            $"- Höchste Priorität: {report.HighestPriority}",
            $"- Hermes kann selbst bearbeiten: {report.HermesCanHandle}",
            $"- Frank muss prüfen: {report.FrankItems}",
            string.Empty,
            "## Arbeitsbereiche",
        };

        lines.AddRange(report.GroupedImprovementAreas.Count == 0
            ? ["- keine"]
            : report.GroupedImprovementAreas.Select(group => $"- {group.GroupTitle}: {group.ItemCount} [{group.Domain}/{group.Priority}] -> {group.NextAction}"));
        lines.Add(string.Empty);
        lines.Add("## Aufgaben");
        lines.AddRange(report.Tasks.Count == 0
            ? ["- keine"]
            : report.Tasks.Select(task => $"- {task.Title} [{task.Domain}/{task.Priority}] -> {task.SuggestedAction}"));
        return string.Join(Environment.NewLine, lines);
    }

    private void WriteQueueOutputs(AutonomousImprovementQueueReport report, AutonomousImprovementQueueSummaryReport summary, AutonomousImprovementWorkAreasReport workAreas)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var summaryJson = JsonSerializer.Serialize(summary, JsonDefaults.WriteOptions);
        var workAreasJson = JsonSerializer.Serialize(workAreas, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);

        TryWriteCopies(Root, QueuePath, MarkdownPath, json, markdown);
        TryWriteCopies(Root, SummaryPath, Path.Combine(Root, "autonomous_improvement_queue_summary.md"), summaryJson, markdown);
        TryWriteCopies(Root, WorkAreasPath, Path.Combine(Root, "autonomous_improvement_work_areas.md"), workAreasJson, BuildWorkAreasMarkdown(workAreas));
    }

    private static void TryWriteCopies(string root, string jsonPath, string markdownPath, string json, string markdown)
    {
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(jsonPath, json);
            File.WriteAllText(markdownPath, markdown);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "autonomous_improvement_queue");
            Directory.CreateDirectory(fallbackRoot);
            var fileName = Path.GetFileName(jsonPath);
            File.WriteAllText(Path.Combine(fallbackRoot, fileName), json);
            File.WriteAllText(Path.Combine(fallbackRoot, Path.GetFileName(markdownPath)), markdown);
        }
    }

    private static string BuildWorkAreasMarkdown(AutonomousImprovementWorkAreasReport report)
    {
        var lines = new List<string>
        {
            "# Autonomous Improvement Work Areas",
            string.Empty,
            $"- Updated UTC: {report.UpdatedAtUtc:O}",
            $"- Aktive Bereiche: {report.ActiveAreas}",
            $"- Aktive Items: {report.ActiveItems}",
            $"- Frank nötig: {report.FrankItems}",
            string.Empty,
            "## Bereiche",
        };

        lines.AddRange(report.WorkAreas.Select(area => $"- {area.AreaTitle}: {area.ItemCount} [{area.Status}] -> {area.NextAction}"));
        return string.Join(Environment.NewLine, lines);
    }
}
