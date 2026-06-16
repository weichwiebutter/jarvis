using System.Text.Json;

namespace Hermes.Runtime;

public sealed record EvidenceTaskExecutionEntry(
    string TaskId,
    string ReviewId,
    string KnowledgeItemId,
    string Title,
    string Domain,
    string ActionType,
    string Status,
    string Result,
    bool Supported,
    int ExecutedCount,
    IReadOnlyList<string> OutputPaths,
    IReadOnlyList<string> Warnings,
    DateTimeOffset CreatedAtUtc);

public sealed record EvidenceTaskExecutionReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int TasksFound,
    int TasksExecuted,
    int TasksSkipped,
    int UnsupportedTasks,
    int EvidenceCollected,
    int ValidationTasksExecuted,
    int NeedsMoreEvidenceBefore,
    int NeedsMoreEvidenceAfter,
    int PendingReviewsBefore,
    int PendingReviewsAfter,
    int UpdatedKnowledgeItems,
    int UpdatedReviews,
    IReadOnlyList<string> Domains,
    IReadOnlyList<EvidenceTaskExecutionEntry> ExecutedTasks,
    IReadOnlyList<EvidenceTaskExecutionEntry> SkippedTasks,
    IReadOnlyList<string> Warnings,
    bool FrankActionRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string SourceReportPath,
    string QueuePath,
    string ReportPath,
    string MarkdownPath);

