using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record AutonomousKnowledgeSupervisorMetricSnapshot(
    int TrustedKnowledge,
    int WeakKnowledge,
    int ContradictionCount,
    int ValidationPlansOpen,
    int ValidationTasksPending,
    int KnowledgeItemsNeedingOos,
    int InvalidValidationTasks,
    int PendingReviews,
    int DocumentationValidationPending,
    int SoftwareValidationPending,
    int ProcessValidationPending,
    int ResearchValidationPending,
    int CleanupCandidates,
    double AverageQualityScore,
    double AverageTrustScore,
    double EvidenceCoverage,
    double ValidationCoverage,
    string KnowledgeHealth,
    string DomainValidationHealth);

public sealed record AutonomousKnowledgeSupervisorBacklogItem(
    string BacklogClass,
    int Count,
    string Priority,
    string SelectedCommand,
    string Reason,
    bool RequiresHumanReview,
    bool SafeToExecute);

public sealed record AutonomousKnowledgeSupervisorActionResult(
    int ActionIndex,
    string BacklogClass,
    string SelectedCommand,
    string SelectedCommandResult,
    AutonomousKnowledgeSupervisorMetricSnapshot Before,
    AutonomousKnowledgeSupervisorMetricSnapshot After,
    IReadOnlyDictionary<string, int> Deltas,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath,
    DateTimeOffset ExecutedAtUtc);

public sealed record AutonomousKnowledgeSupervisorReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    string InitialMasterStatusPath,
    string? InitialMasterStatusSnapshotPath,
    string CurrentFocus,
    IReadOnlyList<AutonomousKnowledgeSupervisorBacklogItem> BacklogClasses,
    string SelectedBacklogClass,
    IReadOnlyList<AutonomousKnowledgeSupervisorActionResult> Actions,
    AutonomousKnowledgeSupervisorMetricSnapshot Before,
    AutonomousKnowledgeSupervisorMetricSnapshot After,
    IReadOnlyDictionary<string, int> MetricChanges,
    IReadOnlyList<string> Warnings,
    string Recommendation,
    string ReportPath,
    string MarkdownPath,
    bool ReadOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool ResearchOnly,
    bool HumanReviewRequired,
    bool Executed);

