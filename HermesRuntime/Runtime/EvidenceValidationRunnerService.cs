using System.Text.Json;

namespace Hermes.Runtime;

public sealed record EvidenceValidationExecutionTask(
    string Domain,
    string Action,
    string Status,
    string Result,
    int ExecutedCount,
    int EvidenceRefsAdded,
    IReadOnlyList<string> OutputPaths,
    IReadOnlyList<string> Warnings);

public sealed record EvidenceValidationRunnerReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int ValidationTasksExecuted,
    int EvidenceTasksExecuted,
    int NeedsMoreEvidenceBefore,
    int NeedsMoreEvidenceAfter,
    int PendingReviewsBefore,
    int PendingReviewsAfter,
    int NewPendingReviews,
    int PreparedForReviewCount,
    int StillNeedsMoreEvidenceCount,
    int ValidationTasksPending,
    IReadOnlyList<string> Domains,
    IReadOnlyList<EvidenceValidationExecutionTask> ExecutedTasks,
    IReadOnlyList<string> Warnings,
    bool FrankActionRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string AuditPath,
    string QueuePath,
    string ReportPath,
    string MarkdownPath);

public sealed class EvidenceValidationRunnerService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;
    private string? _resolvedQueuePath;

    public EvidenceValidationRunnerService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "evidence_validation_runner");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "evidence_validation_runner.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "evidence_validation_runner.md");

    public EvidenceValidationRunnerReport Run(int maxDomains = 5, int maxItemsPerDomain = 20)
    {
        var (reportPath, markdownPath, root) = ResolveOutputPaths();
        _resolvedReportPath = reportPath;
        _resolvedMarkdownPath = markdownPath;
        var humanReview = new HumanReviewWorkflow(_storagePaths);
        var beforeQueue = humanReview.LoadOrCreateQueue();
        var beforeNeedsMoreEvidence = beforeQueue.NeedsMoreEvidenceReviews;
        var beforePendingReviews = beforeQueue.PendingReviews;
        var validation = new KnowledgeValidationStrategy(_storagePaths);
        var statusBefore = validation.LoadStatus() ?? validation.BuildStatus();
        var plans = validation.LoadPlanReport() ?? validation.GeneratePlans(50);
        var domains = plans.Plans
            .Where(plan => plan.Status.Equals("open", StringComparison.OrdinalIgnoreCase))
            .GroupBy(plan => plan.Domain, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => DomainPriorityRank(group.Key))
            .Take(Math.Clamp(maxDomains, 1, 10))
            .Select(group => group.Key)
            .ToList();

        var executedTasks = new List<EvidenceValidationExecutionTask>();
        var validationExecutions = new List<KnowledgeValidationExecutionResult>();
        var validationExecutor = new KnowledgeValidationExecutor(_storagePaths);

        foreach (var domain in domains)
        {
            var results = validationExecutor.ExecuteDomain(domain, maxItemsPerDomain);
            var evidenceRefsAdded = results.Sum(result => result.EvidenceRefs.Count);
            executedTasks.Add(new EvidenceValidationExecutionTask(
                Domain: domain,
                Action: "Validation-/Evidenzaufgaben ausgeführt",
                Status: "executed",
                Result: $"domain={domain}; tasks={results.Count}; evidence_refs={evidenceRefsAdded}",
                ExecutedCount: results.Count,
                EvidenceRefsAdded: evidenceRefsAdded,
                OutputPaths: results.SelectMany(result => result.OutputPaths).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Warnings: results.SelectMany(result => result.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToList()));
            validationExecutions.AddRange(results);
        }

        var queueAfter = humanReview.LoadOrCreateQueue();
        var refreshedQueue = RefreshEvidenceStatuses(queueAfter);
        var afterQueue = WriteQueue(refreshState: refreshedQueue, humanReview);
        var afterNeedsMoreEvidence = afterQueue.NeedsMoreEvidenceReviews;
        var afterPendingReviews = afterQueue.PendingReviews;
        var preparedForReview = Math.Max(0, beforeNeedsMoreEvidence - afterNeedsMoreEvidence);
        var stillNeedsMoreEvidence = afterNeedsMoreEvidence;
        var frankActionRequired = afterPendingReviews > 0;

        var report = new EvidenceValidationRunnerReport(
            ReportVersion: "evidence_validation_runner_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ValidationTasksExecuted: validationExecutions.Count,
            EvidenceTasksExecuted: executedTasks.Sum(task => task.ExecutedCount),
            NeedsMoreEvidenceBefore: beforeNeedsMoreEvidence,
            NeedsMoreEvidenceAfter: afterNeedsMoreEvidence,
            PendingReviewsBefore: beforePendingReviews,
            PendingReviewsAfter: afterPendingReviews,
            NewPendingReviews: Math.Max(0, afterPendingReviews - beforePendingReviews),
            PreparedForReviewCount: preparedForReview,
            StillNeedsMoreEvidenceCount: stillNeedsMoreEvidence,
            ValidationTasksPending: statusBefore.ValidationTasksPending,
            Domains: domains,
            ExecutedTasks: executedTasks,
            Warnings: validationExecutions.Count == 0 ? ["evidence_validation_runner_no_tasks_executed"] : [],
            FrankActionRequired: frankActionRequired,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            AuditPath: new KnowledgeValidationAuditService(_storagePaths).AuditPath,
            QueuePath: humanReview.QueuePath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteTextWithFallback(reportPath, markdownPath, root, report);
        validation.BuildStatus();
        new KnowledgeValidationAuditService(_storagePaths).Run();
        try
        {
            new MasterStatusWriter(new MasterStatusService(_storagePaths, Directory.GetCurrentDirectory())).WriteSnapshot();
        }
        catch (Exception ex) when (ex is NullReferenceException or IOException or UnauthorizedAccessException)
        {
            report = report with
            {
                Warnings = report.Warnings.Concat([$"master_status_snapshot_failed:{ex.GetType().Name}"]).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            };
            WriteTextWithFallback(reportPath, markdownPath, root, report);
        }
        return report;
    }

    public EvidenceValidationRunnerReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<EvidenceValidationRunnerReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
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

    private HumanReviewQueue WriteQueue(HumanReviewQueue refreshState, HumanReviewWorkflow humanReview)
    {
        var path = _resolvedQueuePath ?? ResolveQueuePath();
        _resolvedQueuePath = path;
        var json = JsonSerializer.Serialize(refreshState, JsonDefaults.WriteOptions);
        var queueCopy = humanReview.LoadOrCreateQueue() with
        {
            UpdatedAtUtc = refreshState.UpdatedAtUtc,
            PendingReviews = refreshState.PendingReviews,
            ApprovedReviews = refreshState.ApprovedReviews,
            RejectedReviews = refreshState.RejectedReviews,
            NeedsMoreEvidenceReviews = refreshState.NeedsMoreEvidenceReviews,
            DeferredReviews = refreshState.DeferredReviews,
            Items = refreshState.Items,
            Warnings = refreshState.Warnings
        };
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
            File.WriteAllText(path, JsonSerializer.Serialize(queueCopy, JsonDefaults.WriteOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "cognitive_core");
            Directory.CreateDirectory(fallbackRoot);
            var fallbackPath = Path.Combine(fallbackRoot, "human_review_queue.json");
            _resolvedQueuePath = fallbackPath;
            File.WriteAllText(fallbackPath, json);
            File.WriteAllText(fallbackPath, JsonSerializer.Serialize(queueCopy, JsonDefaults.WriteOptions));
        }
        return queueCopy;
    }

    private static string BuildMarkdown(EvidenceValidationRunnerReport report)
    {
        var lines = new List<string>
        {
            "# Evidence Validation Runner",
            string.Empty,
            $"- Updated UTC: {report.UpdatedAtUtc:O}",
            $"- Ausgeführte Validation Tasks: {report.ValidationTasksExecuted}",
            $"- Ausgeführte Evidence Tasks: {report.EvidenceTasksExecuted}",
            $"- Needs More Evidence vorher: {report.NeedsMoreEvidenceBefore}",
            $"- Needs More Evidence nachher: {report.NeedsMoreEvidenceAfter}",
            $"- Pending Reviews vorher: {report.PendingReviewsBefore}",
            $"- Pending Reviews nachher: {report.PendingReviewsAfter}",
            $"- Neue Pending Reviews: {report.NewPendingReviews}",
            $"- Frank nötig: {(report.FrankActionRequired ? "ja" : "nein")}",
            string.Empty,
            "## Ausgeführte Tasks",
        };
        lines.AddRange(report.ExecutedTasks.Count == 0
            ? ["- keine"]
            : report.ExecutedTasks.Select(task => $"- {task.Domain}: {task.Result}"));
        return string.Join(Environment.NewLine, lines);
    }

    private static int DomainPriorityRank(string domain) =>
        domain.ToLowerInvariant() switch
        {
            "trading" => 0,
            "documentation" => 1,
            "software" => 2,
            "process" => 3,
            "research" => 4,
            _ => 5
        };

    private string ResolveQueuePath()
    {
        var primaryRoot = Path.Combine(_storagePaths.Root, "cognitive_core");
        try
        {
            Directory.CreateDirectory(primaryRoot);
            return Path.Combine(primaryRoot, "human_review_queue.json");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "cognitive_core");
            Directory.CreateDirectory(fallbackRoot);
            return Path.Combine(fallbackRoot, "human_review_queue.json");
        }
    }

    private (string ReportPath, string MarkdownPath, string Root) ResolveOutputPaths()
    {
        var primaryRoot = Root;
        try
        {
            Directory.CreateDirectory(primaryRoot);
            return (Path.Combine(primaryRoot, "evidence_validation_runner.json"), Path.Combine(primaryRoot, "evidence_validation_runner.md"), primaryRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "evidence_validation_runner");
            Directory.CreateDirectory(fallbackRoot);
            return (Path.Combine(fallbackRoot, "evidence_validation_runner.json"), Path.Combine(fallbackRoot, "evidence_validation_runner.md"), fallbackRoot);
        }
    }

    private static void WriteTextWithFallback(string reportPath, string markdownPath, string root, EvidenceValidationRunnerReport report)
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
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "evidence_validation_runner");
            Directory.CreateDirectory(fallbackRoot);
            File.WriteAllText(Path.Combine(fallbackRoot, "evidence_validation_runner.json"), json);
            File.WriteAllText(Path.Combine(fallbackRoot, "evidence_validation_runner.md"), markdown);
        }
    }
}
