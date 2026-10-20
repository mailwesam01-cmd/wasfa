# Project C (new) — **Wasfa** (وصفة)

> **A medication-safety and e-prescription platform: one medication list per patient, checked for
> interactions before the pharmacist dispenses.**
> Suggested repo: `github.com/<you>/wasfa`
> Tagline: *"One patient. One medication list. Checked before it's dispensed."*

**Stack:** ASP.NET Core + HL7 FHIR (Firely SDK) + PostgreSQL + Flutter + Angular + Docker
**Why this one:** the problem is **patient safety**, which is as big as software problems get — and
health IT is a real, growing, well-paid employer in Jordan. **FHIR is a rare, high-value skill** that
almost no junior in the region has, and it is directly transferable to international remote work.

---

## 1. The idea

Wasfa is an open-source e-prescription and medication-safety service built on the **HL7 FHIR R4**
standard. It has four parts:

1. **Prescriber (Angular web app)** — a doctor writes a prescription; as they add each drug, the
   system checks it in real time against the patient's *current* medication list, their allergies,
   their age/pregnancy status and their renal flags, and warns before signing.
2. **Safety engine (ASP.NET Core)** — drug–drug interactions, duplicate therapy (the same molecule
   under two brand names), allergy contraindications, dose-range checks, and pregnancy category
   warnings. Every warning is graded, cited and overridable **with a recorded reason**.
3. **Pharmacy app (Flutter/web)** — the pharmacist scans the prescription's QR/code, sees the exact
   items, dispenses, and marks them dispensed. This closes the loop: the system knows what was
   *actually* taken, not just what was written.
4. **Patient app (Flutter)** — the patient's own consolidated medication list, dose reminders,
   refill alerts, and a shareable "medication card" they can show at any clinic or emergency room.

Everything is stored and exchanged as **FHIR resources** (`Patient`, `Practitioner`,
`MedicationRequest`, `MedicationDispense`, `AllergyIntolerance`, `Condition`), so it can
interoperate with any standards-compliant health system rather than becoming another island.

---

## 2. The problem it solves

**Polypharmacy kills people quietly.** A patient in Jordan may see a private GP, a specialist at a
different hospital, and a third doctor at a clinic — and buy from any of several pharmacies. Each
prescriber knows only what the patient remembers to tell them. Prescriptions are still very often
**handwritten on paper**. The predictable results:

| Failure | Consequence |
|---|---|
| **No consolidated medication list** | Doctor #3 prescribes a drug that dangerously interacts with what doctors #1 and #2 already prescribed. Nobody has the full picture — including the patient. |
| **Duplicate therapy** | The same active molecule under two different brand names, taken together. Trivially detectable by software, invisible on paper. |
| **Allergy contraindication** | The allergy is recorded in one clinic's file and nowhere else. |
| **Handwriting and brand confusion** | Misread names, wrong strength, wrong frequency. Look-alike/sound-alike brands are a documented global cause of dispensing error. |
| **No dispensing feedback** | The prescriber never learns whether the medicine was collected, so non-adherence is invisible. |
| **Chronic-disease burden** | Diabetes, hypertension and cardiac disease are highly prevalent in Jordan, so long multi-drug regimens are the norm, not the exception. |
| **Elderly patients** | Often the most drugs, the most prescribers, and the least able to keep the list straight. |

