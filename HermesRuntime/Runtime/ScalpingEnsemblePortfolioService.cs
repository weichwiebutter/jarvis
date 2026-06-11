using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hermes.Runtime;

public sealed record ScalpingEnsemblePortfolioAssetEntry(
    string Asset,
    int SetupCount,
    int CertifiedCandidateCount,
    int SignalSpecCount,
    string PrimarySetup,
    IReadOnlyList<string> BackupSetups,
    string PrimaryCandidate,
    IReadOnlyList<string> BackupCandidates,
    double ConfidenceBaseline,
    string Readiness,
    string PortfolioReadiness,
    bool HumanReviewRequired,
    IReadOnlyList<string> SafetyFlags);

public sealed record ScalpingEnsemblePortfolioReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<string> Assets,
    IReadOnlyList<ScalpingEnsemblePortfolioAssetEntry> Entries,
    int SetupCountTotal,
    int CertifiedCandidateCountTotal,
    int SignalSpecCountTotal,
    string PortfolioReadiness,
    IReadOnlyList<string> Warnings,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled,
    bool ResearchOnly);

public sealed record EnsembleSignalAgentPackageEntry(
    [property: JsonPropertyName("asset")] string Asset,
    [property: JsonPropertyName("setup_id")] string SetupId,
    [property: JsonPropertyName("setup_name")] string SetupName,
    [property: JsonPropertyName("timeframe")] string Timeframe,
    [property: JsonPropertyName("direction")] string Direction,
    [property: JsonPropertyName("primary_candidate")] string PrimaryCandidate,
    [property: JsonPropertyName("backup_candidates")] IReadOnlyList<string> BackupCandidates,
    [property: JsonPropertyName("confidence_baseline")] double ConfidenceBaseline,
    [property: JsonPropertyName("signal_frequency")] string SignalFrequency,
    [property: JsonPropertyName("entry_logic")] IReadOnlyList<string> EntryLogic,
    [property: JsonPropertyName("exit_logic")] IReadOnlyList<string> ExitLogic,
    [property: JsonPropertyName("stop_loss_logic")] IReadOnlyList<string> StopLossLogic,
    [property: JsonPropertyName("take_profit_logic")] IReadOnlyList<string> TakeProfitLogic,
    [property: JsonPropertyName("invalidation_logic")] IReadOnlyList<string> InvalidationLogic,
    [property: JsonPropertyName("market_regime_tags")] IReadOnlyList<string> MarketRegimeTags,
    [property: JsonPropertyName("session_tags")] IReadOnlyList<string> SessionTags,
    [property: JsonPropertyName("risk_notes")] IReadOnlyList<string> RiskNotes,
    [property: JsonPropertyName("readiness")] string Readiness,
    [property: JsonPropertyName("human_review_required")] bool HumanReviewRequired,
    [property: JsonPropertyName("no_auto_trading")] bool NoAutoTrading,
    [property: JsonPropertyName("broker_orders_enabled")] bool BrokerOrdersEnabled,
    [property: JsonPropertyName("live_trading_enabled")] bool LiveTradingEnabled);

public sealed record EnsembleSignalAgentPortfolioPackage(
    [property: JsonPropertyName("package_id")] string PackageId,
    [property: JsonPropertyName("generated_at")] DateTimeOffset GeneratedAtUtc,
    [property: JsonPropertyName("package_version")] string PackageVersion,
    [property: JsonPropertyName("source_system")] string SourceSystem,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("assets")] IReadOnlyList<EnsembleSignalAgentPackageEntry> Assets,
    [property: JsonPropertyName("safety_flags")] IReadOnlyList<string> SafetyFlags,
    [property: JsonPropertyName("no_auto_trading")] bool NoAutoTrading,
    [property: JsonPropertyName("human_review_required")] bool HumanReviewRequired,
    [property: JsonPropertyName("broker_orders_enabled")] bool BrokerOrdersEnabled,
    [property: JsonPropertyName("live_trading_enabled")] bool LiveTradingEnabled,
    [property: JsonPropertyName("research_only")] bool ResearchOnly);

public sealed class ScalpingEnsemblePortfolioService
{
    private static readonly string[] DefaultAssets = ["GER40", "XAUUSD", "EURUSD"];
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedRoot;

    public ScalpingEnsemblePortfolioService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => _resolvedRoot ??= ResolveRoot();
    public string PortfolioPath => Path.Combine(Root, "ensemble_portfolio_status.json");
    public string PortfolioMarkdownPath => Path.Combine(Root, "ensemble_portfolio_status.md");
    public string PackagePath => Path.Combine(Root, "ensemble_signal_agent_package.json");
    public string PackageMarkdownPath => Path.Combine(Root, "ensemble_signal_agent_package.md");

