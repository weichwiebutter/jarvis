namespace Hermes.Runtime;

public sealed record DomainKnowledgeValidationResult(
    string ValidationStatus,
    double EvidenceStrength,
    IReadOnlyList<string> MissingEvidence,
    IReadOnlyList<string> Warnings,
    double QualityDeltaHint,
    string Recommendation,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> OutputPaths,
    string Summary);

public interface IDomainKnowledgeValidationAdapter
{
    bool Supports(string requirementType);

    DomainKnowledgeValidationResult Validate(
        KnowledgeCatalogItem item,
        KnowledgeValidationPlan plan,
        KnowledgeValidationRequirement requirement);
}

public sealed class DocumentationValidationAdapter : DomainKnowledgeValidationAdapterBase
{
    public DocumentationValidationAdapter(StoragePaths storagePaths)
        : base(storagePaths)
    {
    }

    public override bool Supports(string requirementType) =>
        requirementType is "consistency_check" or "reference_check" or "stale_check" or "domain_review";

    protected override DomainKnowledgeValidationResult ValidateCore(
        KnowledgeCatalogItem item,
        KnowledgeValidationPlan plan,
        KnowledgeValidationRequirement requirement)
    {
        var context = BuildContext(item);
        var missing = new List<string>();
        var warnings = new List<string>();
        var evidence = new List<string>();
        var checks = 0;
        var passed = 0;

        Check(HasText(item.Title), "title_present", "title_missing");
        Check(HasText(item.DescriptionShort), "description_present", "description_missing");
        Check(item.Tags.Count > 0, "tags_present", "tags_missing");
        Check(context.MatchedSources.Count > 0, "source_metadata_present", "source_metadata_missing");
        Check(item.SourceIds.Count >= 1, "source_reference_present", "source_reference_missing");
        Check(item.SourceIds.Count >= 2, "second_source_present", "second_independent_source_missing", warningOnly: true);
        Check(RelatedItemsResolvable(item, context), "related_items_consistent", "related_items_unresolved", warningOnly: true);

        if (requirement.RequirementType.Equals("reference_check", StringComparison.OrdinalIgnoreCase))
        {
            Check(context.SourceReferencesUsable, "references_resolvable_or_metadata_present", "reference_metadata_missing");
        }

        if (requirement.RequirementType.Equals("stale_check", StringComparison.OrdinalIgnoreCase))
        {
            var fresh = IsFresh(item.LastValidatedUtc)
                || context.MatchedSources.Any(source => IsFresh(source.LastCheckedUtc));
            Check(fresh, "fresh_validation_or_source_check", "fresh_validation_timestamp_missing");
        }

        return ResultFor(
            "documentation",
            item,
            requirement,
            checks,
            passed,
            missing,
            warnings,
            evidence,
            context.OutputPaths,
            item.SourceIds.Count >= 2 ? "promote_to_promising" : "needs_more_evidence");

        void Check(bool condition, string evidenceRef, string missingRef, bool warningOnly = false)
        {
            checks++;
            if (condition)
            {
                passed++;
                evidence.Add(evidenceRef);
                return;
            }

            missing.Add(missingRef);
            if (warningOnly)
            {
                warnings.Add(missingRef);
            }
        }
    }
}

public sealed class SoftwareValidationAdapter : DomainKnowledgeValidationAdapterBase
{
    public SoftwareValidationAdapter(StoragePaths storagePaths)
        : base(storagePaths)
    {
    }

    public override bool Supports(string requirementType) =>
        requirementType is "static_analysis" or "test_presence_check" or "build_reference_check" or "source_verification" or "stale_check" or "domain_review";

