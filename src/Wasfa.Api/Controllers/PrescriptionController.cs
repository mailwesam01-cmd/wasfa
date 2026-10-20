using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using Wasfa.Domain.Entities;
using Wasfa.Infrastructure.Persistence;

namespace Wasfa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PrescriptionController : ControllerBase
{
    private readonly WasfaDbContext _db;

    public PrescriptionController(WasfaDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Look up prescription by short pickup code (e.g. WASFA-8931) for pharmacy scanner
    /// </summary>
    [HttpGet("{shortCode}")]
    public async Task<IActionResult> GetByShortCode(string shortCode)
    {
        var cleanCode = shortCode.Trim().ToUpperInvariant();
        var item = await _db.MedicationRequests.FirstOrDefaultAsync(m => m.ShortCode == cleanCode || m.FhirId == shortCode);

        if (item == null)
        {
            return NotFound(new { error = $"Prescription code '{shortCode}' not found." });
        }

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.FhirId == item.PatientFhirId);
        var overrides = await _db.SafetyOverrides.Where(o => o.RequestFhirId == item.FhirId).ToListAsync();
        var dispenses = await _db.MedicationDispenses.Where(d => d.RequestFhirId == item.FhirId).ToListAsync();

        return Ok(new
        {
            shortCode = item.ShortCode,
            prescriptionId = item.FhirId,
            patient = new
            {
                id = patient?.FhirId,
                nameEn = patient?.FullNameEn,
                nameAr = patient?.FullNameAr,
                gender = patient?.Gender,
                birthDate = patient?.BirthDate,
                weightKg = patient?.WeightKg,
                isPregnant = patient?.IsPregnant,
                renalImpairment = patient?.RenalImpairment
            },
            medication = new
            {
                moleculeCode = item.MoleculeCode,
                brandName = item.BrandName,
                strength = item.Strength,
                doseText = item.DoseText,
                frequency = item.Frequency,
                durationDays = item.DurationDays,
                status = item.Status,
                authoredOn = item.AuthoredOn
            },
            safetyOverrides = overrides.Select(o => new
            {
                code = o.FindingCode,
                severity = o.Severity,
                reason = o.Reason,
                overriddenBy = o.OverriddenBy,
                overriddenAt = o.OverriddenAt
            }),
            dispensingHistory = dispenses.Select(d => new
            {
                dispenseId = d.FhirId,
                pharmacyId = d.PharmacyId,
                dispensedAt = d.DispensedAt,
                quantity = d.Quantity,
                substitutedBrand = d.SubstitutedBrand,
                substitutionReason = d.SubstitutionReason
            }),
            isDispensed = dispenses.Any(),
            qrUrl = $"/api/prescription/{item.ShortCode}/qr"
        });
    }

    /// <summary>
    /// Generate QR code PNG image for prescription short code
    /// </summary>
    [HttpGet("{shortCode}/qr")]
    public IActionResult GenerateQrCode(string shortCode)
    {
        var cleanCode = shortCode.Trim().ToUpperInvariant();
        
        using var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode($"WASFA:RX:{cleanCode}", QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(20);

        return File(qrBytes, "image/png");
    }

    /// <summary>
    /// Emergency Room & Patient shareable consolidated medication card
    /// </summary>
    [HttpGet("card/{patientId}")]
    public async Task<IActionResult> GetMedicationCard(string patientId)
    {
        var cleanId = patientId.Replace("Patient/", "");
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.FhirId == cleanId);
        if (patient == null)
        {
            return NotFound(new { error = $"Patient/{cleanId} not found" });
        }

        var activeMeds = (await _db.MedicationRequests
            .Where(m => m.PatientFhirId == cleanId && m.Status == "active")
            .ToListAsync())
            .OrderByDescending(m => m.AuthoredOn)
            .ToList();

        var overrides = await _db.SafetyOverrides
            .Where(o => activeMeds.Select(m => m.FhirId).Contains(o.RequestFhirId))
            .ToListAsync();

        return Ok(new
        {
            title = "Wasfa Patient Emergency Medication Card",
            titleAr = "بطاقة الأدوية الموحدة للمريض - وصفة",
            issuedAt = DateTimeOffset.UtcNow,
            patient = new
            {
                fhirId = patient.FhirId,
                nameEn = patient.FullNameEn,
                nameAr = patient.FullNameAr,
                gender = patient.Gender,
                birthDate = patient.BirthDate,
                weightKg = patient.WeightKg,
                isPregnant = patient.IsPregnant,
                renalStatus = patient.RenalImpairment
            },
            activeMedicationsCount = activeMeds.Count,
            medications = activeMeds.Select(m => new
            {
                shortCode = m.ShortCode,
                moleculeCode = m.MoleculeCode,
                brandName = m.BrandName ?? m.MoleculeCode,
                strength = m.Strength,
                frequency = m.Frequency,
                durationDays = m.DurationDays,
                prescribedOn = m.AuthoredOn,
                hasSafetyOverride = overrides.Any(o => o.RequestFhirId == m.FhirId)
            }),
            clinicalNotice = "Verified standard HL7 FHIR R4 Consolidated Patient Medication Record."
        });
    }
}
