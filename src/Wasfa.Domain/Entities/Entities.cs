namespace Wasfa.Domain.Entities;

public sealed class FhirResourceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ResourceType { get; set; } = string.Empty;
    public string FhirId { get; set; } = string.Empty;
    public int VersionId { get; set; } = 1;
    public string Content { get; set; } = "{}";
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
}

public sealed class FhirResourceHistoryRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ResourceType { get; set; } = string.Empty;
    public string FhirId { get; set; } = string.Empty;
    public int VersionId { get; set; } = 1;
    public string Content { get; set; } = "{}";
    public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
}

public sealed class PatientProjection
{
    public string FhirId { get; set; } = string.Empty;
    public string NationalIdHash { get; set; } = string.Empty;
    public string FullNameEn { get; set; } = string.Empty;
    public string FullNameAr { get; set; } = string.Empty;
    public DateOnly? BirthDate { get; set; }
    public string Gender { get; set; } = "unknown";
    public decimal? WeightKg { get; set; }
    public bool IsPregnant { get; set; }
    public string? RenalImpairment { get; set; }
}

public sealed class MedicationRequestProjection
{
    public string FhirId { get; set; } = string.Empty;
    public string PatientFhirId { get; set; } = string.Empty;
    public string PrescriberId { get; set; } = string.Empty;
    public string MoleculeCode { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string Strength { get; set; } = string.Empty;
    public string DoseText { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public int DurationDays { get; set; }
    public string Status { get; set; } = "active";
    public DateTimeOffset AuthoredOn { get; set; } = DateTimeOffset.UtcNow;
    public string ShortCode { get; set; } = string.Empty;
}

public sealed class MedicationDispenseProjection
{
    public string FhirId { get; set; } = string.Empty;
    public string RequestFhirId { get; set; } = string.Empty;
    public string PharmacyId { get; set; } = string.Empty;
    public DateTimeOffset DispensedAt { get; set; } = DateTimeOffset.UtcNow;
    public decimal Quantity { get; set; }
    public string? SubstitutedBrand { get; set; }
    public string? SubstitutionReason { get; set; }
}

public sealed class SafetyOverrideRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RequestFhirId { get; set; } = string.Empty;
    public string FindingCode { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string OverriddenBy { get; set; } = string.Empty;
    public DateTimeOffset OverriddenAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ConsentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string PatientFhirId { get; set; } = string.Empty;
    public string GranteeOrgId { get; set; } = string.Empty;
    public string Scope { get; set; } = "read_medications";
    public DateTimeOffset GrantedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class AuditLogRecord
{
    public long Id { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // read | write | override | dispense
    public string? ResourceType { get; set; }
    public string? ResourceId { get; set; }
    public string? PatientFhirId { get; set; }
    public string? Ip { get; set; }
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Detail { get; set; }
}
