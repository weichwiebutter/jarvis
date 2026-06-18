using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record AutonomousForwardObservationSyncItem(
    string ForwardValidationJobId,
    string SourceOosJobId,
    string HypothesisId,
    string ObservationStatus,
    string SyncedStatus,
    string NextStep,
    bool IsOpen);

public sealed record AutonomousForwardObservationSyncReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    int PlansRead,
    int OpenPlans,
    int CompletedPlans,
    int BlockedPlans,
    IReadOnlyList<AutonomousForwardObservationSyncItem> Items,
    IReadOnlyList<string> SourceReports,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string NextSafeStep,
    string SafetySummary,
    bool FrankRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class AutonomousForwardObservationCompletionSyncService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public AutonomousForwardObservationCompletionSyncService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "autonomous_forward_observation_sync");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "autonomous_forward_observation_sync.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "autonomous_forward_observation_sync.md");

    public AutonomousForwardObservationSyncReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AutonomousForwardObservationSyncReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public AutonomousForwardObservationSyncReport Run()
    {
        Directory.CreateDirectory(Root);

        var planning = new AutonomousForwardValidationPlanningService(_storagePaths, _runtimeRoot).Load()
            ?? new AutonomousForwardValidationPlanningService(_storagePaths, _runtimeRoot).Run();
        var observationGate = new AutonomousForwardObservationGateService(_storagePaths, _runtimeRoot).Load()
            ?? new AutonomousForwardObservationGateService(_storagePaths, _runtimeRoot).Run();

        var observationStatus = observationGate.GateStatus;
        var items = planning.Plans.Select(plan =>
        {
            var synced = MapStatus(observationStatus, plan.Status);
            return new AutonomousForwardObservationSyncItem(
                ForwardValidationJobId: plan.ForwardValidationJobId,
                SourceOosJobId: plan.SourceOosJobId,
                HypothesisId: plan.HypothesisId,
                ObservationStatus: observationStatus,
                SyncedStatus: synced,
                NextStep: BuildNextStep(synced),
                IsOpen: IsOpenStatus(synced));
        }).ToList();

        var report = new AutonomousForwardObservationSyncReport(
            ReportVersion: "autonomous_forward_observation_sync_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            PlansRead: items.Count,
            OpenPlans: items.Count(item => item.IsOpen),
            CompletedPlans: items.Count(item => item.SyncedStatus.StartsWith("completed", StringComparison.OrdinalIgnoreCase)),
            BlockedPlans: items.Count(item => item.SyncedStatus.Equals("blocked", StringComparison.OrdinalIgnoreCase)),
            Items: items,
            SourceReports: BuildSourceReports(),
            Warnings: observationStatus is "no_signal" or "observation_pending" ? [] : [],
            OperatorSummary: BuildOperatorSummary(items, observationStatus),
            NextSafeStep: BuildNextSafeStep(items, observationStatus),
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true, observation_only=true",
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

    private static string MapStatus(string observationStatus, string currentStatus)
    {
        return observationStatus switch
        {
            "completed" => "completed",
            "invalidated" => "completed_invalidated",
            "blocked" => "blocked",
            "waiting_for_allowed_window" => "still_open_waiting_for_window",
            "waiting_for_market_data" => "still_open_waiting_for_market_data",
            "signal_seen" => "active_signal_seen",
            "observation_pending" => "still_open_observation_pending",
            "no_signal" => "still_open_waiting_for_signal",
            _ => currentStatus.StartsWith("completed_", StringComparison.OrdinalIgnoreCase)
                ? currentStatus
                : "still_open_waiting_for_signal"
        };
    }

    private static bool IsOpenStatus(string status) =>
        status.StartsWith("still_open_", StringComparison.OrdinalIgnoreCase)
        || status.Equals("active_signal_seen", StringComparison.OrdinalIgnoreCase)
        || status.Equals("ready_to_observe", StringComparison.OrdinalIgnoreCase);

    private static string BuildNextStep(string syncedStatus)
        => syncedStatus switch
        {
            "still_open_waiting_for_signal" => "Noch kein passendes Signal gesehen. Hermes beobachtet später weiter.",
            "still_open_observation_pending" => "Noch keine vollständige Beobachtung. Hermes beobachtet weiter.",
            "active_signal_seen" => "Signal erkannt. Hermes verfolgt den Verlauf weiter.",
            "completed" => "Forward-Beobachtung abgeschlossen. Ergebnis auswerten.",
            "completed_invalidated" => "Forward-Plan wurde invalidiert. Hypothese zurückstufen.",
            "blocked" => "Forward-Plan ist blockiert.",
            "still_open_waiting_for_market_data" => "Warten auf read-only Market Snapshot.",
            "still_open_waiting_for_window" => "Warten auf erlaubtes Zeitfenster.",
            _ => "Forward-Plan offen."
        };

    private static string BuildOperatorSummary(IReadOnlyList<AutonomousForwardObservationSyncItem> items, string observationStatus)
    {
        var openCount = items.Count(item => item.IsOpen);
        return $"Hermes hat Forward-Observation synchronisiert. Status={observationStatus}. Offene Forward-Pläne={openCount}. Frank muss nichts tun.";
    }

    private static string BuildNextSafeStep(IReadOnlyList<AutonomousForwardObservationSyncItem> items, string observationStatus)
    {
        var selected = items.FirstOrDefault();
        return selected is null ? "Forward-Observation später erneut prüfen." : selected.NextStep;
    }

    private static IReadOnlyList<string> BuildSourceReports() =>
    [
        "/mnt/d/HermesData/reports/autonomous_forward_validation_planning/autonomous_forward_validation_planning.json",
        "/mnt/d/HermesData/reports/autonomous_forward_observation_gate/autonomous_forward_observation_gate.json",
        "/mnt/d/HermesData/reports/autonomous_forward_observation_gate/history/autonomous_forward_observation_history.jsonl"
    ];

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

    private void WriteArtifacts(AutonomousForwardObservationSyncReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(AutonomousForwardObservationSyncReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Autonomous Forward Observation Sync");
        sb.AppendLine();
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("| Job | Synced Status | Open |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var item in report.Items)
        {
            sb.AppendLine($"| {item.ForwardValidationJobId} | {item.SyncedStatus} | {item.IsOpen.ToString().ToLowerInvariant()} |");
        }

        return sb.ToString();
    }
}
