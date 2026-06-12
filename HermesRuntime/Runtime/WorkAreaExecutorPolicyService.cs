using System.Text.Json;

namespace Hermes.Runtime;

public sealed record WorkAreaExecutorPolicy(
    string AreaId,
    string AreaTitle,
    bool AutoAllowed,
    bool RequiresWorkWindow,
    bool RequiresNightlyWindow,
    bool RequiresResourceGuard,
    bool RequiresHumanReview,
    bool SafeDeleteOnly,
    string ExecutionMode,
    string NextExecutionWindowHint,
    string PlannedAction,
    string Notes,
    string ReportPathHint);

public sealed record WorkAreaExecutorDecision(
    string AreaId,
    string AreaTitle,
    string Status,
    bool AutomaticallyAllowed,
    bool RequiresHumanReview,
    bool FrankRequired,
    string HighestPriority,
    int ItemCount,
    int CompletedCount,
    int FailedCount,
    string NextExecutionWindow,
    string PlannedAction,
    string Result,
    string? OutputReportPath,
    DateTimeOffset? ExecutedAtUtc,
    IReadOnlyList<string> Warnings);

public sealed record WorkAreaExecutorPolicyReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string ConfigPath,
    string TimeControlPath,
    string ResourcePath,
    bool InWorkWindow,
    bool InNightlyWindow,
    bool ResourceHealthy,
    int ActiveAreas,
    int ActiveImprovements,
    int FrankItems,
    IReadOnlyList<WorkAreaExecutorDecision> WorkAreas,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class WorkAreaExecutorPolicyService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _configPath;
    private readonly string _queuePath;
    private readonly string _workAreasPath;

    public WorkAreaExecutorPolicyService(StoragePaths storagePaths, string? configPath = null)
    {
        _storagePaths = storagePaths;
        _configPath = configPath ?? Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", "config", "work_area_executor_policy.json");
        _queuePath = Path.Combine(_storagePaths.Root, "reports", "autonomous_improvement_queue", "autonomous_improvement_queue.json");
        _workAreasPath = Path.Combine(_storagePaths.Root, "reports", "autonomous_improvement_queue", "autonomous_improvement_work_areas.json");
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "autonomous_improvement_queue");

    public string ReportPath => Path.Combine(Root, "work_area_executor_policy.json");

    public string MarkdownPath => Path.Combine(Root, "work_area_executor_policy.md");

    public WorkAreaExecutorPolicyReport Run() => Evaluate(execute: false);

    public WorkAreaExecutorPolicyReport Execute() => Evaluate(execute: true);

    public WorkAreaExecutorPolicyReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            var report = JsonSerializer.Deserialize<WorkAreaExecutorPolicyReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
            return IsValidReport(report) ? report : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private WorkAreaExecutorPolicyReport Evaluate(bool execute)
    {
        Directory.CreateDirectory(Root);

        var scheduler = new HermesInternalScheduler(_storagePaths, Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", "config", "schedules.json"));
        var timeControl = scheduler.GetTimeControlStatus();
        var resourceGuard = new ResourceGuard(_storagePaths);
        var resource = resourceGuard.Check();
        var queue = LoadWorkAreaQueue();
        var workAreas = LoadWorkAreas();
        var policies = LoadPolicies();
        var executionCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        var decisions = policies
            .Select(policy => BuildDecision(policy, queue, workAreas, timeControl, resource, execute, executionCache))
            .ToList();

        var report = new WorkAreaExecutorPolicyReport(
            ReportVersion: execute ? "work_area_executor_policy_execution_v1" : "work_area_executor_policy_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ConfigPath: _configPath,
            TimeControlPath: timeControl.ConfigPath,
            ResourcePath: resourceGuard.StatusPath,
            InWorkWindow: timeControl.InWorkWindow,
            InNightlyWindow: timeControl.NightlyWindow.ActiveNow,
            ResourceHealthy: !resource.ShouldPause && !resource.ShouldStop,
            ActiveAreas: workAreas.ActiveAreas,
            ActiveImprovements: queue.ActiveImprovements,
            FrankItems: queue.FrankItems,
            WorkAreas: decisions,
            Warnings: decisions.SelectMany(item => item.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        TryWriteReport(report);
        return report;
    }

    private AutonomousImprovementQueueReport LoadWorkAreaQueue()
    {
        var queueService = new AutonomousImprovementQueueService(_storagePaths);
        if (File.Exists(_queuePath))
        {
            try
            {
                var loaded = queueService.Load();
                if (loaded is not null)
                {
                    return loaded;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // Fall through to regeneration below.
            }
        }

        return queueService.Generate();
    }

    private AutonomousImprovementWorkAreasReport LoadWorkAreas()
    {
        if (File.Exists(_workAreasPath))
        {
            try
            {
                var report = JsonSerializer.Deserialize<AutonomousImprovementWorkAreasReport>(
                    File.ReadAllText(_workAreasPath),
                    JsonDefaults.SnapshotReadOptions);
                if (report is not null
                    && report.WorkAreas is { Count: 5 }
                    && report.WorkAreas.All(item => !string.IsNullOrWhiteSpace(item.AreaId) && !string.IsNullOrWhiteSpace(item.AreaTitle)))
                {
                    return report;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // Fall through to queue-derived fallback.
            }
        }

        var queue = LoadWorkAreaQueue();
        var policies = LoadPolicies();
        var areas = policies.Select(policy =>
        {
            var count = queue.GroupedImprovementAreas
                .Where(group => MatchesAreaId(group.ActionType, policy.AreaId))
                .Sum(group => group.ItemCount);
            return new AutonomousImprovementWorkArea(
                AreaId: policy.AreaId,
                AreaTitle: policy.AreaTitle,
                ItemCount: count,
                Status: ResolveFallbackStatus(policy, queue),
                HighestPriority: ResolveHighestPriority(policy.AreaId),
                FrankRequired: policy.RequiresHumanReview,
                NextAction: policy.PlannedAction);
        }).ToList();

        return new AutonomousImprovementWorkAreasReport(
            ReportVersion: "autonomous_improvement_work_areas_fallback_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            ActiveAreas: areas.Count,
            ActiveItems: queue.ActiveImprovements,
            HermesCanHandle: queue.HermesCanHandle,
            FrankItems: queue.FrankItems,
            WorkAreas: areas,
            Warnings: queue.Warnings,
            NoTradingExecution: queue.NoTradingExecution,
            NoBrokerAction: queue.NoBrokerAction,
            NoAutoTrading: queue.NoAutoTrading,
            HumanReviewRequired: queue.HumanReviewRequired,
            QueuePath: queue.QueuePath,
            SummaryPath: queue.SummaryPath,
            MarkdownPath: queue.MarkdownPath);
    }

    private IReadOnlyList<WorkAreaExecutorPolicy> LoadPolicies()
    {
        if (File.Exists(_configPath))
        {
            try
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<List<WorkAreaExecutorPolicy>>(json, JsonDefaults.SnapshotReadOptions);
                if (config is { Count: > 0 })
                {
                    return config;
                }
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // Fall through to defaults.
            }
        }

        return new[]
        {
            new WorkAreaExecutorPolicy("gather_more_evidence", "Evidenz sammeln", true, true, false, false, false, false, "auto", "Arbeitsfenster", "Evidenz sammeln", "Bevorzugt im Arbeitsfenster.", "reports/knowledge_validation_audit/knowledge_validation_audit.json"),
            new WorkAreaExecutorPolicy("source_expansion", "Quellen erweitern", true, true, false, false, false, false, "auto", "Arbeitsfenster", "Quellen erweitern", "Nur sichere lokale/zugelassene Quellen.", "reports/knowledge_trust_improvement_plan/knowledge_trust_improvement_plan.json"),
            new WorkAreaExecutorPolicy("schedule_revalidation", "Re-Validierung", true, false, true, true, false, false, "plan_or_nightly", "Nightly", "Re-Validierung planen", "Schwere Läufe nur im Nightly-Fenster.", "reports/knowledge_validation_audit/knowledge_validation_audit.json"),
            new WorkAreaExecutorPolicy("contradiction_analysis", "Widersprüche prüfen", true, true, false, false, true, false, "analysis_only", "Arbeitsfenster", "Widersprüche analysieren", "Analyse automatisch; Auflösung nur im Prüfzentrum.", "reports/trusted_knowledge_review_gate/trusted_knowledge_review_gate.json"),
            new WorkAreaExecutorPolicy("systempflege", "Systempflege", true, true, false, true, false, true, "plan_only", "bei Bedarf", "Cleanup-Plan aktualisieren", "Reale Löschungen nur mit safe cleanup.", "reports/storage/cleanup_plan.json"),
        };
    }

    private static string ResolveFallbackStatus(WorkAreaExecutorPolicy policy, AutonomousImprovementQueueReport queue)
    {
        if (policy.RequiresNightlyWindow)
        {
            return "wartet auf Nightly";
        }

        if (queue.FrankItems > 0 && policy.RequiresHumanReview)
        {
            return "geplant";
        }

        return "bereit";
    }

    private WorkAreaExecutorDecision BuildDecision(
        WorkAreaExecutorPolicy policy,
        AutonomousImprovementQueueReport queue,
        AutonomousImprovementWorkAreasReport workAreas,
        ScheduleTimeControlStatus timeControl,
        ResourceSnapshot resource,
        bool execute,
        IDictionary<string, string?> executionCache)
    {
        var inWorkWindow = timeControl.InWorkWindow;
        var inNightlyWindow = timeControl.NightlyWindow.ActiveNow;
        var resourceHealthy = !resource.ShouldPause && !resource.ShouldStop;
        var autoAllowed = policy.AutoAllowed && resourceHealthy;
        var readyNow = autoAllowed && (!policy.RequiresWorkWindow || inWorkWindow) && (!policy.RequiresNightlyWindow || inNightlyWindow);
        DateTimeOffset? executedAtUtc = execute && readyNow ? DateTimeOffset.UtcNow : null;
        var outputReportPath = execute && readyNow ? ExecuteSafeAction(policy, executionCache) : ResolveOutputReportPath(policy);
        var status = ResolveStatus(policy, inWorkWindow, inNightlyWindow, resourceHealthy, executedAtUtc is not null);
        var nextExecutionWindow = ResolveNextWindow(policy, inWorkWindow, inNightlyWindow, resourceHealthy);
        var result = execute && readyNow ? "ausgeführt" : execute ? "geplant" : "bereit";

        return new WorkAreaExecutorDecision(
            AreaId: policy.AreaId,
            AreaTitle: policy.AreaTitle,
            Status: status,
            AutomaticallyAllowed: autoAllowed,
            RequiresHumanReview: policy.RequiresHumanReview,
            FrankRequired: policy.RequiresHumanReview,
            HighestPriority: ResolveHighestPriority(policy.AreaId),
            ItemCount: ResolveItemCount(workAreas, policy.AreaId),
            CompletedCount: ResolveCompletedCount(workAreas, policy.AreaId),
            FailedCount: ResolveFailedCount(workAreas, policy.AreaId),
            NextExecutionWindow: nextExecutionWindow,
            PlannedAction: policy.PlannedAction,
            Result: result,
            OutputReportPath: outputReportPath,
            ExecutedAtUtc: executedAtUtc,
            Warnings: BuildWarnings(policy, inWorkWindow, inNightlyWindow, resourceHealthy));
    }

    private static string ResolveStatus(
        WorkAreaExecutorPolicy policy,
        bool inWorkWindow,
        bool inNightlyWindow,
        bool resourceHealthy,
        bool executed)
    {
        if (executed)
        {
            return "ausgeführt";
        }

        if (!resourceHealthy)
        {
            return "geplant";
        }

        if (policy.RequiresNightlyWindow && !inNightlyWindow)
        {
            return "wartet auf Nightly";
        }

        if (policy.RequiresWorkWindow && !inWorkWindow)
        {
            return "bereit";
        }

        return "bereit";
    }

    private static string ResolveNextWindow(WorkAreaExecutorPolicy policy, bool inWorkWindow, bool inNightlyWindow, bool resourceHealthy)
    {
        if (!resourceHealthy)
        {
            return "nach ResourceGuard";
        }

        if (policy.RequiresNightlyWindow && !inNightlyWindow)
        {
            return "Nightly";
        }

        if (policy.RequiresWorkWindow && !inWorkWindow)
        {
            return "Arbeitsfenster";
        }

        return "jetzt";
    }

    private static string ResolveOutputReportPath(WorkAreaExecutorPolicy policy) => policy.AreaId switch
    {
        "gather_more_evidence" => "reports/knowledge_validation_audit/knowledge_validation_audit.json",
        "source_expansion" => "reports/knowledge_trust_improvement_plan/knowledge_trust_improvement_plan.json",
        "schedule_revalidation" => "reports/knowledge_validation_audit/knowledge_validation_audit.json",
        "contradiction_analysis" => "reports/trusted_knowledge_review_gate/trusted_knowledge_review_gate.json",
        "systempflege" => "reports/storage/cleanup_plan.json",
        _ => string.Empty,
    };

    private string? ExecuteSafeAction(WorkAreaExecutorPolicy policy, IDictionary<string, string?> executionCache)
    {
        if (executionCache.TryGetValue(policy.AreaId, out var cached))
        {
            return cached;
        }

        string? result = policy.AreaId switch
        {
            "gather_more_evidence" => new KnowledgeValidationAuditService(_storagePaths).Run().AuditPath,
            "source_expansion" => new KnowledgeTrustImprovementPlannerService(_storagePaths).Run().ReportPath,
            "schedule_revalidation" => new KnowledgeValidationAuditService(_storagePaths).Run().AuditPath,
            "validation_queue_repair" => new AutonomousImprovementQueueService(_storagePaths).Generate().QueuePath,
            _ => null,
        };

        if (policy.AreaId.Equals("contradiction_analysis", StringComparison.OrdinalIgnoreCase))
        {
            var service = new TrustedKnowledgeReviewGateService(_storagePaths);
            service.Run();
            result = service.GatePath;
        }
        else if (policy.AreaId.Equals("systempflege", StringComparison.OrdinalIgnoreCase) || policy.AreaId.Equals("cleanup_plan_update", StringComparison.OrdinalIgnoreCase))
        {
            var hygiene = new StorageHygieneService(_storagePaths);
            hygiene.BuildPlan();
            result = hygiene.CleanupPlanPath;
        }
        executionCache[policy.AreaId] = result;
        return result;
    }

    private static int ResolveItemCount(AutonomousImprovementWorkAreasReport workAreas, string areaId)
    {
        var area = workAreas.WorkAreas?.FirstOrDefault(item => MatchesAreaId(item.AreaId, areaId));
        return area?.ItemCount ?? 0;
    }

    private static int ResolveCompletedCount(AutonomousImprovementWorkAreasReport workAreas, string areaId)
    {
        return 0;
    }

    private static int ResolveFailedCount(AutonomousImprovementWorkAreasReport workAreas, string areaId)
    {
        return 0;
    }

    private static bool MatchesAreaId(string? candidate, string areaId)
    {
        if (string.Equals(candidate, areaId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return areaId switch
        {
            "systempflege" => string.Equals(candidate, "cleanup_plan_update", StringComparison.OrdinalIgnoreCase),
            "contradiction_analysis" => string.Equals(candidate, "contradiction_analysis", StringComparison.OrdinalIgnoreCase),
            "gather_more_evidence" => string.Equals(candidate, "gather_more_evidence", StringComparison.OrdinalIgnoreCase),
            "source_expansion" => string.Equals(candidate, "source_expansion", StringComparison.OrdinalIgnoreCase),
            "schedule_revalidation" => string.Equals(candidate, "schedule_revalidation", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static bool IsValidReport(WorkAreaExecutorPolicyReport? report)
    {
        return report is not null
            && report.WorkAreas is { Count: 5 }
            && report.WorkAreas.All(item => !string.IsNullOrWhiteSpace(item.AreaId) && !string.IsNullOrWhiteSpace(item.AreaTitle));
    }

    private static string ResolveHighestPriority(string areaId) => areaId switch
    {
        "contradiction_analysis" => "high",
        "systempflege" => "low",
        _ => "medium",
    };

    private static IReadOnlyList<string> BuildWarnings(WorkAreaExecutorPolicy policy, bool inWorkWindow, bool inNightlyWindow, bool resourceHealthy)
    {
        var warnings = new List<string>();

        if (policy.RequiresNightlyWindow && !inNightlyWindow)
        {
            warnings.Add("wartet_auf_nightly");
        }

        if (policy.RequiresWorkWindow && !inWorkWindow)
        {
            warnings.Add("wartet_auf_arbeitsfenster");
        }

        if (!resourceHealthy)
        {
            warnings.Add("resourceguard_signal");
        }

        if (policy.RequiresHumanReview)
        {
            warnings.Add("requires_human_review_for_resolution_only");
        }

        if (policy.SafeDeleteOnly)
        {
            warnings.Add("safe_delete_only");
        }

        return warnings;
    }

    private void TryWriteReport(WorkAreaExecutorPolicyReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        try
        {
            File.WriteAllText(ReportPath, json);
            File.WriteAllText(MarkdownPath, markdown);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "autonomous_improvement_queue");
            Directory.CreateDirectory(fallbackRoot);
            File.WriteAllText(Path.Combine(fallbackRoot, "work_area_executor_policy.json"), json);
            File.WriteAllText(Path.Combine(fallbackRoot, "work_area_executor_policy.md"), markdown);
        }
    }

    private static string BuildMarkdown(WorkAreaExecutorPolicyReport report)
    {
        var lines = new List<string>
        {
            "# Work Area Executor Policy",
            string.Empty,
            $"- Updated UTC: {report.UpdatedAtUtc:O}",
            $"- In Work Window: {report.InWorkWindow}",
            $"- In Nightly Window: {report.InNightlyWindow}",
            $"- Resource Healthy: {report.ResourceHealthy}",
            $"- Active Areas: {report.ActiveAreas}",
            $"- Active Improvements: {report.ActiveImprovements}",
            $"- Frank Items: {report.FrankItems}",
            string.Empty,
            "## Arbeitsbereiche",
        };

        lines.AddRange(report.WorkAreas.Select(item => $"- {item.AreaTitle}: {item.Status} · {item.AutomaticallyAllowed} · {item.NextExecutionWindow}"));
        return string.Join(Environment.NewLine, lines);
    }
}
