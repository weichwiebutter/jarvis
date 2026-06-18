using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record MutationCandidateQueueItem(
    string MutationId,
    string ParentHypothesis,
    string StrategyPattern,
    string Asset,
    string Timeframe,
    string MutationType,
    string Priority,
    string Reason,
    string ExpectedBenefit,
    string EstimatedRisk,
    string Status);

public sealed record MutationCandidateQueueReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int QueueSize,
    int HighPriorityCount,
    int MediumPriorityCount,
    int LowPriorityCount,
    IReadOnlyList<MutationCandidateQueueItem> QueueItems,
    IReadOnlyList<string> SourceReports,
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

public sealed class MutationCandidateQueueService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public MutationCandidateQueueService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "mutation_candidate_queue");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "mutation_candidate_queue.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "mutation_candidate_queue.md");

    public MutationCandidateQueueReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<MutationCandidateQueueReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public MutationCandidateQueueReport Run()
    {
        Directory.CreateDirectory(Root);

        var planner = new FailureGuidedMutationPlannerService(_storagePaths, AppContext.BaseDirectory).Load()
            ?? new FailureGuidedMutationPlannerService(_storagePaths, AppContext.BaseDirectory).Run();

        var queueItems = planner.MutationCandidates
            .OrderBy(candidate => PriorityRank(candidate.Priority))
            .ThenBy(candidate => candidate.Title, StringComparer.OrdinalIgnoreCase)
            .Select(candidate => new MutationCandidateQueueItem(
                MutationId: candidate.MutationId,
                ParentHypothesis: planner.SourceBacktestJobId,
                StrategyPattern: planner.StrategyPattern,
                Asset: planner.Asset,
                Timeframe: planner.Timeframe,
                MutationType: candidate.MutationType,
                Priority: candidate.Priority,
                Reason: candidate.WhySuggested,
                ExpectedBenefit: candidate.ExpectedBenefit,
                EstimatedRisk: candidate.RiskLevel,
                Status: "planned"))
            .ToList();

        var report = new MutationCandidateQueueReport(
            ReportVersion: "mutation_candidate_queue_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            QueueSize: queueItems.Count,
            HighPriorityCount: queueItems.Count(item => item.Priority.Equals("high", StringComparison.OrdinalIgnoreCase)),
            MediumPriorityCount: queueItems.Count(item => item.Priority.Equals("medium", StringComparison.OrdinalIgnoreCase)),
            LowPriorityCount: queueItems.Count(item => item.Priority.Equals("low", StringComparison.OrdinalIgnoreCase)),
            QueueItems: queueItems,
            SourceReports: planner.SourceReports.Concat([planner.ReportPath]).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings: planner.Warnings.Concat(planner.MutationCandidates.Count == 0 ? ["no_mutation_candidates_found"] : []).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            OperatorSummary: $"Hermes hat {queueItems.Count} neue Forschungskandidaten vorbereitet. {queueItems.Count(item => item.Priority.Equals("high", StringComparison.OrdinalIgnoreCase))} Kandidaten besitzen hohe Priorität. Frank muss nichts entscheiden.",
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true",
            FrankRequired: false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteArtifacts(report);
        return report;
    }

    private static int PriorityRank(string priority)
        => priority.Equals("high", StringComparison.OrdinalIgnoreCase) ? 0
            : priority.Equals("medium", StringComparison.OrdinalIgnoreCase) ? 1
            : 2;

    private void WriteArtifacts(MutationCandidateQueueReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, markdown);
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(MutationCandidateQueueReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Mutation Candidate Queue");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Queue size: {report.QueueSize}");
        sb.AppendLine($"- High priority: {report.HighPriorityCount}");
        sb.AppendLine($"- Medium priority: {report.MediumPriorityCount}");
        sb.AppendLine($"- Low priority: {report.LowPriorityCount}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Queue Items");
        foreach (var item in report.QueueItems)
        {
            sb.AppendLine($"- {item.MutationId} [{item.Priority}] {item.MutationType}");
            sb.AppendLine($"  - Parent hypothesis: {item.ParentHypothesis}");
            sb.AppendLine($"  - Reason: {item.Reason}");
            sb.AppendLine($"  - Benefit: {item.ExpectedBenefit}");
            sb.AppendLine($"  - Risk: {item.EstimatedRisk}");
        }
        return sb.ToString();
    }
}
