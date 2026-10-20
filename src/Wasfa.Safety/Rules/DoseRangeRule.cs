using Wasfa.Domain.Model;
using Wasfa.Safety.Contracts;
using Wasfa.Safety.KnowledgeBase;

namespace Wasfa.Safety.Rules;

public sealed class DoseRangeRule : ISafetyRule
{
    private readonly IDrugKnowledgeProvider _knowledgeProvider;

    public string Id => "S4.DOSE_RANGE";
    public string Name => "Dose Range & Paediatric Dosing Rule";

    public DoseRangeRule(IDrugKnowledgeProvider knowledgeProvider)
    {
        _knowledgeProvider = knowledgeProvider;
    }

    public IEnumerable<SafetyFinding> Evaluate(PatientContext patient, ProposedMedication proposed)
    {
        var doseDef = _knowledgeProvider.GetDoseRangeForMoleculeAsync(proposed.MoleculeCode).GetAwaiter().GetResult();
        if (doseDef == null)
        {
            yield break;
        }

        var isPaediatric = patient.AgeYears < 18 || (patient.WeightKg.HasValue && patient.WeightKg.Value < 40);

        // Check 1: Paediatric weight-based dose check
        if (isPaediatric)
        {
            if (patient.WeightKg.HasValue && doseDef.PaediatricMaxDoseMgPerKgPerDay.HasValue)
            {
                var maxDailyAllowedMg = doseDef.PaediatricMaxDoseMgPerKgPerDay.Value * patient.WeightKg.Value;
                var proposedDailyMg = proposed.DailyDoseMg ?? (proposed.DoseQuantity * GetDosesPerDay(proposed.Frequency));

                if (proposedDailyMg > maxDailyAllowedMg * 1.05m)
                {
                    yield return new SafetyFinding(
                        Code: "DOSE.PAEDIATRIC.EXCEEDED_DAILY",
                        Severity: Severity.Major,
                        TitleEn: $"Excessive Paediatric Daily Dose: {doseDef.NameEn}",
                        TitleAr: $"تجاوز الحد الأقصى لجرعة الأطفال اليومية: {doseDef.NameEn}",
                        ExplanationEn: $"Proposed daily dose of {proposedDailyMg:F0} mg exceeds the maximum recommended paediatric threshold of {maxDailyAllowedMg:F0} mg ({doseDef.PaediatricMaxDoseMgPerKgPerDay} mg/kg/day for {patient.WeightKg} kg).",
                        ExplanationAr: $"الجرعة اليومية المقترحة ({proposedDailyMg:F0} مغ) تتجاوز الحد الأقصى الآمن لوزن الطفل البالغ ({maxDailyAllowedMg:F0} مغ يومياً لوزن {patient.WeightKg} كغ).",
                        InvolvedMolecules: new[] { proposed.MoleculeCode },
                        Source: doseDef.Citation,
                        RequiresReasonToOverride: true);
                }
            }
            else if (patient.AgeYears < 12 && proposed.DoseQuantity >= (doseDef.MaxAdultSingleDoseMg ?? 500m))
            {
                // Adult dose prescribed to young child without weight adjustment
                yield return new SafetyFinding(
                    Code: "DOSE.PAEDIATRIC.ADULT_DOSE_PRESCRIBED",
                    Severity: Severity.Major,
                    TitleEn: $"Full Adult Dose Prescribed to Paediatric Patient",
                    TitleAr: $"صرف جرعة بالغين كاملة لطفل دون احتساب الوزن",
                    ExplanationEn: $"Standard adult strength ({proposed.DoseQuantity} {proposed.DoseUnit}) was prescribed to a child aged {patient.AgeYears} years without paediatric weight-based titration.",
                    ExplanationAr: $"تم تحديد جرعة مخصصة للبالغين ({proposed.DoseQuantity} {proposed.DoseUnit}) لطفل عمره {patient.AgeYears} سنوات دون حساب الجرعة على أساس الوزن.",
                    InvolvedMolecules: new[] { proposed.MoleculeCode },
                    Source: doseDef.Citation,
                    RequiresReasonToOverride: true);
            }
        }
        else
        {
            // Adult check
            var proposedSingleMg = proposed.SingleDoseMg ?? proposed.DoseQuantity;
            if (doseDef.MaxAdultSingleDoseMg.HasValue && proposedSingleMg > doseDef.MaxAdultSingleDoseMg.Value * 1.05m)
            {
                yield return new SafetyFinding(
                    Code: "DOSE.ADULT.EXCEEDED_SINGLE",
                    Severity: Severity.Major,
                    TitleEn: $"Exceeded Maximum Adult Single Dose: {doseDef.NameEn}",
                    TitleAr: $"تجاوز الجرعة المفردة القصوى للبالغين: {doseDef.NameEn}",
                    ExplanationEn: $"Single dose of {proposedSingleMg:F0} mg exceeds maximum safe single administration ceiling of {doseDef.MaxAdultSingleDoseMg} mg.",
                    ExplanationAr: $"الجرعة الواحدة ({proposedSingleMg:F0} مغ) تتجاوز سقف الجرعة المفردة المسموحة سريرياً ({doseDef.MaxAdultSingleDoseMg} مغ).",
                    InvolvedMolecules: new[] { proposed.MoleculeCode },
                    Source: doseDef.Citation,
                    RequiresReasonToOverride: true);
            }

            var proposedDailyMg = proposed.DailyDoseMg ?? (proposed.DoseQuantity * GetDosesPerDay(proposed.Frequency));
            if (doseDef.MaxAdultDailyDoseMg.HasValue && proposedDailyMg > doseDef.MaxAdultDailyDoseMg.Value * 1.05m)
            {
                yield return new SafetyFinding(
                    Code: "DOSE.ADULT.EXCEEDED_DAILY",
                    Severity: Severity.Major,
                    TitleEn: $"Exceeded Maximum Adult Daily Ceiling: {doseDef.NameEn}",
                    TitleAr: $"تجاوز الحد اليومي الأقصى التراكمي: {doseDef.NameEn}",
                    ExplanationEn: $"Total daily dose of {proposedDailyMg:F0} mg exceeds the absolute maximum ceiling of {doseDef.MaxAdultDailyDoseMg} mg/day.",
                    ExplanationAr: $"إجمالي الجرعة اليومية ({proposedDailyMg:F0} مغ) يتجاوز الحد التراكمي الأقصى المسموح به يومياً ({doseDef.MaxAdultDailyDoseMg} مغ/يوم).",
                    InvolvedMolecules: new[] { proposed.MoleculeCode },
                    Source: doseDef.Citation,
                    RequiresReasonToOverride: true);
            }
        }
    }

    private static decimal GetDosesPerDay(string frequency)
    {
        var f = (frequency ?? "").Trim().ToUpperInvariant();
        return f switch
        {
            "QD" or "ONCE DAILY" or "DAILY" or "24H" or "Q24H" => 1m,
            "BID" or "TWICE DAILY" or "Q12H" or "12H" => 2m,
            "TID" or "THREE TIMES DAILY" or "Q8H" or "8H" => 3m,
            "QID" or "FOUR TIMES DAILY" or "Q6H" or "6H" => 4m,
            "Q4H" or "EVERY 4 HOURS" => 6m,
            "WEEKLY" or "ONCE WEEKLY" => 0.14m,
            _ => 1m
        };
    }
}
