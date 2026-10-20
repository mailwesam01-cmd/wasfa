using Microsoft.EntityFrameworkCore;
using Wasfa.Domain.Entities;

namespace Wasfa.Infrastructure.Persistence;

public class WasfaDbContext : DbContext
{
    public WasfaDbContext(DbContextOptions<WasfaDbContext> options) : base(options)
    {
    }

    public DbSet<FhirResourceRecord> FhirResources => Set<FhirResourceRecord>();
    public DbSet<FhirResourceHistoryRecord> FhirResourceHistory => Set<FhirResourceHistoryRecord>();
    public DbSet<PatientProjection> Patients => Set<PatientProjection>();
    public DbSet<MedicationRequestProjection> MedicationRequests => Set<MedicationRequestProjection>();
    public DbSet<MedicationDispenseProjection> MedicationDispenses => Set<MedicationDispenseProjection>();
    public DbSet<SafetyOverrideRecord> SafetyOverrides => Set<SafetyOverrideRecord>();
    public DbSet<ConsentRecord> Consents => Set<ConsentRecord>();
    public DbSet<AuditLogRecord> AuditLogs => Set<AuditLogRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // fhir_resources
        modelBuilder.Entity<FhirResourceRecord>(entity =>
        {
            entity.ToTable("fhir_resources");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ResourceType, e.FhirId, e.VersionId }).IsUnique();
            entity.Property(e => e.ResourceType).IsRequired().HasMaxLength(128);
            entity.Property(e => e.FhirId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Content).IsRequired();
        });

        // fhir_resource_history
        modelBuilder.Entity<FhirResourceHistoryRecord>(entity =>
        {
            entity.ToTable("fhir_resource_history");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ResourceType).IsRequired().HasMaxLength(128);
            entity.Property(e => e.FhirId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Content).IsRequired();
        });

        // patients projection
        modelBuilder.Entity<PatientProjection>(entity =>
        {
            entity.ToTable("patients");
            entity.HasKey(e => e.FhirId);
            entity.HasIndex(e => e.NationalIdHash).IsUnique();
            entity.Property(e => e.FhirId).HasMaxLength(128);
            entity.Property(e => e.NationalIdHash).IsRequired().HasMaxLength(256);
            entity.Property(e => e.FullNameEn).HasMaxLength(256);
            entity.Property(e => e.FullNameAr).HasMaxLength(256);
            entity.Property(e => e.Gender).HasMaxLength(32);
            entity.Property(e => e.RenalImpairment).HasMaxLength(64);
        });

        // medication_requests projection
        modelBuilder.Entity<MedicationRequestProjection>(entity =>
        {
            entity.ToTable("medication_requests");
            entity.HasKey(e => e.FhirId);
            entity.HasIndex(e => e.ShortCode).IsUnique();
            entity.HasIndex(e => new { e.PatientFhirId, e.Status });
            entity.Property(e => e.FhirId).HasMaxLength(128);
            entity.Property(e => e.PatientFhirId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.PrescriberId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.MoleculeCode).IsRequired().HasMaxLength(64);
            entity.Property(e => e.BrandName).HasMaxLength(128);
            entity.Property(e => e.Strength).HasMaxLength(64);
            entity.Property(e => e.DoseText).HasMaxLength(64);
            entity.Property(e => e.Frequency).HasMaxLength(64);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(32);
            entity.Property(e => e.ShortCode).IsRequired().HasMaxLength(32);
        });

        // medication_dispenses projection
        modelBuilder.Entity<MedicationDispenseProjection>(entity =>
        {
            entity.ToTable("medication_dispenses");
            entity.HasKey(e => e.FhirId);
            entity.HasIndex(e => e.RequestFhirId);
            entity.Property(e => e.FhirId).HasMaxLength(128);
            entity.Property(e => e.RequestFhirId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.PharmacyId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.SubstitutedBrand).HasMaxLength(128);
            entity.Property(e => e.SubstitutionReason).HasMaxLength(256);
        });

        // safety_overrides
        modelBuilder.Entity<SafetyOverrideRecord>(entity =>
        {
            entity.ToTable("safety_overrides");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RequestFhirId);
            entity.Property(e => e.RequestFhirId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.FindingCode).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Severity).IsRequired().HasMaxLength(32);
            entity.Property(e => e.Reason).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.OverriddenBy).IsRequired().HasMaxLength(128);
        });

        // consents
        modelBuilder.Entity<ConsentRecord>(entity =>
        {
            entity.ToTable("consents");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.PatientFhirId, e.GranteeOrgId });
            entity.Property(e => e.PatientFhirId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.GranteeOrgId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Scope).IsRequired().HasMaxLength(64);
        });

        // audit_log (append-only)
        modelBuilder.Entity<AuditLogRecord>(entity =>
        {
            entity.ToTable("audit_log");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();
            entity.HasIndex(e => e.PatientFhirId);
            entity.HasIndex(e => e.OccurredAt);
            entity.Property(e => e.ActorId).IsRequired().HasMaxLength(128);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(64);
            entity.Property(e => e.ResourceType).HasMaxLength(128);
            entity.Property(e => e.ResourceId).HasMaxLength(128);
            entity.Property(e => e.PatientFhirId).HasMaxLength(128);
            entity.Property(e => e.Ip).HasMaxLength(64);
        });
    }
}
