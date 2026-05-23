using Hermes.Runtime;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

var app = new HermesCli(args);
return app.Run();

internal sealed class HermesCli
{
    private const string CliVersion = "0.1.0";

    private readonly string[] _args;
    private readonly string _runtimeRoot;
    private readonly string _dataRoot;

    public HermesCli(string[] args)
    {
        _args = args;
        _runtimeRoot = ResolveRuntimeRoot(args);
        _dataRoot = ResolveDataRoot(_runtimeRoot);
    }

    public int Run()
    {
        var command = FirstCommand(_args);

        return command switch
        {
            "" or "help" or "--help" or "-h" => ShowHelp(),
            "health" => ShowHealth(),
            "setup-watch" => ShowSetupWatch(),
            "events" => ShowEvents(),
            "jobs" => ShowJobs(),
            "storage" => ShowStorage(),
            "ctrader-health" => ShowCTraderHealth(),
            "ctrader-symbols" => ShowCTraderSymbols(),
            "ctrader-auth-url" => ShowCTraderAuthUrl(),
            "ctrader-auth-code" => ExchangeCTraderAuthCode(),
            "ctrader-auth-status" => ShowCTraderAuthStatus(),
            "download-history" => DownloadCTraderHistory(),
            "import-csv" => ImportCsv(),
            "generate-features" => GenerateFeatures(),
            "run-nightly-research" => RunNightlyResearch(),
            "research-status" => ShowResearchStatus(),
            "research-report" => ShowResearchReport(),
            "run-beta-learning" => RunBetaLearning(),
            "beta-status" => ShowBetaStatus(),
            "update-research-memory" => UpdateResearchMemory(),
            "research-memory" => ShowResearchMemory(),
            "run-long-research" => RunLongResearch(),
            "run-research-autopilot" => RunResearchAutopilot(),
            "run-strategy-research" => RunStrategyResearch(),
            "strategy-research-status" => ShowStrategyResearchStatus(),
            "top-strategies" => ShowTopStrategies(),
            "research-insights" => ShowResearchInsights(),
            "strategy-clusters" => ShowStrategyClusters(),
            "pattern-catalog" => ShowPatternCatalog(),
            "pattern-performance" => ShowPatternPerformance(),
            "features" => ShowFeatures(),
            "signals" => ShowSignals(),
            "backtests" => ShowBacktests(),
            "outcomes" => ShowOutcomes(),
            "market-data" => ShowMarketData(),
            "version" => ShowVersion(),
            _ => UnknownCommand(command)
        };
    }

    private int ShowHelp()
    {
        WriteHeader("Hermes CLI Foundation");
        Console.WriteLine("Lokale Sicherheits-CLI fuer HermesRuntime. Status-Kommandos sind read-only; generate-features und run-nightly-research schreiben nur lokale Analyseartefakte.");
        Console.WriteLine();
        Console.WriteLine("Kommandos:");
        Console.WriteLine("  hermes health             RuntimeHealth anzeigen");
        Console.WriteLine("  hermes setup-watch        Setup-Watch-Kandidaten anzeigen");
        Console.WriteLine("  hermes events recent      letzte Runtime-Events anzeigen");
        Console.WriteLine("  hermes jobs               Queue/Jobjournale anzeigen");
        Console.WriteLine("  hermes storage            lokale Storage-Uebersicht anzeigen");
        Console.WriteLine("  hermes ctrader-health     cTrader Open API Health anzeigen");
        Console.WriteLine("  hermes ctrader-symbols    cTrader Symbol-Mapping anzeigen");
        Console.WriteLine("  hermes ctrader-auth-url   cTrader OAuth URL anzeigen");
        Console.WriteLine("  hermes ctrader-auth-code  OAuth Redirect-Code gegen Token tauschen");
        Console.WriteLine("  hermes ctrader-auth-status lokalen cTrader Token-Status anzeigen");
        Console.WriteLine("  hermes download-history   historische cTrader-Candles read-only laden oder Stub-Fallback nutzen");
        Console.WriteLine("  hermes import-csv         cTrader Candle-CSV lokal importieren");
        Console.WriteLine("  hermes generate-features  FeatureVectors aus lokalen Candle-Daten erzeugen");
        Console.WriteLine("  hermes run-nightly-research lokale Research-Pipeline ausfuehren");
        Console.WriteLine("  hermes research-status    letzten Nightly-Research-Report anzeigen");
        Console.WriteLine("  hermes research-report    letzten ResearchSummaryReport anzeigen");
        Console.WriteLine("  hermes run-beta-learning  Trading Learning Beta 1 lokal ausfuehren");
        Console.WriteLine("  hermes beta-status        letzten Trading Learning Beta Report anzeigen");
        Console.WriteLine("  hermes update-research-memory Research Memory Index aktualisieren");
        Console.WriteLine("  hermes research-memory    Research Memory Index anzeigen");
        Console.WriteLine("  hermes run-long-research  checkpointed Long-Run Research starten");
        Console.WriteLine("  hermes run-research-autopilot kombinierte Data-/Pattern-/Strategy-Research-Pipeline starten");
        Console.WriteLine("  hermes run-strategy-research adaptive Strategy-Research-Varianten bewerten");
        Console.WriteLine("  hermes strategy-research-status Strategy-Research-Memory anzeigen");
        Console.WriteLine("  hermes top-strategies     beste Strategy-Research-Varianten anzeigen");
        Console.WriteLine("  hermes research-insights  Strategy-Research-Insights anzeigen");
        Console.WriteLine("  hermes strategy-clusters  Strategy-Cluster anzeigen");
        Console.WriteLine("  hermes pattern-catalog    Strategy/Pattern Knowledge Base anzeigen");
        Console.WriteLine("  hermes pattern-performance Pattern-Fitness aggregiert anzeigen");
        Console.WriteLine("  hermes features           letzte Feature-JSONL-Zeilen anzeigen");
        Console.WriteLine("  hermes signals            letzte Signal-JSONL-Zeilen anzeigen");
        Console.WriteLine("  hermes backtests          Demo-Backtest-Reports anzeigen");
        Console.WriteLine("  hermes outcomes           Signal-Outcome-Reports anzeigen");
        Console.WriteLine("  hermes market-data        historische Candle-JSONL-Dateien anzeigen");
        Console.WriteLine("  hermes version            CLI-/Runtime-Version anzeigen");
        Console.WriteLine();
        Console.WriteLine("Start ohne Installation:");
        Console.WriteLine("  dotnet run --project ./cli/Hermes.Cli.csproj -- health");
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowHealth()
    {
        WriteHeader("Hermes Runtime Health");
        var path = Path.Combine(_dataRoot, "reports", "runtime_health.json");
        if (!TryLoadJson(path, out var root))
        {
            WriteWarning($"RuntimeHealth nicht gefunden: {path}");
            WriteSafety();
            return 0;
        }

        WriteField("Runtime State", GetString(root, "runtime_state", "runtimeState"));
        WriteField("Timestamp UTC", GetString(root, "timestamp_utc", "timestampUtc"));
        WriteField("Safe Mode", GetBoolText(root, "safe_mode", "safeMode"));
        WriteField("no_auto_trading", GetBoolText(root, "no_auto_trading", "noAutoTrading"));
        WriteField("human_review_required", GetBoolText(root, "human_review_required", "humanReviewRequired"));
        WriteField("Free Disk", $"{GetDouble(root, "free_disk_gb", "freeDiskGb"):0.##} GB");
        WriteField("Pending Jobs", GetInt(root, "pending_jobs", "pendingJobs").ToString());
        WriteField("Running Jobs", GetInt(root, "running_jobs", "runningJobs").ToString());
        WriteField("Failed Jobs", GetInt(root, "failed_jobs", "failedJobs").ToString());
        WriteField("Quarantined Jobs", GetInt(root, "quarantined_jobs", "quarantinedJobs").ToString());
        WriteField("Active Setup Watches", GetInt(root, "active_setup_watches", "activeSetupWatches").ToString());
        WriteField("Last Snapshot", GetString(root, "last_snapshot_id", "lastSnapshotId"));
        WriteField("Last Error", GetString(root, "last_error", "lastError") ?? "-");
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowSetupWatch()
    {
        WriteHeader("Hermes Setup Watch");
        var path = Path.Combine(_dataRoot, "setup_watch", "setup_watch.json");
        if (!TryLoadJson(path, out var root) || root.ValueKind != JsonValueKind.Array)
        {
            WriteWarning($"Setup-Watch-Datei nicht gefunden oder nicht lesbar: {path}");
            WriteSafety();
            return 0;
        }

        var count = 0;
        foreach (var item in root.EnumerateArray())
        {
            count++;
            WriteSubHeader($"{GetString(item, "symbol") ?? "UNKNOWN"} - {GetString(item, "bias") ?? "unknown"}");
            WriteField("Status", GetString(item, "status"));
            WriteField("Confidence", $"{GetDouble(item, "confidence") * 100:0}%");
            WriteField("Entry Zone", GetString(item, "entry_zone", "entryZone"));
            WriteField("Stop-Loss", GetString(item, "suggested_stop_loss", "suggestedStopLoss"));
            WriteField("Target", GetString(item, "suggested_target", "suggestedTarget"));
            WriteField("Invalidation", GetString(item, "invalidation_level", "invalidationLevel"));
            WriteField("Trigger", GetString(item, "trigger_condition", "triggerCondition"));
            WriteField("Time Window", $"{GetInt(item, "time_window_minutes", "timeWindowMinutes")} min");
            WriteField("Notes", GetString(item, "notes"));
            Console.WriteLine();
        }

        if (count == 0)
        {
            Console.WriteLine("Keine Setup-Watches vorhanden.");
        }

        WriteSafety();
        return 0;
    }

    private int ShowEvents()
    {
        var subCommand = CommandAt(_args, 1);
        if (subCommand is not ("recent" or ""))
        {
            return UnknownCommand($"events {subCommand}");
        }

        WriteHeader("Hermes Recent Runtime Events");
        var limit = ReadLimit(_args, 12);
        var files = FindEventFiles().ToList();
        if (files.Count == 0)
        {
            WriteWarning("Keine Runtime-Event-Dateien gefunden.");
            return 0;
        }

        var file = files[^1];
        Console.WriteLine($"Quelle: {DisplayPath(file)}");
        Console.WriteLine();

        var lines = File.ReadLines(file)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .TakeLast(limit)
            .ToList();

        foreach (var line in lines)
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var timestamp = GetString(root, "timestamp_utc", "timestampUtc") ?? "-";
                var eventType = GetString(root, "event_type", "eventType") ?? "UnknownEvent";
                var severity = GetString(root, "severity") ?? "Info";
                var source = GetString(root, "source") ?? "-";
                var message = TryGetProperty(root, out var payload, "payload")
                    ? GetString(payload, "message") ?? "-"
                    : "-";

                WriteEvent(timestamp, severity, eventType, source, message);
            }
            catch (JsonException)
            {
                WriteWarning("Ungueltige JSONL-Zeile uebersprungen.");
            }
        }

        WriteSafety();
        return 0;
    }

    private int ShowJobs()
    {
        WriteHeader("Hermes Queue Jobs");
        var jobsRoot = Path.Combine(_dataRoot, "jobs");
        var states = new[] { "pending", "running", "completed", "failed", "quarantined" };

        foreach (var state in states)
        {
            var directory = Path.Combine(jobsRoot, state);
            var jobFiles = Directory.Exists(directory)
                ? Directory.EnumerateFiles(directory, "*.job.json").OrderBy(path => path).ToList()
                : [];

            WriteField(CultureTitle(state), jobFiles.Count.ToString());
        }

        Console.WriteLine();
        var latestJobs = Directory.Exists(jobsRoot)
            ? Directory.EnumerateFiles(jobsRoot, "*.job.json", SearchOption.AllDirectories)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(8)
                .ToList()
            : [];

        if (latestJobs.Count == 0)
        {
            Console.WriteLine("Keine Job-Manifeste vorhanden.");
        }

        foreach (var jobFile in latestJobs)
        {
            if (!TryLoadJson(jobFile, out var root))
            {
                continue;
            }

            WriteSubHeader(GetString(root, "job_id", "jobId") ?? Path.GetFileName(jobFile));
            WriteField("Type", GetString(root, "job_type", "jobType"));
            WriteField("Status", GetString(root, "status"));
            WriteField("Priority", GetInt(root, "priority").ToString());
            WriteField("Requested By", GetString(root, "requested_by", "requestedBy"));
            WriteField("Path", DisplayPath(jobFile));
            Console.WriteLine();
        }

        WriteSafety();
        return 0;
    }

