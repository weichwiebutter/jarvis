using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record ValidationBacklogDomainFinding(
    string Domain,
    int PendingCount,
    string Severity,
    string Cause,
    bool FrankRequired,
    bool HermesCanExecuteAutomatically,
    string RecommendedNextAction,
    string Priority);

public sealed record ValidationBacklogPlanItem(
    string Category,
    string Title,
    string Domain,
    int Count,
    string Severity,
    string Cause,
    bool FrankRequired,
    bool HermesCanExecuteAutomatically,
    string RecommendedNextAction,
    string Priority);

public sealed record ValidationBacklogAnalyzerReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ValidationBacklogDomainFinding> OpenValidationsByDomain,
    IReadOnlyList<ValidationBacklogPlanItem> AutoResolutionPlan,
    int SoftwareValidationPending,
    int ProcessValidationPending,
    int ResearchValidationPending,
    int DocumentationValidationPending,
    int ValidationPlansOpen,
    int RobustStrategies,
    int CleanupCandidates,
    string KnowledgeHealth,
    bool FrankRequired,
    string OperatorSummary,
    IReadOnlyList<string> Warnings,
    string ReportPath,
    string MarkdownPath,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class ValidationBacklogAnalyzerService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public ValidationBacklogAnalyzerService(StoragePaths storagePaths, string runtimeRoot)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot;
    }

    public string ReportDirectory => Path.Combine(_storagePaths.Root, "reports", "validation_backlog");

    public string ReportPath => Path.Combine(ReportDirectory, "validation_backlog_analyzer.json");

    public string MarkdownPath => Path.Combine(ReportDirectory, "validation_backlog_analyzer.md");

    public ValidationBacklogAnalyzerReport Build()
    {
        var now = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(ReportDirectory);

        var knowledgeValidation = new KnowledgeValidationStrategy(_storagePaths).LoadStatus()
            ?? new KnowledgeValidationStrategy(_storagePaths).BuildStatus();
        var domainValidation = new DomainKnowledgeValidationService(_storagePaths).BuildStatus();
        var quality = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        var storage = new StorageHygieneService(_storagePaths).LoadStatus()
            ?? new StorageHygieneService(_storagePaths).BuildStatus();
        var walkForward = new WalkForwardValidationService(_storagePaths).LoadReport();
        var warnings = new List<string>();

        var openValidationsByDomain = new List<ValidationBacklogDomainFinding>
        {
            BuildFinding("software", domainValidation.SoftwareValidationPending, "high",
                "Viele offene Software-Validierungen blockieren die Konsolidierung.",
                frankRequired: false, hermesCanExecuteAutomatically: true,
                "Hermes kann Validierungsläufe planen.", "high"),
            BuildFinding("process", domainValidation.ProcessValidationPending, "medium",
                "Prozesswissen ist noch nicht ausreichend validiert.",
                frankRequired: false, hermesCanExecuteAutomatically: true,
                "Hermes kann Prozess-Validierungen und Reviews planen.", "medium"),
            BuildFinding("research", domainValidation.ResearchValidationPending, "medium",
                "Mehr Research- und OOS-Absicherung wird benötigt.",
                frankRequired: false, hermesCanExecuteAutomatically: true,
                "Hermes kann Research- und OOS-Läufe planen.", "medium"),
            BuildFinding("documentation", domainValidation.DocumentationValidationPending, "low",
                "Dokumentationswissen benötigt noch Nachweise.",
                frankRequired: false, hermesCanExecuteAutomatically: true,
                "Hermes kann Quellen prüfen und Dokumentation validieren.", "low"),
        }
        .Where(item => item.PendingCount > 0)
        .ToList();

        if (knowledgeValidation.ValidationPlansOpen > 0)
        {
            openValidationsByDomain.Insert(0, new ValidationBacklogDomainFinding(
                "knowledge",
                knowledgeValidation.ValidationPlansOpen,
                knowledgeValidation.ValidationPlansOpen > 100 ? "high" : "medium",
                "Es liegen offene Validierungspläne vor, die weiter in Tasks übersetzt werden müssen.",
                false,
                true,
                "Hermes kann Validation Queues nachfüllen und Tasks ausführen.",
                knowledgeValidation.ValidationPlansOpen > 100 ? "high" : "medium"));
        }

        var cleanupCandidates = FirstPositive(storage.CleanupCandidates, storageStatusCandidate(storage));
        var robustStrategies = walkForward?.RobustStrategies ?? 0;

        var autoResolutionPlan = new List<ValidationBacklogPlanItem>();
        if (domainValidation.SoftwareValidationPending > 0)
        {
            autoResolutionPlan.Add(new ValidationBacklogPlanItem(
                "Nightly geeignet",
                "Software-Validierung planen",
                "software",
                domainValidation.SoftwareValidationPending,
                "high",
                "Software hat den größten Validierungsstau.",
                false,
                true,
                "Validierungsläufe planen und abarbeiten.",
                "high"));
        }

        if (domainValidation.ProcessValidationPending > 0)
        {
            autoResolutionPlan.Add(new ValidationBacklogPlanItem(
                "Im nächsten Lernfenster möglich",
                "Prozessvalidierung vorbereiten",
                "process",
                domainValidation.ProcessValidationPending,
                "medium",
                "Prozesswissen braucht noch Nachweise.",
                false,
                true,
                "Prozessbezogene Validierungen und Evidenzläufe vorbereiten.",
                "medium"));
        }

        if (domainValidation.ResearchValidationPending > 0)
        {
            autoResolutionPlan.Add(new ValidationBacklogPlanItem(
                "Nightly geeignet",
                "Research-Validierung planen",
                "research",
                domainValidation.ResearchValidationPending,
                "medium",
                "Research braucht zusätzliche OOS-Absicherung.",
                false,
                true,
                "Research- und OOS-Läufe planen.",
                "medium"));
        }

        if (domainValidation.DocumentationValidationPending > 0)
        {
            autoResolutionPlan.Add(new ValidationBacklogPlanItem(
                "Sofort automatisch möglich",
                "Dokumentationsquellen prüfen",
                "documentation",
                domainValidation.DocumentationValidationPending,
                "low",
                "Dokumentationswissen kann aus vorhandenen Quellen weiter geprüft werden.",
                false,
                true,
                "Quellen prüfen und Evidenz ergänzen.",
                "low"));
        }

        if (knowledgeValidation.ValidationPlansOpen > 0)
        {
            autoResolutionPlan.Add(new ValidationBacklogPlanItem(
                "Sofort automatisch möglich",
                "Validation Queue nachfüllen",
                "knowledge",
                knowledgeValidation.ValidationPlansOpen,
                "high",
                "Offene Validierungspläne sind noch nicht vollständig in Tasks übersetzt.",
                false,
                true,
                "Offene Pläne in Validation Tasks überführen.",
                "high"));
        }

        if (robustStrategies == 0)
        {
            autoResolutionPlan.Add(new ValidationBacklogPlanItem(
                "Im nächsten Lernfenster möglich",
                "Robustheits-Plan anstoßen",
                "research",
                1,
                "high",
                "Es gibt noch keine robuste Strategie.",
                false,
                true,
                "Research-/Robustness-Läufe planen.",
                "high"));
        }

        if (cleanupCandidates > 0)
        {
            autoResolutionPlan.Add(new ValidationBacklogPlanItem(
                "Nur Hinweis / Wartung",
                "Cleanup-Plan aktualisieren",
                "process",
                cleanupCandidates,
                cleanupCandidates > 50000 ? "medium" : "low",
                "Sehr viele Dateien wären aufräumbar; das ist Wartung, kein Blocker.",
                false,
                true,
                "Cleanup-Plan aktualisieren und bei Bedarf manuell ausführen.",
                cleanupCandidates > 50000 ? "medium" : "low"));
        }

        if (walkForward?.Assessments.Any(item => item.OosAvailable) == false)
        {
            autoResolutionPlan.Add(new ValidationBacklogPlanItem(
                "Blockiert durch fehlende Daten",
                "OOS-Absicherung aufbauen",
                "research",
                1,
                "high",
                "Out-of-Sample-/Walk-Forward-Daten fehlen für Teile der Wissensbasis.",
                false,
                true,
                "OOS-/Walk-Forward-Läufe planen.",
                "high"));
        }

        var frankRequired = openValidationsByDomain.Any(item => item.FrankRequired) || false;
        var operatorSummary = BuildOperatorSummary(openValidationsByDomain, autoResolutionPlan, frankRequired);
        var report = new ValidationBacklogAnalyzerReport(
            ReportVersion: "validation_backlog_analyzer_v1",
            UpdatedAtUtc: now,
            OpenValidationsByDomain: openValidationsByDomain,
            AutoResolutionPlan: autoResolutionPlan,
            SoftwareValidationPending: domainValidation.SoftwareValidationPending,
            ProcessValidationPending: domainValidation.ProcessValidationPending,
            ResearchValidationPending: domainValidation.ResearchValidationPending,
            DocumentationValidationPending: domainValidation.DocumentationValidationPending,
            ValidationPlansOpen: knowledgeValidation.ValidationPlansOpen,
            RobustStrategies: robustStrategies,
            CleanupCandidates: cleanupCandidates,
            KnowledgeHealth: quality.KnowledgeHealth,
            FrankRequired: frankRequired,
            OperatorSummary: operatorSummary,
            Warnings: warnings,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
        return report;
    }

    public ValidationBacklogAnalyzerReport? Load()
    {
        if (!File.Exists(ReportPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ValidationBacklogAnalyzerReport>(File.ReadAllText(ReportPath), JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private static ValidationBacklogDomainFinding BuildFinding(string domain, int count, string severity, string cause, bool frankRequired, bool hermesCanExecuteAutomatically, string nextAction, string priority) =>
        new(domain, count, severity, cause, frankRequired, hermesCanExecuteAutomatically, nextAction, priority);

    private static int storageStatusCandidate(StorageStatusSnapshot? status)
    {
        return status?.CleanupCandidates ?? 0;
    }

    private static int FirstPositive(params int[] values) => values.FirstOrDefault(value => value > 0);

    private static string BuildOperatorSummary(IReadOnlyList<ValidationBacklogDomainFinding> domains, IReadOnlyList<ValidationBacklogPlanItem> plan, bool frankRequired)
    {
        var software = domains.FirstOrDefault(item => item.Domain.Equals("software", StringComparison.OrdinalIgnoreCase))?.PendingCount ?? 0;
        var process = domains.FirstOrDefault(item => item.Domain.Equals("process", StringComparison.OrdinalIgnoreCase))?.PendingCount ?? 0;
        var research = domains.FirstOrDefault(item => item.Domain.Equals("research", StringComparison.OrdinalIgnoreCase))?.PendingCount ?? 0;
        var docs = domains.FirstOrDefault(item => item.Domain.Equals("documentation", StringComparison.OrdinalIgnoreCase))?.PendingCount ?? 0;
        var knowledge = domains.FirstOrDefault(item => item.Domain.Equals("knowledge", StringComparison.OrdinalIgnoreCase))?.PendingCount ?? 0;
        var lines = new List<string>
        {
            $"Hermes hat {software + process + research + docs + knowledge} Validierungsthemen analysiert.",
            $"Am meisten blockiert: Software ({software}), Research ({research}) und Prozesswissen ({process}).",
            frankRequired ? "Aktion für Frank: Ja, einzelne Prioritätsfälle prüfen." : "Aktion für Frank: Keine.",
            "Hermes kann die meisten Punkte selbst planen und schrittweise abbauen."
        };
        return string.Join(" ", lines);
    }

    private string BuildMarkdown(ValidationBacklogAnalyzerReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Validation Backlog Analyzer");
        sb.AppendLine();
        sb.AppendLine($"- Updated at: {report.UpdatedAtUtc:O}");
        sb.AppendLine($"- Knowledge health: {report.KnowledgeHealth}");
        sb.AppendLine($"- Frank required: {(report.FrankRequired ? "yes" : "no")}");
        sb.AppendLine();
        sb.AppendLine("## Open validations by domain");
        foreach (var item in report.OpenValidationsByDomain)
        {
            sb.AppendLine($"- {item.Domain}: {item.PendingCount} | {item.Severity} | {item.Cause} | next: {item.RecommendedNextAction}");
        }

        sb.AppendLine();
        sb.AppendLine("## Auto resolution plan");
        foreach (var item in report.AutoResolutionPlan)
        {
            sb.AppendLine($"- {item.Category}: {item.Title} ({item.Domain}) x{item.Count} | {item.RecommendedNextAction}");
        }

        sb.AppendLine();
        sb.AppendLine("## Operator summary");
        sb.AppendLine(report.OperatorSummary);
        sb.AppendLine();
        sb.AppendLine("Safety: keine Trading-Ausfuehrung, keine Broker-Orders, no_auto_trading=true, human_review_required=true, broker_orders_enabled=false, live_trading_enabled=false, research_only=true.");
        return sb.ToString();
    }
}
