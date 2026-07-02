using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hermes.Runtime;

public sealed record KnowledgeRuntimeManagerMetricSnapshot(
    int TrustedKnowledge,
    int PromisingKnowledge,
    int WeakKnowledge,
    int ContradictionCount,
    int ValidationPlansOpen);

public sealed record KnowledgeRuntimeManagerPhaseResult(
    string Phase,
    string Command,
    string Status,
    string PhaseEffect,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    long DurationMs,
    bool Executed,
    KnowledgeRuntimeManagerMetricSnapshot Before,
    KnowledgeRuntimeManagerMetricSnapshot After,
    IReadOnlyDictionary<string, int> MetricChanges,
    IReadOnlyList<string> Warnings,
    string? Details = null);

public sealed record KnowledgeRuntimeManagerReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int MaxActions,
    int DiagnosticsCount,
    string? ReasoningTopic,
    double? ReasoningConfidence,
    KnowledgeRuntimeManagerMetricSnapshot Before,
    KnowledgeRuntimeManagerMetricSnapshot After,
    int ActionsExecuted,
    int ActionsSkipped,
    long ExecutionTimeMs,
    string SafetyStatus,
    string NextRecommendedAction,
    string SelectedNextPhaseReason,
    bool SkippedDueToRecentNoEffect,
    bool SuppressedDueToDependencyNoEffect,
    string NoEffectDependencyChain,
    string NextNonBlockedPhase,
    IReadOnlyList<KnowledgeRuntimeManagerPhaseResult> Phases,
    IReadOnlyList<string> SuppressedRecommendations,
    IReadOnlyList<string> Warnings,
    string DiagnosticsReportPath,
    string UsageAuditReportPath,
    string ImpactReportPath,
    string TimestampRepairReportPath,
    string MissingReferenceRepairReportPath,
    string SupervisorReportPath,
    string AdvancementReportPath,
    string PromotionReportPath,
    string MasterStatusPath,
    string ReportPath,
    string MarkdownPath,
    bool ResearchOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool Executed);

