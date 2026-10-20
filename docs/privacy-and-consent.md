# Privacy, Security, Consent, and Audit Architecture

## 1. Regulatory Context in Jordan
Healthcare information security in Jordan is governed by the **Jordan Personal Data Protection Law (PDPL) of 2023** and medical ethics directives from the Ministry of Health and Jordan Medical Association. Health records are classified as sensitive personal data requiring heightened technical and administrative safeguards.

---

## 2. Cryptographic Protection of Patient Identity

### National ID Hashing (HMAC-SHA256)
- **Vulnerability:** Storing raw Jordanian National ID numbers (الرقم الوطني) exposes citizens to identity theft and correlation attacks across unrelated public databases.
- **Wasfa Solution:**
  1. The raw national ID is never stored in any table or FHIR payload.
  2. At the ingress boundary, the national ID is passed through HMAC-SHA256 with a cryptographically secure salt stored in server environment variables:
     $$\text{NationalIdHash} = \text{HMAC-SHA256}(\text{NationalId}, \text{Salt})$$
  3. All database queries, indexing, and FHIR identifier lookups execute strictly against `national_id_hash`.

---

## 3. Dynamic Patient Consent Architecture

In outpatient care, the patient owns their health information. Wasfa enforces an active consent model:

```
[Patient] 
   │  Grants access (or revokes immediately) via Patient App
   ▼
[Consent Record] ───► Scope: 'read_medications' | 'read_all'
   │                  Target Organization: 'clinic_amman_heart_center'
   │                  Expiry / Revocation Timestamp
   ▼
[Wasfa Consent Middleware]
   │  Evaluates incoming Practitioner JWT + requested Patient ID
   ├── If Active Consent Present ──► Allow FHIR Read/Write
   └── If Consent Revoked/Missing ──► HTTP 403 Forbidden ("Patient consent required")
```

- **Revocation:** Patients can immediately revoke a clinic's or pharmacy's access from their mobile app. Revocation invalidates subsequent read requests instantaneously.
- **Emergency Override:** In emergency scenarios, a documented break-glass mechanism allows authorized emergency clinicians to query the active medication list, generating a high-priority audit event flagged for review.

---

## 4. Immutable, Append-Only Audit Logging

Every read, write, clinical override, and dispense action is logged to the `audit_log` table.

### Database-Level Enforcement
```sql
REVOKE UPDATE, DELETE ON audit_log FROM PUBLIC;
```
Even database administrators cannot silently edit or remove historic audit entries without altering physical database logs.

### Audit Entry Payload
Each entry records:
- `actor_id`: Authenticated user / practitioner ID.
- `action`: `read` | `write` | `override` | `dispense`.
- `resource_type` and `resource_id`: Target FHIR entity.
- `patient_fhir_id`: Affected patient identifier.
- `ip`: Client IP address.
- `occurred_at`: UTC timestamp.
- `detail`: JSON metadata, including override justifications, old vs new values, and client user-agent.

---

## 5. Synthetic Patient Guarantee
The public repository and demonstration environments strictly use **100% synthetic fictitious patients** (e.g., "Ahmad Mansoor", "Fatima Al-Zahra", "Omar Haddad"). No real-world patient records, identifiable health data, or real Jordanian national IDs are ever committed to version control.
