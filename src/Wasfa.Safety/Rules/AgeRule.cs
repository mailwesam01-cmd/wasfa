using Wasfa.Domain.Model;
using Wasfa.Safety.Contracts;
using Wasfa.Safety.KnowledgeBase;

namespace Wasfa.Safety.Rules;

public sealed class AgeRule : ISafetyRule
{
    private readonly IDrugKnowledgeProvider _knowledgeProvider;

    public string Id => "S7.AGE_APPROPRIATENESS";
    public string Name => "Age Appropriateness (Paediatric & Beers Geriatric) Rule";

    public AgeRule(IDrugKnowledgeProvider knowledgeProvider)
    {
        _knowledgeProvider = knowledgeProvider;
    }

    public IEnumerable<SafetyFinding> Evaluate(PatientContext patient, ProposedMedication proposed)
    {
        var mol = _knowledgeProvider.GetMoleculeByCodeAsync(proposed.MoleculeCode).GetAwaiter().GetResult();
        if (mol == null)
        {
            yield break;
        }

        // 1. Paediatric Inappropriateness (<18 or specific thresholds)
        if (patient.AgeYears < 16 && string.Equals(mol.Code, "N02BA01", StringComparison.OrdinalIgnoreCase)) // Aspirin
        {
            yield return new SafetyFinding(
                Code: "AGE.PAEDIATRIC.REYE_SYNDROME",
                Severity: Severity.Contraindicated,
                TitleEn: "Aspirin Contraindicated Under 16 (Reye's Syndrome)",
                TitleAr: "يحظر الأسبرين دون سن 16 عاماً (خطر متلازمة راي)",
                ExplanationEn: "Salicylate administration in viral illnesses under age 16 can induce lethal encephalopathy and acute fatty liver failure (Reye's syndrome).",
                ExplanationAr: "تناول الساليسيلات لدى الأطفال واليافعين دون سن 16 عاماً يسبب تلفاً دماغياً وكبدياً مميتاً يعرف بمتلازمة راي.",
                InvolvedMolecules: new[] { mol.Code },
                Source: "U.K. MHRA Safety Warning; AAP Committee on Infectious Diseases.",
                RequiresReasonToOverride: true);
        }
        else if (patient.AgeYears < 12 && string.Equals(mol.Code, "N02AX02", StringComparison.OrdinalIgnoreCase)) // Tramadol
        {
            yield return new SafetyFinding(
                Code: "AGE.PAEDIATRIC.TRAMADOL_RESPIRATORY_DEPRESSION",
                Severity: Severity.Contraindicated,
                TitleEn: "Tramadol Contraindicated Under 12 Years",
                TitleAr: "يحظر الترامادول تماماً للأطفال دون 12 عاماً",
                ExplanationEn: "Unpredictable CYP2D6 ultra-rapid metabolism can precipitate catastrophic, fatal opioid-induced respiratory arrest.",
                ExplanationAr: "الاستقلاب فائق السرعة عبر CYP2D6 قد يؤدي إلى هبوط تنفسي أفيوني مفاجئ ومميت لدى الأطفال.",
                InvolvedMolecules: new[] { mol.Code },
                Source: "FDA Black Box Warning on Pediatric Tramadol.",
                RequiresReasonToOverride: true);
        }
        else if (patient.AgeYears < 18 && (string.Equals(mol.Code, "J01MA02", StringComparison.OrdinalIgnoreCase) || string.Equals(mol.Code, "J01MA12", StringComparison.OrdinalIgnoreCase))) // Fluoroquinolones
        {
            yield return new SafetyFinding(
                Code: "AGE.PAEDIATRIC.FLUOROQUINOLONE_ARTHROPATHY",
                Severity: Severity.Major,
                TitleEn: $"Fluoroquinolone ({mol.NameEn}) Restricted in Growing Children",
                TitleAr: $"تقييد استعمال الفلوروكينولون ({mol.NameAr}) لنمو المفاصل والغضاريف",
                ExplanationEn: "Cartilage toxicity, permanent arthropathy, and tendon rupture documented in juvenile weight-bearing joints.",
                ExplanationAr: "سمية موثقة للغضاريف والمفاصل الحاملة للوزن وخطر تمزق الأوتار لدى الأطفال في مرحلة النمو.",
                InvolvedMolecules: new[] { mol.Code },
                Source: "EMA Pediatric Committee; AAP Guidelines.",
                RequiresReasonToOverride: true);
        }
        else if (patient.AgeYears < 8 && string.Equals(mol.Code, "J01AA02", StringComparison.OrdinalIgnoreCase)) // Doxycycline / Tetracyclines
        {
            yield return new SafetyFinding(
                Code: "AGE.PAEDIATRIC.TETRACYCLINE_TEETH",
                Severity: Severity.Major,
                TitleEn: "Tetracycline Caution Under 8 Years (Dental Staining)",
                TitleAr: "تحذير التتراسيكلين دون سن 8 سنوات (تصبغ الأسنان الدائم)",
                ExplanationEn: "Permanent yellow-brown tooth discoloration and enamel hypoplasia due to chelation with developing calcium.",
                ExplanationAr: "تصبغ دائم باللون البني الأصفر في الأسنان ونقص تنسج المينا نتيجة الارتباط مع الكالسيوم النامي.",
                InvolvedMolecules: new[] { mol.Code },
                Source: "FDA Drug Safety Bulletin; BNF for Children.",
                RequiresReasonToOverride: true);
        }

        // 2. Geriatric Inappropriateness (Beers Criteria, Age >= 65)
        if (patient.AgeYears >= 65 && mol.GeriatricBeersFlag)
        {
            yield return new SafetyFinding(
                Code: $"AGE.GERIATRIC.BEERS.{mol.Code}",
                Severity: Severity.Moderate,
                TitleEn: $"Beers Criteria Alert for Elderly Patients: {mol.NameEn}",
                TitleAr: $"تنبيه معايير بيرز لكبار السن: {mol.NameAr}",
                ExplanationEn: $"Identified on the AGS Beers Criteria as potentially inappropriate in older adults due to elevated fall risks, cognitive slowing, or bleeding.",
                ExplanationAr: $"مُدرج ضمن معايير بيرز للأدوية التي تتطلب حذراً بالغاً لدى كبار السن لتسببها في السقوط أو تدهور الإدراك أو النزيف.",
                InvolvedMolecules: new[] { mol.Code },
                Source: "American Geriatrics Society (AGS) Beers Criteria for Potentially Inappropriate Medication Use in Older Adults (2023).",
                RequiresReasonToOverride: false);
        }
    }
}