    public ScalpingEnsemblePortfolioReport Build()
    {
        var registryService = new CertifiedCandidateInventoryService(_storagePaths, _runtimeRoot);
        var registry = registryService.LoadRegistry() ?? registryService.BuildRegistry();
        var inventory = registryService.LoadInventory() ?? registryService.BuildInventory();
        var signalSpecDirs = SignalSpecDirectories().ToList();
        var readinessService = new ScalpingAssetReadinessService(_storagePaths, _runtimeRoot);
        var assets = DefaultAssets
            .Concat(registry.Assets.Select(entry => entry.Asset))
            .Concat(inventory.Items.Select(item => item.Asset))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entries = assets.Select(asset =>
        {
            var assetRegistry = registry.Assets.Where(entry => entry.Asset.Equals(asset, StringComparison.OrdinalIgnoreCase)).ToList();
            var assetInventory = inventory.Items.Where(item => item.Asset.Equals(asset, StringComparison.OrdinalIgnoreCase)).ToList();
            var readiness = readinessService.Evaluate(asset);
            var primary = assetRegistry.OrderByDescending(entry => entry.ConfidenceBaseline).FirstOrDefault();
            var backups = assetRegistry
                .OrderByDescending(entry => entry.ConfidenceBaseline)
                .Skip(1)
                .Select(entry => entry.SetupId)
                .ToList();
            var primaryCandidate = primary?.PrimaryCandidate ?? "-";
            var backupCandidates = primary?.BackupCandidates ?? [];
            var signalSpecCount = assetInventory.Count(item => signalSpecDirs.Any(directory => File.Exists(Path.Combine(directory, item.CandidateId, "signal_agent_spec.json"))));
            var readinessStatus = readiness.AssetStatus is "bot_ready" or "setup_ready" ? readiness.AssetStatus : readiness.SignalReadyStatus;
            var portfolioReadiness = readinessStatus is "bot_ready" ? "portfolio_ready" : readinessStatus;
            return new ScalpingEnsemblePortfolioAssetEntry(
                Asset: asset,
                SetupCount: assetRegistry.Count,
                CertifiedCandidateCount: assetInventory.Count,
                SignalSpecCount: signalSpecCount,
                PrimarySetup: primary?.SetupId ?? "-",
                BackupSetups: backups,
                PrimaryCandidate: primaryCandidate,
                BackupCandidates: backupCandidates,
                ConfidenceBaseline: primary?.ConfidenceBaseline ?? 0,
                Readiness: readinessStatus,
                PortfolioReadiness: portfolioReadiness,
                HumanReviewRequired: true,
                SafetyFlags: ["no_auto_trading=true", "human_review_required=true", "broker_orders_enabled=false", "live_trading_enabled=false", "research_only=true"]);
        }).ToList();

        var report = new ScalpingEnsemblePortfolioReport(
            ReportVersion: "scalping_ensemble_portfolio_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Assets: assets,
            Entries: entries,
            SetupCountTotal: entries.Sum(entry => entry.SetupCount),
            CertifiedCandidateCountTotal: entries.Sum(entry => entry.CertifiedCandidateCount),
            SignalSpecCountTotal: entries.Sum(entry => entry.SignalSpecCount),
            PortfolioReadiness: entries.All(entry => entry.PortfolioReadiness == "portfolio_ready") ? "portfolio_ready" : entries.Any(entry => entry.Readiness == "signal_ready") ? "signal_ready" : "needs_validation",
            Warnings: BuildWarnings(entries),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false,
            ResearchOnly: true);

        Directory.CreateDirectory(Root);
        File.WriteAllText(PortfolioPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(PortfolioMarkdownPath, BuildMarkdown(report));
        return report;
    }

    public ScalpingEnsemblePortfolioReport? Load()
        => File.Exists(PortfolioPath)
            ? JsonSerializer.Deserialize<ScalpingEnsemblePortfolioReport>(File.ReadAllText(PortfolioPath), JsonDefaults.SnapshotReadOptions)
            : null;

    public EnsembleSignalAgentPortfolioPackage Export()
    {
        var report = Load() ?? Build();
        var registryService = new CertifiedCandidateInventoryService(_storagePaths, _runtimeRoot);
        var registry = registryService.LoadRegistry() ?? registryService.BuildRegistry();
        var researchService = new ScalpingResearchService(_storagePaths, _runtimeRoot);
        var package = new EnsembleSignalAgentPortfolioPackage(
            PackageId: $"ensemble_signal_agent_package_{report.UpdatedAtUtc:yyyyMMddHHmmss}",
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            PackageVersion: "ensemble_signal_agent_package_v1",
            SourceSystem: "HermesRuntime/SystemA",
            Status: report.PortfolioReadiness,
            Assets: report.Entries.Select(entry =>
            {
                var registryEntry = registry.Assets.FirstOrDefault(item => item.Asset.Equals(entry.Asset, StringComparison.OrdinalIgnoreCase) && item.SetupId.Equals(entry.PrimarySetup, StringComparison.OrdinalIgnoreCase));
                var candidate = researchService.FindCandidate(entry.PrimaryCandidate);
                var signalSpec = LoadSignalSpec(entry.PrimaryCandidate);
                return new EnsembleSignalAgentPackageEntry(
                    Asset: entry.Asset,
                    SetupId: registryEntry?.SetupId ?? entry.PrimarySetup,
                    SetupName: registryEntry?.SetupId ?? entry.PrimarySetup,
                    Timeframe: registryEntry?.PrimaryTimeframe ?? candidate?.Timeframe ?? "unknown",
                    Direction: registryEntry?.AllowedDirections.FirstOrDefault() ?? candidate?.SetupType ?? "long_short",
                    PrimaryCandidate: entry.PrimaryCandidate,
                    BackupCandidates: entry.BackupCandidates,
                    ConfidenceBaseline: entry.ConfidenceBaseline,
                    SignalFrequency: registryEntry?.ExpectedSignalFrequency ?? $"{entry.SignalSpecCount} signal specs",
                    EntryLogic: signalSpec?.EntryConditions ?? candidate?.EntryRules ?? [entry.PrimarySetup],
                    ExitLogic: signalSpec?.ExitConditions ?? candidate?.ExitRules ?? ["exit_on_signal_invalidated_or_target_hit"],
                    StopLossLogic: candidate?.StopLossRules ?? ["technical_stop_beyond_recent_swing"],
                    TakeProfitLogic: candidate?.TakeProfitRules ?? ["target_at_setup_volatility_band"],
                    InvalidationLogic: signalSpec?.InvalidationConditions ?? [.. (candidate?.StopLossRules ?? []), "signal_invalidated"],
                    MarketRegimeTags: registryEntry?.MarketRegimeTags ?? [],
                    SessionTags: registryEntry?.SessionTags ?? [],
                    RiskNotes: BuildRiskNotes(entry, registryEntry, candidate, signalSpec),
                    Readiness: entry.Readiness,
                    HumanReviewRequired: true,
                    NoAutoTrading: true,
                    BrokerOrdersEnabled: false,
                    LiveTradingEnabled: false);
            }).ToList(),
            SafetyFlags: ["no_auto_trading=true", "human_review_required=true", "broker_orders_enabled=false", "live_trading_enabled=false", "research_only=true"],
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false,
            ResearchOnly: true);

        File.WriteAllText(PackagePath, JsonSerializer.Serialize(package, JsonDefaults.WriteOptions));
        File.WriteAllText(PackageMarkdownPath, BuildPackageMarkdown(package));
        return package;
    }

    public EnsembleSignalAgentPortfolioPackage? LoadPackage()
        => File.Exists(PackagePath)
            ? JsonSerializer.Deserialize<EnsembleSignalAgentPortfolioPackage>(File.ReadAllText(PackagePath), JsonDefaults.SnapshotReadOptions)
            : null;

    public string ExplainSelection(string asset)
    {
        var report = Load() ?? Build();
        var normalized = NormalizeAsset(asset);
        var entry = report.Entries.FirstOrDefault(item => item.Asset.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            return $"asset={normalized}; no_setup_selected; reason=no_portfolio_entry; no_auto_trading=true; human_review_required=true; broker_orders_enabled=false; live_trading_enabled=false; research_only=true";
        }

        var reasons = new List<string>
        {
            $"selected_setup={entry.PrimarySetup}",
            $"primary_candidate={entry.PrimaryCandidate}",
            $"backup_setups={string.Join(",", entry.BackupSetups)}",
            $"backup_candidates={string.Join(",", entry.BackupCandidates)}",
            $"confidence_baseline={entry.ConfidenceBaseline:0.####}",
            $"signal_frequency={entry.SignalSpecCount}",
            $"readiness={entry.Readiness}"
        };
        if (entry.Readiness == "bot_ready")
        {
            reasons.AddRange(["highest_quality", "sufficient_signal_frequency", "monte_carlo_ok", "oos_ok", "walk_forward_ok"]);
        }
        else if (entry.Readiness == "setup_ready")
        {
            reasons.Add("setup_registry_ready");
        }
        else
        {
            reasons.Add("needs_more_validation");
        }

        return $"asset={normalized}; {string.Join("; ", reasons)}; no_auto_trading=true; human_review_required=true; broker_orders_enabled=false; live_trading_enabled=false; research_only=true";
    }

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<ScalpingEnsemblePortfolioAssetEntry> entries)
    {
        var warnings = new List<string>();
        if (entries.Any(entry => entry.SignalSpecCount == 0)) warnings.Add("missing_signal_specs");
        if (entries.Any(entry => entry.SetupCount == 0)) warnings.Add("missing_setup_registry_entries");
        if (entries.Any(entry => entry.Readiness == "signal_ready")) warnings.Add("signal_ready_before_bot_ready");
        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private ScalpingSignalSpec? LoadSignalSpec(string candidateId)
    {
        foreach (var path in SignalSpecPaths(candidateId))
        {
            if (!File.Exists(path)) continue;
            var spec = JsonSerializer.Deserialize<ScalpingSignalSpec>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
            if (spec is not null) return spec;
        }
        return null;
    }

    private IEnumerable<string> SignalSpecPaths(string candidateId)
    {
        foreach (var directory in SignalSpecDirectories())
        {
            yield return Path.Combine(directory, candidateId, "signal_agent_spec.json");
        }
    }

    private IEnumerable<string> SignalSpecDirectories()
    {
        yield return Path.Combine(_storagePaths.Root, "reports", "signal_agent_specs");
        yield return Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "signal_agent_specs");
    }

