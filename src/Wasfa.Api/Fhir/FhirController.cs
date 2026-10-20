using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wasfa.Domain.Entities;
using Wasfa.Domain.Model;
using Wasfa.Infrastructure.Persistence;
using Wasfa.Infrastructure.Security;
using Wasfa.Safety.Contracts;
using Wasfa.Safety.KnowledgeBase;

namespace Wasfa.Api.Fhir;

[ApiController]
[Route("fhir")]
public class FhirController : ControllerBase
{
    private readonly WasfaDbContext _db;
    private readonly ISafetyEngine _safetyEngine;
    private readonly IDrugKnowledgeProvider _knowledgeProvider;
    private readonly FhirJsonSerializer _serializer = new(new SerializerSettings { Pretty = true });
    private readonly FhirJsonParser _parser = new();

    public FhirController(
        WasfaDbContext db,
        ISafetyEngine safetyEngine,
        IDrugKnowledgeProvider knowledgeProvider)
    {
        _db = db;
        _safetyEngine = safetyEngine;
        _knowledgeProvider = knowledgeProvider;
    }

    /// <summary>
    /// FHIR R4 CapabilityStatement (metadata)
    /// </summary>
    [HttpGet("metadata")]
    public IActionResult GetCapabilityStatement()
    {
        var cs = new CapabilityStatement
        {
            Status = PublicationStatus.Active,
            Date = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
            Publisher = "Wasfa Health IT Platform",
            Kind = CapabilityStatementKind.Instance,
            FhirVersion = FHIRVersion.N4_0_1,
            Format = new[] { "json" },
            Rest = new List<CapabilityStatement.RestComponent>
            {
                new()
                {
                    Mode = CapabilityStatement.RestfulCapabilityMode.Server,
                    Resource = new List<CapabilityStatement.ResourceComponent>
                    {
                        new() { Type = "Patient", Interaction = new() { new() { Code = CapabilityStatement.TypeRestfulInteraction.Read }, new() { Code = CapabilityStatement.TypeRestfulInteraction.SearchType } } },
                        new() { Type = "MedicationRequest", Interaction = new() { new() { Code = CapabilityStatement.TypeRestfulInteraction.Read }, new() { Code = CapabilityStatement.TypeRestfulInteraction.Create }, new() { Code = CapabilityStatement.TypeRestfulInteraction.SearchType } } },
                        new() { Type = "MedicationDispense", Interaction = new() { new() { Code = CapabilityStatement.TypeRestfulInteraction.Read }, new() { Code = CapabilityStatement.TypeRestfulInteraction.Create } } },
                        new() { Type = "AllergyIntolerance", Interaction = new() { new() { Code = CapabilityStatement.TypeRestfulInteraction.Read }, new() { Code = CapabilityStatement.TypeRestfulInteraction.SearchType } } },
                        new() { Type = "Condition", Interaction = new() { new() { Code = CapabilityStatement.TypeRestfulInteraction.Read }, new() { Code = CapabilityStatement.TypeRestfulInteraction.SearchType } } }
                    },
                    Operation = new List<CapabilityStatement.OperationComponent>
                    {
                        new()
                        {
                            Name = "check-safety",
                            Definition = "http://wasfa.jo/fhir/OperationDefinition/MedicationRequest-check-safety"
                        }
                    }
                }
            }
        };

        return Content(_serializer.SerializeToString(cs), "application/fhir+json");
    }