    protected override DomainKnowledgeValidationResult ValidateCore(
        KnowledgeCatalogItem item,
        KnowledgeValidationPlan plan,
        KnowledgeValidationRequirement requirement)
    {
        var context = BuildContext(item);
        var text = CombinedText(item, context);
        var missing = new List<string>();
        var warnings = new List<string>();
        var evidence = new List<string> { "no_external_code_execution_required" };
        var checks = 0;
        var passed = 0;

        Check(context.MatchedSources.Count > 0, "source_metadata_present", "source_metadata_missing");
        Check(context.HasPathOrRepoReference || LooksLikeCodeModule(item), "repo_or_file_reference_present", "repo_or_file_reference_missing");
        Check(item.Tags.Count > 0, "tags_present", "tags_missing");

        if (requirement.RequirementType.Equals("static_analysis", StringComparison.OrdinalIgnoreCase))
        {
            Check(!HasRiskFlag(context, item), "risk_flags_absent_or_not_reported", "risk_flags_present", warningOnly: true);
            Check(ContainsAny(text, "module", "class", "service", "adapter", "runtime", "cli", "source", "code"), "static_structure_metadata_present", "static_structure_metadata_missing");
        }

        if (requirement.RequirementType.Equals("test_presence_check", StringComparison.OrdinalIgnoreCase))
        {
            Check(ContainsAny(text, "test", "build", "dotnet build", "npm run build", "py_compile"), "test_or_validation_reference_present", "test_reference_missing");
        }

        if (requirement.RequirementType.Equals("build_reference_check", StringComparison.OrdinalIgnoreCase))
        {
            Check(ContainsAny(text, "build", "dotnet build", "npm run build", "compile"), "build_reference_present", "build_reference_missing");
        }

        if (requirement.RequirementType.Equals("stale_check", StringComparison.OrdinalIgnoreCase))
        {
            Check(IsFresh(item.LastValidatedUtc) || context.MatchedSources.Any(source => IsFresh(source.LastCheckedUtc)), "fresh_validation_or_source_check", "fresh_validation_timestamp_missing");
        }

        return ResultFor(
            "software",
            item,
            requirement,
            checks,
            passed,
            missing,
            warnings,
            evidence,
            context.OutputPaths,
            HasRiskFlag(context, item) ? "needs_more_evidence" : "promote_to_promising");

        void Check(bool condition, string evidenceRef, string missingRef, bool warningOnly = false)
        {
            checks++;
            if (condition)
            {
                passed++;
                evidence.Add(evidenceRef);
                return;
            }

            missing.Add(missingRef);
            if (warningOnly)
            {
                warnings.Add(missingRef);
            }
        }
    }
}

public sealed class ProcessValidationAdapter : DomainKnowledgeValidationAdapterBase
{
    public ProcessValidationAdapter(StoragePaths storagePaths)
        : base(storagePaths)
    {
    }

    public override bool Supports(string requirementType) =>
        requirementType is "consistency_check" or "domain_review" or "process_owner_review_stub" or "stale_check";

    protected override DomainKnowledgeValidationResult ValidateCore(
        KnowledgeCatalogItem item,
        KnowledgeValidationPlan plan,
        KnowledgeValidationRequirement requirement)
    {
        var context = BuildContext(item);
        var text = CombinedText(item, context);
        var missing = new List<string>();
        var warnings = new List<string>();
        var evidence = new List<string>();
        var checks = 0;
        var passed = 0;

        Check(HasText(item.DescriptionShort), "process_goal_described", "process_goal_missing");
        Check(ContainsAny(text, "trigger", "input", "output", "workflow", "checklist", "task", "review", "owner"), "process_structure_metadata_present", "process_inputs_outputs_missing");
        Check(ContainsAny(text, "risk", "warning", "safety", "approval", "review", "human"), "process_risk_or_review_hint_present", "process_risk_review_hint_missing", warningOnly: true);
        Check(context.MatchedSources.Count > 0, "source_metadata_present", "source_metadata_missing");

        if (requirement.RequirementType.Equals("process_owner_review_stub", StringComparison.OrdinalIgnoreCase))
        {
            Check(ContainsAny(text, "owner", "review", "human", "approval"), "owner_or_review_stub_present", "process_owner_review_stub_required", warningOnly: true);
            warnings.Add("human_process_owner_review_still_required");
        }

        if (requirement.RequirementType.Equals("stale_check", StringComparison.OrdinalIgnoreCase))
        {
            Check(IsFresh(item.LastValidatedUtc) || context.MatchedSources.Any(source => IsFresh(source.LastCheckedUtc)), "fresh_validation_or_source_check", "fresh_validation_timestamp_missing");
        }

        return ResultFor(
            "process",
            item,
            requirement,
            checks,
            passed,
            missing,
            warnings,
            evidence,
            context.OutputPaths,
            warnings.Contains("human_process_owner_review_still_required", StringComparer.OrdinalIgnoreCase)
                ? "needs_more_evidence"
                : "promote_to_promising");

        void Check(bool condition, string evidenceRef, string missingRef, bool warningOnly = false)
        {
            checks++;
            if (condition)
            {
                passed++;
                evidence.Add(evidenceRef);
                return;
            }

            missing.Add(missingRef);
            if (warningOnly)
            {
                warnings.Add(missingRef);
            }
        }
    }
}

