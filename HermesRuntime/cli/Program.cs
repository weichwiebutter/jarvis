using Hermes.Runtime;
using System.Diagnostics;
using System.Globalization;
using System.Net;
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
            "run-nightly-beta3" => RunNightlyBeta3(),
            "nightly-status" => ShowNightlyStatus(),
            "nightly-stop-request" => RequestNightlyStop(),
            "scheduler-status" => ShowSchedulerStatus(),
            "scheduler-jobs" => ShowSchedulerJobs(),
            "readonly-bridge" or "bridge-start" => StartReadOnlyBridge(),
            "supervisor-start" => StartSupervisor(),
            "supervisor-status" => ShowSupervisorStatus(),
            "supervisor-stop-request" => RequestSupervisorStop(),
            "resource-status" => ShowResourceStatus(),
            "storage-status" => ShowStorageStatus(),
            "cleanup-plan" => ShowCleanupPlan(),
            "cleanup-apply" => ApplyCleanup(),
            "research-status" => ShowResearchStatus(),
            "research-report" => ShowResearchReport(),
            "run-beta-learning" => RunBetaLearning(),
            "beta-status" => ShowBetaStatus(),
            "update-research-memory" => UpdateResearchMemory(),
            "research-memory" => ShowResearchMemory(),
            "run-long-research" => RunLongResearch(),
            "run-research-autopilot" => RunResearchAutopilot(),
            "run-strategy-research" => RunStrategyResearch(),
            "run-walkforward-validation" => RunWalkForwardValidation(),
            "realism-report" => ShowRealismReport(),
            "walkforward-summary" => ShowWalkForwardSummary(),
            "cost-sensitivity-report" => ShowCostSensitivityReport(),
            "monte-carlo-report" => ShowMonteCarloReport(),
            "cost-stress-report" => ShowCostStressReport(),
            "risk-of-ruin-report" => ShowRiskOfRuinReport(),
            "simulation-status" => ShowSimulationStatus(),
            "strategy-discovery-status" => ShowStrategyDiscoveryStatus(),
            "overfit-report" => ShowOverfitReport(),
            "robust-strategies" => ShowRobustStrategies(),
            "cognitive-status" => ShowCognitiveStatus(),
            "scan-knowledge-sources" => ScanKnowledgeSources(),
            "domain-status" => ShowDomainStatus(),
            "domain-insights" => ShowDomainInsights(),
            "software-domain-status" => ShowSingleDomainStatus("software"),
            "scan-software-domain" => ScanDomain("software"),
            "documentation-domain-status" => ShowSingleDomainStatus("documentation"),
            "scan-documentation-domain" => ScanDomain("documentation"),
            "process-domain-status" => ShowSingleDomainStatus("process"),
            "scan-process-domain" => ScanDomain("process"),
            "research-domain-status" => ShowSingleDomainStatus("research"),
            "scan-research-domain" => ScanDomain("research"),
            "knowledge-catalog" => ShowKnowledgeCatalog(),
            "knowledge-item" => ShowKnowledgeItem(),
            "research-queue" => ShowResearchQueue(),
            "enqueue-research" => EnqueueResearch(),
            "process-research-queue" => ProcessResearchQueue(),
            "generate-hypotheses" => GenerateHypotheses(),
            "cognitive-insights" => ShowCognitiveInsights(),
            "planning-status" => ShowPlanningStatus(),
            "detect-needs" => DetectNeeds(),
            "plan-next-tasks" => PlanNextTasks(),
            "run-planning-cycle" => RunPlanningCycle(),
            "execute-planned-tasks" => ExecutePlannedTasks(),
            "planned-task-status" => ShowPlannedTaskStatus(),
            "task-execution-log" => ShowTaskExecutionLog(),
            "evaluate-task-outcomes" => EvaluateTaskOutcomes(),
            "outcome-feedback-status" => ShowOutcomeFeedbackStatus(),
            "planner-feedback" => ShowPlannerFeedback(),
            "goal-feedback" => ShowGoalFeedback(),
            "run-autonomous-loop" => RunAutonomousLoop(),
            "autonomous-loop-status" => ShowAutonomousLoopStatus(),
            "autonomous-loop-log" => ShowAutonomousLoopLog(),
            "explain-last-loop" => ExplainLastLoop(),
            "meta-review" => ShowMetaReview(),
            "domain-health" => ShowDomainHealth(),
            "learning-strategy" => ShowLearningStrategy(),
            "governance-status" => ShowGovernanceStatus(),
            "explain-plan" => ExplainPlan(),
            "explain-task" => ExplainTask(),
            "bot-candidates" => ShowBotCandidates(),
            "bot-candidate-report" => ShowBotCandidateReport(),
            "candidate-rejection-analysis" => ShowCandidateRejectionAnalysis(),
            "near-miss-strategies" => ShowNearMissStrategies(),
            "improvement-experiments" => ShowImprovementExperiments(),
            "run-quality-improvement-experiments" => RunQualityImprovementExperiments(),
            "quality-improvement-report" => ShowQualityImprovementReport(),
            "cost-resilience-report" => ShowCostResilienceReport(),
            "oos-stability-report" => ShowOosStabilityReport(),
            "risk-sensitivity-report" => ShowRiskSensitivityReport(),
            "strategy-research-status" => ShowStrategyResearchStatus(),
            "top-strategies" => ShowTopStrategies(),
            "knowledge-sources" => ShowKnowledgeSources(),
            "research-insights" => ShowResearchInsights(),
            "strategy-clusters" => ShowStrategyClusters(),
            "regime-summary" => ShowRegimeSummary(),
            "strategy-regime-performance" => ShowStrategyRegimePerformance(),
            "regime-distribution" => ShowRegimeDistribution(),
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
        Console.WriteLine("  hermes run-nightly-beta3 Nightly Beta 3 Research-Orchestrierung starten");
        Console.WriteLine("  hermes nightly-status    Nightly Beta 3 Status anzeigen");
        Console.WriteLine("  hermes nightly-stop-request sicheren Stop-Request fuer Nightly Beta 3 setzen");
        Console.WriteLine("  hermes scheduler-status  internen Hermes Scheduler Status anzeigen");
        Console.WriteLine("  hermes scheduler-jobs    geplante Hermes Jobs anzeigen");
        Console.WriteLine("  hermes readonly-bridge   localhost Read-only Bridge fuer Jarvis Control Center starten");
        Console.WriteLine("  hermes supervisor-start  langlebigen Hermes Supervisor starten");
        Console.WriteLine("  hermes supervisor-status Supervisor Heartbeat/State anzeigen");
        Console.WriteLine("  hermes supervisor-stop-request sicheren Supervisor Stop Request setzen");
        Console.WriteLine("  hermes resource-status   CPU/RAM/Disk ResourceGuard anzeigen");
        Console.WriteLine("  hermes storage-status    Storage-/Retention-Status anzeigen");
        Console.WriteLine("  hermes cleanup-plan      sicheren Storage Cleanup-Plan erzeugen");
        Console.WriteLine("  hermes cleanup-apply --safe sicheren Cleanup-Plan anwenden");
        Console.WriteLine("  hermes research-status    letzten Nightly-Research-Report anzeigen");
        Console.WriteLine("  hermes research-report    letzten ResearchSummaryReport anzeigen");
        Console.WriteLine("  hermes run-beta-learning  Trading Learning Beta 1 lokal ausfuehren");
        Console.WriteLine("  hermes beta-status        letzten Trading Learning Beta Report anzeigen");
        Console.WriteLine("  hermes update-research-memory Research Memory Index aktualisieren");
        Console.WriteLine("  hermes research-memory    Research Memory Index anzeigen");
        Console.WriteLine("  hermes run-long-research  checkpointed Long-Run Research starten");
        Console.WriteLine("  hermes run-research-autopilot kombinierte Data-/Pattern-/Strategy-Research-Pipeline starten");
        Console.WriteLine("  hermes run-strategy-research adaptive Strategy-Research-Varianten bewerten");
        Console.WriteLine("  hermes run-walkforward-validation Walk-Forward-/Overfit-Validation ausfuehren");
        Console.WriteLine("  hermes realism-report    Realism-/Kosten-/Overfit-Qualitaetsreport anzeigen");
        Console.WriteLine("  hermes walkforward-summary Walk-Forward Summary anzeigen");
        Console.WriteLine("  hermes cost-sensitivity-report Brokerkosten-Sensitivitaetsreport anzeigen");
        Console.WriteLine("  hermes monte-carlo-report Monte-Carlo-Qualitaetsreport erzeugen/anzeigen");
        Console.WriteLine("  hermes cost-stress-report Spread-/Slippage-Stress-Test anzeigen");
        Console.WriteLine("  hermes risk-of-ruin-report konservativen Risk-of-Ruin-Report anzeigen");
        Console.WriteLine("  hermes simulation-status Realistic Simulation Status anzeigen");
        Console.WriteLine("  hermes strategy-discovery-status Trusted Strategy Discovery anzeigen");
        Console.WriteLine("  hermes overfit-report     Overfit-/Risk-Report anzeigen");
        Console.WriteLine("  hermes robust-strategies  robuste Strategy-Kandidaten anzeigen");
        Console.WriteLine("  hermes cognitive-status   Cognitive Core Status anzeigen");
        Console.WriteLine("  hermes scan-knowledge-sources Knowledge Sources read-only scannen");
        Console.WriteLine("  hermes domain-status      aktive Cognitive Domains anzeigen");
        Console.WriteLine("  hermes domain-insights    Multi-Domain Insights anzeigen");
        Console.WriteLine("  hermes scan-software-domain Software-Domaene lokal scannen");
        Console.WriteLine("  hermes scan-documentation-domain Dokumentations-Domaene lokal scannen");
        Console.WriteLine("  hermes scan-process-domain Prozess-Domaene lokal scannen");
        Console.WriteLine("  hermes scan-research-domain Research-Domaene metadata-only scannen");
        Console.WriteLine("  hermes knowledge-catalog  allgemeinen Cognitive Knowledge Catalog anzeigen");
        Console.WriteLine("  hermes knowledge-item --id <ID> einzelnes Knowledge Item anzeigen");
        Console.WriteLine("  hermes research-queue     Cognitive Research Queue anzeigen");
        Console.WriteLine("  hermes enqueue-research --domain trading --type validation Research-Item einreihen");
        Console.WriteLine("  hermes process-research-queue --max-items 50 Research Queue verarbeiten");
        Console.WriteLine("  hermes generate-hypotheses --domain trading Cross-Knowledge-Hypothesen erzeugen");
        Console.WriteLine("  hermes cognitive-insights Cognitive Insights anzeigen");
        Console.WriteLine("  hermes planning-status  Autonomous Planning Status anzeigen");
        Console.WriteLine("  hermes detect-needs     aktuelle Bedarfe erkennen");
        Console.WriteLine("  hermes plan-next-tasks --max-items 20 Aufgaben aus Needs/Goals planen");
        Console.WriteLine("  hermes run-planning-cycle --max-items 20 Planning Cycle ausfuehren und Research Queue aktualisieren");
        Console.WriteLine("  hermes execute-planned-tasks --max-items 10 geplante Aufgaben kontrolliert ausfuehren");
        Console.WriteLine("  hermes planned-task-status Planned Task Execution Status anzeigen");
        Console.WriteLine("  hermes task-execution-log Planned Task Execution Log anzeigen");
        Console.WriteLine("  hermes evaluate-task-outcomes --max-items 50 ausgefuehrte Planned Tasks bewerten");
        Console.WriteLine("  hermes outcome-feedback-status Outcome Feedback Status anzeigen");
        Console.WriteLine("  hermes planner-feedback Planner Feedback anzeigen");
        Console.WriteLine("  hermes goal-feedback Goal Feedback anzeigen");
        Console.WriteLine("  hermes run-autonomous-loop --max-iterations 5 vollstaendigen Need->Insight Lernloop ausfuehren");
        Console.WriteLine("  hermes autonomous-loop-status Autonomous Learning Loop Status anzeigen");
        Console.WriteLine("  hermes autonomous-loop-log Autonomous Learning Loop JSONL-Auszug anzeigen");
        Console.WriteLine("  hermes explain-last-loop letzte autonome Loop-Iteration erklaeren");
        Console.WriteLine("  hermes meta-review Meta-Learning Review erzeugen/anzeigen");
        Console.WriteLine("  hermes domain-health Domain Health Scores anzeigen");
        Console.WriteLine("  hermes learning-strategy aktuelle Lernstrategie anzeigen");
        Console.WriteLine("  hermes governance-status Governance Entscheidungen anzeigen");
        Console.WriteLine("  hermes explain-plan     letzte Planning-Entscheidung erklaeren");
        Console.WriteLine("  hermes explain-task --id <TASK_ID> einzelne geplante Aufgabe erklaeren");
        Console.WriteLine("  hermes bot-candidates     strenge Demo-Bot-Kandidatenbewertung anzeigen");
        Console.WriteLine("  hermes bot-candidate-report Bot-Candidate-Report mit Ablehnungsgruenden anzeigen");
        Console.WriteLine("  hermes candidate-rejection-analysis Ablehnungsdiagnose fuer Bot-Kandidaten anzeigen");
        Console.WriteLine("  hermes near-miss-strategies beinahe geeignete verworfene Strategien anzeigen");
        Console.WriteLine("  hermes improvement-experiments naechste Research-Experimente anzeigen");
        Console.WriteLine("  hermes run-quality-improvement-experiments gezielte OOS-/Cost-/Risk-Experimente erzeugen");
        Console.WriteLine("  hermes quality-improvement-report Quality-Improvement-Experimentbericht anzeigen");
        Console.WriteLine("  hermes cost-resilience-report Cost-Resilience-Experimente anzeigen");
        Console.WriteLine("  hermes oos-stability-report OOS-/Walk-Forward-Experimente anzeigen");
        Console.WriteLine("  hermes risk-sensitivity-report Risk-Sensitivity-Experimente anzeigen");
        Console.WriteLine("  hermes strategy-research-status Strategy-Research-Memory anzeigen");
        Console.WriteLine("  hermes top-strategies     beste Strategy-Research-Varianten anzeigen");
        Console.WriteLine("  hermes knowledge-sources  kuratierte Strategy-Discovery-Quellen anzeigen");
        Console.WriteLine("  hermes research-insights  Strategy-Research-Insights anzeigen");
        Console.WriteLine("  hermes strategy-clusters  Strategy-Cluster anzeigen");
        Console.WriteLine("  hermes regime-summary     Market-Regime-Zusammenfassung erzeugen/anzeigen");
        Console.WriteLine("  hermes strategy-regime-performance Strategy-Performance nach Regime anzeigen");
        Console.WriteLine("  hermes regime-distribution Regime-Verteilung nach Symbol/Timeframe anzeigen");
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

    private int StartReadOnlyBridge()
    {
        var url = ReadOption(_args, "--url") ?? "http://127.0.0.1:8787/";
        if (!url.EndsWith("/", StringComparison.Ordinal))
        {
            url += "/";
        }

        var storagePaths = BuildReadOnlyStoragePaths();
        var bridge = new HermesReadOnlyBridge(storagePaths);
        using var cancellation = new CancellationTokenSource();

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            bridge.RunAsync(url, cancellation.Token).GetAwaiter().GetResult();
        }
        catch (HttpListenerException ex)
        {
            WriteError($"Read-only Bridge konnte nicht starten: {ex.Message}");
            Console.WriteLine("Hinweis: Nutze einen freien localhost-Port, z. B. --url http://127.0.0.1:8788/");
            WriteSafety();
            return 1;
        }

        Console.WriteLine("Read-only Bridge wurde beendet.");
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

    private int RunNightlyBeta3()
    {
        WriteHeader("Hermes Nightly Robust Research Beta 3");
        var storagePaths = BuildStoragePaths();
        var configPath = Path.Combine(_runtimeRoot, "config", "nightly.research.json");
        var nightly = new NightlyResearchService(storagePaths, configPath);
        var config = nightly.LoadConfig();
        var now = DateTimeOffset.Now;
        var maxRuntimeHours = ReadDoubleOption(_args, "--max-runtime-hours", config.MaxRuntimeHours, min: 0.01, max: 24);
        var sleepSeconds = ReadIntOption(
            _args,
            "--sleep-seconds",
            fallback: config.SleepSecondsBetweenIterations,
            min: 0,
            max: 3600);
        var maxIdleIterations = ReadIntOption(
            _args,
            "--max-idle-iterations",
            fallback: config.MaxIdleIterations,
            min: 1,
            max: 1000);
        var maxQualityCandidates = ReadIntOption(
            _args,
            "--max-quality-candidates",
            fallback: 64,
            min: 1,
            max: 500);

        WriteField("Config", DisplayPath(configPath));
        WriteField("Allowed Window", $"{config.StartHour:00}:00 -> {config.EndHour:00}:00");
        WriteField("Current Local Time", now.ToString("yyyy-MM-dd HH:mm:ss zzz"));
        WriteField("Max Quality Candidates", maxQualityCandidates.ToString());
        var schedulerStatus = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json")).GetStatus();
        var cognitiveJobsEnabled = AreCognitiveJobsEnabled(schedulerStatus);
        var cognitiveNightly = new CognitiveNightlyService(storagePaths);
        var cognitiveSummary = cognitiveNightly.LoadSummary();
        var autonomousLoop = new AutonomousLearningLoop(storagePaths, Path.Combine(_runtimeRoot, "config", "autonomous.loop.json"));

        if (!config.IsInAllowedWindow(now))
        {
            var existing = nightly.LoadState();
            var state = nightly.WriteState(WithCognitiveState(existing with
            {
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Status = "outside_nightly_window",
                NextAction = "wait_for_23_00_to_05_00_window",
                NextScheduledStartUtc = NightlyResearchService.CalculateNextScheduledStart(config, now).ToUniversalTime(),
                CurrentlyRunning = false,
                ProcessId = null,
                StopRequestedAtUtc = null
            }, cognitiveJobsEnabled, cognitiveSummary, cognitiveNightly.SummaryPath));
            WriteField("Status", state.Status);
            WriteField("Nightly State", DisplayPath(nightly.StatePath));
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        var startedAtUtc = DateTimeOffset.UtcNow;
        var deadlineUtc = startedAtUtc.AddHours(maxRuntimeHours);
        var runId = $"nightly_beta3_{startedAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}";
        var memoryService = new ResearchMemoryIndexService(storagePaths);
        var configLoad = LoadCTraderConfig();
        var authContext = BuildCTraderAuthContext(configLoad, storagePaths);
        var targetToUtc = new DateTimeOffset(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var targetFromUtc = targetToUtc.AddYears(-1);
        var job = new LongRunResearchJob(
            JobId: runId,
            StartedAtUtc: startedAtUtc,
            DeadlineUtc: deadlineUtc,
            RequestedHours: maxRuntimeHours,
            RequestedBy: "hermes_nightly_beta3",
            NoAutoTrading: true,
            HumanReviewRequired: true);

        var iterations = 0;
        var idleIterations = 0;
        var workPerformed = 0;
        var status = "running";
        var nextAction = "start_iteration";
        string? lastCheckpoint = null;
        string? lastError = null;
        DateTimeOffset? stopRequestedAtUtc = null;
        var cognitiveStepCompleted = false;

        nightly.ClearStopRequest();
        nightly.WriteState(WithCognitiveState(
            nightly.CreateRunState(runId, startedAtUtc, deadlineUtc, status, nextAction),
            cognitiveJobsEnabled,
            cognitiveSummary,
            cognitiveNightly.SummaryPath));

        using var eventStore = new EventStore(storagePaths);
        var eventBus = new EventBus();
        eventBus.Subscribe(eventStore.Append);

        while (DateTimeOffset.UtcNow < deadlineUtc)
        {
            if (nightly.IsStopRequested())
            {
                stopRequestedAtUtc = nightly.StopRequestedAtUtc();
                status = "stopped_by_stop_request";
                nextAction = "safe_stop_requested";
                break;
            }

            if (!config.IsInAllowedWindow(DateTimeOffset.Now))
            {
                status = "stopped_outside_nightly_window";
                nextAction = "wait_for_next_window";
                break;
            }

            var resources = new ResourceGuard(storagePaths).Check();
            var storageHygiene = new StorageHygieneService(storagePaths);
            var cleanupPlan = storageHygiene.LoadPlan() ?? storageHygiene.BuildPlan();
            if (resources.ShouldStop)
            {
                status = "stopped_resource_guard";
                nextAction = "review_resource_status";
                break;
            }

            if (resources.ShouldPause)
            {
                status = "paused_resource_guard";
                nextAction = "sleep_then_recheck_resources";
                Thread.Sleep(TimeSpan.FromSeconds(sleepSeconds));
                continue;
            }

            try
            {
                var loopMinutes = Math.Max(0.01, Math.Min(10, (deadlineUtc - DateTimeOffset.UtcNow).TotalMinutes));
                var loopSummary = autonomousLoop.Run(maxIterations: 1, maxMinutes: loopMinutes);
                workPerformed += loopSummary.WorkPerformed;

                WriteSubHeader("Autonomous Learning Loop Step");
                WriteField("loop_status", loopSummary.Status);
                WriteField("loop_iterations", loopSummary.IterationsCompleted.ToString());
                WriteField("loop_work_performed", loopSummary.WorkPerformed.ToString());
                WriteField("loop_idle_iterations", loopSummary.IdleIterations.ToString());
                WriteField("loop_next_action", loopSummary.NextAction);
                WriteField("loop_summary", DisplayPath(autonomousLoop.SummaryPath));
                WriteField("loop_log", DisplayPath(autonomousLoop.LogPath));
                if (loopSummary.LastIteration is not null)
                {
                    WriteField("needs_detected", loopSummary.LastIteration.NeedsDetected.ToString());
                    WriteField("planned_tasks", loopSummary.LastIteration.TasksPlanned.ToString());
                    WriteField("executed_planned_tasks", loopSummary.LastIteration.TasksExecuted.ToString());
                    WriteField("evaluated_outcomes", loopSummary.LastIteration.OutcomesEvaluated.ToString());
                    WriteField("avg_learning", $"{loopSummary.LastIteration.AverageOutcomeLearningValue:0.####}");
                    WriteMessages("feedback_changes", loopSummary.LastIteration.FeedbackChanges.Take(8).ToList());
                }
                Console.WriteLine();

                if (!cognitiveStepCompleted)
                {
                    cognitiveSummary = cognitiveNightly.Run(maxQueueItems: 20);
                    cognitiveStepCompleted = true;
                    var cognitiveWorkUnits = cognitiveSummary.QueueItemsProcessed
                        + cognitiveSummary.HypothesesGenerated
                        + cognitiveSummary.InsightsGenerated;
                    workPerformed += cognitiveWorkUnits;

                    WriteSubHeader("Cognitive Nightly Step");
                    WriteField("sources_scanned", cognitiveSummary.SourcesScanned.ToString());
                    WriteField("knowledge_items", cognitiveSummary.KnowledgeItems.ToString());
                    WriteField("queue_items_processed", cognitiveSummary.QueueItemsProcessed.ToString());
                    WriteField("hypotheses_generated", cognitiveSummary.HypothesesGenerated.ToString());
                    WriteField("insights_generated", cognitiveSummary.InsightsGenerated.ToString());
                    WriteField("summary", DisplayPath(cognitiveNightly.SummaryPath));
                    WriteMessages("Warnings", cognitiveSummary.Warnings);
                    Console.WriteLine();
                }

                var iteration = RunResearchAutopilotIteration(
                    storagePaths,
                    memoryService,
                    configLoad,
                    authContext,
                    eventBus,
                    job,
                    iterations + 1,
                    targetFromUtc,
                    targetToUtc,
                    maxDownloads: Math.Min(9, config.AllowedSymbols.Count * config.AllowedTimeframes.Count),
                    maxRequests: 500,
                    inheritedWarnings: [],
                    runQualityGates: true,
                    maxQualityCandidates: maxQualityCandidates);
                eventStore.Flush();

                iterations++;
                if (iteration.WorkPerformed)
                {
                    idleIterations = 0;
                    workPerformed += iteration.WorkUnits;
                }
                else
                {
                    idleIterations++;
                }

                nextAction = iteration.NextAction;
                lastCheckpoint = memoryService.WriteCheckpoint(
                    job,
                    iterations,
                    iteration.Status,
                    $"nightly_beta3 work_performed={iteration.WorkUnits}; cognitive_jobs_enabled={cognitiveJobsEnabled.ToString().ToLowerInvariant()}; idle_iterations={idleIterations}; cleanup_candidates={cleanupPlan.Candidates.Count}",
                    iteration.Index,
                    betaRunId: iteration.BetaReport?.RunId);

                nightly.WriteState(WithCognitiveState(new NightlyResearchState(
                    StateVersion: "nightly_research_state_v1",
                    UpdatedAtUtc: DateTimeOffset.UtcNow,
                    Status: "running",
                    RunId: runId,
                    StartedAtUtc: startedAtUtc,
                    DeadlineUtc: deadlineUtc,
                    IterationsCompleted: iterations,
                    IdleIterations: idleIterations,
                    WorkPerformed: workPerformed,
                    NextAction: nextAction,
                    LastCheckpointPath: lastCheckpoint,
                    LastAutopilotReportPath: null,
                    LastError: null,
                    NoAutoTrading: true,
                    HumanReviewRequired: true,
                    NextScheduledStartUtc: NightlyResearchService.CalculateNextScheduledStart(config, DateTimeOffset.Now).ToUniversalTime(),
                    LastStartUtc: startedAtUtc,
                    LastStopUtc: null,
                    CurrentlyRunning: true,
                    RuntimeDurationMinutes: Math.Round((DateTimeOffset.UtcNow - startedAtUtc).TotalMinutes, 2),
                    ProcessId: Environment.ProcessId,
                    StopRequestedAtUtc: null),
                    cognitiveJobsEnabled,
                    cognitiveSummary,
                    cognitiveNightly.SummaryPath));

                WriteSubHeader($"Nightly Iteration {iterations}");
                WriteField("work_performed", iteration.WorkUnits.ToString());
                WriteField("idle_iterations", idleIterations.ToString());
                WriteField("resource_action", resources.Action);
                WriteField("cleanup_candidates", cleanupPlan.Candidates.Count.ToString());
                WriteField("checkpoint", DisplayPath(lastCheckpoint));
                Console.WriteLine();

                if (idleIterations >= maxIdleIterations)
                {
                    status = "stopped_max_idle_iterations";
                    nextAction = "wait_for_new_data_or_new_strategy_space";
                    break;
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or JsonException)
            {
                lastError = ex.Message;
                status = "stopped_critical_error";
                nextAction = "review_last_error";
                break;
            }

            var remaining = deadlineUtc - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                status = "completed_deadline_reached";
                nextAction = "nightly_window_complete";
                break;
            }

            if (SleepUntilNextIterationOrStop(nightly, TimeSpan.FromSeconds(Math.Min(sleepSeconds, Math.Max(0, remaining.TotalSeconds)))))
            {
                stopRequestedAtUtc = nightly.StopRequestedAtUtc();
                status = "stopped_by_stop_request";
                nextAction = "safe_stop_requested";
                break;
            }
        }

        if (status == "running")
        {
            status = "completed_deadline_reached";
            nextAction = "nightly_window_complete";
        }

        lastCheckpoint ??= memoryService.WriteCheckpoint(
            job,
            iteration: iterations + 1,
            status: status,
            message: $"nightly_beta3 final checkpoint; status={status}; work_performed={workPerformed}; idle_iterations={idleIterations}",
            memoryService.UpdateIndex(),
            betaRunId: null);

        var finalState = nightly.WriteState(WithCognitiveState(new NightlyResearchState(
            StateVersion: "nightly_research_state_v1",
            UpdatedAtUtc: DateTimeOffset.UtcNow,
            Status: status,
            RunId: runId,
            StartedAtUtc: startedAtUtc,
            DeadlineUtc: deadlineUtc,
            IterationsCompleted: iterations,
            IdleIterations: idleIterations,
            WorkPerformed: workPerformed,
            NextAction: nextAction,
            LastCheckpointPath: lastCheckpoint,
            LastAutopilotReportPath: null,
            LastError: lastError,
            NoAutoTrading: true,
            HumanReviewRequired: true,
            NextScheduledStartUtc: NightlyResearchService.CalculateNextScheduledStart(config, DateTimeOffset.Now).ToUniversalTime(),
            LastStartUtc: startedAtUtc,
            LastStopUtc: DateTimeOffset.UtcNow,
            CurrentlyRunning: false,
            RuntimeDurationMinutes: Math.Round((DateTimeOffset.UtcNow - startedAtUtc).TotalMinutes, 2),
            ProcessId: null,
            StopRequestedAtUtc: stopRequestedAtUtc),
            cognitiveJobsEnabled,
            cognitiveSummary,
            cognitiveNightly.SummaryPath));

        nightly.ClearStopRequest();

        WriteField("Nightly State", DisplayPath(nightly.StatePath));
        WriteNightlyState(finalState);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private static bool SleepUntilNextIterationOrStop(NightlyResearchService nightly, TimeSpan duration)
    {
        var deadline = DateTimeOffset.UtcNow.Add(duration);
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (nightly.IsStopRequested())
            {
                return true;
            }

            var remaining = deadline - DateTimeOffset.UtcNow;
            Thread.Sleep(TimeSpan.FromSeconds(Math.Min(5, Math.Max(0.1, remaining.TotalSeconds))));
        }

        return nightly.IsStopRequested();
    }

    private int ShowNightlyStatus()
    {
        WriteHeader("Hermes Nightly Beta 3 Status");
        var storagePaths = BuildStoragePaths();
        var service = new NightlyResearchService(storagePaths, Path.Combine(_runtimeRoot, "config", "nightly.research.json"));
        var config = service.LoadConfig();
        var state = service.LoadState();
        var displayState = state with
        {
            NextScheduledStartUtc = NightlyResearchService.CalculateNextScheduledStart(config, DateTimeOffset.Now).ToUniversalTime()
        };

        WriteField("State", DisplayPath(service.StatePath));
        WriteNightlyState(displayState);
        WriteCognitiveOperationalStatus(storagePaths);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RequestNightlyStop()
    {
        WriteHeader("Hermes Nightly Beta 3 Stop Request");
        var storagePaths = BuildStoragePaths();
        var service = new NightlyResearchService(storagePaths, Path.Combine(_runtimeRoot, "config", "nightly.research.json"));
        var state = service.RequestStop();
        WriteField("Stop Request", DisplayPath(service.StopRequestPath));
        WriteField("State", DisplayPath(service.StatePath));
        WriteNightlyState(state);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowSchedulerStatus()
    {
        WriteHeader("Hermes Internal Scheduler Status");
        var storagePaths = BuildStoragePaths();
        var scheduler = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var status = scheduler.GetStatus();

        WriteField("Config", DisplayPath(status.ConfigPath));
        WriteField("State", DisplayPath(status.StatePath));
        WriteField("Check Interval", $"{status.CheckIntervalSeconds} s");
        WriteField("Enabled Jobs", status.Jobs.Count(job => job.Enabled).ToString());
        WriteField("Next Action", status.Jobs.Count == 0 ? "no_jobs_configured" : $"next_job={status.Jobs.FirstOrDefault(job => job.NextRunUtc is not null)?.JobId ?? "-"}");
        WriteMessages("Warnings", status.Warnings);
        WriteCognitiveOperationalStatus(storagePaths, status);
        foreach (var job in status.Jobs.Take(8))
        {
            WriteSchedulerJob(job);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowSchedulerJobs()
    {
        WriteHeader("Hermes Scheduled Jobs");
        var storagePaths = BuildStoragePaths();
        var scheduler = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var status = scheduler.GetStatus();

        WriteField("Config", DisplayPath(status.ConfigPath));
        WriteField("State", DisplayPath(status.StatePath));
        foreach (var job in status.Jobs)
        {
            WriteSchedulerJob(job);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int StartSupervisor()
    {
        WriteHeader("Hermes Supervisor");
        var storagePaths = BuildStoragePaths();
        var scheduleConfigPath = Path.Combine(_runtimeRoot, "config", "schedules.json");
        var scheduler = new HermesInternalScheduler(storagePaths, scheduleConfigPath);
        var schedulerStatus = scheduler.GetStatus();
        var supervisor = new HermesSupervisor(storagePaths, scheduleConfigPath);
        var processManager = new SupervisorProcessManager(storagePaths);
        var maxRuntimeMinutes = ReadIntOption(_args, "--max-runtime-minutes", fallback: 1440, min: 1, max: 10080);
        var checkIntervalSeconds = ReadIntOption(
            _args,
            "--check-interval-seconds",
            fallback: schedulerStatus.CheckIntervalSeconds,
            min: 5,
            max: 3600);
        var maxJobsPerLoop = ReadIntOption(_args, "--max-jobs-per-loop", fallback: 2, min: 1, max: 8);

        if (_args.Any(arg => arg.Equals("--background", StringComparison.OrdinalIgnoreCase)))
        {
            return StartSupervisorBackground(supervisor, processManager, maxRuntimeMinutes, checkIntervalSeconds, maxJobsPerLoop);
        }

        var processStatus = processManager.GetStatus(supervisor.LoadState(), supervisor.LoadHeartbeat());
        if (processStatus.Running && processStatus.Pid != Environment.ProcessId)
        {
            WriteWarning("Hermes Supervisor laeuft bereits. Es wird kein zweiter Supervisor gestartet.");
            WriteSupervisorProcessStatus(processStatus);
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        WriteField("Config", DisplayPath(scheduleConfigPath));
        WriteField("State", DisplayPath(supervisor.StatePath));
        WriteField("Heartbeat", DisplayPath(supervisor.HeartbeatPath));
        WriteField("PID", DisplayPath(supervisor.PidPath));
        WriteField("Log", DisplayPath(supervisor.LogPath));
        WriteField("Max Runtime", $"{maxRuntimeMinutes} min");
        WriteField("Check Interval", $"{checkIntervalSeconds} s");
        Console.WriteLine();

        var result = supervisor.Run(
            new SupervisorRunOptions(maxRuntimeMinutes, checkIntervalSeconds, maxJobsPerLoop),
            ExecuteScheduledJob);

        WriteSupervisorState(result.State);
        Console.WriteLine();
        WriteField("Scheduler State", DisplayPath(result.SchedulerStatus.StatePath));
        foreach (var job in result.SchedulerStatus.Jobs.Take(8))
        {
            WriteSchedulerJob(job);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int StartSupervisorBackground(
        HermesSupervisor supervisor,
        SupervisorProcessManager processManager,
        int maxRuntimeMinutes,
        int checkIntervalSeconds,
        int maxJobsPerLoop)
    {
        processManager.ClearStalePid();
        var processStatus = processManager.GetStatus(supervisor.LoadState(), supervisor.LoadHeartbeat());
        if (processStatus.Running)
        {
            WriteField("Status", "already_running");
            WriteSupervisorProcessStatus(processStatus);
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        processManager.RotateLogIfNeeded();
        processManager.AppendLogLine("Hermes Supervisor background launcher invoked.");

        var command = string.Join(
            " ",
            [
                "cd",
                ShellQuote(_runtimeRoot),
                "&&",
                "nohup",
                "setsid",
                "-f",
                "dotnet",
                "run",
                "--project",
                "./cli/Hermes.Cli.csproj",
                "--",
                "supervisor-start",
                "--max-runtime-minutes",
                maxRuntimeMinutes.ToString(CultureInfo.InvariantCulture),
                "--check-interval-seconds",
                checkIntervalSeconds.ToString(CultureInfo.InvariantCulture),
                "--max-jobs-per-loop",
                maxJobsPerLoop.ToString(CultureInfo.InvariantCulture),
                ">>",
                ShellQuote(processManager.LogPath),
                "2>&1",
                "<",
                "/dev/null"
            ]);
        var startInfo = new ProcessStartInfo
        {
            FileName = "bash",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = _runtimeRoot
        };
        startInfo.ArgumentList.Add("-lc");
        startInfo.ArgumentList.Add(command);

        using var process = Process.Start(startInfo);
        process?.WaitForExit(5000);

        SupervisorProcessStatus latestStatus = processManager.GetStatus(supervisor.LoadState(), supervisor.LoadHeartbeat());
        for (var attempt = 0; attempt < 20 && !latestStatus.Running; attempt++)
        {
            Thread.Sleep(500);
            latestStatus = processManager.GetStatus(supervisor.LoadState(), supervisor.LoadHeartbeat());
        }

        WriteField("Status", latestStatus.Running ? "background_started" : "background_starting");
        WriteSupervisorProcessStatus(latestStatus);
        WriteField("State", DisplayPath(supervisor.StatePath));
        WriteField("Heartbeat", DisplayPath(supervisor.HeartbeatPath));
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowSupervisorStatus()
    {
        WriteHeader("Hermes Supervisor Status");
        var storagePaths = BuildStoragePaths();
        var supervisor = new HermesSupervisor(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var scheduler = new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var processManager = new SupervisorProcessManager(storagePaths);
        var state = supervisor.LoadState();
        var heartbeat = supervisor.LoadHeartbeat();
        var activeHeartbeat = heartbeat?.SupervisorId == state.SupervisorId ? heartbeat : null;
        var processStatus = processManager.GetStatus(state, activeHeartbeat);

        WriteField("State", DisplayPath(supervisor.StatePath));
        WriteField("Heartbeat", DisplayPath(supervisor.HeartbeatPath));
        WriteSupervisorProcessStatus(processStatus);
        WriteSupervisorState(state);
        if (activeHeartbeat is not null)
        {
            WriteSubHeader("Heartbeat");
            WriteField("Heartbeat UTC", activeHeartbeat.TimestampUtc.ToString("O"));
            WriteField("Status", activeHeartbeat.Status);
            WriteField("Current Job", activeHeartbeat.CurrentJobId ?? "-");
            WriteField("Resource Action", activeHeartbeat.ResourceAction);
            WriteField("Storage Action", activeHeartbeat.StorageAction);
            WriteField("Next Action", activeHeartbeat.NextAction);
        }

        var schedulerStatus = scheduler.GetStatus();
        var nextJob = schedulerStatus.Jobs.FirstOrDefault(job => job.NextRunUtc is not null);
        WriteSubHeader("Scheduler");
        WriteField("Config", DisplayPath(schedulerStatus.ConfigPath));
        WriteField("Jobs", schedulerStatus.Jobs.Count.ToString());
        WriteField("Next Scheduled Job", nextJob is null ? "-" : $"{nextJob.JobId} @ {nextJob.NextRunUtc?.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}");
        foreach (var job in schedulerStatus.Jobs.Take(5))
        {
            WriteSchedulerJob(job);
        }

        WriteCognitiveOperationalStatus(storagePaths, schedulerStatus);

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RequestSupervisorStop()
    {
        WriteHeader("Hermes Supervisor Stop Request");
        var storagePaths = BuildStoragePaths();
        var supervisor = new HermesSupervisor(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json"));
        var processManager = new SupervisorProcessManager(storagePaths);
        var state = supervisor.RequestStop();

        // If the supervisor is currently inside the existing Nightly Beta3 loop,
        // reuse its safe-stop flag instead of killing the process.
        var nightly = new NightlyResearchService(storagePaths, Path.Combine(_runtimeRoot, "config", "nightly.research.json"));
        nightly.RequestStop();

        WriteField("Stop Request", DisplayPath(supervisor.StopRequestPath));
        WriteField("Nightly Stop Request", DisplayPath(nightly.StopRequestPath));
        WriteField("State", DisplayPath(supervisor.StatePath));
        WriteSupervisorProcessStatus(processManager.GetStatus(state, supervisor.LoadHeartbeat()));
        WriteSupervisorState(state);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private ScheduledJobExecutionResult ExecuteScheduledJob(ScheduledJobDefinition job, SupervisorJobContext context)
    {
        var storagePaths = BuildStoragePaths();
        return job.JobType.ToLowerInvariant() switch
        {
            "nightly_beta3_research" => ExecuteNightlyBeta3ScheduledJob(job, context),
            "storage_hygiene" => ExecuteStorageHygieneJob(storagePaths),
            "research_insights" => ExecuteResearchInsightsJob(storagePaths),
            "health_snapshot" => ExecuteHealthSnapshotJob(storagePaths),
            "strategy_discovery" => ExecuteStrategyDiscoveryJob(storagePaths),
            "walkforward_validation" => ExecuteWalkForwardValidationJob(storagePaths),
            "scan_knowledge_sources" => ExecuteScanKnowledgeSourcesJob(storagePaths),
            "scan_software_domain" => ExecuteDomainScanJob(storagePaths, "software"),
            "scan_documentation_domain" => ExecuteDomainScanJob(storagePaths, "documentation"),
            "scan_process_domain" => ExecuteDomainScanJob(storagePaths, "process"),
            "scan_research_domain" => ExecuteDomainScanJob(storagePaths, "research"),
            "process_research_queue" => ExecuteProcessResearchQueueJob(storagePaths, job),
            "generate_cognitive_insights" => ExecuteGenerateCognitiveInsightsJob(storagePaths, job),
            "generate_domain_insights" => ExecuteGenerateDomainInsightsJob(storagePaths),
            "trading_nightly_beta3" => ExecuteNightlyBeta3ScheduledJob(job, context),
            "run_planning_cycle" => ExecutePlanningCycleJob(storagePaths, job),
            "process_planned_tasks" => ExecuteProcessPlannedTasksJob(storagePaths, job),
            "evaluate_task_outcomes" => ExecuteEvaluateTaskOutcomesJob(storagePaths, job),
            "run_autonomous_loop" => ExecuteAutonomousLoopJob(storagePaths, job),
            "market_data_refresh" => new ScheduledJobExecutionResult(
                Status: "skipped",
                WorkPerformed: false,
                Action: "market_data_refresh_disabled_until_explicit_config",
                ReportPath: null,
                Warnings: ["market_data_refresh is intentionally disabled by default; no broker/order action was attempted."]),
            _ => new ScheduledJobExecutionResult(
                Status: "skipped",
                WorkPerformed: false,
                Action: "unsupported_internal_job_type",
                ReportPath: null,
                Warnings: [$"Unsupported internal job type: {job.JobType}"])
        };
    }

    private ScheduledJobExecutionResult ExecuteNightlyBeta3ScheduledJob(ScheduledJobDefinition job, SupervisorJobContext context)
    {
        var remainingMinutes = Math.Max(1, (int)Math.Floor(context.RemainingRuntime.TotalMinutes));
        var maxRuntimeMinutes = Math.Clamp(job.MaxRuntimeMinutes ?? 360, 1, remainingMinutes);
        var maxQualityCandidates = job.Parameters is not null
            && job.Parameters.TryGetValue("max_quality_candidates", out var maxQualityCandidatesText)
            && int.TryParse(maxQualityCandidatesText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxQualityCandidates)
            ? Math.Clamp(parsedMaxQualityCandidates, 1, 500)
            : 64;
        var args = new List<string>
        {
            "run-nightly-beta3",
            "--max-runtime-hours",
            (maxRuntimeMinutes / 60.0).ToString("0.####", CultureInfo.InvariantCulture),
            "--sleep-seconds",
            (job.SleepSeconds ?? 60).ToString(CultureInfo.InvariantCulture),
            "--max-idle-iterations",
            (job.MaxIdleIterations ?? 10).ToString(CultureInfo.InvariantCulture),
            "--max-quality-candidates",
            maxQualityCandidates.ToString(CultureInfo.InvariantCulture)
        };

        var exitCode = new HermesCli(args.ToArray()).Run();
        return new ScheduledJobExecutionResult(
            Status: exitCode == 0 ? "completed" : "failed",
            WorkPerformed: true,
            Action: "run-nightly-beta3",
            ReportPath: Path.Combine(_dataRoot, "reports", "nightly_beta3", "nightly_state.json"),
            Warnings: exitCode == 0 ? [] : [$"run-nightly-beta3 exited with code {exitCode}"]);
    }

    private static ScheduledJobExecutionResult ExecuteStorageHygieneJob(StoragePaths storagePaths)
    {
        var hygiene = new StorageHygieneService(storagePaths);
        var plan = hygiene.BuildPlan();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: plan.Candidates.Count > 0,
            Action: $"cleanup_plan candidates={plan.Candidates.Count}",
            ReportPath: hygiene.CleanupPlanPath,
            Warnings: []);
    }

    private static ScheduledJobExecutionResult ExecuteResearchInsightsJob(StoragePaths storagePaths)
    {
        var generator = new ResearchInsightsGenerator(storagePaths);
        var insights = generator.Generate();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: true,
            Action: $"research_insights clusters={insights.Clusters.Count}",
            ReportPath: generator.InsightsPath,
            Warnings: []);
    }

    private static ScheduledJobExecutionResult ExecuteHealthSnapshotJob(StoragePaths storagePaths)
    {
        var guard = new ResourceGuard(storagePaths);
        var snapshot = guard.Check();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: true,
            Action: $"health_snapshot resource_action={snapshot.Action}",
            ReportPath: guard.StatusPath,
            Warnings: snapshot.Warnings);
    }

    private static ScheduledJobExecutionResult ExecuteStrategyDiscoveryJob(StoragePaths storagePaths)
    {
        var discovery = new StrategyDiscoveryService(storagePaths);
        var report = discovery.Run();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: true,
            Action: $"strategy_discovery analyzed={report.StrategiesAnalyzed}",
            ReportPath: discovery.DiscoveryStatusPath,
            Warnings: report.Warnings);
    }

    private static ScheduledJobExecutionResult ExecuteWalkForwardValidationJob(StoragePaths storagePaths)
    {
        var simulations = new RealisticSimulationService(storagePaths).Run();
        var walkForward = new WalkForwardValidationService(storagePaths);
        var report = walkForward.Run();
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: true,
            Action: $"walkforward strategies={report.StrategiesEvaluated}; simulations={simulations.Count}",
            ReportPath: walkForward.WalkForwardSummaryPath,
            Warnings: []);
    }

    private static ScheduledJobExecutionResult ExecuteScanKnowledgeSourcesJob(StoragePaths storagePaths)
    {
        var scout = new KnowledgeSourceScout(storagePaths);
        var sources = scout.Scan();
        var registry = new KnowledgeSourceRegistry(storagePaths);
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: true,
            Action: $"scan_knowledge_sources sources={sources.Count}",
            ReportPath: registry.SourcesPath,
            Warnings: []);
    }

    private static ScheduledJobExecutionResult ExecuteDomainScanJob(StoragePaths storagePaths, string domain)
    {
        var service = new DomainCognitiveService(storagePaths);
        var result = service.ScanDomain(domain);
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: result.KnowledgeItems > 0,
            Action: $"scan_{domain}_domain sources={result.SourcesScanned}; knowledge_items={result.KnowledgeItems}",
            ReportPath: service.DomainStatusPath,
            Warnings: result.Warnings);
    }

    private static ScheduledJobExecutionResult ExecuteGenerateDomainInsightsJob(StoragePaths storagePaths)
    {
        var service = new DomainCognitiveService(storagePaths);
        var status = service.BuildStatus();
        var insights = service.BuildInsights(status);
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: insights.Insights.Count > 0,
            Action: $"generate_domain_insights active_domains={status.ActiveDomains.Count}; insights={insights.Insights.Count}",
            ReportPath: service.DomainInsightsPath,
            Warnings: []);
    }

    private static ScheduledJobExecutionResult ExecuteProcessResearchQueueJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var maxItems = job.Parameters is not null
            && job.Parameters.TryGetValue("max_items", out var maxItemsText)
            && int.TryParse(maxItemsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxItems)
            ? Math.Clamp(parsedMaxItems, 1, 500)
            : 50;
        var service = new ResearchQueueService(storagePaths);
        var queue = service.Process(maxItems);
        var processed = queue.Items.Count(item => item.Status.Equals("processed", StringComparison.OrdinalIgnoreCase));
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: processed > 0,
            Action: $"process_research_queue processed_total={processed}",
            ReportPath: service.QueuePath,
            Warnings: []);
    }

    private static ScheduledJobExecutionResult ExecuteGenerateCognitiveInsightsJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var maxItems = job.Parameters is not null
            && job.Parameters.TryGetValue("max_items", out var maxItemsText)
            && int.TryParse(maxItemsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxItems)
            ? Math.Clamp(parsedMaxItems, 1, 500)
            : 20;
        var service = new CognitiveNightlyService(storagePaths);
        var summary = service.Run(maxItems);
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: summary.HypothesesGenerated > 0 || summary.QueueItemsProcessed > 0,
            Action: $"generate_cognitive_insights hypotheses={summary.HypothesesGenerated}; queue_processed={summary.QueueItemsProcessed}",
            ReportPath: service.SummaryPath,
            Warnings: summary.Warnings);
    }

    private static ScheduledJobExecutionResult ExecutePlanningCycleJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var maxItems = ReadMaxItems(job, fallback: 20);
        var service = new AutonomousPlanningCycleService(storagePaths);
        var decision = service.RunPlanningCycle(maxItems);
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: decision.PlannedTasks.Count > 0,
            Action: $"run_planning_cycle needs={decision.Needs.Count}; planned={decision.PlannedTasks.Count}",
            ReportPath: service.PlanningStatusPath,
            Warnings: []);
    }

    private static ScheduledJobExecutionResult ExecuteProcessPlannedTasksJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var maxItems = ReadMaxItems(job, fallback: 20);
        var executor = new PlannedTaskExecutor(storagePaths);
        var results = executor.Execute(maxItems);
        var completed = results.Count(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase));
        var skipped = results.Count(result => result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase));
        var failed = results.Count(result => result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase));
        return new ScheduledJobExecutionResult(
            Status: failed > 0 ? "failed" : "completed",
            WorkPerformed: completed > 0,
            Action: $"process_planned_tasks completed={completed}; skipped={skipped}; failed={failed}",
            ReportPath: executor.ExecutionStatePath,
            Warnings: results.SelectMany(result => result.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList());
    }

    private static ScheduledJobExecutionResult ExecuteEvaluateTaskOutcomesJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var maxItems = ReadMaxItems(job, fallback: 50);
        var evaluator = new TaskOutcomeEvaluator(storagePaths);
        var outcomes = evaluator.Evaluate(maxItems);
        return new ScheduledJobExecutionResult(
            Status: "completed",
            WorkPerformed: outcomes.Count > 0,
            Action: $"evaluate_task_outcomes evaluated={outcomes.Count}",
            ReportPath: evaluator.PlannerFeedbackPath,
            Warnings: outcomes
                .Where(outcome => outcome.Recommendation is "escalate_to_review" or "retire_task_type")
                .Select(outcome => $"{outcome.TaskType}:{outcome.Recommendation}")
                .Take(20)
                .ToList());
    }

    private ScheduledJobExecutionResult ExecuteAutonomousLoopJob(StoragePaths storagePaths, ScheduledJobDefinition job)
    {
        var maxIterations = job.Parameters is not null
            && job.Parameters.TryGetValue("max_iterations", out var maxIterationsText)
            && int.TryParse(maxIterationsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedIterations)
                ? Math.Clamp(parsedIterations, 1, 1000)
                : 1;
        var maxMinutes = job.Parameters is not null
            && job.Parameters.TryGetValue("max_minutes", out var maxMinutesText)
            && double.TryParse(maxMinutesText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMinutes)
                ? Math.Clamp(parsedMinutes, 0.01, 1440)
                : 10;
        var loop = new AutonomousLearningLoop(
            storagePaths,
            Path.Combine(_runtimeRoot, "config", "autonomous.loop.json"));
        var summary = loop.Run(maxIterations, maxMinutes);
        return new ScheduledJobExecutionResult(
            Status: summary.Status.StartsWith("stopped_", StringComparison.OrdinalIgnoreCase) ? "skipped" : "completed",
            WorkPerformed: summary.WorkPerformed > 0,
            Action: $"run_autonomous_loop iterations={summary.IterationsCompleted}; work={summary.WorkPerformed}; idle={summary.IdleIterations}",
            ReportPath: loop.SummaryPath,
            Warnings: summary.Warnings);
    }

    private static int ReadMaxItems(ScheduledJobDefinition job, int fallback) =>
        job.Parameters is not null
            && job.Parameters.TryGetValue("max_items", out var maxItemsText)
            && int.TryParse(maxItemsText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxItems)
            ? Math.Clamp(parsedMaxItems, 1, 500)
            : fallback;

    private int ShowResourceStatus()
    {
        WriteHeader("Hermes Resource Guard");
        var guard = new ResourceGuard(BuildStoragePaths());
        var snapshot = guard.Check();
        WriteField("Report", DisplayPath(guard.StatusPath));
        WriteResourceSnapshot(snapshot);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStorageStatus()
    {
        WriteHeader("Hermes Storage Status");
        var storagePaths = BuildStoragePaths();
        var guard = new ResourceGuard(storagePaths);
        var resource = guard.Check();
        var hygiene = new StorageHygieneService(storagePaths);
        var plan = hygiene.BuildPlan();
        WriteField("Storage Root", DisplayPath(storagePaths.Root));
        WriteField("Free Disk", $"{resource.FreeDiskMb / 1024.0:0.##} GB ({resource.FreeDiskPercent:0.##}%)");
        WriteField("Resource Action", resource.Action);
        WriteField("Cleanup Plan", DisplayPath(hygiene.CleanupPlanPath));
        WriteCleanupPlan(plan, limit: 8);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCleanupPlan()
    {
        WriteHeader("Hermes Cleanup Plan");
        var hygiene = new StorageHygieneService(BuildStoragePaths());
        var plan = hygiene.BuildPlan();
        WriteField("Cleanup Plan", DisplayPath(hygiene.CleanupPlanPath));
        WriteCleanupPlan(plan, limit: 20);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ApplyCleanup()
    {
        WriteHeader("Hermes Safe Cleanup Apply");
        if (!_args.Any(arg => arg.Equals("--safe", StringComparison.OrdinalIgnoreCase)))
        {
            WriteError("cleanup-apply braucht explizit --safe. Es wurden keine Dateien geloescht.");
            WriteSafety();
            return 2;
        }

        var hygiene = new StorageHygieneService(BuildStoragePaths());
        var report = hygiene.ApplySafeCleanup();
        WriteField("Cleanup Report", DisplayPath(hygiene.CleanupReportPath));
        WriteField("Files Deleted", report.FilesDeleted.ToString());
        WriteField("Bytes Freed", report.BytesFreed.ToString());
        WriteField("Safe Mode", report.SafeMode.ToString().ToLowerInvariant());
        WriteMessages("Skipped", report.SkippedPaths.Take(12).ToList());
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
        var deadlineUtc = DateTimeOffset.UtcNow.AddHours(hours);
        var sleepSeconds = ReadIntOption(_args, "--sleep-seconds", fallback: 60, min: 0, max: 3600);
        var maxIdleIterations = ReadIntOption(_args, "--max-idle-iterations", fallback: 10, min: 1, max: 1000);
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
        var job = new LongRunResearchJob(
            JobId: $"research_autopilot_{startedAtUtc:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            StartedAtUtc: startedAtUtc,
            DeadlineUtc: deadlineUtc,
            RequestedHours: hours,
            RequestedBy: "hermes_research_autopilot",
            NoAutoTrading: true,
            HumanReviewRequired: true);
        var warnings = new List<string>();
        var downloadResults = new List<AutopilotDownloadResult>();
        StrategyResearchStepResult? latestStrategyResearch = null;
        ResearchMemoryIndex? latestIndex = null;
        WalkForwardValidationReport? latestWalkForward = null;
        StrategyDiscoveryReport? latestDiscovery = null;
        var totalDownloadPlans = 0;
        var totalDownloadsAttempted = 0;
        var totalStrategyVariantsTested = 0;
        var latestSimulationReports = 0;
        var iterationsCompleted = 0;
        var idleIterations = 0;
        var totalWorkPerformed = 0;
        var status = "completed";
        var nextAction = "deadline_reached";

        using (var eventStore = new EventStore(storagePaths))
        {
            var eventBus = new EventBus();
            eventBus.Subscribe(eventStore.Append);

            while (DateTimeOffset.UtcNow < deadlineUtc)
            {
                var iteration = iterationsCompleted + 1;
                var iterationResult = RunResearchAutopilotIteration(
                    storagePaths,
                    memoryService,
                    configLoad,
                    authContext,
                    eventBus,
                    job,
                    iteration,
                    targetFromUtc,
                    targetToUtc,
                    maxDownloads,
                    maxRequests,
                    warnings);

                eventStore.Flush();

                iterationsCompleted++;
                totalDownloadPlans += iterationResult.DownloadPlans;
                totalDownloadsAttempted += iterationResult.DownloadsAttempted;
                totalStrategyVariantsTested += iterationResult.StrategyResearch.TestedNow;
                latestStrategyResearch = iterationResult.StrategyResearch;
                latestIndex = iterationResult.Index;
                latestWalkForward = iterationResult.WalkForward;
                latestDiscovery = iterationResult.Discovery;
                latestSimulationReports = iterationResult.SimulationReports;
                downloadResults.AddRange(iterationResult.Downloads);
                warnings.AddRange(iterationResult.Warnings);

                if (iterationResult.WorkPerformed)
                {
                    idleIterations = 0;
                    totalWorkPerformed += iterationResult.WorkUnits;
                }
                else
                {
                    idleIterations++;
                }

                nextAction = iterationResult.NextAction;
                var checkpoint = memoryService.WriteCheckpoint(
                    job,
                    iteration,
                    iterationResult.Status,
                    $"work_performed={iterationResult.WorkUnits}; idle_iterations={idleIterations}; next_action={nextAction}",
                    latestIndex,
                    betaRunId: iterationResult.BetaReport?.RunId);

                WriteSubHeader($"Iteration {iteration}");
                WriteField("elapsed_minutes", $"{(DateTimeOffset.UtcNow - startedAtUtc).TotalMinutes:0.##}");
                WriteField("work_performed", iterationResult.WorkUnits.ToString());
                WriteField("idle_iterations", idleIterations.ToString());
                WriteField("next_action", nextAction);
                WriteField("Checkpoint", DisplayPath(checkpoint));
                Console.WriteLine();

                if (iterationResult.Status == "stopped_storage_critical")
                {
                    status = "stopped_storage_critical";
                    nextAction = "fix_storage_before_retry";
                    break;
                }

                if (idleIterations >= maxIdleIterations)
                {
                    status = "stopped_max_idle_iterations";
                    nextAction = "wait_for_new_data_or_expand_strategy_space";
                    break;
                }

                var remaining = deadlineUtc - DateTimeOffset.UtcNow;
                if (remaining <= TimeSpan.Zero)
                {
                    status = "completed_deadline_reached";
                    nextAction = "deadline_reached";
                    break;
                }

                if (sleepSeconds > 0)
                {
                    var sleepFor = TimeSpan.FromSeconds(Math.Min(sleepSeconds, Math.Max(0, remaining.TotalSeconds)));
                    Thread.Sleep(sleepFor);
                }
            }
        }

        if (DateTimeOffset.UtcNow >= deadlineUtc && status == "completed")
        {
            status = "completed_deadline_reached";
            nextAction = "deadline_reached";
        }

        if (iterationsCompleted == 0)
        {
            latestIndex = memoryService.UpdateIndex();
            latestStrategyResearch = RunStrategyResearchAndInsights(storagePaths);
            latestWalkForward = new WalkForwardValidationService(storagePaths).Run();
            latestDiscovery = new StrategyDiscoveryService(storagePaths).Run();
            latestSimulationReports = new RealisticSimulationService(storagePaths).Run().Count;
            iterationsCompleted = 1;
            status = "completed_minimum_iteration";
            nextAction = "deadline_reached";
        }

        latestStrategyResearch ??= RunStrategyResearchAndInsights(storagePaths);
        latestIndex ??= memoryService.UpdateIndex();
        latestWalkForward ??= new WalkForwardValidationService(storagePaths).Run();
        latestDiscovery ??= new StrategyDiscoveryService(storagePaths).Run();

        var elapsedMinutes = (DateTimeOffset.UtcNow - startedAtUtc).TotalMinutes;
        var report = WriteResearchAutopilotReport(
            storagePaths,
            job.JobId,
            startedAtUtc,
            hours,
            targetFromUtc,
            targetToUtc,
            totalDownloadPlans,
            totalDownloadsAttempted,
            downloadResults,
            totalStrategyVariantsTested,
            latestStrategyResearch.Memory.ResearchEntries?.Count ?? 0,
            catalog.CatalogPath,
            latestStrategyResearch.InsightsPath,
            status,
            elapsedMinutes,
            iterationsCompleted,
            totalWorkPerformed,
            idleIterations,
            nextAction,
            warnings);

        var finalCheckpoint = memoryService.WriteCheckpoint(
            job,
            iteration: iterationsCompleted + 1,
            status: report.Status,
            message: $"Research Autopilot stopped with {report.Status}. Iterations: {iterationsCompleted}. Work performed: {totalWorkPerformed}.",
            latestIndex,
            betaRunId: null);

        WriteField("Autopilot Report", DisplayPath(Path.Combine(storagePaths.Root, "strategy_research", "autopilot", $"{report.ReportId}.autopilot_report.json")));
        WriteField("Checkpoint", DisplayPath(finalCheckpoint));
        WriteField("requested_hours", $"{hours:0.##}");
        WriteField("elapsed_minutes", $"{elapsedMinutes:0.##}");
        WriteField("iterations_completed", iterationsCompleted.ToString());
        WriteField("work_performed", totalWorkPerformed.ToString());
        WriteField("idle_iterations", idleIterations.ToString());
        WriteField("next_action", nextAction);
        WriteField("Target Range", $"{targetFromUtc:yyyy-MM-dd} -> {targetToUtc:yyyy-MM-dd}");
        WriteField("Pattern Catalog", DisplayPath(catalog.CatalogPath));
        WriteField("Patterns", patterns.Count.ToString());
        WriteField("Download Plans", report.DownloadPlans.ToString());
        WriteField("Downloads Attempted", report.DownloadsAttempted.ToString());
        WriteField("Candles Downloaded", report.CandlesDownloaded.ToString());
        WriteField("Download Requests", report.DownloadRequests.ToString());
        WriteField("Strategy Variants Tested", report.StrategyVariantsTested.ToString());
        WriteField("Strategy Memory Entries", report.StrategyResearchEntries.ToString());
        WriteField("Simulation Reports", latestSimulationReports.ToString());
        WriteField("Walk-Forward Robust", latestWalkForward.RobustStrategies.ToString());
        WriteField("Overfit Suspects", latestWalkForward.OverfitSuspectedStrategies.ToString());
        WriteField("Discovery Strategies Analyzed", latestDiscovery.StrategiesAnalyzed.ToString());
        WriteField("Discovery Risk Flags", latestDiscovery.RiskFlagsDetected.ToString());
        WriteField("Insights", DisplayPath(latestStrategyResearch.InsightsPath));
        WriteField("Status", report.Status);
        WriteResearchMemoryIndex(latestIndex);
        WriteStrategyResearchMemory(latestStrategyResearch.Memory, limit: 3);
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private AutopilotIterationResult RunResearchAutopilotIteration(
        StoragePaths storagePaths,
        ResearchMemoryIndexService memoryService,
        CTraderOpenApiConfigLoadResult configLoad,
        (CTraderOAuthUrlResult OAuthUrl, CTraderAuthStatus AuthStatus, CTraderAuthTokenState TokenState) authContext,
        EventBus eventBus,
        LongRunResearchJob job,
        int iteration,
        DateTimeOffset targetFromUtc,
        DateTimeOffset targetToUtc,
        int maxDownloads,
        int maxRequests,
        IReadOnlyList<string> inheritedWarnings,
        bool runQualityGates = false,
        int maxQualityCandidates = 64)
    {
        var warnings = new List<string>();
        var disk = new DiskSpaceGuard().Check(storagePaths, minimumFreeDiskMb: 512);
        if (!disk.IsOk)
        {
            warnings.Add($"Storage critical: {disk.Warning}");
            var index = memoryService.UpdateIndex();
            var strategy = RunStrategyResearchAndInsights(storagePaths);
            var storageStopWalkForward = new WalkForwardValidationService(storagePaths).Run();
            var storageStopDiscovery = new StrategyDiscoveryService(storagePaths).Run();
            return new AutopilotIterationResult(
                Iteration: iteration,
                DownloadPlans: 0,
                DownloadsAttempted: 0,
                Downloads: [],
                BetaReport: null,
                Index: index,
                StrategyResearch: strategy,
                SimulationReports: 0,
                WalkForward: storageStopWalkForward,
                Discovery: storageStopDiscovery,
                Warnings: warnings,
                WorkPerformed: false,
                WorkUnits: 0,
                Status: "stopped_storage_critical",
                NextAction: "fix_storage_before_retry");
        }

        var beforeRanges = memoryService.GetCurrentMarketDataRanges();
        var beforeFingerprint = memoryService.BuildMarketDataFingerprint(beforeRanges);
        var plans = BuildAutopilotDownloadPlans(beforeRanges, targetFromUtc, targetToUtc);
        var selectedPlans = plans.Take(maxDownloads).ToList();
        var downloads = new List<AutopilotDownloadResult>();

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
                downloads.Add(download);
                warnings.AddRange(download.Warnings);
            }
            catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException or TaskCanceledException or IOException)
            {
                warnings.Add($"{plan.Request.Symbol} {plan.Request.Timeframe} {plan.Reason}: {ex.Message}");
            }
        }

        var afterRanges = memoryService.GetCurrentMarketDataRanges();
        var afterFingerprint = memoryService.BuildMarketDataFingerprint(afterRanges);
        var candlesAvailable = afterRanges.Sum(range => range.CandleCount) > 0;
        var newCandles = downloads.Sum(download => download.ImportResult.CandleCount);
        var marketDataChanged = !string.Equals(beforeFingerprint, afterFingerprint, StringComparison.Ordinal);
        var shouldRunBeta = candlesAvailable
            && (iteration == 1 || marketDataChanged || !HasFeatureExports(storagePaths));
        TradingLearningBetaReport? betaReport = null;
        if (shouldRunBeta)
        {
            using var betaEventStore = new EventStore(storagePaths);
            var betaBus = new EventBus();
            betaBus.Subscribe(betaEventStore.Append);
            var pipeline = new TradingLearningBetaPipeline(storagePaths, betaBus, CliVersion);
            betaReport = pipeline.Run();
            betaEventStore.Flush();
        }
        else if (!candlesAvailable)
        {
            warnings.Add("No candle data available after Autopilot data expansion; beta pipeline skipped.");
        }

        var updatedIndex = memoryService.UpdateIndex();
        var strategyResearch = RunStrategyResearchAndInsights(storagePaths);
        var simulationReports = new RealisticSimulationService(storagePaths).Run();
        var walkForward = new WalkForwardValidationService(storagePaths).Run();
        var discovery = new StrategyDiscoveryService(storagePaths).Run();
        if (runQualityGates)
        {
            new MonteCarloSimulationService(storagePaths).Run(maxCandidates: maxQualityCandidates);
            new CostStressTestService(storagePaths).Run(maxQualityCandidates);
            new RiskOfRuinService(storagePaths).Run(maxQualityCandidates);
            new BotCandidatePipelineService(storagePaths).Evaluate();
            new BotCandidateRejectionAnalyzer(storagePaths).Run();
            new ResearchQualityImprovementExperimentService(storagePaths)
                .Run(maxBatchSize: Math.Min(maxQualityCandidates, 64));
        }

        new ResearchInsightsGenerator(storagePaths).Generate();

        var workUnits = newCandles
            + strategyResearch.TestedNow
            + (betaReport is { CandlesProcessed: > 0 } ? 1 : 0);
        var nextAction = workUnits > 0
            ? "continue_until_deadline"
            : "sleep_then_mutate_or_stop_after_idle_limit";

        return new AutopilotIterationResult(
            Iteration: iteration,
            DownloadPlans: plans.Count,
            DownloadsAttempted: selectedPlans.Count,
            Downloads: downloads,
            BetaReport: betaReport,
            Index: updatedIndex,
            StrategyResearch: strategyResearch,
            SimulationReports: simulationReports.Count,
            WalkForward: walkForward,
            Discovery: discovery,
            Warnings: warnings
                .Concat(inheritedWarnings)
                .Distinct(StringComparer.Ordinal)
                .Take(80)
                .ToList(),
            WorkPerformed: workUnits > 0,
            WorkUnits: workUnits,
            Status: workUnits > 0 ? "iteration_completed" : "idle_iteration",
            NextAction: nextAction);
    }

    private int RunWalkForwardValidation()
    {
        WriteHeader("Hermes Walk-Forward Validation");
        var storagePaths = BuildStoragePaths();
        var simulations = new RealisticSimulationService(storagePaths).Run();
        var report = new WalkForwardValidationService(storagePaths).Run();
        new ResearchInsightsGenerator(storagePaths).Generate();

        WriteField("Simulation Reports", simulations.Count.ToString());
        WriteField("Walk-Forward Report", DisplayPath(Path.Combine(storagePaths.Root, "simulation", "walkforward_validation.json")));
        WriteField("Overfit Report", DisplayPath(Path.Combine(storagePaths.Root, "simulation", "overfit_report.json")));
        WriteWalkForwardSummary(report);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowSimulationStatus()
    {
        WriteHeader("Hermes Realistic Simulation Status");
        var service = new RealisticSimulationService(BuildStoragePaths());
        var reports = service.LoadReports();
        if (reports.Count == 0)
        {
            WriteWarning("Keine Simulation-Reports gefunden. Nutze run-walkforward-validation.");
            WriteSafety();
            return 0;
        }

        WriteField("Simulation Root", DisplayPath(service.SimulationRoot));
        WriteField("Reports", reports.Count.ToString());
        WriteField("Latest UTC", reports.Max(report => report.CreatedAtUtc).ToString("O"));
        WriteField("Average Stability", $"{reports.Average(report => report.Metrics.StabilityScore):0.####}");
        WriteField("Average Profit Factor", $"{reports.Average(report => report.Metrics.ProfitFactor):0.####}");
        WriteField("Average Realism Penalty", $"{reports.Average(report => report.Metrics.RealismPenalty):0.####}");
        WriteField("Average Realism Score", $"{reports.Average(report => report.Metrics.RealismScore):0.####}");
        WriteField("Average Overfit Risk", $"{reports.Average(report => report.Metrics.OverfitRisk):0.####}");
        WriteField("Average Robustness", $"{reports.Average(report => report.Metrics.RobustnessConfidence):0.####}");
        WriteField("Average Cost Sensitivity", $"{reports.Average(report => report.Metrics.CostSensitivity):0.####}");
        WriteField("Too Good To Be True", reports.Count(report => report.Metrics.TooGoodToBeTrue).ToString());
        WriteField("no_auto_trading", "true");
        WriteField("human_review_required", "true");
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowRealismReport()
    {
        WriteHeader("Hermes Realism Report");
        var service = new RealisticSimulationService(BuildStoragePaths());
        service.Run();
        var report = service.LoadRealismReport();
        if (report is null)
        {
            report = service.LoadRealismReport();
        }

        if (report is null)
        {
            WriteWarning("Kein Realism Report erzeugbar.");
            WriteSafety();
            return 0;
        }

        WriteField("Realism Report", DisplayPath(service.RealismReportPath));
        WriteField("Report ID", report.ReportId);
        WriteField("Created UTC", report.CreatedAtUtc.ToString("O"));
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("Realistic Strategies", report.RealisticStrategies.ToString());
        WriteField("Suspicious Strategies", report.SuspiciousStrategies.ToString());
        WriteField("Average Realism Penalty", $"{report.AverageRealismPenalty:0.####}");
        WriteField("Average Realism Score", $"{report.AverageRealismScore:0.####}");
        WriteField("Average Overfit Risk", $"{report.AverageOverfitRisk:0.####}");
        WriteField("Average Cost Sensitivity", $"{report.AverageCostSensitivity:0.####}");
        WriteField("Average Loss Distribution", $"{report.AverageLossDistributionQuality:0.####}");
        WriteField("too_good_to_be_true", report.TooGoodToBeTrueStrategies.ToString());
        WriteMessages("Most Realistic", report.MostRealisticStrategies.Take(10).ToList());
        WriteMessages("Suspicious", report.SuspiciousStrategiesList.Take(10).ToList());
        WriteMessages("Too Good To Be True", report.TooGoodToBeTrueStrategiesList?.Take(10).ToList() ?? []);
        WriteMessages("Cost Sensitive", report.CostSensitiveStrategies?.Take(10).ToList() ?? []);
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCostSensitivityReport()
    {
        WriteHeader("Hermes Cost Sensitivity Report");
        var service = new RealisticSimulationService(BuildStoragePaths());
        service.Run();
        var report = service.LoadCostSensitivityReport();
        if (report is null)
        {
            report = service.LoadCostSensitivityReport();
        }

        if (report is null)
        {
            WriteWarning("Kein Cost Sensitivity Report erzeugbar.");
            WriteSafety();
            return 0;
        }

        WriteField("Cost Report", DisplayPath(service.CostSensitivityReportPath));
        WriteField("Report ID", report.ReportId);
        WriteField("Created UTC", report.CreatedAtUtc.ToString("O"));
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("Cost Sensitive", report.CostSensitiveStrategies.ToString());
        WriteField("Stress Cost Failures", report.StressCostFailures.ToString());
        WriteField("Average Cost Sensitivity", $"{report.AverageCostSensitivity:0.####}");
        foreach (var entry in report.Entries.Take(12))
        {
            WriteSubHeader($"{entry.StrategyFamily} / {entry.PatternId ?? "-"} / {entry.StrategyVariantId}");
            WriteField("Status", entry.Status);
            WriteField("Trades", entry.TradeCount.ToString());
            WriteField("Normal Cost Score", $"{entry.NormalCostScore:0.####}");
            WriteField("High Cost Score", $"{entry.HighCostScore:0.####}");
            WriteField("Stress Cost Score", $"{entry.StressCostScore:0.####}");
            WriteField("Cost Sensitivity", $"{entry.CostSensitivity:0.####}");
            WriteField("Works Only Without Costs", entry.WorksOnlyWithoutCosts.ToString().ToLowerInvariant());
            WriteField("too_good_to_be_true", entry.TooGoodToBeTrue.ToString().ToLowerInvariant());
        }

        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowMonteCarloReport()
    {
        WriteHeader("Hermes Monte-Carlo Report");
        var simulationRuns = ReadIntOption(_args, "--simulations", fallback: 100, min: 20, max: 2000);
        var maxCandidates = ReadIntOption(_args, "--max-candidates", fallback: 100, min: 1, max: 500);
        var service = new MonteCarloSimulationService(BuildStoragePaths());
        var report = service.Run(simulationRuns, maxCandidates);

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Report ID", report.ReportId);
        WriteField("Created UTC", report.CreatedAtUtc.ToString("O"));
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("Simulations/Strategy", report.SimulationsPerStrategy.ToString());
        WriteField("Passed", report.Passed.ToString());
        WriteField("Failed", report.Failed.ToString());
        WriteField("Avg Positive Ratio", $"{report.AveragePositiveSimulationRatio:0.####}");
        WriteField("Avg Ruin Probability", $"{report.AverageRuinProbabilityEstimate:0.####}");
        foreach (var result in report.Results.Take(10))
        {
            WriteMonteCarloResult(result);
        }

        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCostStressReport()
    {
        WriteHeader("Hermes Cost Stress Report");
        var maxCandidates = ReadIntOption(_args, "--max-candidates", fallback: 100, min: 1, max: 500);
        var service = new CostStressTestService(BuildStoragePaths());
        var report = service.Run(maxCandidates);

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Report ID", report.ReportId);
        WriteField("Created UTC", report.CreatedAtUtc.ToString("O"));
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("Survives Normal", report.SurvivesNormalCost.ToString());
        WriteField("Survives Spread x2", report.SurvivesSpreadX2.ToString());
        WriteField("Survives Spread x3", report.SurvivesSpreadX3.ToString());
        WriteField("Survives Stress", report.SurvivesStressCost.ToString());
        WriteField("Stress Failures", report.StressCostFailures.ToString());
        foreach (var result in report.Results.Take(10))
        {
            WriteCostStressResult(result);
        }

        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowRiskOfRuinReport()
    {
        WriteHeader("Hermes Risk-of-Ruin Report");
        var maxCandidates = ReadIntOption(_args, "--max-candidates", fallback: 100, min: 1, max: 500);
        var service = new RiskOfRuinService(BuildStoragePaths());
        var report = service.Run(maxCandidates);

        WriteField("Report", DisplayPath(service.ReportPath));
        WriteField("Report ID", report.ReportId);
        WriteField("Created UTC", report.CreatedAtUtc.ToString("O"));
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("Passed", report.Passed.ToString());
        WriteField("Failed", report.Failed.ToString());
        WriteField("Avg Ruin Probability", $"{report.AverageRuinProbabilityEstimate:0.####}");
        WriteField("Avg Recommended Risk", $"{report.AverageRecommendedMaxRiskPerTrade:0.####}%");
        foreach (var entry in report.Entries.Take(10))
        {
            WriteRiskOfRuinEntry(entry);
        }

        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowWalkForwardSummary()
    {
        WriteHeader("Hermes Walk-Forward Summary");
        var storagePaths = BuildStoragePaths();
        var service = new WalkForwardValidationService(storagePaths);
        var report = service.LoadReport() ?? service.Run();

        WriteField("Walk-Forward Report", DisplayPath(service.WalkForwardPath));
        WriteField("Walk-Forward Summary", DisplayPath(service.WalkForwardSummaryPath));
        WriteWalkForwardSummary(report);
        foreach (var item in report.Assessments.Take(8))
        {
            WriteSubHeader($"{item.StrategyFamily} / {item.PatternId ?? "-"} / {item.StrategyVariantId}");
            WriteField("Confidence", item.StrategyConfidence);
            WriteField("Train", $"{item.TrainScore:0.####}");
            WriteField("Validation", $"{item.ValidationScore:0.####}");
            WriteField("Out-of-Sample", $"{item.OutOfSampleScore:0.####}");
            WriteField("OOS Available", item.OosAvailable.ToString().ToLowerInvariant());
            WriteField("WalkForward Confidence", $"{item.WalkForwardConfidence:0.####}");
            WriteField("Degradation", $"{item.DegradationScore:0.####}");
            WriteField("Robustness Gap", $"{item.RobustnessGap:0.####}");
            WriteField("Realism Score", $"{item.RealismScore:0.####}");
            WriteField("Realism Penalty", $"{item.RealismPenalty:0.####}");
            WriteField("Cost Sensitivity", $"{item.CostSensitivity:0.####}");
            WriteField("Regime Consistency", $"{item.RegimeConsistencyScore:0.####}");
            WriteField("Overfit Risk", $"{item.OverfitRisk:0.####}");
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyDiscoveryStatus()
    {
        WriteHeader("Hermes Trusted Strategy Discovery");
        var service = new StrategyDiscoveryService(BuildStoragePaths());
        var report = service.Run();

        WriteField("Trusted Sources", DisplayPath(service.TrustedSourcesPath));
        WriteField("Discovery Status", DisplayPath(service.DiscoveryStatusPath));
        WriteField("Sources Whitelisted", report.SourcesWhitelisted.ToString());
        WriteField("Local .cs Files Analyzed", report.LocalCsFilesAnalyzed.ToString());
        WriteField("Strategies Analyzed", report.StrategiesAnalyzed.ToString());
        WriteField("Risk Flags", report.RiskFlagsDetected.ToString());
        WriteField("Foreign Code Executed", (!report.NoForeignCodeExecuted).ToString().ToLowerInvariant());
        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowOverfitReport()
    {
        WriteHeader("Hermes Overfit Report");
        var storagePaths = BuildStoragePaths();
        var service = new WalkForwardValidationService(storagePaths);
        var report = service.LoadReport() ?? service.Run();

        WriteField("Overfit Report", DisplayPath(service.OverfitReportPath));
        WriteWalkForwardSummary(report);
        foreach (var item in report.Assessments.Where(item => item.StrategyConfidence == "overfit_suspected").Take(12))
        {
            WriteSubHeader($"{item.StrategyFamily} / {item.PatternId ?? "-"} / {item.StrategyVariantId}");
            WriteField("Confidence", item.StrategyConfidence);
            WriteField("Validation", $"{item.ValidationScore:0.####}");
            WriteField("Out-of-Sample", $"{item.OutOfSampleScore:0.####}");
            WriteField("OOS Available", item.OosAvailable.ToString().ToLowerInvariant());
            WriteField("Realism Score", $"{item.RealismScore:0.####}");
            WriteField("Cost Sensitivity", $"{item.CostSensitivity:0.####}");
            WriteField("Regime Consistency", $"{item.RegimeConsistencyScore:0.####}");
            WriteMessages("Flags", item.OverfitFlags);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowRobustStrategies()
    {
        WriteHeader("Hermes Robust Strategies");
        var storagePaths = BuildStoragePaths();
        var service = new WalkForwardValidationService(storagePaths);
        var report = service.LoadReport() ?? service.Run();

        WriteField("Walk-Forward Report", DisplayPath(service.WalkForwardPath));
        WriteWalkForwardSummary(report);
        foreach (var item in report.Assessments.Where(item => item.Robust).Take(12))
        {
            WriteSubHeader($"{item.StrategyFamily} / {item.PatternId ?? "-"} / {item.StrategyVariantId}");
            WriteField("Confidence", item.StrategyConfidence);
            WriteField("Train", $"{item.TrainScore:0.####}");
            WriteField("Validation", $"{item.ValidationScore:0.####}");
            WriteField("Out-of-Sample", $"{item.OutOfSampleScore:0.####}");
            WriteField("WalkForward Confidence", $"{item.WalkForwardConfidence:0.####}");
            WriteField("Realism Score", $"{item.RealismScore:0.####}");
            WriteField("Cost Sensitivity", $"{item.CostSensitivity:0.####}");
            WriteField("Regime Consistency", $"{item.RegimeConsistencyScore:0.####}");
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowBotCandidates()
    {
        WriteHeader("Hermes Bot Candidate Pipeline");
        var service = new BotCandidatePipelineService(BuildStoragePaths());
        var report = service.Evaluate();

        WriteField("Candidates", DisplayPath(service.BotCandidatesPath));
        WriteField("Rejected", DisplayPath(service.RejectedCandidatesPath));
        WriteField("Report", DisplayPath(service.LatestReportPath));
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("bot_candidate_count", report.BotCandidateCount.ToString());
        WriteField("demo_bot_candidate", report.DemoBotCandidateCount.ToString());
        WriteField("Promising", report.PromisingCandidateCount.ToString());
        WriteField("Robust", report.RobustCandidateCount.ToString());
        WriteField("Rejected", report.RejectedCandidateCount.ToString());
        WriteField("Blocked Monte-Carlo", report.CandidatesBlockedByMonteCarlo.ToString());
        WriteField("Blocked Cost Stress", report.CandidatesBlockedByCostStress.ToString());
        WriteField("Blocked Risk", report.CandidatesBlockedByRisk.ToString());
        WriteMessages("Monte-Carlo Summary", report.MonteCarloSummary ?? Array.Empty<string>());
        WriteMessages("Cost Stress Summary", report.CostStressSummary ?? Array.Empty<string>());
        WriteMessages("Risk-of-Ruin Summary", report.RiskOfRuinSummary ?? Array.Empty<string>());
        WriteMessages("Top Demo Bot Candidates", report.TopDemoBotCandidates);
        WriteMessages("Next Validation", report.NextValidationRecommendations);
        foreach (var candidate in report.Candidates.Take(10))
        {
            WriteBotCandidate(candidate);
        }

        WriteField("No Bot Created", report.NoBotCreated.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowBotCandidateReport()
    {
        WriteHeader("Hermes Bot Candidate Report");
        var service = new BotCandidatePipelineService(BuildStoragePaths());
        var report = service.Evaluate();

        WriteField("Report", DisplayPath(service.LatestReportPath));
        WriteField("Created UTC", report.CreatedAtUtc.ToString("O"));
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("Bot Candidates", report.BotCandidateCount.ToString());
        WriteField("Demo Bot Candidates", report.DemoBotCandidateCount.ToString());
        WriteField("Rejected", report.RejectedCandidateCount.ToString());
        WriteField("Blocked Monte-Carlo", report.CandidatesBlockedByMonteCarlo.ToString());
        WriteField("Blocked Cost Stress", report.CandidatesBlockedByCostStress.ToString());
        WriteField("Blocked Risk", report.CandidatesBlockedByRisk.ToString());
        WriteMessages("Monte-Carlo Summary", report.MonteCarloSummary ?? Array.Empty<string>());
        WriteMessages("Cost Stress Summary", report.CostStressSummary ?? Array.Empty<string>());
        WriteMessages("Risk-of-Ruin Summary", report.RiskOfRuinSummary ?? Array.Empty<string>());
        WriteMessages("Top Demo Bot Candidates", report.TopDemoBotCandidates);
        WriteMessages("Next Validation", report.NextValidationRecommendations);
        WriteMessages(
            "Top Rejection Reasons",
            report.RejectionReasonCounts
                .OrderByDescending(item => item.Value)
                .ThenBy(item => item.Key, StringComparer.Ordinal)
                .Take(12)
                .Select(item => $"{item.Key}: {item.Value}")
                .ToList());

        foreach (var candidate in report.RejectedCandidates.Take(8))
        {
            WriteBotCandidate(candidate);
        }

        WriteField("No Bot Created", report.NoBotCreated.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", report.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", report.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCandidateRejectionAnalysis()
    {
        WriteHeader("Hermes Candidate Rejection Analysis");
        var analyzer = new BotCandidateRejectionAnalyzer(BuildStoragePaths());
        var report = analyzer.Run();

        WriteField("Analysis", DisplayPath(analyzer.AnalysisPath));
        WriteField("Near Miss", DisplayPath(analyzer.NearMissPath));
        WriteField("Experiments", DisplayPath(analyzer.ImprovementExperimentsPath));
        WriteField("Candidates Analyzed", report.CandidatesAnalyzed.ToString());
        WriteField("Rejected", report.RejectedCandidates.ToString());
        WriteField("Near Miss Count", report.NearMissCount.ToString());
        WriteMessages("Why No Candidates", report.WhyNoCandidates);
        WriteMessages(
            "Top Blockers",
            report.ReasonSummaries
                .Take(12)
                .Select(summary => $"{summary.Reason}: count={summary.Count}, share={summary.Share:P2}, category={summary.Category}, hint={summary.ImprovementHint}")
                .ToList());
        WriteMessages("Potential Clusters", report.PotentialClusters);
        WriteMessages("Unsuitable Clusters", report.UnsuitableClusters);
        WriteMessages(
            "Recommended Experiments",
            report.RecommendedImprovementExperiments
                .Take(8)
                .Select(FormatSuggestion)
                .ToList());
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowNearMissStrategies()
    {
        WriteHeader("Hermes Near-Miss Strategies");
        var analyzer = new BotCandidateRejectionAnalyzer(BuildStoragePaths());
        var report = analyzer.LoadAnalysis() ?? analyzer.Run();

        WriteField("Near Miss", DisplayPath(analyzer.NearMissPath));
        WriteField("Near Miss Count", report.NearMissCount.ToString());
        if (report.NearMissStrategies.Count == 0)
        {
            WriteWarning("Keine echten Near-Miss-Strategien gefunden. Zeige beste verworfene Strategien als Diagnose.");
            foreach (var item in report.BestRejectedStrategies.Take(12))
            {
                WriteCandidateGateDiagnostic(item);
            }
        }
        else
        {
            foreach (var item in report.NearMissStrategies.Take(20))
            {
                WriteCandidateGateDiagnostic(item);
            }
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowImprovementExperiments()
    {
        WriteHeader("Hermes Improvement Experiments");
        var analyzer = new BotCandidateRejectionAnalyzer(BuildStoragePaths());
        var report = analyzer.LoadAnalysis() ?? analyzer.Run();

        WriteField("Experiments", DisplayPath(analyzer.ImprovementExperimentsPath));
        WriteField("Suggestions", report.RecommendedImprovementExperiments.Count.ToString());
        foreach (var suggestion in report.RecommendedImprovementExperiments)
        {
            WriteStrategyImprovementSuggestion(suggestion);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunQualityImprovementExperiments()
    {
        WriteHeader("Hermes Quality Improvement Experiments");
        var maxBatchSize = ReadIntOption(_args, "--max-batch-size", fallback: 64, min: 1, max: 250);
        var service = new ResearchQualityImprovementExperimentService(BuildStoragePaths());
        var report = service.Run(maxBatchSize);

        WriteQualityImprovementReportHeader(service, report);
        WriteMessages("Blockers Addressed", report.BlockersAddressed);
        WriteMessages("Expected Blocker Reduction", report.ExpectedBlockerReduction);
        WriteField("OOS Experiments", report.OosExperiments.Count.ToString());
        WriteField("Cost Experiments", report.CostResilienceExperiments.Count.ToString());
        WriteField("Risk Experiments", report.RiskSensitivityExperiments.Count.ToString());
        WriteField("Regime Experiments", report.RegimeSessionFilterExperiments.Count.ToString());
        WriteField("Near Miss Changed", report.NearMissCountChanged.ToString().ToLowerInvariant());
        WriteField("Near Miss Note", report.NearMissImpactNote);
        WriteField("No Forced Approval", report.NoCandidateApprovalForced.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowQualityImprovementReport()
    {
        WriteHeader("Hermes Quality Improvement Report");
        var service = new ResearchQualityImprovementExperimentService(BuildStoragePaths());
        var report = service.LoadReport() ?? service.Run();

        WriteQualityImprovementReportHeader(service, report);
        WriteMessages("Blockers Addressed", report.BlockersAddressed);
        WriteMessages("Expected Blocker Reduction", report.ExpectedBlockerReduction);
        WriteSubHeader("Top OOS Experiments");
        foreach (var experiment in report.OosExperiments.Take(5))
        {
            WriteOosExperiment(experiment);
        }

        WriteSubHeader("Top Cost Experiments");
        foreach (var experiment in report.CostResilienceExperiments.Take(5))
        {
            WriteCostExperiment(experiment);
        }

        WriteSubHeader("Top Risk Experiments");
        foreach (var experiment in report.RiskSensitivityExperiments.Take(5))
        {
            WriteRiskExperiment(experiment);
        }

        WriteSubHeader("Top Regime/Session Experiments");
        foreach (var experiment in report.RegimeSessionFilterExperiments.Take(5))
        {
            WriteRegimeExperiment(experiment);
        }

        WriteField("Near Miss Baseline", report.BaselineNearMissCount.ToString());
        WriteField("Near Miss Changed", report.NearMissCountChanged.ToString().ToLowerInvariant());
        WriteField("Near Miss Note", report.NearMissImpactNote);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCostResilienceReport()
    {
        WriteHeader("Hermes Cost Resilience Experiments");
        var service = new ResearchQualityImprovementExperimentService(BuildStoragePaths());
        var experiments = service.LoadCostResilienceExperiments();
        if (experiments.Count == 0)
        {
            experiments = service.Run().CostResilienceExperiments;
        }

        WriteField("Report", DisplayPath(service.CostResiliencePath));
        WriteField("Experiments", experiments.Count.ToString());
        foreach (var experiment in experiments.Take(12))
        {
            WriteCostExperiment(experiment);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowOosStabilityReport()
    {
        WriteHeader("Hermes OOS Stability Experiments");
        var service = new ResearchQualityImprovementExperimentService(BuildStoragePaths());
        var experiments = service.LoadOosStabilityExperiments();
        if (experiments.Count == 0)
        {
            experiments = service.Run().OosExperiments;
        }

        WriteField("Report", DisplayPath(service.OosStabilityPath));
        WriteField("Experiments", experiments.Count.ToString());
        foreach (var experiment in experiments.Take(12))
        {
            WriteOosExperiment(experiment);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowRiskSensitivityReport()
    {
        WriteHeader("Hermes Risk Sensitivity Experiments");
        var service = new ResearchQualityImprovementExperimentService(BuildStoragePaths());
        var experiments = service.LoadRiskSensitivityExperiments();
        if (experiments.Count == 0)
        {
            experiments = service.Run().RiskSensitivityExperiments;
        }

        WriteField("Report", DisplayPath(service.RiskSensitivityPath));
        WriteField("Experiments", experiments.Count.ToString());
        foreach (var experiment in experiments.Take(12))
        {
            WriteRiskExperiment(experiment);
        }

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
        var insights = generator.Generate();

        WriteField("Insights", DisplayPath(generator.InsightsPath));
        WriteResearchInsights(insights);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeSources()
    {
        var storagePaths = BuildStoragePaths();
        var registry = new KnowledgeSourceRegistry(storagePaths);
        var sources = registry.LoadOrCreateSources();

        WriteHeader("Hermes Cognitive Knowledge Sources");
        WriteField("Sources", DisplayPath(registry.SourcesPath));
        WriteField("Source Count", sources.Count.ToString());
        foreach (var source in sources)
        {
            WriteSubHeader($"{source.SourceName} / {source.SourceId}");
            WriteField("URL/Path", source.UrlOrPath);
            WriteField("Domain", source.Domain);
            WriteField("Type", source.SourceType);
            WriteField("Trust", $"{source.TrustProfile.TrustLevel} ({source.TrustProfile.TrustScore:0.##})");
            WriteField("License", source.TrustProfile.LicenseHint);
            WriteField("Extraction", source.ExtractionStatus);
            WriteField("Last Checked", source.LastCheckedUtc.ToString("O"));
            WriteMessages("Concepts", source.ExtractedConcepts);
            WriteMessages("Risk Flags", source.RiskFlags);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCognitiveStatus()
    {
        WriteHeader("Hermes Cognitive Core Status");
        var service = new CognitiveCoreService(BuildStoragePaths());
        var status = service.BuildStatus();

        WriteField("Status", DisplayPath(service.StatusPath));
        WriteField("Root", DisplayPath(status.CognitiveRoot));
        WriteField("Sources", status.SourceCount.ToString());
        WriteField("Knowledge Items", status.KnowledgeItemCount.ToString());
        WriteField("Queue Items", status.QueueItemCount.ToString());
        WriteField("Insights", status.InsightCount.ToString());
        WriteField("Memory Entries", status.MemoryEntryCount.ToString());
        WriteMessages("Active Domains", status.ActiveDomains);
        WriteMessages("Next Actions", status.NextActions);
        foreach (var domain in status.Domains)
        {
            WriteSubHeader($"{domain.Name} / {domain.DomainId}");
            WriteField("Active", domain.Active.ToString().ToLowerInvariant());
            WriteField("Status", domain.Status);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ScanKnowledgeSources()
    {
        WriteHeader("Hermes Knowledge Source Scout");
        var storagePaths = BuildStoragePaths();
        var scout = new KnowledgeSourceScout(storagePaths);
        var sources = scout.Scan();
        var registry = new KnowledgeSourceRegistry(storagePaths);

        WriteField("Sources", DisplayPath(registry.SourcesPath));
        WriteField("Sources Scanned", sources.Count.ToString());
        WriteMessages("Domains", sources.Select(source => source.Domain).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        WriteMessages("Risk Flags", sources.SelectMany(source => source.RiskFlags).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowDomainStatus()
    {
        WriteHeader("Hermes Multi-Domain Cognitive Status");
        var service = new DomainCognitiveService(BuildStoragePaths());
        var status = service.BuildStatus();
        var insights = service.BuildInsights(status);

        WriteField("Domain Status", DisplayPath(service.DomainStatusPath));
        WriteField("Domain Insights", DisplayPath(service.DomainInsightsPath));
        WriteField("Active Domains", string.Join(", ", status.ActiveDomains));
        WriteField("Domains", status.Domains.Count.ToString());
        WriteField("Insights", insights.Insights.Count.ToString());
        WriteMessages("Weak Domains", status.WeakDomains);
        WriteMessages("Strong Domains", status.StrongDomains);
        foreach (var entry in status.Domains)
        {
            WriteCognitiveDomainStatusEntry(entry);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowDomainInsights()
    {
        WriteHeader("Hermes Multi-Domain Insights");
        var service = new DomainCognitiveService(BuildStoragePaths());
        var report = service.BuildInsights();

        WriteField("Domain Insights", DisplayPath(service.DomainInsightsPath));
        WriteField("Updated UTC", report.UpdatedAtUtc.ToString("O"));
        WriteField("Insights", report.Insights.Count.ToString());
        foreach (var insight in report.Insights.Take(40))
        {
            WriteCognitiveDomainInsight(insight);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowSingleDomainStatus(string domain)
    {
        WriteHeader($"Hermes {domain} Domain Status");
        var service = new DomainCognitiveService(BuildStoragePaths());
        var status = service.BuildStatus();
        var insights = service.BuildInsights(status);
        var entry = status.Domains.FirstOrDefault(item => item.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));
        if (entry is null)
        {
            WriteWarning($"Domain nicht gefunden: {domain}");
            WriteSafety();
            return 1;
        }

        WriteField("Domain Status", DisplayPath(service.DomainStatusPath));
        WriteField("Domain Directory", DisplayPath(Path.Combine(service.DomainsRoot, domain)));
        WriteCognitiveDomainStatusEntry(entry);
        foreach (var insight in insights.Insights.Where(item => item.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase)).Take(12))
        {
            WriteCognitiveDomainInsight(insight);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ScanDomain(string domain)
    {
        WriteHeader($"Hermes scan-{domain}-domain");
        var service = new DomainCognitiveService(BuildStoragePaths());
        var result = service.ScanDomain(domain);

        WriteDomainScanResult(result);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeCatalog()
    {
        WriteHeader("Hermes Cognitive Knowledge Catalog");
        var catalog = new KnowledgeCatalog(BuildStoragePaths());
        var items = catalog.LoadOrCreateItems();

        WriteField("Catalog", DisplayPath(catalog.CatalogPath));
        WriteField("Items", items.Count.ToString());
        foreach (var item in items.Take(40))
        {
            WriteKnowledgeCatalogItem(item);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowKnowledgeItem()
    {
        WriteHeader("Hermes Cognitive Knowledge Item");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteWarning("Bitte --id <ID> angeben, z. B. --id trading:breakout.");
            WriteSafety();
            return 1;
        }

        var catalog = new KnowledgeCatalog(BuildStoragePaths());
        var item = catalog.FindById(id);
        if (item is null)
        {
            WriteWarning($"Knowledge Item nicht gefunden: {id}");
            WriteSafety();
            return 1;
        }

        WriteField("Catalog", DisplayPath(catalog.CatalogPath));
        WriteKnowledgeCatalogItem(item);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowResearchQueue()
    {
        WriteHeader("Hermes Cognitive Research Queue");
        var service = new ResearchQueueService(BuildStoragePaths());
        var queue = service.LoadOrCreateQueue();

        WriteField("Queue", DisplayPath(service.QueuePath));
        WriteField("Items", queue.Items.Count.ToString());
        WriteField("Open", queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)).ToString());
        WriteField("Processed", queue.Items.Count(item => item.Status.Equals("processed", StringComparison.OrdinalIgnoreCase)).ToString());
        foreach (var item in queue.Items.Take(30))
        {
            WriteResearchQueueItem(item);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int EnqueueResearch()
    {
        WriteHeader("Hermes Enqueue Research");
        var domain = ReadOption(_args, "--domain") ?? "trading";
        var type = ReadOption(_args, "--type") ?? "validation";
        var service = new ResearchQueueService(BuildStoragePaths());
        var queue = service.Enqueue(domain, type);

        WriteField("Queue", DisplayPath(service.QueuePath));
        WriteField("Items", queue.Items.Count.ToString());
        WriteField("Last Item", queue.Items.LastOrDefault()?.QueueItemId ?? "-");
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ProcessResearchQueue()
    {
        WriteHeader("Hermes Process Research Queue");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 50, min: 1, max: 500);
        var service = new ResearchQueueService(BuildStoragePaths());
        var before = service.LoadOrCreateQueue();
        var beforeProcessed = before.Items.Count(item => item.Status.Equals("processed", StringComparison.OrdinalIgnoreCase));
        var queue = service.Process(maxItems);
        var afterProcessed = queue.Items.Count(item => item.Status.Equals("processed", StringComparison.OrdinalIgnoreCase));

        WriteField("Queue", DisplayPath(service.QueuePath));
        WriteField("Processed This Run", Math.Max(0, afterProcessed - beforeProcessed).ToString());
        WriteField("Processed Total", afterProcessed.ToString());
        WriteField("Open", queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)).ToString());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int GenerateHypotheses()
    {
        WriteHeader("Hermes Hypothesis Generator");
        var domain = ReadOption(_args, "--domain") ?? "trading";
        var generator = new HypothesisGenerator(BuildStoragePaths());
        var hypotheses = generator.Generate(domain);

        WriteField("Hypotheses", DisplayPath(generator.HypothesesPath));
        WriteField("Insights", DisplayPath(generator.InsightsPath));
        WriteField("Generated", hypotheses.Count.ToString());
        foreach (var hypothesis in hypotheses.Take(20))
        {
            WriteCognitiveHypothesis(hypothesis);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowCognitiveInsights()
    {
        WriteHeader("Hermes Cognitive Insights");
        var generator = new HypothesisGenerator(BuildStoragePaths());
        var insights = generator.LoadInsights();
        if (insights.Count == 0)
        {
            generator.Generate("trading");
            insights = generator.LoadInsights();
        }

        WriteField("Insights", DisplayPath(generator.InsightsPath));
        WriteField("Count", insights.Count.ToString());
        foreach (var insight in insights.Take(30))
        {
            WriteCognitiveInsight(insight);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowPlanningStatus()
    {
        WriteHeader("Hermes Autonomous Planning Status");
        var storagePaths = BuildStoragePaths();
        var service = new AutonomousPlanningCycleService(storagePaths);
        var status = service.BuildStatus();
        var outcomeStatus = new TaskOutcomeEvaluator(storagePaths).BuildStatus();

        WriteField("Status", DisplayPath(service.PlanningStatusPath));
        WriteField("Detected Needs", status.NeedsDetected.ToString());
        WriteField("Active Goals", status.ActiveGoals.ToString());
        WriteField("Planned Tasks", status.PlannedTasks.ToString());
        WriteField("Queued Research Items", status.QueuedResearchItems.ToString());
        WriteField("Last Decision", status.LastDecisionId);
        WriteField("Next Action", status.NextAction);
        WriteMessages("Active Domains", status.ActiveDomains);
        WriteMessages("Top Needs", status.TopNeeds);
        WriteMessages("Top Tasks", status.TopTasks);
        WriteMessages("Warnings", status.Warnings);
        WriteField("Outcome Feedback", DisplayPath(outcomeStatus.TaskOutcomesPath));
        WriteField("Outcome Total", outcomeStatus.TotalOutcomes.ToString());
        WriteField("Outcome Last UTC", outcomeStatus.LastOutcomeUtc?.ToString("O") ?? "-");
        WriteMessages("Outcome Recommendations", outcomeStatus.LatestRecommendations.Take(5).ToList());
        WriteField("no_auto_trading", status.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", status.HumanReviewRequired.ToString().ToLowerInvariant());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int DetectNeeds()
    {
        WriteHeader("Hermes Need Detection");
        var service = new AutonomousPlanningCycleService(BuildStoragePaths());
        var needs = service.DetectNeeds();

        WriteField("Needs", DisplayPath(service.DetectedNeedsPath));
        WriteField("Detected", needs.Count.ToString());
        foreach (var need in needs.Take(30))
        {
            WriteDetectedNeed(need);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int PlanNextTasks()
    {
        WriteHeader("Hermes Autonomous Task Planner");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 20, min: 1, max: 100);
        var service = new AutonomousPlanningCycleService(BuildStoragePaths());
        var decision = service.PlanNextTasks(maxItems);

        WriteField("Decision", decision.DecisionId);
        WriteField("Planned Tasks", DisplayPath(service.PlannedTasksPath));
        WriteField("Needs", decision.Needs.Count.ToString());
        WriteField("Goals", decision.Goals.Count.ToString());
        WriteField("Tasks", decision.PlannedTasks.Count.ToString());
        foreach (var task in decision.PlannedTasks.Take(30))
        {
            WritePlannedTask(task);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunPlanningCycle()
    {
        WriteHeader("Hermes Autonomous Planning Cycle");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 20, min: 1, max: 100);
        var storagePaths = BuildStoragePaths();
        var service = new AutonomousPlanningCycleService(storagePaths);
        var decision = service.RunPlanningCycle(maxItems);
        var queue = new ResearchQueueService(storagePaths).LoadOrCreateQueue();

        WriteField("Planning Status", DisplayPath(service.PlanningStatusPath));
        WriteField("Detected Needs", decision.Needs.Count.ToString());
        WriteField("Planned Tasks", decision.PlannedTasks.Count.ToString());
        WriteField("Research Queue", DisplayPath(new ResearchQueueService(storagePaths).QueuePath));
        WriteField("Open Queue Items", queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase)).ToString());
        WriteMessages("Top Reasons", decision.Explanations.Take(8).ToList());
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExecutePlannedTasks()
    {
        WriteHeader("Hermes Controlled Planned Task Execution");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 10, min: 1, max: 100);
        var storagePaths = BuildStoragePaths();
        var executor = new PlannedTaskExecutor(storagePaths);
        var results = executor.Execute(maxItems);
        var state = executor.LoadState() ?? executor.BuildStatus();

        WriteField("Execution State", DisplayPath(executor.ExecutionStatePath));
        WriteField("Execution Log", DisplayPath(executor.ExecutionLogPath));
        WriteField("Requested Max Items", maxItems.ToString());
        WriteField("Results", results.Count.ToString());
        WriteField("Completed", results.Count(result => result.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)).ToString());
        WriteField("Skipped", results.Count(result => result.Status.Equals("skipped", StringComparison.OrdinalIgnoreCase)).ToString());
        WriteField("Failed", results.Count(result => result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)).ToString());
        WriteField("Pending Tasks", state.PendingTasks.ToString());
        foreach (var result in results)
        {
            WritePlannedTaskExecutionResult(result);
        }

        Console.WriteLine();
        WriteSafety();
        return results.Any(result => result.Status.Equals("failed", StringComparison.OrdinalIgnoreCase)) ? 1 : 0;
    }

    private int ShowPlannedTaskStatus()
    {
        WriteHeader("Hermes Planned Task Execution Status");
        var executor = new PlannedTaskExecutor(BuildStoragePaths());
        var state = executor.BuildStatus();

        WriteField("State", DisplayPath(executor.ExecutionStatePath));
        WriteField("Execution Log", DisplayPath(executor.ExecutionLogPath));
        WriteField("Updated UTC", state.UpdatedAtUtc.ToString("O"));
        WriteField("Pending", state.PendingTasks.ToString());
        WriteField("Running", state.RunningTasks.ToString());
        WriteField("Completed", state.CompletedTasks.ToString());
        WriteField("Skipped", state.SkippedTasks.ToString());
        WriteField("Failed", state.FailedTasks.ToString());
        WriteField("Running Task", state.RunningTaskId ?? "-");
        WriteField("Last Task", state.LastTaskId ?? "-");
        WriteField("Last Status", state.LastStatus);
        foreach (var result in state.RecentResults.Take(10))
        {
            WritePlannedTaskExecutionResult(result);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowTaskExecutionLog()
    {
        WriteHeader("Hermes Planned Task Execution Log");
        var limit = ReadIntOption(_args, "--limit", fallback: 20, min: 1, max: 200);
        var executor = new PlannedTaskExecutor(BuildStoragePaths());
        var results = executor.LoadRecentResults(limit);

        WriteField("Execution Log", DisplayPath(executor.ExecutionLogPath));
        WriteField("Entries Shown", results.Count.ToString());
        foreach (var result in results)
        {
            WritePlannedTaskExecutionResult(result);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int EvaluateTaskOutcomes()
    {
        WriteHeader("Hermes Task Outcome Evaluation");
        var maxItems = ReadIntOption(_args, "--max-items", fallback: 50, min: 1, max: 500);
        var evaluator = new TaskOutcomeEvaluator(BuildStoragePaths());
        var outcomes = evaluator.Evaluate(maxItems);
        var status = evaluator.BuildStatus();

        WriteField("Task Outcomes", DisplayPath(evaluator.TaskOutcomesPath));
        WriteField("Planner Feedback", DisplayPath(evaluator.PlannerFeedbackPath));
        WriteField("Goal Feedback", DisplayPath(evaluator.GoalFeedbackPath));
        WriteField("Evaluated", outcomes.Count.ToString());
        WriteField("Total Outcomes", status.TotalOutcomes.ToString());
        foreach (var outcome in outcomes.Take(20))
        {
            WriteTaskOutcome(outcome);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowOutcomeFeedbackStatus()
    {
        WriteHeader("Hermes Outcome Feedback Status");
        var evaluator = new TaskOutcomeEvaluator(BuildStoragePaths());
        var status = evaluator.BuildStatus();

        WriteField("Status", DisplayPath(evaluator.StatusPath));
        WriteField("Task Outcomes", DisplayPath(status.TaskOutcomesPath));
        WriteField("Planner Feedback", DisplayPath(status.PlannerFeedbackPath));
        WriteField("Goal Feedback", DisplayPath(status.GoalFeedbackPath));
        WriteField("Updated UTC", status.UpdatedAtUtc.ToString("O"));
        WriteField("Total Outcomes", status.TotalOutcomes.ToString());
        WriteField("Last Outcome UTC", status.LastOutcomeUtc?.ToString("O") ?? "-");
        WriteField("Evaluated Last Run", status.OutcomesEvaluatedLastRun.ToString());
        WriteMessages("Latest Recommendations", status.LatestRecommendations);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowPlannerFeedback()
    {
        WriteHeader("Hermes Planner Feedback");
        var evaluator = new TaskOutcomeEvaluator(BuildStoragePaths());
        var feedback = evaluator.LoadOrCreatePlannerFeedback();

        WriteField("Planner Feedback", DisplayPath(evaluator.PlannerFeedbackPath));
        WriteField("Updated UTC", feedback.UpdatedAtUtc.ToString("O"));
        WriteField("Outcomes Evaluated", feedback.OutcomesEvaluated.ToString());
        WriteMessages("Retired Task Types", feedback.RetiredTaskTypes);
        WriteMessages("Warnings", feedback.Warnings);
        foreach (var item in feedback.TaskTypeFeedback.Take(30))
        {
            WritePlannerTaskTypeFeedback(item);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowGoalFeedback()
    {
        WriteHeader("Hermes Goal Feedback");
        var evaluator = new TaskOutcomeEvaluator(BuildStoragePaths());
        var feedback = evaluator.LoadOrCreateGoalFeedback();

        WriteField("Goal Feedback", DisplayPath(evaluator.GoalFeedbackPath));
        WriteField("Updated UTC", feedback.UpdatedAtUtc.ToString("O"));
        WriteField("Outcomes Evaluated", feedback.OutcomesEvaluated.ToString());
        WriteMessages("Warnings", feedback.Warnings);
        foreach (var item in feedback.Goals.Take(30))
        {
            WriteGoalFeedbackEntry(item);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int RunAutonomousLoop()
    {
        WriteHeader("Hermes Autonomous Learning Loop");
        var loop = BuildAutonomousLearningLoop();
        var config = loop.LoadConfig();
        var maxIterations = ReadIntOption(
            _args,
            "--max-iterations",
            fallback: config.MaxIdleIterations,
            min: 1,
            max: 1000);
        var maxMinutes = ReadDoubleOption(_args, "--max-minutes", fallback: 30, min: 0.01, max: 1440);
        var summary = loop.Run(maxIterations, maxMinutes);

        WriteField("Config", DisplayPath(Path.Combine(_runtimeRoot, "config", "autonomous.loop.json")));
        WriteAutonomousLoopSummary(summary);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowAutonomousLoopStatus()
    {
        WriteHeader("Hermes Autonomous Learning Loop Status");
        var loop = BuildAutonomousLearningLoop();
        var state = loop.LoadState();
        var summary = loop.LoadSummary();

        WriteField("State", DisplayPath(loop.StatePath));
        WriteField("Summary", DisplayPath(loop.SummaryPath));
        WriteField("Log", DisplayPath(loop.LogPath));
        WriteField("Status", state.Status);
        WriteField("Run ID", string.IsNullOrWhiteSpace(state.RunId) ? "-" : state.RunId);
        WriteField("Iterations", state.IterationsCompleted.ToString());
        WriteField("Idle Iterations", state.IdleIterations.ToString());
        WriteField("Work Performed", state.WorkPerformed.ToString());
        WriteField("Average Learning", $"{state.AverageLearningValue:0.####}");
        WriteField("Next Action", state.NextAction);
        WriteField("Last Stop Reason", state.LastStopReason ?? "-");
        WriteField("Last Checkpoint", DisplayOptionalPath(state.LastCheckpointPath));
        if (summary?.LastIteration is not null)
        {
            WriteAutonomousLoopIteration(summary.LastIteration);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowAutonomousLoopLog()
    {
        WriteHeader("Hermes Autonomous Learning Loop Log");
        var loop = BuildAutonomousLearningLoop();
        var limit = ReadIntOption(_args, "--limit", fallback: 10, min: 1, max: 100);
        var iterations = loop.LoadLog(limit);

        WriteField("Log", DisplayPath(loop.LogPath));
        WriteField("Entries", iterations.Count.ToString());
        foreach (var iteration in iterations.Take(limit))
        {
            WriteAutonomousLoopIteration(iteration);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExplainLastLoop()
    {
        WriteHeader("Hermes Last Autonomous Loop Explanation");
        var loop = BuildAutonomousLearningLoop();
        var summary = loop.LoadSummary();
        var last = summary?.LastIteration ?? loop.LoadLog(1).FirstOrDefault();
        if (last is null)
        {
            WriteField("Status", "no_loop_iteration_available");
            WriteField("Next Action", "run-autonomous-loop");
            Console.WriteLine();
            WriteSafety();
            return 0;
        }

        WriteAutonomousLoopIteration(last);
        WriteMessages("Warum weiter/stoppen",
            [
                last.StopReason is not null
                    ? $"stop_reason:{last.StopReason}"
                    : last.Idle
                        ? "idle:true; keine neuen sinnvollen Ausfuehrungen oder Outcomes"
                        : "learning_work_detected:true",
                $"next_action:{last.NextAction}",
                $"feedback_changes:{last.FeedbackChanges.Count}",
                "no_trading_execution:true",
                "human_review_required:true"
            ]);

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowMetaReview()
    {
        WriteHeader("Hermes Meta Review");
        var engine = new MetaReviewEngine(BuildStoragePaths());
        var review = engine.RunReview();

        WriteField("Meta Review", DisplayPath(engine.MetaReviewPath));
        WriteMetaReview(review);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowDomainHealth()
    {
        WriteHeader("Hermes Domain Health");
        var engine = new MetaReviewEngine(BuildStoragePaths());
        var health = engine.LoadOrCreateDomainHealth();

        WriteField("Domain Health", DisplayPath(engine.DomainHealthPath));
        foreach (var domain in health)
        {
            WriteDomainHealth(domain);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowLearningStrategy()
    {
        WriteHeader("Hermes Learning Strategy");
        var engine = new MetaReviewEngine(BuildStoragePaths());
        var strategy = engine.LoadOrCreateLearningStrategy();

        WriteField("Learning Strategy", DisplayPath(engine.LearningStrategyPath));
        WriteLearningStrategy(strategy);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowGovernanceStatus()
    {
        WriteHeader("Hermes Governance Status");
        var engine = new MetaReviewEngine(BuildStoragePaths());
        var review = engine.LoadOrCreateReview();

        WriteField("Meta Review", DisplayPath(engine.MetaReviewPath));
        WriteField("Decisions", review.GovernanceDecisions.Count.ToString());
        foreach (var decision in review.GovernanceDecisions)
        {
            WriteGovernanceDecision(decision);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExplainPlan()
    {
        WriteHeader("Hermes Planning Explanation");
        var storagePaths = BuildStoragePaths();
        var service = new AutonomousPlanningCycleService(storagePaths);
        var decision = service.LoadLatestDecision() ?? service.PlanNextTasks(20);
        var plannerFeedback = new TaskOutcomeEvaluator(storagePaths).LoadPlannerFeedback();

        WriteField("Decision", decision.DecisionId);
        WriteField("Created UTC", decision.CreatedAtUtc.ToString("O"));
        WriteField("Needs", decision.Needs.Count.ToString());
        WriteField("Goals", decision.Goals.Count.ToString());
        WriteField("Tasks", decision.PlannedTasks.Count.ToString());
        if (plannerFeedback is not null)
        {
            WriteField("Planner Feedback UTC", plannerFeedback.UpdatedAtUtc.ToString("O"));
            WriteMessages(
                "Feedback Adjustments",
                plannerFeedback.TaskTypeFeedback
                    .Where(item => Math.Abs(item.PriorityAdjustment) > 0.0001)
                    .Select(item => $"{item.TaskType}: {item.Recommendation}, adjustment={item.PriorityAdjustment:0.####}")
                    .Take(12)
                    .ToList());
        }

        WriteMessages("Explanation", decision.Explanations.Take(20).ToList());
        foreach (var task in decision.PlannedTasks.Take(10))
        {
            WritePlannedTask(task);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ExplainTask()
    {
        WriteHeader("Hermes Task Explanation");
        var id = ReadOption(_args, "--id");
        if (string.IsNullOrWhiteSpace(id))
        {
            WriteWarning("Bitte --id <TASK_ID> angeben.");
            WriteSafety();
            return 1;
        }

        var service = new AutonomousPlanningCycleService(BuildStoragePaths());
        var decision = service.LoadLatestDecision() ?? service.PlanNextTasks(20);
        var task = decision.PlannedTasks.FirstOrDefault(item => item.TaskId.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (task is null)
        {
            WriteWarning($"Task nicht gefunden: {id}");
            WriteSafety();
            return 1;
        }

        WritePlannedTask(task);
        var need = decision.Needs.FirstOrDefault(item => item.NeedId.Equals(task.NeedId, StringComparison.OrdinalIgnoreCase));
        if (need is not null)
        {
            WriteDetectedNeed(need);
        }

        var goal = decision.Goals.FirstOrDefault(item => item.GoalId.Equals(task.GoalId, StringComparison.OrdinalIgnoreCase));
        if (goal is not null)
        {
            WriteHermesGoal(goal);
        }

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

    private int ShowRegimeSummary()
    {
        WriteHeader("Hermes Market Regime Summary");
        var classifier = new MarketRegimeClassifier(BuildStoragePaths());
        var analysis = classifier.Run();

        WriteField("Summary", DisplayPath(analysis.SummaryPath));
        WriteField("Distribution", DisplayPath(analysis.DistributionPath));
        WriteField("Strategy Performance", DisplayPath(analysis.StrategyPerformancePath));
        WriteField("Snapshot Memory", DisplayPath(analysis.SnapshotMemoryPath));
        WriteRegimeSummary(analysis.Summary);
        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowStrategyRegimePerformance()
    {
        WriteHeader("Hermes Strategy Regime Performance");
        var classifier = new MarketRegimeClassifier(BuildStoragePaths());
        var report = classifier.LoadStrategyPerformance() ?? classifier.Run().StrategyPerformance;

        WriteField("Report", DisplayPath(classifier.StrategyPerformancePath));
        WriteField("Strategies Analyzed", report.StrategiesAnalyzed.ToString());
        WriteField("Regime Snapshots", report.RegimeSnapshotsAnalyzed.ToString());
        WriteField("Regime Consistency", $"{report.RegimeConsistencyScore:0.####}");
        WriteField("Regime Sample Quality", $"{report.RegimeSampleQuality:0.####}");
        WriteMessages("Strong Regime Matches", report.StrongRegimeMatches.Take(12).ToList());
        WriteMessages("Weak Regime Matches", report.WeakRegimeMatches.Take(12).ToList());
        WriteMessages("Preferred Regimes", report.PreferredRegimes?.Take(12).ToList() ?? []);
        WriteMessages("Avoided Regimes", report.AvoidedRegimes?.Take(12).ToList() ?? []);
        WriteMessages("Preferred Sessions", report.PreferredSessions);
        WriteMessages("Avoid Sessions", report.AvoidSessions);
        WriteMessages("Volatility Preference", report.VolatilityPreference);
        foreach (var entry in report.Entries.Take(10))
        {
            WriteStrategyRegimeEntry(entry);
        }

        Console.WriteLine();
        WriteSafety();
        return 0;
    }

    private int ShowRegimeDistribution()
    {
        WriteHeader("Hermes Regime Distribution");
        var classifier = new MarketRegimeClassifier(BuildStoragePaths());
        var report = classifier.LoadDistribution() ?? classifier.Run().Distribution;

        WriteField("Report", DisplayPath(classifier.DistributionPath));
        WriteField("Total Candles", report.TotalCandles.ToString());
        foreach (var entry in report.Entries.Take(20))
        {
            WriteSubHeader($"{entry.Symbol} {entry.Timeframe} / {entry.RegimeType} / {entry.Session}");
            WriteField("Candles", entry.CandleCount.ToString());
            WriteField("Share", $"{entry.Percentage:P2}");
            WriteField("Confidence", $"{entry.AverageConfidence:0.####}");
        }

        WriteMessages("Warnings", report.Warnings);
        Console.WriteLine();
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
            WriteField("Source", pattern.SourceName ?? "local");
            WriteField("Source URL", pattern.SourceUrl ?? "-");
            WriteField("source_trust", pattern.SourceTrust ?? "-");
            WriteField("Category", pattern.Category ?? "-");
            WriteField("Direction Bias", pattern.DirectionBias);
            WriteField("Strategy Family", StrategyPatternCatalog.StrategyFamilyForPattern(pattern.Id));
            WriteField("Timeframes", string.Join(", ", pattern.RequiredTimeframes));
            WriteField("Market Context", pattern.MarketContext ?? "-");
            WriteField("Test Priority", pattern.TestPriority ?? "-");
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
        var sourcePerformance = generator.LoadSourcePerformance();

        WriteField("Catalog", DisplayPath(catalog.CatalogPath));
        WriteField("Insights", DisplayPath(generator.InsightsPath));
        WriteMessages("Pattern Performance", performance);
        WriteMessages("Source Performance", sourcePerformance);
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

    private void WriteBotCandidate(BotCandidate candidate)
    {
        WriteSubHeader($"{candidate.Status} / {candidate.StrategyFamily} / {candidate.PatternId ?? "-"} / {candidate.StrategyId}");
        WriteField("Candidate ID", candidate.CandidateId);
        WriteField("Symbol/Timeframe", $"{candidate.Symbol}/{candidate.Timeframe}");
        WriteField("Confidence", candidate.Criteria.Confidence);
        WriteField("OOS Available", candidate.Criteria.OosAvailable.ToString().ToLowerInvariant());
        WriteField("WalkForward Confidence", $"{candidate.Criteria.WalkForwardConfidence:0.####}");
        WriteField("Realism Score", $"{candidate.Criteria.RealismScore:0.####}");
        WriteField("Overfit Risk", $"{candidate.Criteria.OverfitRisk:0.####}");
        WriteField("Cost Sensitivity", $"{candidate.Criteria.CostSensitivity:0.####}");
        WriteField("Regime Consistency", $"{candidate.Criteria.RegimeConsistencyScore:0.####}");
        WriteField("Max Drawdown", $"{candidate.Criteria.MaxDrawdown:0.####}");
        WriteField("Profit Factor", $"{candidate.Criteria.ProfitFactor:0.####}");
        WriteField("Sample Quality", $"{candidate.Criteria.SampleQuality:0.####}");
        WriteField("too_good_to_be_true", candidate.Criteria.TooGoodToBeTrue.ToString().ToLowerInvariant());
        WriteField("Monte-Carlo Passed", candidate.Criteria.MonteCarloPassed.ToString().ToLowerInvariant());
        WriteField("Positive Sim Ratio", $"{candidate.Criteria.PositiveSimulationRatio:0.####}");
        WriteField("Survives Spread x2", candidate.Criteria.SurvivesSpreadX2.ToString().ToLowerInvariant());
        WriteField("Survives Stress Cost", candidate.Criteria.SurvivesStressCost.ToString().ToLowerInvariant());
        WriteField("Risk of Ruin", $"{candidate.Criteria.RiskOfRuinProbabilityEstimate:0.####}");
        WriteField("Recommended Risk", $"{candidate.Criteria.RecommendedMaxRiskPerTrade:0.####}%");
        WriteField("Next Validation", candidate.NextValidationRecommendation);
        WriteMessages("Rejection Reasons", candidate.RejectionReasons.Take(12).ToList());
        WriteMessages("Overfit Flags", candidate.OverfitFlags.Take(8).ToList());
        WriteField("No Bot Created", candidate.NoBotCreated.ToString().ToLowerInvariant());
        WriteField("No Trading Execution", candidate.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("No Broker Action", candidate.NoBrokerAction.ToString().ToLowerInvariant());
        Console.WriteLine();
    }

    private void WriteCandidateGateDiagnostic(CandidateGateDiagnostics item)
    {
        WriteSubHeader($"{item.StrategyFamily} / {item.PatternId ?? "-"} / {item.StrategyId}");
        WriteField("Candidate ID", item.CandidateId);
        WriteField("Symbol/Timeframe", $"{item.Symbol}/{item.Timeframe}");
        WriteField("Status", item.Status);
        WriteField("Primary Reason", item.PrimaryRejectionReason);
        WriteField("Weakest Metric", item.WeakestMetric);
        WriteField("Nearest Threshold", item.NearestPassThreshold);
        WriteField("Near-Miss Score", $"{item.NearMissScore:0.####}");
        WriteField("Near Miss", item.IsNearMiss.ToString().ToLowerInvariant());
        WriteField("Unsuitable", item.IsCompletelyUnsuitable.ToString().ToLowerInvariant());
        WriteField("Improvement Hint", item.ImprovementHint);
        WriteMessages("Secondary Reasons", item.SecondaryRejectionReasons.Take(6).ToList());
        Console.WriteLine();
    }

    private void WriteStrategyImprovementSuggestion(StrategyImprovementSuggestion suggestion)
    {
        WriteSubHeader($"{suggestion.Priority} / {suggestion.SuggestionId}");
        WriteField("Title", suggestion.Title);
        WriteField("Target Metric", suggestion.TargetMetric);
        WriteField("Expected Impact", suggestion.ExpectedImpact);
        WriteField("Description", suggestion.Description);
        WriteMessages("Related Reasons", suggestion.RelatedRejectionReasons);
        Console.WriteLine();
    }

    private static string FormatSuggestion(StrategyImprovementSuggestion suggestion) =>
        $"{suggestion.Priority}:{suggestion.SuggestionId}:{suggestion.Title} -> {suggestion.TargetMetric}";

    private void WriteQualityImprovementReportHeader(
        ResearchQualityImprovementExperimentService service,
        QualityImprovementExperimentReport report)
    {
        WriteField("Quality Report", DisplayPath(service.QualityImprovementPath));
        WriteField("OOS Report", DisplayPath(service.OosStabilityPath));
        WriteField("Cost Report", DisplayPath(service.CostResiliencePath));
        WriteField("Risk Report", DisplayPath(service.RiskSensitivityPath));
        WriteField("Candidates Analyzed", report.CandidatesAnalyzed.ToString());
        WriteField("Batch Size", report.BatchSize.ToString());
        WriteField("Baseline Near Miss", report.BaselineNearMissCount.ToString());
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteOosExperiment(OosQualityImprovementExperiment experiment)
    {
        WriteSubHeader($"{experiment.PriorityRank:00} / {experiment.StrategyFamily} / {experiment.PatternId ?? "-"} / {experiment.TargetStrategyId}");
        WriteField("Symbol/Timeframe", $"{experiment.Symbol}/{experiment.Timeframe}");
        WriteField("Source Score", $"{experiment.SourceNearMissScore:0.####}");
        WriteField("Walk-Forward Plan", experiment.WalkForwardPlan);
        WriteField("Expected Impact", experiment.ExpectedImpact);
        WriteMessages("Addressed Blockers", experiment.AddressedBlockers);
        WriteMessages("Proposed Filters", experiment.ProposedFilters);
        WriteMessages("Rolling Windows", experiment.RollingValidationWindows);
        Console.WriteLine();
    }

    private void WriteCostExperiment(CostResilienceExperiment experiment)
    {
        WriteSubHeader($"{experiment.PriorityRank:00} / {experiment.StrategyFamily} / {experiment.PatternId ?? "-"} / {experiment.TargetStrategyId}");
        WriteField("Symbol/Timeframe", $"{experiment.Symbol}/{experiment.Timeframe}");
        WriteField("Source Score", $"{experiment.SourceNearMissScore:0.####}");
        WriteField("Minimum Move/Cost", $"{experiment.MinimumMoveToCostRatio:0.##}x");
        WriteField("Spread Stress", experiment.SpreadStressScenario);
        WriteField("Slippage Stress", experiment.SlippageStressScenario);
        WriteField("Expected Impact", experiment.ExpectedImpact);
        WriteMessages("Addressed Blockers", experiment.AddressedBlockers);
        WriteMessages("Proposed Filters", experiment.ProposedFilters);
        WriteMessages("Avoid Sessions", experiment.AvoidSessions);
        Console.WriteLine();
    }

    private void WriteRiskExperiment(RiskSensitivityExperiment experiment)
    {
        WriteSubHeader($"{experiment.PriorityRank:00} / {experiment.StrategyFamily} / {experiment.PatternId ?? "-"} / {experiment.TargetStrategyId}");
        WriteField("Symbol/Timeframe", $"{experiment.Symbol}/{experiment.Timeframe}");
        WriteField("Source Score", $"{experiment.SourceNearMissScore:0.####}");
        WriteField("Risk Profiles", string.Join(", ", experiment.RiskProfiles.Select(value => $"{value:P2}")));
        WriteField("Target Ruin Probability", $"{experiment.TargetRuinProbability:P2}");
        WriteField("Trade Frequency", experiment.MaxTradeFrequencyHint);
        WriteField("Drawdown Control", experiment.DrawdownControl);
        WriteField("Expected Impact", experiment.ExpectedImpact);
        WriteMessages("Addressed Blockers", experiment.AddressedBlockers);
        Console.WriteLine();
    }

    private void WriteRegimeExperiment(RegimeSessionFilterExperiment experiment)
    {
        WriteSubHeader($"{experiment.PriorityRank:00} / {experiment.StrategyFamily} / {experiment.PatternId ?? "-"} / {experiment.TargetStrategyId}");
        WriteField("Symbol/Timeframe", $"{experiment.Symbol}/{experiment.Timeframe}");
        WriteField("Source Score", $"{experiment.SourceNearMissScore:0.####}");
        WriteField("Volatility Filter", experiment.VolatilityFilter);
        WriteField("Expected Impact", experiment.ExpectedImpact);
        WriteMessages("Addressed Blockers", experiment.AddressedBlockers);
        WriteMessages("Preferred Regimes", experiment.PreferredRegimes);
        WriteMessages("Avoided Regimes", experiment.AvoidedRegimes);
        WriteMessages("Preferred Sessions", experiment.PreferredSessions);
        WriteMessages("Avoided Sessions", experiment.AvoidedSessions);
        Console.WriteLine();
    }

    private void WriteMonteCarloResult(MonteCarloResult result)
    {
        WriteSubHeader($"{result.StrategyFamily} / {result.PatternId ?? "-"} / {result.StrategyVariantId}");
        WriteField("Symbol/Timeframe", $"{result.Symbol}/{result.Timeframe}");
        WriteField("Simulations", result.SimulationsRun.ToString());
        WriteField("Positive Ratio", $"{result.PositiveSimulationRatio:0.####}");
        WriteField("Median Return", $"{result.MedianReturn:0.####}");
        WriteField("Worst Drawdown", $"{result.WorstCaseDrawdown:0.####}");
        WriteField("Ruin Probability", $"{result.RuinProbabilityEstimate:0.####}");
        WriteField("monte_carlo_passed", result.MonteCarloPassed.ToString().ToLowerInvariant());
        WriteMessages("Warnings", result.Warnings);
    }

    private void WriteCostStressResult(CostStressResult result)
    {
        WriteSubHeader($"{result.StrategyFamily} / {result.PatternId ?? "-"} / {result.StrategyVariantId}");
        WriteField("Symbol/Timeframe", $"{result.Symbol}/{result.Timeframe}");
        WriteField("Survives Normal", result.SurvivesNormalCost.ToString().ToLowerInvariant());
        WriteField("Survives Spread x2", result.SurvivesSpreadX2.ToString().ToLowerInvariant());
        WriteField("Survives Spread x3", result.SurvivesSpreadX3.ToString().ToLowerInvariant());
        WriteField("Survives Stress", result.SurvivesStressCost.ToString().ToLowerInvariant());
        WriteField("Failure Reason", result.CostFailureReason);
        foreach (var scenario in result.ScenarioResults.Take(3))
        {
            WriteField($"Scenario {scenario.Scenario.Name}", $"pf={scenario.AdjustedProfitFactor:0.####},net_r={scenario.AdjustedNetR:0.####},score={scenario.SurvivalScore:0.####},survived={scenario.Survived.ToString().ToLowerInvariant()}");
        }
    }

    private void WriteRiskOfRuinEntry(RiskOfRuinEntry entry)
    {
        WriteSubHeader($"{entry.StrategyFamily} / {entry.PatternId ?? "-"} / {entry.StrategyVariantId}");
        WriteField("Symbol/Timeframe", $"{entry.Symbol}/{entry.Timeframe}");
        WriteField("Expected Drawdown", $"{entry.ExpectedDrawdown:0.####}%");
        WriteField("Losing Streak Risk", $"{entry.LosingStreakRisk:0.####}");
        WriteField("Ruin Probability", $"{entry.AccountRuinProbabilityEstimate:0.####}");
        WriteField("Recommended Risk", $"{entry.RecommendedMaxRiskPerTrade:0.####}%");
        WriteField("risk_of_ruin_passed", entry.RiskOfRuinPassed.ToString().ToLowerInvariant());
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
        WriteMessages("Trading.de Best Patterns", insights.BestTradingDePatterns ?? Array.Empty<string>());
        WriteMessages("Source Performance", insights.SourcePerformance ?? Array.Empty<string>());
        WriteMessages("Robust Strategies", insights.RobustStrategies ?? Array.Empty<string>());
        WriteMessages("Overfit Suspected", insights.OverfitSuspectedStrategies ?? Array.Empty<string>());
        WriteMessages("High Risk Strategies", insights.HighRiskStrategies ?? Array.Empty<string>());
        WriteMessages("Stable Symbol/Timeframe", insights.StableSymbolTimeframeCombinations ?? Array.Empty<string>());
        WriteMessages("Best Regimes", insights.BestRegimes ?? Array.Empty<string>());
        WriteMessages("Weak Regimes", insights.WeakRegimes ?? Array.Empty<string>());
        WriteMessages("Preferred Sessions", insights.PreferredSessions ?? Array.Empty<string>());
        WriteMessages("Avoid Sessions", insights.AvoidSessions ?? Array.Empty<string>());
        WriteMessages("Volatility Preference", insights.VolatilityPreference ?? Array.Empty<string>());
        WriteField("Regime Consistency", insights.RegimeConsistencyScore is null ? "-" : $"{insights.RegimeConsistencyScore:0.####}");
        WriteMessages("Preferred Regimes", insights.PreferredRegimes ?? Array.Empty<string>());
        WriteMessages("Avoided Regimes", insights.AvoidedRegimes ?? Array.Empty<string>());
        WriteMessages("Too Good To Be True", insights.TooGoodToBeTrueStrategies ?? Array.Empty<string>());
        WriteMessages("Cost Sensitive", insights.CostSensitiveStrategies ?? Array.Empty<string>());
        WriteMessages("Cost Sensitivity Summary", insights.CostSensitivitySummary ?? Array.Empty<string>());
        WriteMessages("Robust Gate Summary", insights.RobustGateSummary ?? Array.Empty<string>());
        WriteField("bot_candidates", insights.BotCandidateCount?.ToString() ?? "-");
        WriteField("rejected_candidates", insights.RejectedCandidateCount?.ToString() ?? "-");
        WriteMessages("Top Demo Bot Candidates", insights.TopDemoBotCandidates ?? Array.Empty<string>());
        WriteMessages("Next Validation Recommendations", insights.NextValidationRecommendations ?? Array.Empty<string>());
        WriteMessages("Monte-Carlo Summary", insights.MonteCarloSummary ?? Array.Empty<string>());
        WriteMessages("Cost Stress Summary", insights.CostStressSummary ?? Array.Empty<string>());
        WriteMessages("Risk-of-Ruin Summary", insights.RiskOfRuinSummary ?? Array.Empty<string>());
        WriteField("blocked_by_monte_carlo", insights.CandidatesBlockedByMonteCarlo?.ToString() ?? "-");
        WriteField("blocked_by_cost_stress", insights.CandidatesBlockedByCostStress?.ToString() ?? "-");
        WriteField("blocked_by_risk", insights.CandidatesBlockedByRisk?.ToString() ?? "-");
        WriteMessages("Why No Candidates", insights.WhyNoCandidates ?? Array.Empty<string>());
        WriteMessages("Top Blockers", insights.TopBlockers ?? Array.Empty<string>());
        WriteField("near_miss_count", insights.NearMissCount?.ToString() ?? "-");
        WriteMessages("Recommended Next Experiments", insights.RecommendedNextExperiments ?? Array.Empty<string>());
        WriteMessages("Avoid Combinations", insights.AvoidCombinations ?? Array.Empty<string>());
        WriteMessages("Next Recommended Tests", insights.NextRecommendedTests ?? Array.Empty<string>());
        WriteMessages("Parameter Statistics", insights.ParameterStatistics);
        WriteMessages("Timeframe Comparisons", insights.TimeframeComparisons);
        WriteField("no_auto_trading", insights.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", insights.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteRegimeSummary(RegimeSummaryReport report)
    {
        WriteField("Generated UTC", report.GeneratedAtUtc.ToString("O"));
        WriteField("Source Features", DisplayPath(report.SourceFeatureFile));
        WriteField("Features Analyzed", report.FeaturesAnalyzed.ToString());
        WriteField("Snapshots", report.SnapshotCount.ToString());
        WriteMessages("Symbols", report.Symbols);
        WriteMessages("Timeframes", report.Timeframes);
        WriteMessages("Dominant Regimes", report.DominantRegimes);
        WriteMessages("Dominant Sessions", report.DominantSessions);
        foreach (var snapshot in report.TopSnapshots.Take(8))
        {
            WriteSubHeader($"{snapshot.Symbol} {snapshot.Timeframe} / {snapshot.RegimeType} / {snapshot.Session}");
            WriteField("Candles", snapshot.CandleCount.ToString());
            WriteField("Range Ratio", $"{snapshot.AverageRangeRatio:0.########}");
            WriteField("Body Ratio", $"{snapshot.AverageBodyRatio:0.####}");
            WriteField("Trend Slope", $"{snapshot.TrendSlope:0.########}");
            WriteField("Breakout Frequency", $"{snapshot.BreakoutFrequency:0.####}");
            WriteField("Confidence", $"{snapshot.Confidence:0.####}");
        }

        WriteMessages("Warnings", report.Warnings);
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteStrategyRegimeEntry(StrategyRegimePerformanceEntry entry)
    {
        WriteSubHeader($"{entry.StrategyFamily} / {entry.PatternName} / {entry.RegimeType} / {entry.Session}");
        WriteField("Pattern ID", entry.PatternId);
        WriteField("Variants", entry.VariantCount.ToString());
        WriteField("Trades", entry.TotalTrades.ToString());
        WriteField("Avg Fitness", $"{entry.AverageFitness:0.####}");
        WriteField("Avg Winrate", $"{entry.AverageWinrate:P2}");
        WriteField("Regime Confidence", $"{entry.AverageRegimeConfidence:0.####}");
        WriteField("Regime Fit", $"{entry.RegimeFitScore:0.####}");
        WriteField("Status", entry.Status);
    }

    private void WriteWalkForwardSummary(WalkForwardValidationReport report)
    {
        WriteField("Report ID", report.ReportId);
        WriteField("Created UTC", report.CreatedAtUtc.ToString("O"));
        WriteField("Train Range", $"{report.TrainFromUtc:yyyy-MM-dd} -> {report.TrainToUtc:yyyy-MM-dd}");
        WriteField("Validation Range", $"{report.ValidationFromUtc:yyyy-MM-dd} -> {report.ValidationToUtc:yyyy-MM-dd}");
        WriteField("Strategies Evaluated", report.StrategiesEvaluated.ToString());
        WriteField("Robust Strategies", report.RobustStrategies.ToString());
        WriteField("Overfit Suspected", report.OverfitSuspectedStrategies.ToString());
        WriteField("High Risk Strategies", report.HighRiskStrategies.ToString());
        WriteField("OOS Available", report.Assessments.Count(item => item.OosAvailable).ToString());
        WriteField("Too Good To Be True", report.Assessments.Count(item => item.TooGoodToBeTrue).ToString());
        WriteField("Avg WalkForward Confidence", $"{(report.Assessments.Count == 0 ? 0 : report.Assessments.Average(item => item.WalkForwardConfidence)):0.####}");
        WriteField("Avg Cost Sensitivity", $"{(report.Assessments.Count == 0 ? 0 : report.Assessments.Average(item => item.CostSensitivity)):0.####}");
        WriteField("Avg Regime Consistency", $"{(report.Assessments.Count == 0 ? 0 : report.Assessments.Average(item => item.RegimeConsistencyScore)):0.####}");
        WriteField("no_auto_trading", report.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", report.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private NightlyResearchState WithCognitiveState(
        NightlyResearchState state,
        bool cognitiveJobsEnabled,
        NightlyCognitiveSummary? summary,
        string summaryPath) =>
        state with
        {
            CognitiveJobsEnabled = cognitiveJobsEnabled,
            LastKnowledgeScanUtc = summary?.LastKnowledgeScanUtc,
            LastQueueProcessedUtc = summary?.LastQueueProcessedUtc,
            LastCognitiveInsightsUtc = summary?.LastCognitiveInsightsUtc,
            QueuedResearchItems = summary?.QueuedResearchItems,
            ActiveDomains = summary?.ActiveDomains ?? ["trading"],
            LastCognitiveError = summary?.LastError,
            LastCognitiveSummaryPath = summaryPath
        };

    private void WriteCognitiveOperationalStatus(StoragePaths storagePaths, SchedulerStatus? schedulerStatus = null)
    {
        schedulerStatus ??= new HermesInternalScheduler(storagePaths, Path.Combine(_runtimeRoot, "config", "schedules.json")).GetStatus();
        var summaryService = new CognitiveNightlyService(storagePaths);
        var summary = summaryService.LoadSummary();
        var sources = new KnowledgeSourceRegistry(storagePaths).LoadSources();
        var insights = new HypothesisGenerator(storagePaths).LoadInsights();
        var cognitiveStatus = new CognitiveCoreService(storagePaths).BuildStatus();
        var queuedResearchItems = summary?.QueuedResearchItems;

        if (queuedResearchItems is null)
        {
            var queue = new ResearchQueueService(storagePaths).LoadOrCreateQueue();
            queuedResearchItems = queue.Items.Count(item => item.Status.Equals("open", StringComparison.OrdinalIgnoreCase));
        }

        var lastKnowledgeScan = summary?.LastKnowledgeScanUtc
            ?? sources.OrderByDescending(source => source.LastCheckedUtc).FirstOrDefault()?.LastCheckedUtc;
        var lastCognitiveInsights = summary?.LastCognitiveInsightsUtc
            ?? insights.OrderByDescending(insight => insight.CreatedAtUtc).FirstOrDefault()?.CreatedAtUtc;

        WriteSubHeader("Cognitive Core");
        WriteField("cognitive_jobs_enabled", AreCognitiveJobsEnabled(schedulerStatus).ToString().ToLowerInvariant());
        WriteField("last_knowledge_scan", lastKnowledgeScan?.ToString("O") ?? "-");
        WriteField("last_queue_processed", summary?.LastQueueProcessedUtc?.ToString("O") ?? "-");
        WriteField("last_cognitive_insights", lastCognitiveInsights?.ToString("O") ?? "-");
        WriteField("queued_research_items", queuedResearchItems?.ToString() ?? "-");
        var activeDomains = (summary?.ActiveDomains ?? [])
            .Concat(cognitiveStatus.ActiveDomains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        WriteField("active_domains", string.Join(", ", activeDomains));
        WriteField("last_cognitive_error", string.IsNullOrWhiteSpace(summary?.LastError) ? "-" : summary.LastError);
        WriteField("cognitive_summary", File.Exists(summaryService.SummaryPath) ? DisplayPath(summaryService.SummaryPath) : "-");
    }

    private static bool AreCognitiveJobsEnabled(SchedulerStatus schedulerStatus)
    {
        string[] required =
        [
            "scan_knowledge_sources",
            "process_research_queue",
            "generate_cognitive_insights"
        ];

        return required.All(jobType => schedulerStatus.Jobs.Any(job =>
            job.Enabled
            && job.JobType.Equals(jobType, StringComparison.OrdinalIgnoreCase)));
    }

    private void WriteNightlyState(NightlyResearchState state)
    {
        var currentlyRunning = state.CurrentlyRunning && IsProcessAlive(state.ProcessId);
        var duration = currentlyRunning && state.LastStartUtc is not null
            ? Math.Round((DateTimeOffset.UtcNow - state.LastStartUtc.Value).TotalMinutes, 2)
            : state.RuntimeDurationMinutes;

        WriteField("Status", state.Status);
        WriteField("Run ID", string.IsNullOrWhiteSpace(state.RunId) ? "-" : state.RunId);
        WriteField("Next Scheduled Start", state.NextScheduledStartUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-");
        WriteField("Last Start", state.LastStartUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-");
        WriteField("Last Stop", state.LastStopUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-");
        WriteField("Currently Running", currentlyRunning.ToString().ToLowerInvariant());
        WriteField("Process ID", state.ProcessId?.ToString() ?? "-");
        WriteField("Runtime Duration", $"{duration:0.##} min");
        WriteField("Started UTC", state.StartedAtUtc?.ToString("O") ?? "-");
        WriteField("Deadline UTC", state.DeadlineUtc?.ToString("O") ?? "-");
        WriteField("Iterations", state.IterationsCompleted.ToString());
        WriteField("Idle Iterations", state.IdleIterations.ToString());
        WriteField("Work Performed", state.WorkPerformed.ToString());
        WriteField("Next Action", state.NextAction);
        WriteField("Last Checkpoint", DisplayOptionalPath(state.LastCheckpointPath));
        WriteField("Stop Requested UTC", state.StopRequestedAtUtc?.ToString("O") ?? "-");
        WriteField("Last Error", state.LastError ?? "-");
        WriteField("cognitive_jobs_enabled", state.CognitiveJobsEnabled?.ToString().ToLowerInvariant() ?? "-");
        WriteField("last_knowledge_scan", state.LastKnowledgeScanUtc?.ToString("O") ?? "-");
        WriteField("last_queue_processed", state.LastQueueProcessedUtc?.ToString("O") ?? "-");
        WriteField("last_cognitive_insights", state.LastCognitiveInsightsUtc?.ToString("O") ?? "-");
        WriteField("queued_research_items", state.QueuedResearchItems?.ToString() ?? "-");
        WriteField("active_domains", state.ActiveDomains is { Count: > 0 } ? string.Join(", ", state.ActiveDomains) : "-");
        WriteField("last_cognitive_error", string.IsNullOrWhiteSpace(state.LastCognitiveError) ? "-" : state.LastCognitiveError);
        WriteField("cognitive_summary", DisplayOptionalPath(state.LastCognitiveSummaryPath));
        WriteField("no_auto_trading", state.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", state.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteSupervisorState(HermesSupervisorState state)
    {
        var currentlyRunning = state.CurrentlyRunning && IsProcessAlive(state.ProcessId);
        var duration = currentlyRunning && state.StartedAtUtc is not null
            ? Math.Round((DateTimeOffset.UtcNow - state.StartedAtUtc.Value).TotalMinutes, 2)
            : state.StartedAtUtc is not null && state.StoppedAtUtc is not null
                ? Math.Round((state.StoppedAtUtc.Value - state.StartedAtUtc.Value).TotalMinutes, 2)
                : 0;

        WriteField("Status", state.Status);
        WriteField("Supervisor ID", string.IsNullOrWhiteSpace(state.SupervisorId) ? "-" : state.SupervisorId);
        WriteField("Currently Running", currentlyRunning.ToString().ToLowerInvariant());
        WriteField("Process ID", state.ProcessId?.ToString() ?? "-");
        WriteField("Started UTC", state.StartedAtUtc?.ToString("O") ?? "-");
        WriteField("Deadline UTC", state.DeadlineUtc?.ToString("O") ?? "-");
        WriteField("Stopped UTC", state.StoppedAtUtc?.ToString("O") ?? "-");
        WriteField("Runtime Duration", $"{duration:0.##} min");
        WriteField("Heartbeat UTC", state.HeartbeatUtc?.ToString("O") ?? "-");
        WriteField("Iterations", state.IterationsCompleted.ToString());
        WriteField("Jobs Started", state.JobsStarted.ToString());
        WriteField("Jobs Completed", state.JobsCompleted.ToString());
        WriteField("Jobs Skipped", state.JobsSkipped.ToString());
        WriteField("Current Job", state.CurrentJobId ?? "-");
        WriteField("Last Job", state.LastJobId ?? "-");
        WriteField("Next Action", state.NextAction);
        WriteField("Stop Requested UTC", state.StopRequestedAtUtc?.ToString("O") ?? "-");
        WriteField("Last Error", state.LastError ?? "-");
        WriteField("no_auto_trading", state.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", state.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteSupervisorProcessStatus(SupervisorProcessStatus status)
    {
        WriteField("Running", status.Running.ToString().ToLowerInvariant());
        WriteField("PID", status.Pid?.ToString() ?? "-");
        WriteField("PID File", DisplayPath(status.PidPath));
        WriteField("Stale PID", status.StalePid.ToString().ToLowerInvariant());
        WriteField("Started At", status.StartedAtUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-");
        WriteField("Heartbeat Age", status.HeartbeatAgeSeconds is null ? "-" : $"{status.HeartbeatAgeSeconds:0.#} s");
        WriteField("Log", DisplayPath(status.LogPath));
        WriteField("Process Warning", status.Warning ?? "-");
    }

    private void WriteSchedulerJob(ScheduledJobState job)
    {
        WriteSubHeader($"{job.JobId} / {job.JobType}");
        WriteField("Enabled", job.Enabled.ToString().ToLowerInvariant());
        WriteField("Status", job.Status);
        WriteField("Next Run", job.NextRunUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-");
        WriteField("Last Run", job.LastRunUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-");
        WriteField("Last Completed", job.LastCompletedUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz") ?? "-");
        WriteField("Currently Running", job.CurrentlyRunning.ToString().ToLowerInvariant());
        WriteField("Run Count", job.RunCount.ToString());
        WriteField("Failures", job.FailureCount.ToString());
        WriteField("Last Action", job.LastAction ?? "-");
        WriteField("Last Report", DisplayOptionalPath(job.LastReportPath));
        WriteField("Skipped Reason", job.LastSkippedReason ?? "-");
        WriteField("Last Error", job.LastError ?? "-");
        WriteMessages("Warnings", job.Warnings);
    }

    private void WriteKnowledgeCatalogItem(KnowledgeCatalogItem item)
    {
        WriteSubHeader($"{item.Title} / {item.Id}");
        WriteField("Domain", item.Domain);
        WriteField("Confidence", $"{item.Confidence:0.####}");
        WriteField("Validation", item.ValidationStatus);
        WriteField("Last Validated", item.LastValidatedUtc?.ToString("O") ?? "-");
        WriteMessages("Sources", item.SourceIds);
        WriteMessages("Tags", item.Tags);
        WriteMessages("Related", item.RelatedItems.Take(8).ToList());
        WriteField("Summary", item.DescriptionShort);
    }

    private void WriteCognitiveDomainStatusEntry(DomainStatusEntry entry)
    {
        WriteSubHeader($"{entry.Domain} / {(entry.Active ? "active" : "inactive")}");
        WriteField("Last Scan", entry.LastScannedAtUtc?.ToString("O") ?? "-");
        WriteField("Sources", entry.SourceCount.ToString());
        WriteField("Knowledge Items", entry.KnowledgeItemCount.ToString());
        WriteField("Open Needs", entry.OpenNeeds.ToString());
        WriteField("Open Queue", entry.OpenQueueItems.ToString());
        WriteMessages("Next Tasks", entry.NextRecommendedTasks);
        WriteMessages("Warnings", entry.Warnings);
    }

    private void WriteCognitiveDomainInsight(DomainInsight insight)
    {
        WriteSubHeader($"{insight.Severity} / {insight.Domain} / {insight.InsightId}");
        WriteField("Title", insight.Title);
        WriteField("Summary", insight.Summary);
        WriteMessages("Evidence", insight.EvidenceRefs.Select(DisplayPath).ToList());
        WriteMessages("Recommended Tasks", insight.RecommendedTasks);
    }

    private void WriteDomainScanResult(DomainScanResult result)
    {
        WriteField("Domain", result.Domain);
        WriteField("Scanned UTC", result.ScannedAtUtc.ToString("O"));
        WriteField("Sources Scanned", result.SourcesScanned.ToString());
        WriteField("Knowledge Items", result.KnowledgeItems.ToString());
        WriteMessages("Output Paths", result.OutputPaths.Select(DisplayPath).ToList());
        WriteMessages("Warnings", result.Warnings);
        WriteField("no_trading_execution", result.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("no_broker_action", result.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("no_auto_trading", result.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", result.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteResearchQueueItem(ResearchQueueItem item)
    {
        WriteSubHeader($"{item.QueueItemId} / {item.Domain} / {item.Type}");
        WriteField("Queue", item.Queue);
        WriteField("Priority", item.Priority.ToString());
        WriteField("Status", item.Status);
        WriteField("Requested By", item.RequestedBy);
        WriteField("Created UTC", item.CreatedAtUtc.ToString("O"));
        WriteField("Updated UTC", item.UpdatedAtUtc?.ToString("O") ?? "-");
        WriteMessages("Source Refs", item.SourceRefs);
        WriteMessages("Notes", item.Notes);
    }

    private void WriteDetectedNeed(DetectedNeed need)
    {
        WriteSubHeader($"{need.Severity} / {need.Category} / {need.NeedId}");
        WriteField("Domain", need.Domain);
        WriteField("Title", need.Title);
        WriteField("Description", need.Description);
        WriteMessages("Evidence", need.EvidenceRefs);
        WriteMessages("Suggested Tasks", need.SuggestedTaskTypes);
        WriteField("no_trading_execution", need.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("human_review_required", need.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteHermesGoal(HermesGoal goal)
    {
        WriteSubHeader($"{goal.Priority:00} / {goal.GoalId}");
        WriteField("Active", goal.Active.ToString().ToLowerInvariant());
        WriteField("Progress", $"{goal.ProgressScore:0.####}");
        WriteField("Description", goal.Description);
        WriteMessages("Blockers", goal.Blockers);
        WriteMessages("Next Actions", goal.NextActions);
    }

    private void WritePlannedTask(PlannedTask task)
    {
        WriteSubHeader($"{task.TaskType} / {task.TaskId}");
        WriteField("Domain", task.Domain);
        WriteField("Goal", task.GoalId);
        WriteField("Need", task.NeedId);
        WriteField("Queue", task.QueueType);
        WriteField("Status", task.Status);
        WriteField("Priority", $"{task.Priority.TotalScore:0.####}");
        WriteField("Score Detail", $"impact={task.Priority.Impact:0.##}, urgency={task.Priority.Urgency:0.##}, confidence={task.Priority.Confidence:0.##}, cost={task.Priority.Cost:0.##}, risk={task.Priority.Risk:0.##}, learning={task.Priority.ExpectedLearningValue:0.##}");
        WriteField("Reason", task.Reason);
        WriteField("Expected Outcome", task.ExpectedOutcome);
        WriteMessages("Source Refs", task.SourceRefs);
        WriteField("no_trading_execution", task.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("human_review_required", task.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WritePlannedTaskExecutionResult(PlannedTaskExecutionResult result)
    {
        WriteSubHeader($"{result.Status} / {result.TaskType} / {result.TaskId}");
        WriteField("Need", result.NeedId);
        WriteField("Goal", result.GoalId);
        WriteField("Started UTC", result.StartedAtUtc.ToString("O"));
        WriteField("Completed UTC", result.CompletedAtUtc?.ToString("O") ?? "-");
        WriteField("Reason", result.Reason);
        WriteField("Skipped Reason", result.SkippedReason ?? "-");
        WriteMessages("Output Paths", result.OutputPaths.Select(DisplayPath).ToList());
        WriteMessages("Warnings", result.Warnings);
        WriteField("no_trading_execution", result.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("no_broker_action", result.NoBrokerAction.ToString().ToLowerInvariant());
        WriteField("no_auto_trading", result.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", result.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteTaskOutcome(TaskOutcomeResult outcome)
    {
        WriteSubHeader($"{outcome.Recommendation} / {outcome.TaskType} / {outcome.TaskId}");
        WriteField("Outcome ID", outcome.OutcomeId);
        WriteField("Need", outcome.NeedId);
        WriteField("Goal", outcome.GoalId);
        WriteField("Executed UTC", outcome.ExecutedAtUtc.ToString("O"));
        WriteField("Evaluated UTC", outcome.EvaluatedAtUtc.ToString("O"));
        WriteField("Usefulness", $"{outcome.OutcomeScore.UsefulnessScore:0.####}");
        WriteField("Learning Value", $"{outcome.OutcomeScore.LearningValue:0.####}");
        WriteField("Cost", $"{outcome.OutcomeScore.CostScore:0.####}");
        WriteField("Risk", $"{outcome.OutcomeScore.RiskScore:0.####}");
        WriteField("Redundancy", $"{outcome.OutcomeScore.RedundancyScore:0.####}");
        WriteField("Need Reduced", outcome.Evidence.NeedReduced.ToString().ToLowerInvariant());
        WriteField("Goal Improved", outcome.Evidence.GoalImproved.ToString().ToLowerInvariant());
        WriteField("Queue Changed", outcome.Evidence.ResearchQueueChanged.ToString().ToLowerInvariant());
        WriteMessages("Evidence", outcome.Evidence.Notes);
        WriteMessages("Followups", outcome.FollowupTaskIds);
        WriteField("no_trading_execution", outcome.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("human_review_required", outcome.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WritePlannerTaskTypeFeedback(PlannerTaskTypeFeedback item)
    {
        WriteSubHeader($"{item.Recommendation} / {item.TaskType}");
        WriteField("Evaluations", item.Evaluations.ToString());
        WriteField("Avg Usefulness", $"{item.AverageUsefulnessScore:0.####}");
        WriteField("Avg Learning", $"{item.AverageLearningValue:0.####}");
        WriteField("Avg Cost", $"{item.AverageCostScore:0.####}");
        WriteField("Avg Risk", $"{item.AverageRiskScore:0.####}");
        WriteField("Avg Redundancy", $"{item.AverageRedundancyScore:0.####}");
        WriteField("Priority Adjustment", $"{item.PriorityAdjustment:0.####}");
        WriteField("Last Evaluated", item.LastEvaluatedUtc.ToString("O"));
        WriteMessages("Repeated Unsuccessful Needs", item.RepeatedUnsuccessfulNeeds);
    }

    private void WriteGoalFeedbackEntry(GoalFeedbackEntry item)
    {
        WriteSubHeader($"{item.GoalId}");
        WriteField("Evaluations", item.Evaluations.ToString());
        WriteField("Avg Usefulness", $"{item.AverageUsefulnessScore:0.####}");
        WriteField("Progress Delta", $"{item.ProgressDelta:0.####}");
        WriteField("Last Evaluated", item.LastEvaluatedUtc.ToString("O"));
        WriteMessages("Improved Needs", item.ImprovedNeeds);
        WriteMessages("Persistent Needs", item.PersistentNeeds);
        WriteMessages("Recommended Actions", item.RecommendedActions);
    }

    private void WriteAutonomousLoopSummary(AutonomousLoopSummary summary)
    {
        WriteField("Status", summary.Status);
        WriteField("Run ID", summary.RunId);
        WriteField("Requested Iterations", summary.RequestedIterations.ToString());
        WriteField("Max Minutes", $"{summary.MaxMinutes:0.###}");
        WriteField("Iterations", summary.IterationsCompleted.ToString());
        WriteField("Idle Iterations", summary.IdleIterations.ToString());
        WriteField("Work Performed", summary.WorkPerformed.ToString());
        WriteField("Average Learning", $"{summary.AverageLearningValue:0.####}");
        WriteField("Next Action", summary.NextAction);
        WriteField("Stop Reason", summary.StopReason ?? "-");
        WriteMessages("Warnings", summary.Warnings);
        if (summary.LastIteration is not null)
        {
            WriteAutonomousLoopIteration(summary.LastIteration);
        }
    }

    private void WriteAutonomousLoopIteration(AutonomousLoopIterationSummary iteration)
    {
        WriteSubHeader($"{iteration.Status} / iteration {iteration.IterationNumber}");
        WriteField("Iteration ID", iteration.IterationId);
        WriteField("Started UTC", iteration.StartedAtUtc.ToString("O"));
        WriteField("Completed UTC", iteration.CompletedAtUtc.ToString("O"));
        WriteField("Resource Action", iteration.ResourceAction);
        WriteField("Cleanup Candidates", iteration.CleanupCandidates.ToString());
        WriteField("Needs", iteration.NeedsDetected.ToString());
        WriteField("Planned Tasks", iteration.TasksPlanned.ToString());
        WriteField("Executed Tasks", iteration.TasksExecuted.ToString());
        WriteField("Completed/Skipped/Failed", $"{iteration.TasksCompleted}/{iteration.TasksSkipped}/{iteration.TasksFailed}");
        WriteField("Outcomes Evaluated", iteration.OutcomesEvaluated.ToString());
        WriteField("Avg Usefulness", $"{iteration.AverageOutcomeUsefulness:0.####}");
        WriteField("Avg Learning", $"{iteration.AverageOutcomeLearningValue:0.####}");
        WriteField("Cognitive Insights", iteration.CognitiveInsights.ToString());
        WriteField("Work Performed", iteration.WorkPerformed.ToString().ToLowerInvariant());
        WriteField("Idle", iteration.Idle.ToString().ToLowerInvariant());
        WriteField("Next Action", iteration.NextAction);
        WriteField("Stop Reason", iteration.StopReason ?? "-");
        WriteField("Checkpoint", DisplayOptionalPath(iteration.CheckpointPath));
        WriteMessages("Feedback Changes", iteration.FeedbackChanges);
        WriteMessages("Warnings", iteration.Warnings);
        WriteMessages("Resource Warnings", iteration.ResourceWarnings);
        WriteField("no_trading_execution", iteration.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("human_review_required", iteration.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteMetaReview(MetaReviewResult review)
    {
        WriteField("Status", review.Status);
        WriteField("Updated UTC", review.UpdatedAtUtc.ToString("O"));
        WriteField("Goals Reviewed", review.GoalsReviewed.ToString());
        WriteField("Outcomes Reviewed", review.OutcomesReviewed.ToString());
        WriteField("Planner Task Types", review.PlannerTaskTypesReviewed.ToString());
        WriteField("Knowledge Items", review.KnowledgeItems.ToString());
        WriteField("Research Queue Items", review.ResearchQueueItems.ToString());
        WriteField("Learning Strategy", review.LearningStrategy.CurrentStrategy);
        WriteMessages("Activities With Progress", review.ActivitiesWithProgress);
        WriteMessages("Activities Generating Work", review.ActivitiesGeneratingWork);
        WriteMessages("Stagnant Goals", review.StagnantGoals);
        WriteMessages("Recurring Needs", review.RecurringNeeds);
        foreach (var observation in review.Observations.Take(12))
        {
            WriteMetaObservation(observation);
        }
        WriteField("no_auto_trading", review.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", review.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteMetaObservation(MetaObservation observation)
    {
        WriteSubHeader($"{observation.Severity} / {observation.Category} / {observation.Title}");
        WriteField("Observation", observation.ObservationId);
        WriteField("Summary", observation.Summary);
        WriteMessages("Evidence", observation.EvidenceRefs);
        WriteMessages("Recommended Actions", observation.RecommendedActions);
    }

    private void WriteDomainHealth(DomainHealth health)
    {
        WriteSubHeader($"{health.Domain} / {health.Score.Classification}");
        WriteField("Active", health.Active.ToString().ToLowerInvariant());
        WriteField("Sources", health.SourceCount.ToString());
        WriteField("Knowledge Items", health.KnowledgeItemCount.ToString());
        WriteField("Queue Items", health.QueueItems.ToString());
        WriteField("Open Queue", health.OpenQueueItems.ToString());
        WriteField("Processed Queue", health.ProcessedQueueItems.ToString());
        WriteField("Outcomes", health.OutcomeCount.ToString());
        WriteField("Overall", $"{health.Score.OverallScore:0.####}");
        WriteField("Knowledge", $"{health.Score.KnowledgeCoverage:0.####}");
        WriteField("Validation", $"{health.Score.ValidationCoverage:0.####}");
        WriteField("Trust", $"{health.Score.TrustScore:0.####}");
        WriteField("Redundancy", $"{health.Score.RedundancyScore:0.####}");
        WriteField("Learning Velocity", $"{health.Score.LearningVelocity:0.####}");
        WriteMessages("Reasons", health.Score.Reasons);
        WriteMessages("Warnings", health.Warnings);
    }

    private void WriteLearningStrategy(LearningStrategy strategy)
    {
        WriteField("Strategy", strategy.CurrentStrategy);
        WriteField("Updated UTC", strategy.UpdatedAtUtc.ToString("O"));
        WriteField("Reason", strategy.Reason);
        WriteField("Expected Effect", strategy.ExpectedEffect);
        WriteMessages("Priority Task Types", strategy.PriorityTaskTypes);
        WriteMessages("Deprioritized Task Types", strategy.DeprioritizedTaskTypes);
        WriteMessages("Domain Focus", strategy.DomainFocus);
        WriteField("no_auto_trading", strategy.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", strategy.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteGovernanceDecision(GovernanceDecision decision)
    {
        WriteSubHeader($"{decision.Status} / {decision.RuleId}");
        WriteField("Reason", decision.Reason);
        WriteField("Action", decision.Action);
        WriteMessages("Evidence", decision.EvidenceRefs);
    }

    private void WriteCognitiveHypothesis(CognitiveHypothesis hypothesis)
    {
        WriteSubHeader($"{hypothesis.Title} / {hypothesis.HypothesisId}");
        WriteField("Domain", hypothesis.Domain);
        WriteField("Status", hypothesis.Status);
        WriteField("Trust", $"{hypothesis.Trust.Value:0.####} / {hypothesis.Trust.Classification}");
        WriteField("Evidence", $"{hypothesis.Evidence.Value:0.####} / {hypothesis.Evidence.Classification}");
        WriteField("Validation", hypothesis.ProposedValidation);
        WriteMessages("Source Items", hypothesis.SourceItemIds);
    }

    private void WriteCognitiveInsight(CognitiveInsight insight)
    {
        WriteSubHeader($"{insight.Title} / {insight.InsightId}");
        WriteField("Domain", insight.Domain);
        WriteField("Status", insight.Status);
        WriteField("Summary", insight.Summary);
        WriteMessages("Evidence", insight.EvidenceRefs);
        WriteMessages("Recommended Actions", insight.RecommendedActions);
        WriteField("no_trading_execution", insight.NoTradingExecution.ToString().ToLowerInvariant());
        WriteField("human_review_required", insight.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private static bool IsProcessAlive(int? processId)
    {
        return SupervisorProcessManager.IsProcessAlive(processId);
    }

    private void WriteResourceSnapshot(ResourceSnapshot snapshot)
    {
        WriteField("Timestamp UTC", snapshot.TimestampUtc.ToString("O"));
        WriteField("CPU", $"{snapshot.CpuUsagePercent:0.##}%");
        WriteField("RAM", $"{snapshot.MemoryUsagePercent:0.##}% ({snapshot.UsedMemoryMb}/{snapshot.TotalMemoryMb} MB)");
        WriteField("Free Disk", $"{snapshot.FreeDiskMb / 1024.0:0.##} GB ({snapshot.FreeDiskPercent:0.##}%)");
        WriteField("Storage Root", DisplayPath(snapshot.StorageRoot));
        WriteField("Action", snapshot.Action);
        WriteField("Should Pause", snapshot.ShouldPause.ToString().ToLowerInvariant());
        WriteField("Should Stop", snapshot.ShouldStop.ToString().ToLowerInvariant());
        WriteMessages("Warnings", snapshot.Warnings);
        WriteField("no_auto_trading", snapshot.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", snapshot.HumanReviewRequired.ToString().ToLowerInvariant());
    }

    private void WriteCleanupPlan(CleanupPlan plan, int limit)
    {
        WriteField("Plan ID", plan.PlanId);
        WriteField("Created UTC", plan.CreatedAtUtc.ToString("O"));
        WriteField("Candidates", plan.Candidates.Count.ToString());
        WriteField("Estimated Free", $"{plan.EstimatedBytesToFree / 1024.0 / 1024.0:0.##} MB");
        WriteField("Safe To Apply", plan.SafeToApply.ToString().ToLowerInvariant());
        foreach (var candidate in plan.Candidates.Take(limit))
        {
            WriteSubHeader(candidate.Reason);
            WriteField("Path", DisplayPath(candidate.Path));
            WriteField("Bytes", candidate.EstimatedBytes.ToString());
            WriteField("Safe", candidate.SafeToDelete.ToString().ToLowerInvariant());
        }

        WriteField("Protected Paths", plan.ProtectedPaths.Count.ToString());
        WriteField("no_auto_trading", plan.NoAutoTrading.ToString().ToLowerInvariant());
        WriteField("human_review_required", plan.HumanReviewRequired.ToString().ToLowerInvariant());
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
        string reportId,
        DateTimeOffset startedAtUtc,
        double requestedHours,
        DateTimeOffset targetFromUtc,
        DateTimeOffset targetToUtc,
        int downloadPlans,
        int downloadsAttempted,
        IReadOnlyList<AutopilotDownloadResult> downloads,
        int strategyVariantsTested,
        int strategyResearchEntries,
        string patternCatalogPath,
        string insightsPath,
        string status,
        double elapsedMinutes,
        int iterationsCompleted,
        int workPerformed,
        int idleIterations,
        string nextAction,
        IReadOnlyList<string> warnings)
    {
        var completedAtUtc = DateTimeOffset.UtcNow;
        var report = new ResearchAutopilotReport(
            ReportId: reportId,
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
            StrategyVariantsTested: strategyVariantsTested,
            StrategyResearchEntries: strategyResearchEntries,
            PatternCatalogPath: patternCatalogPath,
            InsightsPath: insightsPath,
            Status: warnings.Count == 0 ? status : $"{status}_with_warnings",
            Warnings: warnings.Distinct(StringComparer.Ordinal).Take(80).ToList(),
            NoAutoTrading: true,
            HumanReviewRequired: true,
            ElapsedMinutes: Math.Round(elapsedMinutes, 2),
            IterationsCompleted: iterationsCompleted,
            WorkPerformed: workPerformed,
            IdleIterations: idleIterations,
            NextAction: nextAction);

        var directory = Path.Combine(storagePaths.Root, "strategy_research", "autopilot");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"{report.ReportId}.autopilot_report.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(Path.Combine(directory, "latest_autopilot_report.json"), JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        return report;
    }

    private static bool HasFeatureExports(StoragePaths storagePaths)
    {
        var featuresRoot = Path.Combine(storagePaths.Root, "exports", "features");
        return Directory.Exists(featuresRoot)
            && Directory.EnumerateFiles(featuresRoot, "*.jsonl", SearchOption.AllDirectories).Any();
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

    private static double ReadDoubleOption(string[] args, string name, double fallback, double min, double max)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (args[index].Equals(name, StringComparison.OrdinalIgnoreCase)
                && double.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return Math.Clamp(value, min, max);
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

    private static string ShellQuote(string value)
    {
        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
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
            if (arg is "--root" or "--limit" or "--hours" or "--max-runtime-hours" or "--max-requests" or "--max-downloads" or "--sleep-seconds" or "--max-idle-iterations" or "--from" or "--to" or "--url")
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

    private sealed record AutopilotIterationResult(
        int Iteration,
        int DownloadPlans,
        int DownloadsAttempted,
        IReadOnlyList<AutopilotDownloadResult> Downloads,
        TradingLearningBetaReport? BetaReport,
        ResearchMemoryIndex Index,
        StrategyResearchStepResult StrategyResearch,
        int SimulationReports,
        WalkForwardValidationReport WalkForward,
        StrategyDiscoveryReport Discovery,
        IReadOnlyList<string> Warnings,
        bool WorkPerformed,
        int WorkUnits,
        string Status,
        string NextAction);

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

    private AutonomousLearningLoop BuildAutonomousLearningLoop() =>
        new(BuildStoragePaths(), Path.Combine(_runtimeRoot, "config", "autonomous.loop.json"));

    private StoragePaths BuildReadOnlyStoragePaths()
    {
        var profilePath = Path.Combine(_runtimeRoot, "config", "storage.profile.json");
        if (File.Exists(profilePath))
        {
            return StorageProfile.Load(profilePath).ToPaths(Path.GetDirectoryName(profilePath) ?? _runtimeRoot);
        }

        return BuildFallbackStoragePaths(_dataRoot);
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
        Console.WriteLine("Safety: keine Trading-Ausfuehrung, keine Broker-Orders, Supervisor nur per kontrolliertem Start/Stop-Request, no_auto_trading sichtbar, human_review_required sichtbar.");
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