public sealed class EvidenceTaskExecutionService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;
    private string? _resolvedQueuePath;

    public EvidenceTaskExecutionService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "evidence_task_execution");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "evidence_task_execution.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "evidence_task_execution.md");

    public EvidenceTaskExecutionReport Run(int maxTasks = 72)
    {
        var (reportPath, markdownPath, root) = ResolveOutputPaths();
        _resolvedReportPath = reportPath;
        _resolvedMarkdownPath = markdownPath;

        var autoLoop = new EvidenceAutoLoopService(_storagePaths);
        var autoLoopReport = autoLoop.Load() ?? autoLoop.Run();
        var tasks = autoLoopReport.PlannedTasksList
            .Where(task => task.SafeToExecute && !task.RequiresHumanReview)
            .Take(Math.Clamp(maxTasks, 1, 500))
            .ToList();

        var humanReview = new HumanReviewWorkflow(_storagePaths);
        var beforeQueue = humanReview.LoadOrCreateQueue();
        var beforeNeedsMoreEvidence = beforeQueue.NeedsMoreEvidenceReviews;
        var beforePendingReviews = beforeQueue.PendingReviews;

        var executionEntries = new List<EvidenceTaskExecutionEntry>();
        var skippedEntries = new List<EvidenceTaskExecutionEntry>();
        var knowledgeValidation = new KnowledgeValidationStrategy(_storagePaths);
        var knowledgeExecutor = new KnowledgeValidationExecutor(_storagePaths);
        var qualityEngine = new KnowledgeQualityEngine(_storagePaths);
        var contradictionDetector = new ContradictionDetector(_storagePaths);
        var walkForward = new WalkForwardValidationService(_storagePaths);
        var MonteCarlo = new MonteCarloSimulationService(_storagePaths);
        var costStress = new CostStressTestService(_storagePaths);
        var domains = tasks.Select(task => task.Domain).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var group in tasks.GroupBy(task => $"{task.Domain}::{task.ActionType}", StringComparer.OrdinalIgnoreCase))
        {
            var entry = ExecuteTask(group.First(), group.Count(), knowledgeValidation, knowledgeExecutor, qualityEngine, contradictionDetector, walkForward, MonteCarlo, costStress);
            if (entry.Supported)
            {
                executionEntries.Add(entry);
            }
            else
            {
                skippedEntries.Add(entry);
            }
        }

        var queueAfter = RefreshEvidenceStatuses(humanReview.LoadOrCreateQueue());
        WriteQueue(queueAfter, humanReview);

        var afterQueue = humanReview.LoadOrCreateQueue();
        var afterNeedsMoreEvidence = afterQueue.NeedsMoreEvidenceReviews;
        var afterPendingReviews = afterQueue.PendingReviews;

        var report = new EvidenceTaskExecutionReport(
            ReportVersion: "evidence_task_execution_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TasksFound: tasks.Count,
            TasksExecuted: executionEntries.Sum(task => task.ExecutedCount),
            TasksSkipped: skippedEntries.Sum(task => task.ExecutedCount),
            UnsupportedTasks: skippedEntries.Where(task => !task.Supported).Sum(task => task.ExecutedCount),
            EvidenceCollected: executionEntries.Where(task => task.ActionType.Contains("evidence", StringComparison.OrdinalIgnoreCase) || task.ActionType.Contains("source", StringComparison.OrdinalIgnoreCase)).Sum(task => task.ExecutedCount),
            ValidationTasksExecuted: executionEntries.Where(task => task.ActionType.Contains("validation", StringComparison.OrdinalIgnoreCase) || task.ActionType.Contains("oos", StringComparison.OrdinalIgnoreCase) || task.ActionType.Contains("forward", StringComparison.OrdinalIgnoreCase)).Sum(task => task.ExecutedCount),
            NeedsMoreEvidenceBefore: beforeNeedsMoreEvidence,
            NeedsMoreEvidenceAfter: afterNeedsMoreEvidence,
            PendingReviewsBefore: beforePendingReviews,
            PendingReviewsAfter: afterPendingReviews,
            UpdatedKnowledgeItems: Math.Max(0, beforeNeedsMoreEvidence - afterNeedsMoreEvidence),
            UpdatedReviews: Math.Max(0, afterPendingReviews - beforePendingReviews),
            Domains: domains,
            ExecutedTasks: executionEntries,
            SkippedTasks: skippedEntries,
            Warnings: skippedEntries.Count == 0 ? [] : ["unsupported_evidence_tasks_skipped"],
            FrankActionRequired: afterPendingReviews > 0,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            SourceReportPath: autoLoop.ReportPath,
            QueuePath: autoLoop.ReportPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteTextWithFallback(reportPath, markdownPath, root, report);
        try
        {
            new MasterStatusWriter(new MasterStatusService(_storagePaths, Directory.GetCurrentDirectory())).WriteSnapshot();
        }
        catch
        {
        }
        return report;
    }

    public EvidenceTaskExecutionReport? Load()
    {
        var readablePath = ResolveReadableReportPath();
        _resolvedReportPath = readablePath;
        if (!File.Exists(readablePath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<EvidenceTaskExecutionReport>(File.ReadAllText(readablePath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private EvidenceTaskExecutionEntry ExecuteTask(
        EvidenceAutoLoopTask task,
        int taskCount,
        KnowledgeValidationStrategy validation,
        KnowledgeValidationExecutor executor,
        KnowledgeQualityEngine qualityEngine,
        ContradictionDetector contradictionDetector,
        WalkForwardValidationService walkForward,
        MonteCarloSimulationService monteCarlo,
        CostStressTestService costStress)
    {
        var now = DateTimeOffset.UtcNow;
        var warnings = new List<string>();
        var outputPaths = new List<string>();
        var supported = true;
        var executedCount = taskCount;
        var action = task.ActionType.ToLowerInvariant();
        var result = "skipped";

        try
        {
            switch (action)
            {
                case "documentation_source_check":
                case "source_check":
                case "collect_evidence":
                    {
                        _ = qualityEngine.LoadOrCreateReport();
                        _ = validation.LoadStatus() ?? validation.BuildStatus();
                        outputPaths.Add(executor.ExecutionLogPath);
                        outputPaths.Add(qualityEngine.EvidencePath);
                        outputPaths.Add(validation.StatusPath);
                        result = "collect_evidence_completed";
                        break;
                    }
                case "knowledge_item_validation":
                case "validate_knowledge_items":
                    {
                        _ = validation.LoadStatus() ?? validation.BuildStatus();
                        outputPaths.Add(executor.ExecutionLogPath);
                        outputPaths.Add(validation.StatusPath);
                        result = "knowledge_item_validation_completed";
                        break;
                    }
                case "trading_historical_oos_check":
                case "run_oos_validation":
                    {
                        var report = walkForward.LoadReport();
                        if (report is null)
                        {
                            supported = false;
                            warnings.Add("missing_walkforward_report");
                            result = "missing_report";
                        }
                        else
                        {
                            outputPaths.Add(walkForward.WalkForwardPath);
                            outputPaths.Add(walkForward.WalkForwardSummaryPath);
                            result = "trading_historical_oos_check_completed";
                        }
                        break;
                    }
                case "trading_forward_observation_check":
                case "run_forward_validation":
                    {
                        var mc = monteCarlo.LoadReport();
                        var stress = costStress.LoadReport();
                        if (mc is null && stress is null)
                        {
                            supported = false;
                            warnings.Add("missing_trading_stress_reports");
                            result = "missing_report";
                        }
                        else
                        {
                            if (mc is not null)
                            {
                                outputPaths.Add(monteCarlo.ReportPath);
                            }

                            if (stress is not null)
                            {
                                outputPaths.Add(costStress.ReportPath);
                            }

                            result = "trading_forward_observation_check_completed";
                        }
                        break;
                    }
                case "evidence_quality_recheck":
                    {
                        _ = qualityEngine.LoadOrCreateReport();
                        outputPaths.Add(qualityEngine.EvidencePath);
                        outputPaths.Add(qualityEngine.QualityPath);
                        result = "evidence_quality_recheck_completed";
                        break;
                    }
                case "contradiction_check":
                    {
                        _ = contradictionDetector.LoadOrRun();
                        outputPaths.Add(contradictionDetector.ContradictionsPath);
                        result = "contradiction_check_completed";
                        break;
                    }
                default:
                    supported = false;
                    warnings.Add($"unsupported_evidence_task_type:{task.ActionType}");
                    result = "unsupported";
                    break;
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            supported = false;
            warnings.Add($"evidence_task_failed:{task.ActionType}:{ex.GetType().Name}");
            result = "failed";
        }

        return new EvidenceTaskExecutionEntry(
            TaskId: task.TaskId,
            ReviewId: task.ReviewId,
            KnowledgeItemId: task.KnowledgeItemId,
            Title: task.Title,
            Domain: task.Domain,
            ActionType: task.ActionType,
            Status: result,
            Result: result,
            Supported: supported,
            ExecutedCount: executedCount,
            OutputPaths: outputPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings: warnings,
            CreatedAtUtc: now);
    }

    private HumanReviewQueue RefreshEvidenceStatuses(HumanReviewQueue queue)
    {
        var quality = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        var now = DateTimeOffset.UtcNow;
        var updated = queue.Items.Select(item =>
        {
            if (!item.Status.Equals("needs_more_evidence", StringComparison.OrdinalIgnoreCase))
            {
                return item;
            }

            var qualityItem = quality.Items.FirstOrDefault(candidate => candidate.KnowledgeId.Equals(item.KnowledgeItemId, StringComparison.OrdinalIgnoreCase));
            if (qualityItem is null)
            {
                return item;
            }

            var readyForReview = qualityItem.ValidationScore >= 0.55
                || qualityItem.TrustScore >= 0.6
                || qualityItem.EvidenceRefs.Count(reference => reference.StartsWith("validation:", StringComparison.OrdinalIgnoreCase) || reference.StartsWith("source:", StringComparison.OrdinalIgnoreCase)) >= 3;

            if (!readyForReview)
            {
                return item;
            }

            return item with
            {
                Status = "pending",
                UpdatedAtUtc = now,
                Recommendation = "ready_for_human_review"
            };
        }).ToList();

        return queue with
        {
            UpdatedAtUtc = now,
            PendingReviews = updated.Count(item => item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase)),
            ApprovedReviews = updated.Count(item => item.Status.Equals("approved", StringComparison.OrdinalIgnoreCase)),
            RejectedReviews = updated.Count(item => item.Status.Equals("rejected", StringComparison.OrdinalIgnoreCase)),
            NeedsMoreEvidenceReviews = updated.Count(item => item.Status.Equals("needs_more_evidence", StringComparison.OrdinalIgnoreCase)),
            DeferredReviews = updated.Count(item => item.Status.Equals("deferred", StringComparison.OrdinalIgnoreCase)),
            Items = updated,
            Warnings = queue.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private void WriteQueue(HumanReviewQueue refreshState, HumanReviewWorkflow humanReview)
    {
        var path = _resolvedQueuePath ?? humanReview.QueuePath;
        _resolvedQueuePath = path;
        var json = JsonSerializer.Serialize(refreshState, JsonDefaults.WriteOptions);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "cognitive_core");
            Directory.CreateDirectory(fallbackRoot);
            var fallbackPath = Path.Combine(fallbackRoot, "human_review_queue.json");
            _resolvedQueuePath = fallbackPath;
            File.WriteAllText(fallbackPath, json);
        }
    }

    private (string ReportPath, string MarkdownPath, string Root) ResolveOutputPaths()
    {
        var primaryRoot = Root;
        try
        {
            Directory.CreateDirectory(primaryRoot);
            return (Path.Combine(primaryRoot, "evidence_task_execution.json"), Path.Combine(primaryRoot, "evidence_task_execution.md"), primaryRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "evidence_task_execution");
            Directory.CreateDirectory(fallbackRoot);
            return (Path.Combine(fallbackRoot, "evidence_task_execution.json"), Path.Combine(fallbackRoot, "evidence_task_execution.md"), fallbackRoot);
        }
    }

    private string ResolveReadableReportPath()
    {
        if (File.Exists(ReportPath))
        {
            return ReportPath;
        }

        var fallbackPath = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "evidence_task_execution", "evidence_task_execution.json");
        return File.Exists(fallbackPath) ? fallbackPath : ReportPath;
    }

    private static void WriteTextWithFallback(string reportPath, string markdownPath, string root, EvidenceTaskExecutionReport report)
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
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "evidence_task_execution");
            Directory.CreateDirectory(fallbackRoot);
            File.WriteAllText(Path.Combine(fallbackRoot, "evidence_task_execution.json"), json);
            File.WriteAllText(Path.Combine(fallbackRoot, "evidence_task_execution.md"), markdown);
        }
    }

    private static string BuildMarkdown(EvidenceTaskExecutionReport report)
    {
        var lines = new List<string>
        {
            "# Evidence Task Execution",
            string.Empty,
            $"- Updated UTC: {report.UpdatedAtUtc:O}",
            $"- Tasks found: {report.TasksFound}",
            $"- Tasks executed: {report.TasksExecuted}",
            $"- Tasks skipped: {report.TasksSkipped}",
            $"- Unsupported tasks: {report.UnsupportedTasks}",
            $"- Needs More Evidence before: {report.NeedsMoreEvidenceBefore}",
            $"- Needs More Evidence after: {report.NeedsMoreEvidenceAfter}",
            $"- Pending Reviews before: {report.PendingReviewsBefore}",
            $"- Pending Reviews after: {report.PendingReviewsAfter}",
            string.Empty,
            "## Nächste Aktion",
            report.FrankActionRequired ? "- Frank muss jetzt entscheiden." : "- Frank muss aktuell nichts tun.",
            string.Empty,
            "## Ausgeführte Tasks",
        };

        lines.AddRange(report.ExecutedTasks.Count == 0
            ? ["- keine"]
            : report.ExecutedTasks.Select(task => $"- {task.Domain}: {task.ActionType} · {task.Result}"));
        lines.Add(string.Empty);
        lines.Add("## Übersprungene Tasks");
        lines.AddRange(report.SkippedTasks.Count == 0
            ? ["- keine"]
            : report.SkippedTasks.Select(task => $"- {task.Domain}: {task.ActionType} · {task.Result}"));
        return string.Join(Environment.NewLine, lines);
    }
}