public sealed class ResearchValidationAdapter : DomainKnowledgeValidationAdapterBase
{
    public ResearchValidationAdapter(StoragePaths storagePaths)
        : base(storagePaths)
    {
    }

    public override bool Supports(string requirementType) =>
        requirementType is "citation_check" or "reproducibility_check" or "cross_source_confirmation" or "stale_check" or "domain_review";

    protected override DomainKnowledgeValidationResult ValidateCore(
        KnowledgeCatalogItem item,
        KnowledgeValidationPlan plan,
        KnowledgeValidationRequirement requirement)
    {
        var context = BuildContext(item);
        var text = CombinedText(item, context);
        var missing = new List<string>();
        var warnings = new List<string>();
        var evidence = new List<string>();
        var checks = 0;
        var passed = 0;

        Check(item.SourceIds.Count > 0 && context.MatchedSources.Count > 0, "citation_source_metadata_present", "citation_source_missing");
        Check(item.SourceIds.Count >= 2, "second_source_present", "second_independent_source_missing", warningOnly: true);
        Check(ContainsAny(text, "method", "reproduce", "validation", "evidence", "source", "result", "hypothesis", "test"), "reproducibility_hint_present", "reproducibility_hint_missing");
        Check(!ContainsAny(text, "assumption_missing", "unknown", "unverified"), "open_assumptions_not_detected", "open_assumptions_present", warningOnly: true);

        if (requirement.RequirementType.Equals("stale_check", StringComparison.OrdinalIgnoreCase))
        {
            Check(IsFresh(item.LastValidatedUtc) || context.MatchedSources.Any(source => IsFresh(source.LastCheckedUtc)), "fresh_validation_or_source_check", "fresh_validation_timestamp_missing");
        }

        return ResultFor(
            "research",
            item,
            requirement,
            checks,
            passed,
            missing,
            warnings,
            evidence,
            context.OutputPaths,
            item.SourceIds.Count >= 2 ? "promote_to_promising" : "needs_more_evidence");

        void Check(bool condition, string evidenceRef, string missingRef, bool warningOnly = false)
        {
            checks++;
            if (condition)
            {
                passed++;
                evidence.Add(evidenceRef);
                return;
            }

            missing.Add(missingRef);
            if (warningOnly)
            {
                warnings.Add(missingRef);
            }
        }
    }
}

public abstract class DomainKnowledgeValidationAdapterBase : IDomainKnowledgeValidationAdapter
{
    private readonly StoragePaths _storagePaths;