    private string ResolveRoot()
    {
        var preferred = Path.Combine(_storagePaths.Root, "reports", "scalping_portfolio", "ensemble_portfolio");
        try
        {
            Directory.CreateDirectory(preferred);
            var probePath = Path.Combine(preferred, ".write_probe");
            File.WriteAllText(probePath, "probe");
            File.Delete(probePath);
            return preferred;
        }
        catch (IOException)
        {
            return ResolveFallbackRoot();
        }
        catch (UnauthorizedAccessException)
        {
            return ResolveFallbackRoot();
        }
    }

    private string ResolveFallbackRoot()
    {
        var fallback = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "scalping_portfolio", "ensemble_portfolio");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static string NormalizeAsset(string asset)
        => asset.Trim().Equals("GOLD", StringComparison.OrdinalIgnoreCase) ? "XAUUSD" : asset.Trim().ToUpperInvariant();

    private static string BuildMarkdown(ScalpingEnsemblePortfolioReport report) => $"""
# Scalping Ensemble Portfolio

- portfolio_readiness: {report.PortfolioReadiness}
- setup_count_total: {report.SetupCountTotal}
- certified_candidate_count_total: {report.CertifiedCandidateCountTotal}
- signal_spec_count_total: {report.SignalSpecCountTotal}
- no_auto_trading: true
- human_review_required: true
- broker_orders_enabled: false
- live_trading_enabled: false
- research_only: true

## Assets
{string.Join(Environment.NewLine, report.Entries.Select(entry => $"- {entry.Asset}: primary_setup={entry.PrimarySetup}, readiness={entry.Readiness}, setup_count={entry.SetupCount}, certified_candidates={entry.CertifiedCandidateCount}, signal_specs={entry.SignalSpecCount}"))}

## Warnings
{string.Join(Environment.NewLine, report.Warnings.Select(warning => $"- {warning}"))}
""";

