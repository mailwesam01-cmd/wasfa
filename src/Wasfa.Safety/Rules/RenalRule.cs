using Wasfa.Domain.Model;
using Wasfa.Safety.Contracts;
using Wasfa.Safety.KnowledgeBase;

namespace Wasfa.Safety.Rules;

public sealed class RenalRule : ISafetyRule
{
    private readonly IDrugKnowledgeProvider _knowledgeProvider;

    public string Id => "S5.RENAL";
    public string Name => "Renal Function & Dose Adjustment Rule";

    public RenalRule(IDrugKnowledgeProvider knowledgeProvider)
    {
        _knowledgeProvider = knowledgeProvider;
    }

    public IEnumerable<SafetyFinding> Evaluate(PatientContext patient, ProposedMedication proposed)
    {
        if (string.IsNullOrWhiteSpace(patient.RenalImpairment) ||
            string.Equals(patient.RenalImpairment, "Normal", StringComparison.OrdinalIgnoreCase))
        {
            yield break;
        }

        var mol = _knowledgeProvider.GetMoleculeByCodeAsync(proposed.MoleculeCode).GetAwaiter().GetResult();
        if (mol == null || !mol.RenalClearanceFlag)
        {
            yield break;
        }

        var impairment = patient.RenalImpairment.Trim().ToUpperInvariant();

        // High-risk molecules requiring absolute contraindication in severe renal impairment
        if (impairment.Contains("SEVERE") || impairment.Contains("ESRD") || impairment.Contains("ENDSTAGE"))
        {
            if (string.Equals(mol.Code, "A10BA02", StringComparison.OrdinalIgnoreCase)) // Metformin
            {
                yield return new SafetyFinding(
                    Code: "RENAL.CONTRAINDICATION.METFORMIN",
                    Severity: Severity.Contraindicated,
                    TitleEn: "Metformin Contraindicated in Severe Renal Impairment",
                    TitleAr: "يحظر ميتفورمين في حالات القصور الكلوي الشديد",
                    ExplanationEn: "Metformin clearance is predominantly renal; severe accumulation induces potentially fatal lactic acidosis.",
                    ExplanationAr: "يعتمد تصريف الميتفورمين على الكلى، ويؤدي تراكمه في القصور الكلوي الشديد إلى حماض لبني مميت.",
                    InvolvedMolecules: new[] { mol.Code },
                    Source: "FDA Black Box Warning on Metformin; ADA/KDIGO Guidelines.",
                    RequiresReasonToOverride: true);
                yield break;
            }

            if (mol.Category.Contains("NSAID", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mol.Code, "M01AE01", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(mol.Code, "M01AB05", StringComparison.OrdinalIgnoreCase))
            {
                yield return new SafetyFinding(
                    Code: $"RENAL.CONTRAINDICATION.NSAID.{mol.Code}",
                    Severity: Severity.Contraindicated,
                    TitleEn: $"NSAID ({mol.NameEn}) Contraindicated in Advanced Renal Failure",
                    TitleAr: $"يحظر استخدام مضادات الالتهاب غير الستيرويدية ({mol.NameAr}) في الفشل الكلوي المتقدم",
                    ExplanationEn: "Inhibition of vasodilatory renal prostaglandins precipitates acute on chronic renal decompensation and electrolyte crisis.",
                    ExplanationAr: "تثبيط البروستاغلاندينات الكلوية يؤدي إلى هبوط حاد إضافي في وظائف الكلى واضطراب خطير في شوارد الدم.",
                    InvolvedMolecules: new[] { mol.Code },
                    Source: "KDIGO Clinical Practice Guideline for CKD; BNF 86.",
                    RequiresReasonToOverride: true);
                yield break;
            }

            yield return new SafetyFinding(
                Code: $"RENAL.SEVERE_ADJUSTMENT.{mol.Code}",
                Severity: Severity.Major,
                TitleEn: $"Major Dose Adjustment Required: {mol.NameEn}",
                TitleAr: $"تعديل جوهري إلزامي للجرعة في القصور الكلوي: {mol.NameAr}",
                ExplanationEn: $"Substantial renal clearance dependency ({mol.NameEn}). Reduce dosage or extend dosing interval to prevent toxic retention.",
                ExplanationAr: $"يعتمد الدواء ({mol.NameAr}) على الإخراج الكلوي. يجب خفض الجرعة أو مباعدة الفترات لمنع التراكم السمي.",
                InvolvedMolecules: new[] { mol.Code },
                Source: "Renal Drug Handbook 5th Edition; BNF 86.",
                RequiresReasonToOverride: true);
        }
        else if (impairment.Contains("MODERATE"))
        {
            yield return new SafetyFinding(
                Code: $"RENAL.MODERATE_WARNING.{mol.Code}",
                Severity: Severity.Moderate,
                TitleEn: $"Renal Monitoring Recommended: {mol.NameEn}",
                TitleAr: $"يوصى بمراقبة وظائف الكلى وتعديل الجرعة: {mol.NameAr}",
                ExplanationEn: $"Patient has moderate renal impairment. Evaluate creatinine clearance and adjust {mol.NameEn} dosage accordingly.",
                ExplanationAr: $"يعاني المريض من قصور كلوي متوسط. يوصى بقياس تصفية الكرياتينين وتعديل جرعة ({mol.NameAr}) حسب الإرشادات.",
                InvolvedMolecules: new[] { mol.Code },
                Source: "Renal Drug Database; BNF 86.",
                RequiresReasonToOverride: false);
        }
    }
}
