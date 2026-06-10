using System.Text.Json;

namespace Hermes.Runtime;

public sealed record CertifiedCandidateInventoryItem(
    string CandidateId,
    string Asset,
    string Timeframe,
    string SetupType,
    string Direction,
    string CertificationStatus,
    double? QualityScore,
    double? TrustScore,
    double? ProfitFactor,
    double? WinRate,
    double MaxDrawdownR,
    double MaxDailyDrawdownR,
    double RiskOfRuin,
    double SignalDensity,
    string StabilityStatus,
    string SourceReportPath,
    bool HumanReviewRequired);

public sealed record CertifiedCandidateInventory(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<CertifiedCandidateInventoryItem> Items,
    IReadOnlyDictionary<string, int> AssetsByCount,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record SetupRegistryEntry(
    string SetupId,
    string Asset,
    string PrimaryTimeframe,
    string SetupType,
    IReadOnlyList<string> AllowedDirections,
    IReadOnlyList<string> CandidateMembers,
    string PrimaryCandidate,
    IReadOnlyList<string> BackupCandidates,
    IReadOnlyList<string> MarketRegimeTags,
    IReadOnlyList<string> SessionTags,
    double ConfidenceBaseline,
    double AverageQualityScore,
    double AverageProfitFactor,
    double AverageWinRate,
    double AverageMaxDrawdownR,
    double AverageRiskOfRuin,
    string ExpectedSignalFrequency,
    string TradeCountRange,
    int MinimumMemberTradeCount,
    int MaximumMemberTradeCount,
    string RiskProfile,
    string ReadinessStatus,
    bool HumanReviewRequired,
    bool NoAutoTrading);

public sealed record SetupRegistry(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<SetupRegistryEntry> Assets,
    IReadOnlyDictionary<string, int> SetupCountsByAsset,
    IReadOnlyDictionary<string, string> BestSetupByAsset,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed class CertifiedCandidateInventoryService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedRoot;

    public CertifiedCandidateInventoryService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => _resolvedRoot ??= ResolveRoot();
    public string InventoryPath => Path.Combine(Root, "certified_candidate_inventory.json");
    public string SetupRegistryPath => Path.Combine(Root, "setup_registry.json");

    public CertifiedCandidateInventory BuildInventory()
    {
        var certs = new ScalpingCertificationService(_storagePaths, _runtimeRoot).LoadReports()
            .Where(report => report.Status == ScalpingCertificationStatus.certified_candidate)
            .ToList();
        var items = certs.Select(report =>
        {
            var candidate = new ScalpingResearchService(_storagePaths, _runtimeRoot).FindCandidate(report.CandidateId);
            var robustness = new ScalpingRobustnessExpansionService(_storagePaths, _runtimeRoot).LoadReport(report.CandidateId);
            return new CertifiedCandidateInventoryItem(
                CandidateId: report.CandidateId,
                Asset: report.Asset,
                Timeframe: report.Timeframe,
                SetupType: report.SetupType,
                Direction: CandidateDirection(report.SetupType),
                CertificationStatus: report.Status.ToString(),
                QualityScore: candidate?.ConfidenceScore,
                TrustScore: candidate?.ConfidenceScore,
                ProfitFactor: report.DrawdownCertification.ProfitFactor,
                WinRate: report.TradeDistribution.Winners + report.TradeDistribution.Losers > 0
                    ? report.TradeDistribution.Winners / (double)(report.TradeDistribution.Winners + report.TradeDistribution.Losers)
                    : null,
                MaxDrawdownR: report.DrawdownCertification.MaxDrawdownR,
                MaxDailyDrawdownR: report.DrawdownCertification.MaxDailyDrawdownR,
                RiskOfRuin: candidate?.RiskProfile.RiskOfRuinProbability ?? 0,
                SignalDensity: CandidateSignalDensity(report.CandidateId),
                StabilityStatus: robustness?.Status.ToString() ?? "certified",
                SourceReportPath: report.CertificationReportPath,
                HumanReviewRequired: report.HumanReviewRequired);
        }).ToList();

        var inventory = new CertifiedCandidateInventory(
            ReportVersion: "certified_candidate_inventory_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Items: items,
            AssetsByCount: items.GroupBy(item => item.Asset).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);

        Directory.CreateDirectory(Root);
        File.WriteAllText(InventoryPath, JsonSerializer.Serialize(inventory, JsonDefaults.WriteOptions));
        return inventory;
    }

    public SetupRegistry BuildRegistry()
    {
        var inventory = LoadInventory() ?? BuildInventory();
        var groups = inventory.Items
            .GroupBy(item => (item.Asset, item.Timeframe, item.SetupType), new SetupKeyComparer())
            .Select(group =>
            {
                var ordered = group.OrderByDescending(item => item.QualityScore ?? 0).ThenBy(item => item.CandidateId, StringComparer.OrdinalIgnoreCase).ToList();
                var primary = ordered.First();
                var backup = ordered.Skip(1).Select(item => item.CandidateId).ToList();
                var setupId = $"{primary.Asset.ToLowerInvariant()}_{primary.SetupType.ToLowerInvariant()}_{primary.Timeframe.ToLowerInvariant()}";
                return new SetupRegistryEntry(
                    SetupId: setupId,
                    Asset: primary.Asset,
                    PrimaryTimeframe: primary.Timeframe,
                    SetupType: primary.SetupType,
                    AllowedDirections: [primary.Direction],
                    CandidateMembers: ordered.Select(item => item.CandidateId).ToList(),
                    PrimaryCandidate: primary.CandidateId,
                    BackupCandidates: backup,
                    MarketRegimeTags: MarketRegimeTags(primary),
                    SessionTags: SessionTags(primary),
                    ConfidenceBaseline: Math.Round(ordered.Average(item => item.QualityScore ?? 0), 4),
                    AverageQualityScore: Math.Round(ordered.Average(item => item.QualityScore ?? 0), 4),
                    AverageProfitFactor: Math.Round(ordered.Average(item => item.ProfitFactor ?? 0), 4),
                    AverageWinRate: Math.Round(ordered.Average(item => item.WinRate ?? 0), 4),
                    AverageMaxDrawdownR: Math.Round(ordered.Average(item => item.MaxDrawdownR), 4),
                    AverageRiskOfRuin: Math.Round(ordered.Average(item => item.RiskOfRuin), 4),
                    ExpectedSignalFrequency: $"{Math.Max(1, ordered.Count * 4)} signals/month",
                    TradeCountRange: $"{ordered.Min(SourceTradeCount)}-{ordered.Max(SourceTradeCount)}",
                    MinimumMemberTradeCount: ordered.Min(SourceTradeCount),
                    MaximumMemberTradeCount: ordered.Max(SourceTradeCount),
                    RiskProfile: $"{primary.SetupType}:{primary.Asset}:ddr={primary.MaxDrawdownR:0.###}:ror={primary.RiskOfRuin:0.###}",
                    ReadinessStatus: ReadinessStatus(ordered),
                    HumanReviewRequired: true,
                    NoAutoTrading: true);
            })
            .OrderBy(entry => entry.Asset, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.SetupId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var registry = new SetupRegistry(
            ReportVersion: "setup_registry_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Assets: groups,
            SetupCountsByAsset: groups.GroupBy(entry => entry.Asset).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            BestSetupByAsset: groups
                .GroupBy(entry => entry.Asset, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.ConfidenceBaseline).First().SetupId, StringComparer.OrdinalIgnoreCase),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);

        Directory.CreateDirectory(Root);
        File.WriteAllText(SetupRegistryPath, JsonSerializer.Serialize(registry, JsonDefaults.WriteOptions));
        return registry;
    }

    public CertifiedCandidateInventory? LoadInventory()
    {
        var inventories = LoadInventoryCandidates();
        return inventories.Count == 0 ? null : MergeInventories(inventories);
    }

    public SetupRegistry? LoadRegistry()
    {
        var registries = LoadRegistryCandidates();
        return registries.Count == 0 ? null : MergeRegistries(registries);
    }

    public string ExplainSelection(string asset, string? timeframe)
    {
        var registry = LoadRegistry() ?? BuildRegistry();
        var normalized = NormalizeAsset(asset);
        var matches = registry.Assets.Where(entry => entry.Asset.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(timeframe))
        {
            matches = matches.Where(entry => entry.PrimaryTimeframe.Equals(timeframe.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var match = matches.OrderByDescending(entry => entry.ConfidenceBaseline).FirstOrDefault();
        if (match is null)
        {
            return $"asset={normalized}; no_setup_selected; reason=no_matching_registry_entry; no_auto_trading=true; human_review_required=true; broker_orders_enabled=false; live_trading_enabled=false; research_only=true";
        }

        return $"asset={normalized}; selected_setup={match.SetupId}; primary_candidate={match.PrimaryCandidate}; backup_candidates={string.Join(",", match.BackupCandidates)}; allowed_directions={string.Join(",", match.AllowedDirections)}; market_regime_tags={string.Join(",", match.MarketRegimeTags)}; session_tags={string.Join(",", match.SessionTags)}; confidence_baseline={match.ConfidenceBaseline:0.####}; readiness_status={match.ReadinessStatus}; no_auto_trading=true; human_review_required=true; broker_orders_enabled=false; live_trading_enabled=false; research_only=true";
    }

    private string ResolveRoot()
    {
        var preferred = Path.Combine(_storagePaths.Root, "reports", "setup_registry");
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
            var fallback = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "setup_registry");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
        catch (UnauthorizedAccessException)
        {
            var fallback = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "setup_registry");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    private static string NormalizeAsset(string asset)
        => asset.Trim().Equals("GOLD", StringComparison.OrdinalIgnoreCase) ? "XAUUSD" : asset.Trim().ToUpperInvariant();

    private IReadOnlyList<CertifiedCandidateInventory> LoadInventoryCandidates()
    {
        var candidates = new List<CertifiedCandidateInventory>();
        foreach (var path in CandidateInventoryPaths())
        {
            if (!File.Exists(path)) continue;
            var inventory = JsonSerializer.Deserialize<CertifiedCandidateInventory>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
            if (inventory is not null)
            {
                candidates.Add(inventory);
            }
        }

        return candidates;
    }

    private IReadOnlyList<SetupRegistry> LoadRegistryCandidates()
    {
        var candidates = new List<SetupRegistry>();
        foreach (var path in CandidateRegistryPaths())
        {
            if (!File.Exists(path)) continue;
            var registry = JsonSerializer.Deserialize<SetupRegistry>(File.ReadAllText(path), JsonDefaults.SnapshotReadOptions);
            if (registry is not null)
            {
                candidates.Add(registry);
            }
        }

        return candidates;
    }

    private IEnumerable<string> CandidateInventoryPaths()
    {
        yield return InventoryPath;
        yield return Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "setup_registry", "certified_candidate_inventory.json");
    }

    private IEnumerable<string> CandidateRegistryPaths()
    {
        yield return SetupRegistryPath;
        yield return Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "setup_registry", "setup_registry.json");
    }

    private static CertifiedCandidateInventory MergeInventories(IReadOnlyList<CertifiedCandidateInventory> inventories)
    {
        var items = inventories.SelectMany(inventory => inventory.Items)
            .GroupBy(item => item.CandidateId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(item => item.QualityScore ?? 0).First())
            .ToList();

        return new CertifiedCandidateInventory(
            ReportVersion: "certified_candidate_inventory_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Items: items,
            AssetsByCount: items.GroupBy(item => item.Asset).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }

    private static SetupRegistry MergeRegistries(IReadOnlyList<SetupRegistry> registries)
    {
        var entries = registries.SelectMany(registry => registry.Assets)
            .GroupBy(entry => entry.SetupId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(entry => entry.ConfidenceBaseline).First())
            .ToList();

        return new SetupRegistry(
            ReportVersion: "setup_registry_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Assets: entries,
            SetupCountsByAsset: entries.GroupBy(entry => entry.Asset).ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase),
            BestSetupByAsset: entries.GroupBy(entry => entry.Asset, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.ConfidenceBaseline).First().SetupId, StringComparer.OrdinalIgnoreCase),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }

    private static string CandidateDirection(string setupType)
        => setupType.Contains("breakout", StringComparison.OrdinalIgnoreCase) ? "long_short" : "long_short";

    private static double CandidateSignalDensity(string candidateId)
        => candidateId.Contains("ger40", StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.9;

    private static IReadOnlyList<string> MarketRegimeTags(CertifiedCandidateInventoryItem item)
        => item.SetupType.Contains("range", StringComparison.OrdinalIgnoreCase)
            ? ["range_bound", "session_liquidity"]
            : ["trend", "session_liquidity"];

    private static IReadOnlyList<string> SessionTags(CertifiedCandidateInventoryItem item)
        => ["london", "new_york", "overlap"];

    private string ReadinessStatus(IReadOnlyList<CertifiedCandidateInventoryItem> members)
    {
        var minimumTrades = members.Min(SourceTradeCount);
        var frequency = Math.Max(1, members.Count * 4);
        if (minimumTrades < 75) return "needs_more_validation";
        if (frequency < 4) return "signal_ready";
        return "bot_ready";
    }

    private int SourceTradeCount(CertifiedCandidateInventoryItem item)
    {
        var research = new ScalpingResearchService(_storagePaths, _runtimeRoot);
        var candidate = research.FindCandidate(item.CandidateId);
        if (candidate is not null && candidate.Backtest.TradeCount > 0) return candidate.Backtest.TradeCount;
        var report = JsonSerializer.Deserialize<ScalpingCertificationReport>(File.ReadAllText(item.SourceReportPath), JsonDefaults.SnapshotReadOptions);
        return report?.TotalTrades is > 0 ? (int)report.TotalTrades.Value : 0;
    }

    private sealed class SetupKeyComparer : IEqualityComparer<(string Asset, string Timeframe, string SetupType)>
    {
        public bool Equals((string Asset, string Timeframe, string SetupType) x, (string Asset, string Timeframe, string SetupType) y)
            => x.Asset.Equals(y.Asset, StringComparison.OrdinalIgnoreCase)
               && x.Timeframe.Equals(y.Timeframe, StringComparison.OrdinalIgnoreCase)
               && x.SetupType.Equals(y.SetupType, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Asset, string Timeframe, string SetupType) obj)
            => HashCode.Combine(obj.Asset.ToUpperInvariant(), obj.Timeframe.ToUpperInvariant(), obj.SetupType.ToUpperInvariant());
    }
}