**Where the gap is.** Jordan has a serious national e-health programme: **Hakeem**, launched in 2009
and run by Electronic Health Solutions, deploys a national Electronic Health Record based on VistA,
and facilities continue to join it — [15 new healthcare facilities joined during 2026 alone](https://petra.gov.jo/awiki/index.php/en/news/15-new-healthcare-facilities-joined-hakeem-system-in-2026).
Clinicians can pull a patient's record by national ID **at participating facilities**.

But the published material describes a programme centred on **participating (largely public)
healthcare facilities**. The path that most people actually walk — *private clinic → private
pharmacy* — and the **patient's own copy of their medication list** are not addressed by anything
publicly available. There is no open, standards-based service that a small private clinic or an
independent pharmacy can adopt, and no patient-held medication record.

> ⚠️ **Before you build:** contact Electronic Health Solutions and check Hakeem's current coverage,
> its private-sector programme, and whether it exposes any FHIR interface. Then position Wasfa
> deliberately — **as a complement that fills the private-sector and patient-held gap, and can
> hand off to Hakeem via FHIR** — not as a competitor. Write that positioning in the README. Being
> seen to understand the existing national infrastructure is worth more than pretending it isn't
> there.

**Why it makes you hireable:** health IT is one of the few software domains where correctness is
non-negotiable, and interviewers know it. Demonstrating that you can work to a formal standard
(FHIR), model clinical safety rules, handle audit trails and think about consent and privacy is a
level of seriousness that CRUD projects cannot reach. And FHIR + .NET is a globally in-demand
combination — it opens remote roles paying in foreign currency.

---

## 3. How it works

```
 ┌───────────────────────────────────────────────────────────────────────────┐
 │  PRESCRIBER  (Angular)                                                    │
 │   search patient → see current meds, allergies, conditions                │
 │   add drug → LIVE SAFETY CHECK → warnings → sign → QR issued              │
 └────────────────────────────┬──────────────────────────────────────────────┘
                              ▼   POST /fhir/MedicationRequest
 ┌───────────────────────────────────────────────────────────────────────────┐
 │  ASP.NET CORE + Firely FHIR SDK                                           │
 │                                                                           │
 │  ┌─────────────────────────────────────────────────────────────────────┐  │
 │  │ SAFETY ENGINE                                                       │  │
 │  │  S1 Drug–drug interaction   ingredient-pair rules, graded           │  │
 │  │  S2 Duplicate therapy       same ATC level-5 / same ingredient      │  │
 │  │  S3 Allergy contraindication cross-reactivity groups (e.g. β-lactam)│  │
 │  │  S4 Dose range              min/max per age band and weight         │  │
 │  │  S5 Renal / hepatic flags   dose adjustment warnings                │  │
 │  │  S6 Pregnancy / lactation   category warnings                       │  │
 │  │  S7 Paediatric / geriatric  age-inappropriate drug lists            │  │
 │  │                                                                     │  │
 │  │  Every finding: severity · explanation · citation · overridable     │  │
 │  │  with a mandatory recorded reason (never silently blocked)          │  │
 │  └─────────────────────────────────────────────────────────────────────┘  │
 │                                                                           │
 │  FHIR store (PostgreSQL)   ·   Consent engine   ·   Immutable audit log   │
 └──────────────┬────────────────────────────────────┬───────────────────────┘
                ▼                                    ▼
 ┌────────────────────────────┐        ┌─────────────────────────────────────┐
 │ PHARMACY (Flutter/web)     │        │ PATIENT (Flutter)                   │
 │ scan QR → verify → dispense│        │ my medication list · reminders ·    │
 │ → MedicationDispense       │        │ refill alerts · medication card     │
 │ partial dispense supported │        │ · grant/revoke access to a clinic   │
 └────────────────────────────┘        └─────────────────────────────────────┘
```

### The drug knowledge base — be honest about this

The safety engine is only as good as its data, and clinical drug databases are commercial. Handle
it like an engineer, not by hand-waving:

- **Model the data separately from the engine.** Ship an interface `IDrugKnowledgeProvider` with
  pluggable implementations.
- **Ship an openly-licensed starter dataset**, clearly scoped: the top ~200 molecules used in
  Jordan, mapped to **ATC codes** and **RxNorm** identifiers, with a curated set of well-documented
  major interactions from public sources.
- **Do not claim clinical completeness.** State plainly in the README and inside the app: *this is a
  demonstration knowledge base, not a certified clinical decision-support system; verify against an
  authoritative source.* Include a `CLINICAL-DISCLAIMER.md`.
- **Allow a commercial provider to be plugged in** — that is exactly how a real deployment would
  work, and showing you designed for it is the professional answer.
- Map local brand names to molecules using the **JFDA** registered-drug listings where available.

Being rigorous about the limits of your data is not a weakness in this project. It is the single
most senior thing you can demonstrate, and an interviewer in health IT will specifically look for it.

---

## 4. Target audience

**Primary:** private clinics, polyclinics and independent pharmacies in Jordan — the segment least
served by existing systems, and the one where most prescriptions are still written by hand.

**Secondary:** patients with chronic conditions and their families — especially those managing an
elderly parent's medications across several doctors.

**Tertiary:**
- Hospital IT and health-tech companies in Jordan (Electronic Health Solutions and the wider
  Jordanian health-IT sector) evaluating FHIR tooling.
- Medical tourism: Jordan is a major regional destination for treatment, and visiting patients
  arrive with no accessible medication history and leave with prescriptions their home doctor never
  sees. A portable FHIR medication record is a genuine fit.
- The global FHIR open-source community — this project is publishable well beyond Jordan.

---

## 5. Skills used

| Skill | Where it shows up | Signal |
|---|---|---|
| **ASP.NET Core / C#** | FHIR server, safety engine, consent, audit | Your #1 keyword at its most serious |
| **HL7 FHIR (Firely SDK)** | Resources, profiles, validation, search, `$operations` | **Rare, standards-based, globally in demand** |
| **Angular** | Prescriber workstation — dense, form-heavy, keyboard-driven | Your new keyword in the use case Angular is best at |
| **Flutter** | Patient and pharmacy apps | Mobile keyword |
| **PostgreSQL** | FHIR resource storage, JSONB, indexing, temporal history | Advanced data modelling |
| **Security & privacy** | OAuth2/SMART-on-FHIR-style scopes, consent, RBAC, immutable audit, encryption at rest | Uses your Cybersecurity skill where it genuinely matters |
| **Algorithms** | Interaction graph traversal, cross-reactivity groups, dose-range evaluation | Real logic |
| **QA / testing** | A clinical-scenario test suite: given meds A+B, exactly these warnings must fire | **Testing as patient safety** — the strongest possible QA story |
| **Domain modelling** | Clinical concepts done properly rather than approximated | Maturity |
| **Docker** | One-command stack with seeded demo patients | Delivery |
| **Technical writing** | Disclaimer, data provenance, positioning vs. national infrastructure | Judgement |

---

## 6. Design & visual identity

**Concept:** *clinical calm with unmissable warnings.* Two different design languages in one
product: the routine interface must be quiet and fast; the warnings must be impossible to skim past.

### Colour palette

| Role | Hex | Use |
|---|---|---|
| Primary | `#1B5E5A` | Deep clinical teal. App bar, brand, headings. |
| Primary light | `#2E8B84` | Secondary actions, active nav. |
| Accent | `#3A7CA5` | Calm blue. Links, informational chips. |
| **Warning: contraindicated** | `#8B0000` | Dark red. Absolute contraindication — the most serious level. |
| **Warning: major** | `#D62828` | Red. |
| **Warning: moderate** | `#F77F00` | Orange. |
| **Warning: minor** | `#FCBF49` | Amber. |
| **No issue** | `#2A9D8F` | Green check. |
| Surface | `#FFFFFF` | Cards, forms. |
| Canvas | `#F4F7F6` | Background. |
| Ink | `#0D1F1E` | Text. |
| Muted | `#5F7370` | Labels, metadata. |
| Rule | `#DCE5E3` | Table borders. |

Rationale: teal reads clinical and trustworthy without the tired hospital-blue cliché. The four
warning levels are a deliberate **ordered scale** — dark red → red → orange → amber — so severity is
readable at a glance and by position, not just hue. **Every warning also carries a text label and an
icon**, because colour alone is unacceptable in a safety-critical interface.

### The warning component (the most important UI in the product)

- Full-width banner, left border 4 px in the severity colour, tinted background at 8 % opacity.
- Structure, always in this order:
  1. **Severity label** in caps (`MAJOR INTERACTION`)
  2. **The two drugs**, in bold, by molecule name with the brand in parentheses
  3. **One sentence of plain clinical consequence** — what could happen to the patient
  4. **Citation / source** of the rule
  5. Actions: `Change drug` (primary) · `Override with reason` (secondary, requires typed text)
- Contraindicated (`#8B0000`) warnings require typing a reason before signing is possible. Never a
  one-click dismiss.
- Never use a modal that can be dismissed by clicking the backdrop.

### Typography

- **Latin:** `Inter`. **Arabic:** `IBM Plex Sans Arabic`.
- **Drug names in `IBM Plex Mono`** at the point of prescribing. This is a deliberate safety choice:
  a monospaced face makes look-alike names (`Zantac` / `Zyrtec`, `Losec` / `Lasix`) visually
  distinguishable, and prevents strength values from being misread.
- Strength and dose always as `500 mg` with a hard space, never `500mg`.
- Minimum 15 px body; 17 px for anything a patient reads.

### Key screens

**Prescriber (Angular)** — dense, keyboard-first, built for a doctor with a queue outside the door:
1. Patient header strip: name, age, weight, allergies as `#D62828` chips, active conditions.
2. Current medication list — always visible, never behind a tab. This is the whole point.
3. Drug search with molecule-first results (brand shown as secondary), and the safety check firing
   as you type.
4. Warning stack, ordered by severity.
5. Sign & issue → QR + short human-readable code.

**Patient (Flutter)** — the opposite: large, plain, reassuring:
1. Today's doses as a timeline with big tick targets.
2. My medications — card per drug: what it is *for*, in plain Arabic, plus dose and duration.
3. **Medication card** — a single shareable screen (and PDF) listing everything, for emergency-room
   or new-doctor visits. This is the feature families will actually use.
4. Refill alerts. 5. Access control: which clinics can see my list, revocable.

**Pharmacy (Flutter/web):** scan → verify → dispense, with partial dispensing and substitution
recording.

### Accessibility — non-negotiable here

Patients include elderly and low-vision users. WCAG AA contrast minimum, dynamic type support, full
Arabic RTL, and every warning conveyed by **colour + icon + text** together.

---

## 7. Repository structure

```
wasfa/
├── README.md                         ← positioning vs Hakeem + screenshots + quickstart
├── LICENSE                           ← MIT (code)
├── CLINICAL-DISCLAIMER.md            ← ⚠️ write this first, link it from every entry point
├── docker-compose.yml                ← api + postgres + prescriber + seeded demo data
├── docs/
│   ├── positioning.md                ← how Wasfa complements national infrastructure
│   ├── fhir-profiles.md              ← the resources and profiles used
│   ├── safety-rules.md               ← each rule, its logic, its source
│   ├── data-provenance.md            ← where every drug/interaction record came from
│   ├── privacy-and-consent.md
│   └── architecture.md
├── knowledge-base/
│   ├── molecules.json                ← ATC + RxNorm codes
│   ├── brands-jo.json                ← local brand → molecule mapping
│   ├── interactions.json             ← pairs, severity, mechanism, citation
│   ├── allergy-groups.json           ← cross-reactivity classes
│   └── dose-ranges.json
├── src/
│   ├── Wasfa.Api/                    ← ASP.NET Core + Firely
│   │   ├── Fhir/{FhirController.cs,SearchHandler.cs,ValidationSetup.cs}
│   │   ├── Auth/{ScopesSetup.cs,ConsentMiddleware.cs}
│   │   └── Audit/AuditInterceptor.cs
│   ├── Wasfa.Safety/
│   │   ├── ISafetyRule.cs
│   │   ├── Rules/{InteractionRule,DuplicateTherapyRule,AllergyRule,
│   │   │          DoseRangeRule,RenalRule,PregnancyRule,AgeRule}.cs
│   │   ├── KnowledgeBase/{IDrugKnowledgeProvider.cs,JsonKnowledgeProvider.cs}
│   │   └── Model/{SafetyFinding.cs,Severity.cs}
│   ├── Wasfa.Domain/
│   └── Wasfa.Infrastructure/         ← EF Core, FHIR resource storage
├── tests/
│   ├── Wasfa.Safety.Tests/
│   │   └── ClinicalScenarios/        ← ⭐ given these meds → exactly these findings
│   └── Wasfa.Api.IntegrationTests/
├── apps/
│   ├── prescriber/                   ← Angular
│   ├── patient/                      ← Flutter
│   └── pharmacy/                     ← Flutter (web + mobile)
└── .github/workflows/ci.yml
```

---

## 8. Data model — FHIR first

Store FHIR resources natively (JSONB) with relational projections for the columns you search on.

```sql
CREATE TABLE fhir_resources (
  id              uuid PRIMARY KEY,
  resource_type   text NOT NULL,          -- Patient | MedicationRequest | …
  fhir_id         text NOT NULL,
  version_id      int  NOT NULL DEFAULT 1,
  content         jsonb NOT NULL,         -- the full FHIR resource
  last_updated    timestamptz NOT NULL DEFAULT now(),
  is_deleted      boolean NOT NULL DEFAULT false,
  UNIQUE (resource_type, fhir_id, version_id)
);
CREATE INDEX ix_fhir_content ON fhir_resources USING GIN (content jsonb_path_ops);

CREATE TABLE fhir_resource_history (      -- FHIR requires full version history
  LIKE fhir_resources INCLUDING ALL
);

-- Search projections
CREATE TABLE patients (
  fhir_id         text PRIMARY KEY,
  national_id_hash text NOT NULL UNIQUE,  -- hashed, never stored in clear
  birth_date      date, gender text, weight_kg numeric,
  is_pregnant     boolean, renal_impairment text
);

CREATE TABLE medication_requests (
  fhir_id         text PRIMARY KEY,
  patient_fhir_id text NOT NULL REFERENCES patients(fhir_id),
  prescriber_id   text NOT NULL,
  molecule_code   text NOT NULL,          -- ATC / RxNorm
  brand_name      text,
  strength        text, dose_text text, frequency text,
  duration_days   int,
  status          text NOT NULL,          -- active | completed | cancelled
  authored_on     timestamptz NOT NULL,
  short_code      text NOT NULL UNIQUE    -- what the pharmacy scans/types
);
CREATE INDEX ix_medreq_patient_active ON medication_requests(patient_fhir_id)
  WHERE status = 'active';

CREATE TABLE medication_dispenses (
  fhir_id         text PRIMARY KEY,
  request_fhir_id text NOT NULL REFERENCES medication_requests(fhir_id),
  pharmacy_id     text NOT NULL,
  dispensed_at    timestamptz NOT NULL,
  quantity        numeric,
  substituted_brand text,
  substitution_reason text
);

CREATE TABLE safety_overrides (           -- who overrode what, and why
  id              uuid PRIMARY KEY,
  request_fhir_id text NOT NULL REFERENCES medication_requests(fhir_id),
  finding_code    text NOT NULL,
  severity        text NOT NULL,
  reason          text NOT NULL,          -- mandatory free text
  overridden_by   text NOT NULL,
  overridden_at   timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE consents (
  id              uuid PRIMARY KEY,
  patient_fhir_id text NOT NULL REFERENCES patients(fhir_id),
  grantee_org_id  text NOT NULL,
  scope           text NOT NULL,          -- read_medications | read_all
  granted_at      timestamptz NOT NULL,
  revoked_at      timestamptz
);

CREATE TABLE audit_log (                  -- append-only
  id              bigserial PRIMARY KEY,
  actor_id        text NOT NULL,
  action          text NOT NULL,          -- read | write | override | dispense
  resource_type   text, resource_id text,
  patient_fhir_id text,
  ip              inet,
  occurred_at     timestamptz NOT NULL DEFAULT now(),
  detail          jsonb
);
REVOKE UPDATE, DELETE ON audit_log FROM PUBLIC;   -- append-only, enforced
```

**Privacy decisions to make explicit in `docs/privacy-and-consent.md`:**
- National ID is **hashed with a salt**, never stored in clear.
- Access to a patient's list requires an **active consent record**; consent is revocable and
  revocation is immediate.
- Every read of patient data is audited — reads, not just writes.
- The demo instance uses **synthetic patients only**. Never put real patient data in a public repo,
  a demo deployment, or a screenshot. Say so in the README.

---

## 9. The safety engine contract

```csharp
public enum Severity { Minor, Moderate, Major, Contraindicated }

public sealed record SafetyFinding(
    string   Code,               // "DDI.WARFARIN.NSAID"
    Severity Severity,
    string   TitleEn, string TitleAr,
    string   ExplanationEn,      // one plain sentence of clinical consequence
    string   ExplanationAr,
    IReadOnlyList<string> InvolvedMolecules,
    string   Source,             // citation
    bool     RequiresReasonToOverride);

public interface ISafetyRule
{
    string Id { get; }
    IEnumerable<SafetyFinding> Evaluate(PatientContext patient, ProposedMedication proposed);
}

public sealed record PatientContext(
    int AgeYears, decimal? WeightKg, string Gender, bool IsPregnant,
    string? RenalImpairment,
    IReadOnlyList<ActiveMedication> ActiveMedications,
    IReadOnlyList<Allergy> Allergies,
    IReadOnlyList<string> ConditionCodes);
```

### The clinical scenario test suite (your strongest QA artefact)

```
tests/ClinicalScenarios/
  warfarin-plus-nsaid.json          → expect MAJOR bleeding-risk finding
  two-brands-same-molecule.json     → expect MAJOR duplicate-therapy finding
  penicillin-allergy-amoxicillin    → expect CONTRAINDICATED cross-reactivity finding
  paediatric-adult-dose.json        → expect MAJOR dose-range finding
  pregnancy-category-x.json         → expect CONTRAINDICATED finding
  safe-combination.json             → expect ZERO findings   ← the false-positive guard
```

Each file: patient context + proposed drug + **the exact expected findings**. CI fails if a finding
appears that shouldn't, or fails to appear when it should. In a safety system, false positives are
as dangerous as false negatives — alert fatigue makes clinicians ignore *all* warnings. Include the
negative cases and say why.

---

## 10. Build plan

### Phase 0 — Positioning & disclaimer (3 days, before code)
- [ ] Contact Electronic Health Solutions; ask about Hakeem's private-sector coverage and any FHIR
      interface. Even a "no reply" is worth documenting.
- [ ] Write `CLINICAL-DISCLAIMER.md` and `docs/positioning.md`.
- **Done when:** you can explain in two sentences what Wasfa does that existing systems don't.

### Phase 1 — Knowledge base (week 1)
- [ ] `molecules.json` — top ~200 molecules used in Jordan, ATC + RxNorm coded.
- [ ] `interactions.json` — a curated, cited set of well-documented major interactions.
- [ ] `allergy-groups.json`, `dose-ranges.json`.
- [ ] `docs/data-provenance.md` — the source of every record.
- **Done when:** the data is defensible, and its limits are written down.

### Phase 2 — Safety engine (week 2)  ← build this before any UI
- [ ] `ISafetyRule` + the seven rules as pure functions.
- [ ] The clinical scenario suite, including negative cases.
- **Done when:** `dotnet test` proves the engine on every scenario.

### Phase 3 — FHIR API (week 3)
- [ ] ASP.NET Core + Firely: `Patient`, `Practitioner`, `MedicationRequest`,
      `MedicationDispense`, `AllergyIntolerance`, `Condition`.
- [ ] Resource storage with version history, FHIR search parameters, resource validation.
- [ ] Custom operation `POST /MedicationRequest/$check-safety`.
- [ ] Auth with scoped tokens, consent middleware, append-only audit.
- **Done when:** a standard FHIR client can read and write against your server.

### Phase 4 — Prescriber app (week 4)
- [ ] Angular: patient header, always-visible medication list, drug search, live safety check,
      the warning component, sign + QR.
- [ ] Full ar/en, RTL, keyboard-first.
- **Done when:** you can prescribe a drug that triggers a major interaction and the warning is
      genuinely impossible to miss. **Screenshot that.**

### Phase 5 — Pharmacy + patient apps (week 5)
- [ ] Flutter pharmacy: scan → verify → dispense → `MedicationDispense`.
- [ ] Flutter patient: medication list, dose timeline, refill alerts, medication card PDF, consent
      control.

### Phase 6 — Demo + publish (week 6)
- [ ] `docker compose up` → seeded synthetic patients with pre-built dangerous scenarios so a
      reviewer can reproduce a warning in 60 seconds.
- [ ] README: positioning → the warning screenshot → quickstart → disclaimer → provenance.
- [ ] Post to the FHIR / open health-IT community as well as locally — this project has an
      international audience.

---

## 11. Risks and how to handle them

| Risk | Mitigation |
|---|---|
| Clinical liability | `CLINICAL-DISCLAIMER.md`, in-app banner, demo-only knowledge base, MIT licence, explicit "not a certified CDS" statement. Non-negotiable. |
| Drug data is commercial | Pluggable provider interface + a small, openly-sourced, clearly-scoped starter set. Never scrape a licensed database. |
| Real patient data | Synthetic only, always. Never in the repo, the demo, or a screenshot. |
| Duplicating Hakeem | Position as complementary and FHIR-interoperable; document it. |
| Scope is large | Phases 1–4 alone (knowledge base + engine + FHIR API + prescriber) are a complete, impressive project. The patient and pharmacy apps are upside. |

---

## 12. How to talk about it in an interview

> *"Jordan has a national EHR in Hakeem, but the private clinic to private pharmacy path is still
> largely paper, and no patient holds their own consolidated medication list. So a third doctor
> can prescribe something that interacts with what two other doctors already prescribed. I built an
> open FHIR-based e-prescription service with a safety engine — interactions, duplicate therapy,
> allergy cross-reactivity, dose ranges — where every warning is graded, cited, and overridable only
> with a recorded reason. I wrote the engine before any UI and drove it from a clinical scenario
> test suite that includes negative cases, because in a safety system false positives cause alert
> fatigue and that gets people hurt. And I was explicit about the limits of my drug data rather
> than pretending it was clinical-grade."*

That last sentence is what makes a senior engineer decide you are safe to hire.

---

## Sources

- [About the Hakeem Program — Electronic Health Solutions](https://ehs.com.jo/hakeem-program/about-hakeem-program)
- [15 new healthcare facilities joined Hakeem system in 2026 — Jordan News Agency (Petra)](https://petra.gov.jo/awiki/index.php/en/news/15-new-healthcare-facilities-joined-hakeem-system-in-2026)
- [Jordan Expands Healthcare Capacity, Digital Services, Specialized Treatment — Petra](https://petra.gov.jo/en/news/jordan-expands-healthcare-capacity-digital-services-specialized-treatment)
- [Health Care and Pharmacy Practice in Jordan — PMC/NIH](https://pmc.ncbi.nlm.nih.gov/articles/PMC5407425/)
- [Digital transformation of the healthcare sector in Jordan — Electronic Health Solutions](https://www.ehs.com.jo/)
