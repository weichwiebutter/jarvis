using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeConsolidationExecutorCandidate(
    string ConsolidationCandidateId,
    string Domain,
    string Title,
    string Summary,
    string PatternDescription,
    int SupportingItemsCount,
    int DuplicateItemsCount,
    double EvidenceStrength,
    string ValidationStatus,
    double TrustBaseline,
    string RiskNotes,
    string RecommendedNextAction,
    bool FrankRequired,
    IReadOnlyList<string> ItemIds,
    IReadOnlyList<string> ItemTitles,
    IReadOnlyList<string> SampleSources);

public sealed record KnowledgeConsolidationExecutorReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int AnalyzerClusterCount,
    int CandidatesPreparedCount,
    int RawItemsCount,
    int DuplicateItemsCount,
    int ConsolidatableGroupCount,
    int TrustedKnowledgeItems,
    int WeakKnowledgeItems,
    IReadOnlyList<KnowledgeConsolidationExecutorCandidate> Candidates,
    IReadOnlyList<string> Domains,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string SafetySummary,
    bool FrankRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class KnowledgeConsolidationExecutorService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public KnowledgeConsolidationExecutorService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_consolidation");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "knowledge_consolidation_executor.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "knowledge_consolidation_executor.md");

    public KnowledgeConsolidationExecutorReport Run()
    {
        Directory.CreateDirectory(Root);

        var analyzer = new KnowledgeConsolidationAnalyzerService(_storagePaths).Run();
        var candidates = analyzer.Clusters
            .Where(cluster => cluster.RawItemCount > 1)
            .Select(cluster => MapCandidate(cluster))
            .OrderByDescending(candidate => candidate.SupportingItemsCount)
            .ThenByDescending(candidate => candidate.EvidenceStrength)
            .ThenBy(candidate => candidate.Domain, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var report = new KnowledgeConsolidationExecutorReport(
            ReportVersion: "knowledge_consolidation_executor_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            AnalyzerClusterCount: analyzer.ClusterCount,
            CandidatesPreparedCount: candidates.Count,
            RawItemsCount: analyzer.RawItems.Count,
            DuplicateItemsCount: analyzer.DuplicateCount,
            ConsolidatableGroupCount: analyzer.ConsolidatableGroupCount,
            TrustedKnowledgeItems: analyzer.TrustedKnowledgeItems,
            WeakKnowledgeItems: analyzer.WeakKnowledgeItems,
            Candidates: candidates,
            Domains: analyzer.Domains,
            Warnings: analyzer.Warnings,
            OperatorSummary: $"{analyzer.ClusterCount} Muster erkannt. Davon wurden {candidates.Count} als Konsolidierungs-Kandidaten vorbereitet. Frank muss nichts freigeben. Keine Rohdaten wurden gelöscht.",
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true",
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

    public KnowledgeConsolidationExecutorReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeConsolidationExecutorReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static KnowledgeConsolidationExecutorCandidate MapCandidate(KnowledgeConsolidationAnalyzerCluster cluster)
    {
        var evidenceStrength = Math.Round((cluster.AverageEvidenceScore + cluster.AverageValidationScore) / 2, 4);
        var riskNotes = BuildRiskNotes(cluster);
        var recommendedNextAction = cluster.Domain switch
        {
            "trading" => "Pattern-Regel als Review-Kandidat vorbereiten",
            "research" => "Research-Muster verdichten und später validieren",
            "documentation" => "Dokumentationsmuster gruppieren",
            "process" => "Prozessmuster bündeln",
            _ => "Muster weiter konsolidieren"
        };

        return new KnowledgeConsolidationExecutorCandidate(
            ConsolidationCandidateId: $"consolidation_{cluster.ClusterId}",
            Domain: cluster.Domain,
            Title: cluster.PatternDescription,
            Summary: cluster.RuleCandidateSummary,
            PatternDescription: cluster.PatternDescription,
            SupportingItemsCount: cluster.RawItemCount,
            DuplicateItemsCount: cluster.DuplicateCount,
            EvidenceStrength: evidenceStrength,
            ValidationStatus: cluster.ValidationState,
            TrustBaseline: cluster.AverageTrustScore,
            RiskNotes: riskNotes,
            RecommendedNextAction: recommendedNextAction,
            FrankRequired: false,
            ItemIds: cluster.ItemIds,
            ItemTitles: cluster.ItemTitles,
            SampleSources: cluster.SampleSources);
    }

    private static string BuildRiskNotes(KnowledgeConsolidationAnalyzerCluster cluster)
    {
        var notes = new List<string>();
        if (cluster.DuplicateCount > 0)
        {
            notes.Add("Dubletten nur als Kandidat verdichtet, keine Löschung");
        }
        if (cluster.AverageValidationScore < 0.6)
        {
            notes.Add("Validierung noch nicht stark genug");
        }
        if (cluster.AverageTrustScore < 0.6)
        {
            notes.Add("Vertrauensbasis noch mittel");
        }
        return notes.Count == 0 ? "Keine akuten Risiken" : string.Join("; ", notes);
    }

    private void WriteReport(KnowledgeConsolidationExecutorReport report)
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
            var fallbackReportPath = Path.Combine(fallbackRoot, "knowledge_consolidation_executor.json");
            var fallbackMarkdownPath = Path.Combine(fallbackRoot, "knowledge_consolidation_executor.md");
            File.WriteAllText(fallbackReportPath, json);
            File.WriteAllText(fallbackMarkdownPath, markdown);
            _resolvedReportPath = fallbackReportPath;
            _resolvedMarkdownPath = fallbackMarkdownPath;
        }
    }

    private static string BuildMarkdown(KnowledgeConsolidationExecutorReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Knowledge Consolidation Executor");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Analyzer clusters: {report.AnalyzerClusterCount}");
        sb.AppendLine($"- Candidates prepared: {report.CandidatesPreparedCount}");
        sb.AppendLine($"- Raw items: {report.RawItemsCount}");
        sb.AppendLine($"- Duplicate items: {report.DuplicateItemsCount}");
        sb.AppendLine($"- Consolidatable groups: {report.ConsolidatableGroupCount}");
        sb.AppendLine($"- Trusted knowledge items: {report.TrustedKnowledgeItems}");
        sb.AppendLine($"- Weak knowledge items: {report.WeakKnowledgeItems}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Safety");
        sb.AppendLine($"- {report.SafetySummary}");
        sb.AppendLine();
        sb.AppendLine("## Candidates");
        foreach (var candidate in report.Candidates.Take(50))
        {
            sb.AppendLine($"- {candidate.Domain}: {candidate.Title} · raw={candidate.SupportingItemsCount} · dup={candidate.DuplicateItemsCount} · evidence={candidate.EvidenceStrength:0.####} · validation={candidate.ValidationStatus} · trust={candidate.TrustBaseline:0.####}");
        }
        return sb.ToString();
    }
}
