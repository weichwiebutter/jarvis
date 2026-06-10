using System.Text.Json;

namespace Hermes.Runtime;

public sealed record CertifiedCandidateAuditFinding(
    string CandidateId,
    string Asset,
    string Timeframe,
    string SetupType,
    string Direction,
    string CertificationStatus,
    string UsedIndicators,
    string UsedFilters,
    string SessionFilter,
    string MarketRegimeFilter,
    string EntryRules,
    string ExitRules,
    string StopLossLogic,
    string TakeProfitLogic,
    string InvalidationRules,
    string TradesTotal,
    string TradesPerYear,
    string TradesPerMonth,
    string TradesPerWeek,
    string AverageHoldingDuration,
    string WinRate,
    string ProfitFactor,
    string Expectancy,
    string Sharpe,
    string Sortino,
    string MaxDrawdown,
    string MaxDailyDrawdown,
    string RiskOfRuin,
    string SignalDensity,
    string WalkForwardStatus,
    string OosStatus,
    string MonteCarloStatus,
    string SensitivityStatus,
    string SpreadStressStatus,
    string SlippageStressStatus,
    string CertificationReason,
    IReadOnlyList<string> MinimumThresholdsMet,
    IReadOnlyList<string> Weaknesses,
    IReadOnlyList<string> AuditWarnings,
    string SourceCertificationPath,
    string SourceExpansionPath,
    bool HumanReviewRequired);

public sealed record CertifiedCandidateAuditReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Asset,
    IReadOnlyList<CertifiedCandidateAuditFinding> Findings,
    IReadOnlyList<string> AuditWarnings,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    bool BrokerOrdersEnabled,
    bool LiveTradingEnabled,
    bool ResearchOnly);