    private int ShowStorage()
    {
        WriteHeader("Hermes Storage");
        Console.WriteLine($"Runtime Root: {_runtimeRoot}");
        Console.WriteLine($"Data Root:    {_dataRoot}");
        Console.WriteLine();

        var healthPath = Path.Combine(_dataRoot, "reports", "runtime_health.json");
        if (TryLoadJson(healthPath, out var health))
        {
            WriteField("Free Disk", $"{GetDouble(health, "free_disk_gb", "freeDiskGb"):0.##} GB");
        }

        var profilePath = Path.Combine(_runtimeRoot, "config", "storage.profile.json");
        if (TryLoadJson(profilePath, out var profile))
        {
            WriteField("Minimum Free Disk", $"{GetInt(profile, "minimum_free_disk_mb", "minimumFreeDiskMb")} MB");
            WriteField("Profile", GetString(profile, "profile_name", "profileName"));
        }

        Console.WriteLine();
        foreach (var name in new[] { "cache", "events", "snapshots", "replays", "exports", "market_data", "jobs", "reports", "setup_watch", "archive" })
        {
            var directory = Path.Combine(_dataRoot, name);
            var size = Directory.Exists(directory) ? DirectorySize(directory) : 0;
            WriteField(name, FormatBytes(size));
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowFeatures()
    {
        WriteHeader("Hermes Feature Exports");
        var limit = ReadLimit(_args, 8);
        var files = FindExportFiles("features").ToList();
        if (files.Count == 0)
        {
            WriteWarning("Keine Feature-Export-Dateien gefunden.");
            WriteSafety();
            return 0;
        }

        var file = files[^1];
        Console.WriteLine($"Quelle: {DisplayPath(file)}");
        Console.WriteLine();

        foreach (var line in ReadRecentJsonlLines(file, limit))
        {
            if (!TryParseJsonLine(line, out var root))
            {
                WriteWarning("Ungueltige Feature-JSONL-Zeile uebersprungen.");
                continue;
            }

            WriteSubHeader($"{GetString(root, "symbol") ?? "UNKNOWN"} {GetString(root, "timeframe") ?? "-"}");
            WriteField("Timestamp UTC", GetString(root, "timestamp_utc", "timestampUtc"));
            if (TryGetProperty(root, out _, "simple_return", "simpleReturn"))
            {
                WriteField("Schema", "generated_from_market_data_v1");
                WriteField("Close", $"{GetDouble(root, "close"):0.#####}");
                WriteField("Simple Return", $"{GetDouble(root, "simple_return", "simpleReturn"):0.########}");
                WriteField("Candle Range", $"{GetDouble(root, "candle_range", "candleRange"):0.#####}");
                WriteField("Body Size", $"{GetDouble(root, "body_size", "bodySize"):0.#####}");
                WriteField("Direction", GetString(root, "direction"));
                WriteField("Mock Session", GetString(root, "mock_session", "mockSession"));
                WriteField("Mock Regime", GetString(root, "mock_regime", "mockRegime"));
                WriteField("Mock Signal Score", $"{GetDouble(root, "mock_signal_score", "mockSignalScore"):0.####}");
            }
            else
            {
                WriteField("Schema", "feature_export_demo_v1");
                WriteField("Session", GetString(root, "session"));
                WriteField("H4 Regime", GetString(root, "h4_regime", "h4Regime"));
                WriteField("H1 Bias", GetString(root, "h1_bias", "h1Bias"));
                WriteField("M15 Setup", GetString(root, "m15_setup", "m15Setup"));
                WriteField("M5 Trigger", GetString(root, "m5_trigger", "m5Trigger"));
                WriteField("ADX", $"{GetDouble(root, "adx"):0.##}");
                WriteField("ATR", $"{GetDouble(root, "atr"):0.#####}");
                WriteField("RSI", $"{GetDouble(root, "rsi"):0.##}");
                WriteField("Structure", GetString(root, "structure_state", "structureState"));
                WriteField("Pattern", GetString(root, "pattern_candidate", "patternCandidate"));
                WriteField("Signal Score", $"{GetDouble(root, "signal_score", "signalScore"):0.##}");
                WriteField("Spread", $"{GetDouble(root, "spread"):0.#####}");
            }

            Console.WriteLine();
        }

        WriteSafety();
        return 0;
    }

    private int GenerateFeatures()
    {
        WriteHeader("Hermes Feature Generation");
        var storagePaths = BuildStoragePaths();

        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        var service = new FeatureGenerationService(storagePaths, eventBus, CliVersion);
        var result = service.GenerateFromMarketData();
        eventStore.Flush();

        WriteField("Generation ID", result.Job.GenerationId);
        WriteField("Source", DisplayPath(result.Job.SourceRoot));
        WriteField("Output", DisplayPath(result.OutputPath));
        WriteField("Candle Rows", result.CandleCount.ToString());
        WriteField("Feature Rows", result.FeatureCount.ToString());
        WriteField("Symbols Processed", string.Join(", ", result.SymbolsProcessed));
        WriteField("Symbols", string.Join(", ", result.Job.Symbols));
        WriteField("Timeframes", string.Join(", ", result.Job.Timeframes));
        Console.WriteLine();

        if (result.FeatureCount == 0)
        {
            WriteWarning($"Keine Features erzeugt. Pruefe lokale Candle-Daten unter {DisplayPath(Path.Combine(storagePaths.Root, "market_data", "candles"))}.");
        }

        WriteSafety();
        return 0;
    }

    private int RunNightlyResearch()
    {
        WriteHeader("Hermes Nightly Research");
        var storagePaths = BuildStoragePaths();
        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        var schedule = new ResearchJobScheduleStub();
        var job = schedule.CreateDemoNightlyRun("hermes_cli");
        var coordinator = new ResearchPipelineCoordinator(storagePaths, eventBus, CliVersion);
        var report = coordinator.RunNightlyResearch(job);
        eventStore.Flush();

        WriteResearchReport(report);
        WriteSafety();
        return report.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private int ShowResearchStatus()
    {
        WriteHeader("Hermes Research Status");
        if (!TryLoadLatestResearchReport(out var latestPath, out var root))
        {
            WriteWarning("Kein Nightly-/Research-Report gefunden.");
            WriteSafety();
            return 0;
        }

        WriteField("Latest Report", DisplayPath(latestPath));
        WriteResearchSummaryFields(root, detailed: false);
        Console.WriteLine();

        WriteSafety();
        return 0;
    }

    private int ShowResearchReport()
    {
        WriteHeader("Hermes Research Summary Report");
        if (!TryLoadLatestResearchReport(out var latestPath, out var root))
        {
            WriteWarning("Kein ResearchSummaryReport gefunden.");
            WriteSafety();
            return 0;
        }

        WriteField("Latest Report", DisplayPath(latestPath));
        WriteResearchSummaryFields(root, detailed: true);
        Console.WriteLine();

        WriteSafety();
        return 0;
    }

    private int RunBetaLearning()
    {
        WriteHeader("Hermes Trading Learning Beta 1");
        var storagePaths = BuildStoragePaths();
        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        var pipeline = new TradingLearningBetaPipeline(storagePaths, eventBus, CliVersion);
        var report = pipeline.Run();
        eventStore.Flush();

        WriteBetaReport(report);
        WriteSafety();
        return report.Status.Equals("completed", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
    }

    private int ShowBetaStatus()
    {
        WriteHeader("Hermes Trading Learning Beta Status");
        if (!TryLoadLatestBetaReport(out var latestPath, out var root))
        {
            WriteWarning("Kein Trading Learning Beta Report gefunden.");
            WriteSafety();
            return 0;
        }

        WriteField("Latest Report", DisplayPath(latestPath));
        WriteBetaReportFields(root, detailed: true);
        Console.WriteLine();

        WriteSafety();
        return 0;
    }

    private int UpdateResearchMemory()
    {
        WriteHeader("Hermes Research Memory Update");
        var storagePaths = BuildStoragePaths();
        var service = new ResearchMemoryIndexService(storagePaths);
        var index = service.UpdateIndex();

        WriteField("Index", DisplayPath(service.IndexPath));
        WriteResearchMemoryIndex(index);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowResearchMemory()
    {
        WriteHeader("Hermes Research Memory");
        var storagePaths = BuildStoragePaths();
        var service = new ResearchMemoryIndexService(storagePaths);
        var index = service.LoadIndex();
        if (index is null)
        {
            WriteWarning($"Kein Research Memory Index gefunden: {DisplayPath(service.IndexPath)}");
            WriteSafety();
            return 0;
        }

        WriteField("Index", DisplayPath(service.IndexPath));
        WriteResearchMemoryIndex(index);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunLongResearch()
    {
        WriteHeader("Hermes Long-Run Research");
        var hours = ReadHours(_args, 1);
        var storagePaths = BuildStoragePaths();
        var service = new ResearchMemoryIndexService(storagePaths);
        var startedAtUtc = DateTimeOffset.UtcNow;
        var job = new LongRunResearchJob(
            JobId: $"long_research_{startedAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            StartedAtUtc: startedAtUtc,
            DeadlineUtc: startedAtUtc.AddHours(hours),
            RequestedHours: hours,
            RequestedBy: "hermes_cli",
            NoAutoTrading: true,
            HumanReviewRequired: true);

        var existingIndex = service.LoadIndex();
        var currentRanges = service.GetCurrentMarketDataRanges();
        var currentCandleCount = currentRanges.Sum(range => range.CandleCount);
        if (currentCandleCount == 0)
        {
            var index = service.UpdateIndex();
            var checkpoint = service.WriteCheckpoint(
                job,
                iteration: 0,
                status: "stopped_no_data",
                message: "No historical candle data found. Long-run research stopped before beta learning.",
                index,
                betaRunId: null);

            WriteField("Job ID", job.JobId);
            WriteField("Status", "stopped_no_data");
            WriteField("Checkpoint", DisplayPath(checkpoint));
            WriteField("Market Data Candles", "0");
            WriteResearchMemoryIndex(index);
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        var currentFingerprint = service.BuildMarketDataFingerprint(currentRanges);
        if (existingIndex is not null
            && existingIndex.LearningReady
            && service.BuildMarketDataFingerprint(existingIndex.ProcessedRanges) == currentFingerprint)
        {
            var strategyStep = RunStrategyResearchAndInsights(storagePaths);
            var checkpoint = service.WriteCheckpoint(
                job,
                iteration: 0,
                status: strategyStep.TestedNow > 0 ? "strategy_research_checkpointed" : "stopped_no_new_data",
                message: $"Current market-data ranges already match the Research Memory Index. No duplicate beta run started. Strategy variants tested: {strategyStep.TestedNow}.",
                existingIndex,
                betaRunId: null);

            WriteField("Job ID", job.JobId);
            WriteField("Status", strategyStep.TestedNow > 0 ? "strategy_research_checkpointed" : "stopped_no_new_data");
            WriteField("Checkpoint", DisplayPath(checkpoint));
            WriteField("Market Data Candles", currentCandleCount.ToString());
            WriteField("Strategy Variants Tested", strategyStep.TestedNow.ToString());
            WriteField("Strategy Insights", DisplayPath(strategyStep.InsightsPath));
            WriteResearchMemoryIndex(existingIndex);
            WriteStrategyResearchMemory(strategyStep.Memory, limit: 3);
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        var pipeline = new TradingLearningBetaPipeline(storagePaths, eventBus, CliVersion);
        var betaReport = pipeline.Run();
        eventStore.Flush();

        var updatedIndex = service.UpdateIndex();
        var strategyResearch = RunStrategyResearchAndInsights(storagePaths);
        var finalStatus = betaReport.CandlesProcessed == 0
            ? "stopped_no_data"
            : "checkpointed_no_new_data";
        var finalMessage = betaReport.CandlesProcessed == 0
            ? "Beta learning produced no candle-based work; long-run research stopped."
            : $"Beta learning checkpoint written. Strategy variants tested: {strategyResearch.TestedNow}. No second iteration was started without new market-data ranges.";
        var finalCheckpoint = service.WriteCheckpoint(
            job,
            iteration: 1,
            status: finalStatus,
            message: finalMessage,
            updatedIndex,
            betaReport.RunId);

        WriteField("Job ID", job.JobId);
        WriteField("Requested Hours", $"{job.RequestedHours:0.##}");
        WriteField("Status", finalStatus);
        WriteField("Iterations", "1");
        WriteField("Checkpoint", DisplayPath(finalCheckpoint));
        WriteField("Beta Run", betaReport.RunId);
        WriteField("Beta Report", DisplayOptionalPath(betaReport.BetaReportPath));
        WriteField("Strategy Variants Tested", strategyResearch.TestedNow.ToString());
        WriteField("Strategy Insights", DisplayPath(strategyResearch.InsightsPath));
        WriteResearchMemoryIndex(updatedIndex);
        WriteStrategyResearchMemory(strategyResearch.Memory, limit: 3);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunResearchAutopilot()
    {
        WriteHeader("Hermes Research Autopilot Beta");
        var hours = ReadHours(_args, 1);
        var maxDownloads = ReadIntOption(
            _args,
            "--max-downloads",
            fallback: Math.Clamp((int)Math.Ceiling(hours), 1, 9),
            min: 0,
            max: 27);
        var maxRequests = ReadIntOption(_args, "--max-requests", fallback: 500, min: 1, max: 500);
        var targetToUtc = new DateTimeOffset(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var targetFromUtc = targetToUtc.AddYears(-1);
        var fromOption = ReadOption(_args, "--from");
        if (!string.IsNullOrWhiteSpace(fromOption) && TryParseCliDate(fromOption, out var fromOverride))
        {
            targetFromUtc = fromOverride;
        }

        var toOption = ReadOption(_args, "--to");
        if (!string.IsNullOrWhiteSpace(toOption) && TryParseCliDate(toOption, out var toOverride))
        {
            targetToUtc = toOverride;
        }

        var startedAtUtc = DateTimeOffset.UtcNow;
        var storagePaths = BuildStoragePaths();
        var catalog = new StrategyPatternCatalog(storagePaths);
        var patterns = catalog.LoadOrCreateCatalog();
        var memoryService = new ResearchMemoryIndexService(storagePaths);
        var configLoad = LoadCTraderConfig();
        var authContext = BuildCTraderAuthContext(configLoad, storagePaths);
        var currentRanges = memoryService.GetCurrentMarketDataRanges();
        var plans = BuildAutopilotDownloadPlans(currentRanges, targetFromUtc, targetToUtc);
        var selectedPlans = plans.Take(maxDownloads).ToList();
        var warnings = new List<string>();
        var downloadResults = new List<AutopilotDownloadResult>();

        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        foreach (var plan in selectedPlans)
        {
            try
            {
                var download = DownloadHistoricalRangeForAutopilot(
                    storagePaths,
                    configLoad,
                    authContext,
                    eventBus,
                    plan.Request,
                    maxRequests);
                downloadResults.Add(download);
                warnings.AddRange(download.Warnings);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException)
            {
                warnings.Add($"{plan.Request.Symbol} {plan.Request.Timeframe} {plan.Reason}: {ex.Message}");
            }
        }

        eventStore.Flush();

        TradingLearningBetaReport? betaReport = null;
        if (memoryService.GetCurrentMarketDataRanges().Sum(range => range.CandleCount) > 0)
        {
            using var betaEventStore = new EventStore(storagePaths);
            var betaBus = new EventBus();
            betaBus.Subscribe(betaEventStore.Append);
            var pipeline = new TradingLearningBetaPipeline(storagePaths, betaBus, CliVersion);
            betaReport = pipeline.Run();
            betaEventStore.Flush();
        }
        else
        {
            warnings.Add("No candle data available after Autopilot data expansion; beta pipeline skipped.");
        }

        var updatedIndex = memoryService.UpdateIndex();
        var strategyResearch = RunStrategyResearchAndInsights(storagePaths);
        var report = WriteResearchAutopilotReport(
            storagePaths,
            startedAtUtc,
            hours,
            targetFromUtc,
            targetToUtc,
            plans.Count,
            selectedPlans.Count,
            downloadResults,
            strategyResearch,
            catalog.CatalogPath,
            warnings);

        var job = new LongRunResearchJob(
            JobId: report.ReportId,
            StartedAtUtc: startedAtUtc,
            DeadlineUtc: startedAtUtc.AddHours(hours),
            RequestedHours: hours,
            RequestedBy: "hermes_research_autopilot",
            NoAutoTrading: true,
            HumanReviewRequired: true);
        var checkpoint = memoryService.WriteCheckpoint(
            job,
            iteration: 1,
            status: report.Status,
            message: $"Research Autopilot completed. Downloads attempted: {report.DownloadsAttempted}. Strategy variants tested: {report.StrategyVariantsTested}.",
            updatedIndex,
            betaRunId: betaReport?.RunId);

        WriteField("Autopilot Report", DisplayPath(Path.Combine(storagePaths.Root, "strategy_research", "autopilot", $"{report.ReportId}.autopilot_report.json")));
        WriteField("Checkpoint", DisplayPath(checkpoint));
        WriteField("Requested Hours", $"{hours:0.##}");
        WriteField("Target Range", $"{targetFromUtc:yyyy-MM-dd} -> {targetToUtc:yyyy-MM-dd}");
        WriteField("Pattern Catalog", DisplayPath(catalog.CatalogPath));
        WriteField("Patterns", patterns.Count.ToString());
        WriteField("Download Plans", plans.Count.ToString());
        WriteField("Downloads Attempted", report.DownloadsAttempted.ToString());
        WriteField("Candles Downloaded", report.CandlesDownloaded.ToString());
        WriteField("Download Requests", report.DownloadRequests.ToString());
        WriteField("Strategy Variants Tested", report.StrategyVariantsTested.ToString());
        WriteField("Strategy Memory Entries", report.StrategyResearchEntries.ToString());
        WriteField("Insights", DisplayPath(strategyResearch.InsightsPath));
        WriteField("Status", report.Status);
        WriteResearchMemoryIndex(updatedIndex);
        WriteStrategyResearchMemory(strategyResearch.Memory, limit: 3);
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunStrategyResearch()
    {
        WriteHeader("Hermes Strategy Research Beta 2");
        var storagePaths = BuildStoragePaths();
        var service = new StrategyResearchService(storagePaths);
        var strategyStep = RunStrategyResearchAndInsights(storagePaths);

        WriteField("Memory", DisplayPath(service.MemoryPath));
        WriteField("Variants Tested Total", strategyStep.Memory.VariantsTested.ToString());
        WriteField("Variants Tested This Run", strategyStep.TestedNow.ToString());
        WriteField("Research Insights", DisplayPath(strategyStep.InsightsPath));
        WriteStrategyResearchMemory(strategyStep.Memory, limit: 5);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyResearchStatus()
    {
        WriteHeader("Hermes Strategy Research Status");
        var service = new StrategyResearchService(BuildStoragePaths());
        var memory = service.LoadOrCreateMemory();

        WriteField("Memory", DisplayPath(service.MemoryPath));
        WriteStrategyResearchMemory(memory, limit: 5);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowResearchInsights()
    {
        WriteHeader("Hermes Research Insights");
        var generator = new ResearchInsightsGenerator(BuildStoragePaths());
        var insights = generator.LoadInsights() ?? generator.Generate();

        WriteField("Insights", DisplayPath(generator.InsightsPath));
        WriteResearchInsights(insights);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyClusters()
    {
        WriteHeader("Hermes Strategy Clusters");
        var generator = new ResearchInsightsGenerator(BuildStoragePaths());
        var clusters = generator.LoadClusters();
        if (clusters.Count == 0)
        {
            clusters = generator.Generate().Clusters;
        }

        WriteField("Clusters", DisplayPath(generator.ClustersPath));
        foreach (var cluster in clusters)
        {
            WriteStrategyCluster(cluster);
        }

        WriteSafety();
        return 0;
    }

    private int ShowPatternCatalog()
    {
        WriteHeader("Hermes Strategy/Pattern Knowledge Base");
        var catalog = new StrategyPatternCatalog(BuildStoragePaths());
        var patterns = catalog.LoadOrCreateCatalog();

        WriteField("Catalog", DisplayPath(catalog.CatalogPath));
        WriteField("Patterns", patterns.Count.ToString());
        foreach (var pattern in patterns)
        {
            WriteSubHeader($"{pattern.Name} / {pattern.Id}");
            WriteField("Direction Bias", pattern.DirectionBias);
            WriteField("Strategy Family", StrategyPatternCatalog.StrategyFamilyForPattern(pattern.Id));
            WriteField("Timeframes", string.Join(", ", pattern.RequiredTimeframes));
            WriteField("Sessions", string.Join(", ", pattern.PreferredSessions));
            WriteField("Regimes", string.Join(", ", pattern.MarketRegimes));
            WriteField("Risk Hint", pattern.RiskModelHint);
            WriteMessages("Trigger Rules", pattern.TriggerRules.Select(rule => $"{rule.RuleId}: {rule.Description}").ToList());
            WriteMessages("Invalidation Rules", pattern.InvalidationRules.Select(rule => $"{rule.RuleId}: {rule.Description}").ToList());
            WriteMessages("Tags", pattern.Tags.Select(tag => tag.Id).ToList());
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowPatternPerformance()
    {
        WriteHeader("Hermes Pattern Performance");
        var generator = new ResearchInsightsGenerator(BuildStoragePaths());
        var catalog = new StrategyPatternCatalog(BuildStoragePaths());
        var performance = generator.LoadPatternPerformance();

        WriteField("Catalog", DisplayPath(catalog.CatalogPath));
        WriteField("Insights", DisplayPath(generator.InsightsPath));
        WriteMessages("Pattern Performance", performance);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowTopStrategies()
    {
        WriteHeader("Hermes Top Strategies");
        var service = new StrategyResearchService(BuildStoragePaths());
        var memory = service.LoadOrCreateMemory();

        if (memory.TopVariants.Count == 0)
        {
            WriteWarning("Noch keine Strategy-Research-Ergebnisse vorhanden.");
            WriteSafety();
            return 0;
        }

        foreach (var result in memory.TopVariants.Take(10))
        {
            WriteStrategyResult(result);
        }

        WriteSafety();
        return 0;
    }

    private StrategyResearchStepResult RunStrategyResearchAndInsights(StoragePaths storagePaths)
    {
        var service = new StrategyResearchService(storagePaths);
        var before = service.LoadOrCreateMemory().VariantsTested;
        var memory = service.RunResearch();
        var testedNow = Math.Max(0, memory.VariantsTested - before);
        var generator = new ResearchInsightsGenerator(storagePaths);
        var insights = generator.Generate();

        return new StrategyResearchStepResult(
            memory,
            testedNow,
            generator.InsightsPath,
            generator.ClustersPath,
            insights);
    }

    private bool TryLoadLatestResearchReport(out string path, out JsonElement root)
    {
        var preferredPaths = new[]
        {
            Path.Combine(_dataRoot, "reports", "research", "latest_research_summary.json"),
            Path.Combine(_dataRoot, "reports", "nightly", "latest_nightly_research.json")
        };

        foreach (var preferredPath in preferredPaths)
        {
            if (TryLoadJson(preferredPath, out root))
            {
                path = preferredPath;
                return true;
            }
        }

        var latestReport = FindResearchSummaryReports().LastOrDefault()
            ?? FindNightlyResearchReports().LastOrDefault();
        if (latestReport is not null && TryLoadJson(latestReport, out root))
        {
            path = latestReport;
            return true;
        }

        path = string.Empty;
        root = default;
        return false;
    }

    private bool TryLoadLatestBetaReport(out string path, out JsonElement root)
    {
        var latestPath = Path.Combine(_dataRoot, "reports", "beta", "latest_beta_learning.json");
        if (TryLoadJson(latestPath, out root))
        {
            path = latestPath;
            return true;
        }

        var latestReport = FindBetaReports().LastOrDefault();
        if (latestReport is not null && TryLoadJson(latestReport, out root))
        {
            path = latestReport;
            return true;
        }

        path = string.Empty;
        root = default;
        return false;
    }

    private void WriteResearchSummaryFields(JsonElement root, bool detailed)
    {
        var symbolsProcessed = GetStringArray(root, "symbols_processed", "symbolsProcessed");
        WriteField("Run ID", GetString(root, "run_id", "runId", "job_id", "jobId"));
        WriteField("Status", GetString(root, "status"));
        WriteField("Started UTC", GetString(root, "started_at_utc", "startedAtUtc"));
        WriteField("Completed UTC", GetString(root, "completed_at_utc", "completedAtUtc"));
        WriteField("Symbols Processed", symbolsProcessed.Count == 0 ? "-" : string.Join(", ", symbolsProcessed));
        WriteField("Candles Processed", GetInt(root, "candles_processed", "candlesProcessed").ToString());
        WriteField("Features", GetInt(root, "features_generated", "featuresGenerated", "feature_count", "featureCount").ToString());
        WriteField("Signals", GetInt(root, "signals_generated", "signalsGenerated", "signal_count", "signalCount").ToString());
        WriteField("Outcomes", GetInt(root, "outcomes_generated", "outcomesGenerated", "outcome_count", "outcomeCount").ToString());
        WriteField("Backtests", GetInt(root, "backtests_generated", "backtestsGenerated", "backtest_count", "backtestCount").ToString());
        WriteField("Reports", GetInt(root, "reports_generated", "reportsGenerated").ToString());
        WriteField("Duration", $"{GetDouble(root, "duration_seconds", "durationSeconds"):0.###} s");
        WriteField("no_auto_trading", GetBoolText(root, "no_auto_trading", "noAutoTrading"));
        WriteField("human_review_required", GetBoolText(root, "human_review_required", "humanReviewRequired"));

        if (detailed)
        {
            WriteField("Feature Output", DisplayOptionalPath(GetString(root, "feature_output_path", "featureOutputPath")));
            WriteField("Signal Output", DisplayOptionalPath(GetString(root, "signal_output_path", "signalOutputPath")));
            WriteField("Outcome Report", DisplayOptionalPath(GetString(root, "outcome_report_path", "outcomeReportPath")));
            WriteField("Backtest Report", DisplayOptionalPath(GetString(root, "backtest_report_path", "backtestReportPath")));
            WriteField("Nightly Report", DisplayOptionalPath(GetString(root, "nightly_report_path", "nightlyReportPath")));
            WriteField("Research Report", DisplayOptionalPath(GetString(root, "research_report_path", "researchReportPath")));
        }

        WriteMessages("Warnings", GetStringArray(root, "warnings"));
    }

    private void WriteBetaReportFields(JsonElement root, bool detailed)
    {
        var symbolsProcessed = GetStringArray(root, "symbols_processed", "symbolsProcessed");
        WriteField("Run ID", GetString(root, "run_id", "runId"));
        WriteField("Status", GetString(root, "status"));
        WriteField("Started UTC", GetString(root, "started_at_utc", "startedAtUtc"));
        WriteField("Completed UTC", GetString(root, "completed_at_utc", "completedAtUtc"));
        WriteField("Symbols Processed", symbolsProcessed.Count == 0 ? "-" : string.Join(", ", symbolsProcessed));
        WriteField("Candles Processed", GetInt(root, "candles_processed", "candlesProcessed").ToString());
        WriteField("Features", GetInt(root, "features_generated", "featuresGenerated").ToString());
        WriteField("Signals", GetInt(root, "signals_generated", "signalsGenerated").ToString());
        WriteField("Outcomes", GetInt(root, "outcomes_generated", "outcomesGenerated").ToString());
        WriteField("Backtests", GetInt(root, "backtests_generated", "backtestsGenerated").ToString());
        WriteField("Duration", $"{GetDouble(root, "duration_seconds", "durationSeconds"):0.###} s");
        WriteField("learning_ready", GetBoolText(root, "learning_ready", "learningReady"));
        WriteField("no_auto_trading", GetBoolText(root, "no_auto_trading", "noAutoTrading"));
        WriteField("human_review_required", GetBoolText(root, "human_review_required", "humanReviewRequired"));

        if (detailed)
        {
            WriteField("Beta Report", DisplayOptionalPath(GetString(root, "beta_report_path", "betaReportPath")));
            WriteField("Research Report", DisplayOptionalPath(GetString(root, "research_report_path", "researchReportPath")));
            WriteField("Feature Output", DisplayOptionalPath(GetString(root, "feature_output_path", "featureOutputPath")));
            WriteField("Signal Output", DisplayOptionalPath(GetString(root, "signal_output_path", "signalOutputPath")));
            WriteField("Outcome Report", DisplayOptionalPath(GetString(root, "outcome_report_path", "outcomeReportPath")));
            WriteField("Backtest Report", DisplayOptionalPath(GetString(root, "backtest_report_path", "backtestReportPath")));
        }

        WriteMessages("Warnings", GetStringArray(root, "warnings"));
    }

    private void WriteResearchMemoryIndex(ResearchMemoryIndex index)
    {
        WriteField("Updated UTC", index.UpdatedAtUtc.ToString("O"));
        WriteField("Last Run UTC", index.LastRunAt?.ToString("O") ?? "-");
        WriteField("Run Count", index.RunCount.ToString());
        WriteField("Symbols Processed", index.SymbolsProcessed.Count == 0 ? "-" : string.Join(", ", index.SymbolsProcessed));
        WriteField("Timeframes Processed", index.TimeframesProcessed.Count == 0 ? "-" : string.Join(", ", index.TimeframesProcessed));
        WriteField("Candles Processed", index.CandlesProcessed.ToString());
        WriteField("Features", index.FeaturesGenerated.ToString());
        WriteField("Signals", index.SignalsGenerated.ToString());
        WriteField("Outcomes", index.OutcomesGenerated.ToString());
        WriteField("Backtests", index.BacktestsGenerated.ToString());
        WriteField("Processed Ranges", index.ProcessedRanges.Count.ToString());
        WriteField("learning_ready", index.LearningReady.ToString().ToLowerInvariant());
        WriteField("Indexed Run IDs", index.IndexedRunIds.Count.ToString());

        foreach (var range in index.ProcessedRanges.TakeLast(5))
        {
            WriteSubHeader($"{range.Symbol} {range.Timeframe}");
            WriteField("Candles", range.CandleCount.ToString());
            WriteField("From UTC", range.FromUtc?.ToString("O") ?? "-");
            WriteField("To UTC", range.ToUtc?.ToString("O") ?? "-");
            WriteField("Source", DisplayPath(range.SourcePath));
        }

        WriteMessages("Warnings", index.Warnings);
    }

    private void WriteStrategyResearchMemory(StrategyResearchMemory memory, int limit)
    {
        WriteField("Updated UTC", memory.UpdatedAtUtc.ToString("O"));
        WriteField("Variants Tested", memory.VariantsTested.ToString());
        WriteField("Top Variants", memory.TopVariants.Count.ToString());
        WriteField("Rejected Variants", memory.RejectedVariants.Count.ToString());
        WriteField("Research Memory Entries", (memory.ResearchEntries?.Count ?? 0).ToString());
        WriteField("no_auto_trading", memory.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", memory.HumanReviewRequired.ToString().ToLowerInvariant());

        foreach (var result in memory.TopVariants.Take(limit))
        {
            WriteStrategyResult(result);
        }

        WriteMessages("Warnings", memory.Warnings);
    }

    private void WriteStrategyResult(StrategyResearchResult result)
    {
        var patternName = ResolvePatternName(result.Variant.PatternId);
        WriteSubHeader($"{result.Variant.Family} / {patternName} / {result.Variant.VariantId}");
        WriteField("Pattern ID", result.Variant.PatternId ?? "-");
        WriteField("Pattern", patternName);
        WriteField("Score", $"{result.Fitness.Score:0.####}");
        WriteField("Winrate", $"{result.Fitness.Winrate * 100:0.##}%");
        WriteField("Average RR", $"{result.Fitness.AverageRr:0.####}");
        WriteField("Drawdown Penalty", $"{result.Fitness.DrawdownPenalty:0.####}");
        WriteField("Stability Bonus", $"{result.Fitness.StabilityBonus:0.####}");
        WriteField("Trade Count Factor", $"{result.Fitness.TradeCountFactor:0.####}");
        WriteField("Trades", result.TradeCount.ToString());
        WriteField("Wins/Losses", $"{result.WinCount}/{result.LossCount}");
        WriteField("Avg R", $"{result.AverageR:0.####}");
        WriteField("Max Drawdown", $"{result.MaxDrawdown:0.####}");
        WriteField("EMA", $"{result.Variant.FastEma}/{result.Variant.SlowEma}");
        WriteField("RR", $"{result.Variant.RiskRewardRatio:0.##}");
        WriteField("SL ATR", $"{result.Variant.StopLossAtrMultiplier:0.##}");
        WriteField("Confirmation", result.Variant.RequireConfirmationCandle.ToString().ToLowerInvariant());
        WriteField("Vol Filter", result.Variant.UseVolatilityFilter.ToString().ToLowerInvariant());
        WriteField("Session Filter", result.Variant.SessionFilter ?? "any");
        WriteField("Variant Timeframe", result.Variant.Timeframe ?? "any");
        WriteField("From UTC", result.FromUtc?.ToString("O") ?? "-");
        WriteField("To UTC", result.ToUtc?.ToString("O") ?? "-");
    }

    private void WriteResearchInsights(StrategyEvolutionSummary insights)
    {
        WriteField("Generated UTC", insights.GeneratedAtUtc.ToString("O"));
        WriteField("Top Strategies", insights.TopStrategies.Count.ToString());
        WriteField("Weak Strategies", insights.WeakStrategies.Count.ToString());
        WriteField("Clusters", insights.Clusters.Count.ToString());
        WriteField("Best Symbols", insights.BestSymbols.Count == 0 ? "-" : string.Join(", ", insights.BestSymbols));
        WriteField("Best Timeframes", insights.BestTimeframes.Count == 0 ? "-" : string.Join(", ", insights.BestTimeframes));
        WriteMessages("Stability Metrics", insights.StabilityMetrics);
        WriteMessages("Fitness Trends", insights.FitnessTrends.TakeLast(5).ToList());
        WriteMessages("Exploration Coverage", insights.ExplorationCoverage);
        WriteMessages("Strategy Rankings", insights.StrategyRankings);
        WriteMessages("Best Patterns", insights.BestPatterns ?? Array.Empty<string>());
        WriteMessages("Weak Patterns", insights.WeakPatterns ?? Array.Empty<string>());
        WriteMessages("Avoid Combinations", insights.AvoidCombinations ?? Array.Empty<string>());
        WriteMessages("Next Recommended Tests", insights.NextRecommendedTests ?? Array.Empty<string>());
        WriteMessages("Parameter Statistics", insights.ParameterStatistics);
        WriteMessages("Timeframe Comparisons", insights.TimeframeComparisons);
        WriteField("no_auto_trading", insights.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", insights.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteStrategyCluster(StrategyCluster cluster)
    {
        WriteSubHeader($"{cluster.Family} / {cluster.ClusterId}");
        WriteField("Variants", cluster.VariantCount.ToString());
        WriteField("Average Fitness", $"{cluster.AverageFitness:0.####}");
        WriteField("Best Fitness", $"{cluster.BestFitness:0.####}");
        WriteField("Average Winrate", $"{cluster.AverageWinrate * 100:0.##}%");
        WriteField("Average Trades", $"{cluster.AverageTradeCount:0.##}");
        WriteField("Prioritized", cluster.Prioritized.ToString().ToLowerInvariant());
        WriteField("Reduced", cluster.Reduced.ToString().ToLowerInvariant());
        WriteMessages("Common Parameters", cluster.CommonParameters);
    }

    private string ResolvePatternName(string? patternId)
    {
        if (string.IsNullOrWhiteSpace(patternId))
        {
            return "-";
        }

        var catalog = new StrategyPatternCatalog(BuildStoragePaths());
        return StrategyPatternCatalog.PatternName(catalog.LoadOrCreateCatalog(), patternId);
    }

    private int ShowCTraderHealth()
    {
        WriteHeader("Hermes cTrader Open API Health");
        var configLoad = LoadCTraderConfig();
        var config = configLoad.Config;
        var storagePaths = BuildStoragePaths();
        var authContext = BuildCTraderAuthContext(configLoad, storagePaths);
        var mapper = new CTraderSymbolMapper(config.AllowedSymbols);
        var tokenStore = new CTraderTokenStore(storagePaths);
        var storedToken = tokenStore.LoadToken();
        ICTraderHistoricalDataClient client = configLoad.LocalConfigLoaded
                && config.AuthMode.Equals("oauth", StringComparison.OrdinalIgnoreCase)
            ? new CTraderOpenApiHistoricalDataClient(config, mapper, storedToken ?? new CTraderStoredToken())
            : new CTraderHistoricalDataClientStub(config, mapper, authContext.TokenState);
        var health = client.CheckHealth();

        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);
        PublishCTraderEvent(
            eventBus,
            EventType.CTraderConnectorHealthChecked,
            EventSeverity.Info,
            new
            {
                message = health.StubActive
                    ? "cTrader Open API connector health checked. Stub fallback; no live connection."
                    : "cTrader Open API connector health checked. Real read-only client configured; no download performed.",
                health,
                configPath = configLoad.ConfigPath,
                configLoad.LocalConfigLoaded,
                configLoad.LocalConfigMissing,
                authMode = authContext.AuthStatus.AuthMode,
                authUrlAvailable = authContext.AuthStatus.AuthUrlAvailable,
                tokenLoaded = authContext.AuthStatus.TokenLoaded,
                authStatus = authContext.AuthStatus.Status,
                noAutoTrading = true,
                humanReviewRequired = true
            });
        eventStore.Flush();

        Console.WriteLine(health.StubActive
            ? "Open API connector stub active"
            : "Open API read-only client configured");
        Console.WriteLine("No cTrader download was performed by health check.");
        WriteField("Status", health.Status);
        WriteField("Environment", health.Environment);
        WriteField("Stub Active", health.StubActive.ToString().ToLowerInvariant());
        WriteField("Auth Configured", health.AuthConfigured.ToString().ToLowerInvariant());
        WriteField("Auth Mode", authContext.AuthStatus.AuthMode);
        WriteField("Auth URL Available", authContext.AuthStatus.AuthUrlAvailable.ToString().ToLowerInvariant());
        WriteField("Auth Status", authContext.AuthStatus.Status);
        WriteField("Token Store Path", DisplayPath(authContext.AuthStatus.TokenStorePath));
        WriteField("Token Loaded", authContext.AuthStatus.TokenLoaded.ToString().ToLowerInvariant());
        WriteField("Client ID Configured", health.ClientIdConfigured.ToString().ToLowerInvariant());
        WriteField("Account ID Configured", health.AccountIdConfigured.ToString().ToLowerInvariant());
        WriteField("no_orders", health.NoOrders.ToString().ToLowerInvariant());
        WriteField("Read-only Market Data", health.ReadOnlyMarketData.ToString().ToLowerInvariant());
        WriteField("Config", DisplayPath(configLoad.ConfigPath));
        WriteField("Local Config Loaded", configLoad.LocalConfigLoaded.ToString().ToLowerInvariant());
        WriteField("Local Config Missing", configLoad.LocalConfigMissing.ToString().ToLowerInvariant());
        WriteMessages("Warnings", CombineWarnings(configLoad.Warnings, health.Warnings, authContext.AuthStatus.Warnings));
        Console.WriteLine();

        WriteSafety();
        return 0;
    }

    private int ShowCTraderAuthUrl()
    {
        WriteHeader("Hermes cTrader OAuth URL");
        var configLoad = LoadCTraderConfig();
        var oauthUrl = new CTraderOAuthUrlBuilder().Build(configLoad.Config);

        WriteField("Auth URL Available", oauthUrl.Available.ToString().ToLowerInvariant());
        WriteField("Config", DisplayPath(configLoad.ConfigPath));
        WriteField("Local Config Loaded", configLoad.LocalConfigLoaded.ToString().ToLowerInvariant());
        WriteField("Redirect URI", oauthUrl.RedirectUri);
        WriteField("Scopes", string.Join(", ", oauthUrl.Scopes));
        WriteField("no_orders", configLoad.Config.NoOrders.ToString().ToLowerInvariant());
        WriteField("Read-only Market Data", configLoad.Config.ReadOnlyMarketData.ToString().ToLowerInvariant());
        Console.WriteLine();

        if (oauthUrl.Url is not null)
        {
            Console.WriteLine("OAuth URL:");
            Console.WriteLine(oauthUrl.Url);
            Console.WriteLine();
            Console.WriteLine("Browser nicht automatisch geoeffnet.");
            Console.WriteLine("Oeffne die URL manuell im Browser, melde dich bei cTrader an und kopiere danach den Redirect-Code.");
        }

        WriteMessages("Warnings", CombineWarnings(configLoad.Warnings, oauthUrl.Warnings));
        Console.WriteLine();
        WriteSafety();
        return oauthUrl.Available ? 0 : 1;
    }

    private int ExchangeCTraderAuthCode()
    {
        WriteHeader("Hermes cTrader OAuth Code Exchange");
        var code = ReadOption(_args, "--code");
        if (string.IsNullOrWhiteSpace(code)
            || code.Contains('<', StringComparison.Ordinal)
            || code.Contains('>', StringComparison.Ordinal))
        {
            WriteError("Ein frischer OAuth Redirect-Code ist erforderlich. Beispiel: ctrader-auth-code --code ABC123");
            WriteSafety();
            return 2;
        }

        var configLoad = LoadCTraderConfig();
        var storagePaths = BuildStoragePaths();
        var tokenStore = new CTraderTokenStore(storagePaths);
        var oauthUrl = new CTraderOAuthUrlBuilder().Build(configLoad.Config);

        if (!configLoad.LocalConfigLoaded)
        {
            WriteError("config/ctrader.openapi.local.json fehlt. Kein Token Exchange ausgefuehrt.");
            WriteMessages("Warnings", CombineWarnings(configLoad.Warnings, oauthUrl.Warnings));
            WriteSafety();
            return 1;
        }

        if (!oauthUrl.Available)
        {
            WriteError("cTrader OAuth ist nicht sicher konfiguriert. Kein Token Exchange ausgefuehrt.");
            WriteMessages("Warnings", CombineWarnings(configLoad.Warnings, oauthUrl.Warnings));
            WriteSafety();
            return 1;
        }

        try
        {
            using var httpClient = new HttpClient();
            var exchangeClient = new CTraderTokenExchangeClient(httpClient);
            var token = exchangeClient
                .ExchangeAuthorizationCodeAsync(configLoad.Config, code.Trim())
                .GetAwaiter()
                .GetResult();
            tokenStore.SaveToken(token);

            WriteField("Status", "authenticated");
            WriteField("Token Store Path", DisplayPath(tokenStore.TokenStorePath));
            WriteField("Token Loaded", "true");
            WriteField("Expires UTC", token.ExpiresAtUtc?.ToString("O") ?? "-");
            WriteField("Tokens Printed", "false");
            Console.WriteLine();
            Console.WriteLine("Token wurde lokal gespeichert. Access-/Refresh-Token werden nicht ausgegeben.");
            WriteSafety();
            return 0;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            WriteError(ex.Message);
            WriteField("Token Store Path", DisplayPath(tokenStore.TokenStorePath));
            WriteField("Token Written", "false");
            Console.WriteLine();
            WriteSafety();
            return 1;
        }
    }

    private int ShowCTraderAuthStatus()
    {
        WriteHeader("Hermes cTrader Auth Status");
        var configLoad = LoadCTraderConfig();
        var authContext = BuildCTraderAuthContext(configLoad, BuildStoragePaths());
        var status = authContext.AuthStatus;

        WriteField("Status", status.Status);
        WriteField("Auth URL Available", status.AuthUrlAvailable.ToString().ToLowerInvariant());
        WriteField("Auth Configured", status.AuthConfigured.ToString().ToLowerInvariant());
        WriteField("Auth Mode", status.AuthMode);
        WriteField("Token Store Path", DisplayPath(status.TokenStorePath));
        WriteField("Token Store Exists", status.TokenStoreExists.ToString().ToLowerInvariant());
        WriteField("Token Loaded", status.TokenLoaded.ToString().ToLowerInvariant());
        WriteField("Expires UTC", status.ExpiresAtUtc?.ToString("O") ?? "-");
        WriteField("Config", DisplayPath(configLoad.ConfigPath));
        WriteField("Local Config Loaded", configLoad.LocalConfigLoaded.ToString().ToLowerInvariant());
        WriteField("no_orders", configLoad.Config.NoOrders.ToString().ToLowerInvariant());
        WriteField("Read-only Market Data", configLoad.Config.ReadOnlyMarketData.ToString().ToLowerInvariant());
        WriteMessages("Warnings", status.Warnings);
        Console.WriteLine();

        WriteSafety();
        return 0;
    }

    private int ShowCTraderSymbols()
    {
        WriteHeader("Hermes cTrader Symbol Mapping");
        var configLoad = LoadCTraderConfig();
        var config = configLoad.Config;
        var mapper = new CTraderSymbolMapper(config.AllowedSymbols);

        Console.WriteLine("Lokales Symbol-Mapping; echte brokerseitige Symbol-IDs werden beim Download aus der cTrader Symbol-Liste aufgeloest.");
        WriteField("Config", DisplayPath(configLoad.ConfigPath));
        WriteField("Local Config Loaded", configLoad.LocalConfigLoaded.ToString().ToLowerInvariant());
        WriteField("Local Config Missing", configLoad.LocalConfigMissing.ToString().ToLowerInvariant());
        WriteField("Allowed Timeframes", string.Join(", ", config.AllowedTimeframes));
        WriteMessages("Warnings", configLoad.Warnings);
        Console.WriteLine();

        foreach (var mapping in mapper.GetMappings())
        {
            WriteSubHeader(mapping.HermesSymbol);
            WriteField("cTrader Name", mapping.CTraderSymbolName);
            WriteField("cTrader Symbol ID", mapping.CTraderSymbolId);
            WriteField("Aliases", string.Join(", ", mapping.Aliases));
            WriteField("Stub Mapping", mapping.StubMapping.ToString().ToLowerInvariant());
            Console.WriteLine();
        }

        WriteSafety();
        return 0;
    }

    private int DownloadCTraderHistory()
    {
        WriteHeader("Hermes cTrader Historical Download");
        var symbol = ReadOption(_args, "--symbol");
        var timeframe = ReadOption(_args, "--timeframe");
        var fromText = ReadOption(_args, "--from");
        var toText = ReadOption(_args, "--to");
        var maxRequests = ReadIntOption(_args, "--max-requests", fallback: 500, min: 1, max: 500);

        if (string.IsNullOrWhiteSpace(symbol)
            || string.IsNullOrWhiteSpace(timeframe)
            || string.IsNullOrWhiteSpace(fromText)
            || string.IsNullOrWhiteSpace(toText)
            || !TryParseCliDate(fromText, out var fromUtc)
            || !TryParseCliDate(toText, out var toUtc))
        {
            WriteError("Pflichtargumente fehlen oder Datumswerte sind ungueltig.");
            Console.WriteLine("Beispiel:");
            Console.WriteLine("  dotnet run --project ./cli/Hermes.Cli.csproj -- download-history --symbol XAUUSD --timeframe M5 --from 2025-01-01 --to 2025-01-02");
            WriteSafety();
            return 2;
        }

        var storagePaths = BuildStoragePaths();
        var configLoad = LoadCTraderConfig();
        var config = configLoad.Config;
        var authContext = BuildCTraderAuthContext(configLoad, storagePaths);
        var useRealClient = configLoad.LocalConfigLoaded
            && config.AuthMode.Equals("oauth", StringComparison.OrdinalIgnoreCase);
        var stubActive = !useRealClient;
        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        var request = new CTraderHistoricalDataRequest(
            Symbol: symbol.Trim().ToUpperInvariant(),
            Timeframe: timeframe.Trim().ToUpperInvariant(),
            FromUtc: fromUtc,
            ToUtc: toUtc);

        PublishCTraderEvent(
            eventBus,
            EventType.CTraderHistoricalDownloadStarted,
            EventSeverity.Info,
            new
            {
                message = useRealClient
                    ? "cTrader historical download started. Real read-only Open API path."
                    : "cTrader historical download started. Stub fallback; no live Open API call.",
                request.Symbol,
                request.Timeframe,
                request.FromUtc,
                request.ToUtc,
                stubActive,
                noAutoTrading = true,
                humanReviewRequired = true
            });

        try
        {
            var mapper = new CTraderSymbolMapper(config.AllowedSymbols);
            var tokenStore = new CTraderTokenStore(storagePaths);
            var storedToken = tokenStore.LoadToken();
            ICTraderHistoricalDataClient client;

            if (useRealClient)
            {
                if (storedToken is null || !storedToken.HasAccessToken)
                {
                    throw new InvalidOperationException(
                        "cTrader OAuth token missing. Run ctrader-auth-url, open the URL in a browser, then run ctrader-auth-code --code <CODE> with a fresh redirect code. No real cTrader data was downloaded.");
                }

                client = new CTraderOpenApiHistoricalDataClient(config, mapper, storedToken);
            }
            else
            {
                client = new CTraderHistoricalDataClientStub(config, mapper, authContext.TokenState);
            }

            var download = DownloadPagedHistoricalCandles(client, request, maxRequests);
            var importer = new CTraderTrendbarImporter(storagePaths);
            var result = useRealClient
                ? importer.ImportCandles(request, download.Candles, sourceName: "ctrader_openapi_paged", stubData: false)
                : importer.ImportStubCandles(request, download.Candles);

            PublishCTraderEvent(
                eventBus,
                EventType.CTraderHistoricalDownloadCompleted,
                EventSeverity.Info,
                new
                {
                    message = useRealClient
                        ? "cTrader historical download completed with real read-only Open API data."
                        : "cTrader historical download completed with stub data. No real cTrader data was loaded.",
                    result.DownloadId,
                    result.Symbol,
                    result.Timeframe,
                    result.OutputPath,
                    result.CandleCount,
                    result.FromUtc,
                    result.ToUtc,
                    result.StubData,
                    requests = download.Requests,
                    duplicatesSkipped = download.DuplicatesSkipped,
                    download.Truncated,
                    download.Warnings,
                    configPath = configLoad.ConfigPath,
                    configLoad.LocalConfigLoaded,
                    configLoad.LocalConfigMissing,
                    authMode = authContext.AuthStatus.AuthMode,
                    tokenLoaded = authContext.AuthStatus.TokenLoaded,
                    authStatus = authContext.AuthStatus.Status,
                    noAutoTrading = true,
                    humanReviewRequired = true
                });
            eventStore.Flush();

            Console.WriteLine(useRealClient
                ? "Open API read-only historical download completed"
                : "Open API connector stub active");
            Console.WriteLine(useRealClient
                ? "Echte cTrader Candles wurden lokal normalisiert und gespeichert."
                : "No real cTrader data was loaded.");
            if (!useRealClient && configLoad.LocalConfigMissing)
            {
                Console.WriteLine("Local config missing: config/ctrader.openapi.local.json. Stub active.");
            }

            WriteField("Download ID", result.DownloadId);
            WriteField("Symbol", result.Symbol);
            WriteField("Timeframe", result.Timeframe);
            WriteField("Rows", result.CandleCount.ToString());
            WriteField("Requests", download.Requests.ToString());
            WriteField("Max Requests", maxRequests.ToString());
            WriteField("Duplicates Skipped", download.DuplicatesSkipped.ToString());
            WriteField("Truncated", download.Truncated.ToString().ToLowerInvariant());
            WriteField("From UTC", result.FromUtc?.ToString("O"));
            WriteField("To UTC", result.ToUtc?.ToString("O"));
            WriteField("Output", DisplayPath(result.OutputPath));
            WriteField("Stub Data", result.StubData.ToString().ToLowerInvariant());
            WriteField("Config", DisplayPath(configLoad.ConfigPath));
            WriteField("Local Config Loaded", configLoad.LocalConfigLoaded.ToString().ToLowerInvariant());
            WriteMessages("Warnings", CombineWarnings(configLoad.Warnings, authContext.AuthStatus.Warnings, download.Warnings));
            Console.WriteLine();

            WriteSafety();
            return 0;
        }
        catch (Exception ex)
        {
            PublishCTraderEvent(
                eventBus,
                EventType.CTraderHistoricalDownloadFailed,
                EventSeverity.Warning,
                new
                {
                    message = useRealClient
                        ? "cTrader historical download failed before any trading action. Real read-only path."
                        : "cTrader historical download failed before any trading action. Stub fallback.",
                    request.Symbol,
                    request.Timeframe,
                    request.FromUtc,
                    request.ToUtc,
                    error = ex.Message,
                    stubActive,
                    noAutoTrading = true,
                    humanReviewRequired = true
                });
            eventStore.Flush();

            WriteError(ex.Message);
            WriteSafety();
            return 1;
        }
    }

    private HistoricalDownloadPageResult DownloadPagedHistoricalCandles(
        ICTraderHistoricalDataClient client,
        CTraderHistoricalDataRequest request,
        int maxRequests)
    {
        var interval = TimeframeInterval(request.Timeframe);
        var candlesByTimestamp = new Dictionary<DateTimeOffset, MarketDataCandle>();
        var warnings = new List<string>();
        var currentToUtc = request.ToUtc;
        var requests = 0;
        var duplicatesSkipped = 0;
        var truncated = false;

        while (currentToUtc >= request.FromUtc)
        {
            if (requests >= maxRequests)
            {
                truncated = true;
                warnings.Add($"download-history stopped at max_requests={maxRequests}; requested range may be incomplete.");
                break;
            }

            var pageRequest = request with { ToUtc = currentToUtc };
            var page = client.DownloadHistoricalCandles(pageRequest)
                .Where(candle => candle.TimestampUtc >= request.FromUtc && candle.TimestampUtc <= request.ToUtc)
                .OrderBy(candle => candle.TimestampUtc)
                .ToList();
            requests++;

            if (page.Count == 0)
            {
                break;
            }

            foreach (var candle in page)
            {
                if (!candlesByTimestamp.TryAdd(candle.TimestampUtc, candle))
                {
                    duplicatesSkipped++;
                }
            }

            var earliest = page[0].TimestampUtc;
            if (earliest <= request.FromUtc || page.Count < 1000)
            {
                break;
            }

            var nextToUtc = earliest - interval;
            if (nextToUtc >= currentToUtc)
            {
                truncated = true;
                warnings.Add("download-history paging stopped because cTrader returned a non-progressing page.");
                break;
            }

            currentToUtc = nextToUtc;
            Thread.Sleep(50);
        }

        var candles = candlesByTimestamp
            .Values
            .OrderBy(candle => candle.TimestampUtc)
            .ToList();

        return new HistoricalDownloadPageResult(
            Candles: candles,
            Requests: requests,
            DuplicatesSkipped: duplicatesSkipped,
            Truncated: truncated,
            Warnings: warnings);
    }

    private AutopilotDownloadResult DownloadHistoricalRangeForAutopilot(
        StoragePaths storagePaths,
        CTraderOpenApiConfigLoadResult configLoad,
        (CTraderOAuthUrlResult OAuthUrl, CTraderAuthStatus AuthStatus, CTraderAuthTokenState TokenState) authContext,
        EventBus eventBus,
        CTraderHistoricalDataRequest request,
        int maxRequests)
    {
        var config = configLoad.Config;
        var useRealClient = configLoad.LocalConfigLoaded
            && config.AuthMode.Equals("oauth", StringComparison.OrdinalIgnoreCase);
        var stubActive = !useRealClient;

        PublishCTraderEvent(
            eventBus,
            EventType.CTraderHistoricalDownloadStarted,
            EventSeverity.Info,
            new
            {
                message = useRealClient
                    ? "Research Autopilot historical download started. Real read-only Open API path."
                    : "Research Autopilot historical download started. Stub fallback; no live Open API call.",
                request.Symbol,
                request.Timeframe,
                request.FromUtc,
                request.ToUtc,
                stubActive,
                noAutoTrading = true,
                humanReviewRequired = true
            });

        var mapper = new CTraderSymbolMapper(config.AllowedSymbols);
        var tokenStore = new CTraderTokenStore(storagePaths);
        var storedToken = tokenStore.LoadToken();
        ICTraderHistoricalDataClient client;
        if (useRealClient)
        {
            if (storedToken is null || !storedToken.HasAccessToken)
            {
                throw new InvalidOperationException(
                    "cTrader OAuth token missing. Autopilot did not download real cTrader data for this range.");
            }

            client = new CTraderOpenApiHistoricalDataClient(config, mapper, storedToken);
        }
        else
        {
            client = new CTraderHistoricalDataClientStub(config, mapper, authContext.TokenState);
        }

        var download = DownloadPagedHistoricalCandles(client, request, maxRequests);
        var importer = new CTraderTrendbarImporter(storagePaths);
        var result = useRealClient
            ? importer.ImportCandles(request, download.Candles, sourceName: "ctrader_openapi_autopilot_paged", stubData: false)
            : importer.ImportStubCandles(request, download.Candles);

        PublishCTraderEvent(
            eventBus,
            EventType.CTraderHistoricalDownloadCompleted,
            EventSeverity.Info,
            new
            {
                message = useRealClient
                    ? "Research Autopilot historical download completed with real read-only Open API data."
                    : "Research Autopilot historical download completed with stub data. No real cTrader data was loaded.",
                result.DownloadId,
                result.Symbol,
                result.Timeframe,
                result.OutputPath,
                result.CandleCount,
                result.FromUtc,
                result.ToUtc,
                result.StubData,
                requests = download.Requests,
                duplicatesSkipped = download.DuplicatesSkipped,
                download.Truncated,
                noAutoTrading = true,
                humanReviewRequired = true
            });

        return new AutopilotDownloadResult(
            Request: request,
            ImportResult: result,
            Requests: download.Requests,
            DuplicatesSkipped: download.DuplicatesSkipped,
            Truncated: download.Truncated,
            Warnings: CombineWarnings(configLoad.Warnings, authContext.AuthStatus.Warnings, download.Warnings));
    }

    private IReadOnlyList<AutopilotDownloadPlan> BuildAutopilotDownloadPlans(
        IReadOnlyList<ResearchProcessedRange> ranges,
        DateTimeOffset targetFromUtc,
        DateTimeOffset targetToUtc)
    {
        var plans = new List<AutopilotDownloadPlan>();
        foreach (var symbol in new[] { "XAUUSD", "EURUSD", "GER40" })
        foreach (var timeframe in new[] { "M5", "M15", "H1" })
        {
            var relevant = ranges
                .Where(range => range.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)
                    && range.Timeframe.Equals(timeframe, StringComparison.OrdinalIgnoreCase)
                    && range.FromUtc is not null
                    && range.ToUtc is not null)
                .ToList();
            if (relevant.Count == 0)
            {
                plans.Add(CreateAutopilotPlan(symbol, timeframe, targetFromUtc, targetToUtc, "missing_range"));
                continue;
            }

            var earliest = relevant.Min(range => range.FromUtc)!.Value;
            var latest = relevant.Max(range => range.ToUtc)!.Value;
            var interval = TimeframeInterval(timeframe);
            if (latest < targetToUtc - interval)
            {
                plans.Add(CreateAutopilotPlan(symbol, timeframe, latest + interval, targetToUtc, "extend_forward"));
            }
            else if (earliest > targetFromUtc + interval)
            {
                plans.Add(CreateAutopilotPlan(symbol, timeframe, targetFromUtc, earliest - interval, "extend_backward"));
            }
        }

        return plans
            .Where(plan => plan.Request.FromUtc < plan.Request.ToUtc)
            .OrderBy(plan => plan.Request.Timeframe == "M5" ? 0 : plan.Request.Timeframe == "M15" ? 1 : 2)
            .ThenBy(plan => plan.Request.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static AutopilotDownloadPlan CreateAutopilotPlan(
        string symbol,
        string timeframe,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        string reason)
    {
        return new AutopilotDownloadPlan(
            new CTraderHistoricalDataRequest(
                Symbol: symbol,
                Timeframe: timeframe,
                FromUtc: fromUtc,
                ToUtc: toUtc),
            reason);
    }

    private ResearchAutopilotReport WriteResearchAutopilotReport(
        StoragePaths storagePaths,
        DateTimeOffset startedAtUtc,
        double requestedHours,
        DateTimeOffset targetFromUtc,
        DateTimeOffset targetToUtc,
        int downloadPlans,
        int downloadsAttempted,
        IReadOnlyList<AutopilotDownloadResult> downloads,
        StrategyResearchStepResult strategyResearch,
        string patternCatalogPath,
        IReadOnlyList<string> warnings)
    {
        var completedAtUtc = DateTimeOffset.UtcNow;
        var report = new ResearchAutopilotReport(
            ReportId: $"research_autopilot_{startedAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: completedAtUtc,
            RequestedHours: requestedHours,
            TargetSymbols: ["XAUUSD", "EURUSD", "GER40"],
            TargetTimeframes: ["M5", "M15", "H1"],
            TargetFromUtc: targetFromUtc,
            TargetToUtc: targetToUtc,
            DownloadPlans: downloadPlans,
            DownloadsAttempted: downloadsAttempted,
            CandlesDownloaded: downloads.Sum(download => download.ImportResult.CandleCount),
            DownloadRequests: downloads.Sum(download => download.Requests),
            StrategyVariantsTested: strategyResearch.TestedNow,
            StrategyResearchEntries: strategyResearch.Memory.ResearchEntries?.Count ?? 0,
            PatternCatalogPath: patternCatalogPath,
            InsightsPath: strategyResearch.InsightsPath,
            Status: warnings.Count == 0 ? "completed" : "completed_with_warnings",
            Warnings: warnings.Distinct(StringComparer.Ordinal).Take(80).ToList(),
            NoAutoTrading: true,
            HumanReviewRequired: true);

        var directory = Path.Combine(storagePaths.Root, "strategy_research", "autopilot");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{report.ReportId}.autopilot_report.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(Path.Combine(directory, "latest_autopilot_report.json"), JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    private int ImportCsv()
    {
        WriteHeader("Hermes cTrader CSV Import");
        var symbol = ReadOption(_args, "--symbol");
        var timeframe = ReadOption(_args, "--timeframe");
        var file = ReadOption(_args, "--file");

        if (string.IsNullOrWhiteSpace(symbol)
            || string.IsNullOrWhiteSpace(timeframe)
            || string.IsNullOrWhiteSpace(file))
        {
            WriteError("Pflichtargumente fehlen.");
            Console.WriteLine("Beispiel:");
            Console.WriteLine("  dotnet run --project ./cli/Hermes.Cli.csproj -- import-csv --symbol XAUUSD --timeframe M5 --file path/to/file.csv");
            WriteSafety();
            return 2;
        }

        var storagePaths = BuildStoragePaths();
        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        var importer = new CTraderCsvCandleImporter(storagePaths, eventBus, CliVersion);
        var result = importer.Import(symbol, timeframe, file);
        eventStore.Flush();

        WriteField("Import ID", result.ImportId);
        WriteField("Symbol", result.Symbol);
        WriteField("Timeframe", result.Timeframe);
        WriteField("Format", result.Format.ToString());
        WriteField("Source", result.SourcePath);
        WriteField("Status", result.Validation.IsValid ? "imported" : "failed_validation");
        WriteField("Source Rows", result.Validation.SourceRowCount.ToString());
        WriteField("Imported Rows", result.Validation.ImportedRowCount.ToString());
        WriteField("Invalid Rows", result.Validation.InvalidRowCount.ToString());
        WriteField("From UTC", result.Validation.FromUtc?.ToString("O"));
        WriteField("To UTC", result.Validation.ToUtc?.ToString("O"));
        WriteField("Output", result.OutputPath is null ? "-" : DisplayPath(result.OutputPath));
        WriteField("Raw Copy", result.RawImportPath is null ? "-" : DisplayPath(result.RawImportPath));

        WriteMessages("Missing Columns", result.Validation.MissingColumns);
        WriteMessages("Warnings", result.Validation.Warnings);
        WriteMessages("Invalid Rows", result.Validation.InvalidRows);
        Console.WriteLine();

        WriteSafety();
        return result.Validation.IsValid ? 0 : 1;
    }

    private int ShowSignals()
    {
        WriteHeader("Hermes Signal Results");
        var limit = ReadLimit(_args, 8);
        var files = FindExportFiles("signals").ToList();
        if (files.Count == 0)
        {
            WriteWarning("Keine Signal-Export-Dateien gefunden.");
            WriteSafety();
            return 0;
        }

        var file = files[^1];
        Console.WriteLine($"Quelle: {DisplayPath(file)}");
        Console.WriteLine();

        foreach (var line in ReadRecentJsonlLines(file, limit))
        {
            if (!TryParseJsonLine(line, out var root))
            {
                WriteWarning("Ungueltige Signal-JSONL-Zeile uebersprungen.");
                continue;
            }

            WriteSubHeader($"{GetString(root, "symbol") ?? "UNKNOWN"} - {GetString(root, "direction") ?? "unknown"}");
            WriteField("Timestamp UTC", GetString(root, "timestamp_utc", "timestampUtc"));
            WriteField("Signal Type", GetString(root, "signal_type", "signalType"));
            WriteField("Score", $"{GetDouble(root, "score"):0.##}");
            WriteField("Confidence", $"{GetDouble(root, "confidence") * 100:0}%");
            WriteField("Theoretical Entry", $"{GetDouble(root, "theoretical_entry", "theoreticalEntry"):0.#####}");
            WriteField("Theoretical Stop", $"{GetDouble(root, "theoretical_stop", "theoreticalStop"):0.#####}");
            WriteField("Theoretical Target", $"{GetDouble(root, "theoretical_target", "theoreticalTarget"):0.#####}");
            WriteField("Reason Codes", string.Join(", ", GetStringArray(root, "reason_codes", "reasonCodes")));
            Console.WriteLine();
        }

        WriteSafety();
        return 0;
    }

    private int ShowBacktests()
    {
        WriteHeader("Hermes Backtest Reports");
        var limit = ReadLimit(_args, 8);
        var files = FindBacktestReportFiles().TakeLast(limit).ToList();
        if (files.Count == 0)
        {
            WriteWarning("Keine Backtest-Reports gefunden.");
            WriteSafety();
            return 0;
        }

        foreach (var file in files)
        {
            if (!TryLoadJson(file, out var root))
            {
                WriteWarning($"Backtest-Report nicht lesbar: {DisplayPath(file)}");
                continue;
            }

            WriteSubHeader(GetString(root, "run_id", "runId") ?? Path.GetFileNameWithoutExtension(file));
            WriteField("Symbol", GetString(root, "symbol"));
            WriteField("Timeframe", GetString(root, "timeframe"));
            WriteField("Strategy", GetString(root, "strategy_name", "strategyName"));
            WriteField("Status", GetString(root, "status"));
            WriteField("Started UTC", GetString(root, "started_at_utc", "startedAtUtc"));
            WriteField("Completed UTC", GetString(root, "completed_at_utc", "completedAtUtc"));
            WriteField("Trade Count", GetInt(root, "trade_count", "tradeCount").ToString());
            WriteField("Winrate", $"{GetDouble(root, "winrate") * 100:0}%");
            WriteField("Profit Factor", $"{GetDouble(root, "profit_factor", "profitFactor"):0.##}");
            WriteField("Max Drawdown", $"{GetDouble(root, "max_drawdown", "maxDrawdown") * 100:0.#}%");
            WriteField("Expectancy", $"{GetDouble(root, "expectancy"):0.##}");
            WriteField("no_auto_trading", GetBoolText(root, "no_auto_trading", "noAutoTrading"));
            WriteField("Notes", GetString(root, "notes"));
            WriteField("Path", DisplayPath(file));
            Console.WriteLine();
        }

        WriteSafety();
        return 0;
    }

    private int ShowOutcomes()
    {
        WriteHeader("Hermes Signal Outcomes");
        var limit = ReadLimit(_args, 8);
        var files = FindOutcomeReportFiles().TakeLast(limit).ToList();
        if (files.Count == 0)
        {
            WriteWarning("Keine Outcome-Reports gefunden.");
            WriteSafety();
            return 0;
        }

        foreach (var file in files)
        {
            if (!TryLoadJson(file, out var root))
            {
                WriteWarning($"Outcome-Report nicht lesbar: {DisplayPath(file)}");
                continue;
            }

            Console.WriteLine($"Quelle: {DisplayPath(file)}");
            Console.WriteLine();

            if (root.ValueKind == JsonValueKind.Array)
            {
                foreach (var outcome in root.EnumerateArray())
                {
                    WriteOutcome(outcome);
                }
            }
            else if (root.ValueKind == JsonValueKind.Object)
            {
                WriteOutcome(root);
            }
        }

        WriteSafety();
        return 0;
    }

    private int ShowMarketData()
    {
        WriteHeader("Hermes Historical Market Data");
        var limit = ReadLimit(_args, 2);
        var candlesRoot = Path.Combine(_dataRoot, "market_data", "candles");
        var files = FindMarketDataCandleFiles().ToList();
        if (files.Count == 0)
        {
            WriteWarning($"Keine historischen Candle-Dateien gefunden: {DisplayPath(candlesRoot)}");
            WriteSafety();
            return 0;
        }

        WriteField("Root", DisplayPath(candlesRoot));
        WriteField("Candle Files", files.Count.ToString());
        WriteField("Rows Total", files.Sum(CountJsonlRows).ToString());
        Console.WriteLine();

        foreach (var file in files)
        {
            var (symbol, timeframe) = ResolveMarketDataIdentity(candlesRoot, file);
            WriteSubHeader($"{symbol} {timeframe}");
            WriteField("Rows", CountJsonlRows(file).ToString());
            WriteField("Path", DisplayPath(file));

            foreach (var line in ReadRecentJsonlLines(file, limit))
            {
                if (!TryParseJsonLine(line, out var root))
                {
                    WriteWarning("Ungueltige Candle-JSONL-Zeile uebersprungen.");
                    continue;
                }

                var timestamp = GetString(root, "timestamp_utc", "timestampUtc") ?? "-";
                var open = GetDouble(root, "open");
                var high = GetDouble(root, "high");
                var low = GetDouble(root, "low");
                var close = GetDouble(root, "close");
                var volume = GetDouble(root, "volume");
                Console.WriteLine(
                    $"  {timestamp}  O {open:0.#####} H {high:0.#####} L {low:0.#####} C {close:0.#####} V {volume:0.##}");
            }

            Console.WriteLine();
        }

        WriteSafety();
        return 0;
    }

    private int ShowVersion()
    {
        WriteHeader("Hermes CLI Version");
        WriteField("Hermes CLI", CliVersion);
        WriteField("Runtime Root", _runtimeRoot);

        var projectPath = Path.Combine(_runtimeRoot, "Hermes.Runtime.csproj");
        if (File.Exists(projectPath))
        {
            try
            {
                var project = XDocument.Load(projectPath);
                var targetFramework = project.Descendants("TargetFramework").FirstOrDefault()?.Value;
                var assemblyName = project.Descendants("AssemblyName").FirstOrDefault()?.Value;
                WriteField("Runtime Assembly", assemblyName ?? "Hermes.Runtime");
                WriteField("Target Framework", targetFramework ?? "unknown");
            }
            catch
            {
                WriteWarning("Runtime-Projektdatei konnte nicht gelesen werden.");
            }
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int UnknownCommand(string command)
    {
        WriteError($"Unbekanntes Kommando: {command}");
        Console.WriteLine("Nutze: hermes help");
        return 2;
    }

    private IEnumerable<string> FindEventFiles()
    {
        var files = new List<string>();
        var runtimeEvents = Path.Combine(_dataRoot, "events", "runtime");
        if (Directory.Exists(runtimeEvents))
        {
            files.AddRange(Directory.EnumerateFiles(runtimeEvents, "*.jsonl"));
        }

        var legacyEvents = Path.Combine(_dataRoot, "events");
        if (Directory.Exists(legacyEvents))
        {
            files.AddRange(Directory.EnumerateFiles(legacyEvents, "*.jsonl"));
        }

        foreach (var file in files.OrderBy(File.GetLastWriteTimeUtc).ThenBy(path => path))
        {
            yield return file;
        }
    }

    private IEnumerable<string> FindExportFiles(string exportType)
    {
        var directory = Path.Combine(_dataRoot, "exports", exportType);
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.jsonl")
                     .OrderBy(File.GetLastWriteTimeUtc)
                     .ThenBy(path => path))
        {
            yield return file;
        }
    }

    private IEnumerable<string> FindBacktestReportFiles()
    {
        var directory = Path.Combine(_dataRoot, "reports", "backtests");
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.json")
                     .OrderBy(File.GetLastWriteTimeUtc)
                     .ThenBy(path => path))
        {
            yield return file;
        }
    }

    private IEnumerable<string> FindOutcomeReportFiles()
    {
        var directory = Path.Combine(_dataRoot, "reports", "outcomes");
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.json")
                     .OrderBy(File.GetLastWriteTimeUtc)
                     .ThenBy(path => path))
        {
            yield return file;
        }
    }

    private IEnumerable<string> FindNightlyResearchReports()
    {
        var directory = Path.Combine(_dataRoot, "reports", "nightly");
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.nightly.json")
                     .OrderBy(File.GetLastWriteTimeUtc)
                     .ThenBy(path => path))
        {
            yield return file;
        }
    }

    private IEnumerable<string> FindResearchSummaryReports()
    {
        var directory = Path.Combine(_dataRoot, "reports", "research");
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.research_summary.json")
                     .OrderBy(File.GetLastWriteTimeUtc)
                     .ThenBy(path => path))
        {
            yield return file;
        }
    }

    private IEnumerable<string> FindBetaReports()
    {
        var directory = Path.Combine(_dataRoot, "reports", "beta");
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.beta_report.json")
                     .OrderBy(File.GetLastWriteTimeUtc)
                     .ThenBy(path => path))
        {
            yield return file;
        }
    }

    private IEnumerable<string> FindMarketDataCandleFiles()
    {
        var directory = Path.Combine(_dataRoot, "market_data", "candles");
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.AllDirectories)
                     .OrderBy(path => path))
        {
            yield return file;
        }
    }

    private static (string Symbol, string Timeframe) ResolveMarketDataIdentity(string candlesRoot, string file)
    {
        var relative = Path.GetRelativePath(candlesRoot, file);
        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (parts.Length >= 3)
        {
            return (parts[0], parts[1]);
        }

        if (parts.Length >= 2)
        {
            var timeframe = Path.GetFileName(file).Split('.', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "-";
            return (parts[0], timeframe);
        }

        return ("UNKNOWN", Path.GetFileNameWithoutExtension(file));
    }

    private void WriteOutcome(JsonElement root)
    {
        WriteSubHeader(GetString(root, "outcome_id", "outcomeId") ?? "unknown_outcome");
        WriteField("Signal ID", GetString(root, "signal_id", "signalId"));
        WriteField("Symbol", GetString(root, "symbol"));
        WriteField("Timeframe", GetString(root, "timeframe"));
        WriteField("Direction", GetString(root, "direction"));
        WriteField("Outcome", GetString(root, "outcome_status", "outcomeStatus"));
        WriteField("Hit Target", GetBoolText(root, "hit_target", "hitTarget"));
        WriteField("Hit Stop", GetBoolText(root, "hit_stop", "hitStop"));
        WriteField("Expired", GetBoolText(root, "expired"));
        WriteField("Invalidated", GetBoolText(root, "invalidated"));
        WriteField("MFE", $"{GetDouble(root, "mfe"):0.##} R");
        WriteField("MAE", $"{GetDouble(root, "mae"):0.##} R");
        WriteField("Final R", $"{GetDouble(root, "final_r", "finalR"):0.##} R");
        WriteField("Evaluated UTC", GetString(root, "evaluated_at_utc", "evaluatedAtUtc"));
        WriteField("Notes", GetString(root, "notes"));
        Console.WriteLine();
    }

    private void WriteResearchReport(ResearchSummaryReport report)
    {
        WriteField("Run ID", report.RunId);
        WriteField("Status", report.Status);
        WriteField("Started UTC", report.StartedAtUtc.ToString("O"));
        WriteField("Completed UTC", report.CompletedAtUtc.ToString("O"));
        WriteField("Symbols Processed", report.SymbolsProcessed.Count == 0 ? "-" : string.Join(", ", report.SymbolsProcessed));
        WriteField("Candles Processed", report.CandlesProcessed.ToString());
        WriteField("Duration", $"{report.DurationSeconds:0.###} s");
        WriteField("Features", report.FeaturesGenerated.ToString());
        WriteField("Signals", report.SignalsGenerated.ToString());
        WriteField("Outcomes", report.OutcomesGenerated.ToString());
        WriteField("Backtests", report.BacktestsGenerated.ToString());
        WriteField("Reports", report.ReportsGenerated.ToString());
        WriteField("Feature Output", DisplayOptionalPath(report.FeatureOutputPath));
        WriteField("Signal Output", DisplayOptionalPath(report.SignalOutputPath));
        WriteField("Outcome Report", DisplayOptionalPath(report.OutcomeReportPath));
        WriteField("Backtest Report", DisplayOptionalPath(report.BacktestReportPath));
        WriteField("Nightly Report", DisplayOptionalPath(report.NightlyReportPath));
        WriteField("Research Report", DisplayOptionalPath(report.ResearchReportPath));
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
    }

    private void WriteBetaReport(TradingLearningBetaReport report)
    {
        WriteField("Run ID", report.RunId);
        WriteField("Status", report.Status);
        WriteField("Started UTC", report.StartedAtUtc.ToString("O"));
        WriteField("Completed UTC", report.CompletedAtUtc.ToString("O"));
        WriteField("Symbols Processed", report.SymbolsProcessed.Count == 0 ? "-" : string.Join(", ", report.SymbolsProcessed));
        WriteField("Candles Processed", report.CandlesProcessed.ToString());
        WriteField("Features", report.FeaturesGenerated.ToString());
        WriteField("Signals", report.SignalsGenerated.ToString());
        WriteField("Outcomes", report.OutcomesGenerated.ToString());
        WriteField("Backtests", report.BacktestsGenerated.ToString());
        WriteField("Duration", $"{report.DurationSeconds:0.###} s");
        WriteField("learning_ready", report.LearningReady.ToString().ToLowerInvariant());
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        WriteField("Beta Report", DisplayOptionalPath(report.BetaReportPath));
        WriteField("Research Report", DisplayOptionalPath(report.ResearchReportPath));
        WriteField("Feature Output", DisplayOptionalPath(report.FeatureOutputPath));
        WriteField("Signal Output", DisplayOptionalPath(report.SignalOutputPath));
        WriteField("Outcome Report", DisplayOptionalPath(report.OutcomeReportPath));
        WriteField("Backtest Report", DisplayOptionalPath(report.BacktestReportPath));
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
    }

    private static IReadOnlyList<string> ReadRecentJsonlLines(string file, int limit)
    {
        return File.ReadLines(file)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .TakeLast(limit)
            .ToList();
    }

    private static int CountJsonlRows(string file)
    {
        try
        {
            return File.ReadLines(file).Count(line => !string.IsNullOrWhiteSpace(line));
        }
        catch
        {
            return 0;
        }
    }

    private static int ReadLimit(string[] args, int fallback)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == "--limit" && int.TryParse(args[index + 1], out var value))
            {
                return Math.Clamp(value, 1, 100);
            }
        }

        return fallback;
    }

    private static double ReadHours(string[] args, double fallback)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index].Equals("--hours", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return Math.Clamp(value, 0.01, 168);
            }
        }

        return fallback;
    }

    private static int ReadIntOption(string[] args, string name, int fallback, int min, int max)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(args[index + 1], out var value))
            {
                return Math.Clamp(value, min, max);
            }
        }

        return fallback;
    }

    private static string? ReadOption(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    private static bool TryParseCliDate(string value, out DateTimeOffset timestampUtc)
    {
        var styles = DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, styles, out var parsed))
        {
            timestampUtc = parsed.ToUniversalTime();
            return true;
        }

        timestampUtc = default;
        return false;
    }

    private static TimeSpan TimeframeInterval(string timeframe) =>
        timeframe.ToUpperInvariant() switch
        {
            "H4" => TimeSpan.FromHours(4),
            "H1" => TimeSpan.FromHours(1),
            "M15" => TimeSpan.FromMinutes(15),
            "M5" => TimeSpan.FromMinutes(5),
            _ => TimeSpan.FromMinutes(5)
        };

    private static void WriteMessages(string label, IReadOnlyList<string> messages)
    {
        if (messages.Count == 0)
        {
            return;
        }

        WriteField(label, string.Empty);
        foreach (var message in messages)
        {
            Console.WriteLine($"  - {message}");
        }
    }

    private bool TryLoadJson(string path, out JsonElement root)
    {
        root = default;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            root = document.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseJsonLine(string line, out JsonElement root)
    {
        root = default;
        try
        {
            using var document = JsonDocument.Parse(line);
            root = document.RootElement.Clone();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetProperty(JsonElement root, out JsonElement value, params string[] names)
    {
        value = default;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (names.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static string? GetString(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => value.GetRawText()
        };
    }

    private static int GetInt(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        return int.TryParse(GetString(root, names), out var parsed) ? parsed : 0;
    }

    private static double GetDouble(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return 0;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
        {
            return number;
        }

        return double.TryParse(GetString(root, names), out var parsed) ? parsed : 0;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToList();
    }

    private static string GetBoolText(JsonElement root, params string[] names)
    {
        if (!TryGetProperty(root, out var value, names))
        {
            return "unknown";
        }

        return value.ValueKind switch
        {
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => GetString(root, names) ?? "unknown"
        };
    }

    private static string FirstCommand(string[] args) => CommandAt(args, 0);

    private static string CommandAt(string[] args, int commandIndex)
    {
        var commands = new List<string>();
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (arg is "--root" or "--limit" or "--hours" or "--max-requests")
            {
                index++;
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                commands.Add(arg);
            }
        }

        return commandIndex < commands.Count ? commands[commandIndex] : string.Empty;
    }

    private sealed record HistoricalDownloadPageResult(
        IReadOnlyList<MarketDataCandle> Candles,
        int Requests,
        int DuplicatesSkipped,
        bool Truncated,
        IReadOnlyList<string> Warnings);

    private sealed record AutopilotDownloadPlan(
        CTraderHistoricalDataRequest Request,
        string Reason);

    private sealed record AutopilotDownloadResult(
        CTraderHistoricalDataRequest Request,
        CTraderTrendbarImportResult ImportResult,
        int Requests,
        int DuplicatesSkipped,
        bool Truncated,
        IReadOnlyList<string> Warnings);

    private sealed record StrategyResearchStepResult(
        StrategyResearchMemory Memory,
        int TestedNow,
        string InsightsPath,
        string ClustersPath,
        StrategyEvolutionSummary Insights);

    private static string ResolveRuntimeRoot(string[] args)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index] == "--root")
            {
                return Path.GetFullPath(args[index + 1]);
            }
        }

        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var directProject = Path.Combine(directory.FullName, "Hermes.Runtime.csproj");
                if (File.Exists(directProject))
                {
                    return directory.FullName;
                }

                var nestedProject = Path.Combine(directory.FullName, "HermesRuntime", "Hermes.Runtime.csproj");
                if (File.Exists(nestedProject))
                {
                    return Path.Combine(directory.FullName, "HermesRuntime");
                }

                directory = directory.Parent;
            }
        }

        return Directory.GetCurrentDirectory();
    }

    private StoragePaths BuildStoragePaths()
    {
        var profilePath = Path.Combine(_runtimeRoot, "config", "storage.profile.json");
        if (File.Exists(profilePath))
        {
            var paths = StorageProfile.Load(profilePath).ToPaths(Path.GetDirectoryName(profilePath) ?? _runtimeRoot);
            EnsureStorageDirectories(paths);
            return paths;
        }

        var fallbackPaths = BuildFallbackStoragePaths(_dataRoot);
        EnsureStorageDirectories(fallbackPaths);
        return fallbackPaths;
    }

    private static string ResolveDataRoot(string runtimeRoot)
    {
        var profilePath = Path.Combine(runtimeRoot, "config", "storage.profile.json");
        if (!File.Exists(profilePath))
        {
            return Path.Combine(runtimeRoot, "data");
        }

        try
        {
            return StorageProfile.Load(profilePath)
                .ToPaths(Path.GetDirectoryName(profilePath) ?? runtimeRoot)
                .Root;
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidOperationException)
        {
            return Path.Combine(runtimeRoot, "data");
        }
    }

    private static StoragePaths BuildFallbackStoragePaths(string dataRoot)
    {
        return new StoragePaths(
            Root: dataRoot,
            Events: Path.Combine(dataRoot, "events"),
            Snapshots: Path.Combine(dataRoot, "snapshots"),
            Logs: Path.Combine(dataRoot, "logs"),
            Cache: Path.Combine(dataRoot, "cache"),
            Jobs: Path.Combine(dataRoot, "jobs"),
            Archive: Path.Combine(dataRoot, "archive"));
    }

    private static void EnsureStorageDirectories(StoragePaths paths)
    {
        foreach (var directory in paths.AllDirectories)
        {
            Directory.CreateDirectory(directory);
        }
    }

    private CTraderOpenApiConfigLoadResult LoadCTraderConfig()
    {
        var loader = new CTraderOpenApiConfigLoader();
        return loader.Load(_runtimeRoot);
    }

    private static (
        CTraderOAuthUrlResult OAuthUrl,
        CTraderAuthStatus AuthStatus,
        CTraderAuthTokenState TokenState) BuildCTraderAuthContext(
            CTraderOpenApiConfigLoadResult configLoad,
            StoragePaths storagePaths)
    {
        var oauthUrl = new CTraderOAuthUrlBuilder().Build(configLoad.Config);
        var tokenStore = new CTraderTokenStore(storagePaths);
        var authStatus = tokenStore.GetStatus(configLoad.Config, configLoad, oauthUrl);
        var tokenState = new CTraderAuthTokenState(
            AuthConfigured: authStatus.AuthConfigured,
            TokenAvailable: authStatus.TokenLoaded,
            AuthMode: authStatus.AuthMode,
            TokenCachePath: authStatus.TokenStorePath,
            Warnings: authStatus.Warnings);

        return (oauthUrl, authStatus, tokenState);
    }

    private static IReadOnlyList<string> CombineWarnings(params IEnumerable<string>[] groups)
    {
        return groups
            .SelectMany(group => group)
            .Where(warning => !string.IsNullOrWhiteSpace(warning))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static void PublishCTraderEvent(
        EventBus eventBus,
        EventType eventType,
        EventSeverity severity,
        object payload)
    {
        eventBus.Publish(EventEnvelope.Create(
            eventType,
            "hermes_ctrader_openapi",
            severity,
            CliVersion,
            payload));
    }

    private static long DirectorySize(string directory)
    {
        try
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Select(file =>
                {
                    try
                    {
                        return new FileInfo(file).Length;
                    }
                    catch
                    {
                        return 0L;
                    }
                })
                .Sum();
        }
        catch
        {
            return 0;
        }
    }

    private string DisplayPath(string path)
    {
        var relative = Path.GetRelativePath(_runtimeRoot, path);
        return relative.StartsWith("..", StringComparison.Ordinal) ? path : relative;
    }

    private string DisplayOptionalPath(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? "-" : DisplayPath(path);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var size = (double)bytes;
        var unit = 0;

        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.##} {units[unit]}";
    }

    private static string CultureTitle(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static void WriteHeader(string text)
    {
        WriteColored(text, ConsoleColor.Cyan);
        Console.WriteLine(new string('-', text.Length));
    }

    private static void WriteSubHeader(string text)
    {
        WriteColored(text, ConsoleColor.DarkCyan);
    }

    private static void WriteField(string label, string? value)
    {
        Console.Write(label.PadRight(24));
        Console.WriteLine(value ?? "-");
    }

    private static void WriteEvent(string timestamp, string severity, string eventType, string source, string message)
    {
        Console.Write($"{timestamp} ");
        WriteColored($"[{severity}]", SeverityColor(severity), newline: false);
        Console.WriteLine($" {eventType} ({source})");
        Console.WriteLine($"  {message}");
    }

    private static void WriteSafety()
    {
        Console.WriteLine("Safety: keine Runtime-Steuerung, keine Trading-Ausfuehrung, no_auto_trading sichtbar, human_review_required sichtbar.");
    }

    private static void WriteWarning(string message) => WriteColored($"WARN: {message}", ConsoleColor.Yellow);

    private static void WriteError(string message) => WriteColored($"ERROR: {message}", ConsoleColor.Red);

    private static ConsoleColor SeverityColor(string severity)
    {
        return severity.ToLowerInvariant() switch
        {
            "warning" or "warn" => ConsoleColor.Yellow,
            "error" or "critical" => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };
    }

    private static void WriteColored(string text, ConsoleColor color, bool newline = true)
    {
        if (Console.IsOutputRedirected)
        {
            if (newline)
            {
                Console.WriteLine(text);
            }
            else
            {
                Console.Write(text);
            }

            return;
        }

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        if (newline)
        {
            Console.WriteLine(text);
        }
        else
        {
            Console.Write(text);
        }

        Console.ForegroundColor = previous;
    }
}