public sealed class KnowledgeRuntimeManagerService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public KnowledgeRuntimeManagerService(StoragePaths storagePaths, string? runtimeRoot = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot ?? Directory.GetCurrentDirectory();
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_runtime_manager");

    public string ReportPath => Path.Combine(Root, "knowledge_runtime_manager_report.json");

    public string MarkdownPath => Path.Combine(Root, "knowledge_runtime_manager_report.md");

    public string DiagnosticsReportPath => Path.Combine(_storagePaths.Root, "reports", "knowledge_state_repair_diagnostics", "knowledge_state_repair_diagnostics_report.json");

    public string UsageAuditReportPath => Path.Combine(_storagePaths.Root, "reports", "trusted_knowledge_usage_audit", "trusted_knowledge_usage_audit_report.json");

    public string ImpactReportPath => Path.Combine(_storagePaths.Root, "reports", "trusted_knowledge_impact", "trusted_knowledge_impact_report.json");

    public string TimestampRepairReportPath => Path.Combine(_storagePaths.Root, "reports", "knowledge_state_timestamp_repair", "knowledge_state_timestamp_repair_report.json");

    public string MissingReferenceRepairReportPath => Path.Combine(_storagePaths.Root, "reports", "knowledge_state_missing_item_reference_repair", "knowledge_state_missing_item_reference_repair_report.json");

    public string SupervisorReportPath => Path.Combine(_storagePaths.Root, "reports", "autonomous_knowledge_supervisor", "autonomous_knowledge_supervisor_report.json");

    public string AdvancementReportPath => Path.Combine(_storagePaths.Root, "reports", "autonomous_knowledge_advancement", "autonomous_knowledge_advancement_report.json");

    public string PromotionReportPath => Path.Combine(_storagePaths.Root, "reports", "knowledge_trust_promotion", "knowledge_trust_promotion_report.json");

    public string MasterStatusPath => Path.Combine(_storagePaths.Root, "reports", "master-status", "master_status.json");

    public KnowledgeRuntimeManagerReport Run(int maxActions = 1, bool execute = false)
    {
        Directory.CreateDirectory(Root);

        var startedAt = DateTimeOffset.UtcNow;
        var actionBudget = Math.Clamp(maxActions, 1, 5);
        var actionTimeout = TimeSpan.FromSeconds(120);
        var latestReport = LoadLatestReport();
        var lastNoEffectPhase = latestReport?.Phases.LastOrDefault(phase => string.Equals(phase.PhaseEffect, "no_metric_change", StringComparison.OrdinalIgnoreCase) && phase.Executed)?.Phase;
        var lastNoEffectExecutedPhase = latestReport?.Phases.LastOrDefault(phase => string.Equals(phase.PhaseEffect, "no_metric_change", StringComparison.OrdinalIgnoreCase) && phase.Executed);

        var diagnosticsService = new KnowledgeStateRepairDiagnosticsService(_storagePaths);
        var usageAuditService = new TrustedKnowledgeUsageAuditService(_storagePaths, _runtimeRoot);
        var impactService = new TrustedKnowledgeImpactService(_storagePaths, _runtimeRoot);
        var timestampRepairService = new KnowledgeStateTimestampRepairService(_storagePaths);
        var missingReferenceRepairService = new KnowledgeStateMissingItemReferenceRepairService(_storagePaths);
        var supervisorService = new AutonomousKnowledgeSupervisorService(_storagePaths, _runtimeRoot);
        var advancementService = new AutonomousKnowledgeAdvancementEngineService(_storagePaths, _runtimeRoot);
        var promotionService = new KnowledgeTrustPromotionPipelineService(_storagePaths);
        var masterStatusWriter = new MasterStatusWriter(new MasterStatusService(_storagePaths, _runtimeRoot));
        var knowledgeQualityEngine = new KnowledgeQualityEngine(_storagePaths);

        var diagnostics = diagnosticsService.Run();
        var usageAudit = usageAuditService.Run();
        var impact = impactService.Run();

        var reasoningTopic = usageAudit.UsedTopics.FirstOrDefault()
            ?? impact.Topics.FirstOrDefault();
        KnowledgeReasoningReport? reasoning = null;
        if (!string.IsNullOrWhiteSpace(reasoningTopic))
        {
            reasoning = new KnowledgeReasoningService(_storagePaths).Run(reasoningTopic!);
        }

        var currentMasterSnapshot = LoadCurrentSnapshot(masterStatusWriter);
        var before = BuildSnapshot(currentMasterSnapshot);

        var phases = new List<KnowledgeRuntimeManagerPhaseResult>();
        var warnings = new List<string>();
        var suppressedRecommendations = new List<string>();
        var noEffectDependencyChain = string.Empty;
        var suppressedDueToDependencyNoEffect = false;
        var actionsExecuted = 0;
        var actionsSkipped = 0;
        var halted = false;

        void RecordPhase(
            string phase,
            string command,
            bool shouldExecute,
            Func<(string Status, object? Report, IReadOnlyList<string> Warnings, string? Details)> action,
            Func<MasterStatusSnapshot>? afterOverride = null)
        {
            var phaseStartedAt = DateTimeOffset.UtcNow;
            var beforeMasterSnapshot = currentMasterSnapshot;
            var beforeSnapshot = BuildSnapshot(beforeMasterSnapshot);

            var status = "skipped";
            var details = (string?)null;
            var phaseWarnings = new List<string>();
            var afterMasterSnapshot = beforeMasterSnapshot;
            var afterSnapshot = beforeSnapshot;
            var executed = false;
            var timedOut = false;

            var suppressedByCooldown = !string.IsNullOrWhiteSpace(lastNoEffectPhase)
                && phase.Equals(lastNoEffectPhase, StringComparison.OrdinalIgnoreCase);

            if (suppressedByCooldown)
            {
                phaseWarnings.Add("skipped_due_to_recent_no_effect");
                suppressedRecommendations.Add($"{phase}: recent no_metric_change cooldown");
            }

            if (!halted && !suppressedByCooldown && shouldExecute && actionsExecuted < actionBudget)
            {
                executed = true;
                actionsExecuted++;
                var actionTask = Task.Run(action);
                if (!actionTask.Wait(actionTimeout))
                {
                    timedOut = true;
                    halted = true;
                    status = "blocked_action_timeout";
                    details = $"action timed out after {actionTimeout.TotalSeconds:0} seconds";
                    phaseWarnings.Add("action_timeout");
                }
                else
                {
                    try
                    {
                        var result = actionTask.Result;
                        status = result.Status;
                        details = result.Details;
                        phaseWarnings.AddRange(result.Warnings);
                        afterMasterSnapshot = afterOverride?.Invoke() ?? LoadCurrentSnapshot(masterStatusWriter);
                        currentMasterSnapshot = afterMasterSnapshot;
                        afterSnapshot = BuildSnapshot(afterMasterSnapshot);
                    }
                    catch (Exception ex)
                    {
                        halted = true;
                        status = "blocked_action_failed";
                        details = ex.Message;
                        phaseWarnings.Add("action_failed");
                    }
                }
            }
            else
            {
                actionsSkipped++;
                if (!suppressedByCooldown)
                {
                    phaseWarnings.Add(halted ? "skipped_due_to_previous_timeout" : "skipped_by_budget_or_not_needed");
                }
            }

            if (timedOut)
            {
                warnings.Add($"{phase}:timeout");
            }

            var phaseEffect = BuildPhaseEffect(beforeSnapshot, afterSnapshot);

            var phaseCompletedAt = DateTimeOffset.UtcNow;
            phases.Add(new KnowledgeRuntimeManagerPhaseResult(
                Phase: phase,
                Command: command,
                Status: status,
                PhaseEffect: phaseEffect,
                StartedAtUtc: phaseStartedAt,
                CompletedAtUtc: phaseCompletedAt,
                DurationMs: Math.Max(0, (long)(phaseCompletedAt - phaseStartedAt).TotalMilliseconds),
                Executed: executed,
                Before: beforeSnapshot,
                After: afterSnapshot,
                MetricChanges: BuildMetricChanges(beforeSnapshot, afterSnapshot),
                Warnings: phaseWarnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Details: details));
        }

        var timestampCandidates = diagnostics.Items
            .Where(item => item.MismatchType.Equals("timestamp_mismatch", StringComparison.OrdinalIgnoreCase) && item.AutoRepairable)
            .Select(item => item.KnowledgeItemId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var missingReferenceCandidates = LoadConsistencyReport()?.Items
            .Where(item => item.MissingItemIdMismatch)
            .Select(item => item.KnowledgeId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        RecordPhase(
            phase: "timestamp_repair",
            command: "knowledge-state-timestamp-repair --apply",
            shouldExecute: execute && timestampCandidates.Count > 0,
            action: () =>
            {
                var report = timestampRepairService.Run(apply: true, dryRun: false);
                return ("applied", report, report.Warnings, report.RepairedIssues > 0 ? $"repaired={report.RepairedIssues}" : "no_repairs");
            });

        RecordPhase(
            phase: "missing_reference_repair",
            command: "knowledge-state-reference-rebuild --apply",
            shouldExecute: execute && missingReferenceCandidates.Count > 0,
            action: () =>
            {
                var report = missingReferenceRepairService.Run(apply: true, dryRun: false, targetIds: missingReferenceCandidates);
                return ("applied", report, report.Warnings, report.RepairedItems > 0 ? $"repaired={report.RepairedItems}" : "no_repairs");
            });

        RecordPhase(
            phase: "supervisor_step",
            command: "autonomous-knowledge-supervisor-step --max-actions 1",
            shouldExecute: execute,
            action: () =>
            {
                var report = supervisorService.Run(maxActions: 1, execute: true);
                return (report.Executed ? "executed" : "idle", report, report.Warnings, report.SelectedBacklogClass);
            });

        RecordPhase(
            phase: "advancement_execute",
            command: "autonomous-knowledge-advancement --execute",
            shouldExecute: execute,
            action: () =>
            {
                var report = advancementService.Run(maxItems: 12, execute: true);
                return (report.Executed ? "executed" : "idle", report, report.Warnings, report.RootCauseSummary);
            });

        var promotionReportPreview = promotionService.Run(apply: false);
        var eligiblePromotions = promotionReportPreview.EligibleForPromotion;
        RecordPhase(
            phase: "promotion_apply",
            command: "knowledge-trust-promote --apply --skip-refresh",
            shouldExecute: execute && eligiblePromotions > 0,
            action: () =>
            {
                var report = promotionService.Run(apply: true, skipRefresh: true, maxSeconds: 60);
                return (report.AppliedCount > 0 ? "applied" : "no_changes", report, report.Warnings, $"eligible={report.EligibleForPromotion}; applied={report.AppliedCount}");
            });

        RecordPhase(
            phase: "knowledge_snapshot",
            command: "master-status-refresh --knowledge-only",
            shouldExecute: execute,
            action: () =>
            {
                var qualityReport = knowledgeQualityEngine.LoadOrCreateReport();
                var refreshed = masterStatusWriter.WriteKnowledgeOnlySnapshot(qualityReport);
                currentMasterSnapshot = refreshed;
                return ("refreshed", refreshed, [], $"trusted={refreshed.TrustedKnowledge}; weak={refreshed.WeakKnowledge}");
            },
            afterOverride: () => currentMasterSnapshot);

        var afterSnapshot = currentMasterSnapshot;
        var after = BuildSnapshot(afterSnapshot);
        var executionTimeMs = Math.Max(0, (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds);
        var status = execute
            ? (actionsExecuted > 0 ? "executed" : "idle")
            : "planned";

        var nextRecommendationContext = BuildRecommendationContext(
            diagnostics,
            promotionReportPreview,
            supervisorService,
            afterSnapshot,
            phases,
            lastNoEffectPhase,
            lastNoEffectExecutedPhase);
        var nextRecommendedAction = nextRecommendationContext.NextRecommendedAction;
        suppressedDueToDependencyNoEffect = nextRecommendationContext.SuppressedDueToDependencyNoEffect;
        noEffectDependencyChain = nextRecommendationContext.NoEffectDependencyChain;
        var report = new KnowledgeRuntimeManagerReport(
            ReportVersion: "knowledge_runtime_manager_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            MaxActions: actionBudget,
            DiagnosticsCount: diagnostics.TotalIssues,
            ReasoningTopic: reasoning?.Topic ?? reasoningTopic,
            ReasoningConfidence: reasoning?.Confidence,
            Before: before,
            After: after,
            ActionsExecuted: actionsExecuted,
            ActionsSkipped: actionsSkipped,
            ExecutionTimeMs: executionTimeMs,
            SafetyStatus: BuildSafetyStatus(),
            NextRecommendedAction: nextRecommendedAction,
            SelectedNextPhaseReason: BuildSelectedNextPhaseReason(nextRecommendedAction, lastNoEffectPhase, diagnostics, promotionReportPreview, suppressedDueToDependencyNoEffect),
            SkippedDueToRecentNoEffect: !string.IsNullOrWhiteSpace(lastNoEffectPhase),
            SuppressedDueToDependencyNoEffect: suppressedDueToDependencyNoEffect,
            NoEffectDependencyChain: noEffectDependencyChain,
            NextNonBlockedPhase: nextRecommendationContext.NextNonBlockedPhase,
            Phases: phases,
            SuppressedRecommendations: suppressedRecommendations.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings: CombineWarnings(diagnostics.Warnings, usageAudit.Warnings, impact.Warnings, reasoning?.Warnings ?? [], suppressedRecommendations),
            DiagnosticsReportPath: DiagnosticsReportPath,
            UsageAuditReportPath: UsageAuditReportPath,
            ImpactReportPath: ImpactReportPath,
            TimestampRepairReportPath: TimestampRepairReportPath,
            MissingReferenceRepairReportPath: MissingReferenceRepairReportPath,
            SupervisorReportPath: SupervisorReportPath,
            AdvancementReportPath: AdvancementReportPath,
            PromotionReportPath: PromotionReportPath,
            MasterStatusPath: MasterStatusPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            ResearchOnly: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            Executed: execute);

        WriteReport(report);
        return report;
    }

    public KnowledgeRuntimeManagerReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeRuntimeManagerReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private MasterStatusSnapshot LoadCurrentSnapshot(MasterStatusWriter writer)
    {
        return writer.LoadSnapshot() ?? new MasterStatusService(_storagePaths, _runtimeRoot).BuildSnapshot();
    }

    private static KnowledgeRuntimeManagerMetricSnapshot BuildSnapshot(MasterStatusSnapshot snapshot)
    {
        return new KnowledgeRuntimeManagerMetricSnapshot(
            TrustedKnowledge: snapshot.TrustedKnowledge,
            PromisingKnowledge: snapshot.TrustDistribution.TryGetValue("promising", out var promising) ? promising : 0,
            WeakKnowledge: snapshot.WeakKnowledge,
            ContradictionCount: snapshot.ContradictionCount,
            ValidationPlansOpen: snapshot.ValidationPlansOpen);
    }

    private static IReadOnlyDictionary<string, int> BuildMetricChanges(KnowledgeRuntimeManagerMetricSnapshot before, KnowledgeRuntimeManagerMetricSnapshot after)
    {
        return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["trusted_knowledge"] = after.TrustedKnowledge - before.TrustedKnowledge,
            ["promising_knowledge"] = after.PromisingKnowledge - before.PromisingKnowledge,
            ["weak_knowledge"] = after.WeakKnowledge - before.WeakKnowledge,
            ["contradiction_count"] = after.ContradictionCount - before.ContradictionCount,
            ["validation_plans_open"] = after.ValidationPlansOpen - before.ValidationPlansOpen
        };
    }

    private static string BuildPhaseEffect(KnowledgeRuntimeManagerMetricSnapshot before, KnowledgeRuntimeManagerMetricSnapshot after)
    {
        return before.TrustedKnowledge == after.TrustedKnowledge
            && before.PromisingKnowledge == after.PromisingKnowledge
            && before.WeakKnowledge == after.WeakKnowledge
            && before.ContradictionCount == after.ContradictionCount
            && before.ValidationPlansOpen == after.ValidationPlansOpen
            ? "no_metric_change"
            : "metric_change";
    }

    private KnowledgeStateConsistencyReport? LoadConsistencyReport()
    {
        var path = Path.Combine(_storagePaths.Root, "reports", "knowledge_state_consistency", "knowledge_state_consistency_report.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeStateConsistencyReport>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static string BuildSafetyStatus() =>
        "research_only=true; no_auto_trading=true; broker_orders_enabled=false; live_trading_enabled=false; human_review_required=true";

    private sealed record KnowledgeRuntimeManagerRecommendationContext(
        string NextRecommendedAction,
        bool SuppressedDueToDependencyNoEffect,
        string NoEffectDependencyChain,
        string NextNonBlockedPhase);

    private static KnowledgeRuntimeManagerRecommendationContext BuildRecommendationContext(
        KnowledgeStateRepairDiagnosticsReport diagnostics,
        KnowledgeTrustPromotionReport promotionReport,
        AutonomousKnowledgeSupervisorService supervisorService,
        MasterStatusSnapshot snapshot,
        IReadOnlyList<KnowledgeRuntimeManagerPhaseResult> phases,
        string? lastNoEffectPhase,
        KnowledgeRuntimeManagerPhaseResult? lastNoEffectExecutedPhase)
    {
        var noEffectPhaseNames = phases
            .Where(phase => string.Equals(phase.PhaseEffect, "no_metric_change", StringComparison.OrdinalIgnoreCase) && phase.Executed)
            .Select(phase => phase.Phase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var hasEligiblePromotions = promotionReport.EligibleForPromotion > 0
            && promotionReport.EligibleForPromotion > promotionReport.PromotedToTrusted;
        var noEffectDependencySuppression = lastNoEffectExecutedPhase is not null
            && (string.Equals(lastNoEffectPhase, "supervisor_step", StringComparison.OrdinalIgnoreCase)
                || string.Equals(lastNoEffectPhase, "advancement_execute", StringComparison.OrdinalIgnoreCase)
                || string.Equals(lastNoEffectPhase, "promotion_apply", StringComparison.OrdinalIgnoreCase));

        string nextRecommendedAction;
        string nextNonBlockedPhase;
        var suppressedDueToDependencyNoEffect = false;
        var dependencyChain = string.Empty;

        if (noEffectDependencySuppression)
        {
            suppressedDueToDependencyNoEffect = true;
            dependencyChain = string.Equals(lastNoEffectPhase, "supervisor_step", StringComparison.OrdinalIgnoreCase)
                ? "supervisor_step -> advancement_execute -> knowledge-trust-promote --apply --skip-refresh"
                : string.Equals(lastNoEffectPhase, "advancement_execute", StringComparison.OrdinalIgnoreCase)
                    ? "advancement_execute -> knowledge-trust-promote --apply --skip-refresh"
                    : "promotion_apply -> dependent_followup_suppression";
            if (diagnostics.TotalIssues > 0)
            {
                nextRecommendedAction = "knowledge-state-repair-diagnostics";
                nextNonBlockedPhase = "knowledge-state-repair-diagnostics";
            }
            else if (snapshot.ValidationPlansOpen > 0)
            {
                nextRecommendedAction = "validation-gap";
                nextNonBlockedPhase = "validation-gap";
            }
            else
            {
                nextRecommendedAction = "autonomous-knowledge-advancement --execute";
                nextNonBlockedPhase = "advancement_execute";
            }
        }
        else if (hasEligiblePromotions)
        {
            nextRecommendedAction = "knowledge-trust-promote --apply --skip-refresh";
            nextNonBlockedPhase = "promotion_apply";
        }
        else if (!string.IsNullOrWhiteSpace(lastNoEffectPhase) && noEffectPhaseNames.Contains(lastNoEffectPhase))
        {
            nextRecommendedAction = diagnostics.TotalIssues > 0
                ? "knowledge-state-repair-diagnostics"
                : "validation-gap";
            nextNonBlockedPhase = diagnostics.TotalIssues > 0 ? "diagnostics" : "validation_gap";
        }
        else if (diagnostics.TotalIssues > 0)
        {
            var hasTimestampIssues = diagnostics.Items.Any(item => item.MismatchType.Equals("timestamp_mismatch", StringComparison.OrdinalIgnoreCase) && item.AutoRepairable);
            if (hasTimestampIssues)
            {
                nextRecommendedAction = "knowledge-state-timestamp-repair --apply";
                nextNonBlockedPhase = "timestamp_repair";
            }
            else
            {
                var hasMissingRefs = snapshot.CognitiveStatus.Metrics.TryGetValue("validation_plans_open", out var plansOpen) && plansOpen is int open && open > 0;
                if (hasMissingRefs)
                {
                    nextRecommendedAction = "knowledge-state-reference-rebuild --apply";
                    nextNonBlockedPhase = "missing_reference_repair";
                }
                else
                {
                    nextRecommendedAction = "knowledge-state-repair-diagnostics";
                    nextNonBlockedPhase = "diagnostics";
                }
            }
        }
        else
        {
            var supervisorRecommendation = supervisorService.LoadLatestReport()?.Recommendation;
            nextRecommendedAction = string.IsNullOrWhiteSpace(supervisorRecommendation)
                ? "master-status-refresh --knowledge-only"
                : supervisorRecommendation;
            nextNonBlockedPhase = string.IsNullOrWhiteSpace(supervisorRecommendation) ? "knowledge_snapshot" : supervisorRecommendation;
        }

        if (suppressedDueToDependencyNoEffect && nextRecommendedAction.StartsWith("knowledge-trust-promote", StringComparison.OrdinalIgnoreCase))
        {
            nextRecommendedAction = diagnostics.TotalIssues > 0
                ? "knowledge-state-repair-diagnostics"
                : snapshot.ValidationPlansOpen > 0
                    ? "validation-gap"
                    : "autonomous-knowledge-advancement --execute";
            nextNonBlockedPhase = nextRecommendedAction.Contains("diagnostics", StringComparison.OrdinalIgnoreCase)
                ? "knowledge-state-repair-diagnostics"
                : nextRecommendedAction.Contains("validation-gap", StringComparison.OrdinalIgnoreCase)
                    ? "validation_gap"
                    : "advancement_execute";
        }

        return new KnowledgeRuntimeManagerRecommendationContext(
            nextRecommendedAction,
            suppressedDueToDependencyNoEffect,
            dependencyChain,
            nextNonBlockedPhase);
    }

    private static string BuildSelectedNextPhaseReason(
        string nextRecommendedAction,
        string? lastNoEffectPhase,
        KnowledgeStateRepairDiagnosticsReport diagnostics,
        KnowledgeTrustPromotionReport promotionReport,
        bool suppressedDueToDependencyNoEffect)
    {
        if (suppressedDueToDependencyNoEffect)
        {
            return string.Equals(lastNoEffectPhase, "supervisor_step", StringComparison.OrdinalIgnoreCase)
                ? "supervisor_step:dependent_no_effect_suppression"
                : string.Equals(lastNoEffectPhase, "advancement_execute", StringComparison.OrdinalIgnoreCase)
                    ? "advancement_execute:dependent_no_effect_suppression"
                    : "promotion_apply:dependent_no_effect_suppression";
        }

        if (!string.IsNullOrWhiteSpace(lastNoEffectPhase))
        {
            return $"{lastNoEffectPhase}:suppressed_due_to_recent_no_metric_change";
        }

        if (nextRecommendedAction.StartsWith("knowledge-trust-promote", StringComparison.OrdinalIgnoreCase))
        {
            return promotionReport.EligibleForPromotion > promotionReport.PromotedToTrusted
                ? "promotion_has_new_eligible_candidates"
                : "promotion_candidate_count_exceeds_trusted_count";
        }

        if (diagnostics.TotalIssues > 0)
        {
            return "diagnostics_has_remaining_issues";
        }

        return "snapshot_or_followup_phase";
    }

    private static IReadOnlyList<string> CombineWarnings(params IEnumerable<string>[] groups)
    {
        return groups
            .SelectMany(group => group ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void WriteReport(KnowledgeRuntimeManagerReport report)
    {
        File.WriteAllText(report.ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(report.MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(KnowledgeRuntimeManagerReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Hermes Knowledge Runtime Manager");
        sb.AppendLine();
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- max_actions: {report.MaxActions}");
        sb.AppendLine($"- diagnostics_count: {report.DiagnosticsCount}");
        sb.AppendLine($"- reasoning_topic: {report.ReasoningTopic ?? "-"}");
        sb.AppendLine($"- reasoning_confidence: {report.ReasoningConfidence?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "-"}");
        sb.AppendLine($"- actions_executed: {report.ActionsExecuted}");
        sb.AppendLine($"- actions_skipped: {report.ActionsSkipped}");
        sb.AppendLine($"- execution_time_ms: {report.ExecutionTimeMs}");
        sb.AppendLine($"- safety_status: {report.SafetyStatus}");
        sb.AppendLine($"- next_recommended_action: {report.NextRecommendedAction}");
        sb.AppendLine($"- selected_next_phase_reason: {report.SelectedNextPhaseReason}");
        sb.AppendLine($"- skipped_due_to_recent_no_effect: {report.SkippedDueToRecentNoEffect}");
        sb.AppendLine($"- suppressed_due_to_dependency_no_effect: {report.SuppressedDueToDependencyNoEffect}");
        sb.AppendLine($"- no_effect_dependency_chain: {report.NoEffectDependencyChain}");
        sb.AppendLine($"- next_non_blocked_phase: {report.NextNonBlockedPhase}");
        sb.AppendLine();
        sb.AppendLine("## Before");
        WriteMetricBlock(sb, report.Before);
        sb.AppendLine();
        sb.AppendLine("## After");
        WriteMetricBlock(sb, report.After);
        sb.AppendLine();
        sb.AppendLine("## Phases");
        foreach (var phase in report.Phases)
        {
            sb.AppendLine($"### {phase.Phase}");
            sb.AppendLine($"- command: {phase.Command}");
            sb.AppendLine($"- status: {phase.Status}");
            sb.AppendLine($"- executed: {phase.Executed}");
            sb.AppendLine($"- duration_ms: {phase.DurationMs}");
            sb.AppendLine($"- details: {phase.Details ?? "-"}");
            sb.AppendLine($"- before: trusted={phase.Before.TrustedKnowledge}, promising={phase.Before.PromisingKnowledge}, weak={phase.Before.WeakKnowledge}, contradictions={phase.Before.ContradictionCount}, validation_plans_open={phase.Before.ValidationPlansOpen}");
            sb.AppendLine($"- after: trusted={phase.After.TrustedKnowledge}, promising={phase.After.PromisingKnowledge}, weak={phase.After.WeakKnowledge}, contradictions={phase.After.ContradictionCount}, validation_plans_open={phase.After.ValidationPlansOpen}");
            if (phase.Warnings.Count > 0)
            {
                sb.AppendLine($"- warnings: {string.Join(", ", phase.Warnings)}");
            }
        }

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
        }

        return sb.ToString();
    }

    private static void WriteMetricBlock(StringBuilder sb, KnowledgeRuntimeManagerMetricSnapshot snapshot)
    {
        sb.AppendLine($"- trusted_knowledge: {snapshot.TrustedKnowledge}");
        sb.AppendLine($"- promising_knowledge: {snapshot.PromisingKnowledge}");
        sb.AppendLine($"- weak_knowledge: {snapshot.WeakKnowledge}");
        sb.AppendLine($"- contradiction_count: {snapshot.ContradictionCount}");
        sb.AppendLine($"- validation_plans_open: {snapshot.ValidationPlansOpen}");
    }
}
