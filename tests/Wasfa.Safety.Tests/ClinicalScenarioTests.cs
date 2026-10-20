using System.Text.Json;
using FluentAssertions;
using Wasfa.Domain.Model;
using Wasfa.Safety.Engine;
using Wasfa.Safety.KnowledgeBase;
using Xunit;

namespace Wasfa.Safety.Tests;

public class ClinicalScenarioTests
{
    private readonly SafetyEngine _safetyEngine;
    private readonly string _scenariosDirectory;

    public ClinicalScenarioTests()
    {
        // Resolve knowledge-base directory
        var baseDir = AppContext.BaseDirectory;
        var kbDir = FindDirectoryUp(baseDir, "knowledge-base") 
            ?? throw new DirectoryNotFoundException("knowledge-base directory not found");

        var kbProvider = new JsonKnowledgeProvider(kbDir);
        _safetyEngine = new SafetyEngine(kbProvider);

        _scenariosDirectory = Path.Combine(baseDir, "ClinicalScenarios");
        if (!Directory.Exists(_scenariosDirectory))
        {
            var sourceScenarios = FindDirectoryUp(baseDir, "tests");
            if (sourceScenarios != null)
            {
                _scenariosDirectory = Path.Combine(sourceScenarios, "Wasfa.Safety.Tests", "ClinicalScenarios");
            }
        }
    }

    private static string? FindDirectoryUp(string startPath, string targetName)
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

    public static IEnumerable<object[]> GetScenarioFiles()
    {
        var baseDir = AppContext.BaseDirectory;
        var testDir = FindDirectoryUp(baseDir, "tests")
            ?? throw new DirectoryNotFoundException("tests directory not found");
        var dir = Path.Combine(testDir, "Wasfa.Safety.Tests", "ClinicalScenarios");
        
        var files = Directory.GetFiles(dir, "*.json");
        return files.Select(f => new object[] { Path.GetFileName(f) });
    }

    [Theory]
    [MemberData(nameof(GetScenarioFiles))]
    public async Task Scenario_Should_Match_Exact_Expected_Findings(string scenarioFileName)
    {
        var filePath = Path.Combine(_scenariosDirectory, scenarioFileName);
        File.Exists(filePath).Should().BeTrue($"Scenario file {scenarioFileName} should exist at {filePath}");

        var json = await File.ReadAllTextAsync(filePath);
        var scenario = JsonSerializer.Deserialize<ClinicalScenarioDefinition>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"Failed to deserialize {scenarioFileName}");

        // Act
        var findings = await _safetyEngine.EvaluatePrescriptionAsync(scenario.Patient, scenario.ProposedMedication);

        // Assert
        if (scenario.ExpectedFindings.Count == 0)
        {
            // False-Positive Guard: Must have zero findings!
            findings.Should().BeEmpty(
                because: $"Scenario '{scenario.ScenarioName}' is a safe regimen designed to guard against alert fatigue and false positives.");
        }
        else
        {
            findings.Should().NotBeEmpty(
                because: $"Scenario '{scenario.ScenarioName}' should produce safety warnings.");

            foreach (var expected in scenario.ExpectedFindings)
            {
                var match = findings.FirstOrDefault(f => string.Equals(f.Code, expected.Code, StringComparison.OrdinalIgnoreCase));
                match.Should().NotBeNull(
                    because: $"Scenario '{scenario.ScenarioName}' must contain expected finding code '{expected.Code}'");

                var expectedSeverity = Enum.Parse<Severity>(expected.Severity, true);
                match!.Severity.Should().Be(expectedSeverity,
                    because: $"Finding '{expected.Code}' must have severity '{expected.Severity}'");

                if (expected.RequiresReasonToOverride.HasValue)
                {
                    match.RequiresReasonToOverride.Should().Be(expected.RequiresReasonToOverride.Value,
                        because: $"Finding '{expected.Code}' override requirement must match");
                }
            }
        }
    }

    private sealed class ClinicalScenarioDefinition
    {
        public string ScenarioName { get; set; } = string.Empty;
        public PatientContext Patient { get; set; } = null!;
        public ProposedMedication ProposedMedication { get; set; } = null!;
        public List<ExpectedFindingDefinition> ExpectedFindings { get; set; } = new();
    }

    private sealed class ExpectedFindingDefinition
    {
        public string Code { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public bool? RequiresReasonToOverride { get; set; }
    }
}
