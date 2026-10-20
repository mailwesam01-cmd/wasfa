using Wasfa.Domain.Model;
using Wasfa.Safety.Contracts;
using Wasfa.Safety.KnowledgeBase;

namespace Wasfa.Safety.Rules;

public sealed class DuplicateTherapyRule : ISafetyRule
{
    private readonly IDrugKnowledgeProvider _knowledgeProvider;

    public string Id => "S2.DUPLICATE_THERAPY";
    public string Name => "Duplicate Therapy Rule";

    public DuplicateTherapyRule(IDrugKnowledgeProvider knowledgeProvider)
    {
        _knowledgeProvider = knowledgeProvider;
    }

    public IEnumerable<SafetyFinding> Evaluate(PatientContext patient, ProposedMedication proposed)
    {
        if (patient.ActiveMedications.Count == 0)
        {
            yield break;
        }

        var proposedMolecule = _knowledgeProvider.GetMoleculeByCodeAsync(proposed.MoleculeCode).GetAwaiter().GetResult();

        foreach (var active in patient.ActiveMedications)
        {
            if (string.Equals(active.Status, "cancelled", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(active.Status, "entered-in-error", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Case 1: Exact identical molecule code (e.g. Panadol + Adol, or Lipitor + Atorlip)
            if (string.Equals(active.MoleculeCode, proposed.MoleculeCode, StringComparison.OrdinalIgnoreCase))
            {
                var activeName = string.IsNullOrWhiteSpace(active.BrandName) ? active.MoleculeCode : active.BrandName;
                var proposedName = string.IsNullOrWhiteSpace(proposed.BrandName) ? proposed.MoleculeCode : proposed.BrandName;
                var molName = proposedMolecule?.NameEn ?? proposed.MoleculeCode;
                var molNameAr = proposedMolecule?.NameAr ?? proposed.MoleculeCode;

                yield return new SafetyFinding(
                    Code: $"DUPLICATE.MOLECULE.{proposed.MoleculeCode}",
                    Severity: Severity.Major,
                    TitleEn: $"Duplicate Active Therapy ({proposedName} & {activeName})",
                    TitleAr: $"تكرار علاجي خطر للمادة الفعالة ({proposedName} مع {activeName})",
                    ExplanationEn: $"Patient is already prescribed active therapy containing {molName}. Co-administration duplicates dosage and risks severe pharmacological toxicity.",
                    ExplanationAr: $"المريض يتناول بالفعل علاجاً يحتوي على مادة ({molNameAr}). تكرار نفس المادة الفعالة يضاعف الجرعة ويعرض المريض للتسمم الدوائي.",
                    InvolvedMolecules: new[] { proposed.MoleculeCode },
                    Source: "WHO Drug Utilization Guidelines; International Pharmacovigilance Consensus.",
                    RequiresReasonToOverride: true);
                
                continue;
            }

            // Case 2: Same chemical subgroup via ATC Level-4 (e.g. two different statins C10AA or two different NSAIDs M01AE)
            if (proposedMolecule != null && !string.IsNullOrWhiteSpace(proposedMolecule.Atc) && proposedMolecule.Atc.Length >= 4)
            {
                var activeMolecule = _knowledgeProvider.GetMoleculeByCodeAsync(active.MoleculeCode).GetAwaiter().GetResult();
                if (activeMolecule != null && !string.IsNullOrWhiteSpace(activeMolecule.Atc) && activeMolecule.Atc.Length >= 4)
                {
                    var proposedClass = proposedMolecule.Atc.Substring(0, 4);
                    var activeClass = activeMolecule.Atc.Substring(0, 4);

                    if (string.Equals(proposedClass, activeClass, StringComparison.OrdinalIgnoreCase))
                    {
                        var activeName = string.IsNullOrWhiteSpace(active.BrandName) ? activeMolecule.NameEn : active.BrandName;
                        var proposedName = string.IsNullOrWhiteSpace(proposed.BrandName) ? proposedMolecule.NameEn : proposed.BrandName;

                        yield return new SafetyFinding(
                            Code: $"DUPLICATE.CLASS.{proposedClass}",
                            Severity: Severity.Moderate,
                            TitleEn: $"Duplicate Therapeutic Class ({proposedMolecule.Category})",
                            TitleAr: $"تكرار فئة علاجية متماثلة ({proposedMolecule.Category})",
                            ExplanationEn: $"Patient is prescribed two different agents from the same pharmacological class ({proposedName} and {activeName}). Review indication to avoid unnecessary polypharmacy.",
                            ExplanationAr: $"يتناول المريض دواءين مختلفين ينتميان لنفس العائلة الدوائية ({proposedName} و {activeName}). يُنصح بمراجعة دواعي الاستعمال لتجنب الإفراط الدوائي.",
                            InvolvedMolecules: new[] { proposed.MoleculeCode, active.MoleculeCode },
                            Source: "ATC/DDD Index 2024; NICE Polypharmacy Optimization Guidance.",
                            RequiresReasonToOverride: false);
                    }
                }
            }
        }
    }
}
