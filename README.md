# وصفة · Wasfa
### *One patient. One medication list. Checked before it's dispensed.*

> ⚠️ **[READ THE CLINICAL DISCLAIMER BEFORE RUNNING](./CLINICAL-DISCLAIMER.md)** — This is a demonstration platform, not a certified clinical decision-support system.

[![Build & Tests](https://img.shields.io/badge/build-passing-brightgreen?style=flat-square)](#)
[![FHIR R4](https://img.shields.io/badge/FHIR-R4-1B5E5A?style=flat-square)](https://hl7.org/fhir/R4/)
[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square)](https://dotnet.microsoft.com)
[![Flutter](https://img.shields.io/badge/Flutter-3.x-02569B?style=flat-square)](https://flutter.dev)
[![Angular](https://img.shields.io/badge/Angular-18-DD0031?style=flat-square)](https://angular.io)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow?style=flat-square)](./LICENSE)

---

**Wasfa (وصفة)** is an open-source, standards-based e-prescription and medication-safety platform
built on **HL7® FHIR® Release 4**. It is designed to close the medication-safety gap in Jordan's
private outpatient sector — the private clinic to community pharmacy pathway — where most
prescriptions are still written on paper and no patient holds a consolidated, verified medication
list.

---

## The Problem

Jordan has a mature national electronic health record through the **Hakeem** program
(Electronic Health Solutions), serving public healthcare facilities. But the path most patients
actually take — *private clinic → private pharmacy* — is still largely paper-based, and no
patient currently holds a consolidated record of their own medications.

The clinical consequence is dangerous **polypharmacy without visibility**:

- A cardiologist in Amman prescribes Warfarin.
- A GP in Zarqa prescribes Ibuprofen.
- No system checks for the interaction. The pharmacist sees only the second prescription.
- The patient goes home with a **major drug–drug interaction** that is trivially detectable by software.

This happens every day. Wasfa exists to make it stop.

---

## How Wasfa Complements Hakeem — Not Competes With It

Wasfa is explicitly positioned **not** as a competitor to Hakeem or Electronic Health Solutions.
It addresses the outpatient private-sector segment and the patient-held medication record —
two gaps that Hakeem's publicly documented programme does not address.

Because Wasfa is built natively on **HL7 FHIR R4**, it can hand off records to any
FHIR-compliant system, including a future FHIR interface from Hakeem, without reengineering.
Wasfa becomes a FHIR satellite: filling the private-sector gap today, interoperable with
national infrastructure whenever that interface becomes available.

See [`docs/positioning.md`](./docs/positioning.md) for the full strategic positioning.

---

## Quickstart (Demo in 60 Seconds)

> **Requirements:** .NET 10 SDK

```bash
# Clone and run the API (uses SQLite, zero configuration required)
git clone https://github.com/<you>/wasfa
cd wasfa

dotnet run --project src/Wasfa.Api

# API now running at http://localhost:5000
# Demo metadata: http://localhost:5000/
# Safety check: POST http://localhost:5000/fhir/MedicationRequest/$check-safety
```

### Run the clinical safety test suite
```bash
dotnet test
# Expected: 10/10 passing — 9 clinical scenarios + 1 API integration test
```

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  PRESCRIBER (Angular)                                                       │
│  Patient header · Current medication list (always visible) · Drug search   │
│  → LIVE SAFETY CHECK → Warning stack (severity-ordered) → Sign → QR issued │
└──────────────────────────────┬──────────────────────────────────────────────┘
                               │ POST /fhir/MedicationRequest
                               ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│  ASP.NET CORE API  (Wasfa.Api)                                              │
│                                                                             │
│  ┌───────────────────────────────────────────────────────────────────────┐  │
│  │  SAFETY ENGINE  (Wasfa.Safety)                                        │  │
│  │  S1 Drug–drug interactions  │ S2 Duplicate therapy                    │  │
│  │  S3 Allergy cross-reactivity│ S4 Dose range (paediatric / adult)      │  │
│  │  S5 Renal / hepatic flags   │ S6 Pregnancy / lactation category       │  │
│  │  S7 Paediatric / geriatric Beers criteria                             │  │
│  │                                                                       │  │
│  │  Every finding → Severity · Bilingual explanation · Citation          │  │
│  │  Override requires mandatory written reason (audited, never silent)   │  │
│  └───────────────────────────────────────────────────────────────────────┘  │
│                                                                             │
│  FHIR Store (PostgreSQL JSONB / SQLite dev)  ·  Consent engine             │
│  Immutable append-only audit log  ·  HMAC-hashed national ID               │
└──────────────┬──────────────────────────────────────┬───────────────────────┘
               ▼                                      ▼
┌─────────────────────────────┐       ┌──────────────────────────────────────┐
│  PHARMACY (Flutter/web)     │       │  PATIENT (Flutter)                   │
│  Scan QR → verify → dispense│       │  Medication list · Dose timeline     │
│  → MedicationDispense FHIR  │       │  Refill alerts · Medication card PDF │
│  Partial dispense supported │       │  Grant/revoke clinic access          │
└─────────────────────────────┘       └──────────────────────────────────────┘
```

---

## Clinical Safety Engine

The engine evaluates 7 safety rules before any prescription is signed:

| Rule | Code | Description |
|---|---|---|
| Drug–Drug Interaction | S1 | Bidirectional interaction graph with graded severity |
| Duplicate Therapy | S2 | Same ATC Level-5 molecule under two brand names |
| Allergy Cross-Reactivity | S3 | Penicillin → Amoxicillin, Sulfa → SMX, etc. |
| Dose Range | S4 | Min/max per age band and weight-based paediatric |
| Renal / Hepatic Flags | S5 | Metformin in CKD4+, NSAIDs in renal failure |
| Pregnancy / Lactation | S6 | Category X absolute contraindications |
| Age-Appropriateness | S7 | Aspirin/Reye under 16, Beers criteria ≥65 |

Every finding carries:
- **Severity level:** `Contraindicated` → `Major` → `Moderate` → `Minor`
- **Bilingual explanation** (English + Arabic) in plain language
- **Clinical citation** (BNF, FDA, KDIGO, Beers Criteria, WAO)
- **Override requirement:** `Contraindicated` and `Major` findings require a typed mandatory reason

### Clinical Scenario Test Suite

The `dotnet test` suite drives the safety engine directly against 9 clinical scenarios:

| Scenario | Expected Finding |
|---|---|
| Warfarin + Ibuprofen | `DDI.WARFARIN.NSAID` — **Major** |
| Two brands same molecule | `DUPLICATE.THERAPY.*` — **Major** |
| Penicillin allergy + Amoxicillin | `ALLERGY.CROSS.ALLERGY_BETA_LACTAMS` — **Major** |
| Child 4 yrs + adult Amoxicillin dose | `DOSE.PAEDIATRIC.EXCEEDED_DAILY` — **Major** |
| Pregnancy + Isotretinoin | `PREG.CATEGORY_X.*` — **Contraindicated** |
| Simvastatin + Clarithromycin | `DDI.SIMVASTATIN.CLARITHROMYCIN` — **Contraindicated** |
| Metformin + Severe CKD | `RENAL.CONTRAINDICATION.METFORMIN` — **Contraindicated** |
| Aspirin + child 9 yrs | `AGE.PAEDIATRIC.REYE_SYNDROME` — **Contraindicated** |
| Paracetamol + Metformin (healthy adult) | **Zero findings** ← false-positive guard |

The false-positive guard is as important as the true-positive cases. Alert fatigue is a proven cause of clinicians dismissing all warnings — including life-threatening ones. CI fails on either a missed finding or a spurious one.

---

## Drug Knowledge Base

The bundled knowledge base is **openly licensed and clearly scoped** for demonstration and
testing only. It covers approximately 30 high-frequency molecules used in Jordan with:

- **ATC codes** (WHO Collaborating Centre, Oslo)
- **RxNorm identifiers** (NIH/NLM)
- **JFDA local brand mappings** (Jordan Food and Drug Administration registered generics and originators)
- Interaction rules curated from **BNF Appendix 1**, **FDA Black Box Warnings**, **Beers Criteria 2023**

The engine accesses all data through an `IDrugKnowledgeProvider` interface. In a real deployment,
a licensed clinical database (First Databank, Wolters Kluwer Lexicomp, Micromedex) would replace
the JSON provider without changing a single line of engine code.

See [`docs/data-provenance.md`](./docs/data-provenance.md) for the source of every record.

---

## Demo Patients (Synthetic Only)

All patients are fictitious. The demo includes 9 pre-seeded scenarios:

| Patient | Scenario |
|---|---|
| Ahmad Al-Majali | Warfarin + Ibuprofen → Major bleeding DDI |
| Fatima Al-Zoubi | Same molecule under two brand names → Duplicate therapy |
| Tareq Al-Khatib | Penicillin allergy + Amoxicillin → Cross-reactivity |
| Noor Al-Husseini | Pregnant + Roaccutane → Category X contraindicated |
| Zaid Haddad (5 yrs) | Child + adult Amoxicillin dose → Paediatric overdose |
| Abu Omar Al-Nabulsi | Simvastatin + Clarithromycin → Rhabdomyolysis contraindicated |
| Um Youssef Al-Qadi | Severe CKD + Metformin → Lactic acidosis contraindicated |
| Leen Al-Shawabkeh (8 yrs) | Child + Aspirin → Reye syndrome contraindicated |
| Khalil Al-Abbadi | Metformin + Paracetamol → **Zero warnings** (safe regimen) |

> **No real patient data is ever committed to this repository, used in any demo, or included in any screenshot. See [`docs/privacy-and-consent.md`](./docs/privacy-and-consent.md).**

---

## Repository Structure

```
wasfa/
├── CLINICAL-DISCLAIMER.md       ← ⚠️ Read first
├── LICENSE                      ← MIT (code only)
├── docs/
│   ├── positioning.md           ← Wasfa vs Hakeem, strategic fit
│   ├── architecture.md          ← System design & key decisions
│   ├── fhir-profiles.md         ← FHIR resources, extensions, operations
│   ├── safety-rules.md          ← The 7 rules documented with citations
│   ├── data-provenance.md       ← Every knowledge-base source listed
│   └── privacy-and-consent.md   ← National ID hashing, consent, audit
├── knowledge-base/
│   ├── molecules.json           ← ATC + RxNorm coded molecules
│   ├── brands-jo.json           ← Jordan brand → molecule mappings
│   ├── interactions.json        ← DDI pairs with severity + citations
│   ├── allergy-groups.json      ← Cross-reactivity families
│   └── dose-ranges.json         ← Adult and paediatric dose limits
├── src/
│   ├── Wasfa.Domain/            ← Core records, enums, interfaces
│   ├── Wasfa.Safety/            ← Safety engine + 7 rules (pure functions)
│   ├── Wasfa.Infrastructure/    ← EF Core, PostgreSQL/SQLite, FHIR store
│   └── Wasfa.Api/               ← ASP.NET Core + FHIR controllers
├── tests/
│   ├── Wasfa.Safety.Tests/      ← 9 clinical scenario tests
│   │   └── ClinicalScenarios/   ← JSON-driven scenario definitions
│   └── Wasfa.Api.IntegrationTests/
└── apps/
    ├── prescriber/              ← Angular (keyboard-first, RTL, ar/en)
    ├── patient/                 ← Flutter (Arabic-first, medication card)
    └── pharmacy/                ← Flutter (QR scan → dispense)
```

---

## Design System

**Concept:** *clinical calm with unmissable warnings.*

| Role | Hex | Use |
|---|---|---|
| Primary | `#1B5E5A` | Deep clinical teal — app bar, brand |
| **Contraindicated** | `#8B0000` | Dark red — absolute block |
| **Major** | `#D62828` | Red — serious DDI or allergy |
| **Moderate** | `#F77F00` | Orange — monitoring required |
| **Minor** | `#FCBF49` | Amber — informational alert |
| Safe | `#2A9D8F` | Green — no issues found |

**Typography:** Latin `Inter` · Arabic `IBM Plex Sans Arabic` · Drug names `IBM Plex Mono`
(a deliberate safety choice — monospaced prevents misreading `Losec` / `Lasix`, `500 mg` / `5 mg`).

Every warning carries: **severity label + icon + drug names + clinical consequence + citation + override action.** Colour alone is never the only signal — this is a WCAG AA, RTL-first, WCAG-accessible interface.

---

## Sources

- [About the Hakeem Program — Electronic Health Solutions](https://ehs.com.jo/hakeem-program/about-hakeem-program)
- [15 new healthcare facilities joined Hakeem in 2026 — Petra](https://petra.gov.jo/awiki/index.php/en/news/15-new-healthcare-facilities-joined-hakeem-system-in-2026)
- [Health Care and Pharmacy Practice in Jordan — PMC/NIH](https://pmc.ncbi.nlm.nih.gov/articles/PMC5407425/)
- [WHO ATC Classification System](https://www.whocc.no/atc_ddd_index/)
- [RxNorm — NIH/NLM](https://www.nlm.nih.gov/research/umls/rxnorm/)
- [British National Formulary — BNF](https://bnf.nice.org.uk/)
- [American Geriatrics Society Beers Criteria® 2023](https://agsjournals.onlinelibrary.wiley.com/doi/10.1111/jgs.18372)
- [KDIGO CKD Guidelines](https://kdigo.org/guidelines/ckd-evaluation-and-management/)
- [World Allergy Organization Drug Allergy Guidelines](https://www.worldallergy.org/)

---

*Wasfa is built with the conviction that a motivated developer who understands clinical standards, designs for safety, and is honest about the limits of their data builds software that matters.*
