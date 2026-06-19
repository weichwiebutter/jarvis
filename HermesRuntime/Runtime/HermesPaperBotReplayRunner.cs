namespace Hermes.Runtime;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HermesPaperBot;
using HermesPaperBot.Models;
using HermesPaperBot.Services;

/// <summary>
/// Runs a small local HermesPaperBot replay and exports a report.
/// </summary>
public sealed record HermesPaperBotReplayRunResult(
    bool Success,
    string Status,
    string Reason,
    string OutputDirectory,
    string JsonPath,
    string MarkdownPath,
    string DatasetPath,
    bool DatasetDiscoveryUsed,
    int DatasetDiscoveryCandidates,
    string SelectedDatasetPath,
    int BarsTotal,
    int BarsValid,
    int BarsSkipped,
    int TradesTotal,
    string SampleSizeClass,
    string QualityClass,
    string BrokerAction,
    bool PaperModeAllowed,
    string[] Warnings);

/// <summary>
/// Small CLI-facing replay runner for the paper bot.
/// </summary>
public sealed class HermesPaperBotReplayRunner
{
    /// <summary>
    /// Runs the local sample replay and exports the replay report.
    /// </summary>
    /// <summary>
    /// Runs the local sample replay or a discovered dataset replay.
    /// </summary>
    public HermesPaperBotReplayRunResult Run(string? outputDirectory = null, string? datasetPath = null, string? asset = null, string? timeframe = null)
    {
        try
        {
            var package = BuildReplayPackage();
            var output = ResolveOutputDirectory(outputDirectory);
            HermesPaperBotReplayDatasetLoadResult? dataset = null;
            IReadOnlyList<ReplayBar> bars;
            var discoveryUsed = false;
            var discoveryCandidates = 0;
            var selectedDatasetPath = string.Empty;

            if (string.IsNullOrWhiteSpace(datasetPath))
            {
                if (!string.IsNullOrWhiteSpace(asset) && !string.IsNullOrWhiteSpace(timeframe))
                {
                    discoveryUsed = true;
                    var loader = new HermesPaperBotReplayDatasetLoader();
                    var discovered = loader.Discover(asset, timeframe);
                    discoveryCandidates = discovered.Count;
                    if (discovered.Count == 0)
                    {
                        return new HermesPaperBotReplayRunResult(
                            Success: false,
                            Status: "blocked",
                            Reason: "dataset_discovery_no_match",
                            OutputDirectory: output,
                            JsonPath: string.Empty,
                            MarkdownPath: string.Empty,
                            DatasetPath: string.Empty,
                            DatasetDiscoveryUsed: true,
                            DatasetDiscoveryCandidates: 0,
                            SelectedDatasetPath: string.Empty,
                            BarsTotal: 0,
                            BarsValid: 0,
                            BarsSkipped: 0,
                            TradesTotal: 0,
                            SampleSizeClass: "none",
                            QualityClass: "invalid",
                            BrokerAction: "none",
                            PaperModeAllowed: true,
                            Warnings: ["dataset_discovery_no_match"]);
                    }

                    selectedDatasetPath = discovered[0];
                    dataset = loader.Load(selectedDatasetPath);
                    if (!dataset.Success)
                    {
                        return new HermesPaperBotReplayRunResult(
                            Success: false,
                            Status: "blocked",
                            Reason: dataset.Reason,
                            OutputDirectory: output,
                            JsonPath: string.Empty,
                            MarkdownPath: string.Empty,
                            DatasetPath: dataset.DatasetPath,
                            DatasetDiscoveryUsed: true,
                            DatasetDiscoveryCandidates: discoveryCandidates,
                            SelectedDatasetPath: selectedDatasetPath,
                            BarsTotal: dataset.BarsTotal,
                            BarsValid: dataset.BarsValid,
                            BarsSkipped: dataset.BarsSkipped,
                            TradesTotal: 0,
                            SampleSizeClass: "none",
                            QualityClass: "invalid",
                            BrokerAction: "none",
                            PaperModeAllowed: true,
                            Warnings: [.. dataset.Warnings]);
                    }

                    bars = dataset.Bars;
                }
                else
                {
                    bars = BuildSampleBars();
                }
            }
            else
            {
                dataset = new HermesPaperBotReplayDatasetLoader().Load(datasetPath);
                selectedDatasetPath = dataset.DatasetPath;
                if (!dataset.Success)
                {
                    return new HermesPaperBotReplayRunResult(
                        Success: false,
                        Status: "blocked",
                        Reason: dataset.Reason,
                        OutputDirectory: output,
                        JsonPath: string.Empty,
                        MarkdownPath: string.Empty,
                        DatasetPath: dataset.DatasetPath,
                        DatasetDiscoveryUsed: false,
                        DatasetDiscoveryCandidates: 0,
                        SelectedDatasetPath: dataset.DatasetPath,
                        BarsTotal: dataset.BarsTotal,
                        BarsValid: dataset.BarsValid,
                        BarsSkipped: dataset.BarsSkipped,
                        TradesTotal: 0,
                        SampleSizeClass: "none",
                        QualityClass: "invalid",
                        BrokerAction: "none",
                        PaperModeAllowed: true,
                        Warnings: [.. dataset.Warnings]);
                }

                bars = dataset.Bars;
            }

            var engine = new MarketReplayEngine();
            var replay = engine.Run(package, bars);
            var export = engine.ExportReport(package, replay, output, dataset, discoveryUsed, discoveryCandidates, selectedDatasetPath);

            return new HermesPaperBotReplayRunResult(
                Success: export.Success,
                Status: export.Success ? "completed" : "blocked",
                Reason: export.Success ? "replay_report_exported" : "replay_report_export_failed",
                OutputDirectory: export.ReportDirectory,
                JsonPath: export.JsonPath,
                MarkdownPath: export.MarkdownPath,
                DatasetPath: dataset?.DatasetPath ?? string.Empty,
                DatasetDiscoveryUsed: discoveryUsed,
                DatasetDiscoveryCandidates: discoveryCandidates,
                SelectedDatasetPath: selectedDatasetPath,
                BarsTotal: dataset?.BarsTotal ?? 0,
                BarsValid: dataset?.BarsValid ?? 0,
                BarsSkipped: dataset?.BarsSkipped ?? 0,
                TradesTotal: replay.Statistics.TradesTotal,
                SampleSizeClass: replay.Statistics.SampleSizeClass,
                QualityClass: replay.Statistics.QualityClass,
                BrokerAction: replay.BrokerAction,
                PaperModeAllowed: true,
                Warnings: [.. (dataset?.Warnings ?? []), .. export.Warnings, .. replay.Statistics.Warnings]);
        }
        catch (Exception ex)
        {
            return new HermesPaperBotReplayRunResult(
                Success: false,
                Status: "blocked",
                Reason: $"replay_runner_failed:{ex.GetType().Name}",
                OutputDirectory: ResolveOutputDirectory(outputDirectory),
                JsonPath: string.Empty,
                MarkdownPath: string.Empty,
                DatasetPath: datasetPath ?? string.Empty,
                DatasetDiscoveryUsed: false,
                DatasetDiscoveryCandidates: 0,
                SelectedDatasetPath: string.Empty,
                BarsTotal: 0,
                BarsValid: 0,
                BarsSkipped: 0,
                TradesTotal: 0,
                SampleSizeClass: "none",
                QualityClass: "invalid",
                BrokerAction: "none",
                PaperModeAllowed: true,
                Warnings: [$"replay_runner_failed:{ex.GetType().Name}"]);
        }
    }

