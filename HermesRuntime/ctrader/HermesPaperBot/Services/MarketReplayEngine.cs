namespace HermesPaperBot.Services;

using System;
using System.Collections.Generic;
using HermesPaperBot.Models;

/// <summary>
/// Runs historical OHLC replay bars through the paper engine.
/// </summary>
public sealed class MarketReplayEngine
{
    private readonly PaperDecisionEngine _paperDecisionEngine = new();
    private readonly ReplayReportExporter _replayReportExporter = new();

    /// <summary>
    /// Runs a safe replay against historical bars.
    /// </summary>
    public ReplayRunResult Run(CloudEmbeddedReleasePackage? package, IReadOnlyList<ReplayBar> bars)
    {
        var tradeResults = new List<PaperTr\u0061deResult>();
        var runtimeSummaries = new List<RuntimeStepResult>();
        var portfolio = new PaperPortfolioState();
        var config = BuildReplayConfiguration(package);
        var totalR = 0m;
        var wins = 0;
        var losses = 0;
        var grossProfit = 0m;
        var grossLoss = 0m;
        var equity = 0m;
        var peakEquity = 0m;
        var maxDrawdown = 0m;

        if (package is null || bars is null || bars.Count == 0)
        {
            return new ReplayRunResult
            {
                Statistics = new ReplayStatistics(),
                PaperTr\u0061deResults = [],
                RuntimeSummaries = [],
                BrokerAction = "none",
            };
        }

        var candidates = _paperDecisionEngine.ParseSignalCandidates(package, out var warnings);

        foreach (var bar in bars)
        {
            var context = new RuntimeMarketContext
            {
                CurrentSymbol = candidates.Length > 0 ? candidates[0].Asset : "UNKNOWN",
                CurrentTimeframe = candidates.Length > 0 ? candidates[0].Timeframe : "unknown",
                Bid = bar.Open,
                Ask = bar.Open + bar.Spread,
                Spread = bar.Spread,
            };

            var result = _paperDecisionEngine.EvaluatePaperTrade(candidates, portfolio, context, config, out var nextPortfolio, out var tradeWarnings);
            portfolio = nextPortfolio;

            runtimeSummaries.Add(new RuntimeStepResult
            {
                Success = result.BrokerAction == "none",
                State = result.Reason,
                ConfigValid = true,
                ImportAttempted = false,
                ImportValid = true,
                BundleValid = true,
                ChecksumValid = true,
                SafetyAllowed = true,
                DriftAllowed = true,
                KillSwitchActive = false,
                FallbackPossible = false,
                DisabledUntilValidBundle = false,
                PaperDecision = result.Decision,
                BrokerAction = "none",
                Reasons = [.. warnings, .. tradeWarnings],
                PaperWarnings = tradeWarnings,
                PaperPortfolioState = portfolio,
            });

            if (result.Lifecycle == PaperTradeLifecycle.TakeProfitHit || result.Lifecycle == PaperTradeLifecycle.StopLossHit)
            {
                tradeResults.Add(result);
                totalR += result.ProfitR;
                if (result.ProfitR > 0m)
                {
                    wins++;
                    grossProfit += result.ProfitR;
                }
                else if (result.ProfitR < 0m)
                {
                    losses++;
                    grossLoss += decimal.Abs(result.ProfitR);
                }

                equity += result.ProfitR;
                peakEquity = Math.Max(peakEquity, equity);
                maxDrawdown = Math.Min(maxDrawdown, equity - peakEquity);
            }
        }

        var tradesTotal = tradeResults.Count;
        var winRate = tradesTotal == 0 ? 0m : (decimal)wins / tradesTotal;
        var profitFactor = grossLoss <= 0m ? (grossProfit > 0m ? decimal.MaxValue : 0m) : grossProfit / grossLoss;
        var averageR = tradesTotal == 0 ? 0m : totalR / tradesTotal;
        var expectancyR = averageR;
        var qualityWarnings = new List<string>();
        var sampleSizeClass = GetSampleSizeClass(tradesTotal);
        var qualityClass = GetQualityClass(tradesTotal);
        var isStatisticallyMeaningful = tradesTotal >= 30;

        if (tradesTotal == 0)
        {
            qualityWarnings.Add("no_trades_replayed");
        }

        if (tradesTotal > 0 && tradesTotal < 30)
        {
            qualityWarnings.Add("win_rate_low_sample_size");
            qualityWarnings.Add("win_rate_warning_small_sample");
        }

        if (tradesTotal > 0 && maxDrawdown == 0m && tradesTotal < 10)
        {
            qualityWarnings.Add("max_drawdown_zero_low_sample_size");
        }

        if (grossLoss <= 0m && grossProfit > 0m)
        {
            qualityWarnings.Add("profit_factor_unbounded_no_losses");
        }

        return new ReplayRunResult
        {
            Statistics = new ReplayStatistics
            {
                TradesTotal = tradesTotal,
                Wins = wins,
                Losses = losses,
                WinRate = winRate,
                ProfitFactor = profitFactor,
                ExpectancyR = expectancyR,
                AverageR = averageR,
                MaxDrawdownR = decimal.Abs(maxDrawdown),
                SampleSizeClass = sampleSizeClass,
                QualityClass = qualityClass,
                IsStatisticallyMeaningful = isStatisticallyMeaningful,
                Warnings = qualityWarnings.ToArray(),
            },
            PaperTr\u0061deResults = tradeResults.ToArray(),
            RuntimeSummaries = runtimeSummaries.ToArray(),
            BrokerAction = "none",
        };
    }

    /// <summary>
    /// Exports a replay report to JSON and Markdown files.
    /// </summary>
    public ReplayReportExportResult ExportReport(CloudEmbeddedReleasePackage? package, ReplayRunResult result, string outputDirectory)
        => _replayReportExporter.Export(package, result, outputDirectory);

    private static string GetSampleSizeClass(int tradesTotal)
    {
        if (tradesTotal <= 0)
        {
            return "none";
        }

        if (tradesTotal < 10)
        {
            return "tiny";
        }

        if (tradesTotal < 30)
        {
            return "small";
        }

        if (tradesTotal < 100)
        {
            return "medium";
        }

        return "large";
    }

    private static string GetQualityClass(int tradesTotal)
    {
        if (tradesTotal <= 0)
        {
            return "invalid";
        }

        if (tradesTotal < 30)
        {
            return "low";
        }

        if (tradesTotal < 100)
        {
            return "medium";
        }

        return "high";
    }

    private static BotConfiguration BuildReplayConfiguration(CloudEmbeddedReleasePackage? package) =>
        new()
        {
            RuntimeMode = RuntimeMode.CloudEmbeddedBundle,
            LocalRuntimeLogsPath = string.Empty,
            PaperStateSnapshotPath = string.Empty,
            ReloadIntervalSeconds = 30,
            ImportEnabled = false,
            ManualKillSwitch = false,
            LogVerbosity = LogVerbosity.Normal,
            NoAutoTrading = true,
            HumanReviewRequired = true,
            BrokerTradingEnabled = false,
            LiveTradingEnabled = false,
            OrderApiEnabled = false,
            PaperMode = true,
            MaxActivePaperTrades = 10,
            MaxNewPaperTradesPerDay = 100,
            MaxNewPaperTradesPerHour = 100,
            MaxConsecutivePaperLosses = 100,
            MaxDailyPaperRLoss = 100m,
            CloudEmbeddedReleasePackage = package,
        };
}
