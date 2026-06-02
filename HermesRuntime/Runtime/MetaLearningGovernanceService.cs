using System.Text.Json;

namespace Hermes.Runtime;

public sealed class MetaReviewEngine
{
    private readonly StoragePaths _storagePaths;

    public MetaReviewEngine(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string MetaReviewPath => Path.Combine(Root, "meta_review.json");

    public string DomainHealthPath => Path.Combine(Root, "domain_health.json");

    public string LearningStrategyPath => Path.Combine(Root, "learning_strategy.json");

    public MetaReviewResult RunReview()
    {
        Directory.CreateDirectory(Root);
        var evaluator = new TaskOutcomeEvaluator(_storagePaths);
        var outcomes = evaluator.LoadOutcomes(5000);
        var plannerFeedback = evaluator.LoadOrCreatePlannerFeedback();
        var goalFeedback = evaluator.LoadOrCreateGoalFeedback();
        var goalProgress = LoadGoalProgress();
        var sources = new KnowledgeSourceRegistry(_storagePaths).LoadOrCreateSources();
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var queue = new ResearchQueueService(_storagePaths).LoadOrCreateQueue();
        var needs = new NeedDetectionEngine(_storagePaths).LoadNeeds();
        var domains = BuildDomainHealth(sources, catalog, queue, outcomes, plannerFeedback, needs);
        var decisions = EvaluateGovernance(plannerFeedback, queue, domains);
        var strategy = new LearningStrategyManager().SelectStrategy(
            domains,
            goalFeedback,
            plannerFeedback,
            queue,
            needs,
            decisions);
        var observations = BuildObservations(
            goalProgress,
            goalFeedback,
            plannerFeedback,
            outcomes,
            sources,
            catalog,
            queue,
            domains,
            decisions,
            needs);

        var result = new MetaReviewResult(
            ReviewVersion: "meta_review_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: decisions.Any(decision => decision.Status.Equals("block", StringComparison.OrdinalIgnoreCase))
                ? "blocked_by_governance"
                : observations.Any(observation => observation.Severity.Equals("warning", StringComparison.OrdinalIgnoreCase))
                    ? "review_required"
                    : "healthy",
            GoalsReviewed: goalProgress.Count,
            OutcomesReviewed: outcomes.Count,
            PlannerTaskTypesReviewed: plannerFeedback.TaskTypeFeedback.Count,
            KnowledgeItems: catalog.Count,
            ResearchQueueItems: queue.Items.Count,
            Observations: observations,
            ActivitiesWithProgress: ActivitiesWithProgress(plannerFeedback),
            ActivitiesGeneratingWork: ActivitiesGeneratingWork(plannerFeedback),
            StagnantGoals: StagnantGoals(goalProgress, goalFeedback),
            RecurringNeeds: RecurringNeeds(goalFeedback, needs),
            DomainHealth: domains,
            GovernanceDecisions: decisions,
            LearningStrategy: strategy,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteDomainHealth(domains);
        WriteLearningStrategy(strategy);
        File.WriteAllText(MetaReviewPath, JsonSerializer.Serialize(result, JsonDefaults.WriteOptions));
        new CognitiveCoreService(_storagePaths).BuildStatus();
        return result;
    }

    public MetaReviewResult LoadOrCreateReview()
    {
        var existing = LoadReview();
        return existing ?? RunReview();
    }

    public MetaReviewResult? LoadReview()
    {
        if (!File.Exists(MetaReviewPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MetaReviewResult>(
                File.ReadAllText(MetaReviewPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public IReadOnlyList<DomainHealth> LoadOrCreateDomainHealth()
    {
        var existing = LoadDomainHealth();
        if (existing.Count > 0)
        {
            return existing;
        }

        return RunReview().DomainHealth;
    }

    public IReadOnlyList<DomainHealth> LoadDomainHealth()
    {
        if (!File.Exists(DomainHealthPath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<DomainHealth>>(
                File.ReadAllText(DomainHealthPath),
                JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    public LearningStrategy LoadOrCreateLearningStrategy()
    {
        var existing = LoadLearningStrategy();
        return existing ?? RunReview().LearningStrategy;
    }

    public LearningStrategy? LoadLearningStrategy()
    {
        if (!File.Exists(LearningStrategyPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<LearningStrategy>(
                File.ReadAllText(LearningStrategyPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private IReadOnlyList<DomainHealth> BuildDomainHealth(
        IReadOnlyList<CognitiveSource> sources,
        IReadOnlyList<KnowledgeCatalogItem> catalog,
        ResearchQueue queue,
        IReadOnlyList<TaskOutcomeResult> outcomes,
        PlannerFeedback plannerFeedback,
        IReadOnlyList<DetectedNeed> needs)
    {
        var needDomains = needs.ToDictionary(need => need.NeedId, need => need.Domain, StringComparer.OrdinalIgnoreCase);
        return CognitiveCoreService.Domains()
            .Select(domain =>
            {
                var domainSources = sources
                    .Where(source => source.Domain.Equals(domain.DomainId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var domainItems = catalog
                    .Where(item => item.Domain.Equals(domain.DomainId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var domainQueue = queue.Items
                    .Where(item => item.Domain.Equals(domain.DomainId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var domainOutcomes = outcomes
                    .Where(outcome => OutcomeDomain(outcome, needDomains).Equals(domain.DomainId, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var duplicateTitles = domainItems
                    .GroupBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
                    .Count(group => group.Count() > 1);
                var duplicateQueueMarkers = domainQueue
                    .SelectMany(item => item.Notes)
                    .Where(note => note.StartsWith("planned_task:", StringComparison.OrdinalIgnoreCase))
                    .GroupBy(note => note, StringComparer.OrdinalIgnoreCase)
                    .Count(group => group.Count() > 1);
                var domainTaskTypes = domainOutcomes
                    .Select(outcome => outcome.TaskType)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var avgRedundancy = plannerFeedback.TaskTypeFeedback
                    .Where(item => domainTaskTypes.Contains(item.TaskType))
                    .Select(item => item.AverageRedundancyScore)
                    .DefaultIfEmpty(0)
                    .Average();

                var knowledgeCoverage = Math.Clamp(domainItems.Count / (domain.Active ? 12.0 : 4.0), 0, 1);
                var processedQueue = domainQueue.Count(item =>
                    item.Status.Equals("processed", StringComparison.OrdinalIgnoreCase)
                    || item.Status.Equals("completed", StringComparison.OrdinalIgnoreCase));
                var validationCoverage = domainItems.Count == 0
                    ? 0
                    : Math.Clamp((processedQueue + domainOutcomes.Count(outcome => outcome.Evidence.GoalImproved)) / (double)Math.Max(1, domainItems.Count), 0, 1);
                var trust = domainSources
                    .Select(source => source.TrustProfile.TrustScore)
                    .Concat(domainItems.Select(item => item.Confidence))
                    .DefaultIfEmpty(0)
                    .Average();
                var redundancy = Math.Clamp(
                    (duplicateTitles + duplicateQueueMarkers) / (double)Math.Max(1, domainItems.Count + domainQueue.Count)
                    + avgRedundancy * 0.5,
                    0,
                    1);
                var learningVelocity = domainOutcomes.Count == 0
                    ? 0
                    : Math.Clamp(domainOutcomes.OrderByDescending(outcome => outcome.EvaluatedAtUtc)
                        .Take(25)
                        .Average(outcome => outcome.OutcomeScore.LearningValue), 0, 1);
                var overall = Math.Round(Math.Clamp(
                    knowledgeCoverage * 0.22
                    + validationCoverage * 0.24
                    + trust * 0.2
                    + learningVelocity * 0.22
                    + (1 - redundancy) * 0.12,
                    0,
                    1), 4);
                var warnings = DomainWarnings(domain, domainSources.Count, domainItems.Count, domainQueue.Count, validationCoverage, redundancy, learningVelocity);
                var score = new DomainHealthScore(
                    Domain: domain.DomainId,
                    KnowledgeCoverage: Math.Round(knowledgeCoverage, 4),
                    ValidationCoverage: Math.Round(validationCoverage, 4),
                    TrustScore: Math.Round(trust, 4),
                    RedundancyScore: Math.Round(redundancy, 4),
                    LearningVelocity: Math.Round(learningVelocity, 4),
                    OverallScore: overall,
                    Classification: Classification(overall, warnings),
                    Reasons: DomainReasons(domainSources.Count, domainItems.Count, validationCoverage, redundancy, learningVelocity));
                return new DomainHealth(
                    Domain: domain.DomainId,
                    Active: domain.Active,
                    SourceCount: domainSources.Count,
                    KnowledgeItemCount: domainItems.Count,
                    QueueItems: domainQueue.Count,
                    OpenQueueItems: domainQueue.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)),
                    ProcessedQueueItems: processedQueue,
                    OutcomeCount: domainOutcomes.Count,
                    Score: score,
                    Warnings: warnings);
            })
            .OrderByDescending(domain => domain.Active)
            .ThenByDescending(domain => domain.Score.OverallScore)
            .ThenBy(domain => domain.Domain, StringComparer.Ordinal)
            .ToList();
    }

    private IReadOnlyList<GovernanceDecision> EvaluateGovernance(
        PlannerFeedback plannerFeedback,
        ResearchQueue queue,
        IReadOnlyList<DomainHealth> domains)
    {
        var decisions = new List<GovernanceDecision>();
        var rules = GovernanceRules();
        var scanFeedback = plannerFeedback.TaskTypeFeedback.FirstOrDefault(item =>
            item.TaskType.Equals("scan_knowledge_sources", StringComparison.OrdinalIgnoreCase));
        var openQueue = queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase));
        var storage = new StorageHygieneService(_storagePaths).LoadPlan();
        var loopState = LoadLoopState();

        decisions.Add(Decision(
            rules[0],
            loopState.IdleIterations >= 3 ? "warn" : "pass",
            loopState.IdleIterations >= 3
                ? $"Autonomous loop idle iterations={loopState.IdleIterations}; stop before repeating work."
                : "Loop idle count below governance threshold.",
            loopState.IdleIterations >= 3 ? "pause_until_new_needs_or_unseen_outcomes" : "continue",
            [Path.Combine(Root, "autonomous_loop_state.json")]));
        decisions.Add(Decision(
            rules[1],
            scanFeedback?.AverageRedundancyScore >= 0.7 ? "warn" : "pass",
            scanFeedback?.AverageRedundancyScore >= 0.7
                ? $"Knowledge scans show redundancy={scanFeedback.AverageRedundancyScore:0.####}."
                : "No excessive scan redundancy detected.",
            scanFeedback?.AverageRedundancyScore >= 0.7 ? "reduce_scan_priority_and_validate_existing_sources" : "continue",
            [new TaskOutcomeEvaluator(_storagePaths).PlannerFeedbackPath]));
        decisions.Add(Decision(
            rules[2],
            openQueue > 100 ? "warn" : "pass",
            openQueue > 100 ? $"Open research queue items={openQueue}." : "Research queue below explosion threshold.",
            openQueue > 100 ? "prefer_process_research_queue_and_consolidation" : "continue",
            [new ResearchQueueService(_storagePaths).QueuePath]));
        decisions.Add(Decision(
            rules[3],
            storage?.Candidates.Count > 50000 ? "warn" : "pass",
            storage?.Candidates.Count > 50000
                ? $"Cleanup candidates={storage.Candidates.Count}; avoid storage growth."
                : "Storage cleanup candidates below warning threshold or no plan loaded.",
            storage?.Candidates.Count > 50000 ? "prioritize_storage_hygiene_review_no_aggressive_delete" : "continue",
            [new StorageHygieneService(_storagePaths).CleanupPlanPath]));
        decisions.Add(Decision(
            rules[4],
            domains.Any(domain => domain.Score.ValidationCoverage < 0.2 && domain.KnowledgeItemCount > 5) ? "warn" : "pass",
            "Prioritization must account for validation coverage and not only volume.",
            domains.Any(domain => domain.Score.ValidationCoverage < 0.2 && domain.KnowledgeItemCount > 5)
                ? "prefer_validation_and_quality_improvement"
                : "continue",
            [DomainHealthPath]));
        return decisions;
    }

    private IReadOnlyList<MetaObservation> BuildObservations(
        IReadOnlyList<GoalProgress> goalProgress,
        GoalFeedback goalFeedback,
        PlannerFeedback plannerFeedback,
        IReadOnlyList<TaskOutcomeResult> outcomes,
        IReadOnlyList<CognitiveSource> sources,
        IReadOnlyList<KnowledgeCatalogItem> catalog,
        ResearchQueue queue,
        IReadOnlyList<DomainHealth> domains,
        IReadOnlyList<GovernanceDecision> decisions,
        IReadOnlyList<DetectedNeed> needs)
    {
        var observations = new List<MetaObservation>();
        observations.AddRange(plannerFeedback.TaskTypeFeedback
            .Where(item => item.AverageUsefulnessScore >= 0.6 && item.AverageLearningValue >= 0.4)
            .Select(item => Observation(
                "activity_progress",
                "info",
                $"{item.TaskType} bringt Fortschritt",
                $"Usefulness={item.AverageUsefulnessScore:0.####}, learning={item.AverageLearningValue:0.####}.",
                [new TaskOutcomeEvaluator(_storagePaths).PlannerFeedbackPath],
                [$"repeat:{item.TaskType}"])));
        observations.AddRange(plannerFeedback.TaskTypeFeedback
            .Where(item => item.AverageUsefulnessScore < 0.35 || item.AverageRedundancyScore > 0.7)
            .Select(item => Observation(
                "activity_noise",
                "warning",
                $"{item.TaskType} erzeugt wenig verwertbaren Fortschritt",
                $"Usefulness={item.AverageUsefulnessScore:0.####}, redundancy={item.AverageRedundancyScore:0.####}.",
                [new TaskOutcomeEvaluator(_storagePaths).PlannerFeedbackPath],
                [$"reduce_priority:{item.TaskType}", "review_need_mapping"])));
        observations.AddRange(goalProgress
            .Where(goal => goal.ProgressScore < 0.5 || goal.Blockers.Count > 0)
            .Select(goal => Observation(
                "goal_stagnation",
                goal.ProgressScore < 0.35 ? "warning" : "info",
                $"{goal.GoalId} stagniert oder ist blockiert",
                $"Progress={goal.ProgressScore:0.####}; blockers={goal.Blockers.Count}.",
                [new GoalManager(_storagePaths).GoalProgressPath],
                goal.NextActions)));
        if (queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)) == 0)
        {
            observations.Add(Observation(
                "queue_health",
                "warning",
                "Research Queue hat keine offenen Items",
                "Ohne offene Queue-Items kann die Validierung stagnieren.",
                [new ResearchQueueService(_storagePaths).QueuePath],
                ["generate_hypotheses", "enqueue_research_validation"]));
        }

        observations.Add(Observation(
            "knowledge_growth",
            catalog.Count == 0 ? "warning" : "info",
            "Knowledge Catalog Status",
            $"sources={sources.Count}, knowledge_items={catalog.Count}, outcomes={outcomes.Count}.",
            [new KnowledgeCatalog(_storagePaths).CatalogPath],
            catalog.Count == 0 ? ["scan_knowledge_sources"] : ["validate_existing_knowledge"]));
        observations.AddRange(domains
            .Where(domain => domain.Score.Classification is "weak" or "needs_more_data")
            .Select(domain => Observation(
                "domain_health",
                domain.Active ? "warning" : "info",
                $"Domain {domain.Domain} braucht Arbeit",
                $"overall={domain.Score.OverallScore:0.####}; classification={domain.Score.Classification}.",
                [DomainHealthPath],
                domain.Warnings.Count == 0 ? ["monitor_domain"] : domain.Warnings)));
        observations.AddRange(decisions
            .Where(decision => !decision.Status.Equals("pass", StringComparison.OrdinalIgnoreCase))
            .Select(decision => Observation(
                "governance",
                decision.Status.Equals("block", StringComparison.OrdinalIgnoreCase) ? "critical" : "warning",
                $"Governance: {decision.RuleId}",
                decision.Reason,
                decision.EvidenceRefs,
                [decision.Action])));
        observations.AddRange(needs
            .GroupBy(need => need.NeedId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => Observation(
                "recurring_need",
                "warning",
                $"{group.Key} tritt wiederholt auf",
                "Need appears multiple times in current detection set.",
                [new NeedDetectionEngine(_storagePaths).NeedsPath],
                ["review_planner_feedback", "avoid_duplicate_tasks"])));
        return observations
            .GroupBy(observation => observation.ObservationId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(observation => SeverityRank(observation.Severity))
            .ThenBy(observation => observation.Category, StringComparer.Ordinal)
            .Take(60)
            .ToList();
    }

    private IReadOnlyList<GoalProgress> LoadGoalProgress()
    {
        var path = new GoalManager(_storagePaths).GoalProgressPath;
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var text = File.ReadAllText(path);
            if (text.TrimStart().StartsWith('{'))
            {
                var report = JsonSerializer.Deserialize<GoalProgressReport>(
                    text,
                    JsonDefaults.SnapshotReadOptions);
                if (report is not null)
                {
                    return report.Goals;
                }
            }

            return JsonSerializer.Deserialize<IReadOnlyList<GoalProgress>>(
                text,
                JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    private AutonomousLoopState LoadLoopState()
    {
        var path = Path.Combine(Root, "autonomous_loop_state.json");
        if (!File.Exists(path))
        {
            return new AutonomousLoopState(
                StateVersion: "autonomous_loop_state_v1",
                UpdatedAtUtc: DateTimeOffset.UtcNow,
                Status: "not_started",
                RunId: string.Empty,
                StartedAtUtc: null,
                DeadlineUtc: null,
                IterationsCompleted: 0,
                IdleIterations: 0,
                WorkPerformed: 0,
                AverageLearningValue: 0,
                NextAction: "run_autonomous_loop",
                LastIterationId: null,
                LastCheckpointPath: null,
                LastStopReason: null,
                StatePath: path,
                SummaryPath: Path.Combine(Root, "autonomous_loop_summary.json"),
                LogPath: Path.Combine(Root, "autonomous_loop_log.jsonl"),
                NoTradingExecution: true,
                NoBrokerAction: true,
                NoAutoTrading: true,
                HumanReviewRequired: true);
        }

        try
        {
            return JsonSerializer.Deserialize<AutonomousLoopState>(
                File.ReadAllText(path),
                JsonDefaults.SnapshotReadOptions) ?? throw new JsonException("state null");
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return new AutonomousLearningLoop(_storagePaths, string.Empty).LoadState();
        }
    }

    private void WriteDomainHealth(IReadOnlyList<DomainHealth> health)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(DomainHealthPath, JsonSerializer.Serialize(health, JsonDefaults.WriteOptions));
    }

    private void WriteLearningStrategy(LearningStrategy strategy)
    {
        Directory.CreateDirectory(Root);
        File.WriteAllText(LearningStrategyPath, JsonSerializer.Serialize(strategy, JsonDefaults.WriteOptions));
    }

    private static IReadOnlyList<string> ActivitiesWithProgress(PlannerFeedback feedback) =>
        feedback.TaskTypeFeedback
            .Where(item => item.AverageUsefulnessScore >= 0.6 && item.AverageLearningValue >= 0.35)
            .Select(item => $"{item.TaskType}:usefulness={item.AverageUsefulnessScore:0.####}:learning={item.AverageLearningValue:0.####}")
            .ToList();

    private static IReadOnlyList<string> ActivitiesGeneratingWork(PlannerFeedback feedback) =>
        feedback.TaskTypeFeedback
            .Where(item => item.AverageUsefulnessScore < 0.35 || item.AverageRedundancyScore > 0.7)
            .Select(item => $"{item.TaskType}:usefulness={item.AverageUsefulnessScore:0.####}:redundancy={item.AverageRedundancyScore:0.####}")
            .ToList();

    private static IReadOnlyList<string> StagnantGoals(IReadOnlyList<GoalProgress> progress, GoalFeedback feedback)
    {
        var lowProgress = progress
            .Where(goal => goal.ProgressScore < 0.5 || goal.Blockers.Count > 0)
            .Select(goal => goal.GoalId);
        var lowOutcome = feedback.Goals
            .Where(goal => goal.AverageUsefulnessScore < 0.35 || goal.PersistentNeeds.Count > 0)
            .Select(goal => goal.GoalId);
        return lowProgress.Concat(lowOutcome)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(goal => goal, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<string> RecurringNeeds(GoalFeedback feedback, IReadOnlyList<DetectedNeed> needs) =>
        feedback.Goals
            .SelectMany(goal => goal.PersistentNeeds)
            .Concat(needs.Select(need => need.NeedId))
            .GroupBy(need => need, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(need => need, StringComparer.Ordinal)
            .ToList();

    private static IReadOnlyList<GovernanceRule> GovernanceRules() =>
    [
        new("no_endless_loops", "Stop or pause when the autonomous loop repeats idle work.", "warning", 3, true),
        new("no_redundant_scans", "Reduce repeated scans when they do not add learning value.", "warning", 0.7, true),
        new("no_queue_explosion", "Avoid unbounded research queue growth.", "warning", 100, true),
        new("no_excessive_storage_usage", "Treat large cleanup pressure as a governance warning.", "warning", 50000, true),
        new("no_mass_only_prioritization", "Do not prioritize by volume without validation coverage.", "warning", 0.2, true)
    ];

    private static GovernanceDecision Decision(
        GovernanceRule rule,
        string status,
        string reason,
        string action,
        IReadOnlyList<string> evidenceRefs) =>
        new(rule.RuleId, status, reason, action, evidenceRefs);

    private static MetaObservation Observation(
        string category,
        string severity,
        string title,
        string summary,
        IReadOnlyList<string> evidenceRefs,
        IReadOnlyList<string> recommendedActions) =>
        new(
            ObservationId: $"meta_{category}_{StableId(title)}",
            Category: category,
            Severity: severity,
            Title: title,
            Summary: summary,
            EvidenceRefs: evidenceRefs,
            RecommendedActions: recommendedActions);

    private static IReadOnlyList<string> DomainWarnings(
        CognitiveDomain domain,
        int sourceCount,
        int itemCount,
        int queueCount,
        double validationCoverage,
        double redundancy,
        double learningVelocity)
    {
        var warnings = new List<string>();
        if (sourceCount == 0)
        {
            warnings.Add("source_gap");
        }

        if (itemCount == 0)
        {
            warnings.Add("knowledge_gap");
        }

        if (domain.Active && validationCoverage < 0.25)
        {
            warnings.Add("validation_coverage_low");
        }

        if (redundancy > 0.55)
        {
            warnings.Add("redundancy_high");
        }

        if (domain.Active && learningVelocity < 0.2)
        {
            warnings.Add("learning_velocity_low");
        }

        if (queueCount == 0 && domain.Active)
        {
            warnings.Add("queue_empty");
        }

        return warnings;
    }

    private static IReadOnlyList<string> DomainReasons(
        int sourceCount,
        int itemCount,
        double validationCoverage,
        double redundancy,
        double learningVelocity) =>
    [
        $"sources:{sourceCount}",
        $"knowledge_items:{itemCount}",
        $"validation_coverage:{validationCoverage:0.####}",
        $"redundancy:{redundancy:0.####}",
        $"learning_velocity:{learningVelocity:0.####}"
    ];

    private static string Classification(double score, IReadOnlyList<string> warnings) =>
        score switch
        {
            >= 0.75 when !warnings.Contains("redundancy_high") => "healthy",
            >= 0.55 => "promising",
            >= 0.35 => "needs_more_data",
            _ => "weak"
        };

    private static string OutcomeDomain(TaskOutcomeResult outcome, IReadOnlyDictionary<string, string> needDomains)
    {
        if (needDomains.TryGetValue(outcome.NeedId, out var domain))
        {
            return domain;
        }

        if (outcome.NeedId.StartsWith("domain_", StringComparison.OrdinalIgnoreCase))
        {
            var parts = outcome.NeedId.Split('_', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2)
            {
                return parts[1];
            }
        }

        return outcome.TaskType switch
        {
            "run_storage_hygiene" => "process",
            "scan_knowledge_sources" => "research",
            "generate_hypotheses" or "generate_cognitive_insights" or "process_research_queue" => "research",
            _ => "trading"
        };
    }

    private static int SeverityRank(string severity) =>
        severity.ToLowerInvariant() switch
        {
            "critical" => 4,
            "warning" => 3,
            "info" => 2,
            _ => 1
        };

    private static string StableId(string text)
    {
        var chars = text
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();
        return new string(chars).Trim('_');
    }
}

public sealed class LearningStrategyManager
{
    public LearningStrategy SelectStrategy(
        IReadOnlyList<DomainHealth> domains,
        GoalFeedback goalFeedback,
        PlannerFeedback plannerFeedback,
        ResearchQueue queue,
        IReadOnlyList<DetectedNeed> needs,
        IReadOnlyList<GovernanceDecision> decisions)
    {
        var openQueue = queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase));
        var validationWeak = domains.Any(domain => domain.KnowledgeItemCount > 5 && domain.Score.ValidationCoverage < 0.25);
        var sourceGaps = domains.Any(domain => domain.SourceCount == 0);
        var qualityRisks = needs.Count(need => need.Category == NeedCategory.quality_risk);
        var redundantWork = plannerFeedback.TaskTypeFeedback.Any(item =>
            item.AverageRedundancyScore > 0.7 || item.Recommendation.Equals("reduce_priority", StringComparison.OrdinalIgnoreCase));
        var storageWarning = decisions.Any(decision =>
            decision.RuleId.Equals("no_excessive_storage_usage", StringComparison.OrdinalIgnoreCase)
            && decision.Status.Equals("warn", StringComparison.OrdinalIgnoreCase));

        string strategy;
        string reason;
        IReadOnlyList<string> priorityTasks;
        IReadOnlyList<string> deprioritized;
        string expected;

        if (storageWarning)
        {
            strategy = "consolidation";
            reason = "Storage pressure is high; preserve stability before expanding work.";
            priorityTasks = ["run_storage_hygiene", "generate_cognitive_insights", "process_research_queue"];
            deprioritized = ["scan_knowledge_sources", "run_strategy_research"];
            expected = "reduce operational pressure and consolidate existing evidence";
        }
        else if (openQueue > 50 || validationWeak)
        {
            strategy = "validation";
            reason = "Knowledge exists but validation coverage or queue processing is behind.";
            priorityTasks = ["process_research_queue", "run_walkforward_validation", "run_overfit_report"];
            deprioritized = redundantWork ? ["scan_knowledge_sources"] : [];
            expected = "convert existing knowledge into stronger evidence";
        }
        else if (qualityRisks > 0 || goalFeedback.Warnings.Count > 0)
        {
            strategy = "quality_improvement";
            reason = "Quality-risk needs or low goal usefulness are present.";
            priorityTasks = ["run_realism_report", "run_overfit_report", "run_walkforward_validation"];
            deprioritized = ["scan_knowledge_sources"];
            expected = "reduce weak hypotheses and improve validation quality";
        }
        else if (sourceGaps)
        {
            strategy = "source_expansion";
            reason = "At least one domain has no knowledge sources.";
            priorityTasks = ["scan_knowledge_sources", "scan_software_domain", "scan_documentation_domain", "scan_process_domain", "scan_research_domain", "generate_domain_insights"];
            deprioritized = [];
            expected = "increase curated knowledge coverage";
        }
        else
        {
            strategy = "exploration";
            reason = "No dominant blocker; explore new hypotheses conservatively.";
            priorityTasks = ["generate_hypotheses", "scan_knowledge_sources", "generate_domain_insights", "process_research_queue"];
            deprioritized = [];
            expected = "discover new candidates while preserving no-auto-trading safety";
        }

        var focusDomains = domains
            .Where(domain => domain.Active || domain.Score.Classification is "weak" or "needs_more_data")
            .Select(domain => domain.Domain)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
        if (focusDomains.Count > 1
            && !focusDomains.Contains("balanced", StringComparer.OrdinalIgnoreCase))
        {
            focusDomains.Insert(0, "balanced");
        }

        return new LearningStrategy(
            StrategyVersion: "learning_strategy_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            CurrentStrategy: strategy,
            Reason: reason,
            PriorityTaskTypes: priorityTasks,
            DeprioritizedTaskTypes: deprioritized.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            DomainFocus: focusDomains.Take(5).ToList(),
            ExpectedEffect: expected,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }
}
