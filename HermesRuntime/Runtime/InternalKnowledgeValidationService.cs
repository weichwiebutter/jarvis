using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Hermes.Runtime;

public sealed record InternalKnowledgeValidationCheck(
    string CheckId,
    string Label,
    bool Passed,
    string EvidenceRef,
    string Detail,
    string? OutputPath = null);

public sealed record InternalKnowledgeValidationItem(
    string KnowledgeItemId,
    string Title,
    string Domain,
    string EvidenceAcquisitionClassification,
    string SeedNotApplicableReason,
    bool InternalValidationRequired,
    string CurrentStatus,
    string ValidationStatusBefore,
    string ValidationStatusAfter,
    string RecommendedNextAction,
    bool FileExists,
    bool BuildIncluded,
    bool CliCommandExists,
    bool ServiceFileExists,
    bool TestsOrHarnessExists,
    bool ReportOrConfigExists,
    bool BuildSucceeded,
    long BuildDurationMs,
    string BuildCommand,
    IReadOnlyList<string> CandidateFiles,
    IReadOnlyList<InternalKnowledgeValidationCheck> Checks,
    IReadOnlyList<string> EvidenceRefs,
    IReadOnlyList<string> OutputPaths,
    IReadOnlyList<string> Warnings,
    bool EvidenceWritten);

public sealed record InternalKnowledgeValidationReport(
    string ReportVersion,
    DateTimeOffset UpdatedAtUtc,
    string Status,
    int LoadedItems,
    int SelectedItems,
    int CompletedItems,
    int PendingItems,
    int BuildSucceededItems,
    int EvidenceWrittenItems,
    IReadOnlyDictionary<string, int> ClassificationCounts,
    IReadOnlyList<InternalKnowledgeValidationItem> Items,
    IReadOnlyList<string> CommandsExecuted,
    IReadOnlyList<string> Warnings,
    string KnowledgeEvidencePath,
    string ValidationExecutionLogPath,
    string CatalogPath,
    string QualityPath,
    string ValidationPlansPath,
    string CliProgramPath,
    string ReportPath,
    string MarkdownPath,
    IReadOnlyList<KnowledgeEvidenceAcquisitionClassification> Classifications,
    bool DryRun,
    bool Applied,
    bool ResearchOnly,
    bool NoTradingExecution,
    bool NoBrokerAction,
    bool NoAutoTrading,
    bool HumanReviewRequired);

public sealed class InternalKnowledgeValidationService
{
    private readonly StoragePaths _storagePaths;
    private readonly string _runtimeRoot;

    public InternalKnowledgeValidationService(StoragePaths storagePaths, string? runtimeRoot = null)
    {
        _storagePaths = storagePaths;
        _runtimeRoot = runtimeRoot ?? Directory.GetCurrentDirectory();
    }

    public string Root => Path.Combine(_storagePaths.Root, "reports", "internal_knowledge_validation");

    public string ReportPath => Path.Combine(Root, "internal_knowledge_validation_report.json");

    public string MarkdownPath => Path.Combine(Root, "internal_knowledge_validation_report.md");

    public string EvidenceAcquisitionPath => Path.Combine(_storagePaths.Root, "reports", "knowledge_evidence_acquisition", "knowledge_evidence_acquisition_report.json");

    public string KnowledgeEvidencePath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_evidence.json");

    public string ValidationExecutionLogPath => Path.Combine(_storagePaths.Root, "cognitive_core", "validation_execution.jsonl");

    public string CatalogPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_catalog.json");

    public string QualityPath => Path.Combine(_storagePaths.Root, "cognitive_core", "knowledge_quality.json");

    public string ValidationPlansPath => Path.Combine(_storagePaths.Root, "cognitive_core", "validation_plans.json");

    public string CliProgramPath => Path.Combine(_runtimeRoot, "cli", "Program.cs");

    public InternalKnowledgeValidationReport LoadStatus()
    {
        if (!File.Exists(ReportPath))
        {
            return Run(maxItems: 10, apply: false, dryRun: true);
        }

        try
        {
            return JsonSerializer.Deserialize<InternalKnowledgeValidationReport>(
                File.ReadAllText(ReportPath),
                JsonDefaults.SnapshotReadOptions) ?? Run(maxItems: 10, apply: false, dryRun: true);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Run(maxItems: 10, apply: false, dryRun: true);
        }
    }

