using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ReviewPrioritizationEntry(
    string ReviewId,
    string KnowledgeItemId,
    string Title,
    string Domain,
    double TrustBefore,
    double ReviewActionScore,
    string RecommendationClass,
    string Recommendation,
    string Reason,
    string Priority,
    string PriorityReason,
    IReadOnlyList<string> MissingEvidence,
    string WhyNow,
    string NextStep);

public sealed record ReviewPrioritizationDomainGroup(
    string Domain,
    int Count,
    IReadOnlyList<ReviewPrioritizationEntry> Reviews);

public sealed record ReviewPrioritizationAuditReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int TotalPendingReviews,
    int TradingReviews,
    int DocumentationReviews,
    int ResearchReviews,
    int SoftwareReviews,
    int ProcessReviews,
    IReadOnlyList<ReviewPrioritizationDomainGroup> DomainGroups,
    IReadOnlyList<ReviewPrioritizationEntry> TopPriorityReviews,
    string OperatorSummary,
    string ReportPath,
    string MarkdownPath,
    IReadOnlyList<string> Warnings,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class ReviewPrioritizationAuditService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public ReviewPrioritizationAuditService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "review_prioritization_audit");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "review_prioritization_audit.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "review_prioritization_audit.md");

    public ReviewPrioritizationAuditReport Run()
    {
        var (reportPath, markdownPath, root) = ResolveOutputPaths();
        _resolvedReportPath = reportPath;
        _resolvedMarkdownPath = markdownPath;

        var workflow = new HumanReviewWorkflow(_storagePaths);
        var queue = workflow.LoadOrCreateQueue();
        var pendingReviews = queue.Items
            .Where(item => item.Status.Equals("pending", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var entries = pendingReviews
            .Select(BuildEntry)
            .OrderByDescending(entry => entry.ReviewActionScore)
            .ThenByDescending(entry => PriorityRank(entry.Priority))
            .ThenByDescending(entry => entry.TrustBefore)
            .ThenBy(entry => entry.Domain, StringComparer.Ordinal)
            .ThenBy(entry => entry.Title, StringComparer.Ordinal)
            .ToList();

        var grouped = entries
            .GroupBy(entry => NormalizeGroupDomain(entry.Domain), StringComparer.OrdinalIgnoreCase)
            .Select(group => new ReviewPrioritizationDomainGroup(
                Domain: group.Key,
                Count: group.Count(),
                Reviews: group.OrderByDescending(entry => PriorityRank(entry.Priority))
                    .ThenByDescending(entry => entry.TrustBefore)
                    .ThenBy(entry => entry.Title, StringComparer.Ordinal)
                    .ToList()))
            .OrderByDescending(group => group.Count)
            .ThenBy(group => DomainRank(group.Domain))
            .ToList();

        var trading = grouped.Where(group => NormalizeGroupDomain(group.Domain) == "trading").Sum(group => group.Count);
        var documentation = grouped.Where(group => NormalizeGroupDomain(group.Domain) == "documentation").Sum(group => group.Count);
        var research = grouped.Where(group => NormalizeGroupDomain(group.Domain) == "research").Sum(group => group.Count);
        var software = grouped.Where(group => NormalizeGroupDomain(group.Domain) == "software").Sum(group => group.Count);
        var process = grouped.Where(group => NormalizeGroupDomain(group.Domain) == "process").Sum(group => group.Count);

        var report = new ReviewPrioritizationAuditReport(
            ReportVersion: "review_prioritization_audit_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            TotalPendingReviews: pendingReviews.Count,
            TradingReviews: trading,
            DocumentationReviews: documentation,
            ResearchReviews: research,
            SoftwareReviews: software,
            ProcessReviews: process,
            DomainGroups: grouped,
            TopPriorityReviews: entries.Take(10).ToList(),
            OperatorSummary: BuildOperatorSummary(trading, documentation, research, software, process, pendingReviews.Count),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            Warnings: queue.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteTextWithFallback(reportPath, markdownPath, root, report);
        return report;
    }

    public ReviewPrioritizationAuditReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ReviewPrioritizationAuditReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static ReviewPrioritizationEntry BuildEntry(HumanReviewItem item)
    {
        var priority = GetPriority(item);
        return new ReviewPrioritizationEntry(
            ReviewId: item.ReviewId,
            KnowledgeItemId: item.KnowledgeItemId,
            Title: item.Title,
            Domain: item.Domain,
            TrustBefore: item.TrustBefore,
            ReviewActionScore: ReviewDecisionAssistantService.BuildEntry(item).ReviewActionScore,
            RecommendationClass: ReviewDecisionAssistantService.BuildEntry(item).RecommendationClass,
            Recommendation: item.Recommendation,
            Reason: item.Reason,
            Priority: priority,
            PriorityReason: GetPriorityReason(item.Domain, item.Recommendation, item.Priority),
            MissingEvidence: ReviewDecisionAssistantService.BuildEntry(item).MissingEvidence,
            WhyNow: ReviewDecisionAssistantService.BuildEntry(item).WhyNow,
            NextStep: ReviewDecisionAssistantService.BuildEntry(item).NextStep);
    }

    private static string GetPriority(HumanReviewItem item) =>
        NormalizeGroupDomain(item.Domain) switch
        {
            "trading" => "hoch",
            "research" => "mittel",
            "software" => "mittel",
            "process" => "niedrig",
            "documentation" => "niedrig",
            _ => item.Priority switch
            {
                HumanReviewPriority.high => "hoch",
                HumanReviewPriority.medium => "mittel",
                _ => "niedrig"
            }
        };

    private static string GetPriorityReason(string domain, string recommendation, HumanReviewPriority queuePriority)
    {
        var normalizedDomain = NormalizeGroupDomain(domain);
        return normalizedDomain switch
        {
            "trading" => "Trading- oder Setupthema",
            "research" => "Research-/Wissensprüfung",
            "software" => "Technisches Thema mit mittlerer Priorität",
            "process" => "Prozesswissen",
            "documentation" => "Dokumentation",
            _ => queuePriority switch
            {
                HumanReviewPriority.high => "Hohe Queue-Priorität",
                HumanReviewPriority.medium => "Mittlere Queue-Priorität",
                _ => "Niedrige Queue-Priorität"
            }
        };
    }

    private static string NormalizeGroupDomain(string domain)
    {
        var normalized = domain.ToLowerInvariant();
        return normalized switch
        {
            "trading" => "trading",
            "documentation" => "documentation",
            "research" => "research",
            "software" => "software",
            "process" => "process",
            _ => normalized
        };
    }

    private static int PriorityRank(string priority) =>
        priority switch
        {
            "hoch" => 3,
            "mittel" => 2,
            _ => 1
        };

    private static int DomainRank(string domain) =>
        NormalizeGroupDomain(domain) switch
        {
            "trading" => 0,
            "research" => 1,
            "software" => 2,
            "process" => 3,
            "documentation" => 4,
            _ => 5
        };

    private static string BuildOperatorSummary(int trading, int documentation, int research, int software, int process, int total)
    {
        var important = trading;
        var knowledge = research + software;
        var docs = documentation + process;
        return $"🔴 {important} wichtige Entscheidungen\n🟡 {knowledge} Wissensprüfungen\n🟢 {docs} Dokumentationsprüfungen\n\nFrank muss nichts tun. Hermes bereitet die Reviews nur vor.";
    }

    private (string ReportPath, string MarkdownPath, string Root) ResolveOutputPaths()
    {
        try
        {
            Directory.CreateDirectory(Root);
            return (Path.Combine(Root, "review_prioritization_audit.json"), Path.Combine(Root, "review_prioritization_audit.md"), Root);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "review_prioritization_audit");
            Directory.CreateDirectory(fallbackRoot);
            return (Path.Combine(fallbackRoot, "review_prioritization_audit.json"), Path.Combine(fallbackRoot, "review_prioritization_audit.md"), fallbackRoot);
        }
    }

    private static void WriteTextWithFallback(string reportPath, string markdownPath, string root, ReviewPrioritizationAuditReport report)
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
            var fallbackRoot = Path.Combine(Directory.GetCurrentDirectory(), "HermesRuntime", ".codex_artifacts", "reports", "review_prioritization_audit");
            Directory.CreateDirectory(fallbackRoot);
            File.WriteAllText(Path.Combine(fallbackRoot, "review_prioritization_audit.json"), json);
            File.WriteAllText(Path.Combine(fallbackRoot, "review_prioritization_audit.md"), markdown);
        }
    }

    private static string BuildMarkdown(ReviewPrioritizationAuditReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Review Prioritization Audit");
        builder.AppendLine();
        builder.AppendLine($"- Updated UTC: {report.UpdatedAtUtc:O}");
        builder.AppendLine($"- Pending Reviews: {report.TotalPendingReviews}");
        builder.AppendLine($"- Trading: {report.TradingReviews}");
        builder.AppendLine($"- Documentation: {report.DocumentationReviews}");
        builder.AppendLine($"- Research: {report.ResearchReviews}");
        builder.AppendLine($"- Software: {report.SoftwareReviews}");
        builder.AppendLine($"- Process: {report.ProcessReviews}");
        builder.AppendLine();
        builder.AppendLine("## Operator Summary");
        builder.AppendLine(report.OperatorSummary);
        builder.AppendLine();
        builder.AppendLine("## Top Priority Reviews");
        foreach (var review in report.TopPriorityReviews)
        {
            builder.AppendLine($"- {review.Priority.ToUpperInvariant()} | {review.Domain} | {review.Title} | trust={review.TrustBefore:0.####} | {review.Recommendation}");
            builder.AppendLine($"  - Grund: {review.Reason}");
            builder.AppendLine($"  - Warum jetzt: {review.PriorityReason}");
        }

        builder.AppendLine();
        builder.AppendLine("## By Domain");
        foreach (var group in report.DomainGroups)
        {
            builder.AppendLine($"- {group.Domain}: {group.Count}");
        }

        return builder.ToString();
    }
}
