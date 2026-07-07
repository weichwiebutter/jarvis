using System.Text;
using System.Text.Json;
using HermesPaperBot.Models;
using HermesPaperBot.Services;

namespace Hermes.Runtime;

public sealed record PaperSignalEvaluationItem(
    string SignalId,
    string Asset,
    string Timeframe,
    string SetupId,
    string SetupName,
    string Direction,
    string SignalStatus,
    string SignalLifecycleStatus,
    string PaperDecision,
    string Reason,
    bool SessionAllowed,
    bool SpreadAllowed,
    bool SafetyAllowed,
    bool MarketContextCompatible,
    bool SignalExpired,
    bool SignalInvalidated,
    bool PaperEntryEnabled,
    decimal ConfidenceBaseline,
    decimal MaxSpreadPips,
    decimal? SpreadPips,
    IReadOnlyList<string> Warnings);

public sealed record PaperSignalEvaluationReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int EvaluatedSignals,
    int ActionableSignals,
    int SkippedSignals,
    int WaitingSignals,
    int WatchingSignals,
    int WouldTriggerSignals,
    int ActiveSignals,
    int CompletedSignals,
    int InvalidatedSignals,
    int ExpiredSignals,
    string PaperDecisionSummary,
    IReadOnlyList<PaperSignalEvaluationItem> Signals,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Recommendations,
    string ReportPath,
    string MarkdownPath);

