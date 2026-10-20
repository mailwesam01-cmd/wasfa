# Data Provenance & Clinical Knowledge Attribution

## 1. Transparency in Clinical Data
Wasfa uses an openly-licensed, transparent starter dataset for demonstration and educational testing. No proprietary or unlicensed commercial drug database has been scraped or reverse-engineered.

---

## 2. Coding Systems & References

### A. Anatomical Therapeutic Chemical (ATC) Classification
- **Source:** WHO Collaborating Centre for Drug Statistics Methodology (Oslo, Norway).
- **Usage:** Standardized 7-character hierarchical coding (Levels 1 to 5) for molecules.
- **Role in Wasfa:**
  - `Level 5`: Exact molecule identification (e.g. `B01AA03` for Warfarin, `C09AA02` for Enalapril).
  - `Level 4 / 3`: Pharmacological/therapeutic subclass grouping used for duplicate therapy detection (e.g. `M01AB` for Acetic acid derivatives / NSAIDs).

### B. RxNorm (U.S. National Library of Medicine)
- **Source:** National Institutes of Health (NIH) / NLM.
- **Usage:** Standardized normalized naming system for clinical drugs.
- **Role in Wasfa:** International interoperability key alongside ATC.

### C. Jordan Food and Drug Administration (JFDA) Registered Drug Listings
- **Source:** JFDA Drug Directorate (Jordan Ministry of Health).
- **Usage:** Identification of common trade names marketed in Jordan by regional pharmaceutical manufacturers (e.g., Hikma Pharmaceuticals, Dar Al Dawa, Arab Pharmaceutical Manufacturing, Jordanian Pharmaceutical Manufacturing) alongside multinational originator brands.
- **Example Mappings:**
  - `Diclofenac` -> *Voltaren* (Novartis), *Cataflam* (Novartis), *Diclogesic* (Dar Al Dawa), *Clofen* (Hikma).
  - `Amoxicillin` -> *Amoxil* (GSK), *Hiconcil* (Hikma), *Penamox* (APM).
  - `Metformin` -> *Glucophage* (Merck), *Formit* (Hikma), *Diaphage* (JPM).

### D. Drug Interaction Evidence Base
The bundled interactions in `knowledge-base/interactions.json` were curated from public clinical monographs and consensus guidelines:
1. **British National Formulary (BNF) Appendix 1: Interactions.**
2. **FDA Prescribing Information & Black Box Warnings.**
3. **WHO Model Formulary.**
4. **American Geriatrics Society (AGS) Beers Criteria® (2023 update)** for potentially inappropriate medication use in older adults.

---

## 3. Knowledge Base File Formats

- `knowledge-base/molecules.json`: Core catalog of ~200 top molecules with ATC, RxNorm, category, adult max daily dose, pediatric max, pregnancy category, and renal threshold.
- `knowledge-base/brands-jo.json`: Brand name mappings with manufacturer and strength.
- `knowledge-base/interactions.json`: Interaction pairs with clinical mechanism, severity (`Contraindicated`, `Major`, `Moderate`, `Minor`), citations, and bilingual descriptions.
- `knowledge-base/allergy-groups.json`: Cross-reactivity families.
- `knowledge-base/dose-ranges.json`: Detailed dosage rules.
