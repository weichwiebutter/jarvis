using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record DemoSignalFeedItem(
    string SignalId,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? ExpiresUtc,
    string Asset,
    string Timeframe,
    string CandidateId,
    string SetupType,
    string Direction,
    double EntryLevel,
    double? EntryZoneLower,
    double? EntryZoneUpper,
    double StopLoss,
    double TakeProfit,
    double InvalidationLevel,
    double Confidence,
    string Status,
    string Reason,
    IReadOnlyList<string> RiskNotes,
    bool HumanReviewRequired,
    bool NoAutoTrading,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record DemoSignalFeedSnapshot(
    string FeedVersion,
    DateTimeOffset UpdatedAtUtc,
    string PackageId,
    string EnsembleReviewStatus,
    string FeedStatus,
    string FeedMode,
    int SignalCount,
    bool DemoSignalsAvailable,
    IReadOnlyList<string> Assets,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings,
    string SourcePackagePath,
    string ReviewStatusPath,
    string LatestSignalsJsonPath,
    string LatestSignalsMarkdownPath,
    string FeedLogPath,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record DemoSignalFeedLogEntry(
    DateTimeOffset TimestampUtc,
    string Action,
    string PackageId,
    string EnsembleReviewStatus,
    string FeedStatus,
    string FeedMode,
    int SignalCount,
    IReadOnlyList<string> Blockers,
    IReadOnlyList<string> Warnings,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

internal sealed record EnsembleSignalAgentPackage(
    string PackageId,
    DateTimeOffset CreatedUtc,
    string Status,
    bool HumanReviewRequired,
    string EnsembleMode,
    IReadOnlyList<EnsembleSignalAgentMember> Members,
    IReadOnlyList<string> RiskNotes,
    IReadOnlyList<string> OperationalLimits);

internal sealed record EnsembleSignalAgentMember(
    string CandidateId,
    string Asset,
    string SetupType,
    double Confidence,
    double ProfitFactor,
    double RecoveryFactor,
    double Drawdown,
    double MaxDailyDrawdown,
    double MaxWeeklyDrawdown,
    double SignalDensityScore,
    string ContributionReason,
    IReadOnlyList<string> RiskNotes,
    string SignalSpecPath,
    string BotSpecPath,
    string CertificationReportPath);

internal sealed record DemoSignalSpec(
    string CandidateId,
    string SignalName,
    string StrategyName,
    string Asset,
    string Timeframe,
    string SetupType,
    IReadOnlyList<string> SignalDirectionLogic,
    IReadOnlyList<string> EntryConditions,
    IReadOnlyList<string> InvalidationConditions,
    IReadOnlyList<string> ExitConditions,
    string SessionFilter,
    string SpreadFilter,
    string NewsFilter,
    double ConfidenceScore,
    IReadOnlyList<string> RiskNotes,
    int MaxTradesPerDay,
    double MaxDailyLoss,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

internal sealed record DemoCandidateCertification(
    string CandidateId,
    string Asset,
    string Timeframe,
    string SetupType,
    string Status);

public sealed class DemoSignalFeedService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public DemoSignalFeedService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "demo_signal_feed");
    public string StatusPath => Path.Combine(Root, "demo_signal_feed_status.json");
    public string LatestSignalsJsonPath => Path.Combine(Root, "latest_demo_signals.json");
    public string LatestSignalsMarkdownPath => Path.Combine(Root, "latest_demo_signals.md");
    public string LogPath => Path.Combine(Root, "demo_signal_feed_log.jsonl");

    public DemoSignalFeedSnapshot LoadOrCreateStatus()
    {
        if (File.Exists(StatusPath))
        {
            var snapshot = JsonSerializer.Deserialize<DemoSignalFeedSnapshot>(File.ReadAllText(StatusPath), JsonDefaults.SnapshotReadOptions);
            if (snapshot is not null)
            {
                return snapshot;
            }
        }

        return SaveSnapshot("status_check", null, []);
    }

    public DemoSignalFeedSnapshot? LoadStatus()
    {
        if (!File.Exists(StatusPath))
        {
            return null;
        }

        var snapshot = JsonSerializer.Deserialize<DemoSignalFeedSnapshot>(File.ReadAllText(StatusPath), JsonDefaults.SnapshotReadOptions);
        return snapshot is null || string.IsNullOrWhiteSpace(snapshot.FeedStatus) ? null : snapshot;
    }

    public DemoSignalFeedSnapshot Generate()
    {
        var signals = BuildSignals(out var blockers, out var warnings);
        return SaveSnapshot("generate_demo_signals", signals, blockers, warnings);
    }

    public IReadOnlyList<DemoSignalFeedItem> LoadLatestSignals()
    {
        if (!File.Exists(LatestSignalsJsonPath))
        {
            return [];
        }

        var signals = JsonSerializer.Deserialize<List<DemoSignalFeedItem>>(File.ReadAllText(LatestSignalsJsonPath), JsonDefaults.SnapshotReadOptions);
        return signals ?? [];
    }

    private DemoSignalFeedSnapshot SaveSnapshot(string action, IReadOnlyList<DemoSignalFeedItem>? signals, IReadOnlyList<string>? blockers = null, IReadOnlyList<string>? warnings = null)
    {
        var reviewService = new ScalpingEnsembleReviewService(_storagePaths, _runtimeRoot);
        var reviewState = reviewService.LoadOrCreate();
        var exportService = new ScalpingEnsembleExportService(_storagePaths, _runtimeRoot);
        var package = LoadPackage();
        var effectiveBlockers = blockers?.ToList() ?? ValidateFeedGates(package, reviewState, exportService);
        var effectiveWarnings = warnings?.ToList() ?? [];
        var feedMode = signals is null || signals.Count == 0
            ? "read_only_demo_pending_generation"
            : signals.Any(signal => signal.Reason.Contains("simulated_demo_signal", StringComparison.OrdinalIgnoreCase))
                ? "simulated_demo_signal"
                : "read_only_market_watch";
        var feedStatus = effectiveBlockers.Count > 0
            ? "blocked"
            : signals is null
                ? "pending_generation"
                : signals.Count == 0 ? "no_signals" : "demo_signals_ready";
        var snapshot = new DemoSignalFeedSnapshot(
            FeedVersion: "demo_signal_feed_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            PackageId: package?.PackageId ?? "missing_package",
            EnsembleReviewStatus: reviewState.ReviewStatus.ToString(),
            FeedStatus: feedStatus,
            FeedMode: feedMode,
            SignalCount: signals?.Count ?? LoadLatestSignals().Count,
            DemoSignalsAvailable: (signals?.Count ?? LoadLatestSignals().Count) > 0,
            Assets: package?.Members.Select(member => member.Asset).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(asset => asset).ToList() ?? [],
            Blockers: effectiveBlockers,
            Warnings: effectiveWarnings,
            SourcePackagePath: exportService.SignalAgentJsonPath,
            ReviewStatusPath: reviewService.StatusPath,
            LatestSignalsJsonPath: LatestSignalsJsonPath,
            LatestSignalsMarkdownPath: LatestSignalsMarkdownPath,
            FeedLogPath: LogPath,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);

        Directory.CreateDirectory(Root);
        File.WriteAllText(StatusPath, JsonSerializer.Serialize(snapshot, JsonDefaults.WriteOptions));
        if (signals is not null)
        {
            File.WriteAllText(LatestSignalsJsonPath, JsonSerializer.Serialize(signals, JsonDefaults.WriteOptions));
            File.WriteAllText(LatestSignalsMarkdownPath, BuildSignalsMarkdown(snapshot, signals));
        }

        AppendLog(action, snapshot);
        return snapshot;
    }

    private IReadOnlyList<DemoSignalFeedItem> BuildSignals(out List<string> blockers, out List<string> warnings)
    {
        var reviewService = new ScalpingEnsembleReviewService(_storagePaths, _runtimeRoot);
        var reviewState = reviewService.LoadOrCreate();
        var exportService = new ScalpingEnsembleExportService(_storagePaths, _runtimeRoot);
        var package = LoadPackage();
        blockers = ValidateFeedGates(package, reviewState, exportService);
        warnings = [];
        if (blockers.Count > 0)
        {
            return [];
        }

        var signals = new List<DemoSignalFeedItem>();
        foreach (var member in package!.Members)
        {
            var signalSpec = LoadSignalSpec(member.SignalSpecPath);
            var certification = LoadCertification(member.CertificationReportPath);
            var timeframe = signalSpec?.Timeframe
                ?? certification?.Timeframe
                ?? ParseTimeframeFromRiskNotes(member.RiskNotes)
                ?? "M5";
            var lastCandle = LoadLatestCandle(member.Asset, timeframe);
            var signal = lastCandle is null
                ? BuildSimulatedSignal(member, signalSpec, certification, timeframe, warnings)
                : BuildMarketWatchSignal(member, signalSpec, certification, timeframe, lastCandle, warnings);
            signals.Add(signal);
        }

        return signals;
    }

    private EnsembleSignalAgentPackage? LoadPackage()
    {
        var exportService = new ScalpingEnsembleExportService(_storagePaths, _runtimeRoot);
        return File.Exists(exportService.SignalAgentJsonPath)
            ? JsonSerializer.Deserialize<EnsembleSignalAgentPackage>(File.ReadAllText(exportService.SignalAgentJsonPath), JsonDefaults.SnapshotReadOptions)
            : null;
    }

    private static DemoSignalSpec? LoadSignalSpec(string path) =>
        File.Exists(path)
            ? JsonSerializer.Deserialize<DemoSignalSpec>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions)
            : null;

    private static DemoCandidateCertification? LoadCertification(string path) =>
        File.Exists(path)
            ? JsonSerializer.Deserialize<DemoCandidateCertification>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions)
            : null;

    private MarketDataCandle? LoadLatestCandle(string asset, string timeframe)
    {
        var directory = Path.Combine(_storagePaths.Root, "market_data", "candles", asset, timeframe);
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var latestFile = Directory.GetFiles(directory, "*.candles.jsonl", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (latestFile is null)
        {
            return null;
        }

        var line = ReadLastNonEmptyLine(latestFile);
        return string.IsNullOrWhiteSpace(line)
            ? null
            : JsonSerializer.Deserialize<MarketDataCandle>(line, JsonDefaults.SnapshotReadOptions);
    }

    private static string? ReadLastNonEmptyLine(string path)
    {
        return File.ReadLines(path).Reverse().FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
    }

    private DemoSignalFeedItem BuildSimulatedSignal(
        EnsembleSignalAgentMember member,
        DemoSignalSpec? signalSpec,
        DemoCandidateCertification? certification,
        string timeframe,
        List<string> warnings)
    {
        if (signalSpec is null)
        {
            warnings.Add($"signal_spec_missing:{member.CandidateId}");
        }

        var direction = InferDirection(member.SetupType, signalSpec);
        var baseEntry = BaseEntryPrice(member.Asset);
        var riskUnit = RiskUnit(member.Asset);
        var isShort = direction.StartsWith("short", StringComparison.OrdinalIgnoreCase);
        var entry = RoundPrice(member.Asset, baseEntry);
        var entryZoneLower = RoundPrice(member.Asset, entry - (riskUnit * 0.1));
        var entryZoneUpper = RoundPrice(member.Asset, entry + (riskUnit * 0.1));
        var stop = RoundPrice(member.Asset, isShort ? entry + riskUnit : entry - riskUnit);
        var target = RoundPrice(member.Asset, isShort ? entry - (riskUnit * 1.5) : entry + (riskUnit * 1.5));
        var invalidation = stop;
        var createdUtc = DateTimeOffset.UtcNow;
        var expiresUtc = createdUtc.AddHours(12);
        var reasonParts = new List<string>
        {
            "simulated_demo_signal",
            "no_current_market_data_used_for_execution",
            "read_only_signal_preview_only",
            signalSpec is null ? "member_signal_spec_missing_used_certification_and_ensemble_fallback" : "member_signal_spec_loaded"
        };

        return new DemoSignalFeedItem(
            SignalId: $"demo_signal_{member.CandidateId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            CreatedUtc: createdUtc,
            ExpiresUtc: expiresUtc,
            Asset: member.Asset,
            Timeframe: timeframe,
            CandidateId: member.CandidateId,
            SetupType: signalSpec?.SetupType ?? certification?.SetupType ?? member.SetupType,
            Direction: direction,
            EntryLevel: entry,
            EntryZoneLower: entryZoneLower,
            EntryZoneUpper: entryZoneUpper,
            StopLoss: stop,
            TakeProfit: target,
            InvalidationLevel: invalidation,
            Confidence: Math.Round(Math.Clamp(signalSpec?.ConfidenceScore ?? member.Confidence, 0, 0.95), 4),
            Status: "watch",
            Reason: string.Join(";", reasonParts),
            RiskNotes: MergeRiskNotes(member, signalSpec, ["simulated_demo_signal", "no_fake_performance_claims", "no_execution_simulation"]),
            HumanReviewRequired: true,
            NoAutoTrading: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }

    private DemoSignalFeedItem BuildMarketWatchSignal(
        EnsembleSignalAgentMember member,
        DemoSignalSpec? signalSpec,
        DemoCandidateCertification? certification,
        string timeframe,
        MarketDataCandle lastCandle,
        List<string> warnings)
    {
        if (signalSpec is null)
        {
            warnings.Add($"signal_spec_missing:{member.CandidateId}");
        }

        var direction = InferDirection(member.SetupType, signalSpec);
        var isShort = direction.StartsWith("short", StringComparison.OrdinalIgnoreCase);
        var candleRange = Math.Max(lastCandle.High - lastCandle.Low, RiskUnit(member.Asset));
        var entry = RoundPrice(member.Asset, lastCandle.Close);
        var entryZoneLower = RoundPrice(member.Asset, entry - (candleRange * 0.15));
        var entryZoneUpper = RoundPrice(member.Asset, entry + (candleRange * 0.15));
        var stop = RoundPrice(member.Asset, isShort ? entry + candleRange : entry - candleRange);
        var target = RoundPrice(member.Asset, isShort ? entry - (candleRange * 1.6) : entry + (candleRange * 1.6));
        var invalidation = stop;
        var stale = DateTimeOffset.UtcNow - lastCandle.TimestampUtc > TimeSpan.FromDays(7);
        var status = stale ? "watch" : "waiting_for_trigger";
        var createdUtc = DateTimeOffset.UtcNow;
        var expiresUtc = createdUtc.AddHours(ParseLifetimeHours(timeframe));
        var reason = stale
            ? "read_only_market_watch;market_data_stale_for_live_context_but_used_for_demo_watch_only"
            : "read_only_market_watch;latest_candle_loaded;no_order_execution";
        if (stale)
        {
            warnings.Add($"market_data_stale:{member.Asset}:{timeframe}:{lastCandle.TimestampUtc:O}");
        }

        return new DemoSignalFeedItem(
            SignalId: $"demo_signal_{member.CandidateId}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
            CreatedUtc: createdUtc,
            ExpiresUtc: expiresUtc,
            Asset: member.Asset,
            Timeframe: timeframe,
            CandidateId: member.CandidateId,
            SetupType: signalSpec?.SetupType ?? certification?.SetupType ?? member.SetupType,
            Direction: direction,
            EntryLevel: entry,
            EntryZoneLower: entryZoneLower,
            EntryZoneUpper: entryZoneUpper,
            StopLoss: stop,
            TakeProfit: target,
            InvalidationLevel: invalidation,
            Confidence: Math.Round(Math.Clamp(signalSpec?.ConfidenceScore ?? member.Confidence, 0, 0.95), 4),
            Status: status,
            Reason: reason,
            RiskNotes: MergeRiskNotes(member, signalSpec, [$"latest_candle_utc={lastCandle.TimestampUtc:O}", "read_only_market_watch", "no_execution_path"]),
            HumanReviewRequired: true,
            NoAutoTrading: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }

    private static List<string> ValidateFeedGates(EnsembleSignalAgentPackage? package, ScalpingEnsembleReviewState reviewState, ScalpingEnsembleExportService exportService)
    {
        var blockers = new List<string>();
        if (package is null) blockers.Add("ensemble_signal_agent_package_missing");
        if (!File.Exists(exportService.SignalAgentJsonPath)) blockers.Add("ensemble_signal_agent_package_file_missing");
        if (package is not null && !package.Status.Equals("ensemble_ready", StringComparison.OrdinalIgnoreCase)) blockers.Add($"ensemble_not_ready:{package.Status}");
        if (reviewState.ReviewStatus != ScalpingEnsembleReviewStatus.approved_for_demo_signal_use) blockers.Add($"ensemble_not_approved_for_demo_signal_use:{reviewState.ReviewStatus}");
        if (package is not null && !package.HumanReviewRequired) blockers.Add("human_review_required_not_confirmed");
        return blockers;
    }

    private void AppendLog(string action, DemoSignalFeedSnapshot snapshot)
    {
        Directory.CreateDirectory(Root);
        var entry = new DemoSignalFeedLogEntry(
            TimestampUtc: DateTimeOffset.UtcNow,
            Action: action,
            PackageId: snapshot.PackageId,
            EnsembleReviewStatus: snapshot.EnsembleReviewStatus,
            FeedStatus: snapshot.FeedStatus,
            FeedMode: snapshot.FeedMode,
            SignalCount: snapshot.SignalCount,
            Blockers: snapshot.Blockers,
            Warnings: snapshot.Warnings,
            NoAutoTrading: snapshot.NoAutoTrading,
            HumanReviewRequired: snapshot.HumanReviewRequired,
            BrokerOrdersEnabled: snapshot.BrokerOrdersEnabled,
            LiveTradingEnabled: snapshot.LiveTradingEnabled);
        File.AppendAllText(LogPath, JsonSerializer.Serialize(entry, JsonDefaults.WriteOptions) + Environment.NewLine);
    }

    private static string BuildSignalsMarkdown(DemoSignalFeedSnapshot snapshot, IReadOnlyList<DemoSignalFeedItem> signals)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Demo Signal Feed");
        builder.AppendLine();
        builder.AppendLine($"- package_id: {snapshot.PackageId}");
        builder.AppendLine($"- ensemble_review_status: {snapshot.EnsembleReviewStatus}");
        builder.AppendLine($"- feed_status: {snapshot.FeedStatus}");
        builder.AppendLine($"- feed_mode: {snapshot.FeedMode}");
        builder.AppendLine($"- signal_count: {signals.Count}");
        builder.AppendLine("- Demo Signal Feed only");
        builder.AppendLine("- no_auto_trading: true");
        builder.AppendLine("- human_review_required: true");
        builder.AppendLine("- broker_orders_enabled: false");
        builder.AppendLine("- live_trading_enabled: false");
        builder.AppendLine("- no cTrader Order API");
        builder.AppendLine("- no broker orders");
        builder.AppendLine();
        foreach (var signal in signals)
        {
            builder.AppendLine($"## {signal.SignalId}");
            builder.AppendLine($"- asset: {signal.Asset}");
            builder.AppendLine($"- timeframe: {signal.Timeframe}");
            builder.AppendLine($"- candidate_id: {signal.CandidateId}");
            builder.AppendLine($"- setup_type: {signal.SetupType}");
            builder.AppendLine($"- direction: {signal.Direction}");
            builder.AppendLine($"- entry_level: {signal.EntryLevel}");
            builder.AppendLine($"- entry_zone_lower: {(signal.EntryZoneLower.HasValue ? signal.EntryZoneLower.Value.ToString("0.#####") : "n/a")}");
            builder.AppendLine($"- entry_zone_upper: {(signal.EntryZoneUpper.HasValue ? signal.EntryZoneUpper.Value.ToString("0.#####") : "n/a")}");
            builder.AppendLine($"- stop_loss: {signal.StopLoss}");
            builder.AppendLine($"- take_profit: {signal.TakeProfit}");
            builder.AppendLine($"- invalidation_level: {signal.InvalidationLevel}");
            builder.AppendLine($"- expires_utc: {(signal.ExpiresUtc.HasValue ? signal.ExpiresUtc.Value.ToString("O") : "n/a")}");
            builder.AppendLine($"- confidence: {signal.Confidence:0.####}");
            builder.AppendLine($"- status: {signal.Status}");
            builder.AppendLine($"- reason: {signal.Reason}");
            builder.AppendLine($"- risk_notes: {string.Join(", ", signal.RiskNotes)}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static List<string> MergeRiskNotes(EnsembleSignalAgentMember member, DemoSignalSpec? signalSpec, IReadOnlyList<string> extra)
    {
        return member.RiskNotes
            .Concat(signalSpec?.RiskNotes ?? [])
            .Concat(extra)
            .Append("demo_signal_feed_only")
            .Append("no_broker_orders")
            .Append("no_ctrader_order_api")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string InferDirection(string setupType, DemoSignalSpec? signalSpec)
    {
        var logic = string.Join(" ", signalSpec?.SignalDirectionLogic ?? []);
        if (logic.Contains("short", StringComparison.OrdinalIgnoreCase)) return "short_watch";
        if (logic.Contains("long", StringComparison.OrdinalIgnoreCase)) return "long_watch";
        return setupType.Contains("breakout", StringComparison.OrdinalIgnoreCase)
            ? "long_watch"
            : "watch";
    }

    private static string? ParseTimeframeFromRiskNotes(IReadOnlyList<string> notes)
    {
        foreach (var note in notes)
        {
            var parts = note.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length >= 3 && parts[1].StartsWith("M", StringComparison.OrdinalIgnoreCase))
            {
                return parts[1].ToUpperInvariant();
            }
        }

        return null;
    }

    private static double BaseEntryPrice(string asset) => asset.ToUpperInvariant() switch
    {
        "EURUSD" => 1.08500,
        "XAUUSD" => 2325.00,
        _ => 100.0
    };

    private static double RiskUnit(string asset) => asset.ToUpperInvariant() switch
    {
        "EURUSD" => 0.0008,
        "XAUUSD" => 12.5,
        "GER40" => 35.0,
        _ => 1.0
    };

    private static int ParseLifetimeHours(string timeframe) => timeframe.ToUpperInvariant() switch
    {
        "M1" => 4,
        "M5" => 12,
        "M15" => 24,
        "M30" => 48,
        "H1" => 72,
        _ => 24
    };

    private static double RoundPrice(string asset, double value) => asset.ToUpperInvariant() switch
    {
        "EURUSD" => Math.Round(value, 5),
        _ => Math.Round(value, 2)
    };
}
