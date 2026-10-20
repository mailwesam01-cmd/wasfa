using Wasfa.Domain.Model;
using Wasfa.Safety.Contracts;
using Wasfa.Safety.KnowledgeBase;

namespace Wasfa.Safety.Rules;

public sealed class InteractionRule : ISafetyRule
{
    private readonly IDrugKnowledgeProvider _knowledgeProvider;

    public string Id => "S1.INTERACTION";
    public string Name => "Drug-Drug Interaction Rule";

    public InteractionRule(IDrugKnowledgeProvider knowledgeProvider)
    {
        _knowledgeProvider = knowledgeProvider;
    }

    public IEnumerable<SafetyFinding> Evaluate(PatientContext patient, ProposedMedication proposed)
    {
        if (patient.ActiveMedications.Count == 0)
        {
            yield break;
        }

        var interactionsTask = _knowledgeProvider.GetInteractionsForMoleculeAsync(proposed.MoleculeCode);
        var interactions = interactionsTask.GetAwaiter().GetResult();

        foreach (var active in patient.ActiveMedications)
        {
            if (string.Equals(active.Status, "cancelled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(active.Status, "entered-in-error", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = interactions.FirstOrDefault(i =>
                (string.Equals(i.MoleculeCodeA, proposed.MoleculeCode, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(i.MoleculeCodeB, active.MoleculeCode, StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(i.MoleculeCodeB, proposed.MoleculeCode, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(i.MoleculeCodeA, active.MoleculeCode, StringComparison.OrdinalIgnoreCase)));

            if (match != null)
            {
                var severity = Enum.TryParse<Severity>(match.Severity, true, out var parsedSeverity)
                    ? parsedSeverity
                    : Severity.Major;

                yield return new SafetyFinding(
                    Code: match.Id,
                    Severity: severity,
                    TitleEn: match.TitleEn,
                    TitleAr: match.TitleAr,
                    ExplanationEn: match.ClinicalConsequenceEn,
                    ExplanationAr: match.ClinicalConsequenceAr,
                    InvolvedMolecules: new[] { proposed.MoleculeCode, active.MoleculeCode },
                    Source: match.Source,
                    RequiresReasonToOverride: severity >= Severity.Major);
            }
        }
    }
}
