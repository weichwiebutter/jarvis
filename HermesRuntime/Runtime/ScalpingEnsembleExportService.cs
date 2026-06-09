using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ScalpingEnsemblePackageManifest(
    string PackageId,
    DateTimeOffset CreatedUtc,
    string Status,
    IReadOnlyDictionary<string, string> Files,
    IReadOnlyList<string> Members,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed record ScalpingEnsembleExportResult(
    string PackageId,
    string SignalAgentJsonPath,
    string SignalAgentMarkdownPath,
    string BotPortfolioJsonPath,
    string BotPortfolioMarkdownPath,
    string HumanReviewPackagePath,
    string ManifestPath,
    string Status,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled);

public sealed class ScalpingEnsembleExportService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ScalpingEnsembleExportService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string ExportDirectory => Path.Combine(_storagePaths.Root, "reports", "scalping_portfolio", "ensemble_export");
    public string ManifestPath => Path.Combine(ExportDirectory, "manifest.json");
    public string SignalAgentJsonPath => Path.Combine(ExportDirectory, "ensemble_signal_agent_package.json");
    public string SignalAgentMarkdownPath => Path.Combine(ExportDirectory, "ensemble_signal_agent_package.md");
    public string BotPortfolioJsonPath => Path.Combine(ExportDirectory, "ensemble_bot_portfolio_spec.json");
    public string BotPortfolioMarkdownPath => Path.Combine(ExportDirectory, "ensemble_bot_portfolio_spec.md");
    public string HumanReviewPackagePath => Path.Combine(ExportDirectory, "ensemble_human_review_package.md");

    public ScalpingEnsembleExportResult Export()
    {
        var optimizer = new ScalpingEnsembleOptimizerService(_storagePaths, _runtimeRoot);
        var report = optimizer.LoadReport() ?? optimizer.Optimize(ScalpingEnsembleOptimizationMode.balanced);
        var selection = report.SelectedEnsemble;
        if (selection.Status != ScalpingOptimizedEnsembleStatus.ensemble_ready)
        {
            throw new InvalidOperationException($"optimized_ensemble_not_ready:{selection.Status}");
        }

        var packageId = $"scalping_ensemble_{selection.Mode}_{selection.UpdatedAtUtc:yyyyMMddHHmmss}";
        var members = selection.Members.Select(member => new
        {
            member.CandidateId,
            member.Asset,
            member.SetupType,
            member.Confidence,
            member.ProfitFactor,
            member.RecoveryFactor,
            member.Drawdown,
            member.MaxDailyDrawdown,
            member.MaxWeeklyDrawdown,
            member.SignalDensityScore,
            member.ContributionReason,
            member.RiskNotes,
            SignalSpecPath = Path.Combine(_storagePaths.Root, "reports", "signal_agent_specs", member.CandidateId, "signal_agent_spec.json"),
            BotSpecPath = Path.Combine(_storagePaths.Root, "reports", "scalping_bot_specs", member.CandidateId, "ctrader_bot_spec.json"),
            CertificationReportPath = Path.Combine(_storagePaths.Root, "reports", "scalping_research", "certification", member.CandidateId, "certification_report.json")
        }).ToList();
        var signalPackage = new
        {
            package_id = packageId,
            created_utc = DateTimeOffset.UtcNow,
            status = selection.Status.ToString(),
            human_review_required = true,
            ensemble_mode = selection.Mode.ToString(),
            members,
            member_signal_specs = members.Select(member => new { member.CandidateId, member.SignalSpecPath }).ToList(),
            asset_coverage = selection.Members.Select(member => member.Asset).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            setup_coverage = selection.Members.Select(member => member.SetupType).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            session_coverage = "member_certified_sessions_only",
            confidence_aggregation_rule = "weighted_by_certification_recovery_and_signal_density",
            conflict_resolution_rule = "suppress_conflicting_same_asset_signals_require_human_review_for_rule_changes",
            signal_suppression_rules = new[] { "suppress_when_asset_daily_loss_guard_active", "suppress_when_member_spread_filter_fails", "suppress_during_news_filter_block", "suppress_if_more_than_one_correlated_signal" },
            risk_notes = new[] { "research_only", "human_review_required_before_use", "no_auto_trading", $"optimized_drawdown_r={selection.OptimizedPortfolioDrawdown:0.####}", $"risk_of_ruin={selection.RiskOfRuinEstimate:0.####}" },
            operational_limits = new[] { "no_broker_orders", "no_live_trading", "no_ctrader_order_api", "max_members=5", "human_review_required=true" },
            certification_references = members.Select(member => new { member.CandidateId, member.CertificationReportPath }).ToList(),
            source_report_paths = new[] { optimizer.OptimizerReportPath, optimizer.BalancedSelectionPath }
        };
        var botPortfolioSpec = new
        {
            portfolio_name = packageId,
            members,
            per_asset_bot_spec_references = members.Select(member => new { member.Asset, member.CandidateId, member.BotSpecPath }).ToList(),
            execution_constraints = new[] { "specification_only", "no_live_order_logic", "no_ctrader_order_api_calls", "no_auto_trading_activation" },
            max_trades_per_asset = 6,
            max_portfolio_daily_loss = 0.015,
            max_correlated_signals = 1,
            kill_switch_rules = new[] { "portfolio_daily_loss_guard", "member_drawdown_guard", "news_filter_block", "spread_filter_block", "manual_human_reenable_required" },
            logging_requirements = new[] { "log_member_signal_state", "log_conflict_resolution", "log_suppression_reason", "log_portfolio_risk_state", "log_no_order_execution_confirmation" },
            safety_requirements = new[] { "no_broker_credentials", "no_live_order_execution", "no_ctrader_order_api_calls", "human_review_required=true", "no_auto_trading=true" }
        };
        var manifest = new ScalpingEnsemblePackageManifest(
            PackageId: packageId,
            CreatedUtc: DateTimeOffset.UtcNow,
            Status: selection.Status.ToString(),
            Files: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["ensemble_signal_agent_package_json"] = SignalAgentJsonPath,
                ["ensemble_signal_agent_package_md"] = SignalAgentMarkdownPath,
                ["ensemble_bot_portfolio_spec_json"] = BotPortfolioJsonPath,
                ["ensemble_bot_portfolio_spec_md"] = BotPortfolioMarkdownPath,
                ["ensemble_human_review_package_md"] = HumanReviewPackagePath,
                ["manifest_json"] = ManifestPath
            },
            Members: selection.Members.Select(member => member.CandidateId).ToList(),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);

        Directory.CreateDirectory(ExportDirectory);
        File.WriteAllText(SignalAgentJsonPath, JsonSerializer.Serialize(signalPackage, JsonDefaults.WriteOptions));
        File.WriteAllText(SignalAgentMarkdownPath, BuildSignalMarkdown(packageId, selection));
        File.WriteAllText(BotPortfolioJsonPath, JsonSerializer.Serialize(botPortfolioSpec, JsonDefaults.WriteOptions));
        File.WriteAllText(BotPortfolioMarkdownPath, BuildBotMarkdown(packageId, selection));
        File.WriteAllText(HumanReviewPackagePath, BuildHumanReviewMarkdown(packageId, selection));
        File.WriteAllText(ManifestPath, JsonSerializer.Serialize(manifest, JsonDefaults.WriteOptions));

        return new ScalpingEnsembleExportResult(
            PackageId: packageId,
            SignalAgentJsonPath: SignalAgentJsonPath,
            SignalAgentMarkdownPath: SignalAgentMarkdownPath,
            BotPortfolioJsonPath: BotPortfolioJsonPath,
            BotPortfolioMarkdownPath: BotPortfolioMarkdownPath,
            HumanReviewPackagePath: HumanReviewPackagePath,
            ManifestPath: ManifestPath,
            Status: selection.Status.ToString(),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false);
    }

    public ScalpingEnsemblePackageManifest? LoadManifest()
    {
        return File.Exists(ManifestPath)
            ? JsonSerializer.Deserialize<ScalpingEnsemblePackageManifest>(File.ReadAllText(ManifestPath), JsonDefaults.SnapshotReadOptions)
            : null;
    }

    private static string BuildSignalMarkdown(string packageId, ScalpingOptimizedEnsembleSelection selection) => $"""
# Ensemble Signal-Agent Package

- package_id: {packageId}
- status: {selection.Status}
- ensemble_mode: {selection.Mode}
- human_review_required: true
- no_auto_trading: true
- broker_orders_enabled: false
- live_trading_enabled: false

## Members
{MemberBullets(selection)}

## Aggregation
- confidence_aggregation_rule: weighted_by_certification_recovery_and_signal_density
- conflict_resolution_rule: suppress_conflicting_same_asset_signals_require_human_review_for_rule_changes
- signal_suppression_rules: spread_filter, news_filter, daily_loss_guard, correlated_signal_guard

## Risk Notes
- optimized_drawdown_r: {selection.OptimizedPortfolioDrawdown:0.####}
- risk_of_ruin: {selection.RiskOfRuinEstimate:0.####}
- stability: {selection.EnsembleStability:0.####}
- research_only_export_package
""";

    private static string BuildBotMarkdown(string packageId, ScalpingOptimizedEnsembleSelection selection) => $"""
# Ensemble Bot Portfolio Spec

- portfolio_name: {packageId}
- status: {selection.Status}
- human_review_required: true
- no_auto_trading: true
- no_ctrader_order_api: true

## Members
{MemberBullets(selection)}

## Execution Constraints
- specification_only
- no_live_order_logic
- no_ctrader_order_api_calls
- no_auto_trading_activation

## Portfolio Risk Limits
- max_trades_per_asset: 6
- max_portfolio_daily_loss: 0.015
- max_correlated_signals: 1

## Kill Switch Rules
- portfolio_daily_loss_guard
- member_drawdown_guard
- news_filter_block
- spread_filter_block
- manual_human_reenable_required

## Logging Requirements
- log_member_signal_state
- log_conflict_resolution
- log_suppression_reason
- log_portfolio_risk_state
- log_no_order_execution_confirmation
""";

    private static string BuildHumanReviewMarkdown(string packageId, ScalpingOptimizedEnsembleSelection selection) => $"""
# Ensemble Human Review Package

## Summary
- package_id: {packageId}
- status: {selection.Status}
- mode: {selection.Mode}
- members: {selection.Members.Count}
- readiness: {selection.Readiness}
- human_review_required: true

## Members
{MemberBullets(selection)}

## Why These Members
- Selected by optimizer from certified candidates only.
- Combines EURUSD and XAUUSD for asset diversity.
- Combines micro_trend_continuation and range_breakout for setup diversity.
- Reduces portfolio drawdown versus combining all certified candidates.

## Metrics
- drawdown_before: {selection.PreviousPortfolioDrawdown:0.####}R
- drawdown_after: {selection.OptimizedPortfolioDrawdown:0.####}R
- signal_density_before: {selection.PreviousSignalDensity:0.####}
- signal_density_after: {selection.OptimizedSignalDensity:0.####}
- risk_of_ruin: {selection.RiskOfRuinEstimate:0.####}
- stability: {selection.EnsembleStability:0.####}

## Open Risks
- live execution is not approved
- signal conflict rules require human review
- asset-specific market regimes can change
- export package is specification-only

## Recommendation
Approve only for further offline review/specification work. Do not enable live trading.

## Human Approval Checklist
- [ ] Confirm all member certification reports
- [ ] Confirm signal-agent package content
- [ ] Confirm bot portfolio spec has no order API calls
- [ ] Confirm risk limits and kill switches
- [ ] Confirm no broker credentials or secrets
- [ ] Confirm human_review_required remains true
""";

    private static string MemberBullets(ScalpingOptimizedEnsembleSelection selection) => string.Join(Environment.NewLine, selection.Members.Select(member => $"- {member.CandidateId}: {member.Asset}/{member.SetupType}, pf={member.ProfitFactor:0.####}, recovery={member.RecoveryFactor:0.####}, drawdown={member.Drawdown:0.####}, contribution={member.ContributionReason}"));
}
