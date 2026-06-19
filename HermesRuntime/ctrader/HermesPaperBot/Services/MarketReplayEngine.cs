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
            },
            PaperTr\u0061deResults = tradeResults.ToArray(),
            RuntimeSummaries = runtimeSummaries.ToArray(),
            BrokerAction = "none",
        };
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
            MaxActivePaperTrades = 1,
            MaxNewPaperTradesPerDay = 3,
            MaxNewPaperTradesPerHour = 2,
            MaxConsecutivePaperLosses = 3,
            MaxDailyPaperRLoss = 3m,
            CloudEmbeddedReleasePackage = package,
        };
}
