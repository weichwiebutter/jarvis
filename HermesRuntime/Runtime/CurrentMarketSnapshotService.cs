using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record CurrentMarketAssetSnapshot(
    string Asset,
    double? Bid,
    double? Ask,
    double? Mid,
    double? Spread,
    DateTimeOffset? TimestampUtc,
    string Source,
    bool IsLiveReadonly,
    bool IsPlaceholder,
    double? AgeSeconds,
    string Status);

public sealed record CurrentMarketStatusSnapshot(
    string SnapshotVersion,
    DateTimeOffset UpdatedAtUtc,
    string SnapshotStatus,
    IReadOnlyList<string> AssetsRequested,
    IReadOnlyList<string> AssetsAvailable,
    string SnapshotHealth,
    DateTimeOffset? LatestUpdateUtc,
    IReadOnlyList<string> Warnings,
    string SnapshotJsonPath,
    string SnapshotMarkdownPath,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed class CurrentMarketSnapshotService
{
    private static readonly string[] SupportedAssets = ["EURUSD", "XAUUSD"];

    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public CurrentMarketSnapshotService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "current_market");
    public string SnapshotJsonPath => Path.Combine(Root, "current_market_snapshot.json");
    public string SnapshotMarkdownPath => Path.Combine(Root, "current_market_snapshot.md");
    public string StatusPath => Path.Combine(Root, "current_market_status.json");

    public CurrentMarketStatusSnapshot LoadOrCreateStatus()
    {
        if (File.Exists(StatusPath))
        {
            var snapshot = JsonSerializer.Deserialize<CurrentMarketStatusSnapshot>(File.ReadAllText(StatusPath), JsonDefaults.SnapshotReadOptions);
            if (snapshot is not null)
            {
                return snapshot;
            }
        }

        return UpdateSnapshot();
    }

    public CurrentMarketStatusSnapshot UpdateSnapshot()
    {
        var warnings = new List<string>();
        var snapshots = SupportedAssets.Select(asset => BuildAssetSnapshot(asset, warnings)).ToList();
        var availableAssets = snapshots
            .Where(snapshot => snapshot.Status == "available" && snapshot.IsLiveReadonly && !snapshot.IsPlaceholder)
            .Select(snapshot => snapshot.Asset)
            .ToList();
        DateTimeOffset? latestUpdateUtc = snapshots
            .Where(snapshot => snapshot.TimestampUtc.HasValue)
            .Select(snapshot => snapshot.TimestampUtc)
            .OrderByDescending(snapshot => snapshot)
            .FirstOrDefault();

        var health = availableAssets.Count > 0
            ? "ok"
            : snapshots.Any(snapshot => snapshot.Status == "placeholder_only")
                ? "placeholder_only"
                : "unavailable";
        var snapshotStatus = availableAssets.Count > 0
            ? "available"
            : snapshots.Any(snapshot => snapshot.Status == "placeholder_only")
                ? "placeholder_only"
                : "unavailable";

        var status = new CurrentMarketStatusSnapshot(
            SnapshotVersion: "current_market_snapshot_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            SnapshotStatus: snapshotStatus,
            AssetsRequested: SupportedAssets,
            AssetsAvailable: availableAssets,
            SnapshotHealth: health,
            LatestUpdateUtc: latestUpdateUtc,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            SnapshotJsonPath: SnapshotJsonPath,
            SnapshotMarkdownPath: SnapshotMarkdownPath,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);

        Directory.CreateDirectory(Root);
        File.WriteAllText(SnapshotJsonPath, JsonSerializer.Serialize(snapshots, JsonDefaults.WriteOptions));
        File.WriteAllText(SnapshotMarkdownPath, BuildSnapshotMarkdown(status, snapshots));
        File.WriteAllText(StatusPath, JsonSerializer.Serialize(status, JsonDefaults.WriteOptions));
        return status;
    }

    public IReadOnlyList<CurrentMarketAssetSnapshot> LoadSnapshot()
    {
        if (!File.Exists(SnapshotJsonPath))
        {
            UpdateSnapshot();
        }

        var snapshots = JsonSerializer.Deserialize<List<CurrentMarketAssetSnapshot>>(File.ReadAllText(SnapshotJsonPath), JsonDefaults.SnapshotReadOptions);
        return snapshots ?? [];
    }

    public CurrentMarketAssetSnapshot? FindSnapshot(string asset)
    {
        return LoadSnapshot().FirstOrDefault(snapshot => snapshot.Asset.Equals(asset, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<string> ExplainGap(string asset)
    {
        var normalizedAsset = string.IsNullOrWhiteSpace(asset) ? "XAUUSD" : asset.Trim().ToUpperInvariant();
        var reasons = new List<string>();
        var status = LoadOrCreateStatus();
        var snapshot = LoadSnapshot().FirstOrDefault(item => item.Asset.Equals(normalizedAsset, StringComparison.OrdinalIgnoreCase));
        var tokenStore = new CTraderTokenStore(_storagePaths);
        var token = tokenStore.LoadToken();

        reasons.Add($"asset={normalizedAsset}");
        reasons.Add($"snapshot_status={status.SnapshotStatus}");
        reasons.Add($"snapshot_health={status.SnapshotHealth}");
        reasons.Add($"assets_available={(status.AssetsAvailable.Count == 0 ? "none" : string.Join(",", status.AssetsAvailable))}");
        reasons.Add($"token_store_exists={File.Exists(tokenStore.TokenStorePath).ToString().ToLowerInvariant()}");
        reasons.Add($"token_loaded={(token?.HasAccessToken ?? false).ToString().ToLowerInvariant()}");
        foreach (var path in CandidateQuotePaths(normalizedAsset))
        {
            reasons.Add($"candidate_quote_path={path}:exists={File.Exists(path).ToString().ToLowerInvariant()}");
        }

        if (snapshot is null)
        {
            reasons.Add("market_snapshot_unavailable:no_snapshot_object");
        }
        else
        {
            reasons.Add($"asset_status={snapshot.Status}");
            reasons.Add($"source={snapshot.Source}");
            reasons.Add($"is_live_readonly={snapshot.IsLiveReadonly.ToString().ToLowerInvariant()}");
            reasons.Add($"is_placeholder={snapshot.IsPlaceholder.ToString().ToLowerInvariant()}");
            reasons.Add($"age_seconds={(snapshot.AgeSeconds.HasValue ? $"{snapshot.AgeSeconds.Value:0.##}" : "n/a")}");
            if (snapshot.Status != "available")
            {
                reasons.Add($"market_snapshot_unavailable:{snapshot.Status}");
            }
        }

        return reasons;
    }

    private CurrentMarketAssetSnapshot BuildAssetSnapshot(string asset, List<string> warnings)
    {
        if (TryLoadLiveReadonlyQuote(asset, out var snapshot, out var liveWarning))
        {
            if (!string.IsNullOrWhiteSpace(liveWarning))
            {
                warnings.Add(liveWarning);
            }

            return snapshot;
        }

        var placeholder = TryBuildPlaceholderFromLatestCandle(asset);
        if (placeholder is not null)
        {
            warnings.Add($"market_snapshot_placeholder_only:{asset}");
            return placeholder;
        }

        warnings.Add($"market_snapshot_unavailable:{asset}");
        return new CurrentMarketAssetSnapshot(
            Asset: asset,
            Bid: null,
            Ask: null,
            Mid: null,
            Spread: null,
            TimestampUtc: null,
            Source: "market_snapshot_unavailable",
            IsLiveReadonly: false,
            IsPlaceholder: false,
            AgeSeconds: null,
            Status: "unavailable");
    }

    private bool TryLoadLiveReadonlyQuote(string asset, out CurrentMarketAssetSnapshot snapshot, out string? warning)
    {
        foreach (var path in CandidateQuotePaths(asset))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var root = document.RootElement;
                var quoteRoot = root.ValueKind == JsonValueKind.Object && TryGetObject(root, out var nestedQuote, "quote", "snapshot", asset)
                    ? nestedQuote
                    : root;

                var bid = ReadDouble(quoteRoot, "bid", "bid_price", "bidPrice");
                var ask = ReadDouble(quoteRoot, "ask", "ask_price", "askPrice");
                var timestamp = ReadDateTimeOffset(quoteRoot, "timestamp_utc", "timestampUtc", "updated_at_utc", "updatedAtUtc", "time", "timestamp");
                if (!bid.HasValue || !ask.HasValue || !timestamp.HasValue)
                {
                    continue;
                }

                var mid = Math.Round((bid.Value + ask.Value) / 2.0, PriceDecimals(asset));
                var spread = Math.Round(ask.Value - bid.Value, PriceDecimals(asset));
                var age = Math.Max(0, (DateTimeOffset.UtcNow - timestamp.Value).TotalSeconds);
                var status = age <= 120 ? "available" : age <= 900 ? "stale" : "unavailable";
                warning = status == "stale" ? $"current_market_snapshot_stale:{asset}:{timestamp.Value:O}" : null;
                snapshot = new CurrentMarketAssetSnapshot(
                    Asset: asset,
                    Bid: Math.Round(bid.Value, PriceDecimals(asset)),
                    Ask: Math.Round(ask.Value, PriceDecimals(asset)),
                    Mid: mid,
                    Spread: spread,
                    TimestampUtc: timestamp.Value,
                    Source: ReadString(quoteRoot, "source", "provider") ?? $"local_readonly_quote:{Path.GetFileName(path)}",
                    IsLiveReadonly: true,
                    IsPlaceholder: false,
                    AgeSeconds: Math.Round(age, 2),
                    Status: status);
                return true;
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                warning = $"current_market_quote_unreadable:{asset}:{Path.GetFileName(path)}";
                snapshot = default!;
                return false;
            }
        }

        snapshot = default!;
        warning = null;
        return false;
    }

    private CurrentMarketAssetSnapshot? TryBuildPlaceholderFromLatestCandle(string asset)
    {
        var candle = LoadLatestCandle(asset, "M5");
        if (candle is null)
        {
            return null;
        }

        var age = Math.Max(0, (DateTimeOffset.UtcNow - candle.TimestampUtc).TotalSeconds);
        return new CurrentMarketAssetSnapshot(
            Asset: asset,
            Bid: null,
            Ask: null,
            Mid: RoundPrice(asset, candle.Close),
            Spread: null,
            TimestampUtc: candle.TimestampUtc,
            Source: $"placeholder_latest_candle_close:{candle.Timeframe}",
            IsLiveReadonly: false,
            IsPlaceholder: true,
            AgeSeconds: Math.Round(age, 2),
            Status: "placeholder_only");
    }

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

        var line = File.ReadLines(latestFile).Reverse().FirstOrDefault(item => !string.IsNullOrWhiteSpace(item));
        return string.IsNullOrWhiteSpace(line)
            ? null
            : JsonSerializer.Deserialize<MarketDataCandle>(line, JsonDefaults.SnapshotReadOptions);
    }

    private IEnumerable<string> CandidateQuotePaths(string asset)
    {
        yield return Path.Combine(_storagePaths.Root, "market_data", "quotes", $"{asset}.json");
        yield return Path.Combine(_storagePaths.Root, "market_data", "quotes", asset, "latest.json");
        yield return Path.Combine(_storagePaths.Root, "reports", "readonly_quotes", $"{asset}.json");
        yield return Path.Combine(_storagePaths.Root, "reports", "current_market", "raw", $"{asset}.json");
        yield return Path.Combine(_storagePaths.Root, "snapshots", "market", $"{asset}.json");
        yield return Path.Combine(_storagePaths.Root, "snapshots", $"{asset}.json");
    }

    private static string BuildSnapshotMarkdown(CurrentMarketStatusSnapshot status, IReadOnlyList<CurrentMarketAssetSnapshot> snapshots)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Current Market Snapshot");
        builder.AppendLine();
        builder.AppendLine($"- snapshot_status: {status.SnapshotStatus}");
        builder.AppendLine($"- snapshot_health: {status.SnapshotHealth}");
        builder.AppendLine($"- assets_available: {(status.AssetsAvailable.Count == 0 ? "none" : string.Join(", ", status.AssetsAvailable))}");
        builder.AppendLine($"- latest_update_utc: {(status.LatestUpdateUtc?.ToString("O") ?? "n/a")}");
        builder.AppendLine("- Read-only market snapshot");
        builder.AppendLine("- No orders");
        builder.AppendLine("- No broker action");
        builder.AppendLine("- No cTrader Order API for trading");
        builder.AppendLine("- no_auto_trading=true");
        builder.AppendLine("- human_review_required=true");
        builder.AppendLine();

        foreach (var snapshot in snapshots)
        {
            builder.AppendLine($"## {snapshot.Asset}");
            builder.AppendLine($"- status: {snapshot.Status}");
            builder.AppendLine($"- source: {snapshot.Source}");
            builder.AppendLine($"- bid: {(snapshot.Bid.HasValue ? snapshot.Bid.Value.ToString("0.#####") : "n/a")}");
            builder.AppendLine($"- ask: {(snapshot.Ask.HasValue ? snapshot.Ask.Value.ToString("0.#####") : "n/a")}");
            builder.AppendLine($"- mid: {(snapshot.Mid.HasValue ? snapshot.Mid.Value.ToString("0.#####") : "n/a")}");
            builder.AppendLine($"- spread: {(snapshot.Spread.HasValue ? snapshot.Spread.Value.ToString("0.#####") : "n/a")}");
            builder.AppendLine($"- timestamp_utc: {(snapshot.TimestampUtc?.ToString("O") ?? "n/a")}");
            builder.AppendLine($"- age_seconds: {(snapshot.AgeSeconds.HasValue ? snapshot.AgeSeconds.Value.ToString("0.##") : "n/a")}");
            builder.AppendLine($"- is_live_readonly: {snapshot.IsLiveReadonly}");
            builder.AppendLine($"- is_placeholder: {snapshot.IsPlaceholder}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static bool TryGetObject(JsonElement root, out JsonElement value, params string[] names)
    {
        value = default;
        foreach (var property in root.EnumerateObject())
        {
            if (names.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                && property.Value.ValueKind == JsonValueKind.Object)
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (names.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                && property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString();
            }
        }

        return null;
    }

    private static double? ReadDouble(JsonElement root, params string[] names)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!names.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetDouble(out var number))
            {
                return number;
            }

            if (property.Value.ValueKind == JsonValueKind.String
                && double.TryParse(property.Value.GetString(), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static DateTimeOffset? ReadDateTimeOffset(JsonElement root, params string[] names)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (!names.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (property.Value.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(property.Value.GetString(), out var parsed))
            {
                return parsed.ToUniversalTime();
            }
        }

        return null;
    }

    private static int PriceDecimals(string asset) => asset.Equals("EURUSD", StringComparison.OrdinalIgnoreCase) ? 5 : 2;

    private static double RoundPrice(string asset, double value) => Math.Round(value, PriceDecimals(asset));
}
