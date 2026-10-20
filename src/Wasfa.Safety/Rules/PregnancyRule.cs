using Wasfa.Domain.Model;
using Wasfa.Safety.Contracts;
using Wasfa.Safety.KnowledgeBase;

namespace Wasfa.Safety.Rules;

public sealed class PregnancyRule : ISafetyRule
{
    private readonly IDrugKnowledgeProvider _knowledgeProvider;

    public string Id => "S6.PREGNANCY";
    public string Name => "Pregnancy & Lactation Teratogenicity Rule";

    public PregnancyRule(IDrugKnowledgeProvider knowledgeProvider)
    {
        _knowledgeProvider = knowledgeProvider;
    }

    public IEnumerable<SafetyFinding> Evaluate(PatientContext patient, ProposedMedication proposed)
    {
        if (!patient.IsPregnant)
        {
            yield break;
        }

        var mol = _knowledgeProvider.GetMoleculeByCodeAsync(proposed.MoleculeCode).GetAwaiter().GetResult();
        if (mol == null)
        {
            yield break;
        }

        var cat = (mol.PregnancyCategory ?? "C").Trim().ToUpperInvariant();

        if (cat == "X" || mol.IsContraindicatedInPregnancy && cat != "D")
        {
            yield return new SafetyFinding(
                Code: $"PREGNANCY.CONTRAINDICATED.CAT_X.{mol.Code}",
                Severity: Severity.Contraindicated,
                TitleEn: $"Category X Absolute Contraindication in Pregnancy: {mol.NameEn}",
                TitleAr: $"ممنوع قطعاً أثناء الحمل (تصنيف X): {mol.NameAr}",
                ExplanationEn: $"Proven human fetal abnormalities and teratogenic injury ({mol.NameEn}). Risk of fetal harm undeniably outweighs any potential clinical benefit.",
                ExplanationAr: $"ثبتت قطيعة تسببه بتشوهات خلقية وأضرار جنينية بالغة ({mol.NameAr}). مخاطر الدواء تفوق قطعاً أي فائدة علاجية.",
                InvolvedMolecules: new[] { mol.Code },
                Source: "FDA Pregnancy and Lactation Labeling Rule (PLLR); Briggs' Drugs in Pregnancy and Lactation.",
                RequiresReasonToOverride: true);
        }
        else if (cat == "D")
        {
            yield return new SafetyFinding(
                Code: $"PREGNANCY.MAJOR.CAT_D.{mol.Code}",
                Severity: Severity.Major,
                TitleEn: $"Category D High Fetal Risk Warning: {mol.NameEn}",
                TitleAr: $"تحذير مخاطر جنينية عالية (تصنيف D): {mol.NameAr}",
                ExplanationEn: $"Positive evidence of human fetal risk ({mol.NameEn}). Use only in maternal life-threatening emergencies where safer alternatives are ineffective.",
                ExplanationAr: $"توجد أدلة بشرية مؤكدة على أضرار جنينية ({mol.NameAr}). لا يستخدم إلا في الحالات الطارئة المهددة للحياة عند انعدام البدائل الآمنة.",
                InvolvedMolecules: new[] { mol.Code },
                Source: "FDA Drug Safety Notices; BNF 86 Appendix on Pregnancy.",
                RequiresReasonToOverride: true);
        }
    }
}
