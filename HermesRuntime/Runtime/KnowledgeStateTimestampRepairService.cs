using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record KnowledgeStateTimestampRepairItem(
    string KnowledgeItemId,
    string Title,
    string Status,
    DateTimeOffset? TimestampBefore,
    DateTimeOffset? TimestampAfter,
    string TimestampSource,
    string Severity,
    string RecommendedAction,
    IReadOnlyList<string> Warnings);

public sealed record KnowledgeStateTimestampRepairReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedIssues,
    int SelectedIssues,
    int RepairedIssues,
    int SkippedIssues,
    IReadOnlyList<KnowledgeStateTimestampRepairItem> Items,
    IReadOnlyList<string> Warnings,
    string DiagnosticsPath,
    string CatalogPath,
    string QualityPath,
    string ValidationExecutionPath,
    string ReportPath,
    string MarkdownPath,
    bool ResearchOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool DryRun,
    bool Applied);

public sealed class KnowledgeStateTimestampRepairService
{
    private readonly StoragePaths _storagePaths;

    public KnowledgeStateTimestampRepairService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "knowledge_state_timestamp_repair");

    public string ReportPath => Path.Combine(Root, "knowledge_state_timestamp_repair_report.json");

    public string MarkdownPath => Path.Combine(Root, "knowledge_state_timestamp_repair_report.md");

    public string DiagnosticsPath => Path.Combine(_storagePaths.Root, "reports", "knowledge_state_repair_diagnostics", "knowledge_state_repair_diagnostics_report.json");

    public string CatalogPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_catalog.json");

    public string QualityPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_quality.json");

    public string ValidationExecutionPath => Path.Combine(_storagePaths.Root, "cognitive_core", "validation_execution.jsonl");

    public KnowledgeStateTimestampRepairReport Run(bool apply, bool dryRun)
    {
        Directory.CreateDirectory(Root);

        var updatedAt = DateTimeOffset.UtcNow;
        var diagnostics = LoadJson<KnowledgeStateRepairDiagnosticsReport>(DiagnosticsPath) ?? new KnowledgeStateRepairDiagnosticsService(_storagePaths).Run();
        var selectedIssues = diagnostics.Items
            .Where(item => item.MismatchType.Equals("timestamp_mismatch", StringComparison.OrdinalIgnoreCase) && item.AutoRepairable)
            .OrderByDescending(item => item.Severity, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var qualityEngine = new KnowledgeQualityEngine(_storagePaths);
        var qualityReport = qualityEngine.LoadReport();
        var catalog = new KnowledgeCatalog(_storagePaths).LoadItems().ToList();
        var catalogById = catalog.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var qualityById = qualityReport?.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, KnowledgeQualityItem>(StringComparer.OrdinalIgnoreCase);
        var executionById = new KnowledgeValidationExecutor(_storagePaths)
            .LoadResults(5000)
            .Where(result => !string.IsNullOrWhiteSpace(result.KnowledgeItemId))
            .GroupBy(result => result.KnowledgeItemId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(result => result.CompletedAtUtc).First(), StringComparer.OrdinalIgnoreCase);

        var repairItems = new List<KnowledgeStateTimestampRepairItem>();
        var modifiedQuality = qualityReport?.Items.ToList() ?? [];
        var qualityIndexById = modifiedQuality
            .Select((item, index) => new { item.KnowledgeId, index })
            .ToDictionary(entry => entry.KnowledgeId, entry => entry.index, StringComparer.OrdinalIgnoreCase);
        var catalogChanged = false;
        var qualityChanged = false;
        var repairedCount = 0;
        var skippedCount = 0;

        foreach (var issue in selectedIssues)
        {
            var timestampSource = DetermineTimestampSource(issue.KnowledgeItemId, qualityById, catalogById, executionById, out var beforeTimestamp, out var afterTimestamp, out var reason, out var warnings);
            var canRepair = afterTimestamp is not null && !Nullable.Equals(beforeTimestamp, afterTimestamp);
            var applied = apply && !dryRun && canRepair;

            if (applied)
            {
                if (qualityIndexById.TryGetValue(issue.KnowledgeItemId, out var qualityIndex))
                {
                    var beforeQuality = modifiedQuality[qualityIndex];
                    if (!Nullable.Equals(beforeQuality.LastValidatedUtc, afterTimestamp))
                    {
                        modifiedQuality[qualityIndex] = beforeQuality with { LastValidatedUtc = afterTimestamp };
                        qualityChanged = true;
                    }
                }

                if (catalogById.TryGetValue(issue.KnowledgeItemId, out var catalogItem))
                {
                    var catalogIndex = catalog.FindIndex(entry => entry.Id.Equals(issue.KnowledgeItemId, StringComparison.OrdinalIgnoreCase));
                    if (catalogIndex >= 0 && !Nullable.Equals(catalog[catalogIndex].LastValidatedUtc, afterTimestamp))
                    {
                        catalog[catalogIndex] = catalog[catalogIndex] with { LastValidatedUtc = afterTimestamp };
                        catalogChanged = true;
                    }
                }

                repairedCount++;
            }
            else
            {
                skippedCount++;
            }

            repairItems.Add(new KnowledgeStateTimestampRepairItem(
                KnowledgeItemId: issue.KnowledgeItemId,
                Title: issue.Title,
                Status: applied ? "repaired" : "skipped",
                TimestampBefore: beforeTimestamp,
                TimestampAfter: applied ? afterTimestamp : beforeTimestamp,
                TimestampSource: timestampSource,
                Severity: issue.Severity,
                RecommendedAction: issue.RecommendedAction,
                Warnings: warnings));
        }

        if (catalogChanged)
        {
            File.WriteAllText(CatalogPath, JsonSerializer.Serialize(catalog, JsonDefaults.WriteOptions));
        }

        if (qualityChanged && qualityReport is not null)
        {
            var updatedQuality = qualityReport with { Items = modifiedQuality };
            File.WriteAllText(QualityPath, JsonSerializer.Serialize(updatedQuality, JsonDefaults.WriteOptions));
            qualityReport = updatedQuality;
        }

        var report = new KnowledgeStateTimestampRepairReport(
            ReportVersion: "knowledge_state_timestamp_repair_v1",
            UpdatedAtUtc: updatedAt,
            Status: apply && !dryRun ? "applied" : "dry_run_ready",
            LoadedIssues: diagnostics.TotalIssues,
            SelectedIssues: selectedIssues.Count,
            RepairedIssues: repairedCount,
            SkippedIssues: skippedCount,
            Items: repairItems,
            Warnings: BuildWarnings(repairItems),
            DiagnosticsPath: DiagnosticsPath,
            CatalogPath: CatalogPath,
            QualityPath: QualityPath,
            ValidationExecutionPath: ValidationExecutionPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            ResearchOnly: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            DryRun: dryRun || !apply,
            Applied: apply && !dryRun);

        WriteReport(report);
        _ = new KnowledgeStateConsistencyService(_storagePaths, Directory.GetCurrentDirectory()).Run(apply: false, dryRun: true);
        return report;
    }

    public KnowledgeStateTimestampRepairReport? LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeStateTimestampRepairReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static string DetermineTimestampSource(
        string knowledgeItemId,
        IReadOnlyDictionary<string, KnowledgeQualityItem> qualityById,
        IReadOnlyDictionary<string, KnowledgeCatalogItem> catalogById,
        IReadOnlyDictionary<string, KnowledgeValidationExecutionResult> executionById,
        out DateTimeOffset? beforeTimestamp,
        out DateTimeOffset? afterTimestamp,
        out string reason,
        out IReadOnlyList<string> warnings)
    {
        warnings = [];
        var quality = qualityById.GetValueOrDefault(knowledgeItemId);
        var catalog = catalogById.GetValueOrDefault(knowledgeItemId);
        var execution = executionById.GetValueOrDefault(knowledgeItemId);

        beforeTimestamp = quality?.LastValidatedUtc ?? catalog?.LastValidatedUtc;
        afterTimestamp = execution?.CompletedAtUtc ?? beforeTimestamp;

        if (execution is not null)
        {
            reason = "validation_execution_completed";
            return execution.CompletedAtUtc.ToString("O");
        }

        if (beforeTimestamp is not null)
        {
            reason = "existing_timestamp_preserved";
            warnings = ["no_validation_execution_found"];
            return beforeTimestamp.Value.ToString("O");
        }

        reason = "no_safe_timestamp_source";
        warnings = ["missing_validation_execution", "missing_existing_timestamp"];
        afterTimestamp = null;
        return "-";
    }

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<KnowledgeStateTimestampRepairItem> items)
    {
        var warnings = new List<string>();
        if (items.Count == 0)
        {
            warnings.Add("no_timestamp_mismatches_selected");
        }

        if (items.Any(item => item.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase)))
        {
            warnings.Add("some_timestamp_repairs_were_skipped");
        }

        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void WriteReport(KnowledgeStateTimestampRepairReport report)
    {
        File.WriteAllText(report.ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(report.MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(KnowledgeStateTimestampRepairReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Knowledge State Timestamp Repair");
        sb.AppendLine();
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- loaded_issues: {report.LoadedIssues}");
        sb.AppendLine($"- selected_issues: {report.SelectedIssues}");
        sb.AppendLine($"- repaired_issues: {report.RepairedIssues}");
        sb.AppendLine($"- skipped_issues: {report.SkippedIssues}");
        sb.AppendLine();
        foreach (var item in report.Items)
        {
            sb.AppendLine($"### {item.KnowledgeItemId} / {item.Title}");
            sb.AppendLine($"- status: {item.Status}");
            sb.AppendLine($"- timestamp_before: {item.TimestampBefore?.ToString("O") ?? "-"}");
            sb.AppendLine($"- timestamp_after: {item.TimestampAfter?.ToString("O") ?? "-"}");
            sb.AppendLine($"- timestamp_source: {item.TimestampSource}");
            sb.AppendLine($"- severity: {item.Severity}");
            sb.AppendLine($"- recommended_action: {item.RecommendedAction}");
            sb.AppendLine($"- warnings: {string.Join(", ", item.Warnings)}");
            sb.AppendLine();
        }

        if (report.Warnings.Count > 0)
        {
            sb.AppendLine("## Warnings");
            foreach (var warning in report.Warnings)
            {
                sb.AppendLine($"- {warning}");
            }
        }

        return sb.ToString();
    }

    private static T? LoadJson<T>(string path)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return default;
        }
    }
}