    protected DomainKnowledgeValidationAdapterBase(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public abstract bool Supports(string requirementType);

    public DomainKnowledgeValidationResult Validate(
        KnowledgeCatalogItem item,
        KnowledgeValidationPlan plan,
        KnowledgeValidationRequirement requirement)
    {
        if (!Supports(requirement.RequirementType))
        {
            return new DomainKnowledgeValidationResult(
                ValidationStatus: "unsupported",
                EvidenceStrength: 0,
                MissingEvidence: [$"{requirement.RequirementType}_not_supported"],
                Warnings: [$"unsupported_domain_validation_requirement:{item.Domain}:{requirement.RequirementType}"],
                QualityDeltaHint: 0,
                Recommendation: "needs_more_evidence",
                EvidenceRefs: [],
                OutputPaths: [new KnowledgeCatalog(_storagePaths).CatalogPath],
                Summary: $"Requirement '{requirement.RequirementType}' is not supported by the {item.Domain} adapter.");
        }

        return ValidateCore(item, plan, requirement);
    }

    protected abstract DomainKnowledgeValidationResult ValidateCore(
        KnowledgeCatalogItem item,
        KnowledgeValidationPlan plan,
        KnowledgeValidationRequirement requirement);

    protected DomainValidationContext BuildContext(KnowledgeCatalogItem item)
    {
        var catalog = new KnowledgeCatalog(_storagePaths);
        var sourcesPath = new KnowledgeSourceRegistry(_storagePaths).SourcesPath;
        var sources = new KnowledgeSourceRegistry(_storagePaths).LoadOrCreateSources();
        var matched = item.SourceIds
            .Select(sourceId => sources.FirstOrDefault(source => source.SourceId.Equals(sourceId, StringComparison.OrdinalIgnoreCase)))
            .Where(source => source is not null)
            .Cast<CognitiveSource>()
            .ToList();
        var catalogItems = catalog.LoadOrCreateItems()
            .ToDictionary(entry => entry.Id, StringComparer.OrdinalIgnoreCase);
        var relatedResolvable = item.RelatedItems
            .Count(relatedId => catalogItems.ContainsKey(relatedId));
        return new DomainValidationContext(
            MatchedSources: matched,
            RelatedItemsResolvable: relatedResolvable,
            SourceReferencesUsable: matched.Any(source => HasText(source.UrlOrPath)),
            HasPathOrRepoReference: matched.Any(source => HasPathOrRepoReference(source.UrlOrPath)),
            OutputPaths: [catalog.CatalogPath, sourcesPath]);
    }

    protected DomainKnowledgeValidationResult ResultFor(
        string domain,
        KnowledgeCatalogItem item,
        KnowledgeValidationRequirement requirement,
        int checks,
        int passed,
        IReadOnlyList<string> missing,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> evidence,
        IReadOnlyList<string> outputPaths,
        string positiveRecommendation)
    {
        var strength = checks <= 0 ? 0 : Math.Round(Math.Clamp(passed / (double)checks, 0, 1), 4);
        var recommendation = RecommendationFor(strength, missing.Count, warnings, positiveRecommendation);
        var validationStatus = recommendation switch
        {
            "reject" => "rejected",
            "mark_deprecated" => "deprecated",
            "promote_to_promising" => "validated",
            _ => "needs_more_evidence"
        };
        var evidenceRefs = evidence
            .Concat([
                $"domain_validation:{domain}:{requirement.RequirementType}:{validationStatus}",
                $"domain_evidence_strength:{strength:0.####}",
                $"domain_quality_delta_hint:{QualityDeltaHint(strength, missing.Count, warnings):0.####}",
                $"domain_validation_recommendation:{recommendation}"
            ])
            .Concat(missing.Select(item => $"missing_evidence:{item}"))
            .Concat(warnings.Select(item => $"domain_validation_warning:{item}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(60)
            .ToList();

        return new DomainKnowledgeValidationResult(
            ValidationStatus: validationStatus,
            EvidenceStrength: strength,
            MissingEvidence: missing.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList(),
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList(),
            QualityDeltaHint: QualityDeltaHint(strength, missing.Count, warnings),
            Recommendation: recommendation,
            EvidenceRefs: evidenceRefs,
            OutputPaths: outputPaths.Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList(),
            Summary: $"{domain} {requirement.RequirementType} completed; checks={checks}; passed={passed}; evidence_strength={strength:0.####}; recommendation={recommendation}.");
    }

    protected static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);

    protected static bool ContainsAny(string value, params string[] tokens) =>
        tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));

    protected static bool IsFresh(DateTimeOffset? timestamp) =>
        timestamp is not null && DateTimeOffset.UtcNow - timestamp.Value <= TimeSpan.FromDays(180);

    protected static bool RelatedItemsResolvable(KnowledgeCatalogItem item, DomainValidationContext context) =>
        item.RelatedItems.Count == 0 || context.RelatedItemsResolvable == item.RelatedItems.Count;

    protected static string CombinedText(KnowledgeCatalogItem item, DomainValidationContext context) =>
        string.Join(" ", [
            item.Id,
            item.Title,
            item.DescriptionShort,
            string.Join(" ", item.Tags),
            string.Join(" ", context.MatchedSources.Select(source => $"{source.SourceName} {source.UrlOrPath} {source.SourceType} {source.ExtractionStatus} {string.Join(" ", source.ExtractedConcepts)} {string.Join(" ", source.RiskFlags)}"))
        ]);

    protected static bool LooksLikeCodeModule(KnowledgeCatalogItem item) =>
        ContainsAny(item.Id, ".cs", ".py", ".ts", ".tsx", "runtime", "cli", "service", "adapter")
        || ContainsAny(item.Title, "module", "service", "adapter", "runtime", "cli");

    protected static bool HasRiskFlag(DomainValidationContext context, KnowledgeCatalogItem item) =>
        item.Tags.Any(tag => tag.Contains("risk", StringComparison.OrdinalIgnoreCase))
        || context.MatchedSources.Any(source => source.RiskFlags.Count > 0 || source.TrustProfile.RiskFlags.Count > 0);