public sealed class PaperSignalEvaluationService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private readonly PaperDecisionEngine _paperDecisionEngine = new();

    public PaperSignalEvaluationService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "paper_signal_evaluation");
    public string ReportPath => Path.Combine(Root, "paper_signal_evaluation.json");
    public string MarkdownPath => Path.Combine(Root, "paper_signal_evaluation.md");

    public PaperSignalEvaluationReport LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return Run(null, null);
        }

        try
        {
            var report = JsonSerializer.Deserialize<PaperSignalEvaluationReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
            return report ?? Run(null, null);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Run(null, null);
        }
    }

    public PaperSignalEvaluationReport Run(BotConfiguration? config, RuntimeMarketContext? marketContext)
    {
        Directory.CreateDirectory(Root);

        var warnings = new List<string>();
        var recommendations = new List<string>();
        var configWarnings = new List<string>();

        if (config is null)
        {
            var bootstrapper = new CloudEmbeddedPackageBootstrapper();
            var bootstrap = bootstrapper.CreateCloudConfiguration();
            if (!bootstrap.Success || bootstrap.Configuration is null)
            {
                warnings.Add(bootstrap.Reason ?? "cloud_bootstrap_failed");
                var emptyReport = BuildReport(Array.Empty<PaperSignalEvaluationItem>(), warnings, recommendations, false, false, 0, 0, 0, 0, 0, 0, 0, 0, 0);
                WriteReport(emptyReport);
                return emptyReport;
            }

            config = bootstrap.Configuration;
        }

        var embeddedPackage = config.CloudEmbeddedReleasePackage;
        var candidates = _paperDecisionEngine.ParseSignalCandidates(embeddedPackage, out var parseWarnings);
        warnings.AddRange(parseWarnings);

        var context = marketContext ?? new RuntimeMarketContext { Source = "unavailable" };
        var safetyAllowed = EvaluateSafety(config, embeddedPackage, configWarnings);
        warnings.AddRange(configWarnings);

        var evaluationItems = new List<PaperSignalEvaluationItem>();
        var actionableSignals = 0;
        var skippedSignals = 0;
        var waitingSignals = 0;
        var watchingSignals = 0;
        var wouldTriggerSignals = 0;
        var activeSignals = 0;
        var completedSignals = 0;
        var invalidatedSignals = 0;
        var expiredSignals = 0;

        foreach (var candidate in candidates)
        {
            var sessionResult = new SessionFilter().Evaluate(context, candidate.SessionTags);
            var spreadResult = new SpreadFilter().Evaluate(context, candidate.MaxSpread);
            var compatible = IsContextCompatible(candidate, context);
            var expired = candidate.ExpiresAtUtc.HasValue && candidate.ExpiresAtUtc.Value <= DateTimeOffset.UtcNow;
            var invalidated = !candidate.PaperEntryEnabled || candidate.ValidationWarnings.Any(warning => warning.Contains("paper_entry_disabled", StringComparison.OrdinalIgnoreCase));

            string signalStatus;
            string paperDecision;
            string reason;
            string lifecycleStatus;

            if (invalidated)
            {
                signalStatus = "invalidated";
                paperDecision = "would_wait";
                reason = "paper_entry_disabled";
                lifecycleStatus = "invalidated";
                invalidatedSignals += 1;
            }
            else if (expired)
            {
                signalStatus = "expired";
                paperDecision = "would_wait";
                reason = "signal_expired";
                lifecycleStatus = "expired";
                expiredSignals += 1;
            }
            else if (!sessionResult.Allowed)
            {
                signalStatus = "skipped_session";
                paperDecision = "would_wait";
                reason = sessionResult.Reason;
                lifecycleStatus = "waiting";
                skippedSignals += 1;
            }
            else if (!spreadResult.Allowed)
            {
                signalStatus = "skipped_spread";
                paperDecision = "would_wait";
                reason = spreadResult.Reason;
                lifecycleStatus = "waiting";
                skippedSignals += 1;
            }
            else if (!compatible)
            {
                signalStatus = "waiting";
                paperDecision = "would_wait";
                reason = "market_context_incompatible";
                lifecycleStatus = "waiting";
            }
            else if (!safetyAllowed)
            {
                signalStatus = "invalidated";
                paperDecision = "would_wait";
                reason = "safety_gate_blocked";
                lifecycleStatus = "invalidated";
                invalidatedSignals += 1;
            }
            else
            {
                signalStatus = "active";
                paperDecision = "would_trigger";
                reason = "signal_actionable";
                actionableSignals += 1;
                lifecycleStatus = candidate.Readiness.Equals("bot_ready", StringComparison.OrdinalIgnoreCase)
                    ? "would_trigger"
                    : "watching";
                if (lifecycleStatus == "would_trigger")
                {
                    wouldTriggerSignals += 1;
                }
                else
                {
                    watchingSignals += 1;
                }
            }

            if (lifecycleStatus == "waiting")
            {
                waitingSignals += 1;
            }

            if (signalStatus is "invalidated" or "expired")
            {
                skippedSignals += 1;
            }

            if (lifecycleStatus == "active")
            {
                activeSignals += 1;
            }
            else if (lifecycleStatus == "completed")
            {
                completedSignals += 1;
            }
            else if (lifecycleStatus == "watching" && signalStatus is not "waiting")
            {
                watchingSignals += 1;
            }

            evaluationItems.Add(new PaperSignalEvaluationItem(
                SignalId: candidate.SignalId,
                Asset: candidate.Asset,
                Timeframe: candidate.Timeframe,
                SetupId: candidate.SetupId,
                SetupName: candidate.SetupName,
                Direction: candidate.Direction,
                SignalStatus: signalStatus,
                SignalLifecycleStatus: lifecycleStatus,
                PaperDecision: paperDecision,
                Reason: reason,
                SessionAllowed: sessionResult.Allowed,
                SpreadAllowed: spreadResult.Allowed,
                SafetyAllowed: safetyAllowed,
                MarketContextCompatible: compatible,
                SignalExpired: expired,
                SignalInvalidated: signalStatus == "invalidated",
                PaperEntryEnabled: candidate.PaperEntryEnabled,
                ConfidenceBaseline: candidate.ConfidenceBaseline,
                MaxSpreadPips: candidate.MaxSpread,
                SpreadPips: context.SpreadPips,
                Warnings: BuildWarnings(candidate, sessionResult, spreadResult, reason)));
        }

        if (evaluationItems.Count == 0)
        {
            warnings.Add("no_signal_candidates");
        }

        var report = BuildReport(
            evaluationItems,
            warnings,
            recommendations,
            safetyAllowed,
            actionableSignals > 0,
            skippedSignals,
            waitingSignals,
            watchingSignals,
            wouldTriggerSignals,
            activeSignals,
            completedSignals,
            invalidatedSignals,
            expiredSignals,
            actionableSignals);
        WriteReport(report);
        return report;
    }

    private PaperSignalEvaluationReport BuildReport(
        IReadOnlyList<PaperSignalEvaluationItem> signals,
        IReadOnlyList<string> warnings,
        IReadOnlyList<string> recommendations,
        bool safetyAllowed,
        bool hasActionableSignals,
        int skippedSignals,
        int waitingSignals,
        int watchingSignals,
        int wouldTriggerSignals,
        int activeSignals,
        int completedSignals,
        int invalidatedSignals,
        int expiredSignals,
        int actionableSignals)
    {
        var paperDecisionSummary = signals.Count == 0
            ? "no_signal_candidates"
            : $"evaluated={signals.Count}; actionable={actionableSignals}; waiting={waitingSignals}; watching={watchingSignals}; would_trigger={wouldTriggerSignals}; active={activeSignals}; completed={completedSignals}; invalidated={invalidatedSignals}; expired={expiredSignals}; skipped={skippedSignals}";

        return new PaperSignalEvaluationReport(
            ReportVersion: "paper_signal_evaluation_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: safetyAllowed ? (hasActionableSignals ? "ready" : "waiting") : "blocked",
            EvaluatedSignals: signals.Count,
            ActionableSignals: actionableSignals,
            SkippedSignals: skippedSignals,
            WaitingSignals: waitingSignals,
            WatchingSignals: watchingSignals,
            WouldTriggerSignals: wouldTriggerSignals,
            ActiveSignals: activeSignals,
            CompletedSignals: completedSignals,
            InvalidatedSignals: invalidatedSignals,
            ExpiredSignals: expiredSignals,
            PaperDecisionSummary: paperDecisionSummary,
            Signals: signals,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Recommendations: recommendations.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);
    }

    private static IReadOnlyList<string> BuildWarnings(SignalCandidate candidate, FilterResult sessionResult, FilterResult spreadResult, string reason)
    {
        var warnings = new List<string>();
        warnings.AddRange(candidate.ValidationWarnings);
        if (!sessionResult.Allowed) warnings.Add(sessionResult.Status);
        if (!spreadResult.Allowed) warnings.Add(spreadResult.Status);
        warnings.Add(reason);
        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool EvaluateSafety(BotConfiguration config, CloudEmbeddedReleasePackage? package, List<string> warnings)
    {
        if (package is null)
        {
            warnings.Add("embedded_package_missing");
            return false;
        }

        var safetyFlags = package.SafetyFlags;
        var ok = safetyFlags.NoAutoTrading
            && safetyFlags.HumanReviewRequired
            && !safetyFlags.BrokerTradingEnabled
            && !safetyFlags.LiveTradingEnabled
            && !safetyFlags.OrderApiEnabled
            && safetyFlags.PaperMode
            && string.Equals(safetyFlags.BrokerAction, "none", StringComparison.OrdinalIgnoreCase)
            && config.RuntimeMode == RuntimeMode.CloudEmbeddedBundle;

        if (!ok)
        {
            warnings.Add("safety_flags_not_compatible");
        }

        return ok;
    }

    private static bool IsContextCompatible(SignalCandidate candidate, RuntimeMarketContext context)
    {
        var contextSymbol = !string.IsNullOrWhiteSpace(context.Symbol) ? context.Symbol : context.CurrentSymbol;
        if (!string.IsNullOrWhiteSpace(contextSymbol) && !string.Equals(contextSymbol, candidate.Asset, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var contextTimeframe = !string.IsNullOrWhiteSpace(context.Timeframe) ? context.Timeframe : context.CurrentTimeframe;
        if (!string.IsNullOrWhiteSpace(contextTimeframe) && !string.Equals(contextTimeframe, candidate.Timeframe, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private void WriteReport(PaperSignalEvaluationReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(PaperSignalEvaluationReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Paper Signal Evaluation");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- evaluated_signals: {report.EvaluatedSignals}");
        sb.AppendLine($"- actionable_signals: {report.ActionableSignals}");
        sb.AppendLine($"- skipped_signals: {report.SkippedSignals}");
        sb.AppendLine($"- waiting_signals: {report.WaitingSignals}");
        sb.AppendLine($"- watching_signals: {report.WatchingSignals}");
        sb.AppendLine($"- would_trigger_signals: {report.WouldTriggerSignals}");
        sb.AppendLine($"- active_signals: {report.ActiveSignals}");
        sb.AppendLine($"- completed_signals: {report.CompletedSignals}");
        sb.AppendLine($"- invalidated_signals: {report.InvalidatedSignals}");
        sb.AppendLine($"- expired_signals: {report.ExpiredSignals}");
        sb.AppendLine($"- paper_decision_summary: {report.PaperDecisionSummary}");
        sb.AppendLine();
        sb.AppendLine("## Signals");
        foreach (var signal in report.Signals)
        {
            sb.AppendLine($"- {signal.SignalId}: {signal.SignalStatus}; lifecycle={signal.SignalLifecycleStatus}; decision={signal.PaperDecision}; reason={signal.Reason}; asset={signal.Asset}; timeframe={signal.Timeframe}");
        }
        if (report.Signals.Count == 0)
        {
            sb.AppendLine("- none");
        }
        return sb.ToString();
    }
}