    /// <summary>
    /// Search Patient by identifier (National ID cleartext or hash) or name
    /// </summary>
    [HttpGet("Patient")]
    public async Task<IActionResult> SearchPatient([FromQuery] string? identifier, [FromQuery] string? name)
    {
        var query = _db.Patients.AsQueryable();

        if (!string.IsNullOrWhiteSpace(identifier))
        {
            var hash = NationalIdHasher.HashNationalId(identifier);
            query = query.Where(p => p.NationalIdHash == hash || p.NationalIdHash == identifier || p.FhirId == identifier);
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            var term = name.Trim().ToLowerInvariant();
            query = query.Where(p => p.FullNameEn.ToLower().Contains(term) || p.FullNameAr.Contains(term));
        }

        var patients = await query.ToListAsync();

        var bundle = new Bundle
        {
            Type = Bundle.BundleType.Searchset,
            Total = patients.Count,
            Entry = new List<Bundle.EntryComponent>()
        };

        foreach (var p in patients)
        {
            var raw = await _db.FhirResources.FirstOrDefaultAsync(r => r.ResourceType == "Patient" && r.FhirId == p.FhirId);
            if (raw != null)
            {
                var patientResource = _parser.Parse<Patient>(raw.Content);
                bundle.Entry.Add(new Bundle.EntryComponent { Resource = patientResource });
            }
        }

        // Audit Read
        _db.AuditLogs.Add(new AuditLogRecord
        {
            ActorId = User.Identity?.Name ?? "system-prescriber",
            Action = "read",
            ResourceType = "Patient",
            OccurredAt = DateTimeOffset.UtcNow,
            Detail = $"Patient search by identifier '{identifier}' or name '{name}'. Returned {patients.Count} matches."
        });
        await _db.SaveChangesAsync();

        return Content(_serializer.SerializeToString(bundle), "application/fhir+json");
    }

    /// <summary>
    /// Get single Patient by FHIR ID
    /// </summary>
    [HttpGet("Patient/{id}")]
    public async Task<IActionResult> GetPatient(string id)
    {
        var record = await _db.FhirResources.FirstOrDefaultAsync(r => r.ResourceType == "Patient" && r.FhirId == id);
        if (record == null)
        {
            return NotFound(new { error = $"Patient/{id} not found" });
        }

        _db.AuditLogs.Add(new AuditLogRecord
        {
            ActorId = User.Identity?.Name ?? "system-prescriber",
            Action = "read",
            ResourceType = "Patient",
            ResourceId = id,
            PatientFhirId = id,
            OccurredAt = DateTimeOffset.UtcNow,
            Detail = $"Direct read of Patient/{id}"
        });
        await _db.SaveChangesAsync();

        return Content(record.Content, "application/fhir+json");
    }

    /// <summary>
    /// Search MedicationRequest for a patient
    /// </summary>
    [HttpGet("MedicationRequest")]
    public async Task<IActionResult> SearchMedicationRequests([FromQuery] string? patient, [FromQuery] string? status)
    {
        if (string.IsNullOrWhiteSpace(patient))
        {
            return BadRequest(new { error = "Parameter 'patient' is required (e.g. ?patient=pat-1001)" });
        }

        var cleanPatientId = patient.Replace("Patient/", "");
        var query = _db.MedicationRequests.Where(m => m.PatientFhirId == cleanPatientId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(m => m.Status == status);
        }

        var items = (await query.ToListAsync()).OrderByDescending(m => m.AuthoredOn).ToList();

        var bundle = new Bundle
        {
            Type = Bundle.BundleType.Searchset,
            Total = items.Count,
            Entry = new List<Bundle.EntryComponent>()
        };

        foreach (var item in items)
        {
            var raw = await _db.FhirResources.FirstOrDefaultAsync(r => r.ResourceType == "MedicationRequest" && r.FhirId == item.FhirId);
            if (raw != null)
            {
                var parsed = _parser.Parse<MedicationRequest>(raw.Content);
                bundle.Entry.Add(new Bundle.EntryComponent { Resource = parsed });
            }
        }

        // Audit Read
        _db.AuditLogs.Add(new AuditLogRecord
        {
            ActorId = User.Identity?.Name ?? "system-prescriber",
            Action = "read",
            ResourceType = "MedicationRequest",
            PatientFhirId = cleanPatientId,
            OccurredAt = DateTimeOffset.UtcNow,
            Detail = $"Queried medication requests for Patient/{cleanPatientId}. Found {items.Count} items."
        });
        await _db.SaveChangesAsync();

        return Content(_serializer.SerializeToString(bundle), "application/fhir+json");
    }

