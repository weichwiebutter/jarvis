using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record EvidenceAutoLoopTask(
    string TaskId,
    string ReviewId,
    string KnowledgeItemId,
    string Title,
    string Domain,
    string Priority,
    string ActionType,
    string Status,
    string Reason,
    string SuggestedAction,
    bool SafeToExecute,
    bool RequiresHumanReview,
    DateTimeOffset CreatedAtUtc);

public sealed record EvidenceAutoLoopDomainSummary(
    string Domain,
    int ReviewCount,
    int EvidenceTasks,
    int ValidationTasks,
    string HighestPriority,
    string Status,
    string NextAction);

public sealed record EvidenceAutoLoopReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int ReviewCount,
    int MoreEvidenceReviews,
    int PlannedTasks,
    int TradingTasks,
    int DocumentationTasks,
    int ValidationTasks,
    int EvidenceTasks,
    int FrankRequired,
    IReadOnlyList<EvidenceAutoLoopDomainSummary> DomainSummaries,
    IReadOnlyList<EvidenceAutoLoopTask> PlannedTasksList,
    string NextAction,
    string SchedulerStatus,
    bool SchedulerConfigured,
    bool SchedulerEnabled,
    string? LastRunUtc,
    string? NextRunUtc,
    string NextRunHint,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class EvidenceAutoLoopService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public EvidenceAutoLoopService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "evidence_auto_loop");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "evidence_auto_loop.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "evidence_auto_loop.md");

    public EvidenceAutoLoopReport Run()
    {
        var (reportPath, markdownPath, root) = ResolveOutputPaths();
        _resolvedReportPath = reportPath;
        _resolvedMarkdownPath = markdownPath;

        var scheduler = new HermesInternalScheduler(_storagePaths, Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", "config", "schedules.json"));
        var schedulerConfig = scheduler.LoadConfig();
        var timeControl = scheduler.GetTimeControlStatus();
        var decisionAssistantService = new ReviewDecisionAssistantService(_storagePaths);
        var decisionAssistant = decisionAssistantService.Load() ?? decisionAssistantService.Run();
        var pendingEvidenceReviews = decisionAssistant.Entries
            .Where(entry => entry.RecommendationKey.Equals("more_evidence", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.Priority == "hoch" ? 3 : entry.Priority == "mittel" ? 2 : 1)
            .ThenByDescending(entry => entry.TrustBefore)
            .ThenBy(entry => entry.Domain, StringComparer.Ordinal)
            .ThenBy(entry => entry.Title, StringComparer.Ordinal)
            .ToList();

        var plannedTasks = new List<EvidenceAutoLoopTask>();
        foreach (var review in pendingEvidenceReviews)
        {
            plannedTasks.AddRange(BuildTasksForReview(review));
        }

        var maxTasksPerRun = Math.Max(1, schedulerConfig.EvidenceAutoLoopMaxTasksPerRun);
        if (plannedTasks.Count > maxTasksPerRun)
        {
            plannedTasks = plannedTasks.Take(maxTasksPerRun).ToList();
        }

        var domains = plannedTasks
            .GroupBy(task => task.Domain, StringComparer.OrdinalIgnoreCase)
            .Select(group => new EvidenceAutoLoopDomainSummary(
                Domain: group.Key,
                ReviewCount: pendingEvidenceReviews.Count(review => review.Domain.Equals(group.Key, StringComparison.OrdinalIgnoreCase)),
                EvidenceTasks: group.Count(task => task.ActionType is "collect_evidence" or "source_check"),
                ValidationTasks: group.Count(task => task.ActionType is "validate_knowledge_items" or "run_oos_validation" or "run_forward_validation"),
                HighestPriority: HighestPriority(group),
                Status: "geplant",
                NextAction: group.OrderBy(task => task.Domain == "trading" ? 0 : 1).ThenByDescending(task => PriorityRank(task.Priority)).First().SuggestedAction))
            .OrderBy(group => DomainRank(group.Domain))
            .ToList();

        var tradingReviews = pendingEvidenceReviews.Count(review => NormalizeDomain(review.Domain) == "trading");
        var documentationReviews = pendingEvidenceReviews.Count(review => NormalizeDomain(review.Domain) == "documentation");
        var schedulerConfigured = schedulerConfig.Jobs.Any(job => job.JobId.Equals("evidence_auto_loop", StringComparison.OrdinalIgnoreCase))
            || schedulerConfig.EvidenceAutoLoopEnabled;
        var schedulerEnabled = schedulerConfig.EvidenceAutoLoopEnabled
            && schedulerConfig.Jobs.Any(job => job.JobId.Equals("evidence_auto_loop", StringComparison.OrdinalIgnoreCase) && job.Enabled)
            && (timeControl.LearningWindow.ActiveNow || timeControl.NightlyWindow.ActiveNow);
        var lastRunUtc = decisionAssistant.UpdatedAtUtc.ToString("O");
        var nextRunUtc = schedulerConfig.EvidenceAutoLoopNextRunUtc?.ToString("O")
            ?? (schedulerEnabled
                ? DateTimeOffset.UtcNow.ToString("O")
                : schedulerConfig.EvidenceAutoLoopEnabled
                    ? CalculateNextWindowStartUtc(timeControl, schedulerConfig.EvidenceAutoLoopRunOnlyInLearningWindow)?.ToString("O")
                    : null);
        var nextRunHint = BuildNextRunHint(schedulerConfig.EvidenceAutoLoopEnabled, schedulerEnabled, schedulerConfig.EvidenceAutoLoopRunOnlyInLearningWindow, nextRunUtc is not null);

        var report = new EvidenceAutoLoopReport(
            ReportVersion: "evidence_auto_loop_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ReviewCount: decisionAssistant.ReviewCount,
            MoreEvidenceReviews: pendingEvidenceReviews.Count,
            PlannedTasks: plannedTasks.Count,
            TradingTasks: plannedTasks.Count(task => NormalizeDomain(task.Domain) == "trading"),
            DocumentationTasks: plannedTasks.Count(task => NormalizeDomain(task.Domain) == "documentation"),
            ValidationTasks: plannedTasks.Count(task => task.ActionType is "validate_knowledge_items" or "run_oos_validation" or "run_forward_validation"),
            EvidenceTasks: plannedTasks.Count(task => task.ActionType is "collect_evidence" or "source_check"),
            FrankRequired: 0,
            DomainSummaries: domains,
            PlannedTasksList: plannedTasks,
            NextAction: pendingEvidenceReviews.Count == 0 ? "Keine Aktion erforderlich." : "Hermes plant weitere Evidenzläufe.",
            SchedulerStatus: schedulerConfigured ? (schedulerEnabled ? "enabled" : "configured") : "disabled",
            SchedulerConfigured: schedulerConfigured,
            SchedulerEnabled: schedulerEnabled,
            LastRunUtc: lastRunUtc,
            NextRunUtc: nextRunUtc,
            NextRunHint: nextRunHint,
            Warnings: pendingEvidenceReviews.Count == 0 ? ["evidence_auto_loop_no_more_evidence_reviews"] : [],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteTextWithFallback(reportPath, markdownPath, root, report);
        DateTimeOffset? parsedNextRun = null;
        if (!string.IsNullOrWhiteSpace(nextRunUtc) && DateTimeOffset.TryParse(nextRunUtc, out var parsedNextRunValue))
        {
            parsedNextRun = parsedNextRunValue;
        }

        scheduler.UpdateEvidenceAutoLoopRunState(report.UpdatedAtUtc, parsedNextRun);
        return report;
    }

    public EvidenceAutoLoopState GetRuntimeState()
    {
        var scheduler = new HermesInternalScheduler(_storagePaths, Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", "config", "schedules.json"));
        var config = scheduler.LoadConfig();
        var timeControl = scheduler.GetTimeControlStatus();
        var enabled = config.EvidenceAutoLoopEnabled
            && config.Jobs.Any(job => job.JobId.Equals("evidence_auto_loop", StringComparison.OrdinalIgnoreCase) && job.Enabled);
        var active = enabled && (timeControl.LearningWindow.ActiveNow || timeControl.NightlyWindow.ActiveNow);
        var mode = !enabled ? "deaktiviert"
            : active ? "läuft"
            : "wartet auf Lernfenster";
        var nextRunUtc = config.EvidenceAutoLoopNextRunUtc
            ?? (enabled ? CalculateNextWindowStartUtc(timeControl, config.EvidenceAutoLoopRunOnlyInLearningWindow) : null);
        var nextRunHint = BuildNextRunHint(config.EvidenceAutoLoopEnabled, active, config.EvidenceAutoLoopRunOnlyInLearningWindow, nextRunUtc is not null);

        return new EvidenceAutoLoopState(
            Configured: config.Jobs.Any(job => job.JobId.Equals("evidence_auto_loop", StringComparison.OrdinalIgnoreCase)) || config.EvidenceAutoLoopEnabled,
            Enabled: enabled,
            Active: active,
            Mode: mode,
            NextRunUtc: nextRunUtc,
            NextRunHint: nextRunHint,
            LastRunUtc: config.EvidenceAutoLoopLastRunUtc);
    }

    private static string BuildNextRunHint(bool enabled, bool active, bool learningOnly, bool hasNextRun)
    {
        if (!enabled)
        {
            return "Evidence Auto-Loop ist deaktiviert.";
        }

        if (active)
        {
            return "Aktiviert – wartet auf Ausführung oder läuft.";
        }

        if (!hasNextRun)
        {
            return "Nächster Lauf wird beim Scheduler-Lauf berechnet.";
        }

        return learningOnly
            ? "Aktiviert – wartet auf Lernfenster."
            : "Aktiviert – wartet auf Lern- oder Nightly-Fenster.";
    }

    private static DateTimeOffset? CalculateNextWindowStartUtc(ScheduleTimeControlStatus timeControl, bool learningOnly)
    {
        var currentLocal = timeControl.CurrentLocal;
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeControl.TimeZone);
        if (TryNextWindowStart(currentLocal, timeControl.LearningWindow, zone, out var learningNext))
        {
            if (learningOnly)
            {
                return learningNext;
            }

            if (TryNextWindowStart(currentLocal, timeControl.NightlyWindow, zone, out var nightlyNext))
            {
                return learningNext <= nightlyNext ? learningNext : nightlyNext;
            }

            return learningNext;
        }

        return TryNextWindowStart(currentLocal, timeControl.NightlyWindow, zone, out var nightly) ? nightly : null;
    }

    private static bool TryNextWindowStart(DateTimeOffset currentLocal, SchedulerWindowStatus window, TimeZoneInfo zone, out DateTimeOffset nextStartUtc)
    {
        nextStartUtc = default;
        if (!window.Enabled)
        {
            return false;
        }

        var start = TimeOnly.Parse(window.Start);
        var localDate = DateOnly.FromDateTime(currentLocal.DateTime);
        var candidate = localDate.ToDateTime(start, DateTimeKind.Unspecified);
        var candidateOffset = new DateTimeOffset(candidate, currentLocal.Offset);
        if (candidateOffset > currentLocal)
        {
            nextStartUtc = TimeZoneInfo.ConvertTimeToUtc(candidateOffset.DateTime, zone);
            return true;
        }

        var tomorrow = localDate.AddDays(1).ToDateTime(start, DateTimeKind.Unspecified);
        nextStartUtc = TimeZoneInfo.ConvertTimeToUtc(tomorrow, zone);
        return true;
    }

    public EvidenceAutoLoopReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<EvidenceAutoLoopReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<EvidenceAutoLoopTask> BuildTasksForReview(ReviewDecisionAssistantEntry review)
    {
        var now = DateTimeOffset.UtcNow;
        var tasks = new List<EvidenceAutoLoopTask>();
        var domain = NormalizeDomain(review.Domain);

        void AddTask(string actionType, string title, string reason, string suggestedAction, bool validationTask)
        {
            tasks.Add(new EvidenceAutoLoopTask(
                TaskId: $"evidence_auto_loop_{actionType}_{review.ReviewId}_{Guid.NewGuid():N}",
                ReviewId: review.ReviewId,
                KnowledgeItemId: review.KnowledgeItemId,
                Title: review.Title,
                Domain: domain,
                Priority: review.Priority,
                ActionType: actionType,
                Status: "planned",
                Reason: reason,
                SuggestedAction: suggestedAction,
                SafeToExecute: true,
                RequiresHumanReview: false,
                CreatedAtUtc: now));
        }

        if (domain == "trading")
        {
            AddTask("collect_evidence", "Evidenz sammeln", "Trading-Review braucht mehr Evidenz.", "Sichere Trading-Evidenz sammeln", false);
            AddTask("validate_knowledge_items", "Knowledge Items validieren", "Trading-Review braucht zusätzliche Validierung.", "Trading-Wissen validieren", true);
            AddTask("run_oos_validation", "OOS-Validierung planen", "Trading-Review braucht zusätzliche OOS-Absicherung.", "OOS-/Walk-Forward-Plan vorbereiten", true);
            AddTask("run_forward_validation", "Forward-Validierung planen", "Trading-Review braucht zusätzliche Forward-Absicherung.", "Forward-Validierung vorbereiten", true);
            return tasks;
        }

        if (domain == "documentation")
        {
            AddTask("source_check", "Quellen prüfen", "Dokumentations-Review braucht Quellenabgleich.", "Sichere Quellen prüfen", false);
            AddTask("collect_evidence", "Evidenz sammeln", "Dokumentations-Review braucht ergänzende Evidenz.", "Zusätzliche Dokumentations-Evidenz sammeln", false);
            AddTask("validate_knowledge_items", "Knowledge Items validieren", "Dokumentations-Review braucht Validierung.", "Dokumentationswissen validieren", true);
            return tasks;
        }

        AddTask("collect_evidence", "Evidenz sammeln", "Review braucht weitere Evidenz.", "Sichere Evidenz sammeln", false);
        AddTask("validate_knowledge_items", "Knowledge Items validieren", "Review braucht Validierung.", "Knowledge-Validierung planen", true);
        return tasks;
    }

    private static string NormalizeDomain(string domain)
    {
        var lowered = (domain ?? string.Empty).ToLowerInvariant();
        return lowered switch
        {
            "trading" => "trading",
            "documentation" => "documentation",
            "research" => "research",
            "software" => "software",
            "process" => "process",
            _ => lowered
        };
    }

    private static int PriorityRank(string priority) =>
        priority switch
        {
            "hoch" => 3,
            "mittel" => 2,
            _ => 1
        };

    private static int DomainRank(string domain) =>
        NormalizeDomain(domain) switch
        {
            "trading" => 0,
            "documentation" => 1,
            "research" => 2,
            "software" => 3,
            "process" => 4,
            _ => 5
        };

    private static string HighestPriority(IEnumerable<EvidenceAutoLoopTask> tasks) =>
        tasks.Any(task => task.Domain == "trading") ? "hoch"
            : tasks.Any(task => task.Domain == "documentation") ? "mittel"
            : "niedrig";

    private (string reportPath, string markdownPath, string root) ResolveOutputPaths()
    {
        var roots = new[]
        {
            Path.Combine(_storagePaths.Root, "reports", "evidence_auto_loop"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".codex_artifacts", "reports", "evidence_auto_loop"),
        };

        foreach (var root in roots)
        {
            try
            {
                Directory.CreateDirectory(root);
                var reportPath = Path.Combine(root, "evidence_auto_loop.json");
                var markdownPath = Path.Combine(root, "evidence_auto_loop.md");
                return (reportPath, markdownPath, root);
            }
            catch
            {
            }
        }

        var fallbackRoot = roots.Last();
        return (Path.Combine(fallbackRoot, "evidence_auto_loop.json"), Path.Combine(fallbackRoot, "evidence_auto_loop.md"), fallbackRoot);
    }

    private static void WriteTextWithFallback(string reportPath, string markdownPath, string root, EvidenceAutoLoopReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(reportPath, json);
            File.WriteAllText(markdownPath, markdown);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "evidence_auto_loop");
            Directory.CreateDirectory(fallbackRoot);
            File.WriteAllText(Path.Combine(fallbackRoot, "evidence_auto_loop.json"), json);
            File.WriteAllText(Path.Combine(fallbackRoot, "evidence_auto_loop.md"), markdown);
        }
    }

    private static string BuildMarkdown(EvidenceAutoLoopReport report)
    {
        var lines = new List<string>
        {
            "# Evidence Auto Loop",
            string.Empty,
            $"- Updated UTC: {report.UpdatedAtUtc:O}",
            $"- Reviews gelesen: {report.ReviewCount}",
            $"- Mehr-Evidenz-Reviews: {report.MoreEvidenceReviews}",
            $"- Geplante Tasks: {report.PlannedTasks}",
            $"- Trading Tasks: {report.TradingTasks}",
            $"- Documentation Tasks: {report.DocumentationTasks}",
            $"- Validation Tasks: {report.ValidationTasks}",
            $"- Evidence Tasks: {report.EvidenceTasks}",
            $"- Frank nötig: {(report.FrankRequired > 0 ? "ja" : "nein")}",
            string.Empty,
            report.NextAction,
            string.Empty,
            "## Geplante Tasks",
        };
        lines.AddRange(report.PlannedTasksList.Count == 0
            ? ["- keine"]
            : report.PlannedTasksList.Take(50).Select(task => $"- {task.Domain}: {task.ActionType} · {task.SuggestedAction}"));
        return string.Join(Environment.NewLine, lines);
    }
}

public sealed record EvidenceAutoLoopState(
    bool Configured,
    bool Enabled,
    bool Active,
    string Mode,
    DateTimeOffset? NextRunUtc,
    string NextRunHint,
    DateTimeOffset? LastRunUtc);