public sealed class CertifiedCandidateAuditService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedRoot;

    public CertifiedCandidateAuditService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => _resolvedRoot ??= ResolveRoot();
    public string ReportPath => Path.Combine(Root, "ger40_candidate_audit_report.json");
    public string MarkdownPath => Path.Combine(Root, "ger40_candidate_audit_report.md");

    public CertifiedCandidateAuditReport BuildReport(string asset = "GER40")
    {
        var normalizedAsset = asset.Trim().ToUpperInvariant();
        var certService = new ScalpingCertificationService(_storagePaths, _runtimeRoot);
        var researchService = new ScalpingResearchService(_storagePaths, _runtimeRoot);
        var expansionService = new ScalpingRobustnessExpansionService(_storagePaths, _runtimeRoot);
        var certifications = certService.LoadReports()
            .Where(report => report.Status == ScalpingCertificationStatus.certified_candidate && report.Asset.Equals(normalizedAsset, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(report => report.DrawdownCertification.ProfitFactor)
            .ThenBy(report => report.CandidateId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var findings = new List<CertifiedCandidateAuditFinding>();
        var warnings = new List<string>();

        foreach (var report in certifications)
        {
            var candidate = researchService.FindCandidate(report.CandidateId);
            var expansion = expansionService.LoadReport(report.CandidateId);
            var tradeCount = candidate?.Backtest.TradeCount ?? 0;
            var oosStatus = candidate is null ? "missing" : candidate.Backtest.OosNetR > 0 ? "positive" : "negative";
            var walkStatus = candidate is null ? "missing" : candidate.Backtest.WalkForwardNetR >= -0.1 ? "acceptable" : "negative";
            var monteCarloStatus = expansion is null ? "missing" : expansion.MonteCarlo.Health;
            var sensitivityStatus = expansion is null ? "missing" : expansion.ParameterSensitivity.Health;
            var regimeStatus = expansion is null ? "missing" : expansion.RegimeValidation.Health;
            var spreadStressStatus = candidate is null ? "missing" : candidate.Backtest.CostStressNetR > 0 ? "survived" : "failed";
            var slippageStressStatus = candidate is null ? "missing" : candidate.Backtest.CostStressNetR > 0 ? "survived" : "failed";
            var thresholdsMet = new List<string>();
            if (report.DrawdownCertification.Health == "ok") thresholdsMet.Add("drawdown_health_ok");
            if (report.DrawdownCertification.ProfitFactor > 1.1) thresholdsMet.Add("profit_factor_above_1_1");
            if (report.DrawdownCertification.RecoveryFactor >= 1.2) thresholdsMet.Add("recovery_factor_above_1_2");
            if (report.SessionValidation.Any(session => session.Status == "positive")) thresholdsMet.Add("positive_session_detected");
            if (report.MultiPeriodValidation.All(period => period.Status == "passed")) thresholdsMet.Add("multi_period_passed");

            var weakness = new List<string>();
            if (tradeCount < 100) weakness.Add("low_trade_sample");
            if (candidate?.Backtest.WinRate is > 0.70) weakness.Add("high_winrate_requires_review");
            if (candidate is null || expansion is null) weakness.Add("missing_robustness_artifacts");
            if (candidate?.Validation.HasCriticalOverfitWarnings == true) weakness.Add("critical_overfit_warning");
            if (tradeCount <= 0) weakness.Add("no_trade_count_available");
            if (candidate?.RiskProfile.RiskOfRuinProbability is > 0.08) weakness.Add("risk_of_ruin_high");
            if (candidate?.Backtest.OosNetR is <= 0) weakness.Add("oos_not_positive");

            var auditWarnings = new List<string>();
            if (tradeCount < 80) auditWarnings.Add("extremely_few_trades");
            if (candidate?.Backtest.WinRate is > 0.70) auditWarnings.Add("unrealistically_high_winrate_check");
            if (expansion is null) auditWarnings.Add("missing_monte_carlo_and_sensitivity_reports");
            if (candidate is null) auditWarnings.Add("candidate_details_missing");
            if (report.SessionValidation.Count == 0) auditWarnings.Add("missing_session_validation");
            if (report.MultiPeriodValidation.Count == 0) auditWarnings.Add("missing_walk_forward_validation");

            if (auditWarnings.Count > 0)
            {
                warnings.AddRange(auditWarnings.Select(item => $"{report.CandidateId}:{item}"));
            }

            findings.Add(new CertifiedCandidateAuditFinding(
                CandidateId: report.CandidateId,
                Asset: report.Asset,
                Timeframe: report.Timeframe,
                SetupType: report.SetupType,
                Direction: SetupDirection(report.SetupType),
                CertificationStatus: report.Status.ToString(),
                UsedIndicators: UsedIndicators(report.SetupType),
                UsedFilters: UsedFilters(candidate),
                SessionFilter: candidate?.SessionFilter ?? "-",
                MarketRegimeFilter: expansion is null ? "missing" : string.Join(",", expansion.RegimeValidation.RegimeScores.Keys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                EntryRules: candidate is null ? "-" : string.Join(" | ", candidate.EntryRules),
                ExitRules: candidate is null ? "-" : string.Join(" | ", candidate.ExitRules),
                StopLossLogic: candidate is null ? "-" : string.Join(" | ", candidate.StopLossRules),
                TakeProfitLogic: candidate is null ? "-" : string.Join(" | ", candidate.TakeProfitRules),
                InvalidationRules: candidate is null ? "-" : string.Join(" | ", candidate.StopLossRules.Concat(["spread_filter_fails", "session_filter_fails", "news_filter_stub_blocks"])),
                TradesTotal: tradeCount > 0 ? tradeCount.ToString() : "not_captured",
                TradesPerYear: tradeCount > 0 ? tradeCount.ToString() : "not_captured",
                TradesPerMonth: tradeCount > 0 ? Math.Max(1, tradeCount / 12).ToString() : "not_captured",
                TradesPerWeek: tradeCount > 0 ? Math.Max(1, tradeCount / 52).ToString() : "not_captured",
                AverageHoldingDuration: "not_captured_in_current_backtest_format",
                WinRate: candidate is null ? "not_captured" : candidate.Backtest.WinRate.ToString("0.####"),
                ProfitFactor: candidate is null ? "not_captured" : candidate.Backtest.ProfitFactor.ToString("0.####"),
                Expectancy: candidate is null ? "not_captured" : candidate.Backtest.AverageTradeR.ToString("0.####"),
                Sharpe: "not_captured",
                Sortino: "not_captured",
                MaxDrawdown: candidate is null ? "not_captured" : candidate.Backtest.MaxDrawdownR.ToString("0.####"),
                MaxDailyDrawdown: report.DrawdownCertification.MaxDailyDrawdownR.ToString("0.####"),
                RiskOfRuin: candidate is null ? "not_captured" : candidate.RiskProfile.RiskOfRuinProbability.ToString("0.####"),
                SignalDensity: "not_captured",
                WalkForwardStatus: walkStatus,
                OosStatus: oosStatus,
                MonteCarloStatus: monteCarloStatus,
                SensitivityStatus: sensitivityStatus,
                SpreadStressStatus: spreadStressStatus,
                SlippageStressStatus: slippageStressStatus,
                CertificationReason: report.CertifiedCandidate ? "passed_multi_period_session_drawdown_distribution_and_overfit_checks" : "certification_failed",
                MinimumThresholdsMet: thresholdsMet,
                Weaknesses: weakness,
                AuditWarnings: auditWarnings,
                SourceCertificationPath: report.CertificationReportPath,
                SourceExpansionPath: expansion is null ? "-" : Path.Combine(expansionService.ExpansionDirectory, $"{report.CandidateId}.json"),
                HumanReviewRequired: report.HumanReviewRequired));
        }

        var auditReport = new CertifiedCandidateAuditReport(
            ReportVersion: "certified_candidate_audit_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Asset: normalizedAsset,
            Findings: findings,
            AuditWarnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item, StringComparer.OrdinalIgnoreCase).ToList(),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            BrokerOrdersEnabled: false,
            LiveTradingEnabled: false,
            ResearchOnly: true);

        Directory.CreateDirectory(Root);
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(auditReport, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(auditReport));
        return auditReport;
    }

    public CertifiedCandidateAuditReport? LoadReport()
    {
        return File.Exists(ReportPath)
            ? JsonSerializer.Deserialize<CertifiedCandidateAuditReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions)
            : null;
    }

    private string ResolveRoot()
    {
        var preferred = Path.Combine(_storagePaths.Root, "reports", "scalping_research");
        try
        {
            Directory.CreateDirectory(preferred);
            var probePath = Path.Combine(preferred, ".write_probe");
            File.WriteAllText(probePath, "probe");
            File.Delete(probePath);
            return Path.Combine(preferred, "audit");
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
        var fallback = Path.Combine(_runtimeRoot, ".codex_artifacts", "reports", "scalping_research", "audit");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    private static string UsedIndicators(string setupType)
        => setupType.Contains("breakout", StringComparison.OrdinalIgnoreCase)
            ? "M5 structure, swing high/low, session liquidity, spread filter"
            : "M5 structure, EMA pullback, session liquidity, spread filter";

    private static string SetupDirection(string setupType)
        => setupType.Contains("breakout", StringComparison.OrdinalIgnoreCase) ? "long_short" : "long_short";

    private static string UsedFilters(ScalpingStrategyCandidate? candidate)
        => candidate is null
            ? "missing"
            : string.Join("; ", new[] { candidate.SessionFilter, candidate.SpreadFilter, candidate.NewsFilterStub });

    private static string BuildMarkdown(CertifiedCandidateAuditReport report)
    {
        var lines = new List<string>
        {
            $"# Certified Candidate Audit - {report.Asset}",
            "",
            $"- report_version: {report.ReportVersion}",
            $"- updated_at_utc: {report.UpdatedAtUtc:O}",
            $"- no_auto_trading: true",
            $"- human_review_required: true",
            $"- broker_orders_enabled: false",
            $"- live_trading_enabled: false",
            $"- research_only: true",
            ""
        };

        foreach (var finding in report.Findings)
        {
            lines.Add($"## {finding.CandidateId}");
            lines.Add($"- setup_type: {finding.SetupType}");
            lines.Add($"- certification_status: {finding.CertificationStatus}");
            lines.Add($"- profit_factor: {finding.ProfitFactor}");
            lines.Add($"- win_rate: {finding.WinRate}");
            lines.Add($"- monte_carlo: {finding.MonteCarloStatus}");
            lines.Add($"- walk_forward: {finding.WalkForwardStatus}");
            lines.Add($"- oos: {finding.OosStatus}");
            lines.Add($"- warnings: {string.Join(", ", finding.AuditWarnings)}");
            lines.Add("");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
