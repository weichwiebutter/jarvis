using System.Text;
using System.Text.Json;
using HermesPaperBot.Models;
using HermesPaperBot.Services;

namespace Hermes.Runtime;

public sealed record PaperRuntimeStepReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    bool RuntimeReady,
    PaperBotRuntimeSelfCheckReport RuntimeSelfCheck,
    bool EmbeddedPackageLoaded,
    bool SignalPackageLoaded,
    bool ChartAnnotationSpecLoaded,
    bool SafetyFlagsActive,
    bool CloudMode,
    bool BrokerActionNone,
    bool MarketContextLoaded,
    string MarketContextSource,
    string MarketSymbol,
    string MarketTimeframe,
    decimal MarketBid,
    decimal MarketAsk,
    decimal? MarketSpreadPips,
    DateTimeOffset MarketServerTimeUtc,
    PaperSignalEvaluationReport SignalEvaluation,
    int EvaluatedSignals,
    int ActionableSignals,
    int SkippedSignals,
    string PaperDecisionSummary,
    string SignalEvaluationReportPath,
    string SignalEvaluationMarkdownPath,
    RuntimeStepResult RuntimeStepResult,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Recommendations,
    string ReportPath,
    string MarkdownPath,
    string LogsPath);

public sealed class PaperRuntimeStepService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public PaperRuntimeStepService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "paper_runtime_step");
    public string ReportPath => Path.Combine(Root, "paper_runtime_step.json");
    public string MarkdownPath => Path.Combine(Root, "paper_runtime_step.md");
    public string LogsPath => Path.Combine(_storagePaths.Root, "logs", "paper_runtime");

    public PaperRuntimeStepReport LoadLatestReport()
    {
        if (!File.Exists(ReportPath))
        {
            return Run();
        }

        try
        {
            var report = JsonSerializer.Deserialize<PaperRuntimeStepReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
            if (report is null || report.SignalEvaluation is null)
            {
                return Run();
            }

            return report;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Run();
        }
    }

    public PaperRuntimeStepReport Run()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogsPath);

        var selfCheckService = new PaperBotRuntimeSelfCheckService(_storagePaths, _runtimeRoot);
        var selfCheck = selfCheckService.Run();
        var bootstrapper = new CloudEmbeddedPackageBootstrapper();
        var signalEvaluationService = new PaperSignalEvaluationService(_storagePaths, _runtimeRoot);
        var bootstrap = bootstrapper.CreateCloudConfiguration();
        var warnings = new List<string>(selfCheck.Warnings);
        var recommendations = new List<string>(selfCheck.Recommendations);

        if (!bootstrap.Success || bootstrap.Configuration is null)
        {
            var blockedResult = new RuntimeStepResult
            {
                Success = false,
                State = "blocked_by_config",
                ConfigValid = false,
                ImportAttempted = false,
                ImportValid = false,
                BundleValid = false,
                ChecksumValid = false,
                SafetyAllowed = false,
                DriftAllowed = false,
                KillSwitchActive = true,
                FallbackPossible = false,
                DisabledUntilValidBundle = true,
                PaperDecision = "would_block_by_safety",
                BrokerAction = "none",
                Reasons = [bootstrap.Reason ?? "cloud_bootstrap_failed"],
                MarketContext = new RuntimeMarketContext { Source = "unavailable" },
                MarketContextSeen = false,
            };
            var blockedSignalEvaluation = signalEvaluationService.Run(null, null);
            warnings.AddRange(blockedSignalEvaluation.Warnings);
            recommendations.AddRange(blockedSignalEvaluation.Recommendations);

            var blockedReport = BuildReport(
                selfCheck,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                new RuntimeMarketContext { Source = "unavailable" },
                blockedSignalEvaluation,
                blockedSignalEvaluation.EvaluatedSignals,
                blockedSignalEvaluation.ActionableSignals,
                blockedSignalEvaluation.SkippedSignals,
                blockedSignalEvaluation.PaperDecisionSummary,
                blockedSignalEvaluation.ReportPath,
                blockedSignalEvaluation.MarkdownPath,
                false,
                blockedResult,
                warnings,
                recommendations);

            WriteReport(blockedReport);
            return blockedReport;
        }

        var configuration = bootstrap.Configuration;
        var embeddedPackage = configuration.CloudEmbeddedReleasePackage;
        var signalPackageLoaded = embeddedPackage is not null && !string.IsNullOrWhiteSpace(embeddedPackage.EmbeddedStrategyJson) && TryParseJson(embeddedPackage.EmbeddedStrategyJson);
        var chartAnnotationSpecLoaded = embeddedPackage is not null && !string.IsNullOrWhiteSpace(embeddedPackage.ChartAnnotationSpecJson) && TryParseJson(embeddedPackage.ChartAnnotationSpecJson);
        var marketContext = LoadMarketContext(_storagePaths, _runtimeRoot, embeddedPackage, out var marketContextLoaded, out var marketContextWarnings);
        warnings.AddRange(marketContextWarnings);

        var signalEvaluation = signalEvaluationService.Run(configuration, marketContext);
        warnings.AddRange(signalEvaluation.Warnings);
        recommendations.AddRange(signalEvaluation.Recommendations);

        var orchestrator = new PaperRuntimeOrchestrator();
        var runtimeResult = orchestrator.RunStep(configuration, marketContext);

        var report = BuildReport(
            selfCheck,
            bootstrap.Success,
            embeddedPackage is not null,
            signalPackageLoaded,
            chartAnnotationSpecLoaded,
            marketContextLoaded,
            runtimeResult.SafetyAllowed,
            string.Equals(runtimeResult.BrokerAction, "none", StringComparison.OrdinalIgnoreCase),
            marketContext,
            signalEvaluation,
            signalEvaluation.EvaluatedSignals,
            signalEvaluation.ActionableSignals,
            signalEvaluation.SkippedSignals,
            signalEvaluation.PaperDecisionSummary,
            signalEvaluation.ReportPath,
            signalEvaluation.MarkdownPath,
            runtimeResult.Success && !runtimeResult.KillSwitchActive && runtimeResult.BrokerAction.Equals("none", StringComparison.OrdinalIgnoreCase) && selfCheck.RuntimeReady,
            runtimeResult,
            warnings,
            recommendations);

        if (!runtimeResult.Success)
        {
            recommendations.Add("review the paper runtime step reasons and the bot runtime summary");
        }

        WriteReport(report);
        return report;
    }

    private static RuntimeMarketContext LoadMarketContext(StoragePaths storagePaths, string runtimeRoot, CloudEmbeddedReleasePackage? package, out bool loaded, out List<string> warnings)
    {
        warnings = [];
        loaded = false;

        var quoteService = new CTraderReadOnlyQuoteService(storagePaths, runtimeRoot);
        try
        {
            var quotes = quoteService.LoadQuotes().Where(quote => quote.Status == "available").ToList();
            var quote = quotes.FirstOrDefault();
            if (quote is not null)
            {
                loaded = true;
                return new RuntimeMarketContext
                {
                    Symbol = quote.Asset,
                    Timeframe = ResolveTimeframe(package),
                    Bid = Convert.ToDecimal(quote.Bid ?? 0d),
                    Ask = Convert.ToDecimal(quote.Ask ?? 0d),
                    Spread = Convert.ToDecimal(quote.Spread ?? 0d),
                    SpreadPips = quote.Spread.HasValue ? Convert.ToDecimal(quote.Spread.Value) : null,
                    TickSize = 0m,
                    PipSize = 0m,
                    ServerTime = quote.TimestampUtc ?? DateTimeOffset.UtcNow,
                    Source = "cTrader_read_only_quote",
                };
            }

            warnings.Add("market_context_quote_unavailable");
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException)
        {
            warnings.Add($"market_context_read_failed:{ex.Message}");
        }

        var currentMarketService = new CurrentMarketSnapshotService(storagePaths, runtimeRoot);
        try
        {
            var current = currentMarketService.LoadSnapshot().FirstOrDefault(snapshot => snapshot.Status == "available");
            if (current is not null)
            {
                loaded = true;
                return new RuntimeMarketContext
                {
                    Symbol = current.Asset,
                    Timeframe = ResolveTimeframe(package),
                    Bid = Convert.ToDecimal(current.Bid ?? 0d),
                    Ask = Convert.ToDecimal(current.Ask ?? 0d),
                    Spread = Convert.ToDecimal(current.Spread ?? 0d),
                    SpreadPips = current.Spread.HasValue ? Convert.ToDecimal(current.Spread.Value) : null,
                    TickSize = 0m,
                    PipSize = 0m,
                    ServerTime = current.TimestampUtc ?? DateTimeOffset.UtcNow,
                    Source = "current_market_snapshot",
                };
            }

            warnings.Add("current_market_snapshot_unavailable");
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            warnings.Add($"current_market_snapshot_read_failed:{ex.Message}");
        }

        return new RuntimeMarketContext
        {
            Symbol = package is null ? "EURUSD" : ResolveFallbackSymbol(package),
            Timeframe = ResolveTimeframe(package),
            Bid = 0m,
            Ask = 0m,
            Spread = 0m,
            TickSize = 0m,
            PipSize = 0m,
            ServerTime = DateTimeOffset.UtcNow,
            Source = "missing_market_context",
        };
    }

    private static string ResolveTimeframe(CloudEmbeddedReleasePackage? package)
    {
        var strategy = package?.EmbeddedStrategyJson;
        if (string.IsNullOrWhiteSpace(strategy))
        {
            return "M5";
        }

        try
        {
            using var document = JsonDocument.Parse(strategy);
            var root = document.RootElement;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("timeframe", out var timeframe) && timeframe.ValueKind == JsonValueKind.String)
                    {
                        var value = timeframe.GetString();
                        if (!string.IsNullOrWhiteSpace(value) && !string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase))
                        {
                            return value;
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return "M5";
    }

    private static string ResolveFallbackSymbol(CloudEmbeddedReleasePackage package)
    {
        try
        {
            using var document = JsonDocument.Parse(package.EmbeddedStrategyJson ?? string.Empty);
            var root = document.RootElement;
            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("asset", out var symbol) && symbol.ValueKind == JsonValueKind.String)
                    {
                        var value = symbol.GetString();
                        if (!string.IsNullOrWhiteSpace(value))
                        {
                            return value;
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return "EURUSD";
    }

    private PaperRuntimeStepReport BuildReport(
        PaperBotRuntimeSelfCheckReport selfCheck,
        bool bootstrapSuccess,
        bool embeddedPackageLoaded,
        bool signalPackageLoaded,
        bool chartAnnotationSpecLoaded,
        bool marketContextLoaded,
        bool safetyFlagsActive,
        bool brokerActionNone,
        RuntimeMarketContext marketContext,
        PaperSignalEvaluationReport signalEvaluation,
        int evaluatedSignals,
        int actionableSignals,
        int skippedSignals,
        string paperDecisionSummary,
        string signalEvaluationReportPath,
        string signalEvaluationMarkdownPath,
        bool runtimeReady,
        RuntimeStepResult runtimeResult,
        List<string> warnings,
        List<string> recommendations)
    {
        warnings.AddRange(runtimeResult.Reasons);
        if (runtimeResult.PaperWarnings.Length > 0)
        {
            warnings.AddRange(runtimeResult.PaperWarnings);
        }

        return new PaperRuntimeStepReport(
            ReportVersion: "paper_runtime_step_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: runtimeReady ? "ready" : "partial",
            RuntimeReady: runtimeReady,
            RuntimeSelfCheck: selfCheck,
            EmbeddedPackageLoaded: embeddedPackageLoaded,
            SignalPackageLoaded: signalPackageLoaded,
            ChartAnnotationSpecLoaded: chartAnnotationSpecLoaded,
            SafetyFlagsActive: safetyFlagsActive,
            CloudMode: bootstrapSuccess,
            BrokerActionNone: brokerActionNone,
            MarketContextLoaded: marketContextLoaded,
            MarketContextSource: marketContext.Source,
            MarketSymbol: marketContext.Symbol,
            MarketTimeframe: marketContext.Timeframe,
            MarketBid: marketContext.Bid,
            MarketAsk: marketContext.Ask,
            MarketSpreadPips: marketContext.SpreadPips,
            MarketServerTimeUtc: marketContext.ServerTime,
            SignalEvaluation: signalEvaluation,
            EvaluatedSignals: evaluatedSignals,
            ActionableSignals: actionableSignals,
            SkippedSignals: skippedSignals,
            PaperDecisionSummary: paperDecisionSummary,
            SignalEvaluationReportPath: signalEvaluationReportPath,
            SignalEvaluationMarkdownPath: signalEvaluationMarkdownPath,
            RuntimeStepResult: CloneRuntimeResult(runtimeResult, marketContext, marketContextLoaded),
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Recommendations: recommendations.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            LogsPath: LogsPath);
    }

    private static RuntimeStepResult CloneRuntimeResult(RuntimeStepResult runtimeResult, RuntimeMarketContext marketContext, bool marketContextSeen)
        => new()
        {
            Success = runtimeResult.Success,
            State = runtimeResult.State,
            ConfigValid = runtimeResult.ConfigValid,
            ImportAttempted = runtimeResult.ImportAttempted,
            ImportValid = runtimeResult.ImportValid,
            BundleValid = runtimeResult.BundleValid,
            ChecksumValid = runtimeResult.ChecksumValid,
            SafetyAllowed = runtimeResult.SafetyAllowed,
            DriftAllowed = runtimeResult.DriftAllowed,
            KillSwitchActive = runtimeResult.KillSwitchActive,
            FallbackPossible = runtimeResult.FallbackPossible,
            DisabledUntilValidBundle = runtimeResult.DisabledUntilValidBundle,
            PaperDecision = runtimeResult.PaperDecision,
            BrokerAction = runtimeResult.BrokerAction,
            Reasons = runtimeResult.Reasons,
            LoggingStatus = runtimeResult.LoggingStatus,
            SignalSeen = runtimeResult.SignalSeen,
            SignalDirection = runtimeResult.SignalDirection,
            SignalConfidence = runtimeResult.SignalConfidence,
            SignalExpired = runtimeResult.SignalExpired,
            SignalCandidates = runtimeResult.SignalCandidates,
            PaperPortfolioState = runtimeResult.PaperPortfolioState,
            PaperTr\u0061deResult = runtimeResult.PaperTr\u0061deResult,
            PaperWarnings = runtimeResult.PaperWarnings,
            MarketContext = marketContext,
            MarketContextSeen = marketContextSeen,
            PaperPositionOpen = runtimeResult.PaperPositionOpen,
            PaperPositionStatus = runtimeResult.PaperPositionStatus,
            PaperExitReason = runtimeResult.PaperExitReason,
            RMultiple = runtimeResult.RMultiple,
            PositionId = runtimeResult.PositionId,
        };

    private static bool TryParseJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            using var _ = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void WriteReport(PaperRuntimeStepReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(PaperRuntimeStepReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Paper Runtime Step");
        sb.AppendLine();
        sb.AppendLine($"- report_version: {report.ReportVersion}");
        sb.AppendLine($"- updated_at_utc: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- status: {report.Status}");
        sb.AppendLine($"- runtime_ready: {report.RuntimeReady.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- cloud_mode: {report.CloudMode.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- broker_action_none: {report.BrokerActionNone.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- embedded_package_loaded: {report.EmbeddedPackageLoaded.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- signal_package_loaded: {report.SignalPackageLoaded.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- chart_annotation_spec_loaded: {report.ChartAnnotationSpecLoaded.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- safety_flags_active: {report.SafetyFlagsActive.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- market_context_loaded: {report.MarketContextLoaded.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- evaluated_signals: {report.EvaluatedSignals}");
        sb.AppendLine($"- actionable_signals: {report.ActionableSignals}");
        sb.AppendLine($"- skipped_signals: {report.SkippedSignals}");
        sb.AppendLine($"- paper_decision_summary: {report.PaperDecisionSummary}");
        sb.AppendLine();
        sb.AppendLine("## Signal Evaluation");
        sb.AppendLine($"- report_path: {report.SignalEvaluationReportPath}");
        sb.AppendLine($"- markdown_path: {report.SignalEvaluationMarkdownPath}");
        sb.AppendLine($"- status: {report.SignalEvaluation.Status}");
        sb.AppendLine($"- evaluated_signals: {report.SignalEvaluation.EvaluatedSignals}");
        sb.AppendLine($"- actionable_signals: {report.SignalEvaluation.ActionableSignals}");
        sb.AppendLine($"- skipped_signals: {report.SignalEvaluation.SkippedSignals}");
        sb.AppendLine($"- waiting_signals: {report.SignalEvaluation.WaitingSignals}");
        sb.AppendLine($"- paper_decision_summary: {report.SignalEvaluation.PaperDecisionSummary}");
        sb.AppendLine();
        sb.AppendLine("## Market Context");
        sb.AppendLine($"- source: {report.MarketContextSource}");
        sb.AppendLine($"- symbol: {report.MarketSymbol}");
        sb.AppendLine($"- timeframe: {report.MarketTimeframe}");
        sb.AppendLine($"- bid: {report.MarketBid}");
        sb.AppendLine($"- ask: {report.MarketAsk}");
        sb.AppendLine($"- spread_pips: {(report.MarketSpreadPips.HasValue ? report.MarketSpreadPips.Value.ToString() : "n/a")}");
        sb.AppendLine($"- server_time_utc: {report.MarketServerTimeUtc:O}");
        sb.AppendLine();
        sb.AppendLine("## Runtime Decision");
        sb.AppendLine($"- state: {report.RuntimeStepResult.State}");
        sb.AppendLine($"- success: {report.RuntimeStepResult.Success.ToString().ToLowerInvariant()}");
        sb.AppendLine($"- paper_decision: {report.RuntimeStepResult.PaperDecision}");
        sb.AppendLine($"- broker_action: {report.RuntimeStepResult.BrokerAction}");
        sb.AppendLine($"- market_context_seen: {report.RuntimeStepResult.MarketContextSeen.ToString().ToLowerInvariant()}");
        sb.AppendLine();
        sb.AppendLine("## Warnings");
        foreach (var warning in report.Warnings)
        {
            sb.AppendLine($"- {warning}");
        }
        if (report.Warnings.Count == 0)
        {
            sb.AppendLine("- none");
        }
        sb.AppendLine();
        sb.AppendLine("## Recommendations");
        foreach (var recommendation in report.Recommendations)
        {
            sb.AppendLine($"- {recommendation}");
        }
        if (report.Recommendations.Count == 0)
        {
            sb.AppendLine("- none");
        }

        return sb.ToString();
    }
}
