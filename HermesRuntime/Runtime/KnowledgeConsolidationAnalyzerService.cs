using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeConsolidationAnalyzerItem(
    string ItemId,
    string Domain,
    string ItemType,
    string Title,
    string NormalizedSignature,
    double TrustScore,
    double EvidenceScore,
    double ValidationScore,
    double ConfidenceScore,
    DateTimeOffset? LastValidatedUtc,
    IReadOnlyList<string> SourceRefs);

public sealed record KnowledgeConsolidationAnalyzerCluster(
    string ClusterId,
    string Domain,
    string PatternDescription,
    string NormalizedSignature,
    int RawItemCount,
    int KnowledgeItemCount,
    int HypothesisCount,
    int ObservationCount,
    int DuplicateCount,
    int ConsolidatableCount,
    double AverageTrustScore,
    double AverageEvidenceScore,
    double AverageValidationScore,
    double ConfidenceScore,
    string ValidationState,
    string TrustState,
    string NextAction,
    string RuleCandidateSummary,
    bool FrankRequired,
    bool SafeToExecute,
    IReadOnlyList<string> ItemIds,
    IReadOnlyList<string> ItemTitles,
    IReadOnlyList<string> SampleSources);

public sealed record KnowledgeConsolidationAnalyzerReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalKnowledgeItems,
    int RawObservationCount,
    int RawHypothesisCount,
    int RawResearchResultCount,
    int ClusterCount,
    int DuplicateCount,
    int ConsolidatableGroupCount,
    int ActiveItemCount,
    int ArchivedPotentialCount,
    int RedundantItemCount,
    int TrustedKnowledgeItems,
    int WeakKnowledgeItems,
    IReadOnlyList<KnowledgeConsolidationAnalyzerCluster> Clusters,
    IReadOnlyList<KnowledgeConsolidationAnalyzerItem> RawItems,
    IReadOnlyList<string> Domains,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string CleanupPotentialSummary,
    bool FrankRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class KnowledgeConsolidationAnalyzerService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public KnowledgeConsolidationAnalyzerService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_consolidation");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "knowledge_consolidation_analyzer.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "knowledge_consolidation_analyzer.md");

    public KnowledgeConsolidationAnalyzerReport Run()
    {
        Directory.CreateDirectory(Root);

        var quality = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var research = new StrategyResearchService(_storagePaths).LoadOrCreateMemory();
        var observations = LoadResearchObservations();
        var hypotheses = LoadHypotheses();
        var items = BuildItems(quality, catalog, research, observations, hypotheses);
        var clusters = BuildClusters(items);
        var duplicateCount = clusters.Sum(cluster => Math.Max(0, cluster.RawItemCount - 1));
        var consolidatableGroupCount = clusters.Count(cluster => cluster.RawItemCount > 1);
        var archivedPotentialCount = clusters.Sum(cluster => Math.Max(0, cluster.DuplicateCount));
        var redundantItemCount = clusters.Sum(cluster => Math.Max(0, cluster.DuplicateCount));
        var trustedKnowledgeItems = quality.Items.Count(item => item.LifecycleStatus.Equals("trusted", StringComparison.OrdinalIgnoreCase));
        var weakKnowledgeItems = quality.WeakKnowledge;
        var domains = items.Select(item => item.Domain).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(domain => domain, StringComparer.OrdinalIgnoreCase).ToList();
        var warnings = new List<string>();
        if (items.Count == 0)
        {
            warnings.Add("knowledge_consolidation_empty");
        }

        var operatorSummary = $"{items.Count} Einträge beschreiben {clusters.Count} ähnliche Muster. Hermes kann Muster verdichten, Frank muss nichts freigeben.";
        var cleanupPotentialSummary = $"{archivedPotentialCount} Einträge könnten später archiviert werden; {redundantItemCount} Einträge wirken redundant; {items.Count - redundantItemCount} Einträge werden aktiv genutzt.";

        var report = new KnowledgeConsolidationAnalyzerReport(
            ReportVersion: "knowledge_consolidation_analyzer_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TotalKnowledgeItems: quality.TotalKnowledgeItems,
            RawObservationCount: observations.Count,
            RawHypothesisCount: hypotheses.Count,
            RawResearchResultCount: research.ResearchEntries.Count,
            ClusterCount: clusters.Count,
            DuplicateCount: duplicateCount,
            ConsolidatableGroupCount: consolidatableGroupCount,
            ActiveItemCount: items.Count - redundantItemCount,
            ArchivedPotentialCount: archivedPotentialCount,
            RedundantItemCount: redundantItemCount,
            TrustedKnowledgeItems: trustedKnowledgeItems,
            WeakKnowledgeItems: weakKnowledgeItems,
            Clusters: clusters,
            RawItems: items,
            Domains: domains,
            Warnings: warnings,
            OperatorSummary: operatorSummary,
            CleanupPotentialSummary: cleanupPotentialSummary,
            FrankRequired: false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteReport(report);
        return report;
    }

    public KnowledgeConsolidationAnalyzerReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeConsolidationAnalyzerReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<KnowledgeConsolidationAnalyzerItem> BuildItems(
        KnowledgeQualityReport quality,
        IReadOnlyList<KnowledgeCatalogItem> catalog,
        StrategyResearchMemory research,
        IReadOnlyList<KnowledgeConsolidationObservation> observations,
        IReadOnlyList<KnowledgeConsolidationHypothesis> hypotheses)
    {
        var items = new List<KnowledgeConsolidationAnalyzerItem>();
        var researchEntries = research.ResearchEntries ?? [];

        items.AddRange(catalog.Select(item => new KnowledgeConsolidationAnalyzerItem(
            ItemId: item.Id,
            Domain: item.Domain,
            ItemType: "knowledge_item",
            Title: item.Title,
            NormalizedSignature: NormalizeSignature($"{item.Domain} {item.Title} {item.DescriptionShort} {string.Join(' ', item.Tags)}"),
            TrustScore: quality.Items.FirstOrDefault(q => q.KnowledgeId.Equals(item.Id, StringComparison.OrdinalIgnoreCase))?.TrustScore ?? 0.5,
            EvidenceScore: quality.Items.FirstOrDefault(q => q.KnowledgeId.Equals(item.Id, StringComparison.OrdinalIgnoreCase))?.EvidenceScore ?? 0.5,
            ValidationScore: quality.Items.FirstOrDefault(q => q.KnowledgeId.Equals(item.Id, StringComparison.OrdinalIgnoreCase))?.ValidationScore ?? 0.5,
            ConfidenceScore: item.Confidence,
            LastValidatedUtc: item.LastValidatedUtc,
            SourceRefs: item.SourceIds)));

        items.AddRange(observations.Select(item => new KnowledgeConsolidationAnalyzerItem(
            ItemId: item.ObservationId,
            Domain: item.Domain,
            ItemType: "trading_observation",
            Title: item.Title,
            NormalizedSignature: NormalizeSignature($"{item.Domain} {item.Title} {item.Summary} {string.Join(' ', item.Tags)}"),
            TrustScore: item.TrustScore,
            EvidenceScore: item.EvidenceScore,
            ValidationScore: item.ValidationScore,
            ConfidenceScore: item.Confidence,
            LastValidatedUtc: item.LastValidatedUtc,
            SourceRefs: item.SourceRefs)));

        items.AddRange(hypotheses.Select(item => new KnowledgeConsolidationAnalyzerItem(
            ItemId: item.HypothesisId,
            Domain: item.Domain,
            ItemType: "hypothesis",
            Title: item.Title,
            NormalizedSignature: NormalizeSignature($"{item.Domain} {item.Title} {item.Description} {item.ProposedValidation}"),
            TrustScore: item.TrustScore,
            EvidenceScore: item.EvidenceScore,
            ValidationScore: item.ValidationScore,
            ConfidenceScore: Math.Clamp((item.TrustScore + item.EvidenceScore) / 2, 0, 1),
            LastValidatedUtc: null,
            SourceRefs: item.SourceItemIds)));

        items.AddRange(researchEntries.Select(entry => new KnowledgeConsolidationAnalyzerItem(
            ItemId: $"{entry.PatternId}:{entry.StrategyVariantId}:{entry.Symbol}:{entry.Timeframe}",
            Domain: "research",
            ItemType: "research_entry",
            Title: entry.PatternId,
            NormalizedSignature: NormalizeSignature($"{entry.PatternId} {entry.Status}"),
            TrustScore: 0.55,
            EvidenceScore: entry.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) ? 0.65 : 0.45,
            ValidationScore: entry.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) ? 0.7 : 0.45,
            ConfidenceScore: entry.FitnessScore,
            LastValidatedUtc: entry.ToUtc ?? entry.FromUtc,
            SourceRefs: [entry.PatternId, entry.StrategyVariantId, entry.Symbol, entry.Timeframe])));

        return items
            .OrderBy(item => item.Domain, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ItemType, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ItemId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<KnowledgeConsolidationAnalyzerCluster> BuildClusters(IReadOnlyList<KnowledgeConsolidationAnalyzerItem> items)
    {
        return items
            .GroupBy(item => $"{item.Domain}:{item.NormalizedSignature}", StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group.OrderByDescending(item => item.ConfidenceScore).ThenBy(item => item.ItemId, StringComparer.OrdinalIgnoreCase).ToList();
                var averageTrust = Average(ordered, item => item.TrustScore);
                var averageEvidence = Average(ordered, item => item.EvidenceScore);
                var averageValidation = Average(ordered, item => item.ValidationScore);
                var duplicateCount = Math.Max(0, ordered.Count - 1);
                var consolidatableCount = ordered.Count(item => item.ConfidenceScore < 0.75 || item.ValidationScore < 0.75);
                var patternDescription = BuildPatternDescription(ordered);
                var validationState = averageValidation >= 0.75 ? "validiert" : averageValidation >= 0.5 ? "teilvalidiert" : "offen";
                var trustState = averageTrust >= 0.75 ? "stark" : averageTrust >= 0.5 ? "mittel" : "schwach";
                var nextAction = ordered.First().ItemType switch
                {
                    "trading_observation" => "Trading-Beobachtung verdichten",
                    "hypothesis" => "Hypothesen zusammenführen",
                    "research_entry" => "Research-Einträge gruppieren",
                    _ => "Cluster verdichten"
                };
                var ruleCandidateSummary = BuildRuleCandidateSummary(ordered, averageTrust, averageEvidence, averageValidation);

                return new KnowledgeConsolidationAnalyzerCluster(
                    ClusterId: $"cluster_{group.Key.GetHashCode():x8}",
                    Domain: group.First().Domain,
                    PatternDescription: patternDescription,
                    NormalizedSignature: group.Key,
                    RawItemCount: ordered.Count,
                    KnowledgeItemCount: ordered.Count(item => item.ItemType == "knowledge_item"),
                    HypothesisCount: ordered.Count(item => item.ItemType == "hypothesis"),
                    ObservationCount: ordered.Count(item => item.ItemType == "trading_observation" || item.ItemType == "research_entry"),
                    DuplicateCount: duplicateCount,
                    ConsolidatableCount: consolidatableCount,
                    AverageTrustScore: averageTrust,
                    AverageEvidenceScore: averageEvidence,
                    AverageValidationScore: averageValidation,
                    ConfidenceScore: Average(ordered, item => item.ConfidenceScore),
                    ValidationState: validationState,
                    TrustState: trustState,
                    NextAction: nextAction,
                    RuleCandidateSummary: ruleCandidateSummary,
                    FrankRequired: false,
                    SafeToExecute: true,
                    ItemIds: ordered.Select(item => item.ItemId).ToList(),
                    ItemTitles: ordered.Select(item => item.Title).Take(5).ToList(),
                    SampleSources: ordered.SelectMany(item => item.SourceRefs).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToList());
            })
            .OrderByDescending(cluster => cluster.RawItemCount)
            .ThenByDescending(cluster => cluster.AverageValidationScore)
            .ThenBy(cluster => cluster.Domain, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private IReadOnlyList<KnowledgeConsolidationObservation> LoadResearchObservations()
    {
        var insights = new ResearchInsightsGenerator(_storagePaths).LoadInsights();
        var results = new List<KnowledgeConsolidationObservation>();
        if (insights is null)
        {
            return results;
        }

        foreach (var cluster in insights.Clusters)
        {
            results.Add(new KnowledgeConsolidationObservation(
                ObservationId: cluster.ClusterId,
                Domain: "research",
                Title: cluster.Family,
                Summary: string.Join(", ", cluster.CommonParameters),
                Tags: cluster.CommonParameters,
                TrustScore: cluster.AverageFitness,
                EvidenceScore: Math.Clamp(cluster.AverageWinrate, 0, 1),
                ValidationScore: cluster.Prioritized ? 0.7 : cluster.Reduced ? 0.35 : 0.5,
                Confidence: cluster.BestFitness,
                LastValidatedUtc: insights.GeneratedAtUtc,
                SourceRefs: [new ResearchInsightsGenerator(_storagePaths).ClustersPath]));
        }

        return results;
    }

    private IReadOnlyList<KnowledgeConsolidationHypothesis> LoadHypotheses()
    {
        var hypothesisPath = Path.Combine(_storagePaths.Root, "cognitive_core", "hypotheses.json");
        if (!File.Exists(hypothesisPath))
        {
            return [];
        }

        try
        {
            var root = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(hypothesisPath), JsonDefaults.SnapshotReadOptions);
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("hypotheses", out var hypothesesElement) && hypothesesElement.ValueKind == JsonValueKind.Array)
            {
                return hypothesesElement.EnumerateArray()
                    .Select(element => new KnowledgeConsolidationHypothesis(
                        HypothesisId: element.TryGetProperty("hypothesis_id", out var id) ? id.GetString() ?? "" : "",
                        Domain: element.TryGetProperty("domain", out var domain) ? domain.GetString() ?? "research" : "research",
                        Title: element.TryGetProperty("title", out var title) ? title.GetString() ?? "" : "",
                        Description: element.TryGetProperty("description", out var description) ? description.GetString() ?? "" : "",
                        ProposedValidation: element.TryGetProperty("proposed_validation", out var validation) ? validation.GetString() ?? "" : "",
                        Status: element.TryGetProperty("status", out var status) ? status.GetString() ?? "open" : "open",
                        TrustScore: element.TryGetProperty("trust_score", out var trust) && trust.TryGetDouble(out var trustScore) ? trustScore : 0.5,
                        EvidenceScore: element.TryGetProperty("evidence_score", out var evidence) && evidence.TryGetDouble(out var evidenceScore) ? evidenceScore : 0.5,
                        ValidationScore: element.TryGetProperty("validation_score", out var validationScoreElement) && validationScoreElement.TryGetDouble(out var validationScore) ? validationScore : 0.5,
                        SourceItemIds: element.TryGetProperty("source_item_ids", out var sources) && sources.ValueKind == JsonValueKind.Array
                            ? sources.EnumerateArray().Select(item => item.GetString() ?? "").Where(value => !string.IsNullOrWhiteSpace(value)).ToList()
                            : [],
                        Tags: element.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array
                            ? tags.EnumerateArray().Select(item => item.GetString() ?? "").Where(value => !string.IsNullOrWhiteSpace(value)).ToList()
                            : []))
                    .Where(item => !string.IsNullOrWhiteSpace(item.HypothesisId))
                    .ToList();
            }
        }
        catch
        {
        }

        return [];
    }

    private static string BuildPatternDescription(IReadOnlyList<KnowledgeConsolidationAnalyzerItem> items)
    {
        var title = items.First().Title;
        if (items.Count == 1)
        {
            return title;
        }

        var sharedDomain = items.First().Domain;
        return $"{items.Count} Einträge zu {sharedDomain}:{title}";
    }

    private static string BuildRuleCandidateSummary(IReadOnlyList<KnowledgeConsolidationAnalyzerItem> items, double trust, double evidence, double validation)
    {
        var trustState = trust >= 0.75 ? "stark" : trust >= 0.5 ? "mittel" : "schwach";
        var evidenceState = evidence >= 0.75 ? "stark" : evidence >= 0.5 ? "mittel" : "schwach";
        var validationState = validation >= 0.75 ? "stark" : validation >= 0.5 ? "mittel" : "schwach";
        return $"Musterkandidat aus {items.Count} Einträgen · Vertrauen {trustState} · Evidenz {evidenceState} · Validierung {validationState}";
    }

    private static double Average(IReadOnlyList<KnowledgeConsolidationAnalyzerItem> items, Func<KnowledgeConsolidationAnalyzerItem, double> selector) =>
        items.Count == 0 ? 0 : Math.Round(items.Average(selector), 4);

    private static string NormalizeSignature(string text)
    {
        var tokens = text
            .ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\r', '\n', '-', '_', '/', '\\', '.', ',', ';', ':', '(', ')', '[', ']', '{', '}', '|', '+' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length > 2)
            .Where(token => !StopWords.Contains(token))
            .Take(8)
            .ToList();

        return tokens.Count == 0 ? "generic" : string.Join("_", tokens);
    }

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "or", "the", "for", "with", "from", "this", "that", "und", "oder", "der", "die", "das", "ein", "eine", "auf", "von", "mit", "im", "in", "to", "of", "a", "an", "is", "are"
    };

    private void WriteReport(KnowledgeConsolidationAnalyzerReport report)
    {
        try
        {
            var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
            var markdown = BuildMarkdown(report);
            File.WriteAllText(ReportPath, json);
            File.WriteAllText(MarkdownPath, markdown);
            _resolvedReportPath = ReportPath;
            _resolvedMarkdownPath = MarkdownPath;
        }
        catch
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "knowledge_consolidation");
            Directory.CreateDirectory(fallbackRoot);
            var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
            var markdown = BuildMarkdown(report);
            var fallbackReportPath = Path.Combine(fallbackRoot, "knowledge_consolidation_analyzer.json");
            var fallbackMarkdownPath = Path.Combine(fallbackRoot, "knowledge_consolidation_analyzer.md");
            File.WriteAllText(fallbackReportPath, json);
            File.WriteAllText(fallbackMarkdownPath, markdown);
            _resolvedReportPath = fallbackReportPath;
            _resolvedMarkdownPath = fallbackMarkdownPath;
        }
    }

    private static string BuildMarkdown(KnowledgeConsolidationAnalyzerReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Knowledge Consolidation Analyzer");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Total Knowledge Items: {report.TotalKnowledgeItems}");
        sb.AppendLine($"- Raw Items: {report.RawObservationCount + report.RawHypothesisCount + report.RawResearchResultCount}");
        sb.AppendLine($"- Clusters: {report.ClusterCount}");
        sb.AppendLine($"- Duplicate Count: {report.DuplicateCount}");
        sb.AppendLine($"- Consolidatable Groups: {report.ConsolidatableGroupCount}");
        sb.AppendLine($"- Active Item Count: {report.ActiveItemCount}");
        sb.AppendLine($"- Archived Potential Count: {report.ArchivedPotentialCount}");
        sb.AppendLine($"- Redundant Item Count: {report.RedundantItemCount}");
        sb.AppendLine($"- Trusted Knowledge Items: {report.TrustedKnowledgeItems}");
        sb.AppendLine($"- Weak Knowledge Items: {report.WeakKnowledgeItems}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Cleanup Potential");
        sb.AppendLine(report.CleanupPotentialSummary);
        sb.AppendLine();
        sb.AppendLine("## Clusters");
        foreach (var cluster in report.Clusters)
        {
            sb.AppendLine($"- {cluster.Domain}: {cluster.PatternDescription} · raw={cluster.RawItemCount} · dup={cluster.DuplicateCount} · trust={cluster.AverageTrustScore:0.####} · evidence={cluster.AverageEvidenceScore:0.####} · validation={cluster.AverageValidationScore:0.####}");
        }
        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine("- no_auto_trading=true");
        sb.AppendLine("- human_review_required=true");
        sb.AppendLine("- broker_orders_enabled=false");
        sb.AppendLine("- live_trading_enabled=false");
        sb.AppendLine("- research_only=true");
        return sb.ToString();
    }
}

public sealed record KnowledgeConsolidationObservation(
    string ObservationId,
    string Domain,
    string Title,
    string Summary,
    IReadOnlyList<string> Tags,
    double TrustScore,
    double EvidenceScore,
    double ValidationScore,
    double Confidence,
    DateTimeOffset? LastValidatedUtc,
    IReadOnlyList<string> SourceRefs);

public sealed record KnowledgeConsolidationHypothesis(
    string HypothesisId,
    string Domain,
    string Title,
    string Description,
    string ProposedValidation,
    string Status,
    double TrustScore,
    double EvidenceScore,
    double ValidationScore,
    IReadOnlyList<string> SourceItemIds,
    IReadOnlyList<string> Tags);