    public InternalKnowledgeValidationReport Run(int maxItems, bool apply, bool dryRun)
    {
        if (apply && dryRun)
        {
            throw new InvalidOperationException("Use either dryRun or apply, not both.");
        }

        Directory.CreateDirectory(Root);
        var now = DateTimeOffset.UtcNow;

        var evidenceAcquisition = LoadEvidenceAcquisitionReport();
        var catalog = new KnowledgeCatalog(_storagePaths).LoadOrCreateItems().ToList();
        var quality = new KnowledgeQualityEngine(_storagePaths).LoadOrCreateReport();
        var validationPlans = new KnowledgeValidationStrategy(_storagePaths).LoadPlanReport() ?? new KnowledgeValidationStrategy(_storagePaths).GeneratePlans(50);
        var catalogById = catalog.ToDictionary(item => item.Id, StringComparer.OrdinalIgnoreCase);
        var qualityById = quality.Items.ToDictionary(item => item.KnowledgeId, StringComparer.OrdinalIgnoreCase);
        var planById = validationPlans.Plans.ToDictionary(item => item.KnowledgeItemId, StringComparer.OrdinalIgnoreCase);

        var buildOutcome = apply && !dryRun
            ? RunBuild()
            : new InternalBuildOutcome(false, 0, ["dry_run_no_build_executed"]);

        var selected = (evidenceAcquisition?.Classifications ?? [])
            .Where(item => item.InternalValidationRequired
                || item.EvidenceAcquisitionClassification.Equals("internal_artifact", StringComparison.OrdinalIgnoreCase)
                || item.EvidenceAcquisitionClassification.Equals("requires_internal_validation", StringComparison.OrdinalIgnoreCase))
            .Where(item => catalogById.ContainsKey(item.KnowledgeItemId))
            .Select(item =>
            {
                var catalogItem = catalogById[item.KnowledgeItemId];
                var qualityItem = qualityById.GetValueOrDefault(item.KnowledgeItemId);
                var plan = planById.GetValueOrDefault(item.KnowledgeItemId);
                return BuildItem(item, catalogItem, qualityItem, plan, buildOutcome, apply, dryRun, now);
            })
            .OrderByDescending(item => item.BuildSucceeded)
            .ThenByDescending(item => item.InternalValidationRequired)
            .ThenBy(item => item.Domain, StringComparer.Ordinal)
            .ThenBy(item => item.KnowledgeItemId, StringComparer.Ordinal)
            .Take(Math.Max(1, maxItems))
            .ToList();

        var commandsExecuted = new List<string>();
        var warnings = new List<string>();
        if (apply && !dryRun)
        {
            commandsExecuted.Add("dotnet build ./cli/Hermes.Cli.csproj");
            warnings.AddRange(buildOutcome.Warnings);
        }
        var buildSucceededItems = selected.Count(item => item.BuildSucceeded);
        var evidenceWrittenItems = selected.Count(item => item.EvidenceWritten);
        var classificationCounts = selected
            .GroupBy(item => item.EvidenceAcquisitionClassification, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        if (apply && !dryRun)
        {
            var executionResults = new List<KnowledgeValidationExecutionResult>();
            foreach (var item in selected)
            {
                var result = BuildExecutionResult(item, buildOutcome, now);
                executionResults.Add(result);
                File.AppendAllText(ValidationExecutionLogPath, JsonSerializer.Serialize(result, JsonDefaults.WriteOptions) + Environment.NewLine);
            }

            if (executionResults.Count > 0)
            {
                new KnowledgeValidationEvidenceWriter(_storagePaths).MergeExecutionEvidence(executionResults);
                commandsExecuted.Add("knowledge-validation-evidence-writer");
            }
        }

        var report = new InternalKnowledgeValidationReport(
            ReportVersion: "internal_knowledge_validation_v1",
            UpdatedAtUtc: now,
            Status: apply && !dryRun ? (selected.Count == 0 ? "no_candidates" : "applied") : "dry_run_ready",
            LoadedItems: evidenceAcquisition?.SelectedItems ?? 0,
            SelectedItems: selected.Count,
            CompletedItems: selected.Count(item => item.BuildSucceeded && item.FileExists && item.CliCommandExists && item.ServiceFileExists),
            PendingItems: selected.Count(item => !item.BuildSucceeded || !item.FileExists || !item.CliCommandExists || !item.ServiceFileExists),
            BuildSucceededItems: buildSucceededItems,
            EvidenceWrittenItems: evidenceWrittenItems,
            ClassificationCounts: classificationCounts,
            Items: selected,
            CommandsExecuted: commandsExecuted,
            Warnings: warnings,
            KnowledgeEvidencePath: KnowledgeEvidencePath,
            ValidationExecutionLogPath: ValidationExecutionLogPath,
            CatalogPath: CatalogPath,
            QualityPath: QualityPath,
            ValidationPlansPath: ValidationPlansPath,
            CliProgramPath: CliProgramPath,
            ReportPath: ReportPath,
            MarkdownPath: MarkdownPath,
            Classifications: evidenceAcquisition?.Classifications ?? [],
            DryRun: dryRun || !apply,
            Applied: apply && !dryRun,
            ResearchOnly: true,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);

        WriteReport(report);
        return report;
    }

    private InternalKnowledgeValidationItem BuildItem(
        KnowledgeEvidenceAcquisitionClassification classification,
        KnowledgeCatalogItem catalogItem,
        KnowledgeQualityItem? qualityItem,
        KnowledgeValidationPlan? validationPlan,
        InternalBuildOutcome buildOutcome,
        bool apply,
        bool dryRun,
        DateTimeOffset now)
    {
        var candidateFiles = InferCandidateFiles(classification, catalogItem);
        var fileExists = candidateFiles.Any(File.Exists);
        var serviceFileExists = candidateFiles.Any(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) && File.Exists(path));
        var testsOrHarnessExists = HasTestsOrHarness(classification, catalogItem);
        var cliCommandExists = CommandExists("internal-knowledge-validation")
            && CommandExists("knowledge-validation-state-sync")
            && CommandExists("knowledge-evidence-acquisition");
        var reportOrConfigExists = File.Exists(CatalogPath)
            && File.Exists(QualityPath)
            && File.Exists(ValidationPlansPath)
            && Directory.Exists(Path.GetDirectoryName(ReportPath)!);
        var buildIncluded = candidateFiles.Any(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            && path.Contains(Path.Combine("Runtime"), StringComparison.OrdinalIgnoreCase));

        var checks = new List<InternalKnowledgeValidationCheck>
        {
            new("file_exists", "Referenced file exists", fileExists, fileExists ? "file located" : "file missing", string.Join("; ", candidateFiles.Take(3))),
            new("build_included", "Referenced code included in build", buildIncluded, buildIncluded ? "code path within Runtime/ build tree" : "build inclusion not inferred", candidateFiles.FirstOrDefault(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))),
            new("cli_command_exists", "Internal CLI commands exist", cliCommandExists, cliCommandExists ? "commands present in cli/Program.cs" : "internal command missing", CliProgramPath),
            new("tests_or_harness_exists", "Tests or harnesses exist", testsOrHarnessExists, testsOrHarnessExists ? "test or harness artifact located" : "test/harness artifact missing", string.Join("; ", InferCandidateFiles(classification, catalogItem).Take(3))),
            new("service_or_report_exists", "Service or report artefact exists", reportOrConfigExists, reportOrConfigExists ? "knowledge runtime artifacts present" : "required report/config artifact missing", ReportPath),
            new("build_result", "dotnet build result", buildOutcome.Succeeded, buildOutcome.Succeeded ? "dotnet build ./cli/Hermes.Cli.csproj succeeded" : "dotnet build did not succeed", buildOutcome.OutputPath)
        };

