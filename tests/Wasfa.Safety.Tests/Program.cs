using System.Text.Json;
using Wasfa.Domain.Model;
using Wasfa.Safety.Engine;
using Wasfa.Safety.KnowledgeBase;

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("===============================================================================");
Console.WriteLine("               WASFA CLINICAL SCENARIO SAFETY TEST SUITE                       ");
Console.WriteLine("===============================================================================");
Console.ResetColor();

var baseDir = AppContext.BaseDirectory;
var kbDir = FindDirectoryUp(baseDir, "knowledge-base")
    ?? throw new DirectoryNotFoundException("Could not find knowledge-base directory.");

var scenariosDir = Path.Combine(baseDir, "ClinicalScenarios");
if (!Directory.Exists(scenariosDir))
{
    var testsDir = FindDirectoryUp(baseDir, "tests");
    if (testsDir != null)
    {
        scenariosDir = Path.Combine(testsDir, "Wasfa.Safety.Tests", "ClinicalScenarios");
    }
}

if (!Directory.Exists(scenariosDir))
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[ERROR] Scenarios directory not found: {scenariosDir}");
    Console.ResetColor();
    return 1;
}

var knowledgeProvider = new JsonKnowledgeProvider(kbDir);
var safetyEngine = new SafetyEngine(knowledgeProvider);

var scenarioFiles = Directory.GetFiles(scenariosDir, "*.json").OrderBy(f => f).ToList();
Console.WriteLine($"Discovered {scenarioFiles.Count} clinical scenario definition files.");
Console.WriteLine($"Knowledge base loaded from: {kbDir}\n");

int passedCount = 0;
int failedCount = 0;

var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

foreach (var file in scenarioFiles)
{
    var fileName = Path.GetFileName(file);
    var content = await File.ReadAllTextAsync(file);
    var scenario = JsonSerializer.Deserialize<ClinicalScenarioDefinition>(content, jsonOptions);

    if (scenario == null)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {fileName}: Could not deserialize scenario.");
        Console.ResetColor();
        failedCount++;
        continue;
    }

    Console.Write($"Running scenario: {scenario.ScenarioName} ({fileName})... ");

    var findings = await safetyEngine.EvaluatePrescriptionAsync(scenario.Patient, scenario.ProposedMedication);
    var scenarioFailed = false;
    var failureReasons = new List<string>();

    if (scenario.ExpectedFindings.Count == 0)
    {
        // False positive guard: Expect 0 findings
        if (findings.Count > 0)
        {
            scenarioFailed = true;
            failureReasons.Add($"False positive guard failed: Expected 0 findings, but received {findings.Count} findings: {string.Join(", ", findings.Select(f => f.Code))}");
        }
    }
    else
    {
        foreach (var expected in scenario.ExpectedFindings)
        {
            var match = findings.FirstOrDefault(f => string.Equals(f.Code, expected.Code, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                scenarioFailed = true;
                failureReasons.Add($"Missing expected finding code: '{expected.Code}'");
            }
            else
            {
                var expectedSev = Enum.Parse<Severity>(expected.Severity, true);
                if (match.Severity != expectedSev)
                {
                    scenarioFailed = true;
                    failureReasons.Add($"Finding '{expected.Code}' had severity '{match.Severity}', expected '{expectedSev}'");
                }

                if (expected.RequiresReasonToOverride.HasValue && match.RequiresReasonToOverride != expected.RequiresReasonToOverride.Value)
                {
                    scenarioFailed = true;
                    failureReasons.Add($"Finding '{expected.Code}' RequiresReasonToOverride was '{match.RequiresReasonToOverride}', expected '{expected.RequiresReasonToOverride.Value}'");
                }
            }
        }
    }

    if (scenarioFailed)
    {
        failedCount++;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("FAILED");
        foreach (var reason in failureReasons)
        {
            Console.WriteLine($"   -> {reason}");
        }
        Console.ResetColor();
    }
    else
    {
        passedCount++;
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("PASSED");
        Console.ForegroundColor = ConsoleColor.DarkGray;
        if (findings.Count > 0)
        {
            foreach (var f in findings)
            {
                Console.WriteLine($"      [{f.Severity}] {f.Code} - {f.TitleEn} (Override Reason Required: {f.RequiresReasonToOverride})");
            }
        }
        else
        {
            Console.WriteLine("      [Clean Check] 0 safety warnings (Clean false-positive guard).");
        }
        Console.ResetColor();
    }
}

Console.WriteLine("\n-------------------------------------------------------------------------------");
if (failedCount == 0)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"ALL {passedCount} CLINICAL SCENARIOS PASSED WITH 100% PRECISION!");
    Console.ResetColor();
    return 0;
}
else
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"TEST SUITE FAILED: {passedCount} Passed, {failedCount} Failed.");
    Console.ResetColor();
    return 1;
}

static string? FindDirectoryUp(string startPath, string targetName)
{
    var current = new DirectoryInfo(startPath);
    while (current != null)
    {
        var candidate = Path.Combine(current.FullName, targetName);
        if (Directory.Exists(candidate))
        {
            return candidate;
        }
        current = current.Parent;
    }
    return null;
}

public sealed class ClinicalScenarioDefinition
{
    public string ScenarioName { get; set; } = string.Empty;
    public PatientContext Patient { get; set; } = null!;
    public ProposedMedication ProposedMedication { get; set; } = null!;
    public List<ExpectedFindingDefinition> ExpectedFindings { get; set; } = new();
}

public sealed class ExpectedFindingDefinition
{
    public string Code { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public bool? RequiresReasonToOverride { get; set; }
}
