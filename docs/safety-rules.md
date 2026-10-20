# Wasfa Clinical Safety Engine — Rules & Decision Logic

## 1. Safety Engine Principles
In clinical informatics, alert fatigue is a primary cause of adverse patient events. If an alert system generates trivial, unranked, or false-positive warnings, prescribers dismiss all warnings reflexively. Wasfa addresses this with four core principles:

1. **Hierarchy of Severity:** Every rule finding is strictly ranked into one of four ordered levels:
   - `Contraindicated` (`#8B0000`): Lethal risk, organ failure, or absolute clinical contraindication. Cannot be signed without mandatory written clinical rationale.
   - `Major` (`#D62828`): High probability of severe adverse drug event or treatment failure. Explicit override required.
   - `Moderate` (`#F77F00`): Clinical monitoring or dose titration indicated.
   - `Minor` (`#FCBF49`): Minor pharmacokinetic or cosmetic effect.
2. **Molecule-First Evaluation:** Brand names are resolved to their international non-proprietary active ingredients (INN / ATC / RxNorm). Duplicate therapy is immediately exposed even if marketed under unrelated trade names.
3. **Transparent Clinical Citations:** Every finding includes plain English and Arabic clinical explanations and a literature citation (BNF, FDA Black Box, Clinical Pharmacology guidelines).
4. **Mandatory Audited Overrides:** Critical alerts can never be dismissed with a single click. A reason must be recorded and permanently attached to the prescription.

---

## 2. The 7 Safety Rules (S1 - S7)

### S1: Drug–Drug Interaction Rule (`InteractionRule`)
- **Input:** Proposed medication molecule code + active medications from the patient's consolidated profile.
- **Mechanism:** Evaluates pairwise interactions in the bidirectional interaction graph.
- **Examples:**
  - *Warfarin + Diclofenac (NSAID):* Major risk of severe gastrointestinal hemorrhage via dual antiplatelet / anticoagulation and gastric mucosal damage.
  - *ACE Inhibitor (Enalapril) + Potassium-sparing diuretic (Spironolactone):* Major risk of life-threatening hyperkalemia.
  - *Clopidogrel + Omeprazole:* Moderate CYP2C19 inhibition reducing antiplatelet activation.

### S2: Duplicate Therapy Rule (`DuplicateTherapyRule`)
- **Input:** Proposed medication ATC Level 5 and active ingredient list.
- **Mechanism:** Detects when two medications share the identical active molecule or identical therapeutic subclass with additive toxicity (e.g., two distinct NSAIDs taken simultaneously).
- **Clinical Significance:** Highly common when patients see two different doctors and receive *Voltarin* (Diclofenac) and *Cataflam* (Diclofenac potassium), resulting in accidental double dosing.

### S3: Allergy Cross-Reactivity Rule (`AllergyRule`)
- **Input:** Patient recorded allergies (`AllergyIntolerance`) + proposed medication.
- **Mechanism:** Traverses allergy cross-reactivity hierarchies rather than simple string matching.
- **Examples:**
  - Penicillin allergy cross-reacting with Amoxicillin (direct contraindication) and Cephalosporins (graded cross-reactivity warning based on side-chain similarity).
  - Sulfonamide antibiotic allergy cross-reacting with Sulfamethoxazole.

### S4: Dose-Range Rule (`DoseRangeRule`)
- **Input:** Proposed dose, frequency, patient age, and patient weight (kg).
- **Mechanism:** Computes daily dose vs. maximum safe adult and pediatric weight-adjusted limits (mg/kg/day).
- **Examples:**
  - Prescribing an adult dose (e.g., Paracetamol 1000 mg every 4 hours = 6g/day) to a pediatric patient (max safe pediatric dose is 60 mg/kg/day) fires an immediate `Major` dose-range finding.

### S5: Renal & Hepatic Flags Rule (`RenalRule`)
- **Input:** Patient estimated Glomerular Filtration Rate (eGFR) / CKD staging + drug clearance profile.
- **Mechanism:** Flags nephrotoxic drugs or drugs requiring mandatory dose adjustment.
- **Examples:**
  - Metformin in severe renal impairment (eGFR < 30 mL/min): Contraindicated due to risk of lactic acidosis.
  - Aminoglycosides (Gentamicin) in renal impairment: Nephrotoxicity and ototoxicity warning with required trough monitoring.

### S6: Pregnancy & Lactation Rule (`PregnancyRule`)
- **Input:** Patient pregnancy status and gestational trimester + FDA/TGA pregnancy category.
- **Mechanism:**
  - **Category X:** Absolute Contraindication. Fetal abnormalities demonstrated. (e.g., Isotretinoin, Methotrexate, Statins).
  - **Category D:** Major warning. Positive evidence of human fetal risk, but potential benefits may warrant use in life-threatening situations (e.g., ACE Inhibitors, Carbamazepine).

### S7: Age-Inappropriate Drugs (Paediatric & Geriatric / Beers Criteria) (`AgeRule`)
- **Input:** Patient age in years + drug age-suitability classification.
- **Mechanism:**
  - **Paediatric (< 18 yrs):** Aspirin contraindicated under age 16 due to Reye's Syndrome; Tetracyclines contraindicated under age 8 due to enamel hypoplasia.
  - **Geriatric (≥ 65 yrs - Beers Criteria):** Long-acting Benzodiazepines, high-dose anticholinergics with acute fall and delirium risk.

---

## 3. Negative Scenarios & False-Positive Prevention
The test suite explicitly includes safe drug combinations (e.g., Paracetamol + Amoxicillin in a healthy non-allergic adult, or Metformin + Amlodipine with normal renal function). If any safety rule generates an alert for a safe combination, the build fails.
