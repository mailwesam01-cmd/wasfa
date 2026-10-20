using Wasfa.Domain.Model;
using Wasfa.Safety.Contracts;
using Wasfa.Safety.KnowledgeBase;
using Wasfa.Safety.Rules;

namespace Wasfa.Safety.Engine;

public sealed class SafetyEngine : ISafetyEngine
{
    private readonly List<ISafetyRule> _rules = new();

    public SafetyEngine(IDrugKnowledgeProvider knowledgeProvider)
    {
        // Register the 7 core safety rules in order S1 -> S7
        _rules.Add(new InteractionRule(knowledgeProvider));
        _rules.Add(new DuplicateTherapyRule(knowledgeProvider));
        _rules.Add(new AllergyRule(knowledgeProvider));
        _rules.Add(new DoseRangeRule(knowledgeProvider));
        _rules.Add(new RenalRule(knowledgeProvider));
        _rules.Add(new PregnancyRule(knowledgeProvider));
        _rules.Add(new AgeRule(knowledgeProvider));
    }

    public SafetyEngine(IEnumerable<ISafetyRule> rules)
    {
        _rules.AddRange(rules);
    }

    public Task<IReadOnlyList<SafetyFinding>> EvaluatePrescriptionAsync(
        PatientContext patient,
        ProposedMedication proposed,
        CancellationToken ct = default)
    {
        var findings = new List<SafetyFinding>();

        foreach (var rule in _rules)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var ruleFindings = rule.Evaluate(patient, proposed);
                if (ruleFindings != null)
                {
                    findings.AddRange(ruleFindings);
                }
            }
            catch (Exception)
            {
                // Safety engine must never swallow or fail catastrophically; rethrow or log
                throw;
            }
        }

        // Distinct by Code and sorted by Severity (Contraindicated first)
        var orderedFindings = findings
            .GroupBy(f => f.Code)
            .Select(g => g.First())
            .OrderByDescending(f => (int)f.Severity)
            .ToList();

        return Task.FromResult<IReadOnlyList<SafetyFinding>>(orderedFindings);
    }
}
