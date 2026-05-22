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
        _dataRoot = Path.Combine(_runtimeRoot, "data");
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
            "download-history" => DownloadCTraderHistory(),
            "import-csv" => ImportCsv(),
            "generate-features" => GenerateFeatures(),
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
        Console.WriteLine("Lokale Sicherheits-CLI fuer HermesRuntime. Status-Kommandos sind read-only; generate-features schreibt nur lokale Analyseartefakte.");
        Console.WriteLine();
        Console.WriteLine("Kommandos:");
        Console.WriteLine("  hermes health             RuntimeHealth anzeigen");
        Console.WriteLine("  hermes setup-watch        Setup-Watch-Kandidaten anzeigen");
        Console.WriteLine("  hermes events recent      letzte Runtime-Events anzeigen");
        Console.WriteLine("  hermes jobs               Queue/Jobjournale anzeigen");
        Console.WriteLine("  hermes storage            lokale Storage-Uebersicht anzeigen");
        Console.WriteLine("  hermes ctrader-health     cTrader Open API Stub-Health anzeigen");
        Console.WriteLine("  hermes ctrader-symbols    cTrader Symbol-Mapping anzeigen");
        Console.WriteLine("  hermes download-history   historische Stub-Candles lokal erzeugen");
        Console.WriteLine("  hermes import-csv         cTrader Candle-CSV lokal importieren");
        Console.WriteLine("  hermes generate-features  FeatureVectors aus lokalen Candle-Daten erzeugen");
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
        WriteField("Feature Rows", result.FeatureCount.ToString());
        WriteField("Symbols", string.Join(", ", result.Job.Symbols));
        WriteField("Timeframes", string.Join(", ", result.Job.Timeframes));
        Console.WriteLine();

        if (result.FeatureCount == 0)
        {
            WriteWarning("Keine Features erzeugt. Pruefe lokale Candle-Daten unter data/market_data/candles/.");
        }

        WriteSafety();
        return 0;
    }

    private int ShowCTraderHealth()
    {
        WriteHeader("Hermes cTrader Open API Health");
        var config = LoadCTraderConfig(out var configPath, out var localConfigLoaded);
        var mapper = new CTraderSymbolMapper(config.AllowedSymbols);
        var client = new CTraderHistoricalDataClientStub(config, mapper);
        var health = client.CheckHealth();

        using var eventStore = new EventStore(BuildStoragePaths());
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);
        PublishCTraderEvent(
            eventBus,
            EventType.CTraderConnectorHealthChecked,
            EventSeverity.Info,
            new
            {
                message = "cTrader Open API connector health checked. Stub only, no live connection.",
                health,
                configPath,
                localConfigLoaded,
                noAutoTrading = true,
                humanReviewRequired = true
            });
        eventStore.Flush();

        Console.WriteLine("Open API connector stub active");
        WriteField("Status", health.Status);
        WriteField("Environment", health.Environment);
        WriteField("Stub Active", health.StubActive.ToString().ToLowerInvariant());
        WriteField("Auth Configured", health.AuthConfigured.ToString().ToLowerInvariant());
        WriteField("Client ID Configured", health.ClientIdConfigured.ToString().ToLowerInvariant());
        WriteField("Account ID Configured", health.AccountIdConfigured.ToString().ToLowerInvariant());
        WriteField("no_orders", health.NoOrders.ToString().ToLowerInvariant());
        WriteField("Read-only Market Data", health.ReadOnlyMarketData.ToString().ToLowerInvariant());
        WriteField("Config", DisplayPath(configPath));
        WriteField("Local Config Loaded", localConfigLoaded.ToString().ToLowerInvariant());
        WriteMessages("Warnings", health.Warnings);
        Console.WriteLine();

        WriteSafety();
        return 0;
    }

    private int ShowCTraderSymbols()
    {
        WriteHeader("Hermes cTrader Symbol Mapping");
        var config = LoadCTraderConfig(out var configPath, out var localConfigLoaded);
        var mapper = new CTraderSymbolMapper(config.AllowedSymbols);

        Console.WriteLine("Open API connector stub active");
        WriteField("Config", DisplayPath(configPath));
        WriteField("Local Config Loaded", localConfigLoaded.ToString().ToLowerInvariant());
        WriteField("Allowed Timeframes", string.Join(", ", config.AllowedTimeframes));
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
                message = "cTrader historical download started. Stub only, no live Open API call.",
                request.Symbol,
                request.Timeframe,
                request.FromUtc,
                request.ToUtc,
                stubActive = true,
                noAutoTrading = true,
                humanReviewRequired = true
            });

        try
        {
            var config = LoadCTraderConfig(out var configPath, out var localConfigLoaded);
            var mapper = new CTraderSymbolMapper(config.AllowedSymbols);
            var client = new CTraderHistoricalDataClientStub(config, mapper);
            var candles = client.DownloadHistoricalCandles(request);
            var importer = new CTraderTrendbarImporter(storagePaths);
            var result = importer.ImportStubCandles(request, candles);

            PublishCTraderEvent(
                eventBus,
                EventType.CTraderHistoricalDownloadCompleted,
                EventSeverity.Info,
                new
                {
                    message = "cTrader historical download completed with stub data. No real cTrader data was loaded.",
                    result.DownloadId,
                    result.Symbol,
                    result.Timeframe,
                    result.OutputPath,
                    result.CandleCount,
                    result.FromUtc,
                    result.ToUtc,
                    result.StubData,
                    configPath,
                    localConfigLoaded,
                    noAutoTrading = true,
                    humanReviewRequired = true
                });
            eventStore.Flush();

            Console.WriteLine("Open API connector stub active");
            Console.WriteLine("No real cTrader data was loaded.");
            WriteField("Download ID", result.DownloadId);
            WriteField("Symbol", result.Symbol);
            WriteField("Timeframe", result.Timeframe);
            WriteField("Rows", result.CandleCount.ToString());
            WriteField("From UTC", result.FromUtc?.ToString("O"));
            WriteField("To UTC", result.ToUtc?.ToString("O"));
            WriteField("Output", DisplayPath(result.OutputPath));
            WriteField("Stub Data", result.StubData.ToString().ToLowerInvariant());
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
                    message = "cTrader historical download failed before any trading action. Stub only.",
                    request.Symbol,
                    request.Timeframe,
                    request.FromUtc,
                    request.ToUtc,
                    error = ex.Message,
                    stubActive = true,
                    noAutoTrading = true,
                    humanReviewRequired = true
                });
            eventStore.Flush();

            WriteError(ex.Message);
            WriteSafety();
            return 1;
        }
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
            if (arg is "--root" or "--limit")
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
        return new StoragePaths(
            Root: _dataRoot,
            Events: Path.Combine(_dataRoot, "events"),
            Snapshots: Path.Combine(_dataRoot, "snapshots"),
            Logs: Path.Combine(_dataRoot, "logs"),
            Cache: Path.Combine(_dataRoot, "cache"),
            Jobs: Path.Combine(_dataRoot, "jobs"),
            Archive: Path.Combine(_dataRoot, "archive"));
    }

    private CTraderOpenApiConfig LoadCTraderConfig(out string configPath, out bool localConfigLoaded)
    {
        var localPath = Path.Combine(_runtimeRoot, "config", "ctrader.openapi.local.json");
        if (File.Exists(localPath))
        {
            configPath = localPath;
            localConfigLoaded = true;
            return CTraderOpenApiConfig.LoadOrDefault(localPath);
        }

        var examplePath = Path.Combine(_runtimeRoot, "config", "ctrader.openapi.example.json");
        configPath = examplePath;
        localConfigLoaded = false;
        return CTraderOpenApiConfig.LoadOrDefault(examplePath);
    }

    private static void PublishCTraderEvent(
        EventBus eventBus,
        EventType eventType,
        EventSeverity severity,
        object payload)
    {
        eventBus.Publish(EventEnvelope.Create(
            eventType,
            "hermes_ctrader_openapi_stub",
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
