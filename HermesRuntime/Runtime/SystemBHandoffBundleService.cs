using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record SystemBHandoffBundleManifest(
    DateTimeOffset GeneratedAtUtc,
    string BundleVersion,
    string SourceSystem,
    int FileCount,
    IReadOnlyList<string> IncludedFiles,
    IReadOnlyDictionary<string, string> Hashes,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled,
    bool ResearchOnly);

public sealed class SystemBHandoffBundleService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public SystemBHandoffBundleService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => ResolveRoot();
    public string BundleDirectory => Path.Combine(Root, "system_b_handoff_bundle");
    public string READMEPath => Path.Combine(BundleDirectory, "README.md");
    public string PackagePath => Path.Combine(BundleDirectory, "ensemble_signal_agent_package.json");
    public string SchemaPath => Path.Combine(BundleDirectory, "ensemble_signal_agent_package.schema.json");
    public string ContractPath => Path.Combine(BundleDirectory, "system_b_signal_agent_export_contract.md");
    public string SummaryJsonPath => Path.Combine(BundleDirectory, "portfolio_summary.json");
    public string SummaryMarkdownPath => Path.Combine(BundleDirectory, "portfolio_summary.md");
    public string ManifestPath => Path.Combine(BundleDirectory, "bundle-manifest.json");

    public SystemBHandoffBundleManifest Export()
    {
        var portfolioService = new ScalpingEnsemblePortfolioService(_storagePaths, _runtimeRoot);
        var portfolio = portfolioService.Load() ?? portfolioService.Build();
        var package = portfolioService.LoadPackage() ?? portfolioService.Export();

        Directory.CreateDirectory(BundleDirectory);

        var files = new List<string>();
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        WriteText(READMEPath, BuildReadme(portfolio, package));
        files.Add(Path.GetFileName(READMEPath));
        hashes["README.md"] = Hash(READMEPath);

        CopyIfExists(packageFile: portfolioService.PackagePath, targetPath: PackagePath, files, hashes);
        CopyIfExists(Path.Combine(_runtimeRoot, "docs", "ensemble_signal_agent_package.schema.json"), SchemaPath, files, hashes);
        CopyIfExists(Path.Combine(_runtimeRoot, "docs", "system_b_signal_agent_export_contract.md"), ContractPath, files, hashes);

        WriteText(SummaryJsonPath, BuildSummaryJson(portfolio));
        files.Add(Path.GetFileName(SummaryJsonPath));
        hashes["portfolio_summary.json"] = Hash(SummaryJsonPath);

        WriteText(SummaryMarkdownPath, BuildSummaryMarkdown(portfolio));
        files.Add(Path.GetFileName(SummaryMarkdownPath));
        hashes["portfolio_summary.md"] = Hash(SummaryMarkdownPath);

        var manifest = new SystemBHandoffBundleManifest(
            GeneratedAtUtc: DateTimeOffset.UtcNow,
            BundleVersion: "system_b_handoff_bundle_v1",
            SourceSystem: "HermesRuntime/SystemA",
            FileCount: files.Count + 1,
            IncludedFiles: files,
            Hashes: hashes,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false,
            ResearchOnly: true);
        WriteText(ManifestPath, JsonSerializer.Serialize(manifest, JsonDefaults.WriteOptions));
        files.Add(Path.GetFileName(ManifestPath));

        return manifest;
    }

    public string ResolveBundlePath() => BundleDirectory;

    public string? ValidateSafety()
    {
        var package = new ScalpingEnsemblePortfolioService(_storagePaths, _runtimeRoot).LoadPackage();
        if (package is null) return "package_missing";
        if (!package.NoAutoTrading || !package.HumanReviewRequired || package.BrokerOrdersEnabled || package.LiveTradingEnabled || !package.ResearchOnly)
        {
            return "safety_flags_invalid";
        }

        return null;
    }

    private string ResolveRoot()
    {
        var preferred = Path.Combine(_storagePaths.Root, "reports", "system_b_handoff");
        try
        {
            Directory.CreateDirectory(preferred);
            return preferred;
        }
        catch
        {
            var fallback = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "system_b_handoff");
            Directory.CreateDirectory(fallback);
            return fallback;
        }
    }

    private static void CopyIfExists(string packageFile, string targetPath, ICollection<string> files, IDictionary<string, string> hashes)
    {
        if (!File.Exists(packageFile)) return;
        File.Copy(packageFile, targetPath, true);
        files.Add(Path.GetFileName(targetPath));
        hashes[Path.GetFileName(targetPath)] = Hash(targetPath);
    }

    private static void WriteText(string path, string content)
    {
        File.WriteAllText(path, content, Encoding.UTF8);
    }

    private static string Hash(string path)
    {
        using var sha = SHA256.Create();
        var bytes = File.ReadAllBytes(path);
        return Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant();
    }

    private static string BuildReadme(ScalpingEnsemblePortfolioReport portfolio, EnsembleSignalAgentPortfolioPackage package) => $"""
# System B Handoff Bundle

## Zweck
Übergabe von freigegebenen Scalping- und Signal-Artefakten an System B / Nous Hermes Agent.

## Dateien
- `ensemble_signal_agent_package.json`
- `ensemble_signal_agent_package.schema.json`
- `system_b_signal_agent_export_contract.md`
- `portfolio_summary.json`
- `portfolio_summary.md`
- `bundle-manifest.json`

## Aktueller Status
- Portfolio Readiness: {portfolio.PortfolioReadiness}
- Assets: {string.Join(", ", portfolio.Assets)}
- Package Status: {package.Status}
- Safety: no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true

## Import-Anleitung für System B
1. `ensemble_signal_agent_package.json` validieren.
2. Nur Assets mit `portfolio_ready`, `signal_ready`, `setup_ready` oder `bot_ready` anzeigen.
3. Keine Order-Buttons darstellen.
4. `needs_more_validation`, `data_ready_only`, `missing_data`, `quote_mapping_pending` nur als Warnung anzeigen.

## Bekannte Einschränkungen
- EURUSD bleibt aktuell `needs_more_validation`.
- Quote-Mapping kann weiterhin `quote_mapping_pending` sein.
- System B ist Anzeige- und Review-System, kein Ausführungssystem.
""";

    private static string BuildSummaryJson(ScalpingEnsemblePortfolioReport portfolio)
    {
        var summary = new
        {
            assets = portfolio.Entries.Select(entry => new
            {
                asset = entry.Asset,
                readiness = entry.Readiness,
                primary_setup = entry.PrimarySetup,
                backup_setups = entry.BackupSetups,
                candidate_count = entry.CertifiedCandidateCount,
                signal_spec_count = entry.SignalSpecCount,
                portfolio_status = entry.PortfolioReadiness,
                safety_flags = entry.SafetyFlags
            }).ToList(),
            portfolio_status = portfolio.PortfolioReadiness,
            safety_flags = new[] { "no_auto_trading=true", "human_review_required=true", "broker_orders_enabled=false", "live_trading_enabled=false", "research_only=true" }
        };

        return JsonSerializer.Serialize(summary, JsonDefaults.WriteOptions);
    }

    private static string BuildSummaryMarkdown(ScalpingEnsemblePortfolioReport portfolio) => $"""
# Portfolio Summary

- Portfolio Status: {portfolio.PortfolioReadiness}
- Assets: {string.Join(", ", portfolio.Assets)}

## Asset Overview
{string.Join(Environment.NewLine, portfolio.Entries.Select(entry => $"- {entry.Asset}: readiness={entry.Readiness}, primary_setup={entry.PrimarySetup}, backups={string.Join(",", entry.BackupSetups)}, candidates={entry.CertifiedCandidateCount}, signal_specs={entry.SignalSpecCount}"))}
""";
}