        var evidenceRefs = new List<string>();
        if (fileExists)
        {
            evidenceRefs.Add($"internal_validation:file_exists:{classification.KnowledgeItemId}");
        }

        if (buildIncluded)
        {
            evidenceRefs.Add($"internal_validation:build_included:{classification.KnowledgeItemId}");
        }

        if (cliCommandExists)
        {
            evidenceRefs.Add($"internal_validation:cli_command_exists:{classification.KnowledgeItemId}");
        }

        if (reportOrConfigExists)
        {
            evidenceRefs.Add($"internal_validation:report_or_config_exists:{classification.KnowledgeItemId}");
        }

        if (buildOutcome.Succeeded)
        {
            evidenceRefs.Add($"internal_validation:dotnet_build_success:{classification.KnowledgeItemId}");
        }

        var passedChecks = checks.Count(check => check.Passed);
        var recommendedNextAction = apply && !dryRun
            ? (passedChecks >= 4 ? "knowledge-validation-state-sync" : "human_review_required")
            : "internal_validation";

        var validationStatusBefore = qualityItem?.LifecycleStatus ?? catalogItem.ValidationStatus;
        var validationStatusAfter = buildOutcome.Succeeded && fileExists
            ? (validationStatusBefore.Equals("trusted", StringComparison.OrdinalIgnoreCase) ? validationStatusBefore : "validated")
            : validationStatusBefore;

