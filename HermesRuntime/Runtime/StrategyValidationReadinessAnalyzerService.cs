using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record StrategyValidationReadinessItem(
    string ValidationPlanId,
    string QueueItemId,
    string StrategyPattern,
    string Asset,
    string Timeframe,
    bool BacktestReady,
    bool OosReady,
    bool ForwardReady,
    double ReadinessScore,
    double ExpectedInformationGain,
    double ValidationEffort,
    string Status,
    IReadOnlyList<string> MissingRequirements,
    IReadOnlyList<string> Blockers);

public sealed record StrategyValidationReadinessAnalyzerReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string QueuePath,
    int QueueItemsAnalyzed,
    int ReadyForBacktestCount,
    int WaitingForOosDataCount,
    int WaitingForForwardObservationCount,
    int BlockedCount,
    IReadOnlyList<StrategyValidationReadinessItem> Items,
    IReadOnlyList<StrategyValidationReadinessItem> TopReadyCandidates,
    IReadOnlyList<StrategyValidationReadinessItem> TopInformationGainCandidates,
    IReadOnlyList<StrategyValidationReadinessItem> TopLowEffortCandidates,
    IReadOnlyList<string> KnowledgeSourcesUsed,
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

public sealed class StrategyValidationReadinessAnalyzerService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;
    private string? _resolvedReportPath;
    private string? _resolvedMarkdownPath;

    public StrategyValidationReadinessAnalyzerService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "strategy_validation_readiness");

    public string ReportPath => _resolvedReportPath ?? Path.Combine(Root, "strategy_validation_readiness_analyzer.json");

    public string MarkdownPath => _resolvedMarkdownPath ?? Path.Combine(Root, "strategy_validation_readiness_analyzer.md");

    public StrategyValidationReadinessAnalyzerReport Run()
    {
        Directory.CreateDirectory(Root);

        var queuePath = Path.Combine(_storagePaths.Root, "queues", "strategy_validation_queue.json");
        var queueItems = LoadQueue(queuePath);
        var planner = new StrategyMutationValidationPlannerService(_storagePaths, _runtimeRoot).Load()
            ?? new StrategyMutationValidationPlannerService(_storagePaths, _runtimeRoot).Run();
        var synthService = new TradingResearchSynthesizerService(_storagePaths, _runtimeRoot);
        var synth = synthService.Load() ?? synthService.Run();
        var parameterPlannerService = new StrategyParameterResearchPlannerService(_storagePaths, _runtimeRoot);
        var parameterPlanner = parameterPlannerService.Load() ?? parameterPlannerService.Run();
        var forwardStatus = new ForwardTestService(_storagePaths, _runtimeRoot).LoadStatus();
        var knowledgeCatalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems();
        var datasetGateService = new StrategyDatasetGateService(_storagePaths, _runtimeRoot);
        var analysis = AnalyzeQueue(queueItems, planner, synth, parameterPlanner, datasetGateService, forwardStatus, knowledgeCatalog);

        var report = new StrategyValidationReadinessAnalyzerReport(
            ReportVersion: "strategy_validation_readiness_analyzer_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            QueuePath: queuePath,
            QueueItemsAnalyzed: analysis.Items.Count,
            ReadyForBacktestCount: analysis.ReadyForBacktestCount,
            WaitingForOosDataCount: analysis.WaitingForOosDataCount,
            WaitingForForwardObservationCount: analysis.WaitingForForwardObservationCount,
            BlockedCount: analysis.BlockedCount,
            Items: analysis.Items,
            TopReadyCandidates: analysis.Items.Where(item => item.Status == "ready_for_backtest").OrderByDescending(item => item.ExpectedInformationGain).ThenBy(item => item.ValidationEffort).Take(5).ToList(),
            TopInformationGainCandidates: analysis.Items.OrderByDescending(item => item.ExpectedInformationGain).ThenBy(item => item.ValidationEffort).Take(5).ToList(),
            TopLowEffortCandidates: analysis.Items.OrderBy(item => item.ValidationEffort).ThenByDescending(item => item.ExpectedInformationGain).Take(5).ToList(),
            KnowledgeSourcesUsed:
            [
                "strategy_validation_queue.json",
                "strategy_mutation_validation_planner.json",
                "trading_research_synthesizer.json",
                "strategy_parameter_research_planner.json",
                "certified_candidates",
                "forward_observations",
                "setup_registry.json",
                "knowledge_catalog.json",
            ],
            Warnings: analysis.Warnings,
            OperatorSummary: $"{analysis.Items.Count} Validierungsaufträge analysiert. {analysis.ReadyForBacktestCount} sofort testbar. {analysis.WaitingForOosDataCount} warten auf OOS-Daten. {analysis.WaitingForForwardObservationCount} warten auf Forward-Beobachtungen. {analysis.BlockedCount} blockiert. Frank nötig: nein.",
            SafetySummary: "no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true",
            FrankRequired: false,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath);

        WriteReport(report);
        return report;
    }

    public StrategyValidationReadinessAnalyzerReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StrategyValidationReadinessAnalyzerReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<StrategyValidationQueueItem> LoadQueue(string queuePath)
    {
        if (!File.Exists(queuePath))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<StrategyValidationQueueItem>>(File.ReadAllText(queuePath), JsonDefaults.SnapshotReadOptions) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return [];
        }
    }

    private static ReadinessAnalysis AnalyzeQueue(
        IReadOnlyList<StrategyValidationQueueItem> queueItems,
        StrategyMutationValidationPlannerReport planner,
        TradingResearchSynthesizerReport synth,
        StrategyParameterResearchPlannerReport parameterPlanner,
        StrategyDatasetGateService datasetGateService,
        ForwardTestStatusSnapshot? forwardStatus,
        IReadOnlyList<KnowledgeCatalogItem> knowledgeCatalog)
    {
        var forwardObservationCount = forwardStatus?.ForwardTestObservationsTotal ?? 0;
        var items = new List<StrategyValidationReadinessItem>();
        var warnings = new List<string>();

        foreach (var item in queueItems)
        {
            var asset = item.Asset.ToUpperInvariant();
            var datasetGate = datasetGateService.Evaluate(asset, item.Timeframe);
            var hasForwardObservations = forwardObservationCount > 0;
            var paramCandidate = parameterPlanner.Candidates.FirstOrDefault(candidate => candidate.SourcePattern.Equals(item.StrategyPattern, StringComparison.OrdinalIgnoreCase));
            var hypothesis = synth.Hypotheses.FirstOrDefault(entry => entry.PatternName.Equals(item.StrategyPattern, StringComparison.OrdinalIgnoreCase));
            var plan = planner.ValidationPlans.FirstOrDefault(entry => entry.ValidationPlanId.Equals(item.ValidationPlanId, StringComparison.OrdinalIgnoreCase));

            var missing = new List<string>();
            var blockers = new List<string>();
            var status = datasetGate.DatasetAvailable ? "ready_for_backtest" : "blocked";
            if (!datasetGate.DatasetAvailable)
            {
                missing.AddRange(datasetGate.MissingRequirements);
                blockers.AddRange(datasetGate.Warnings);
            }

            if (item.RequiredOosTest && forwardStatus is null)
            {
                missing.Add("oos_data");
                status = "waiting_for_oos_data";
            }

            if (item.RequiredForwardObservation && !hasForwardObservations)
            {
                missing.Add("forward_observation");
                status = status == "blocked" ? status : "waiting_for_forward_observation";
            }

            if (item.Asset.Equals("GER40", StringComparison.OrdinalIgnoreCase) && !item.StrategyPattern.Contains("Breakout", StringComparison.OrdinalIgnoreCase))
            {
                missing.Add("asset_specific_validation_context");
            }

            var readinessScore = ComputeReadinessScore(item, status, datasetGate.DatasetAvailable, hasForwardObservations, hypothesis, paramCandidate, plan);
            items.Add(new StrategyValidationReadinessItem(
                ValidationPlanId: item.ValidationPlanId,
                QueueItemId: item.QueueItemId,
                StrategyPattern: item.StrategyPattern,
                Asset: item.Asset,
                Timeframe: item.Timeframe,
                BacktestReady: status == "ready_for_backtest",
                OosReady: datasetGate.DatasetAvailable,
                ForwardReady: hasForwardObservations,
                ReadinessScore: readinessScore,
                ExpectedInformationGain: plan?.ExpectedInformationGain ?? hypothesis?.ExpectedInformationGain ?? 0,
                ValidationEffort: plan?.ValidationEffort ?? hypothesis?.ValidationEffort ?? 1,
                Status: status,
                MissingRequirements: missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                Blockers: blockers.Distinct(StringComparer.OrdinalIgnoreCase).ToList()));
        }

        var readyCount = items.Count(item => item.Status == "ready_for_backtest");
        var oosCount = items.Count(item => item.Status == "waiting_for_oos_data");
        var forwardCount = items.Count(item => item.Status == "waiting_for_forward_observation");
        var blockedCount = items.Count(item => item.Status == "blocked");
        if (blockedCount == 0 && queueItems.Count > 0)
        {
            warnings.Add("no_blocked_readiness_items_detected");
        }

        return new ReadinessAnalysis(items, readyCount, oosCount, forwardCount, blockedCount, warnings);
    }

    private static double ComputeReadinessScore(
        StrategyValidationQueueItem item,
        string status,
        bool hasDataset,
        bool hasForwardObservations,
        TradingResearchHypothesis? hypothesis,
        StrategyParameterMutationPlan? paramCandidate,
        StrategyMutationValidationPlan? plan)
    {
        var score = status switch
        {
            "ready_for_backtest" => 85,
            "waiting_for_oos_data" => 55,
            "waiting_for_forward_observation" => 45,
            _ => 20,
        };

        if (hasDataset)
        {
            score += 10;
        }

        if (hasForwardObservations)
        {
            score += 5;
        }

        score += (int)Math.Round((plan?.ExpectedInformationGain ?? hypothesis?.ExpectedInformationGain ?? 0) * 10);
        score -= (int)Math.Round((plan?.ValidationEffort ?? hypothesis?.ValidationEffort ?? 1) * 5);
        if (paramCandidate is not null)
        {
            score += 5;
        }

        return Math.Clamp(score, 0, 100);
    }

    private void WriteReport(StrategyValidationReadinessAnalyzerReport report)
    {
        var json = JsonSerializer.Serialize(report, JsonDefaults.WriteOptions);
        var markdown = BuildMarkdown(report);
        File.WriteAllText(ReportPath, json);
        File.WriteAllText(MarkdownPath, markdown);
        _resolvedReportPath = ReportPath;
        _resolvedMarkdownPath = MarkdownPath;
    }

    private static string BuildMarkdown(StrategyValidationReadinessAnalyzerReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Strategy Validation Readiness Analyzer");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Analyzed: {report.QueueItemsAnalyzed}");
        sb.AppendLine($"- Ready for backtest: {report.ReadyForBacktestCount}");
        sb.AppendLine($"- Waiting for OOS data: {report.WaitingForOosDataCount}");
        sb.AppendLine($"- Waiting for forward observation: {report.WaitingForForwardObservationCount}");
        sb.AppendLine($"- Blocked: {report.BlockedCount}");
        sb.AppendLine();
        sb.AppendLine("## Operator Summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("## Top Ready Candidates");
        foreach (var item in report.TopReadyCandidates)
        {
            sb.AppendLine($"- {item.StrategyPattern} @ {item.Asset} {item.Timeframe} · readiness={item.ReadinessScore:0}");
        }
        return sb.ToString();
    }

    private sealed record ReadinessAnalysis(
        IReadOnlyList<StrategyValidationReadinessItem> Items,
        int ReadyForBacktestCount,
        int WaitingForOosDataCount,
        int WaitingForForwardObservationCount,
        int BlockedCount,
        IReadOnlyList<string> Warnings);
}
