namespace Hermes.Runtime;

public sealed record ValidationCapability(
    string RequirementType,
    string Description,
    string DefaultTaskType,
    string DefaultMappedInternalTaskType);

public sealed record DomainValidationProfile(
    string Domain,
    IReadOnlyList<ValidationCapability> Capabilities,
    IReadOnlyList<string> ExplicitlyUnsupportedRequirementTypes);

public sealed record DomainValidationRouteResult(
    string Domain,
    IReadOnlyList<KnowledgeValidationRequirement> AllowedRequirements,
    IReadOnlyList<string> SkippedByRouterReasons);

public sealed record DomainValidationRoutingStatus(
    string StatusVersion,
    DateTimeOffset UpdatedAtUtc,
    int Profiles,
    int InvalidValidationTasks,
    int ValidationTasksCleaned,
    string ValidationRoutingHealth,
    IReadOnlyList<DomainValidationProfile> DomainProfiles,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed record ValidationTaskCleanupResult(
    int InvalidValidationTasks,
    int ValidationTasksCleaned,
    string ValidationRoutingHealth,
    IReadOnlyList<string> CleanedQueueItemIds,
    IReadOnlyList<string> Warnings);

public sealed class DomainValidationRouter
{
    private readonly StoragePaths? _storagePaths;

    public DomainValidationRouter(StoragePaths? storagePaths = null)
    {
        _storagePaths = storagePaths;
    }

    public IReadOnlyList<DomainValidationProfile> Profiles => DefaultProfiles;

    public DomainValidationProfile ProfileFor(string domain) =>
        Profiles.FirstOrDefault(profile => profile.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase))
        ?? Profiles.First(profile => profile.Domain.Equals("research", StringComparison.OrdinalIgnoreCase));

    public bool IsAllowed(string domain, string requirementType) =>
        ProfileFor(domain).Capabilities.Any(capability =>
            capability.RequirementType.Equals(requirementType, StringComparison.OrdinalIgnoreCase));

    public ValidationCapability? CapabilityFor(string domain, string requirementType) =>
        ProfileFor(domain).Capabilities.FirstOrDefault(capability =>
            capability.RequirementType.Equals(requirementType, StringComparison.OrdinalIgnoreCase));

    public DomainValidationRouteResult Route(string domain, IReadOnlyList<KnowledgeValidationRequirement> requirements)
    {
        var profile = ProfileFor(domain);
        var allowed = requirements
            .Where(requirement => IsAllowed(profile.Domain, requirement.RequirementType))
            .ToList();
        var skipped = requirements
            .Where(requirement => !IsAllowed(profile.Domain, requirement.RequirementType))
            .Select(requirement => $"skipped_by_router:{profile.Domain}:{requirement.RequirementType}:validation_type_not_supported_for_domain")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return new DomainValidationRouteResult(profile.Domain, allowed, skipped);
    }

    public DomainValidationRoutingStatus BuildStatus()
    {
        var queue = _storagePaths is null
            ? null
            : new ResearchQueueService(_storagePaths).LoadOrCreateQueue();
        var invalid = queue?.Items.Count(IsInvalidOpenValidationTask) ?? 0;
        var cleaned = queue?.Items.Count(item =>
            item.Notes.Any(note => note.Contains("invalid_for_domain", StringComparison.OrdinalIgnoreCase))) ?? 0;
        return new DomainValidationRoutingStatus(
            StatusVersion: "domain_validation_routing_status_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Profiles: Profiles.Count,
            InvalidValidationTasks: invalid,
            ValidationTasksCleaned: cleaned,
            ValidationRoutingHealth: invalid > 0 ? "needs_cleanup" : "ok",
            DomainProfiles: Profiles,
            Warnings: invalid > 0 ? [$"invalid_validation_tasks:{invalid}"] : [],
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    public bool IsInvalidOpenValidationTask(ResearchQueueItem item)
    {
        if (!item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)
            || !IsValidationQueueItem(item))
        {
            return false;
        }

        var requirement = NoteValue(item, "requirement");
        return !string.IsNullOrWhiteSpace(requirement)
            && !IsAllowed(item.Domain, requirement);
    }

    public static bool IsValidationQueueItem(ResearchQueueItem item) =>
        item.RequestedBy.Equals("knowledge_validation_strategy", StringComparison.OrdinalIgnoreCase)
        || item.Notes.Any(note => note.StartsWith("validation_task:", StringComparison.OrdinalIgnoreCase));

    public static string? NoteValue(ResearchQueueItem item, string key)
    {
        var prefix = $"{key}:";
        return item.Notes
            .FirstOrDefault(note => note.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            ?[prefix.Length..];
    }

    private static readonly IReadOnlyList<DomainValidationProfile> DefaultProfiles =
    [
        Profile(
            "trading",
            [
                Capability("source_verification", "Verify curated/local source metadata.", "collect_missing_evidence", "scan_knowledge_sources"),
                Capability("cross_source_confirmation", "Require at least two independent sources.", "run_cross_source_check", "scan_knowledge_sources"),
                Capability("historical_test", "Use existing Strategy Research/Backtest reports.", "validate_knowledge_item", "run_strategy_research"),
                Capability("out_of_sample_test", "Use existing OOS/Walk-Forward evidence.", "run_oos_validation", "run_walkforward_validation"),
                Capability("walkforward_test", "Use existing Walk-Forward validation report.", "run_oos_validation", "run_walkforward_validation"),
                Capability("cost_stress_test", "Use existing Cost Stress report.", "validate_knowledge_item", "cost-stress-report"),
                Capability("monte_carlo_test", "Use existing Monte-Carlo report.", "validate_knowledge_item", "monte-carlo-report"),
                Capability("stale_check", "Check last validation timestamp.", "run_domain_review", "generate_domain_insights"),
                Capability("domain_review", "Structured trading-domain review.", "run_domain_review", "generate_domain_insights")
            ],
            []),
        Profile(
            "documentation",
            [
                Capability("source_verification", "Verify local documentation source metadata.", "collect_missing_evidence", "scan_knowledge_sources"),
                Capability("cross_source_confirmation", "Look for related source/document references.", "run_cross_source_check", "scan_knowledge_sources"),
                Capability("stale_check", "Check last validation timestamp.", "run_domain_review", "generate_domain_insights"),
                Capability("domain_review", "Structured documentation review.", "run_domain_review", "generate_domain_insights"),
                Capability("consistency_check", "Check for contradictory or stale documentation claims.", "run_domain_review", "generate_domain_insights"),
                Capability("reference_check", "Check whether referenced docs/sources exist.", "run_domain_review", "generate_domain_insights")
            ],
            ["historical_test", "out_of_sample_test", "walkforward_test", "cost_stress_test", "monte_carlo_test"]),
        Profile(
            "software",
            [
                Capability("source_verification", "Verify local repo/code source metadata.", "collect_missing_evidence", "scan_knowledge_sources"),
                Capability("cross_source_confirmation", "Cross-check code, docs, and local metadata.", "run_cross_source_check", "scan_knowledge_sources"),
                Capability("stale_check", "Check last validation timestamp.", "run_domain_review", "generate_domain_insights"),
                Capability("domain_review", "Structured software-domain review.", "run_domain_review", "generate_domain_insights"),
                Capability("static_analysis", "Static metadata/code-structure check; no code execution.", "run_domain_review", "scan_software_domain"),
                Capability("test_presence_check", "Check for referenced test commands or test docs.", "run_domain_review", "scan_software_domain"),
                Capability("build_reference_check", "Check build-command references only; no build is triggered.", "run_domain_review", "scan_software_domain")
            ],
            ["historical_test", "out_of_sample_test", "walkforward_test", "cost_stress_test", "monte_carlo_test"]),
        Profile(
            "process",
            [
                Capability("source_verification", "Verify process source metadata.", "collect_missing_evidence", "scan_knowledge_sources"),
                Capability("stale_check", "Check last validation timestamp.", "run_domain_review", "generate_domain_insights"),
                Capability("domain_review", "Structured process-domain review.", "run_domain_review", "generate_domain_insights"),
                Capability("consistency_check", "Check workflow consistency.", "run_domain_review", "generate_domain_insights"),
                Capability("process_owner_review_stub", "Mark human/process owner review requirement.", "run_domain_review", "generate_domain_insights")
            ],
            ["cross_source_confirmation", "historical_test", "out_of_sample_test", "walkforward_test", "cost_stress_test", "monte_carlo_test"]),
        Profile(
            "research",
            [
                Capability("source_verification", "Verify curated research source metadata.", "collect_missing_evidence", "scan_knowledge_sources"),
                Capability("cross_source_confirmation", "Require corroborating references.", "run_cross_source_check", "scan_knowledge_sources"),
                Capability("stale_check", "Check last validation timestamp.", "run_domain_review", "generate_domain_insights"),
                Capability("domain_review", "Structured research-domain review.", "run_domain_review", "generate_domain_insights"),
                Capability("citation_check", "Check citation/source presence.", "run_domain_review", "generate_domain_insights"),
                Capability("reproducibility_check", "Check whether evidence can be reproduced from available metadata.", "run_domain_review", "generate_domain_insights")
            ],
            ["historical_test", "out_of_sample_test", "walkforward_test", "cost_stress_test", "monte_carlo_test"])
    ];

    private static DomainValidationProfile Profile(string domain, IReadOnlyList<ValidationCapability> capabilities, IReadOnlyList<string> unsupported) =>
        new(domain, capabilities, unsupported);

    private static ValidationCapability Capability(string type, string description, string taskType, string mappedInternalTaskType) =>
        new(type, description, taskType, mappedInternalTaskType);
}
