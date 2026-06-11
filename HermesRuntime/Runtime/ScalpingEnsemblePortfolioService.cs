using System.Text.Json;

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
    string Asset,
    string Setup,
    string PrimaryCandidate,
    IReadOnlyList<string> BackupCandidates,
    IReadOnlyList<string> DirectionLogic,
    IReadOnlyList<string> EntryLogic,
    IReadOnlyList<string> ExitLogic,
    IReadOnlyList<string> InvalidationLogic,
    double ConfidenceBaseline,
    string Readiness,
    IReadOnlyList<string> SafetyFlags);

public sealed record EnsembleSignalAgentPortfolioPackage(
    string PackageId,
    DateTimeOffset CreatedUtc,
    string Status,
    IReadOnlyList<EnsembleSignalAgentPackageEntry> Entries,
    IReadOnlyList<string> Assets,
    IReadOnlyList<string> SafetyFlags,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled,
    bool ResearchOnly);

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
        var package = new EnsembleSignalAgentPortfolioPackage(
            PackageId: $"ensemble_signal_agent_package_{report.UpdatedAtUtc:yyyyMMddHHmmss}",
            CreatedUtc: DateTimeOffset.UtcNow,
            Status: report.PortfolioReadiness,
            Entries: report.Entries.Select(entry => new EnsembleSignalAgentPackageEntry(
                Asset: entry.Asset,
                Setup: entry.PrimarySetup,
                PrimaryCandidate: entry.PrimaryCandidate,
                BackupCandidates: entry.BackupCandidates,
                DirectionLogic: LoadSignalSpec(entry.PrimaryCandidate)?.SignalDirectionLogic ?? [entry.PrimarySetup],
                EntryLogic: LoadSignalSpec(entry.PrimaryCandidate)?.EntryConditions ?? [entry.PrimarySetup],
                ExitLogic: LoadSignalSpec(entry.PrimaryCandidate)?.ExitConditions ?? ["exit_on_signal_invalidated_or_target_hit"],
                InvalidationLogic: LoadSignalSpec(entry.PrimaryCandidate)?.InvalidationConditions ?? ["signal_invalidated"],
                ConfidenceBaseline: entry.ConfidenceBaseline,
                Readiness: entry.Readiness,
                SafetyFlags: entry.SafetyFlags)).ToList(),
            Assets: report.Assets,
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
- status: {package.Status}
- no_auto_trading: true
- human_review_required: true
- broker_orders_enabled: false
- live_trading_enabled: false
- research_only: true

## Entries
{string.Join(Environment.NewLine, package.Entries.Select(entry => $"- {entry.Asset}: setup={entry.Setup}, primary_candidate={entry.PrimaryCandidate}, readiness={entry.Readiness}"))}
""";
}
