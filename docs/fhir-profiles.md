# Wasfa HL7® FHIR® R4 Profiles & Resource Mapping

Wasfa adopts the **HL7® FHIR® Release 4 (R4)** standard for all core clinical resources.

## 1. Supported Resources & Key Elements

### 1. `Patient`
Represents the receiving patient.
- `identifier`: System `urn:wasfa:identifier:national-id-hash`. Stores HMAC-SHA256 salted hash of Jordan National ID. Cleartext National ID is never stored.
- `name`: Family and given names in Arabic and Latin.
- `birthDate`: ISO 8601 date (used for pediatric/geriatric safety rule calculation).
- `gender`: `male` | `female` | `other` | `unknown`.
- `extension:pregnancy-status`: Custom extension `http://wasfa.jo/fhir/StructureDefinition/pregnancy-status` (`valueBoolean`, `valueDate` expected due date).
- `extension:renal-impairment`: Custom extension `http://wasfa.jo/fhir/StructureDefinition/renal-impairment` (e.g. `stage_4_severe`, `egfr_25`).

### 2. `Practitioner`
Represents the prescribing physician or dispensing pharmacist.
- `identifier`: Jordan Medical Association (JMA) or Jordan Pharmacists Association (JPA) license number.
- `name`: Clinical practitioner name.
- `qualification`: Specialty / clinical degree.

### 3. `MedicationRequest`
Represents an outpatient prescription order.
- `status`: `active` | `completed` | `cancelled` | `entered-in-error`.
- `intent`: `order`.
- `medicationCodeableConcept`:
  - `coding`:
    - System `http://www.whocc.no/atc` (e.g., `B01AA03` for Warfarin).
    - System `http://www.nlm.nih.gov/research/umls/rxnorm` (e.g., `11289`).
    - System `http://jfda.jo/codes/brands` (Local brand code and trade name).
  - `text`: Human-readable molecule and brand representation (e.g., `Warfarin (Coumadin) 5 mg Tablet`).
- `subject`: Reference to `Patient/fhir_id`.
- `requester`: Reference to `Practitioner/fhir_id`.
- `dosageInstruction`:
  - `text`: e.g., "Take 1 tablet once daily in the evening".
  - `timing`: Repeat frequency.
  - `doseAndRate`: Quantity and unit (`mg`, `ml`).
- `extension:safety-override`: Array of overridden safety warnings with recorded justifications.
- `identifier:short-code`: 8-character human-friendly alphanumeric dispense code + QR code payload.

### 4. `MedicationDispense`
Represents the actual pharmacy fulfillment.
- `status`: `preparation` | `in-progress` | `completed` | `stopped`.
- `authorizingPrescription`: Reference to `MedicationRequest/fhir_id`.
- `performer`: Reference to `Practitioner` (dispensing pharmacist).
- `whenHandedOver`: Timestamp of physical handover.
- `quantity`: Quantity dispensed.
- `substitution`:
  - `wasSubstituted`: Boolean.
  - `type`: Generic substitution code.
  - `reason`: Rationale (e.g., "Prescribed brand unavailable, generic equivalent dispensed").

### 5. `AllergyIntolerance`
Represents drug sensitivities and active allergies.
- `clinicalStatus`: `active`.
- `verificationStatus`: `confirmed`.
- `type`: `allergy`.
- `category`: `medication`.
- `code`: SNOMED-CT / ATC cross-reactivity group (e.g., `group_beta_lactam_penicillins`).
- `patient`: Reference to `Patient`.

### 6. `Condition`
Represents documented chronic diagnoses (e.g., Chronic Kidney Disease, Peptic Ulcer Disease).
- `code`: ICD-10 / SNOMED coding.
- `subject`: Reference to `Patient`.

---

## 2. Extended Operations

### `POST /fhir/MedicationRequest/$check-safety`
Performs an instantaneous, read-only evaluation of a proposed medication against the patient's real-time clinical context.

**Request Body (FHIR `Parameters`):**
```json
{
  "resourceType": "Parameters",
  "parameter": [
    {
      "name": "patient",
      "valueReference": { "reference": "Patient/pat-1001" }
    },
    {
      "name": "proposedMedication",
      "resource": {
        "resourceType": "MedicationRequest",
        "status": "draft",
        "intent": "order",
        "medicationCodeableConcept": {
          "coding": [
            { "system": "http://www.whocc.no/atc", "code": "M01AB05", "display": "Diclofenac" },
            { "system": "http://jfda.jo/codes/brands", "code": "VOLT-50", "display": "Voltaren" }
          ]
        },
        "dosageInstruction": [{ "doseAndRate": [{ "doseQuantity": { "value": 50, "unit": "mg" } }] }]
      }
    }
  ]
}
```

**Response Body (FHIR `OperationOutcome` with enriched findings):**
Returns structured issues, severity levels, clinical explanations in English and Arabic, and citations.