public sealed class AutonomousKnowledgeSupervisorService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public AutonomousKnowledgeSupervisorService(StoragePaths storagePaths, string? runtimeRoot = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot ?? Directory.GetCurrentDirectory();
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "autonomous_knowledge_supervisor");
    public string ReportPath => Path.Combine(Root, "autonomous_knowledge_supervisor_report.json");
    public string MarkdownPath => Path.Combine(Root, "autonomous_knowledge_supervisor_report.md");

    public AutonomousKnowledgeSupervisorReport Run(int maxActions = 1, bool execute = false)
    {
        Directory.CreateDirectory(Root);
        var writer = new MasterStatusWriter(new MasterStatusService(_storagePaths, _runtimeRoot));
        var initialSnapshot = LoadMasterSnapshot(writer);
        var before = BuildMetricSnapshot(initialSnapshot);
        var actions = new List<AutonomousKnowledgeSupervisorActionResult>();
        var warnings = new List<string>();
        var actionBudget = Math.Clamp(maxActions, 1, 3);
        var currentSnapshot = initialSnapshot;
        for (var index = 0; index < actionBudget; index++)
        {
            var backlogClasses = BuildBacklogClasses(currentSnapshot);
            var selected = backlogClasses.FirstOrDefault(item => item.Count > 0);
            if (selected is null)
            {
                break;
            }

            if (!execute)
            {
                break;
            }

            var action = ExecuteAction(selected, index + 1, writer, warnings);
            actions.Add(action);
            currentSnapshot = LoadMasterSnapshot(writer);
        }

        var afterSnapshot = execute && actions.Count > 0 ? currentSnapshot : initialSnapshot;
        var after = BuildMetricSnapshot(afterSnapshot);
        var metricChanges = BuildMetricChanges(before, after);
        var backlogAfter = BuildBacklogClasses(afterSnapshot);
        var selectedBacklogClass = backlogAfter.FirstOrDefault(item => item.Count > 0)?.BacklogClass ?? "none";
        var recommendation = BuildRecommendation(backlogAfter);

        var report = new AutonomousKnowledgeSupervisorReport(
            ReportVersion: "autonomous_knowledge_supervisor_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: execute ? (actions.Count > 0 ? "executed" : "idle") : "planned",
            InitialMasterStatusPath: Path.Combine(_storagePaths.Root, "reports", "master-status", "master_status.json"),
            InitialMasterStatusSnapshotPath: writer.SnapshotPath,
            CurrentFocus: selectedBacklogClass,
            BacklogClasses: backlogAfter,
            SelectedBacklogClass: selectedBacklogClass,
            Actions: actions,
            Before: before,
            After: after,
            MetricChanges: metricChanges,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Recommendation: recommendation,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            ReadOnly: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            ResearchOnly: true,
            HumanReviewRequired: true,
            Executed: execute);

        WriteReport(report);
        return report;
    }

    public AutonomousKnowledgeSupervisorReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AutonomousKnowledgeSupervisorReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private MasterStatusSnapshot LoadMasterSnapshot(MasterStatusWriter writer)
    {
        return writer.LoadSnapshot() ?? new MasterStatusService(_storagePaths, _runtimeRoot).BuildSnapshot();
    }

    private IReadOnlyList<AutonomousKnowledgeSupervisorBacklogItem> BuildBacklogClasses(MasterStatusSnapshot snapshot)
    {
        var items = new List<AutonomousKnowledgeSupervisorBacklogItem>();

        var contradictionCount = snapshot.ContradictionCount;
        if (contradictionCount > 0)
        {
            items.Add(new AutonomousKnowledgeSupervisorBacklogItem(
                "blocking_contradiction",
                contradictionCount,
                "high",
                "knowledge-state-consistency-repair",
                "Konflikte blockieren Trust und sollten zuerst bereinigt werden.",
                RequiresHumanReview: true,
                SafeToExecute: true));
        }

        var validationGap = snapshot.ValidationPlansOpen + snapshot.ValidationTasksPending + snapshot.KnowledgeItemsNeedingOos + snapshot.InvalidValidationTasks;
        if (validationGap > 0)
        {
            items.Add(new AutonomousKnowledgeSupervisorBacklogItem(
                "validation_gap",
                validationGap,
                validationGap > 100 ? "high" : "medium",
                "validation-backlog-executor",
                "Offene Validation-Pläne und Aufgaben sollten zuerst abgearbeitet werden.",
                RequiresHumanReview: false,
                SafeToExecute: true));
        }

        var sourceGap = snapshot.KnowledgeItemsNeedingSourceCheck;
        if (sourceGap == 0 && snapshot.EvidenceCoverage < 0.75)
        {
            sourceGap = (int)Math.Ceiling((1 - snapshot.EvidenceCoverage) * 100);
        }
        if (sourceGap > 0)
        {
            items.Add(new AutonomousKnowledgeSupervisorBacklogItem(
                "source_gap",
                sourceGap,
                sourceGap > 50 ? "high" : "medium",
                "known-article-seed-fetch",
                "Es fehlen zweite Quellen oder Source Checks.",
                RequiresHumanReview: false,
                SafeToExecute: true));
        }

        if (snapshot.DocumentationValidationPending > 0)
        {
            items.Add(new AutonomousKnowledgeSupervisorBacklogItem(
                "documentation_validation_pending",
                snapshot.DocumentationValidationPending,
                snapshot.DocumentationValidationPending > 50 ? "medium" : "low",
                "validation-evidence",
                "Dokumentationswissen braucht noch Validierung.",
                RequiresHumanReview: false,
                SafeToExecute: true));
        }

        if (snapshot.SoftwareValidationPending > 0)
        {
            items.Add(new AutonomousKnowledgeSupervisorBacklogItem(
                "software_validation_pending",
                snapshot.SoftwareValidationPending,
                snapshot.SoftwareValidationPending > 50 ? "medium" : "low",
                "validation-evidence",
                "Softwarewissen braucht noch Validierung.",
                RequiresHumanReview: false,
                SafeToExecute: true));
        }

        if (snapshot.CleanupCandidates > 0)
        {
            items.Add(new AutonomousKnowledgeSupervisorBacklogItem(
                "storage_cleanup_candidates",
                snapshot.CleanupCandidates,
                snapshot.CleanupCandidates > 1000 ? "medium" : "low",
                "storage-hygiene",
                "Es existieren wartungsrelevante Cleanup-Kandidaten.",
                RequiresHumanReview: false,
                SafeToExecute: true));
        }

        return items
            .OrderByDescending(item => item.BacklogClass switch
            {
                "blocking_contradiction" => 600,
                "validation_gap" => 500,
                "source_gap" => 400,
                "documentation_validation_pending" => 300,
                "software_validation_pending" => 200,
                "storage_cleanup_candidates" => 100,
                _ => 0
            })
            .ThenByDescending(item => item.Count)
            .ToList();
    }

    private AutonomousKnowledgeSupervisorActionResult ExecuteAction(
        AutonomousKnowledgeSupervisorBacklogItem backlog,
        int actionIndex,
        MasterStatusWriter writer,
        List<string> warnings)
    {
        var beforeSnapshot = LoadMasterSnapshot(writer);
        var before = BuildMetricSnapshot(beforeSnapshot);
        var executedAt = DateTimeOffset.UtcNow;
        string resultLabel;
        string reportPath;

        switch (backlog.BacklogClass)
        {
            case "blocking_contradiction":
            {
                var service = new KnowledgeStateConsistencyService(_storagePaths, _runtimeRoot);
                var report = service.Run(apply: true, dryRun: false);
                resultLabel = report.Applied ? "applied" : "completed_no_changes";
                reportPath = service.ReportPath;
                warnings.AddRange(report.Warnings);
                break;
            }
            case "validation_gap":
            {
                var service = new ValidationBacklogExecutorService(_storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
                var report = service.Execute(20);
                resultLabel = report.ExecutedSteps > 0 ? "executed" : "completed_no_changes";
                reportPath = service.ReportPath;
                warnings.AddRange(report.Warnings);
                break;
            }
            case "source_gap":
            {
                var service = new KnownArticleSeedCatalogService(_storagePaths, _runtimeRoot);
                var report = service.Run(maxItems: 20, dryRun: false, maxFetchSeconds: 60);
                resultLabel = report.FetchedCandidates > 0 ? "executed" : "completed_no_changes";
                reportPath = service.ReportPath;
                warnings.AddRange(report.Warnings);
                break;
            }
            case "documentation_validation_pending":
            case "software_validation_pending":
            {
                var service = new ValidationEvidencePipelineService(_storagePaths);
                var report = service.Run(apply: true, dryRun: false);
                resultLabel = report.Status.Equals("applied", StringComparison.OrdinalIgnoreCase) ? "executed" : "completed_no_changes";
                reportPath = service.ReportPath;
                warnings.AddRange(report.Warnings);
                break;
            }
            case "storage_cleanup_candidates":
            {
                var service = new StorageHygieneService(_storagePaths);
                var report = service.ApplySafeCleanup();
                resultLabel = report.FilesDeleted > 0 ? "executed" : "completed_no_changes";
                reportPath = service.CleanupReportPath;
                warnings.AddRange(report.SkippedPaths);
                break;
            }
            default:
                resultLabel = "skipped_unknown_backlog";
                reportPath = string.Empty;
                warnings.Add($"unknown_backlog_class:{backlog.BacklogClass}");
                break;
        }

        var refreshedQuality = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        writer.WriteKnowledgeOnlySnapshot(refreshedQuality);
        var afterSnapshot = LoadMasterSnapshot(writer);
        var after = BuildMetricSnapshot(afterSnapshot);
        var deltas = BuildMetricChanges(before, after);

        return new AutonomousKnowledgeSupervisorActionResult(
            ActionIndex: actionIndex,
            BacklogClass: backlog.BacklogClass,
            SelectedCommand: backlog.SelectedCommand,
            SelectedCommandResult: resultLabel,
            Before: before,
            After: after,
            Deltas: deltas,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReportPath: reportPath,
            MarkdownPath: reportPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                ? Path.ChangeExtension(reportPath, ".md")
                : string.Empty,
            ExecutedAtUtc: executedAt);
    }

    private static AutonomousKnowledgeSupervisorMetricSnapshot BuildMetricSnapshot(MasterStatusSnapshot snapshot)
    {
        return new AutonomousKnowledgeSupervisorMetricSnapshot(
            TrustedKnowledge: snapshot.TrustedKnowledge,
            WeakKnowledge: snapshot.WeakKnowledge,
            ContradictionCount: snapshot.ContradictionCount,
            ValidationPlansOpen: snapshot.ValidationPlansOpen,
            ValidationTasksPending: snapshot.ValidationTasksPending,
            KnowledgeItemsNeedingOos: snapshot.KnowledgeItemsNeedingOos,
            InvalidValidationTasks: snapshot.InvalidValidationTasks,
            PendingReviews: snapshot.PendingReviews,
            DocumentationValidationPending: snapshot.DocumentationValidationPending,
            SoftwareValidationPending: snapshot.SoftwareValidationPending,
            ProcessValidationPending: snapshot.ProcessValidationPending,
            ResearchValidationPending: snapshot.ResearchValidationPending,
            CleanupCandidates: snapshot.CleanupCandidates,
            AverageQualityScore: snapshot.AverageQualityScore,
            AverageTrustScore: snapshot.AverageTrustScore,
            EvidenceCoverage: snapshot.EvidenceCoverage,
            ValidationCoverage: snapshot.ValidationCoverage,
            KnowledgeHealth: snapshot.KnowledgeHealth,
            DomainValidationHealth: snapshot.DomainValidationHealth);
    }

    private static IReadOnlyDictionary<string, int> BuildMetricChanges(AutonomousKnowledgeSupervisorMetricSnapshot before, AutonomousKnowledgeSupervisorMetricSnapshot after)
    {
        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["trusted_knowledge"] = after.TrustedKnowledge - before.TrustedKnowledge,
            ["contradiction_count"] = after.ContradictionCount - before.ContradictionCount,
            ["validation_plans_open"] = after.ValidationPlansOpen - before.ValidationPlansOpen,
            ["weak_knowledge"] = after.WeakKnowledge - before.WeakKnowledge,
            ["pending_reviews"] = after.PendingReviews - before.PendingReviews,
            ["documentation_validation_pending"] = after.DocumentationValidationPending - before.DocumentationValidationPending,
            ["software_validation_pending"] = after.SoftwareValidationPending - before.SoftwareValidationPending,
            ["process_validation_pending"] = after.ProcessValidationPending - before.ProcessValidationPending,
            ["research_validation_pending"] = after.ResearchValidationPending - before.ResearchValidationPending,
            ["cleanup_candidates"] = after.CleanupCandidates - before.CleanupCandidates
        };
    }

    private static string BuildRecommendation(IReadOnlyList<AutonomousKnowledgeSupervisorBacklogItem> backlog)
    {
        var next = backlog.FirstOrDefault(item => item.Count > 0);
        return next is null
            ? "no_action_needed"
            : $"run {next.SelectedCommand} for {next.BacklogClass}";
    }

    private static void WriteReport(AutonomousKnowledgeSupervisorReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        File.WriteAllText(report.ReportPath, json);
        File.WriteAllText(report.MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(AutonomousKnowledgeSupervisorReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Autonomous Knowledge Supervisor");
        sb.AppendLine();
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- selected_backlog_class: {report.SelectedBacklogClass}");
        sb.AppendLine($"- recommendation: {report.Recommendation}");
        sb.AppendLine();
        sb.AppendLine("## Before");
        AppendMetrics(sb, report.Before);
        sb.AppendLine();
        sb.AppendLine("## After");
        AppendMetrics(sb, report.After);
        sb.AppendLine();
        sb.AppendLine("## Actions");
        foreach (var action in report.Actions)
        {
            sb.AppendLine($"### Action {action.ActionIndex}: {action.BacklogClass}");
            sb.AppendLine($"- command: {action.SelectedCommand}");
            sb.AppendLine($"- result: {action.SelectedCommandResult}");
            sb.AppendLine($"- executed_at_utc: {action.ExecutedAtUtc:O}");
            sb.AppendLine($"- before_trusted_knowledge: {action.Before.TrustedKnowledge}");
            sb.AppendLine($"- after_trusted_knowledge: {action.After.TrustedKnowledge}");
            sb.AppendLine($"- before_contradiction_count: {action.Before.ContradictionCount}");
            sb.AppendLine($"- after_contradiction_count: {action.After.ContradictionCount}");
            sb.AppendLine($"- before_validation_plans_open: {action.Before.ValidationPlansOpen}");
            sb.AppendLine($"- after_validation_plans_open: {action.After.ValidationPlansOpen}");
            sb.AppendLine($"- before_weak_knowledge: {action.Before.WeakKnowledge}");
            sb.AppendLine($"- after_weak_knowledge: {action.After.WeakKnowledge}");
            sb.AppendLine($"- before_pending_reviews: {action.Before.PendingReviews}");
            sb.AppendLine($"- after_pending_reviews: {action.After.PendingReviews}");
            sb.AppendLine($"- report_path: {action.ReportPath}");
            sb.AppendLine();
        }

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("## Backlog Classes");
        foreach (var backlog in report.BacklogClasses)
        {
            sb.AppendLine($"- {backlog.BacklogClass}: count={backlog.Count}; command={backlog.SelectedCommand}; reason={backlog.Reason}");
        }

        return sb.ToString();
    }

    private static void AppendMetrics(StringBuilder sb, AutonomousKnowledgeSupervisorMetricSnapshot metrics)
    {
        sb.AppendLine($"- trusted_knowledge: {metrics.TrustedKnowledge}");
        sb.AppendLine($"- weak_knowledge: {metrics.WeakKnowledge}");
        sb.AppendLine($"- contradiction_count: {metrics.ContradictionCount}");
        sb.AppendLine($"- validation_plans_open: {metrics.ValidationPlansOpen}");
        sb.AppendLine($"- validation_tasks_pending: {metrics.ValidationTasksPending}");
        sb.AppendLine($"- knowledge_items_needing_oos: {metrics.KnowledgeItemsNeedingOos}");
        sb.AppendLine($"- invalid_validation_tasks: {metrics.InvalidValidationTasks}");
        sb.AppendLine($"- pending_reviews: {metrics.PendingReviews}");
        sb.AppendLine($"- documentation_validation_pending: {metrics.DocumentationValidationPending}");
        sb.AppendLine($"- software_validation_pending: {metrics.SoftwareValidationPending}");
        sb.AppendLine($"- process_validation_pending: {metrics.ProcessValidationPending}");
        sb.AppendLine($"- research_validation_pending: {metrics.ResearchValidationPending}");
        sb.AppendLine($"- cleanup_candidates: {metrics.CleanupCandidates}");
        sb.AppendLine($"- average_quality_score: {metrics.AverageQualityScore:0.###}");
        sb.AppendLine($"- average_trust_score: {metrics.AverageTrustScore:0.###}");
        sb.AppendLine($"- evidence_coverage: {metrics.EvidenceCoverage:0.###}");
        sb.AppendLine($"- validation_coverage: {metrics.ValidationCoverage:0.###}");
        sb.AppendLine($"- knowledge_health: {metrics.KnowledgeHealth}");
        sb.AppendLine($"- domain_validation_health: {metrics.DomainValidationHealth}");
    }
}