    /// <summary>
    /// Custom Operation: POST /fhir/MedicationRequest/$check-safety
    /// Evaluates live safety checks (S1-S7) against patient history
    /// </summary>
    [HttpPost("MedicationRequest/$check-safety")]
    public async Task<IActionResult> CheckSafety([FromBody] SafetyCheckRequestDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.MoleculeCode))
        {
            return BadRequest(new { error = "Proposed molecule code is required." });
        }

        PatientContext patientContext;

        if (!string.IsNullOrWhiteSpace(dto.PatientFhirId))
        {
            var cleanId = dto.PatientFhirId.Replace("Patient/", "");
            var patient = await _db.Patients.FirstOrDefaultAsync(p => p.FhirId == cleanId);
            if (patient == null)
            {
                return NotFound(new { error = $"Patient/{cleanId} not found." });
            }

            // Calculate age
            var age = patient.BirthDate.HasValue 
                ? DateTime.UtcNow.Year - patient.BirthDate.Value.Year 
                : 40;

            // Load active meds
            var activeMeds = await _db.MedicationRequests
                .Where(m => m.PatientFhirId == cleanId && m.Status == "active")
                .Select(m => new ActiveMedication(m.FhirId, m.MoleculeCode, m.BrandName, m.Strength, m.Frequency, m.AuthoredOn, m.Status))
                .ToListAsync();

            // Load allergies
            var allergyResources = await _db.FhirResources
                .Where(r => r.ResourceType == "AllergyIntolerance")
                .ToListAsync();

            var allergies = new List<Wasfa.Domain.Model.Allergy>();
            foreach (var ar in allergyResources)
            {
                var parsed = _parser.Parse<AllergyIntolerance>(ar.Content);
                if (parsed.Patient?.Reference?.EndsWith(cleanId) == true)
                {
                    var code = parsed.Code?.Coding?.FirstOrDefault()?.Code ?? parsed.Code?.Text ?? "UNKNOWN";
                    var name = parsed.Code?.Coding?.FirstOrDefault()?.Display ?? parsed.Code?.Text ?? code;
                    var crit = parsed.Criticality?.ToString().ToLower() ?? "high";
                    allergies.Add(new Wasfa.Domain.Model.Allergy(code, name, crit, code));
                }
            }

            // Load conditions
            var conditionResources = await _db.FhirResources
                .Where(r => r.ResourceType == "Condition")
                .ToListAsync();

            var conditions = new List<string>();
            foreach (var cr in conditionResources)
            {
                var parsed = _parser.Parse<Condition>(cr.Content);
                if (parsed.Subject?.Reference?.EndsWith(cleanId) == true)
                {
                    var code = parsed.Code?.Coding?.FirstOrDefault()?.Code;
                    if (!string.IsNullOrWhiteSpace(code)) conditions.Add(code);
                }
            }

            patientContext = new PatientContext(
                age,
                patient.WeightKg,
                patient.Gender,
                patient.IsPregnant,
                patient.RenalImpairment,
                activeMeds,
                allergies,
                conditions);
        }
        else
        {
            // Inline context provided in DTO
            patientContext = new PatientContext(
                dto.AgeYears ?? 40,
                dto.WeightKg,
                dto.Gender ?? "male",
                dto.IsPregnant ?? false,
                dto.RenalImpairment ?? "Normal",
                dto.ActiveMedications ?? new List<ActiveMedication>(),
                dto.Allergies ?? new List<Wasfa.Domain.Model.Allergy>(),
                dto.Conditions ?? new List<string>());
        }

        var proposed = new ProposedMedication(
            dto.MoleculeCode,
            dto.BrandName,
            dto.DoseQuantity ?? 500m,
            dto.DoseUnit ?? "mg",
            dto.Frequency ?? "BID",
            dto.DurationDays ?? 7,
            dto.DailyDoseMg,
            dto.SingleDoseMg);

        var findings = await _safetyEngine.EvaluatePrescriptionAsync(patientContext, proposed);

        return Ok(new
        {
            status = "evaluated",
            findingsCount = findings.Count,
            hasContraindications = findings.Any(f => f.Severity == Severity.Contraindicated),
            hasMajorWarnings = findings.Any(f => f.Severity == Severity.Major),
            findings = findings.Select(f => new
            {
                code = f.Code,
                severity = f.Severity.ToString(),
                severityLevel = (int)f.Severity,
                titleEn = f.TitleEn,
                titleAr = f.TitleAr,
                explanationEn = f.ExplanationEn,
                explanationAr = f.ExplanationAr,
                involvedMolecules = f.InvolvedMolecules,
                source = f.Source,
                requiresReasonToOverride = f.RequiresReasonToOverride
            })
        });
    }

    /// <summary>
    /// Sign & Create a new MedicationRequest
    /// </summary>
    [HttpPost("MedicationRequest")]
    public async Task<IActionResult> CreateMedicationRequest([FromBody] CreatePrescriptionDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.PatientFhirId) || string.IsNullOrWhiteSpace(dto.MoleculeCode))
        {
            return BadRequest(new { error = "PatientFhirId and MoleculeCode are required." });
        }

        var cleanPatientId = dto.PatientFhirId.Replace("Patient/", "");
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.FhirId == cleanPatientId);
        if (patient == null)
        {
            return NotFound(new { error = $"Patient/{cleanPatientId} not found." });
        }

        var shortCode = $"WASFA-{Random.Shared.Next(1000, 9999)}";
        var requestId = $"req-{Guid.NewGuid():N}".Substring(0, 16);

        var projection = new MedicationRequestProjection
        {
            FhirId = requestId,
            PatientFhirId = cleanPatientId,
            PrescriberId = dto.PrescriberId ?? "doc-dr-omar",
            MoleculeCode = dto.MoleculeCode,
            BrandName = dto.BrandName,
            Strength = dto.Strength ?? "500 mg",
            DoseText = dto.Strength ?? "500 mg",
            Frequency = dto.Frequency ?? "BID",
            DurationDays = dto.DurationDays ?? 7,
            Status = "active",
            AuthoredOn = DateTimeOffset.UtcNow,
            ShortCode = shortCode
        };
        _db.MedicationRequests.Add(projection);

        var medReq = new MedicationRequest
        {
            Id = requestId,
            Status = MedicationRequest.MedicationrequestStatus.Active,
            Intent = MedicationRequest.MedicationRequestIntent.Order,
            Subject = new ResourceReference($"Patient/{cleanPatientId}", patient.FullNameEn),
            Requester = new ResourceReference($"Practitioner/{projection.PrescriberId}", dto.PrescriberName ?? "Dr. Omar Al-Kayyali"),
            AuthoredOn = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            Medication = new CodeableConcept
            {
                Text = $"{dto.BrandName ?? dto.MoleculeCode} ({dto.Strength ?? "500 mg"})",
                Coding = new List<Coding>
                {
                    new("http://www.whocc.no/atc", dto.MoleculeCode),
                    new("http://wasfa.jo/fhir/brands", dto.BrandName ?? dto.MoleculeCode)
                }
            },
            Identifier = new List<Identifier>
            {
                new("http://wasfa.jo/fhir/prescription-code", shortCode)
            }
        };

        var json = _serializer.SerializeToString(medReq);
        _db.FhirResources.Add(new FhirResourceRecord
        {
            ResourceType = "MedicationRequest",
            FhirId = requestId,
            VersionId = 1,
            Content = json,
            LastUpdated = DateTimeOffset.UtcNow
        });

        // Record any safety overrides associated with this signing
        if (dto.Overrides != null)
        {
            foreach (var ovr in dto.Overrides)
            {
                _db.SafetyOverrides.Add(new SafetyOverrideRecord
                {
                    RequestFhirId = requestId,
                    FindingCode = ovr.FindingCode,
                    Severity = ovr.Severity,
                    Reason = ovr.Reason,
                    OverriddenBy = dto.PrescriberId ?? "doc-dr-omar",
                    OverriddenAt = DateTimeOffset.UtcNow
                });
            }
        }

        // Audit Write
        _db.AuditLogs.Add(new AuditLogRecord
        {
            ActorId = dto.PrescriberId ?? "doc-dr-omar",
            Action = "write",
            ResourceType = "MedicationRequest",
            ResourceId = requestId,
            PatientFhirId = cleanPatientId,
            OccurredAt = DateTimeOffset.UtcNow,
            Detail = $"Signed prescription {shortCode} for drug {dto.BrandName ?? dto.MoleculeCode} with {dto.Overrides?.Count ?? 0} safety overrides."
        });

        await _db.SaveChangesAsync();

        return Created($"/fhir/MedicationRequest/{requestId}", new
        {
            id = requestId,
            shortCode = shortCode,
            status = "active",
            authoredOn = projection.AuthoredOn,
            resource = medReq
        });
    }

    /// <summary>
    /// POST /fhir/MedicationDispense
    /// Pharmacist dispensing execution (closes the loop)
    /// </summary>
    [HttpPost("MedicationDispense")]
    public async Task<IActionResult> CreateMedicationDispense([FromBody] CreateDispenseDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.RequestFhirId))
        {
            return BadRequest(new { error = "RequestFhirId is required." });
        }

        var medReq = await _db.MedicationRequests.FirstOrDefaultAsync(m => m.FhirId == dto.RequestFhirId || m.ShortCode == dto.RequestFhirId);
        if (medReq == null)
        {
            return NotFound(new { error = $"Prescription {dto.RequestFhirId} not found." });
        }

        var dispenseId = $"disp-{Guid.NewGuid():N}".Substring(0, 16);

        var dispenseProj = new MedicationDispenseProjection
        {
            FhirId = dispenseId,
            RequestFhirId = medReq.FhirId,
            PharmacyId = dto.PharmacyId ?? "pharm-amman-center",
            DispensedAt = DateTimeOffset.UtcNow,
            Quantity = dto.Quantity ?? 1,
            SubstitutedBrand = dto.SubstitutedBrand,
            SubstitutionReason = dto.SubstitutionReason
        };
        _db.MedicationDispenses.Add(dispenseProj);

        // Update prescription status to completed if fully dispensed
        if (dto.IsCompleteDispense ?? true)
        {
            medReq.Status = "completed";
        }

        var fhirDispense = new MedicationDispense
        {
            Id = dispenseId,
            Status = MedicationDispense.MedicationDispenseStatusCodes.Completed,
            AuthorizingPrescription = new List<ResourceReference>
            {
                new($"MedicationRequest/{medReq.FhirId}")
            },
            WhenHandedOver = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:sszzz"),
            Quantity = new Quantity(dispenseProj.Quantity, "pack")
        };

        if (!string.IsNullOrWhiteSpace(dto.SubstitutedBrand))
        {
            fhirDispense.Substitution = new MedicationDispense.SubstitutionComponent
            {
                WasSubstituted = true,
                Reason = new List<CodeableConcept> { new("http://terminology.hl7.org/CodeSystem/v3-SubstanceAdminSubstitution", dto.SubstitutionReason ?? "Generic substitution") }
            };
        }

        _db.FhirResources.Add(new FhirResourceRecord
        {
            ResourceType = "MedicationDispense",
            FhirId = dispenseId,
            VersionId = 1,
            Content = _serializer.SerializeToString(fhirDispense),
            LastUpdated = DateTimeOffset.UtcNow
        });

        // Audit Dispense
        _db.AuditLogs.Add(new AuditLogRecord
        {
            ActorId = dto.PharmacyId ?? "pharm-amman-center",
            Action = "dispense",
            ResourceType = "MedicationDispense",
            ResourceId = dispenseId,
            PatientFhirId = medReq.PatientFhirId,
            OccurredAt = DateTimeOffset.UtcNow,
            Detail = $"Dispensed prescription {medReq.ShortCode} (Quantity: {dispenseProj.Quantity}). Substitution: {dto.SubstitutedBrand ?? "None"}."
        });

        await _db.SaveChangesAsync();

        return Created($"/fhir/MedicationDispense/{dispenseId}", new
        {
            id = dispenseId,
            requestFhirId = medReq.FhirId,
            shortCode = medReq.ShortCode,
            status = "completed",
            dispensedAt = dispenseProj.DispensedAt,
            substitutedBrand = dto.SubstitutedBrand
        });
    }

    /// <summary>
    /// Search Allergies for a patient
    /// </summary>
    [HttpGet("AllergyIntolerance")]
    public async Task<IActionResult> SearchAllergies([FromQuery] string? patient)
    {
        if (string.IsNullOrWhiteSpace(patient))
        {
            return BadRequest(new { error = "Parameter 'patient' is required." });
        }

        var cleanId = patient.Replace("Patient/", "");
        var rawAllergies = await _db.FhirResources
            .Where(r => r.ResourceType == "AllergyIntolerance")
            .ToListAsync();

        var bundle = new Bundle
        {
            Type = Bundle.BundleType.Searchset,
            Entry = new List<Bundle.EntryComponent>()
        };

        foreach (var r in rawAllergies)
        {
            var parsed = _parser.Parse<AllergyIntolerance>(r.Content);
            if (parsed.Patient?.Reference?.EndsWith(cleanId) == true)
            {
                bundle.Entry.Add(new Bundle.EntryComponent { Resource = parsed });
            }
        }

        bundle.Total = bundle.Entry.Count;
        return Content(_serializer.SerializeToString(bundle), "application/fhir+json");
    }

    /// <summary>
    /// Search Conditions for a patient
    /// </summary>
    [HttpGet("Condition")]
    public async Task<IActionResult> SearchConditions([FromQuery] string? patient)
    {
        if (string.IsNullOrWhiteSpace(patient))
        {
            return BadRequest(new { error = "Parameter 'patient' is required." });
        }

        var cleanId = patient.Replace("Patient/", "");
        var rawConditions = await _db.FhirResources
            .Where(r => r.ResourceType == "Condition")
            .ToListAsync();

        var bundle = new Bundle
        {
            Type = Bundle.BundleType.Searchset,
            Entry = new List<Bundle.EntryComponent>()
        };

        foreach (var r in rawConditions)
        {
            var parsed = _parser.Parse<Condition>(r.Content);
            if (parsed.Subject?.Reference?.EndsWith(cleanId) == true)
            {
                bundle.Entry.Add(new Bundle.EntryComponent { Resource = parsed });
            }
        }

        bundle.Total = bundle.Entry.Count;
        return Content(_serializer.SerializeToString(bundle), "application/fhir+json");
    }
}