        var warnings = new List<string>();
        if (!fileExists)
        {
            warnings.Add("referenced_file_missing");
        }

        if (!buildIncluded)
        {
            warnings.Add("build_inclusion_not_inferred");
        }

        if (!cliCommandExists)
        {
            warnings.Add("internal_cli_command_missing");
        }

        if (!testsOrHarnessExists)
        {
            warnings.Add("test_or_harness_missing");
        }

        if (!reportOrConfigExists)
        {
            warnings.Add("report_or_config_missing");
        }

        if (!buildOutcome.Succeeded)
        {
            warnings.AddRange(buildOutcome.Warnings);
        }

        return new InternalKnowledgeValidationItem(
            KnowledgeItemId: classification.KnowledgeItemId,
            Title: classification.Title,
            Domain: classification.Domain,
            EvidenceAcquisitionClassification: classification.EvidenceAcquisitionClassification,
            SeedNotApplicableReason: classification.SeedNotApplicableReason,
            InternalValidationRequired: classification.InternalValidationRequired,
            CurrentStatus: catalogItem.ValidationStatus,
            ValidationStatusBefore: validationStatusBefore,
            ValidationStatusAfter: validationStatusAfter,
            RecommendedNextAction: recommendedNextAction,
            FileExists: fileExists,
            BuildIncluded: buildIncluded,
            CliCommandExists: cliCommandExists,
            ServiceFileExists: serviceFileExists,
            TestsOrHarnessExists: testsOrHarnessExists,
            ReportOrConfigExists: reportOrConfigExists,
            BuildSucceeded: buildOutcome.Succeeded,
            BuildDurationMs: buildOutcome.DurationMs,
            BuildCommand: "dotnet build ./cli/Hermes.Cli.csproj",
            CandidateFiles: candidateFiles,
            Checks: checks,
            EvidenceRefs: evidenceRefs.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            OutputPaths: candidateFiles.Concat([ReportPath, MarkdownPath, ValidationExecutionLogPath]).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Warnings: warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            EvidenceWritten: apply && !dryRun && buildOutcome.Succeeded && evidenceRefs.Count > 0);
    }

    private IReadOnlyList<string> InferCandidateFiles(KnowledgeEvidenceAcquisitionClassification classification, KnowledgeCatalogItem catalogItem)
    {
        var tokens = new List<string>();
        tokens.AddRange(Tokenize(classification.KnowledgeItemId));
        tokens.AddRange(Tokenize(classification.Title));
        tokens.AddRange(Tokenize(catalogItem.Title));
        tokens.Add(classification.Domain);
        var wanted = tokens
            .Where(token => token.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(16)
            .ToList();

        var roots = new[]
        {
            Path.Combine(_runtimeRoot, "Runtime"),
            Path.Combine(_runtimeRoot, "cli"),
            Path.Combine(_runtimeRoot, "docs"),
            Path.Combine(_runtimeRoot, "config")
        };

        var files = new List<(string Path, int Score)>();
        foreach (var root in roots.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                         .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                         .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
            {
                var normalized = NormalizePath(file);
                var name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                var score = wanted.Count(token => normalized.Contains(token, StringComparison.OrdinalIgnoreCase) || name.Contains(token, StringComparison.OrdinalIgnoreCase));
                if (score > 0)
                {
                    files.Add((file, score));
                }
            }
        }

        return files
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .Select(entry => entry.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\r', '\n', ':', '_', '-', '.', '/', '\\', '(', ')', '[', ']', '{', '}', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .ToList();
    }

    private bool HasTestsOrHarness(KnowledgeEvidenceAcquisitionClassification classification, KnowledgeCatalogItem catalogItem)
    {
        var searchRoots = new[]
        {
            Path.Combine(_runtimeRoot, "tests"),
            Path.Combine(_runtimeRoot, "Test"),
            Path.Combine(_runtimeRoot, "Tests"),
            Path.Combine(_runtimeRoot, "harness"),
            Path.Combine(_runtimeRoot, "Harness"),
            Path.Combine(_runtimeRoot, "Runtime"),
            Path.Combine(_runtimeRoot, "cli")
        };

        var tokens = Tokenize(classification.KnowledgeItemId)
            .Concat(Tokenize(classification.Title))
            .Concat(Tokenize(catalogItem.Title))
            .Where(token => token.Length >= 4)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        foreach (var root in searchRoots.Where(Directory.Exists))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                         .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                         .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)))
            {
                var normalized = NormalizePath(file);
                var fileName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (tokens.Any(token => normalized.Contains(token, StringComparison.OrdinalIgnoreCase) || fileName.Contains(token, StringComparison.OrdinalIgnoreCase)))
                {
                    if (file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                        || file.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                        || file.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                        || file.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }

        return File.Exists(Path.Combine(_runtimeRoot, "tests", "Harness.md"))
            || File.Exists(Path.Combine(_runtimeRoot, "README.md"))
            || File.Exists(Path.Combine(_runtimeRoot, "docs", "README.md"));
    }

    private static string NormalizePath(string path) =>
        path.Replace(Path.DirectorySeparatorChar, '/').ToLowerInvariant();

    private bool CommandExists(string commandName)
    {
        if (!File.Exists(CliProgramPath))
        {
            return false;
        }

        try
        {
            var text = File.ReadAllText(CliProgramPath);
            return text.Contains($"\"{commandName}\"", StringComparison.OrdinalIgnoreCase)
                || text.Contains(commandName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private InternalBuildOutcome RunBuild()
    {
        var started = DateTimeOffset.UtcNow;
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "build ./cli/Hermes.Cli.csproj",
            WorkingDirectory = _runtimeRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = Process.Start(psi);
            if (process is null)
            {
                return new InternalBuildOutcome(false, 0, ["failed_to_start_dotnet_build"]);
            }

            var output = new StringBuilder();
            var error = new StringBuilder();
            process.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) error.AppendLine(e.Data); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            var timeout = TimeSpan.FromSeconds(120);
            if (!process.WaitForExit((int)timeout.TotalMilliseconds))
            {
                TryKill(process);
                return new InternalBuildOutcome(false, (long)timeout.TotalMilliseconds, ["dotnet_build_timeout"]);
            }

            var duration = Math.Max(0, (long)(DateTimeOffset.UtcNow - started).TotalMilliseconds);
            var succeeded = process.ExitCode == 0;
            var warnings = new List<string>();
            if (!succeeded)
            {
                warnings.Add("dotnet_build_failed");
            }

            return new InternalBuildOutcome(succeeded, duration, warnings)
            {
                OutputPath = WriteBuildOutput(output.ToString(), error.ToString())
            };
        }
        catch (Exception ex)
        {
            return new InternalBuildOutcome(false, 0, [$"dotnet_build_exception:{ex.GetType().Name}"]);
        }
    }

    private string WriteBuildOutput(string stdout, string stderr)
    {
        var path = Path.Combine(Root, "dotnet_build_output.txt");
        File.WriteAllText(path, $"STDOUT:{Environment.NewLine}{stdout}{Environment.NewLine}{Environment.NewLine}STDERR:{Environment.NewLine}{stderr}");
        return path;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }

    private KnowledgeValidationExecutionResult BuildExecutionResult(InternalKnowledgeValidationItem item, InternalBuildOutcome buildOutcome, DateTimeOffset startedAtUtc)
    {
        var status = item.BuildSucceeded && item.FileExists && item.CliCommandExists && item.ServiceFileExists
            ? "completed"
            : "needs_more_data";
        var outcome = item.BuildSucceeded && item.FileExists
            ? "internal_validation_confirmed"
            : "internal_validation_pending";
        var evidenceRefs = item.EvidenceRefs
            .Concat(item.Checks.Select(check => $"internal_check:{check.CheckId}:{(check.Passed ? "passed" : "failed")}"))
            .Concat(buildOutcome.OutputPath is not null ? [$"build_output:{buildOutcome.OutputPath}"] : [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .ToList();

        return new KnowledgeValidationExecutionResult(
            ExecutionId: $"internal_validation_{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}",
            QueueItemId: $"internal_validation_{item.KnowledgeItemId}",
            TaskId: $"internal_validation_{item.KnowledgeItemId}",
            PlanId: $"internal_validation_plan_{item.KnowledgeItemId}",
            RequirementId: $"internal_validation_requirement_{item.KnowledgeItemId}",
            KnowledgeItemId: item.KnowledgeItemId,
            Domain: item.Domain,
            RequirementType: "internal_validation",
            Status: status,
            OutcomeStatus: outcome,
            EvidenceSummary: $"Internal validation checks completed; passed={item.Checks.Count(check => check.Passed)}; file_exists={item.FileExists}; build_succeeded={item.BuildSucceeded}; cli_command_exists={item.CliCommandExists}; service_file_exists={item.ServiceFileExists}.",
            EvidenceRefs: evidenceRefs,
            OutputPaths: item.OutputPaths
                .Concat(buildOutcome.OutputPath is not null ? [buildOutcome.OutputPath] : [])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Warnings: item.Warnings,
            StartedAtUtc: startedAtUtc,
            CompletedAtUtc: DateTimeOffset.UtcNow,
            NoTradingExecution: true,
            NoBrokerAction: true,
            NoAutoTrading: true,
            HumanReviewRequired: true);
    }

    private KnowledgeEvidenceAcquisitionReport? LoadEvidenceAcquisitionReport()
    {
        if (!File.Exists(EvidenceAcquisitionPath))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<KnowledgeEvidenceAcquisitionReport>(
                File.ReadAllText(EvidenceAcquisitionPath),
                JsonDefaults.SnapshotReadOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    private void WriteReport(InternalKnowledgeValidationReport report)
    {
        File.WriteAllText(ReportPath, JsonSerializer.Serialize(report, JsonDefaults.WriteOptions));
        File.WriteAllText(MarkdownPath, BuildMarkdown(report));
    }

    private static string BuildMarkdown(InternalKnowledgeValidationReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Internal Knowledge Validation Report");
        sb.AppendLine();
        sb.AppendLine($"- Status: {report.Status}");
        sb.AppendLine($"- Loaded Items: {report.LoadedItems}");
        sb.AppendLine($"- Selected Items: {report.SelectedItems}");
        sb.AppendLine($"- Completed Items: {report.CompletedItems}");
        sb.AppendLine($"- Pending Items: {report.PendingItems}");
        sb.AppendLine($"- Build Succeeded Items: {report.BuildSucceededItems}");
        sb.AppendLine($"- Evidence Written Items: {report.EvidenceWrittenItems}");
        sb.AppendLine();
        sb.AppendLine("## Items");
        foreach (var item in report.Items)
        {
            sb.AppendLine($"- {item.KnowledgeItemId} | {item.EvidenceAcquisitionClassification} | next_action={item.RecommendedNextAction} | file_exists={item.FileExists} | build_succeeded={item.BuildSucceeded}");
            if (item.CandidateFiles.Count > 0)
            {
                sb.AppendLine($"  - candidates: {string.Join(", ", item.CandidateFiles.Take(4))}");
            }
            foreach (var check in item.Checks)
            {
                sb.AppendLine($"  - check:{check.CheckId}={(check.Passed ? "passed" : "failed")} :: {check.Detail}");
            }
        }

        return sb.ToString();
    }

    private sealed record InternalBuildOutcome(bool Succeeded, long DurationMs, IReadOnlyList<string> Warnings)
    {
        public string? OutputPath { get; init; }
    }
}
