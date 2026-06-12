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
    IReadOnlyList<AutonomousImprovementTask> Tasks,
    IReadOnlyList<string> SourceWarnings,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string AuditPath,
    string QueuePath,
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

    public string MarkdownPath => Path.Combine(Root, "autonomous_improvement_queue.md");

    public AutonomousImprovementQueueReport Generate()
    {
        Directory.CreateDirectory(Root);
        var audit = new KnowledgeValidationAuditService(_storagePaths).Run();
        var warnings = audit.Warnings
            .Concat(new[] { "storage_cleanup_candidates" })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var tasks = BuildTasks(warnings, audit);
        var report = new AutonomousImprovementQueueReport(
            ReportVersion: "autonomous_improvement_queue_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ActiveImprovements: tasks.Count(task => task.Status.Equals("open", StringComparison.OrdinalIgnoreCase)),
            HighestPriority: HighestPriority(tasks),
            HermesCanHandle: tasks.Count(task => !task.RequiresHumanReview),
            FrankItems: tasks.Count(task => task.RequiresHumanReview),
            Tasks: tasks,
            SourceWarnings: warnings,
            Warnings: tasks.Count == 0 ? ["autonomous_improvement_queue_empty"] : [],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            AuditPath: audit.AuditPath,
            QueuePath: QueuePath,
            MarkdownPath: MarkdownPath);

        File.WriteAllText(QueuePath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
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
            return JsonSerializer.Deserialize<AutonomousImprovementQueueReport>(
                File.ReadAllText(QueuePath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<AutonomousImprovementTask> BuildTasks(IReadOnlyList<string> warnings, KnowledgeValidationAuditReport audit)
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

        return tasks
            .OrderByDescending(task => PriorityRank(task.Priority))
            .ThenBy(task => task.TaskId, StringComparer.OrdinalIgnoreCase)
            .ToList();
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
            $"- Höchste Priorität: {report.HighestPriority}",
            $"- Hermes kann selbst bearbeiten: {report.HermesCanHandle}",
            $"- Frank muss prüfen: {report.FrankItems}",
            string.Empty,
            "## Aufgaben",
        };

        lines.AddRange(report.Tasks.Count == 0
            ? ["- keine"]
            : report.Tasks.Select(task => $"- {task.Title} [{task.Domain}/{task.Priority}] -> {task.SuggestedAction}"));
        return string.Join(Environment.NewLine, lines);
    }
}
