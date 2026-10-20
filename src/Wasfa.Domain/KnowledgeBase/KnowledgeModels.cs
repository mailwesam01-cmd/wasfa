namespace Wasfa.Domain.KnowledgeBase;

public sealed class MoleculeDefinition
{
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Atc { get; set; } = string.Empty;
    public string RxNorm { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string PregnancyCategory { get; set; } = "C";
    public bool IsContraindicatedInPregnancy { get; set; }
    public bool RenalClearanceFlag { get; set; }
    public bool HepaticToxicityFlag { get; set; }
    public bool GeriatricBeersFlag { get; set; }
    public string? PaediatricWarning { get; set; }
    public string? StandardAdultDose { get; set; }
}

public sealed class BrandMapping
{
    public string BrandName { get; set; } = string.Empty;
    public string BrandNameAr { get; set; } = string.Empty;
    public string MoleculeCode { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public string Form { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
}

public sealed class InteractionRuleDefinition
{
    public string Id { get; set; } = string.Empty;
    public string MoleculeCodeA { get; set; } = string.Empty;
    public string MoleculeCodeB { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string TitleEn { get; set; } = string.Empty;
    public string TitleAr { get; set; } = string.Empty;
    public string MechanismEn { get; set; } = string.Empty;
    public string MechanismAr { get; set; } = string.Empty;
    public string ClinicalConsequenceEn { get; set; } = string.Empty;
    public string ClinicalConsequenceAr { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
}

public sealed class AllergySubgroup
{
    public string Id { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public List<string> MoleculeCodes { get; set; } = new();
}

public sealed class AllergyCrossReactivityGroup
{
    public string Id { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string DescriptionEn { get; set; } = string.Empty;
    public string DescriptionAr { get; set; } = string.Empty;
    public List<AllergySubgroup> Subgroups { get; set; } = new();
    public string CrossReactivityRisk { get; set; } = "Major";
    public string Citation { get; set; } = string.Empty;
}

public sealed class DoseRangeDefinition
{
    public string MoleculeCode { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public decimal? MaxAdultSingleDoseMg { get; set; }
    public decimal? MaxAdultDailyDoseMg { get; set; }
    public decimal? PaediatricDoseMgPerKgPerDose { get; set; }
    public decimal? PaediatricMaxDoseMgPerKgPerDay { get; set; }
    public decimal? MinAgeYears { get; set; }
    public decimal? RenalDoseAdjustmentCrClThreshold { get; set; }
    public string Citation { get; set; } = string.Empty;
}