public sealed class SafetyCheckRequestDto
{
    public string? PatientFhirId { get; set; }
    public string MoleculeCode { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public decimal? DoseQuantity { get; set; }
    public string? DoseUnit { get; set; }
    public string? Frequency { get; set; }
    public int? DurationDays { get; set; }
    public decimal? DailyDoseMg { get; set; }
    public decimal? SingleDoseMg { get; set; }

    // Optional direct context
    public int? AgeYears { get; set; }
    public decimal? WeightKg { get; set; }
    public string? Gender { get; set; }
    public bool? IsPregnant { get; set; }
    public string? RenalImpairment { get; set; }
    public List<ActiveMedication>? ActiveMedications { get; set; }
    public List<Wasfa.Domain.Model.Allergy>? Allergies { get; set; }
    public List<string>? Conditions { get; set; }
}

public sealed class CreatePrescriptionDto
{
    public string PatientFhirId { get; set; } = string.Empty;
    public string? PrescriberId { get; set; }
    public string? PrescriberName { get; set; }
    public string MoleculeCode { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public string? Strength { get; set; }
    public string? Frequency { get; set; }
    public int? DurationDays { get; set; }
    public List<OverrideItemDto>? Overrides { get; set; }
}

public sealed class OverrideItemDto
{
    public string FindingCode { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public sealed class CreateDispenseDto
{
    public string RequestFhirId { get; set; } = string.Empty;
    public string? PharmacyId { get; set; }
    public decimal? Quantity { get; set; }
    public string? SubstitutedBrand { get; set; }
    public string? SubstitutionReason { get; set; }
    public bool? IsCompleteDispense { get; set; }
}