    protected static bool HasPathOrRepoReference(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Contains("github.com", StringComparison.OrdinalIgnoreCase)
            || value.Contains(".git", StringComparison.OrdinalIgnoreCase)
            || value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }

    private static string RecommendationFor(double strength, int missingCount, IReadOnlyList<string> warnings, string positiveRecommendation)
    {
        if (warnings.Any(warning => warning.Contains("risk_flags_present", StringComparison.OrdinalIgnoreCase)))
        {
            return "needs_more_evidence";
        }

        if (strength >= 0.78 && missingCount == 0)
        {
            return positiveRecommendation;
        }

        if (strength >= 0.45)
        {
            return "needs_more_evidence";
        }

        return "keep_weak";
    }

    private static double QualityDeltaHint(double strength, int missingCount, IReadOnlyList<string> warnings)
    {
        var raw = strength * 0.08 - Math.Min(0.05, missingCount * 0.01) - Math.Min(0.04, warnings.Count * 0.008);
        return Math.Round(Math.Clamp(raw, -0.05, 0.1), 4);
    }
}

public sealed record DomainValidationContext(
    IReadOnlyList<CognitiveSource> MatchedSources,
    int RelatedItemsResolvable,
    bool SourceReferencesUsable,
    bool HasPathOrRepoReference,
    IReadOnlyList<string> OutputPaths);

public sealed record DomainValidationStatusReport(
    string StatusVersion,
    DateTimeOffset UpdatedAtUtc,
    string DomainValidationHealth,
    int DocumentationValidationPending,
    int SoftwareValidationPending,
    int ProcessValidationPending,
    int ResearchValidationPending,
    IReadOnlyList<string> DomainValidationWarnings,
    string PlansPath,
    string ExecutionLogPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class DomainKnowledgeValidationService
{
    private readonly StoragePaths _storagePaths;

    public DomainKnowledgeValidationService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "cognitive_core");

    public string StatusPath => Path.Combine(Root, "domain_validation_status.json");

    public DomainValidationStatusReport BuildStatus()
    {
        var strategy = new KnowledgeValidationStrategy(_storagePaths);
        var report = strategy.LoadPlanReport() ?? strategy.GeneratePlans(50);
        var warnings = new List<string>();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["documentation"] = PendingFor(report, "documentation"),
            ["software"] = PendingFor(report, "software"),
            ["process"] = PendingFor(report, "process"),
            ["research"] = PendingFor(report, "research")
        };

        foreach (var entry in counts.Where(entry => entry.Value > 0))
        {
            warnings.Add($"{entry.Key}_validation_pending:{entry.Value}");
        }

        var health = counts.Values.Sum() == 0
            ? "ok"
            : counts.Values.Any(value => value > 50)
                ? "needs_attention"
                : "pending";
        var status = new DomainValidationStatusReport(
            StatusVersion: "domain_validation_status_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            DomainValidationHealth: health,
            DocumentationValidationPending: counts["documentation"],
            SoftwareValidationPending: counts["software"],
            ProcessValidationPending: counts["process"],
            ResearchValidationPending: counts["research"],
            DomainValidationWarnings: warnings,
            PlansPath: strategy.PlansPath,
            ExecutionLogPath: new KnowledgeValidationExecutor(_storagePaths).ExecutionLogPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
        try
        {
            Directory.CreateDirectory(Root);
            File.WriteAllText(StatusPath, System.Text.Json.JsonSerializer.Serialize(status, JsonDefaults.WriteOptions));
        }
        catch (IOException ex)
        {
            warnings.Add($"domain_validation_status_write_failed:{SanitizeMessage(ex.Message)}");
            status = status with { DomainValidationWarnings = warnings };
        }
        catch (UnauthorizedAccessException ex)
        {
            warnings.Add($"domain_validation_status_write_failed:{SanitizeMessage(ex.Message)}");
            status = status with { DomainValidationWarnings = warnings };
        }
        return status;
    }

    private static int PendingFor(KnowledgeValidationPlanReport report, string domain) =>
        report.Plans
            .Where(plan => plan.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase))
            .SelectMany(plan => plan.Requirements)
            .Count(requirement => !requirement.Status.Equals("satisfied", StringComparison.OrdinalIgnoreCase));

    private static string SanitizeMessage(string? message)
    {
        return string.IsNullOrWhiteSpace(message)
            ? "unknown_io_error"
            : message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Trim();
    }
}
