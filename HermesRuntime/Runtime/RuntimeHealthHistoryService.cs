using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record RuntimeHealthHistoryEntry(
    DateTimeOffset TimestampUtc,
    string MainStatus,
    string LastStep,
    string NextStep,
    string LastResult,
    bool FrankRequired,
    int OpenReviews,
    int OpenOosPlans,
    int OpenForwardPlans,
    string SafetyStatus);

public sealed class RuntimeHealthHistoryService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;

    public RuntimeHealthHistoryService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "runtime_health_history");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "runtime_health_history.jsonl");

    public RuntimeHealthHistoryEntry AppendFromSummary()
    {
        Directory.CreateDirectory(Root);

        var summary = new RuntimeHealthSummaryService(_storagePaths, _runtimeRoot).Run();
        var entry = new RuntimeHealthHistoryEntry(
            TimestampUtc: summary.UpdatedAtUtc,
            MainStatus: summary.MainStatus,
            LastStep: summary.LastStep,
            NextStep: summary.NextStep,
            LastResult: summary.LastResult,
            FrankRequired: summary.FrankRequired,
            OpenReviews: summary.OpenReviews,
            OpenOosPlans: summary.OpenOosPlans,
            OpenForwardPlans: summary.OpenForwardPlans,
            SafetyStatus: summary.SafetyStatus);

        File.AppendAllText(ReportPath, JsonSerializer.Serialize(entry, JsonDefaults.WriteOptions) + Environment.NewLine);
        _resolvedReportPath = ReportPath;
        return entry;
    }

    public IReadOnlyList<RuntimeHealthHistoryEntry> LoadEntries()
    {
        if (!File.Exists(ReportPath))
        {
            return [];
        }

        var entries = new List<RuntimeHealthHistoryEntry>();
        foreach (var line in File.ReadLines(ReportPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                var entry = JsonSerializer.Deserialize<RuntimeHealthHistoryEntry>(line, JsonDefaults.SnapshotReadOptions);
                if (entry is not null)
                {
                    entries.Add(entry);
                }
            }
            catch (JsonException)
            {
            }
        }

        _resolvedReportPath = ReportPath;
        return entries;
    }
}
