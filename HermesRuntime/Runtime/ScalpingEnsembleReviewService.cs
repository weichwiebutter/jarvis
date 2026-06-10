using System.Text.Json;

namespace Hermes.Runtime;

public enum ScalpingEnsembleReviewStatus
{
    pending_human_review,
    approved_for_demo_signal_use,
    approved_for_forward_test_preparation,
    rejected,
    needs_more_evidence,
    deferred
}

public sealed record ScalpingEnsembleReviewState(
    string ReviewId,
    DateTimeOffset UpdatedAtUtc,
    string PackageId,
    string PackageStatus,
    ScalpingEnsembleReviewStatus ReviewStatus,
    string? ReviewMode,
    string? Reason,
    IReadOnlyList<string> Members,
    IReadOnlyList<string> Blockers,
    string ManifestPath,
    string HumanReviewPackagePath,
    string ReviewLogPath,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record ScalpingEnsembleReviewLogEntry(
    DateTimeOffset TimestampUtc,
    string Action,
    string PackageId,
    string PackageStatus,
    ScalpingEnsembleReviewStatus ReviewStatus,
    string? ReviewMode,
    string? Reason,
    IReadOnlyList<string> Blockers,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed class ScalpingEnsembleReviewService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ScalpingEnsembleReviewService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "scalping_portfolio", "ensemble_review");
    public string StatusPath => Path.Combine(Root, "ensemble_review_status.json");
    public string StatusMarkdownPath => Path.Combine(Root, "ensemble_review_status.md");
    public string LogPath => Path.Combine(Root, "ensemble_review_log.jsonl");

    public ScalpingEnsembleReviewState LoadOrCreate()
    {
        if (File.Exists(StatusPath))
        {
            var state = JsonSerializer.Deserialize<ScalpingEnsembleReviewState>(File.ReadAllText(StatusPath), JsonDefaults.SnapshotReadOptions);
            if (state is not null) return state;
        }

        return Save(
            action: "initialize_review",
            reviewStatus: ScalpingEnsembleReviewStatus.pending_human_review,
            reviewMode: null,
            reason: null);
    }

    public ScalpingEnsembleReviewState? LoadState()
    {
        if (!File.Exists(StatusPath))
        {
            return null;
        }

        var state = JsonSerializer.Deserialize<ScalpingEnsembleReviewState>(File.ReadAllText(StatusPath), JsonDefaults.SnapshotReadOptions);
        return state is null || string.IsNullOrWhiteSpace(state.PackageId) ? null : state;
    }

    public ScalpingEnsembleReviewState Approve(string mode)
    {
        var normalized = mode.Trim().ToLowerInvariant();
        return normalized switch
        {
            "demo_signal_use" => Save("approve", ScalpingEnsembleReviewStatus.approved_for_demo_signal_use, "demo_signal_use", null),
            "forward_test_preparation" => Save("approve", ScalpingEnsembleReviewStatus.approved_for_forward_test_preparation, "forward_test_preparation", null),
            _ => throw new InvalidOperationException($"invalid_approval_mode:{mode}")
        };
    }

    public ScalpingEnsembleReviewState Reject(string reason)
    {
        EnsureReason(reason, "reject");
        return Save("reject", ScalpingEnsembleReviewStatus.rejected, null, reason);
    }

    public ScalpingEnsembleReviewState Defer(string reason)
    {
        EnsureReason(reason, "defer");
        return Save("defer", ScalpingEnsembleReviewStatus.deferred, null, reason);
    }

    public ScalpingEnsembleReviewState RequestMoreEvidence(string reason)
    {
        EnsureReason(reason, "request_more_evidence");
        return Save("request_more_evidence", ScalpingEnsembleReviewStatus.needs_more_evidence, null, reason);
    }

    private ScalpingEnsembleReviewState Save(string action, ScalpingEnsembleReviewStatus reviewStatus, string? reviewMode, string? reason)
    {
        var exportService = new ScalpingEnsembleExportService(_storagePaths, _runtimeRoot);
        var manifest = exportService.LoadManifest();
        var blockers = ValidateApprovalGates(manifest, exportService);
        var approvalAttempt = action == "approve";
        if (approvalAttempt && blockers.Count > 0)
        {
            throw new InvalidOperationException($"ensemble_review_blocked:{string.Join(",", blockers)}");
        }

        var packageId = manifest?.PackageId ?? "missing_package";
        var packageStatus = manifest?.Status ?? "missing";
        var state = new ScalpingEnsembleReviewState(
            ReviewId: $"ensemble_review_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            PackageId: packageId,
            PackageStatus: packageStatus,
            ReviewStatus: reviewStatus,
            ReviewMode: reviewMode,
            Reason: reason,
            Members: manifest?.Members ?? [],
            Blockers: blockers,
            ManifestPath: exportService.ManifestPath,
            HumanReviewPackagePath: exportService.HumanReviewPackagePath,
            ReviewLogPath: LogPath,
            NoAutoTrading: manifest?.NoAutoTrading ?? false,
            HumanReviewRequired: manifest?.HumanReviewRequired ?? true,
            BrokerOrdersEnabled: manifest?.BrokerOrdersEnabled ?? false,
            LiveTradingEnabled: manifest?.LiveTradingEnabled ?? false);

        Directory.CreateDirectory(Root);
        File.WriteAllText(StatusPath, JsonSerializer.Serialize(state, JsonDefaults.WriteOptions));
        File.WriteAllText(StatusMarkdownPath, BuildMarkdown(state));
        AppendLog(state, action);
        return state;
    }

    private static void EnsureReason(string reason, string action)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException($"reason_required_for:{action}");
        }
    }

    private List<string> ValidateApprovalGates(ScalpingEnsemblePackageManifest? manifest, ScalpingEnsembleExportService exportService)
    {
        var blockers = new List<string>();
        if (manifest is null) blockers.Add("ensemble_package_missing");
        if (manifest is not null && !manifest.Status.Equals("ensemble_ready", StringComparison.OrdinalIgnoreCase)) blockers.Add($"ensemble_not_ready:{manifest.Status}");
        if (!File.Exists(exportService.HumanReviewPackagePath)) blockers.Add("ensemble_human_review_package_missing");
        if (manifest is not null && !manifest.NoAutoTrading) blockers.Add("no_auto_trading_not_confirmed");
        if (manifest is not null && !manifest.HumanReviewRequired) blockers.Add("human_review_required_not_confirmed");
        if (manifest is not null && manifest.BrokerOrdersEnabled) blockers.Add("broker_orders_enabled_not_allowed");
        if (manifest is not null && manifest.LiveTradingEnabled) blockers.Add("live_trading_enabled_not_allowed");
        return blockers;
    }

    private void AppendLog(ScalpingEnsembleReviewState state, string action)
    {
        var entry = new ScalpingEnsembleReviewLogEntry(
            TimestampUtc: DateTimeOffset.UtcNow,
            Action: action,
            PackageId: state.PackageId,
            PackageStatus: state.PackageStatus,
            ReviewStatus: state.ReviewStatus,
            ReviewMode: state.ReviewMode,
            Reason: state.Reason,
            Blockers: state.Blockers,
            NoAutoTrading: state.NoAutoTrading,
            HumanReviewRequired: state.HumanReviewRequired,
            BrokerOrdersEnabled: state.BrokerOrdersEnabled,
            LiveTradingEnabled: state.LiveTradingEnabled);
        File.AppendAllText(LogPath, JsonSerializer.Serialize(entry, JsonDefaults.WriteOptions) + Environment.NewLine);
    }

    private static string BuildMarkdown(ScalpingEnsembleReviewState state) => $"""
# Scalping Ensemble Review Status

- review_id: {state.ReviewId}
- package_id: {state.PackageId}
- package_status: {state.PackageStatus}
- review_status: {state.ReviewStatus}
- review_mode: {state.ReviewMode ?? "-"}
- reason: {state.Reason ?? "-"}
- no_auto_trading: true
- human_review_required: true
- broker_orders_enabled: false
- live_trading_enabled: false

## Members
{string.Join(Environment.NewLine, state.Members.Select(member => $"- {member}"))}

## Blockers
{Bullets(state.Blockers)}

## Approval Scope
Approval erlaubt:
- Demo-Signal-Nutzung
- Forward-Test-Vorbereitung
- weitere Review-Schritte

Approval erlaubt NICHT:
- Live-Trading
- Broker-Orders
- cTrader Order API
- automatische Ausführung
""";

    private static string Bullets(IEnumerable<string> items) => string.Join(Environment.NewLine, items.Any() ? items.Select(item => $"- {item}") : ["- none"]);
}
