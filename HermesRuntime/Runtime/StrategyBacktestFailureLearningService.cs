using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record StrategyBacktestFailureLearningMutationSuggestion(
    string Title,
    string Reason,
    string ExpectedBenefit);

public sealed record StrategyBacktestFailureLearningReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string BacktestJobId,
    string StrategyPattern,
    string Asset,
    string Timeframe,
    int TradesSimulated,
    double WinRate,
    double ProfitFactor,
    double MaxDrawdown,
    double Expectancy,
    double RMultipleAvg,
    string QualityClass,
    bool CertificationReady,
    bool FrankRequired,
    bool FailedBacktestEvidence,
    string KnowledgeUpdateTag,
    string LearningDecision,
    IReadOnlyList<string> BlockingFactors,
    IReadOnlyList<string> RootCauses,
    IReadOnlyList<StrategyBacktestFailureLearningMutationSuggestion> MutationSuggestions,
    IReadOnlyList<string> Recommendations,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string ReportPath,
    string MarkdownPath);

public sealed class StrategyBacktestFailureLearningService
{
    private readonly StoragePaths _storagePaths;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public StrategyBacktestFailureLearningService(StoragePaths storagePaths)
    {
        _storagePaths = storagePaths;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "strategy_backtest_failure_learning");
    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "strategy_backtest_failure_learning.json");
    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "strategy_backtest_failure_learning.md");

    public StrategyBacktestFailureLearningReport Run()
    {
        Directory.CreateDirectory(Root);

        var latestSuccess = StrategyBacktestResultArchiveService.LoadLatestSuccess(_storagePaths);
        var qualityAudit = new StrategyBacktestQualityAuditService(_storagePaths).Load();

        var report = latestSuccess is null
            ? BuildNoSuccessReport(qualityAudit)
            : BuildReport(latestSuccess, qualityAudit);

        WriteArtifacts(report);
        return report;
    }

    public StrategyBacktestFailureLearningReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StrategyBacktestFailureLearningReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private StrategyBacktestFailureLearningReport BuildNoSuccessReport(StrategyBacktestQualityAuditReport? qualityAudit)
    {
        var warnings = new List<string> { "no_successful_backtest_found" };
        return new StrategyBacktestFailureLearningReport(
            ReportVersion: "strategy_backtest_failure_learning_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            BacktestJobId: "-",
            StrategyPattern: "-",
            Asset: "-",
            Timeframe: "-",
            TradesSimulated: 0,
            WinRate: 0,
            ProfitFactor: 0,
            MaxDrawdown: 0,
            Expectancy: 0,
            RMultipleAvg: 0,
            QualityClass: qualityAudit?.Entries.FirstOrDefault()?.QualityClass ?? "unknown",
            CertificationReady: false,
            FrankRequired: false,
            FailedBacktestEvidence: false,
            KnowledgeUpdateTag: "failed_backtest_evidence",
            LearningDecision: "no_successful_backtest_available",
            BlockingFactors: ["keine_erfolgreichen_backtests_vorhanden"],
            RootCauses: ["unknown"],
            MutationSuggestions: [],
            Recommendations: ["Zuerst einen erfolgreichen historischen Backtest erzeugen."],
            Warnings: warnings,
            OperatorSummary: "Kein erfolgreicher Backtest vorhanden. Frank nötig: nein.",
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);
    }

    private StrategyBacktestFailureLearningReport BuildReport(StrategyBacktestExecutorResultArtifact latestSuccess, StrategyBacktestQualityAuditReport? qualityAudit)
    {
        var execution = latestSuccess.Execution;
        var job = latestSuccess.Job;
        var trades = execution.TradesSimulated ?? 0;
        var winRate = execution.WinRate ?? 0;
        var profitFactor = execution.ProfitFactor ?? 0;
        var maxDrawdown = execution.MaxDrawdown ?? 0;
        var expectancy = execution.Expectancy ?? 0;
        var rMultipleAvg = execution.RMultipleAvg ?? 0;
        var qualityClass = qualityAudit?.Entries.FirstOrDefault()?.QualityClass ?? Classify(trades);
        var certificationReady = qualityAudit?.Entries.FirstOrDefault()?.EligibleForCertification ?? false;
        var failedBacktestEvidence = profitFactor < 1.0 || expectancy < 0 || maxDrawdown < -2.0 || winRate < 0.5;

        var blockingFactors = new List<string>();
        if (trades < 30)
        {
            blockingFactors.Add("zu_wenig_trades");
        }
        if (profitFactor < 1.0)
        {
            blockingFactors.Add("profit_factor_unter_1");
        }
        if (expectancy < 0)
        {
            blockingFactors.Add("negativer_erwartungswert");
        }
        if (winRate < 0.5)
        {
            blockingFactors.Add("niedrige_winrate");
        }
        if (maxDrawdown < -2.0)
        {
            blockingFactors.Add("zu_hoher_drawdown");
        }

        var rootCauses = DetermineRootCauses(trades, profitFactor, expectancy, maxDrawdown, winRate, qualityClass);
        var learningDecision = DetermineLearningDecision(failedBacktestEvidence, trades, profitFactor, expectancy, maxDrawdown);
        var suggestions = BuildMutationSuggestions(job, rootCauses);
        var recommendations = BuildRecommendations(learningDecision, rootCauses, suggestions);
        var warnings = new List<string>();
        if (execution.Warnings.Count > 0)
        {
            warnings.AddRange(execution.Warnings);
        }
        if (!failedBacktestEvidence)
        {
            warnings.Add("no_strong_failure_signal_detected");
        }

        var operatorSummary = BuildOperatorSummary(job, trades, winRate, profitFactor, maxDrawdown, expectancy, qualityClass, certificationReady, learningDecision, suggestions);

        return new StrategyBacktestFailureLearningReport(
            ReportVersion: "strategy_backtest_failure_learning_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            BacktestJobId: job.BacktestJobId,
            StrategyPattern: job.StrategyPattern,
            Asset: job.Asset,
            Timeframe: job.Timeframe,
            TradesSimulated: trades,
            WinRate: Math.Round(winRate, 4),
            ProfitFactor: Math.Round(profitFactor, 4),
            MaxDrawdown: Math.Round(maxDrawdown, 4),
            Expectancy: Math.Round(expectancy, 4),
            RMultipleAvg: Math.Round(rMultipleAvg, 4),
            QualityClass: qualityClass,
            CertificationReady: certificationReady,
            FrankRequired: false,
            FailedBacktestEvidence: failedBacktestEvidence,
            KnowledgeUpdateTag: "failed_backtest_evidence",
            LearningDecision: learningDecision,
            BlockingFactors: blockingFactors.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            RootCauses: rootCauses,
            MutationSuggestions: suggestions,
            Recommendations: recommendations,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            OperatorSummary: operatorSummary,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);
    }

    private static string DetermineLearningDecision(bool failedBacktestEvidence, int trades, double profitFactor, double expectancy, double maxDrawdown)
    {
        if (!failedBacktestEvidence)
        {
            return "mutate_and_retest";
        }

        if (trades >= 30 && profitFactor < 1.0 && expectancy < 0)
        {
            return "hypothesis_backtest_failed_backstops_should_be_tightened";
        }

        if (maxDrawdown < -5.0)
        {
            return "hypothesis_backtest_failed_drawdown_too_high";
        }

        return "hypothesis_should_be_reduced_to_mutation_only";
    }

    private static IReadOnlyList<string> DetermineRootCauses(int trades, double profitFactor, double expectancy, double maxDrawdown, double winRate, string qualityClass)
    {
        var causes = new List<string>();
        if (trades < 30)
        {
            causes.Add("zu_wenig_trades");
        }
        if (profitFactor < 1.0)
        {
            causes.Add("schlechter_profit_factor");
        }
        if (expectancy < 0)
        {
            causes.Add("negativer_erwartungswert");
        }
        if (maxDrawdown < -2.0)
        {
            causes.Add("zu_hoher_drawdown");
        }
        if (winRate < 0.5)
        {
            causes.Add("niedrige_winrate");
        }
        if (qualityClass.Equals("low_confidence", StringComparison.OrdinalIgnoreCase))
        {
            causes.Add("low_confidence_sample");
        }
        return causes.Count > 0 ? causes.Distinct(StringComparer.OrdinalIgnoreCase).ToList() : ["unknown"];
    }

    private static IReadOnlyList<StrategyBacktestFailureLearningMutationSuggestion> BuildMutationSuggestions(StrategyBacktestJobPlan job, IReadOnlyList<string> rootCauses)
    {
        var suggestions = new List<StrategyBacktestFailureLearningMutationSuggestion>();
        if (job.StrategyPattern.Equals("Mean Reversion Rejection", StringComparison.OrdinalIgnoreCase))
        {
            suggestions.Add(new StrategyBacktestFailureLearningMutationSuggestion(
                "Sessionfilter schärfen",
                "Mean-Reversion reagiert oft nur in liquiden Sessions robust.",
                "Weniger fehleranfällige Einstiegssituationen und bessere Ausführungsqualität."));
            suggestions.Add(new StrategyBacktestFailureLearningMutationSuggestion(
                "Volatilitätsfilter ergänzen",
                "Negative Erwartung und hoher Drawdown sprechen für ein ungünstiges Regime.",
                "Nur ruhige oder passende Volatilitätsphasen testen."));
            suggestions.Add(new StrategyBacktestFailureLearningMutationSuggestion(
                "Trendfilter ergänzen",
                "Mean-Reversion kann in Trendphasen strukturell verlieren.",
                "Verluste in Trendregimen reduzieren."));
            suggestions.Add(new StrategyBacktestFailureLearningMutationSuggestion(
                "Entry-Zone enger machen",
                "Der Entry scheint aktuell zu breit oder zu spät zu sein.",
                "Treffgenauere Reversion-Einstiege und bessere Winrate."));
            suggestions.Add(new StrategyBacktestFailureLearningMutationSuggestion(
                "Mean-Reversion nur im Range-Regime testen",
                "Der historische Test wirkt regime-abhängig und nicht robust genug.",
                "Bessere Regime-Fokussierung statt allgemeiner Ausweitung."));
            suggestions.Add(new StrategyBacktestFailureLearningMutationSuggestion(
                "Anderes Timeframe prüfen",
                "Die Reaktionsgeschwindigkeit kann auf M5 zu aggressiv oder zu langsam sein.",
                "Stabilere Reversionsstruktur auf anderem Zeitfenster suchen."));
        }

        if (rootCauses.Contains("zu_hoher_drawdown", StringComparer.OrdinalIgnoreCase))
        {
            suggestions.Add(new StrategyBacktestFailureLearningMutationSuggestion(
                "Invalidation früher setzen",
                "Hohe Drawdowns deuten auf zu spätes Aussteigen hin.",
                "Drawdown begrenzen und Verluste schneller kappen."));
        }

        if (rootCauses.Contains("negativer_erwartungswert", StringComparer.OrdinalIgnoreCase))
        {
            suggestions.Add(new StrategyBacktestFailureLearningMutationSuggestion(
                "Andere Parameter testen",
                "Die aktuelle Parameterkombination erzeugt keinen positiven Erwartungswert.",
                "Alternativen mit besserem R-Ertrag priorisieren."));
        }

        return suggestions.DistinctBy(item => item.Title, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<string> BuildRecommendations(string learningDecision, IReadOnlyList<string> rootCauses, IReadOnlyList<StrategyBacktestFailureLearningMutationSuggestion> suggestions)
    {
        var recommendations = new List<string>();
        if (learningDecision.Contains("failed", StringComparison.OrdinalIgnoreCase))
        {
            recommendations.Add("Hypothese zurückstufen");
        }
        else
        {
            recommendations.Add("Nur mit Mutation weiterführen");
        }

        if (rootCauses.Contains("negativer_erwartungswert", StringComparer.OrdinalIgnoreCase))
        {
            recommendations.Add("Zusätzliche Filter testen");
        }

        if (rootCauses.Contains("zu_hoher_drawdown", StringComparer.OrdinalIgnoreCase))
        {
            recommendations.Add("Anderes Marktregime prüfen");
        }

        if (suggestions.Count > 0)
        {
            recommendations.Add("Weitere Parameter testen");
        }

        recommendations.Add("Knowledge-Update als failed_backtest_evidence vorbereiten");
        return recommendations.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static string BuildOperatorSummary(
        StrategyBacktestJobPlan job,
        int trades,
        double winRate,
        double profitFactor,
        double maxDrawdown,
        double expectancy,
        string qualityClass,
        bool certificationReady,
        string learningDecision,
        IReadOnlyList<StrategyBacktestFailureLearningMutationSuggestion> suggestions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{job.StrategyPattern} · {job.Asset} {job.Timeframe}");
        sb.AppendLine();
        sb.AppendLine("Der historische Test zeigt keine zertifizierbare Qualität.");
        sb.AppendLine();
        sb.AppendLine("Blocker:");
        sb.AppendLine($"- Trades: {trades}");
        sb.AppendLine($"- Winrate: {winRate:0.####}");
        sb.AppendLine($"- Profit Factor: {profitFactor:0.####}");
        sb.AppendLine($"- Max Drawdown: {maxDrawdown:0.####}");
        sb.AppendLine($"- Expectancy: {expectancy:0.####}");
        sb.AppendLine($"- Quality: {qualityClass}");
        sb.AppendLine($"- Certification Ready: {certificationReady.ToString().ToLowerInvariant()}");
        sb.AppendLine();
        sb.AppendLine("Lernentscheidung:");
        sb.AppendLine(learningDecision);
        sb.AppendLine();
        sb.AppendLine("Empfehlung:");
        sb.AppendLine("Die Strategie verliert im historischen Test mehr, als sie gewinnt.");
        sb.AppendLine("Pro Trade entsteht im Durchschnitt ein negativer Erwartungswert.");
        sb.AppendLine();
        sb.AppendLine("Mutation:");
        foreach (var suggestion in suggestions.Take(6))
        {
            sb.AppendLine($"- {suggestion.Title}: {suggestion.Reason}");
        }
        sb.AppendLine();
        sb.AppendLine("Frank nötig:");
        sb.AppendLine("nein");
        return sb.ToString().TrimEnd();
    }

    private void WriteArtifacts(StrategyBacktestFailureLearningReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, markdown);
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(StrategyBacktestFailureLearningReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Strategy Backtest Failure Learning");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Job: {report.BacktestJobId}");
        sb.AppendLine($"- Strategy: {report.StrategyPattern}");
        sb.AppendLine($"- Asset: {report.Asset}");
        sb.AppendLine($"- Timeframe: {report.Timeframe}");
        sb.AppendLine($"- Trades simulated: {report.TradesSimulated}");
        sb.AppendLine($"- Win rate: {report.WinRate:0.####}");
        sb.AppendLine($"- Profit factor: {report.ProfitFactor:0.####}");
        sb.AppendLine($"- Max drawdown: {report.MaxDrawdown:0.####}");
        sb.AppendLine($"- Expectancy: {report.Expectancy:0.####}");
        sb.AppendLine($"- Quality class: {report.QualityClass}");
        sb.AppendLine($"- Certification ready: {report.CertificationReady}");
        sb.AppendLine($"- Failed backtest evidence: {report.FailedBacktestEvidence}");
        sb.AppendLine($"- Knowledge update tag: {report.KnowledgeUpdateTag}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Blocking Factors");
        foreach (var factor in report.BlockingFactors)
        {
            sb.AppendLine($"- {factor}");
        }
        sb.AppendLine();
        sb.AppendLine("## Mutation Suggestions");
        foreach (var suggestion in report.MutationSuggestions)
        {
            sb.AppendLine($"- {suggestion.Title}: {suggestion.Reason} -> {suggestion.ExpectedBenefit}");
        }
        return sb.ToString();
    }

    private static string Classify(int trades)
        => trades < 30 ? "insufficient_sample"
            : trades <= 100 ? "low_confidence"
            : trades <= 300 ? "medium_confidence"
            : "high_confidence";
}