    private static CloudEmbeddedReleasePackage BuildReplayPackage()
    {
        var embeddedStrategyJson = """
        {
          "release_mode": "paper_only",
          "assets": [
            {
              "asset": "EURUSD",
              "timeframe": "M5",
              "direction": "long",
              "setup_id": "replay_long_setup",
              "setup_name": "replay_long_setup",
              "primary_candidate": "replay_long_candidate",
              "readiness": "bot_ready",
              "paper_entry_enabled": true,
              "confidence_baseline": 0.9,
              "max_spread": 0.5,
              "stop_loss_r": 1,
              "take_profit_r": 1,
              "entry_logic": [
                "demo entry for replay"
              ],
              "exit_logic": [
                "exit on replay condition"
              ],
              "stop_loss_logic": [
                "replay stop loss"
              ],
              "take_profit_logic": [
                "replay take profit"
              ],
              "invalidation_logic": [
                "replay invalidation"
              ],
              "market_regime_tags": [],
              "session_tags": [],
              "risk_notes": [
                "research_only",
                "human_review_required",
                "no_auto_trading"
              ]
            }
          ]
        }
        """;

        return new CloudEmbeddedReleasePackage
        {
            BotReleaseId = EmbeddedReleasePackage.BotReleaseId,
            BotVersion = EmbeddedReleasePackage.BotVersion,
            StrategyPackageVersion = EmbeddedReleasePackage.StrategyPackageVersion,
            SchemaVersion = "ensemble_signal_agent_package.schema_v1",
            ReleaseMode = ReleaseMode.PaperOnly,
            SafetyFlags = new SafetyFlags
            {
                NoAutoTrading = true,
                HumanReviewRequired = true,
                BrokerTradingEnabled = false,
                LiveTradingEnabled = false,
                OrderApiEnabled = false,
                PaperMode = true,
                BrokerAction = "none",
            },
            ForbiddenCapabilities = new ForbiddenCapabilities
            {
                MarketOrderExecutionForbidden = true,
                LimitOrderPlacementForbidden = true,
                StopOrderPlacementForbidden = true,
                PositionModificationForbidden = true,
                PositionClosingForbidden = true,
                PendingOrderCancellationForbidden = true,
                ExternalNetworkAccessForbidden = true,
            },
            EmbeddedManifestJson = EmbeddedReleasePackage.PackageJson,
            EmbeddedStrategyJson = embeddedStrategyJson,
            EmbeddedChecksum = EmbeddedReleasePackage.EmbeddedChecksum,
        };
    }

    private static IReadOnlyList<ReplayBar> BuildSampleBars() =>
    [
        new ReplayBar
        {
            Timestamp = DateTimeOffset.Parse("2026-06-19T00:00:00Z"),
            Open = 100.0m,
            High = 100.4m,
            Low = 99.8m,
            Close = 100.1m,
            Spread = 0.1m,
        },
        new ReplayBar
        {
            Timestamp = DateTimeOffset.Parse("2026-06-19T00:05:00Z"),
            Open = 101.2m,
            High = 101.4m,
            Low = 100.9m,
            Close = 101.3m,
            Spread = 0.1m,
        },
        new ReplayBar
        {
            Timestamp = DateTimeOffset.Parse("2026-06-19T00:10:00Z"),
            Open = 101.2m,
            High = 101.5m,
            Low = 101.0m,
            Close = 101.4m,
            Spread = 0.1m,
        }
    ];

    private static string ResolveOutputDirectory(string? outputDirectory)
    {
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
            return outputDirectory;
        }

        var runtimeRoot = AppContext.BaseDirectory;
        var current = Directory.GetParent(runtimeRoot)?.Parent?.Parent?.Parent?.Parent?.FullName
            ?? Directory.GetCurrentDirectory();
        var preferred = Path.Combine(current, ".codex_artifacts", "reports", "hermes_paper_bot_replay");
        Directory.CreateDirectory(preferred);
        return preferred;
    }
}
