# Wasfa Positioning & Alignment with National Health IT in Jordan

## Executive Summary
Jordan possesses one of the most mature digital health infrastructures in the Middle East, spearheaded by **Electronic Health Solutions (EHS)** and the nationwide **Hakeem (حكيم)** program launched in 2009. Hakeem deploys an Electronic Health Record (EHR) ecosystem primarily based on VistA across military (Royal Medical Services), Ministry of Health (MoH), university hospitals (such as King Abdullah University Hospital), and participating public healthcare centers.

However, an acute systemic vulnerability remains in the outpatient patient journey: **the private outpatient and community pharmacy sector**, alongside **patient-held consolidated medication records**.

**Wasfa (وصفة)** is deliberately architected not as an alternative or competitor to Hakeem, but as an **interoperable outpatient medication-safety extension** built on international **HL7® FHIR® R4** standards that directly bridges the private-clinic-to-community-pharmacy divide and puts a verified, consolidated medication card into the hands of the patient.

---

## 1. The Landscape: Strengths and Gaps in Jordanian Healthcare

```
┌────────────────────────────────────────────────────────────────────────┐
│                   PUBLIC & PARTICIPATING SECTOR                        │
│                                                                        │
│   Ministry of Health Hospitals  ·  Royal Medical Services  ·  JUH/KAUH │
│                                                                        │
│                [ Hakeem National EHR (EHS / VistA) ]                  │
│                Connected facilities share inpatient history            │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
                         THE OUTPATIENT DISCONNECT
                                    │
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                      PRIVATE & COMMUNITY SECTOR                        │
│                                                                        │
│   ~10,000+ Independent Private Clinics & Polyclinics                   │
│   ~4,000+ Community Pharmacies                                         │
│   Cross-border Medical Tourism Patients (~250k+ annually)              │
│                                                                        │
│   STATUS QUO: Handwritten paper prescriptions                          │
│               No shared medication list                                │
│               No real-time cross-clinic interaction checks             │
│               No closed-loop dispensing confirmation                   │
└────────────────────────────────────────────────────────────────────────┘
```

### The Clinical Reality of Polypharmacy in Jordan
- **Prevalence of Non-Communicable Diseases (NCDs):** Hypertension, Type 2 Diabetes, and Ischemic Heart Disease affect over 30% of adults over 45 in Jordan. Patients routinely take 4 to 8 distinct chronic medications daily.
- **Provider Fragmentation:** A typical patient may see a private cardiologist in Amman, an endocrinologist in Irbid, and their local general practitioner, filling prescriptions at independent neighborhood pharmacies.
- **Paper Blindness:** Each physician sees only what the patient remembers to articulate. Handwritten brand names are easily conflated with look-alike/sound-alike alternatives (LASA errors), and critical drug-drug interactions (e.g., Warfarin + Diclofenac, ACE Inhibitors + Spironolactone) go unchecked before dispensing.

---

## 2. Strategic Positioning of Wasfa

| Dimension | Hakeem (National EHR) | Wasfa (وصفة) |
|---|---|---|
| **Primary Domain** | Institutional EHR (Hospitals, Comprehensive Clinics, MoH) | Outpatient E-Prescribing, Community Safety, & Patient Empowerment |
| **Target Users** | Hospital clinicians, inpatient nurses, institutional pharmacists | Independent private physicians, community pharmacists, chronic disease patients |
| **Interoperability Standard** | VistA core / proprietary integration adapters | **HL7® FHIR® Release 4** native RESTful resources |
| **Patient Ownership** | Facility-centric medical record accessed by authorized clinical staff | **Patient-held active medication card** with revocable clinic consent |
| **Safety Verification** | Inpatient order checks within participating facilities | **Cross-facility real-time interaction & duplicate therapy checks** |
| **Relationship** | Foundational national backbone | **Complementary FHIR satellite / private-sector hand-off layer** |

---

## 3. FHIR-Based Hand-Off Architecture

Wasfa communicates entirely using standard FHIR resources:
- `Patient` (mapped to Jordanian National ID hash)
- `MedicationRequest` (prescriptions with ATC / RxNorm and local JFDA brand metadata)
- `MedicationDispense` (community pharmacy closed-loop verification)
- `AllergyIntolerance` (standardized SNOMED/ICD allergy cross-reactivity)

Whenever an authorized facility operating Hakeem or a private hospital EHR desires to synchronize or query an outpatient medication record, Wasfa exposes standard FHIR endpoints (`/fhir/MedicationRequest?patient=...`, `/fhir/MedicationRequest/$check-safety`). This transforms Wasfa into an **interoperability bridge** rather than a data silo.

---

## 4. Summary: The Senior Value Proposition
By recognizing the legitimate existence and achievements of Hakeem while addressing the demonstrable private-sector outpatient and patient-held gap with HL7 FHIR, Wasfa represents engineering maturity: **solving an unaddressed clinical safety risk while respecting the national digital health ecosystem.**
