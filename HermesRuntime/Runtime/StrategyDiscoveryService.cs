using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hermes.Runtime;

public sealed class StrategyDiscoveryService
{
    private readonly StoragePaths _storagePaths;

    public StrategyDiscoveryService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string DiscoveryRoot => Path.Combine(_storagePaths.Root, "strategy_discovery");

    public string TrustedSourcesPath => Path.Combine(DiscoveryRoot, "trusted_sources.json");

    public string DiscoveryStatusPath => Path.Combine(DiscoveryRoot, "discovery_status.json");

    public StrategyDiscoveryReport Run()
    {
        Directory.CreateDirectory(DiscoveryRoot);
        var sources = TrustedSources();
        File.WriteAllText(TrustedSourcesPath, JsonSerializer.Serialize(sources, JsonDefaults.WriteOptions));

        var findings = new List<StrategyDiscoveryFinding>();
        var warnings = new List<string>();
        foreach (var source in sources)
        {
            var sourceFindings = AnalyzeSource(source).ToList();
            if (sourceFindings.Count == 0)
            {
                findings.Add(new StrategyDiscoveryFinding(
                    FindingId: $"finding_{source.SourceId}_metadata",
                    SourceId: source.SourceId,
                    SourceUrl: source.SourceUrl,
                    LocalFile: null,
                    IndicatorsUsed: [],
                    EntryLogicHints: ["metadata_only_no_local_cs_snapshot"],
                    ExitLogicHints: [],
                    RiskLogicHints: [],
                    RiskFlags: []));
                warnings.Add($"No local .cs snapshot found for {source.SourceId}; metadata only.");
            }
            else
            {
                findings.AddRange(sourceFindings);
            }
        }

        var report = new StrategyDiscoveryReport(
            ReportId: $"strategy_discovery_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            CreatedAtUtc: DateTimeOffset.UtcNow,
            TrustedSources: sources,
            SourcesWhitelisted: sources.Count(source => source.Whitelisted),
            LocalCsFilesAnalyzed: findings.Count(finding => finding.LocalFile is not null),
            StrategiesAnalyzed: findings.Count,
            RiskFlagsDetected: findings.Sum(finding => finding.RiskFlags.Count),
            Findings: findings,
            Warnings: warnings.Distinct(StringComparer.Ordinal).ToList(),
            NoForeignCodeExecuted: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(DiscoveryStatusPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    public StrategyDiscoveryReport? LoadReport()
    {
        if (!File.Exists(DiscoveryStatusPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StrategyDiscoveryReport>(
                File.ReadAllText(DiscoveryStatusPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private IEnumerable<StrategyDiscoveryFinding> AnalyzeSource(TrustedStrategySource source)
    {
        if (!Directory.Exists(source.LocalSnapshotPath))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(source.LocalSnapshotPath, "*.cs", SearchOption.AllDirectories)
                     .OrderBy(path => path)
                     .Take(500))
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (IOException)
            {
                continue;
            }

            yield return AnalyzeFile(source, file, text);
        }
    }

    private static StrategyDiscoveryFinding AnalyzeFile(
        TrustedStrategySource source,
        string file,
        string text)
    {
        var indicators = FindIndicators(text);
        var entryHints = FindHints(text, ["ExecuteMarketOrder", "PlaceLimitOrder", "Buy", "Sell", "cross", "CrossAbove", "CrossBelow", "LastValue"]);
        var exitHints = FindHints(text, ["StopLoss", "TakeProfit", "ClosePosition", "ModifyPosition", "TrailingStop"]);
        var riskHints = FindHints(text, ["Volume", "Risk", "Lots", "Position", "StopLoss", "TakeProfit"]);
        var riskFlags = new List<string>();
        if (Contains(text, "martingale"))
        {
            riskFlags.Add("martingale_detected");
        }

        if (Contains(text, "grid"))
        {
            riskFlags.Add("grid_detected");
        }

        if (Regex.IsMatch(text, "averag(e|ing).*down", RegexOptions.IgnoreCase))
        {
            riskFlags.Add("averaging_down_detected");
        }

        if (Regex.IsMatch(text, "Volume(InUnits)?\\s*[\\*+]=|Lots\\s*[\\*+]=", RegexOptions.IgnoreCase))
        {
            riskFlags.Add("position_size_escalation_hint");
        }

        return new StrategyDiscoveryFinding(
            FindingId: $"finding_{Path.GetFileNameWithoutExtension(file)}_{ShortHash(file)}",
            SourceId: source.SourceId,
            SourceUrl: source.SourceUrl,
            LocalFile: file,
            IndicatorsUsed: indicators,
            EntryLogicHints: entryHints,
            ExitLogicHints: exitHints,
            RiskLogicHints: riskHints,
            RiskFlags: riskFlags);
    }

    private IReadOnlyList<TrustedStrategySource> TrustedSources()
    {
        var snapshotRoot = Path.Combine(DiscoveryRoot, "snapshots");
        return
        [
            Source("spotware_ctrader_algo_samples", "https://github.com/spotware/ctrader-algo-samples", "Spotware cTrader Algo Samples", "github_repository", Path.Combine(snapshotRoot, "spotware_ctrader_algo_samples")),
            Source("spotware_github", "https://github.com/spotware", "Spotware GitHub", "github_org", Path.Combine(snapshotRoot, "spotware")),
            Source("clickalgo_github", "https://clickalgo.com/github", "ClickAlgo GitHub Directory", "curated_directory", Path.Combine(snapshotRoot, "clickalgo"))
        ];
    }

    private static TrustedStrategySource Source(
        string id,
        string url,
        string name,
        string type,
        string snapshotPath) =>
        new(id, url, name, type, Whitelisted: true, CodeExecutionAllowed: false, LocalSnapshotPath: snapshotPath);

    private static IReadOnlyList<string> FindIndicators(string text)
    {
        var known = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ExponentialMovingAverage|EMA"] = "EMA",
            ["SimpleMovingAverage|SMA"] = "SMA",
            ["RelativeStrengthIndex|RSI"] = "RSI",
            ["Macd|MACD"] = "MACD",
            ["AverageTrueRange|ATR"] = "ATR",
            ["Bollinger"] = "BollingerBands",
            ["Stochastic"] = "Stochastic"
        };

        return known
            .Where(item => item.Key.Split('|').Any(token => Contains(text, token)))
            .Select(item => item.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> FindHints(string text, IReadOnlyList<string> tokens)
    {
        return tokens
            .Where(token => Contains(text, token))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();
    }

    private static bool Contains(string text, string token) =>
        text.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static string ShortHash(string value)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..10].ToLowerInvariant();
    }
}
