using Wasfa.Domain.Model;
using Wasfa.Domain.KnowledgeBase;
using Wasfa.Safety.Contracts;
using Wasfa.Safety.KnowledgeBase;

namespace Wasfa.Safety.Rules;

/// <summary>
/// S3: Allergy Contraindication and Cross-Reactivity Rule.
/// Checks whether the proposed medication directly matches a patient allergy,
/// or whether it belongs to an allergy cross-reactivity group that the patient is sensitised to.
/// </summary>
public sealed class AllergyRule : ISafetyRule
{
    private readonly IDrugKnowledgeProvider _knowledgeProvider;

    public string Id => "S3.ALLERGY";
    public string Name => "Allergy Contraindication & Cross-Reactivity Rule";

    public AllergyRule(IDrugKnowledgeProvider knowledgeProvider)
    {
        _knowledgeProvider = knowledgeProvider;
    }

    public IEnumerable<SafetyFinding> Evaluate(PatientContext patient, ProposedMedication proposed)
    {
        if (patient.Allergies.Count == 0)
        {
            yield break;
        }

        var proposedMolecule = _knowledgeProvider.GetMoleculeByCodeAsync(proposed.MoleculeCode).GetAwaiter().GetResult();
        var proposedGroup = _knowledgeProvider.GetAllergyGroupForMoleculeAsync(proposed.MoleculeCode).GetAwaiter().GetResult();

        foreach (var allergy in patient.Allergies)
        {
            // Case 1: Direct molecule code match
            if (string.Equals(allergy.Code, proposed.MoleculeCode, StringComparison.OrdinalIgnoreCase))
            {
                var molName = proposedMolecule?.NameEn ?? proposed.MoleculeCode;
                var molNameAr = proposedMolecule?.NameAr ?? proposed.MoleculeCode;

                yield return new SafetyFinding(
                    Code: $"ALLERGY.DIRECT.{proposed.MoleculeCode}",
                    Severity: Severity.Contraindicated,
                    TitleEn: $"Documented Direct Drug Allergy: {molName}",
                    TitleAr: $"تحسس دوائي مباشر ومثبت: {molNameAr}",
                    ExplanationEn: $"Patient has a confirmed documented allergy to {molName}. Prescribing is contraindicated due to risk of immediate severe hypersensitivity or anaphylaxis.",
                    ExplanationAr: $"يوجد في سجل المريض تحسس موثق لمادة ({molNameAr}). يحظر صرف الدواء لخطر حدوث صدمة تحسسية تأقية حادة.",
                    InvolvedMolecules: new[] { proposed.MoleculeCode },
                    Source: "World Allergy Organization (WAO); Clinical Drug Allergy Guidelines.",
                    RequiresReasonToOverride: true);

                continue;
            }

            // Case 2: Cross-reactivity group match
            // Patient's allergy record may reference a group ID via AllergyGroupId or via Code
            if (proposedGroup != null)
            {
                var isCrossReactive = false;

                // Check if the patient allergy references this group directly by its ID
                if (!string.IsNullOrWhiteSpace(allergy.AllergyGroupId) &&
                    string.Equals(allergy.AllergyGroupId, proposedGroup.Id, StringComparison.OrdinalIgnoreCase))
                {
                    isCrossReactive = true;
                }
                // Or if the allergy Code matches the group ID
                else if (string.Equals(allergy.Code, proposedGroup.Id, StringComparison.OrdinalIgnoreCase))
                {
                    isCrossReactive = true;
                }
                // Or if the allergy Code matches any molecule code in any subgroup of the proposed group
                else
                {
                    foreach (var sub in proposedGroup.Subgroups)
                    {
                        if (string.Equals(sub.Id, allergy.Code, StringComparison.OrdinalIgnoreCase) ||
                            sub.MoleculeCodes.Any(mc => string.Equals(mc, allergy.Code, StringComparison.OrdinalIgnoreCase)))
                        {
                            isCrossReactive = true;
                            break;
                        }
                    }
                }

                if (isCrossReactive)
                {
                    var sev = string.Equals(proposedGroup.CrossReactivityRisk, "Contraindicated", StringComparison.OrdinalIgnoreCase)
                        ? Severity.Contraindicated
                        : Severity.Major;

                    yield return new SafetyFinding(
                        Code: $"ALLERGY.CROSS.{proposedGroup.Id}",
                        Severity: sev,
                        TitleEn: $"Allergy Cross-Reactivity Warning: {proposedGroup.NameEn}",
                        TitleAr: $"تحذير تفاعل تحسسي تبادلي: {proposedGroup.NameAr}",
                        ExplanationEn: $"Patient has a documented allergy ({allergy.DisplayName}) to a compound in the {proposedGroup.NameEn} group. {proposedGroup.DescriptionEn}",
                        ExplanationAr: $"المريض لديه حساسية موثقة ({allergy.DisplayName}) لأحد مركبات مجموعة {proposedGroup.NameAr}. {proposedGroup.DescriptionAr}",
                        InvolvedMolecules: new[] { proposed.MoleculeCode },
                        Source: proposedGroup.Citation,
                        RequiresReasonToOverride: true);
                }
            }
        }
    }
}
