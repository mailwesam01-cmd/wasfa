using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Wasfa.Domain.Entities;
using Wasfa.Infrastructure.Persistence;

namespace Wasfa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SafetyOverrideController : ControllerBase
{
    private readonly WasfaDbContext _db;

    public SafetyOverrideController(WasfaDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    public async Task<IActionResult> RecordOverride([FromBody] RecordOverrideDto dto)
    {
        if (dto == null || string.IsNullOrWhiteSpace(dto.RequestFhirId) || string.IsNullOrWhiteSpace(dto.Reason))
        {
            return BadRequest(new { error = "RequestFhirId and non-empty Reason are required to record clinical override." });
        }

        var record = new SafetyOverrideRecord
        {
            RequestFhirId = dto.RequestFhirId,
            FindingCode = dto.FindingCode ?? "CLINICAL.OVERRIDE",
            Severity = dto.Severity ?? "Major",
            Reason = dto.Reason.Trim(),
            OverriddenBy = dto.OverriddenBy ?? "doc-dr-omar",
            OverriddenAt = DateTimeOffset.UtcNow
        };

        _db.SafetyOverrides.Add(record);

        _db.AuditLogs.Add(new AuditLogRecord
        {
            ActorId = record.OverriddenBy,
            Action = "override",
            ResourceType = "SafetyOverride",
            ResourceId = record.Id.ToString(),
            OccurredAt = DateTimeOffset.UtcNow,
            Detail = $"Clinical safety warning override recorded for prescription {dto.RequestFhirId}. Reason: '{record.Reason}'."
        });

        await _db.SaveChangesAsync();

        return Created($"/api/safetyoverride/{record.Id}", record);
    }

    [HttpGet]
    public async Task<IActionResult> GetOverrides([FromQuery] string? requestId)
    {
        var query = _db.SafetyOverrides.AsQueryable();
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            query = query.Where(o => o.RequestFhirId == requestId);
        }

        var results = (await query.ToListAsync()).OrderByDescending(o => o.OverriddenAt).ToList();
        return Ok(results);
    }
}

public sealed class RecordOverrideDto
{
    public string RequestFhirId { get; set; } = string.Empty;
    public string? FindingCode { get; set; }
    public string? Severity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? OverriddenBy { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class ConsentController : ControllerBase
{
    private readonly WasfaDbContext _db;

    public ConsentController(WasfaDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetConsents([FromQuery] string patientId)
    {
        var cleanId = patientId.Replace("Patient/", "");
        var items = (await _db.Consents
            .Where(c => c.PatientFhirId == cleanId)
            .ToListAsync())
            .OrderByDescending(c => c.GrantedAt)
            .ToList();

        return Ok(items);
    }

    [HttpPost("grant")]
    public async Task<IActionResult> GrantConsent([FromBody] ConsentDto dto)
    {
        var cleanId = dto.PatientFhirId.Replace("Patient/", "");
        var record = new ConsentRecord
        {
            PatientFhirId = cleanId,
            GranteeOrgId = dto.GranteeOrgId,
            Scope = dto.Scope ?? "read_medications",
            GrantedAt = DateTimeOffset.UtcNow
        };

        _db.Consents.Add(record);

        _db.AuditLogs.Add(new AuditLogRecord
        {
            ActorId = $"Patient/{cleanId}",
            Action = "grant_consent",
            ResourceType = "Consent",
            PatientFhirId = cleanId,
            OccurredAt = DateTimeOffset.UtcNow,
            Detail = $"Granted consent to {dto.GranteeOrgId} with scope {record.Scope}."
        });

        await _db.SaveChangesAsync();
        return Ok(record);
    }

    [HttpPost("revoke")]
    public async Task<IActionResult> RevokeConsent([FromBody] RevokeConsentDto dto)
    {
        var consent = await _db.Consents.FirstOrDefaultAsync(c => c.Id == dto.ConsentId);
        if (consent == null)
        {
            return NotFound(new { error = "Consent record not found." });
        }

        consent.RevokedAt = DateTimeOffset.UtcNow;

        _db.AuditLogs.Add(new AuditLogRecord
        {
            ActorId = $"Patient/{consent.PatientFhirId}",
            Action = "revoke_consent",
            ResourceType = "Consent",
            PatientFhirId = consent.PatientFhirId,
            OccurredAt = DateTimeOffset.UtcNow,
            Detail = $"Revoked consent for {consent.GranteeOrgId}."
        });

        await _db.SaveChangesAsync();
        return Ok(new { message = "Consent revoked successfully.", revokedAt = consent.RevokedAt });
    }
}

public sealed class ConsentDto
{
    public string PatientFhirId { get; set; } = string.Empty;
    public string GranteeOrgId { get; set; } = string.Empty;
    public string? Scope { get; set; }
}

public sealed class RevokeConsentDto
{
    public Guid ConsentId { get; set; }
}

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly WasfaDbContext _db;

    public AuditController(WasfaDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditLogs([FromQuery] string? patientId, [FromQuery] int limit = 50)
    {
        var query = _db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(patientId))
        {
            var cleanId = patientId.Replace("Patient/", "");
            query = query.Where(a => a.PatientFhirId == cleanId);
        }

        var logs = await query
            .OrderByDescending(a => a.Id)
            .Take(limit)
            .ToListAsync();

        return Ok(logs);
    }
}
