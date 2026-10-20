# Wasfa System Architecture & Design Patterns

## 1. High-Level Architecture

Wasfa follows **Clean Architecture** (Ports and Adapters) principles to isolate the safety engine, domain logic, and clinical rules from external frameworks, database drivers, and UI clients.

```
┌────────────────────────────────────────────────────────────────────────┐
│                        PRESENTATION LAYER                              │
│                                                                        │
│   Prescriber Web App (Angular)      Patient & Pharmacy Apps (Flutter)  │
│   [Teal Clinical Design System]     [Arabic/English, QR Scanner, PDF]  │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │ HTTPS / REST / FHIR R4
                                    ▼
┌────────────────────────────────────────────────────────────────────────┐
│                        API LAYER (Wasfa.Api)                           │
│                                                                        │
│   FHIR Controllers (Firely SDK)      $check-safety Operation           │
│   Consent & Scope Middleware        Audit Logging Interceptor          │
└──────────────────┬─────────────────────────────────┬───────────────────┘
                   │                                 │
                   ▼                                 ▼
┌─────────────────────────────────────┐   ┌──────────────────────────────┐
│       SAFETY ENGINE (Wasfa.Safety)  │   │  DOMAIN LAYER (Wasfa.Domain) │
│                                     │   │                              │
│  ISafetyRule Pure Rule Evaluator    │   │  Core Entities & Enums       │
│  Drug–Drug, Duplicate, Allergy,     │   │  FHIR Model Projections      │
│  Dose, Renal, Pregnancy, Age Rules  │   │  Consent & Audit Definitions │
│  IDrugKnowledgeProvider Interface   │   │                              │
└──────────────────┬──────────────────┘   └──────────────┬───────────────┘
                   │                                     │
                   ▼                                     ▼
┌────────────────────────────────────────────────────────────────────────┐
│                  INFRASTRUCTURE LAYER (Wasfa.Infrastructure)           │
│                                                                        │
│  PostgreSQL (JSONB Storage + GIN Indexes) / SQLite Fallback            │
│  Temporal Resource History (fhir_resource_history)                     │
│  JsonKnowledgeProvider (Pluggable ATC/RxNorm/JFDA Knowledge Base)      │
│  Append-Only Audit Interceptor & National ID HMAC Hasher               │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Key Architectural Decisions

### 1. Pure Functional Safety Rules
The clinical safety rules implement the `ISafetyRule` interface:
```csharp
public interface ISafetyRule
{
    string Id { get; }
    IEnumerable<SafetyFinding> Evaluate(PatientContext patient, ProposedMedication proposed);
}
```
Rules are stateless, pure, and decoupled from databases. They accept an immutable `PatientContext` and `ProposedMedication`, returning zero or more structured `SafetyFinding` records. This makes the entire clinical safety test suite execute in milliseconds without database mocking or test fixtures.

### 2. Pluggable Drug Knowledge Provider
The safety engine accesses pharmacology data strictly through `IDrugKnowledgeProvider`:
```csharp
public interface IDrugKnowledgeProvider
{
    MoleculeInfo? GetMolecule(string code);
    IReadOnlyList<MoleculeInfo> SearchMolecules(string query);
    IReadOnlyList<BrandMapping> GetBrandsForMolecule(string moleculeCode);
    MoleculeInfo? ResolveBrandToMolecule(string brandNameOrCode);
    IReadOnlyList<InteractionRuleDefinition> GetInteractions(string moleculeA, string moleculeB);
    AllergyGroupInfo? GetAllergyGroup(string groupCode);
    DoseRangeInfo? GetDoseRange(string moleculeCode);
}
```
This enables swapping `JsonKnowledgeProvider` with enterprise vendors (Wolters Kluwer Lexicomp, First Databank) seamlessly.

### 3. Dual Storage: Native FHIR JSONB + Relational Projections
- Complete FHIR resources are preserved verbatim in `fhir_resources.content` (`jsonb`) to support standard FHIR compliance and temporal versioning.
- Indexed relational tables (`patients`, `medication_requests`, `medication_dispenses`) store projection fields for high-throughput relational querying, sorting, and reporting.