    private static string BuildPackageMarkdown(EnsembleSignalAgentPortfolioPackage package) => $"""
# Ensemble Signal Agent Package

- package_id: {package.PackageId}
- package_version: {package.PackageVersion}
- generated_at: {package.GeneratedAtUtc:O}
- source_system: {package.SourceSystem}
- status: {package.Status}
- no_auto_trading: true
- human_review_required: true
- broker_orders_enabled: false
- live_trading_enabled: false
- research_only: true

## Entries
{string.Join(Environment.NewLine, package.Assets.Select(entry => $"- {entry.Asset}: setup={entry.SetupId}, primary_candidate={entry.PrimaryCandidate}, readiness={entry.Readiness}, timeframe={entry.Timeframe}, direction={entry.Direction}"))}
""";

    private static IReadOnlyList<string> BuildRiskNotes(
        ScalpingEnsemblePortfolioAssetEntry entry,
        SetupRegistryEntry? registryEntry,
        ScalpingStrategyCandidate? candidate,
        ScalpingSignalSpec? signalSpec)
    {
        var notes = new List<string>
        {
            "research_only",
            "human_review_required",
            "no_auto_trading",
            "no_broker_orders",
            "no_live_trading"
        };

        if (candidate is not null)
        {
            notes.AddRange(candidate.RejectionReasons);
            notes.AddRange(candidate.RiskProfile.RiskNotes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        if (signalSpec is not null)
        {
            notes.AddRange(signalSpec.RiskNotes);
        }

        if (registryEntry is not null && !registryEntry.ReadinessStatus.Equals("bot_ready", StringComparison.OrdinalIgnoreCase))
        {
            notes.Add(registryEntry.ReadinessStatus);
        }

        if (!string.IsNullOrWhiteSpace(entry.Readiness))
        {
            notes.Add(entry.Readiness);
        }

        return notes.Where(note => !string.IsNullOrWhiteSpace(note)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
