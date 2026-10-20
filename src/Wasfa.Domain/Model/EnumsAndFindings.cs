namespace Wasfa.Domain.Model;

public enum Severity
{
    Minor = 0,
    Moderate = 1,
    Major = 2,
    Contraindicated = 3
}

public sealed record SafetyFinding(
    string Code,
    Severity Severity,
    string TitleEn,
    string TitleAr,
    string ExplanationEn,
    string ExplanationAr,
    IReadOnlyList<string> InvolvedMolecules,
    string Source,
    bool RequiresReasonToOverride);

public sealed record ActiveMedication(
    string Id,
    string MoleculeCode,
    string? BrandName,
    string Strength,
    string Frequency,
    DateTimeOffset AuthoredOn,
    string Status);

public sealed record Allergy(
    string Code,
    string DisplayName,
    string Criticality,
    string? AllergyGroupId = null);

public sealed record ProposedMedication(
    string MoleculeCode,
    string? BrandName,
    decimal DoseQuantity,
    string DoseUnit,
    string Frequency,
    int DurationDays,
    decimal? DailyDoseMg = null,
    decimal? SingleDoseMg = null);

public sealed record PatientContext(
    int AgeYears,
    decimal? WeightKg,
    string Gender,
    bool IsPregnant,
    string? RenalImpairment,
    IReadOnlyList<ActiveMedication> ActiveMedications,
    IReadOnlyList<Allergy> Allergies,
    IReadOnlyList<string> ConditionCodes);
