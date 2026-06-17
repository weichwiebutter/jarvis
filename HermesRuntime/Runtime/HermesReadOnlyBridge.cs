using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hermes.Runtime;

public sealed class HermesReadOnlyBridge
{
    private const string BridgeVersion = "hermes_readonly_bridge_v2";
    private const int MaxArrayItems = 25;

    private static readonly IReadOnlyList<ReportDefinition> Reports =
    [
        new("runtimeHealth", "Runtime Health", "/runtime/health", "reports/runtime_health.json"),
        new("masterStatus", "Hermes Master Status", "/reports/master-status", "reports/master-status/master_status.json"),
        new("setupWatch", "Setup Watch", "/runtime/setup-watch", "setup_watch/setup_watch.json"),
        new("supervisorState", "Supervisor State", "/runtime/supervisor", "reports/supervisor/supervisor_state.json"),
        new("schedulerState", "Scheduler State", "/runtime/scheduler", "reports/supervisor/scheduler_state.json"),
        new("timeControl", "Zeitsteuerung", "/reports/time-control", "config/schedules.json"),
        new("resourceStatus", "Resource Status", "/runtime/resource", "reports/resource/resource_status.json"),
        new("storageStatus", "Storage Status", "/runtime/storage", "reports/storage/storage_status.json"),
        new("storageCleanupSafetyAudit", "Storage Cleanup Safety Audit", "/reports/storage-cleanup-safety-audit", "reports/storage_cleanup/storage_cleanup_safety_audit.json"),
        new("cleanupPlan", "Cleanup Plan", "/runtime/cleanup-plan", "reports/storage/cleanup_plan.json"),
        new("nightlyState", "Nightly State", "/runtime/nightly", "reports/nightly_beta3/nightly_state.json"),
        new("researchInsights", "Research Insights", "/reports/research-insights", "strategy_research/research_insights.json"),
        new("robustStrategies", "Robuste Strategien", "/reports/robust-strategies", "strategy_research/robust_strategies.json"),
        new("overfitReport", "Overfit Report", "/reports/overfit-report", "strategy_research/overfit_report.json"),
        new("humanReviewQueue", "Human Review Queue", "/reports/human-review-queue", "cognitive_core/human_review_queue.json"),
        new("knowledgeValidationAudit", "Knowledge Validation Audit", "/reports/knowledge-validation-audit", "reports/knowledge_validation_audit/knowledge_validation_audit.json"),
        new("validationBacklogAnalyzer", "Validation Backlog Analyzer", "/reports/validation-backlog-analyzer", "reports/validation_backlog/validation_backlog_analyzer.json"),
        new("knowledgeConsolidationAnalyzer", "Knowledge Consolidation Analyzer", "/reports/knowledge-consolidation-analyzer", "reports/knowledge_consolidation/knowledge_consolidation_analyzer.json"),
        new("knowledgeConsolidationExecutor", "Knowledge Consolidation Executor", "/reports/knowledge-consolidation-executor", "reports/knowledge_consolidation/knowledge_consolidation_executor.json"),
        new("strategyMutationAnalyzer", "Strategy Mutation Analyzer", "/reports/strategy-mutation-analyzer", "reports/strategy_mutation/strategy_mutation_analyzer.json"),
        new("strategyParameterResearchPlanner", "Strategy Parameter Research Planner", "/reports/strategy-parameter-research-planner", "reports/strategy_parameter_research/strategy_parameter_research_planner.json"),
        new("tradingResearchSynthesizer", "Trading Research Synthesizer", "/reports/trading-research-synthesizer", "reports/trading_research_synthesis/trading_research_synthesizer.json"),
        new("strategyMutationValidationPlanner", "Strategy Mutation Validation Planner", "/reports/strategy-mutation-validation-planner", "reports/strategy_mutation_validation/strategy_mutation_validation_planner.json"),
        new("strategyValidationQueueExport", "Strategy Validation Queue Export", "/reports/strategy-validation-queue", "reports/strategy_validation_queue/strategy_validation_queue.json"),
        new("strategyValidationReadinessAnalyzer", "Strategy Validation Readiness Analyzer", "/reports/strategy-validation-readiness", "reports/strategy_validation_readiness/strategy_validation_readiness_analyzer.json"),
        new("strategyBacktestJobPlanner", "Strategy Backtest Job Planner", "/reports/strategy-backtest-job-planner", "reports/strategy_backtest_jobs/strategy_backtest_job_planner.json"),
        new("strategyBacktestExecutor", "Strategy Backtest Executor", "/reports/strategy-backtest-executor", "reports/strategy_backtest_execution/strategy_backtest_executor.json"),
        new("strategyBacktestQualityAudit", "Strategy Backtest Quality Audit", "/reports/strategy-backtest-quality-audit", "reports/strategy_backtest_quality/strategy_backtest_quality_audit.json"),
        new("strategyDatasetGateAudit", "Strategy Dataset Gate Audit", "/reports/strategy-dataset-gate-audit", "reports/strategy_dataset_gate/strategy_dataset_gate_audit.json"),
        new("validationBacklogExecutor", "Validation Backlog Executor", "/reports/validation-backlog-executor", "reports/validation_backlog/validation_backlog_executor.json"),
        new("reviewStatusConsistencyAudit", "Review Status Consistency Audit", "/reports/review-status-consistency-audit", "reports/review_status_consistency_audit/review_status_consistency_audit.json"),
        new("validationQueueRefill", "Validation Queue Refill", "/reports/validation-queue-refill", "reports/validation_queue_refill/validation_queue_refill.json"),
        new("evidenceValidationRunner", "Evidence Validation Runner", "/reports/evidence-validation-runner", "reports/evidence_validation_runner/evidence_validation_runner.json"),
        new("autonomousImprovementQueue", "Autonomous Improvement Queue", "/reports/autonomous-improvement-queue", "reports/autonomous_improvement_queue/autonomous_improvement_queue.json"),
        new("autonomousImprovementQueueSummary", "Autonomous Improvement Queue Summary", "/reports/autonomous-improvement-queue-summary", "reports/autonomous_improvement_queue/autonomous_improvement_queue_summary.json"),
        new("autonomousImprovementWorkAreas", "Autonomous Improvement Work Areas", "/reports/autonomous-improvement-work-areas", "reports/autonomous_improvement_queue/autonomous_improvement_work_areas.json"),
        new("workAreaExecutorPolicy", "Work Area Executor Policy", "/reports/work-area-executor-policy", "reports/autonomous_improvement_queue/work_area_executor_policy.json"),
        new("nightlyWorkAreaStatus", "Nightly Work Area Status", "/reports/nightly-work-area-status", "reports/autonomous_improvement_queue/nightly_work_area_status.json"),
        new("evidenceAutoLoop", "Evidence Auto Loop", "/reports/evidence-auto-loop", "reports/evidence_auto_loop/evidence_auto_loop.json"),
        new("evidenceTaskExecution", "Evidence Task Execution", "/reports/evidence-task-execution", "reports/evidence_task_execution/evidence_task_execution.json"),
        new("evidenceImpactAnalysis", "Evidence Impact Analysis", "/reports/evidence-impact-analysis", "reports/evidence_impact_analysis/evidence_impact_analysis.json"),
        new("reviewEvidenceRefresh", "Review Evidence Refresh", "/reports/review-evidence-refresh", "reports/review_evidence_refresh/review_evidence_refresh.json"),
        new("autonomousImprovementExecution", "Autonomous Improvement Execution", "/reports/autonomous-improvement-execution", "reports/autonomous_improvement_execution/autonomous_improvement_execution.json"),
        new("trustedKnowledgeReviewGate", "Trusted Knowledge Review Gate", "/reports/trusted-knowledge-review-gate", "reports/trusted_knowledge_review_gate/trusted_knowledge_review_gate.json"),
        new("knowledgeTrustImprovementPlan", "Knowledge Trust Improvement Plan", "/reports/knowledge-trust-improvement-plan", "reports/knowledge_trust_improvement_plan/knowledge_trust_improvement_plan.json"),
        new("ensemblePortfolioStatus", "Ensemble Portfolio Status", "/reports/ensemble-portfolio-status", "reports/scalping_portfolio/ensemble_portfolio/ensemble_portfolio_status.json"),
        new("systemBHandoffBundle", "System B Handoff Bundle", "/reports/system-b-handoff-bundle", "reports/system_b_handoff/system_b_handoff_bundle/portfolio_summary.json"),
        new("validateEnsembleSignalPackage", "Validate Ensemble Signal Package", "/reports/validate-ensemble-signal-package", "reports/scalping_portfolio/ensemble_portfolio/ensemble_signal_agent_package.json"),
        new("setupRegistry", "Setup Registry", "/reports/setup-registry", "reports/setup_registry/setup_registry.json"),
        new("signalAgentSpecs", "Signal Agent Specs", "/reports/signal-agent-specs", "reports/signal_agent_specs/signal_agent_specs.json"),
        new("multiAssetResearchStatus", "Multi-Asset Research Status", "/reports/multi-asset-research-status", "reports/scalping_portfolio/multi_asset_roadmap.json"),
        new("regimeSummary", "Regime Summary", "/reports/regime-summary", "reports/regimes/regime_summary.json"),
        new("strategyRegimePerformance", "Strategy Regime Performance", "/reports/strategy-regime-performance", "reports/regimes/strategy_regime_performance.json"),
        new("regimeDistribution", "Regime Distribution", "/reports/regime-distribution", "reports/regimes/regime_distribution.json")
    ];

    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "secret",
        "client_secret",
        "api_key",
        "apikey",
        "authorization",
        "access_token",
        "refresh_token",
        "password",
        "token"
    };

    private static readonly HashSet<string> AllowedWriteEndpoints = new(StringComparer.OrdinalIgnoreCase)
    {
        "/bridge/review/approve-review",
        "/bridge/review/reject-review",
        "/bridge/review/request-more-evidence",
        "/bridge/review/defer-review",
        "/bridge/bot-spec/export",
        "/bridge/time-control/update",
        "/bridge/execute-work-areas",
        "/bridge/run-nightly-work-areas",
    };

    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private readonly DateTimeOffset _startedAtUtc = DateTimeOffset.UtcNow;

    public HermesReadOnlyBridge(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public async Task RunAsync(string urlPrefix, CancellationToken cancellationToken)
    {
        var normalizedPrefix = NormalizePrefix(urlPrefix);
        using var listener = new HttpListener();
        listener.Prefixes.Add(normalizedPrefix);
        listener.Start();

        Console.WriteLine("Hermes Read-Only Bridge");
        Console.WriteLine("-----------------------");
        Console.WriteLine($"Listening              {normalizedPrefix}");
        Console.WriteLine($"Storage Root           {DisplayPath(_storagePaths.Root)}");
        Console.WriteLine("Mode                   read-only");
        Console.WriteLine("Safety                 no_auto_trading=true, human_review_required=true");
        Console.WriteLine("Stop                   Ctrl+C");

        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync().WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleRequestAsync(context), CancellationToken.None);
        }
    }

    public BridgeResponseModel CreateHealthResponse()
    {
        var index = BuildReportIndex();
        var health = new BridgeHealthSnapshot(
            Status: "available",
            BridgeVersion: BridgeVersion,
            StartedAtUtc: _startedAtUtc,
            TimestampUtc: DateTimeOffset.UtcNow,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportsConfigured: Reports.Count,
            ReportsAvailable: index.Reports.Count(report => report.Available),
            Endpoints: Reports.Select(report => report.Endpoint)
                .Append("/bridge/review/approve-review")
                .Append("/bridge/review/reject-review")
                .Append("/bridge/review/request-more-evidence")
                .Append("/bridge/review/defer-review")
                .Append("/bridge/bot-spec/export")
                .Append("/bridge/time-control/update")
                .Append("/bridge/execute-work-areas")
                .Append("/bridge/run-nightly-work-areas")
                .Append("/bridge/health")
                .Append("/reports")
                .Append("/operator/dashboard")
                .Order()
                .ToArray());

        return Ok(health);
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        try
        {
            ApplyHeaders(context.Response, context.Request);

            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = 204;
                context.Response.Close();
                return;
            }

            if (context.Request.HttpMethod is not ("GET" or "POST"))
            {
                await WriteJsonAsync(
                    context.Response,
                    Error("method_not_allowed", "Only GET, POST and OPTIONS requests are allowed."),
                    HttpStatusCode.MethodNotAllowed);
                return;
            }

            var path = (context.Request.Url?.AbsolutePath ?? "/").TrimEnd('/');
            path = string.IsNullOrWhiteSpace(path) ? "/" : path;

            if (context.Request.HttpMethod == "POST" && !AllowedWriteEndpoints.Contains(path))
            {
                await WriteJsonAsync(
                    context.Response,
                    Error("not_found", $"Endpoint is not whitelisted: {path}"),
                    HttpStatusCode.NotFound);
                return;
            }

            if (context.Request.HttpMethod == "POST" && TryHandleReviewAction(path, context.Request, out var reviewResponse, out var reviewStatus))
            {
                await WriteJsonAsync(context.Response, reviewResponse, reviewStatus);
                return;
            }

            if (context.Request.HttpMethod == "POST" && TryHandleBotSpecAction(path, context.Request, out var botSpecResponse, out var botSpecStatus))
            {
                await WriteJsonAsync(context.Response, botSpecResponse, botSpecStatus);
                return;
            }

            if (context.Request.HttpMethod == "POST" && TryHandleTimeControlAction(path, context.Request, out var timeControlResponse, out var timeControlStatus))
            {
                await WriteJsonAsync(context.Response, timeControlResponse, timeControlStatus);
                return;
            }

            if (context.Request.HttpMethod == "POST" && TryHandleWorkAreaExecutionAction(path, context.Request, out var workAreaResponse, out var workAreaStatus))
            {
                await WriteJsonAsync(context.Response, workAreaResponse, workAreaStatus);
                return;
            }

            if (context.Request.HttpMethod == "POST" && TryHandleNightlyWorkAreaAction(path, context.Request, out var nightlyResponse, out var nightlyStatus))
            {
                await WriteJsonAsync(context.Response, nightlyResponse, nightlyStatus);
                return;
            }

            var response = path switch
            {
                "/bridge/health" => CreateHealthResponse(),
                "/reports" => Ok(BuildReportIndex()),
                "/operator/dashboard" => BuildOperatorDashboardResponse(),
                "/reports/time-control" => BuildTimeControlResponse(),
                _ => BuildReportResponse(path)
            };

            var statusCode = response.Status == "not_found"
                ? HttpStatusCode.NotFound
                : HttpStatusCode.OK;
            await WriteJsonAsync(context.Response, response, statusCode);
        }
        catch (Exception ex)
        {
            await WriteJsonAsync(
                context.Response,
                Error("bridge_error", $"Read-only bridge error: {ex.Message}"),
                HttpStatusCode.InternalServerError);
        }
    }

    private BridgeResponseModel BuildOperatorDashboardResponse()
    {
        var warnings = new List<string>();
        var dashboard = new Dictionary<string, object?>
        {
            ["reportIndex"] = BuildReportIndex()
        };

        foreach (var report in Reports)
        {
            if (string.Equals(report.Key, "timeControl", StringComparison.OrdinalIgnoreCase))
            {
                var timeControl = BuildTimeControlResponse();
                dashboard[report.Key] = timeControl.Data;
                warnings.AddRange(timeControl.Warnings);
                continue;
            }

            var result = TryReadReport(report);
            dashboard[report.Key] = result.Data;
            warnings.AddRange(result.Warnings);
        }

        return new BridgeResponseModel(
            Status: "available",
            DataSource: "readonly_bridge",
            TimestampUtc: DateTimeOffset.UtcNow,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            Data: dashboard,
            Warnings: warnings);
    }

    private BridgeResponseModel BuildReportResponse(string path)
    {
        var report = Reports.FirstOrDefault(item =>
            string.Equals(item.Endpoint, path, StringComparison.OrdinalIgnoreCase));

        if (report is null)
        {
            return Error("not_found", $"Endpoint is not whitelisted: {path}");
        }

        var result = TryReadReport(report);
        return new BridgeResponseModel(
            Status: result.Available ? "available" : "unavailable",
            DataSource: result.Available ? "readonly_bridge" : "unavailable",
            TimestampUtc: DateTimeOffset.UtcNow,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            Data: result.Data,
            Warnings: result.Warnings);
    }

    private BridgeResponseModel BuildTimeControlResponse()
    {
        var scheduler = new HermesInternalScheduler(_storagePaths, _configPath());
        var status = scheduler.GetTimeControlStatus();
        return Ok(new
        {
            time_zone = status.TimeZone,
            current_utc = status.CurrentUtc,
            current_local = status.CurrentLocal,
            status_label = status.StatusLabel,
            in_work_window = status.InWorkWindow,
            work_window = status.WorkWindow,
            nightly_window = status.NightlyWindow,
            learning_window = status.LearningWindow,
            human_review_window = status.HumanReviewWindow,
            weekdays = status.Weekdays,
            active_weekdays = status.ActiveWeekdays,
            inactive_weekdays = status.InactiveWeekdays,
            config_path = status.ConfigPath,
            no_auto_trading = status.NoAutoTrading,
            human_review_required = status.HumanReviewRequired,
            safety_flags = new[]
            {
                "no_auto_trading=true",
                "human_review_required=true",
                "broker_orders_enabled=false",
                "live_trading_enabled=false",
                "research_only=true",
            },
            warnings = status.Warnings,
        });
    }

    private bool TryHandleReviewAction(
        string path,
        HttpListenerRequest request,
        out BridgeResponseModel response,
        out HttpStatusCode statusCode)
    {
        response = Error("not_found", $"Endpoint is not whitelisted: {path}");
        statusCode = HttpStatusCode.NotFound;

        if (!path.StartsWith("/bridge/review/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var action = path["/bridge/review/".Length..].Trim('/');
        var normalizedDecision = action switch
        {
            "approve-review" => "approved",
            "reject-review" => "rejected",
            "request-more-evidence" => "needs_more_evidence",
            "defer-review" => "deferred",
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(normalizedDecision))
        {
            response = Error("not_found", $"Unsupported review action: {action}");
            statusCode = HttpStatusCode.NotFound;
            return true;
        }

        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = reader.ReadToEnd();
            var payload = JsonNode.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body)?.AsObject();
            var reviewId = payload?["review_id"]?.GetValue<string>() ?? payload?["reviewId"]?.GetValue<string>() ?? string.Empty;
            var note = payload?["note"]?.GetValue<string>() ?? payload?["comment"]?.GetValue<string>() ?? string.Empty;
            var reviewer = payload?["reviewer"]?.GetValue<string>() ?? payload?["decided_by"]?.GetValue<string>() ?? "human";

            if (string.IsNullOrWhiteSpace(reviewId))
            {
                response = Error("invalid_request", "review_id ist erforderlich.");
                statusCode = HttpStatusCode.BadRequest;
                return true;
            }

            var workflow = new HumanReviewWorkflow(_storagePaths);
            var decision = workflow.Decide(reviewId.Trim(), normalizedDecision, note, reviewer);
            var queue = workflow.LoadOrCreateQueue();

            response = Ok(new
            {
                action = action,
                decision = decision.Decision,
                review_id = decision.ReviewId,
                knowledge_item = decision.KnowledgeItemId,
                domain = decision.Domain,
                note = decision.Note,
                timestamp_utc = decision.DecidedAtUtc,
                queue_path = workflow.QueuePath,
                decisions_path = workflow.DecisionsPath,
                learning_feedback_path = workflow.LearningFeedbackPath,
                queue = new
                {
                    pending = queue.PendingReviews,
                    approved = queue.ApprovedReviews,
                    rejected = queue.RejectedReviews,
                    needs_more_evidence = queue.NeedsMoreEvidenceReviews,
                    deferred = queue.DeferredReviews,
                },
                safety_flags = new[]
                {
                    "no_auto_trading=true",
                    "human_review_required=true",
                    "broker_orders_enabled=false",
                    "live_trading_enabled=false",
                    "research_only=true",
                }
            });
            statusCode = HttpStatusCode.OK;
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException or IOException or UnauthorizedAccessException)
        {
            response = Error("review_action_failed", ex.Message);
            statusCode = HttpStatusCode.BadRequest;
            return true;
        }
    }

    private bool TryHandleTimeControlAction(
        string path,
        HttpListenerRequest request,
        out BridgeResponseModel response,
        out HttpStatusCode statusCode)
    {
        response = Error("not_found", $"Endpoint is not whitelisted: {path}");
        statusCode = HttpStatusCode.NotFound;

        if (!AllowedWriteEndpoints.Contains(path))
        {
            return false;
        }

        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = reader.ReadToEnd();
            var payload = JsonNode.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body)?.AsObject();
            var update = new ScheduleTimeControlUpdate(
                TimeZone: payload?["time_zone"]?.GetValue<string>() ?? payload?["timeZone"]?.GetValue<string>(),
                WorkWindow: ParseWindow(payload?["work_window"]?.AsObject() ?? payload?["workWindow"]?.AsObject()),
                NightlyWindow: ParseWindow(payload?["nightly_window"]?.AsObject() ?? payload?["nightlyWindow"]?.AsObject()),
                LearningWindow: ParseWindow(payload?["learning_window"]?.AsObject() ?? payload?["learningWindow"]?.AsObject()),
                HumanReviewWindow: ParseWindow(payload?["human_review_window"]?.AsObject() ?? payload?["humanReviewWindow"]?.AsObject()),
                ActiveWeekdays: payload?["active_weekdays"]?.AsArray()?.Select(node => node?.GetValue<string>() ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).ToList()
                    ?? payload?["activeWeekdays"]?.AsArray()?.Select(node => node?.GetValue<string>() ?? string.Empty).Where(value => !string.IsNullOrWhiteSpace(value)).ToList());

            var scheduler = new HermesInternalScheduler(_storagePaths, _configPath());
            var updatedConfig = scheduler.UpdateTimeControl(update);
            var status = updatedConfig.BuildTimeControlStatus(DateTimeOffset.UtcNow, _configPath());

            response = Ok(new
            {
                action = "update_time_control",
                updated_at_utc = DateTimeOffset.UtcNow,
                config_path = status.ConfigPath,
                status_label = status.StatusLabel,
                in_work_window = status.InWorkWindow,
                time_zone = status.TimeZone,
                work_window = status.WorkWindow,
                nightly_window = status.NightlyWindow,
                learning_window = status.LearningWindow,
                human_review_window = status.HumanReviewWindow,
                active_weekdays = status.ActiveWeekdays,
                inactive_weekdays = status.InactiveWeekdays,
                safety_flags = new[]
                {
                    "no_auto_trading=true",
                    "human_review_required=true",
                    "broker_orders_enabled=false",
                    "live_trading_enabled=false",
                    "research_only=true",
                }
            });
            statusCode = HttpStatusCode.OK;
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException or IOException or UnauthorizedAccessException)
        {
            response = Error("time_control_update_failed", ex.Message);
            statusCode = HttpStatusCode.BadRequest;
            return true;
        }
    }

    private bool TryHandleWorkAreaExecutionAction(
        string path,
        HttpListenerRequest request,
        out BridgeResponseModel response,
        out HttpStatusCode statusCode)
    {
        response = Error("not_found", $"Endpoint is not whitelisted: {path}");
        statusCode = HttpStatusCode.NotFound;

        if (!path.Equals("/bridge/execute-work-areas", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var policyService = new WorkAreaExecutorPolicyService(_storagePaths, Path.Combine(_runtimeRoot, "config", "work_area_executor_policy.json"));
            var report = policyService.Execute();
            response = Ok(new
            {
                report_version = report.ReportVersion,
                updated_at_utc = report.UpdatedAtUtc,
                config_path = report.ConfigPath,
                time_control_path = report.TimeControlPath,
                resource_path = report.ResourcePath,
                in_work_window = report.InWorkWindow,
                in_nightly_window = report.InNightlyWindow,
                resource_healthy = report.ResourceHealthy,
                active_areas = report.ActiveAreas,
                active_improvements = report.ActiveImprovements,
                frank_items = report.FrankItems,
                work_areas = report.WorkAreas,
                warnings = report.Warnings,
                no_trading_execution = report.NoTradingExecution,
                no_broker_action = report.NoBrokerAction,
                no_auto_trading = report.NoAutoTrading,
                human_review_required = report.HumanReviewRequired,
                report_path = report.ReportPath,
                markdown_path = report.MarkdownPath,
            });
            statusCode = HttpStatusCode.OK;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            response = Error("work_area_execution_failed", ex.Message);
            statusCode = HttpStatusCode.BadRequest;
            return true;
        }
    }

    private bool TryHandleNightlyWorkAreaAction(
        string path,
        HttpListenerRequest request,
        out BridgeResponseModel response,
        out HttpStatusCode statusCode)
    {
        response = Error("not_found", $"Endpoint is not whitelisted: {path}");
        statusCode = HttpStatusCode.NotFound;

        if (!path.Equals("/bridge/run-nightly-work-areas", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var service = new NightlyWorkAreaRunnerService(_storagePaths, Path.Combine(_runtimeRoot, "config", "work_area_executor_policy.json"));
            var report = service.Run();
            response = Ok(new
            {
                report_version = report.ReportVersion,
                updated_at_utc = report.UpdatedAtUtc,
                time_control_path = report.TimeControlPath,
                resource_path = report.ResourcePath,
                in_nightly_window = report.InNightlyWindow,
                resource_healthy = report.ResourceHealthy,
                revalidation = report.Revalidation,
                warnings = report.Warnings,
                no_trading_execution = report.NoTradingExecution,
                no_broker_action = report.NoBrokerAction,
                no_auto_trading = report.NoAutoTrading,
                human_review_required = report.HumanReviewRequired,
                report_path = report.ReportPath,
                markdown_path = report.MarkdownPath,
            });
            statusCode = HttpStatusCode.OK;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            response = Error("nightly_work_area_failed", ex.Message);
            statusCode = HttpStatusCode.BadRequest;
            return true;
        }
    }

    private bool TryHandleBotSpecAction(
        string path,
        HttpListenerRequest request,
        out BridgeResponseModel response,
        out HttpStatusCode statusCode)
    {
        response = Error("not_found", $"Endpoint is not whitelisted: {path}");
        statusCode = HttpStatusCode.NotFound;

        if (!string.Equals(path, "/bridge/bot-spec/export", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            var body = reader.ReadToEnd();
            var payload = JsonNode.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body)?.AsObject();
            var candidateId = payload?["candidate_id"]?.GetValue<string>() ?? payload?["candidateId"]?.GetValue<string>() ?? string.Empty;
            var asset = payload?["asset"]?.GetValue<string>() ?? string.Empty;
            var setupId = payload?["setup_id"]?.GetValue<string>() ?? payload?["setupId"]?.GetValue<string>() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(candidateId))
            {
                response = Error("invalid_request", "candidate_id ist erforderlich.");
                statusCode = HttpStatusCode.BadRequest;
                return true;
            }

            var normalizedCandidateId = candidateId.Trim();
            if (!normalizedCandidateId.StartsWith("scalp_", StringComparison.OrdinalIgnoreCase)
                || normalizedCandidateId.Contains('/', StringComparison.Ordinal)
                || normalizedCandidateId.Contains('\\', StringComparison.Ordinal))
            {
                response = Error("invalid_request", "candidate_id ist nicht erlaubt.");
                statusCode = HttpStatusCode.BadRequest;
                return true;
            }

            var result = new ScalpingResearchService(_storagePaths, _runtimeRoot).ExportCTraderBotSpec(normalizedCandidateId);

            response = Ok(new
            {
                action = "export_ctrader_bot_specification",
                candidate_id = normalizedCandidateId,
                asset = asset,
                setup_id = setupId,
                json_path = DisplayPath(result.JsonPath),
                markdown_path = DisplayPath(result.MarkdownPath),
                generated_at_utc = DateTimeOffset.UtcNow,
                output_type = "specification_only",
                contains_bot_code = false,
                contains_order_api = false,
                safety_flags = new[]
                {
                    "no_auto_trading=true",
                    "human_review_required=true",
                    "broker_orders_enabled=false",
                    "live_trading_enabled=false",
                    "research_only=true",
                    "specification_only=true",
                    "no_ctrader_order_api=true",
                }
            });
            statusCode = HttpStatusCode.OK;
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException or IOException or UnauthorizedAccessException)
        {
            response = Error("bot_spec_export_failed", ex.Message);
            statusCode = HttpStatusCode.BadRequest;
            return true;
        }
    }

    private ReportIndex BuildReportIndex()
    {
        return new ReportIndex(
            TimestampUtc: DateTimeOffset.UtcNow,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            Reports: Reports.Select(report =>
            {
                var path = ResolveReadablePath(report.RelativePath, allowFallback: !IsMasterStatusReport(report.Key));
                var info = File.Exists(path) ? new FileInfo(path) : null;

                return new ReportIndexItem(
                    Key: report.Key,
                    Label: report.Label,
                    Endpoint: report.Endpoint,
                    Available: info?.Exists == true,
                    UpdatedAtUtc: info?.Exists == true ? info.LastWriteTimeUtc : null,
                    SizeBytes: info?.Exists == true ? info.Length : null);
            }).ToArray());
    }

    private ReportReadResult TryReadReport(ReportDefinition report)
    {
        var path = ResolveReadablePath(report.RelativePath, allowFallback: !IsMasterStatusReport(report.Key));
        if (!File.Exists(path))
        {
            return new ReportReadResult(
                Available: false,
                Data: null,
                Warnings: [$"{report.Label} nicht gefunden oder noch nicht erzeugt."]);
        }

        try
        {
            var json = File.ReadAllText(path);
            var node = JsonNode.Parse(json);
            Sanitize(node);

            var warnings = new List<string>();
            if (IsLegacySnapshotPath(path))
            {
                warnings.Add($"{report.Label} legacy_snapshot_candidate");
            }

            return new ReportReadResult(
                Available: true,
                Data: node,
                Warnings: warnings);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new ReportReadResult(
                Available: false,
                Data: null,
                Warnings: [$"{report.Label} nicht lesbar: {ex.Message}"]);
        }
    }

    private string GetWhitelistedPath(string relativePath)
    {
        var root = Path.GetFullPath(_storagePaths.Root);
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootWithSeparator, StringComparison.Ordinal)
            && !string.Equals(fullPath, root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Report path escaped Hermes data root.");
        }

        return fullPath;
    }

    private string ResolveReadablePath(string relativePath, bool allowFallback = true)
    {
        var primary = GetWhitelistedPath(relativePath);
        if (File.Exists(primary))
        {
            return primary;
        }

        if (!allowFallback)
        {
            return primary;
        }

        var fallback = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", relativePath));
        return File.Exists(fallback) ? fallback : primary;
    }

    private static bool IsMasterStatusReport(string key) =>
        string.Equals(key, "masterStatus", StringComparison.OrdinalIgnoreCase);

    private static bool IsLegacySnapshotPath(string path) =>
        path.Contains(Path.Combine(".codex_artifacts", "reports", "master-status"), StringComparison.OrdinalIgnoreCase)
        || path.Contains(Path.Combine(".codex_artifacts", "reports"), StringComparison.OrdinalIgnoreCase);

    private static void Sanitize(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject jsonObject:
                foreach (var key in jsonObject.Select(property => property.Key).ToArray())
                {
                    if (IsSecretKey(key))
                    {
                        jsonObject[key] = "[redacted]";
                        continue;
                    }

                    if (IsPathKey(key))
                    {
                        jsonObject[key] = jsonObject[key] is JsonArray
                            ? new JsonArray("[redacted_path]")
                            : "[redacted_path]";
                        continue;
                    }

                    Sanitize(jsonObject[key]);
                }
                break;
            case JsonArray jsonArray:
                var originalCount = jsonArray.Count;
                foreach (var item in jsonArray.Take(MaxArrayItems).ToArray())
                {
                    Sanitize(item);
                }

                while (jsonArray.Count > MaxArrayItems)
                {
                    jsonArray.RemoveAt(jsonArray.Count - 1);
                }

                if (originalCount > MaxArrayItems)
                {
                    jsonArray.Add(new JsonObject
                    {
                        ["truncated"] = true,
                        ["omitted_count"] = originalCount - MaxArrayItems
                    });
                }
                break;
        }
    }

    private static bool IsSecretKey(string key)
    {
        var normalized = key.Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
        return SecretKeys.Any(secretKey =>
            string.Equals(normalized, secretKey, StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(secretKey, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPathKey(string key)
    {
        var normalized = key.Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized == "path"
            || normalized.EndsWith("_path", StringComparison.Ordinal)
            || normalized.EndsWith("_paths", StringComparison.Ordinal)
            || normalized == "protected_paths"
            || normalized == "input_files";
    }

    private static BridgeResponseModel Ok(object data)
    {
        return new BridgeResponseModel(
            Status: "available",
            DataSource: "readonly_bridge",
            TimestampUtc: DateTimeOffset.UtcNow,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            Data: data,
            Warnings: []);
    }

    private static BridgeResponseModel Error(string status, string warning)
    {
        return new BridgeResponseModel(
            Status: status,
            DataSource: "unavailable",
            TimestampUtc: DateTimeOffset.UtcNow,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            Data: null,
            Warnings: [warning]);
    }

    private string _configPath() => Path.Combine(_runtimeRoot, "config", "schedules.json");

    private static SchedulerWindowConfig? ParseWindow(JsonObject? window)
    {
        if (window is null || window.Count == 0)
        {
            return null;
        }

        return new SchedulerWindowConfig(
            Start: window["start"]?.GetValue<string>() ?? window["startTime"]?.GetValue<string>() ?? "00:00",
            End: window["end"]?.GetValue<string>() ?? window["endTime"]?.GetValue<string>() ?? "00:00",
            Enabled: window["enabled"]?.GetValue<bool?>() ?? true);
    }

    private static async Task WriteJsonAsync(
        HttpListenerResponse response,
        BridgeResponseModel model,
        HttpStatusCode statusCode)
    {
        response.StatusCode = (int)statusCode;
        response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(response.OutputStream, model, JsonDefaults.WriteOptions);
        response.Close();
    }

    private static void ApplyHeaders(HttpListenerResponse response, HttpListenerRequest request)
    {
        var origin = request.Headers["Origin"];
        response.Headers["Access-Control-Allow-Origin"] =
            IsLocalOrigin(origin) ? origin! : "http://127.0.0.1:5173";
        response.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
        response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        response.Headers["Cache-Control"] = "no-store";
    }

    private static bool IsLocalOrigin(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            return false;
        }

        return origin.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || origin.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePrefix(string urlPrefix)
    {
        var prefix = string.IsNullOrWhiteSpace(urlPrefix)
            ? "http://127.0.0.1:8787/"
            : urlPrefix.Trim();

        return prefix.EndsWith("/", StringComparison.Ordinal) ? prefix : prefix + "/";
    }

    private static string DisplayPath(string path)
    {
        return path.Replace('\\', '/');
    }

    private sealed record ReportDefinition(
        string Key,
        string Label,
        string Endpoint,
        string RelativePath);

    private sealed record ReportReadResult(
        bool Available,
        object? Data,
        IReadOnlyList<string> Warnings);
}
