using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record FailureGuidedMutationCandidate(
    string MutationId,
    string MutationType,
    string Title,
    string Description,
    string WhySuggested,
    string ExpectedBenefit,
    string RiskLevel,
    string EffortLevel,
    string Priority,
    IReadOnlyList<string> RelatedBlockers,
    IReadOnlyList<string> RelatedRootCauses,
    IReadOnlyList<string> EvidenceSources,
    string SuggestedNextValidation);

public sealed record FailureGuidedMutationPlannerReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string SourceBacktestJobId,
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
    string LearningDecision,
    string KnowledgeUpdateTag,
    int MutationCandidatesCount,
    IReadOnlyList<FailureGuidedMutationCandidate> MutationCandidates,
    IReadOnlyList<string> SourceReports,
    IReadOnlyList<string> Warnings,
    string OperatorSummary,
    string SafetySummary,
    bool FrankRequired,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired,
    string ReportPath,
    string MarkdownPath);

public sealed class FailureGuidedMutationPlannerService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public FailureGuidedMutationPlannerService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "failure_guided_mutation_planner");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "failure_guided_mutation_planner.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "failure_guided_mutation_planner.md");

    public FailureGuidedMutationPlannerReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<FailureGuidedMutationPlannerReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    public FailureGuidedMutationPlannerReport Run()
    {
        Directory.CreateDirectory(Root);

        var latestSuccess = StrategyBacktestResultArchiveService.LoadLatestSuccess(_storagePaths);
        var failureLearning = new StrategyBacktestFailureLearningService(_storagePaths).Load();
        var qualityAudit = new StrategyBacktestQualityAuditService(_storagePaths).Load();
        var parameterPlanner = new StrategyParameterResearchPlannerService(_storagePaths, _runtimeRoot).Load();
        var tradingSynthesizer = new TradingResearchSynthesizerService(_storagePaths, _runtimeRoot).Load();

        var report = BuildReport(latestSuccess, failureLearning, qualityAudit, parameterPlanner, tradingSynthesizer);
        WriteArtifacts(report);
        return report;
    }

    private FailureGuidedMutationPlannerReport BuildReport(
        StrategyBacktestExecutorResultArtifact? latestSuccess,
        StrategyBacktestFailureLearningReport? failureLearning,
        StrategyBacktestQualityAuditReport? qualityAudit,
        StrategyParameterResearchPlannerReport? parameterPlanner,
        TradingResearchSynthesizerReport? tradingSynthesizer)
    {
        if (latestSuccess is null)
        {
            return BuildNoSuccessReport(failureLearning, qualityAudit, parameterPlanner, tradingSynthesizer);
        }

        var execution = latestSuccess.Execution;
        var job = latestSuccess.Job;
        var trades = execution.TradesSimulated ?? 0;
        var winRate = execution.WinRate ?? 0;
        var profitFactor = execution.ProfitFactor ?? 0;
        var maxDrawdown = execution.MaxDrawdown ?? 0;
        var expectancy = execution.Expectancy ?? 0;
        var rMultipleAvg = execution.RMultipleAvg ?? 0;
        var learningDecision = failureLearning?.LearningDecision ?? "hypothesis_should_be_reduced_to_mutation_only";
        var qualityClass = qualityAudit?.Entries.FirstOrDefault()?.QualityClass ?? Classify(trades);

        var blockers = failureLearning?.BlockingFactors?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rootCauses = failureLearning?.RootCauses?.ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var evidenceSources = BuildEvidenceSources(
            latestSuccess,
            failureLearning,
            qualityAudit,
            parameterPlanner,
            tradingSynthesizer);

        var candidates = BuildCandidates(
            job,
            trades,
            winRate,
            profitFactor,
            maxDrawdown,
            expectancy,
            qualityClass,
            blockers,
            rootCauses,
            parameterPlanner,
            tradingSynthesizer,
            evidenceSources);

        var warnings = new List<string>();
        if (failureLearning?.Warnings.Count > 0)
        {
            warnings.AddRange(failureLearning.Warnings);
        }

        if (qualityAudit?.Warnings.Count > 0)
        {
            warnings.AddRange(qualityAudit.Warnings);
        }

        if (parameterPlanner is null)
        {
            warnings.Add("strategy_parameter_research_planner_missing");
        }

        if (tradingSynthesizer is null)
        {
            warnings.Add("trading_research_synthesizer_missing");
        }

        var operatorSummary = BuildOperatorSummary(job, candidates, learningDecision, qualityClass, trades, winRate, profitFactor, maxDrawdown, expectancy);

        return new FailureGuidedMutationPlannerReport(
            ReportVersion: "failure_guided_mutation_planner_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            SourceBacktestJobId: job.BacktestJobId,
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
            CertificationReady: qualityAudit?.Entries.FirstOrDefault()?.EligibleForCertification ?? false,
            LearningDecision: learningDecision,
            KnowledgeUpdateTag: failureLearning?.KnowledgeUpdateTag ?? "failed_backtest_evidence",
            MutationCandidatesCount: candidates.Count,
            MutationCandidates: candidates,
            SourceReports: evidenceSources,
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            OperatorSummary: operatorSummary,
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true",
            FrankRequired: false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);
    }

    private FailureGuidedMutationPlannerReport BuildNoSuccessReport(
        StrategyBacktestFailureLearningReport? failureLearning,
        StrategyBacktestQualityAuditReport? qualityAudit,
        StrategyParameterResearchPlannerReport? parameterPlanner,
        TradingResearchSynthesizerReport? tradingSynthesizer)
    {
        var warnings = new List<string> { "no_successful_backtest_found" };
        if (failureLearning is null)
        {
            warnings.Add("failure_learning_report_missing");
        }

        if (qualityAudit is null)
        {
            warnings.Add("quality_audit_missing");
        }

        if (parameterPlanner is null)
        {
            warnings.Add("strategy_parameter_research_planner_missing");
        }

        if (tradingSynthesizer is null)
        {
            warnings.Add("trading_research_synthesizer_missing");
        }

        return new FailureGuidedMutationPlannerReport(
            ReportVersion: "failure_guided_mutation_planner_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            SourceBacktestJobId: "-",
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
            LearningDecision: "no_successful_backtest_available",
            KnowledgeUpdateTag: "failed_backtest_evidence",
            MutationCandidatesCount: 0,
            MutationCandidates: [],
            SourceReports: BuildEvidenceSources(null, failureLearning, qualityAudit, parameterPlanner, tradingSynthesizer),
            Warnings: warnings,
            OperatorSummary: "Kein erfolgreicher Backtest vorhanden. Frank muss nichts entscheiden.",
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true",
            FrankRequired: false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);
    }

    private static IReadOnlyList<string> BuildEvidenceSources(
        StrategyBacktestExecutorResultArtifact? latestSuccess,
        StrategyBacktestFailureLearningReport? failureLearning,
        StrategyBacktestQualityAuditReport? qualityAudit,
        StrategyParameterResearchPlannerReport? parameterPlanner,
        TradingResearchSynthesizerReport? tradingSynthesizer)
    {
        var sources = new List<string>();
        if (latestSuccess is not null)
        {
            sources.Add("strategy_backtest_latest_success");
        }

        if (failureLearning is not null)
        {
            sources.Add("strategy_backtest_failure_learning");
        }

        if (qualityAudit is not null)
        {
            sources.Add("strategy_backtest_quality_audit");
        }

        if (parameterPlanner is not null)
        {
            sources.Add("strategy_parameter_research_planner");
        }

        if (tradingSynthesizer is not null)
        {
            sources.Add("trading_research_synthesizer");
        }

        return sources;
    }

    private static IReadOnlyList<FailureGuidedMutationCandidate> BuildCandidates(
        StrategyBacktestJobPlan job,
        int trades,
        double winRate,
        double profitFactor,
        double maxDrawdown,
        double expectancy,
        string qualityClass,
        HashSet<string> blockers,
        HashSet<string> rootCauses,
        StrategyParameterResearchPlannerReport? parameterPlanner,
        TradingResearchSynthesizerReport? tradingSynthesizer,
        IReadOnlyList<string> evidenceSources)
    {
        var candidates = new List<FailureGuidedMutationCandidate>();
        void Add(
            string mutationType,
            string title,
            string description,
            string whySuggested,
            string expectedBenefit,
            string riskLevel,
            string effortLevel,
            string priority,
            IReadOnlyList<string> relatedBlockers,
            IReadOnlyList<string> relatedRootCauses,
            string nextValidation)
        {
            candidates.Add(new FailureGuidedMutationCandidate(
                MutationId: $"fgmp_{job.BacktestJobId}_{mutationType}".Replace(' ', '_'),
                MutationType: mutationType,
                Title: title,
                Description: description,
                WhySuggested: whySuggested,
                ExpectedBenefit: expectedBenefit,
                RiskLevel: riskLevel,
                EffortLevel: effortLevel,
                Priority: priority,
                RelatedBlockers: relatedBlockers,
                RelatedRootCauses: relatedRootCauses,
                EvidenceSources: evidenceSources,
                SuggestedNextValidation: nextValidation));
        }

        if (job.StrategyPattern.Equals("Mean Reversion Rejection", StringComparison.OrdinalIgnoreCase) && job.Asset.Equals("XAUUSD", StringComparison.OrdinalIgnoreCase))
        {
            Add(
                "session_filter_sharpen",
                "Sessionfilter schärfen",
                "Mean Reversion Rejection reagiert im Gold-M5-Kontext stark auf Liquidity-Window und Sessionqualität.",
                "Der Verlusttest zeigt niedrige Winrate und negativen Erwartungswert; sessionarme Phasen verschlechtern Mean Reversion oft.",
                "Weniger schlechte Einstiege und bessere Trefferquote in liquiden Handelsfenstern.",
                "medium",
                "low",
                "high",
                [ "niedrige_winrate", "negativer_erwartungswert" ],
                [ "niedrige_winrate", "negativer_erwartungswert" ],
                "Session-gesiebte Validierungsvariante für London/New York");

            Add(
                "volatility_filter_add",
                "Volatilitätsfilter ergänzen",
                "Der Drawdown spricht für ungünstige Volatilitätsfenster.",
                "Profit Factor unter 1 und hoher Drawdown deuten auf falsche Regimebedingungen hin.",
                "Verluste in unpassender Volatilität vermeiden.",
                "medium",
                "medium",
                "high",
                [ "zu_hoher_drawdown", "profit_factor_unter_1" ],
                [ "schlechter_profit_factor", "zu_hoher_drawdown" ],
                "Volatilitätsfilter mit Band-Width- oder ATR-Filter validieren");

            Add(
                "trend_filter_add",
                "Trendfilter ergänzen",
                "Mean-Reversion verliert strukturell in Trendphasen.",
                "Niedrige Winrate und negativer Erwartungswert passen zu Trend-Regime-Fehlern.",
                "Trendphasen ausschließen und Edge stabilisieren.",
                "medium",
                "medium",
                "high",
                [ "niedrige_winrate", "negativer_erwartungswert" ],
                [ "niedrige_winrate", "negativer_erwartungswert" ],
                "Trendfilter-Variante gegen die aktuelle Range-/Mean-Reversion-Hypothese testen");

            Add(
                "range_regime_enforce",
                "Range-Regime erzwingen",
                "Die Strategie sollte nur in Seitwärts- oder Reversionsregimen laufen.",
                "Das aktuelle Ergebnis zeigt, dass ein breiter Marktmodus zu viel Verlust produziert.",
                "Bessere Regime-Fokussierung und weniger Fehltrades.",
                "medium",
                "medium",
                "high",
                [ "profit_factor_unter_1", "negativer_erwartungswert" ],
                [ "schlechter_profit_factor", "negativer_erwartungswert" ],
                "Range-Regime-Kandidaten für denselben Markt validieren");

            Add(
                "entry_zone_narrow",
                "Entry-Zone enger machen",
                "Die Entry-Logik trifft offenbar zu breit oder zu spät.",
                "Niedrige Winrate deutet auf zu viele schwache Einstiege hin.",
                "Bessere Entry-Qualität und weniger Fehlsignale.",
                "medium",
                "low",
                "high",
                [ "niedrige_winrate" ],
                [ "niedrige_winrate", "zu_wenig_trades" ],
                "Engere Entry-Zone als Mutationsvariante prüfen");

            Add(
                "invalidate_earlier",
                "Invalidation früher setzen",
                "Der Drawdown ist zu hoch für einen robusten Mean-Reversion-Kandidaten.",
                "Der Test verliert zu viel pro Fehltrade; schnellere Invalidation kann das begrenzen.",
                "Drawdown reduzieren und Verlustphasen verkürzen.",
                "medium",
                "low",
                "high",
                [ "zu_hoher_drawdown" ],
                [ "zu_hoher_drawdown" ],
                "Frühere Invalidation und engeres Risk-Handling validieren");

            Add(
                "timeframe_alternative",
                "Anderes Timeframe prüfen",
                "M5 kann für diese Reversionshypothese zu laut oder zu langsam sein.",
                "Negative Erwartung und niedrige Winrate können auf einen ungeeigneten Intraday-Takt hinweisen.",
                "Stabilere Reversionsstruktur auf anderem Zeitfenster finden.",
                "low",
                "medium",
                "medium",
                [ "negativer_erwartungswert", "niedrige_winrate" ],
                [ "schlechter_profit_factor", "low_confidence_sample" ],
                "Alternatives Timeframe gegen denselben Markt testen");

            Add(
                "parameter_range_refine",
                "Parameterbereich enger setzen",
                "Die aktuelle Parametrisierung liefert noch kein positives Edge-Signal.",
                "Parameter Research und Trading Research zeigen angrenzende, aber noch unbestätigte Bereiche.",
                "Bessere Parameterkombination mit höherem Erwartungswert finden.",
                "low",
                "medium",
                "medium",
                [ "profit_factor_unter_1", "negativer_erwartungswert" ],
                [ "schlechter_profit_factor", "negativer_erwartungswert" ],
                "Engere Parameter-Range aus Research-Plan und Synthesizer ableiten");
        }
        else
        {
            Add(
                "mutation_only_fallback",
                "Nur mit Mutation weiterführen",
                "Der konkrete Pattern-/Asset-Kombi ist noch nicht belastbar.",
                "Die Analyse zeigt kein robustes Setup ohne Anpassung.",
                "Weiterführende Lernprobe statt Zertifizierungsanwärter.",
                "low",
                "low",
                "medium",
                blockers.ToList(),
                rootCauses.ToList(),
                "Mutation und erneute Validierung vorbereiten");
        }

        candidates = candidates
            .Select(candidate =>
            {
                var updated = candidate;
                if (parameterPlanner is not null && candidate.MutationType.Equals("parameter_range_refine", StringComparison.OrdinalIgnoreCase))
                {
                    updated = updated with
                    {
                        WhySuggested = $"{updated.WhySuggested} Parameter-Research bestätigt angrenzende Range-Klassen.",
                        SuggestedNextValidation = $"{updated.SuggestedNextValidation} Evidence aus Parameter Research einbinden."
                    };
                }

                if (tradingSynthesizer is not null)
                {
                    updated = updated with
                    {
                        WhySuggested = updated.WhySuggested.Contains("Research", StringComparison.OrdinalIgnoreCase)
                            ? updated.WhySuggested
                            : $"{updated.WhySuggested} Trading Research Synthesizer zeigt passende Parameterklassen und offene Fragen."
                    };
                }

                return updated;
            })
            .ToList();

        return candidates
            .DistinctBy(item => item.MutationType, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildOperatorSummary(
        StrategyBacktestJobPlan job,
        IReadOnlyList<FailureGuidedMutationCandidate> candidates,
        string learningDecision,
        string qualityClass,
        int trades,
        double winRate,
        double profitFactor,
        double maxDrawdown,
        double expectancy)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{job.StrategyPattern} · {job.Asset} {job.Timeframe}");
        sb.AppendLine();
        sb.AppendLine("Hermes hat aus dem negativen Test gelernt.");
        sb.AppendLine("Die alte Variante wird nicht weiter als robust behandelt.");
        sb.AppendLine();
        sb.AppendLine("Blocker:");
        sb.AppendLine($"- Trades: {trades}");
        sb.AppendLine($"- Winrate: {winRate:0.####}");
        sb.AppendLine($"- Profit Factor: {profitFactor:0.####}");
        sb.AppendLine($"- Max Drawdown: {maxDrawdown:0.####}");
        sb.AppendLine($"- Expectancy: {expectancy:0.####}");
        sb.AppendLine($"- Quality: {qualityClass}");
        sb.AppendLine();
        sb.AppendLine("Lernentscheidung:");
        sb.AppendLine(learningDecision);
        sb.AppendLine();
        sb.AppendLine("Vorbereitete Mutationen:");
        foreach (var candidate in candidates.Take(8))
        {
            sb.AppendLine($"- {candidate.Title}: {candidate.WhySuggested}");
        }
        sb.AppendLine();
        sb.AppendLine("Frank nötig:");
        sb.AppendLine("nein");
        return sb.ToString().TrimEnd();
    }

    private void WriteArtifacts(FailureGuidedMutationPlannerReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, markdown);
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(FailureGuidedMutationPlannerReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Failure Guided Mutation Planner");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Source job: {report.SourceBacktestJobId}");
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
        sb.AppendLine($"- Knowledge update tag: {report.KnowledgeUpdateTag}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Mutation Candidates");
        foreach (var candidate in report.MutationCandidates)
        {
            sb.AppendLine($"- {candidate.Title} [{candidate.Priority}]");
            sb.AppendLine($"  - Type: {candidate.MutationType}");
            sb.AppendLine($"  - Description: {candidate.Description}");
            sb.AppendLine($"  - Why: {candidate.WhySuggested}");
            sb.AppendLine($"  - Benefit: {candidate.ExpectedBenefit}");
            sb.AppendLine($"  - Next validation: {candidate.SuggestedNextValidation}");
        }
        return sb.ToString();
    }

    private static string Classify(int trades)
        => trades < 30 ? "insufficient_sample"
            : trades <= 100 ? "low_confidence"
            : trades <= 300 ? "medium_confidence"
            : "high_confidence";
}
