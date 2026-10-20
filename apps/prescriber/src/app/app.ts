import { Component, OnInit, OnDestroy, signal, computed, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterOutlet } from '@angular/router';
import { HttpClient, HttpClientModule } from '@angular/common/http';
import { Subject, debounceTime, distinctUntilChanged, switchMap, of, takeUntil } from 'rxjs';

// ── Domain Types ──────────────────────────────────────────────────────
interface Allergy     { code: string; displayName: string; criticality: string; allergyGroupId?: string; }
interface ActiveMed   { id: string; moleculeCode: string; brandName: string; strength: string; frequency: string; status: string; }
interface Patient     {
  id: string; nameEn: string; nameAr: string; ageYears: number; weightKg?: number;
  gender: string; isPregnant: boolean; renalImpairment?: string;
  activeMedications: ActiveMed[]; allergies: Allergy[]; conditionCodes: string[];
}
interface SafetyFinding {
  code: string; severity: 'Contraindicated' | 'Major' | 'Moderate' | 'Minor';
  titleEn: string; titleAr: string; explanationEn: string; explanationAr: string;
  involvedMolecules: string[]; source: string; requiresReasonToOverride: boolean;
}
interface MoleculeResult { code: string; nameEn: string; nameAr: string; category: string; }
interface BrandResult    { brandName: string; brandNameAr: string; moleculeCode: string; strength: string; form: string; }

const DEMO_PATIENTS: Patient[] = [
  {
    id: 'pat-1001', nameEn: 'Ahmad Al-Majali', nameAr: 'أحمد المجالي',
    ageYears: 68, weightKg: 75, gender: 'male', isPregnant: false, renalImpairment: 'Normal',
    allergies: [],
    activeMedications: [
      { id: 'm1', moleculeCode: 'B01AA03', brandName: 'Coumadin', strength: '5 mg', frequency: 'QD', status: 'active' }
    ],
    conditionCodes: ['I48.0']
  },
  {
    id: 'pat-1002', nameEn: 'Fatima Al-Zoubi', nameAr: 'فاطمة الزعبي',
    ageYears: 45, weightKg: 68, gender: 'female', isPregnant: false, renalImpairment: 'Normal',
    allergies: [],
    activeMedications: [
      { id: 'm2', moleculeCode: 'N02BE01', brandName: 'Panadol', strength: '500 mg', frequency: 'QID', status: 'active' }
    ],
    conditionCodes: ['M79.3']
  },
  {
    id: 'pat-1003', nameEn: 'Tareq Al-Khatib', nameAr: 'طارق الخطيب',
    ageYears: 29, weightKg: 62, gender: 'male', isPregnant: false, renalImpairment: 'Normal',
    allergies: [{ code: 'ALLERGY_BETA_LACTAMS', displayName: 'Penicillins & Beta-Lactams', criticality: 'high', allergyGroupId: 'ALLERGY_BETA_LACTAMS' }],
    activeMedications: [],
    conditionCodes: ['J06.9']
  },
  {
    id: 'pat-1004', nameEn: 'Noor Al-Husseini', nameAr: 'نور الحسيني',
    ageYears: 24, weightKg: 58, gender: 'female', isPregnant: true, renalImpairment: 'Normal',
    allergies: [],
    activeMedications: [],
    conditionCodes: ['L70.0', 'Z34.0']
  },
  {
    id: 'pat-1005', nameEn: 'Zaid Haddad (5 yrs)', nameAr: 'زيد حداد (5 سنوات)',
    ageYears: 5, weightKg: 18, gender: 'male', isPregnant: false, renalImpairment: 'Normal',
    allergies: [],
    activeMedications: [],
    conditionCodes: ['H66.9']
  },
  {
    id: 'pat-1007', nameEn: 'Um Youssef Al-Qadi', nameAr: 'أم يوسف القاضي',
    ageYears: 71, weightKg: 64, gender: 'female', isPregnant: false, renalImpairment: 'Severe',
    allergies: [],
    activeMedications: [],
    conditionCodes: ['N18.4', 'E11.9']
  },
  {
    id: 'pat-1009', nameEn: 'Khalil Al-Abbadi', nameAr: 'خليل العبادي',
    ageYears: 52, weightKg: 80, gender: 'male', isPregnant: false, renalImpairment: 'Normal',
    allergies: [],
    activeMedications: [
      { id: 'm9', moleculeCode: 'A10BA02', brandName: 'Glucophage', strength: '850 mg', frequency: 'BID', status: 'active' }
    ],
    conditionCodes: ['E11.9']
  }
];

const DEMO_MOLECULES: MoleculeResult[] = [
  { code: 'M01AE01', nameEn: 'Ibuprofen', nameAr: 'إيبوبروفين', category: 'NSAID' },
  { code: 'M01AB05', nameEn: 'Diclofenac', nameAr: 'ديكلوفيناك', category: 'NSAID' },
  { code: 'N02BE01', nameEn: 'Paracetamol', nameAr: 'باراسيتامول', category: 'Analgesic' },
  { code: 'J01CA04', nameEn: 'Amoxicillin', nameAr: 'أموكسيسيلين', category: 'Antibiotic' },
  { code: 'J01CR02', nameEn: 'Amoxicillin/Clavulanate', nameAr: 'أموكسيسيلين وكلافولانات', category: 'Antibiotic' },
  { code: 'A10BA02', nameEn: 'Metformin', nameAr: 'ميتفورمين', category: 'Antidiabetic' },
  { code: 'C09AA02', nameEn: 'Enalapril', nameAr: 'إنالابريل', category: 'ACE Inhibitor' },
  { code: 'C10AA05', nameEn: 'Atorvastatin', nameAr: 'أتورفاستاتين', category: 'Statin' },
  { code: 'B01AC04', nameEn: 'Clopidogrel', nameAr: 'كلوبيدوغريل', category: 'Antiplatelet' },
  { code: 'A02BC01', nameEn: 'Omeprazole', nameAr: 'أوميبرازول', category: 'PPI' },
  { code: 'D10BA01', nameEn: 'Isotretinoin', nameAr: 'إيزوتريتينوين', category: 'Retinoid' },
  { code: 'J01FA10', nameEn: 'Azithromycin', nameAr: 'أزيثروميسين', category: 'Antibiotic' },
  { code: 'J01MA02', nameEn: 'Ciprofloxacin', nameAr: 'سيبروفلوكساسين', category: 'Antibiotic' },
  { code: 'H03AA01', nameEn: 'Levothyroxine', nameAr: 'ليفوثيروكسين', category: 'Thyroid' },
  { code: 'C03CA01', nameEn: 'Furosemide', nameAr: 'فوروسيميد', category: 'Diuretic' },
  { code: 'N05BA01', nameEn: 'Diazepam', nameAr: 'ديازيبام', category: 'Benzodiazepine' },
  { code: 'N02BA01', nameEn: 'Aspirin (analgesic)', nameAr: 'أسبرين (مسكن)', category: 'Salicylate' },
];

function generateShortCode(): string {
  const chars = 'ABCDEFGHJKMNPQRSTUVWXYZ23456789';
  return Array.from({ length: 8 }, () => chars[Math.floor(Math.random() * chars.length)]).join('');
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterOutlet, HttpClientModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App implements OnInit, OnDestroy {
  // State
  selectedPatient = signal<Patient | null>(null);
  drugSearch = signal('');
  searchResults = signal<MoleculeResult[]>([]);
  showResults = signal(false);
  selectedMolecule = signal<MoleculeResult | null>(null);
  doseQty = signal(500);
  doseUnit = signal('mg');
  frequency = signal('BID');
  duration = signal(7);
  isChecking = signal(false);
  findings = signal<SafetyFinding[]>([]);
  overrideReasons = signal<Record<string, string>>({});
  showOverrideFor = signal<Record<string, boolean>>({});
  issuedCode = signal<string | null>(null);
  showDisclaimer = signal(false);
  lang = signal<'en' | 'ar'>('en');
  apiBaseUrl = 'http://localhost:5000';
  private destroy$ = new Subject<void>();
  private searchSubject = new Subject<string>();

  sortedFindings = computed(() => {
    const order = { Contraindicated: 0, Major: 1, Moderate: 2, Minor: 3 };
    return [...this.findings()].sort((a, b) => order[a.severity] - order[b.severity]);
  });

  canSign = computed(() => {
    if (!this.selectedMolecule()) return false;
    const blocking = this.sortedFindings().filter(f =>
      f.requiresReasonToOverride &&
      !this.overrideReasons()[f.code]?.trim()
    );
    return blocking.length === 0;
  });

  constructor(private http: HttpClient) {
    effect(() => {
      const q = this.drugSearch();
      if (q.length >= 2) {
        this.searchSubject.next(q);
      } else {
        this.searchResults.set([]);
        this.showResults.set(false);
      }
    });
  }

  ngOnInit() {
    if (DEMO_PATIENTS.length > 0) {
      this.selectedPatient.set(DEMO_PATIENTS[0]);
    }

    this.searchSubject.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      switchMap(q => {
        const qLow = q.toLowerCase();
        const filtered = DEMO_MOLECULES.filter(m =>
          m.nameEn.toLowerCase().includes(qLow) ||
          m.nameAr.includes(q) ||
          m.code.toLowerCase().includes(qLow)
        );
        return of(filtered);
      }),
      takeUntil(this.destroy$)
    ).subscribe(results => {
      this.searchResults.set(results.slice(0, 8));
      this.showResults.set(results.length > 0);
    });
  }

  onPatientChange(patientId: string) {
    const found = DEMO_PATIENTS.find(p => p.id === patientId);
    if (found) {
      this.selectPatient(found);
    }
  }

  toggleLang() {
    this.lang.update(l => l === 'en' ? 'ar' : 'en');
  }

  onSearchChange(value: string) {
    this.drugSearch.set(value);
  }

  onSearchFocus() {
    if (this.searchResults().length > 0) {
      this.showResults.set(true);
    }
  }

  updateDoseQty(val: number) {
    this.doseQty.set(val);
    if (this.selectedMolecule()) this.runSafetyCheck();
  }

  updateDoseUnit(val: string) {
    this.doseUnit.set(val);
  }

  updateFrequency(val: string) {
    this.frequency.set(val);
    if (this.selectedMolecule()) this.runSafetyCheck();
  }

  updateDuration(val: number) {
    this.duration.set(val);
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get patients() { return DEMO_PATIENTS; }

  selectPatient(p: Patient) {
    this.selectedPatient.set(p);
    this.selectedMolecule.set(null);
    this.findings.set([]);
    this.drugSearch.set('');
    this.overrideReasons.set({});
    this.showOverrideFor.set({});
    this.issuedCode.set(null);
  }

  loadScenario(patientId: string, moleculeCode: string, qty: number, unit: string, freq: string) {
    const p = DEMO_PATIENTS.find(item => item.id === patientId);
    if (p) {
      this.selectedPatient.set(p);
      this.overrideReasons.set({});
      this.showOverrideFor.set({});
      this.issuedCode.set(null);

      const m = DEMO_MOLECULES.find(item => item.code === moleculeCode);
      if (m) {
        this.selectedMolecule.set(m);
        this.drugSearch.set(m.nameEn);
        this.doseQty.set(qty);
        this.doseUnit.set(unit);
        this.frequency.set(freq);
        this.showResults.set(false);
        this.runSafetyCheck();
      }
    }
  }

  selectMolecule(m: MoleculeResult) {
    this.selectedMolecule.set(m);
    this.drugSearch.set(m.nameEn);
    this.showResults.set(false);
    this.runSafetyCheck();
  }

  runSafetyCheck() {
    const patient = this.selectedPatient();
    const mol = this.selectedMolecule();
    if (!patient || !mol) return;

    this.isChecking.set(true);
    this.findings.set([]);
    this.overrideReasons.set({});
    this.showOverrideFor.set({});

    // Try the real API first; fall back to demo simulation
    const body = {
      patient: {
        ageYears: patient.ageYears,
        weightKg: patient.weightKg,
        gender: patient.gender,
        isPregnant: patient.isPregnant,
        renalImpairment: patient.renalImpairment,
        activeMedications: patient.activeMedications,
        allergies: patient.allergies,
        conditionCodes: patient.conditionCodes
      },
      proposedMedication: {
        moleculeCode: mol.code,
        doseQuantity: this.doseQty(),
        doseUnit: this.doseUnit(),
        frequency: this.frequency(),
        durationDays: this.duration()
      }
    };

    this.http.post<{ findings: SafetyFinding[] }>(`${this.apiBaseUrl}/api/safety/check`, body, {}).subscribe({
      next: res => {
        this.isChecking.set(false);
        this.findings.set(res.findings ?? []);
      },
      error: () => {
        // Fallback: local demo safety simulation when API is offline
        setTimeout(() => {
          this.isChecking.set(false);
          this.findings.set(this.simulateLocalCheck(patient, mol));
        }, 600);
      }
    });
  }

  private simulateLocalCheck(patient: Patient, mol: MoleculeResult): SafetyFinding[] {
    const results: SafetyFinding[] = [];

    // DDI: Warfarin + NSAID
    const isNsaid = ['M01AE01', 'M01AB05'].includes(mol.code);
    const hasWarfarin = patient.activeMedications.some(m => m.moleculeCode === 'B01AA03');
    if (isNsaid && hasWarfarin) {
      results.push({
        code: 'DDI.WARFARIN.NSAID', severity: 'Major',
        titleEn: 'Major Bleeding Risk: Warfarin + NSAID',
        titleAr: 'خطر نزيف شديد: وارفارين + مضاد التهاب',
        explanationEn: 'Concomitant use significantly elevates the risk of severe, potentially life-threatening gastrointestinal hemorrhage due to dual antiplatelet and anticoagulant effect.',
        explanationAr: 'الاستخدام المتزامن يرفع خطر النزيف الهضمي الحاد المهدد للحياة نتيجة التأثير المضاعف على الصفائح والتخثر.',
        involvedMolecules: ['B01AA03', mol.code],
        source: 'BNF Appendix 1; FDA Black Box Warning for NSAIDs + Anticoagulants.',
        requiresReasonToOverride: true
      });
    }

    // Allergy Cross-Reactivity: Beta-Lactam allergy + Amoxicillin
    const hasBetaLactamAllergy = patient.allergies.some(a =>
      a.allergyGroupId === 'ALLERGY_BETA_LACTAMS' || a.code === 'ALLERGY_BETA_LACTAMS');
    if (hasBetaLactamAllergy && ['J01CA04', 'J01CR02', 'J01DB01', 'J01DD04'].includes(mol.code)) {
      results.push({
        code: 'ALLERGY.CROSS.ALLERGY_BETA_LACTAMS', severity: 'Major',
        titleEn: 'Allergy Cross-Reactivity: Beta-Lactam Antibiotics',
        titleAr: 'تحسس تبادلي: مضادات بيتا لاكتام',
        explanationEn: 'Patient has a documented penicillin/beta-lactam allergy. Prescribing Amoxicillin carries 1-3% cross-reactivity risk and risk of anaphylaxis.',
        explanationAr: 'المريض لديه حساسية موثقة للبنسلين. صرف الأموكسيسيلين ينطوي على خطر تفاعل تبادلي واحتمال صدمة تأقية.',
        involvedMolecules: [mol.code],
        source: 'AAAAI/ACAAI Drug Allergy Practice Parameter (2022).',
        requiresReasonToOverride: true
      });
    }

    // Pregnancy Category X
    const pregnancyCatX = ['D10BA01', 'L04AX03', 'C10AA05'].includes(mol.code);
    if (patient.isPregnant && pregnancyCatX) {
      results.push({
        code: `PREG.CATEGORY_X.${mol.code}`, severity: 'Contraindicated',
        titleEn: `Pregnancy Contraindication: ${mol.nameEn} (Category X)`,
        titleAr: `مضاد استطباب في الحمل: ${mol.nameAr} (فئة X)`,
        explanationEn: 'This drug is FDA Pregnancy Category X — demonstrated fetal abnormalities. Absolute contraindication in pregnancy regardless of indication.',
        explanationAr: 'هذا الدواء صنف X للحمل — موثق تشوهات جنينية. مضاد استطباب مطلق في الحمل بصرف النظر عن المؤشر.',
        involvedMolecules: [mol.code],
        source: 'FDA Pregnancy Category X Classification.',
        requiresReasonToOverride: true
      });
    }

    // Renal: Metformin in severe CKD
    const isSevereCKD = patient.renalImpairment?.toLowerCase().includes('severe') ?? false;
    if (mol.code === 'A10BA02' && isSevereCKD) {
      results.push({
        code: 'RENAL.CONTRAINDICATION.METFORMIN', severity: 'Contraindicated',
        titleEn: 'Metformin Contraindicated in Severe Renal Impairment',
        titleAr: 'يحظر ميتفورمين في القصور الكلوي الشديد',
        explanationEn: 'Metformin clearance is predominantly renal. Severe impairment causes accumulation and potentially fatal lactic acidosis.',
        explanationAr: 'يعتمد تصريف الميتفورمين على الكلى. القصور الشديد يتسبب في التراكم والحماض اللبني المميت.',
        involvedMolecules: [mol.code],
        source: 'FDA Black Box Warning; ADA/KDIGO Guidelines.',
        requiresReasonToOverride: true
      });
    }

    // Age: Aspirin in children
    if (['N02BA01', 'B01AC06'].includes(mol.code) && patient.ageYears < 16) {
      results.push({
        code: 'AGE.PAEDIATRIC.REYE_SYNDROME', severity: 'Contraindicated',
        titleEn: "Aspirin Contraindicated Under 16 (Reye's Syndrome)",
        titleAr: 'يحظر الأسبرين دون 16 عاماً (متلازمة راي)',
        explanationEn: "Salicylate administration in children under 16 can trigger potentially fatal Reye's syndrome: acute encephalopathy and liver failure.",
        explanationAr: 'الساليسيلات للأطفال دون 16 سنة قد تسبب متلازمة راي: التهاب دماغ حاد وفشل كبدي مميت.',
        involvedMolecules: [mol.code],
        source: 'MHRA Safety Warning; AAP Committee on Infectious Diseases.',
        requiresReasonToOverride: true
      });
    }

    // Paediatric dose check: Amoxicillin 1000mg to a 5yr child (18kg → max = 90*18 = 1620mg/day; 3×1000=3000 > 1620)
    if (mol.code === 'J01CA04' && patient.ageYears < 12 && patient.weightKg) {
      const maxDailyMg = 90 * patient.weightKg;
      const dosesPerDay = this.frequency() === 'TID' ? 3 : this.frequency() === 'BID' ? 2 : 1;
      const dailyMg = this.doseQty() * dosesPerDay;
      if (dailyMg > maxDailyMg * 1.05) {
        results.push({
          code: 'DOSE.PAEDIATRIC.EXCEEDED_DAILY', severity: 'Major',
          titleEn: 'Excessive Paediatric Daily Dose: Amoxicillin',
          titleAr: 'تجاوز جرعة الأطفال اليومية: أموكسيسيلين',
          explanationEn: `Proposed daily dose (${dailyMg.toFixed(0)} mg) exceeds the maximum safe paediatric dose for this weight (${maxDailyMg.toFixed(0)} mg/day = 90 mg/kg/day × ${patient.weightKg} kg).`,
          explanationAr: `الجرعة اليومية المقترحة (${dailyMg.toFixed(0)} مغ) تتجاوز الحد الأقصى الآمن لوزن الطفل (${maxDailyMg.toFixed(0)} مغ/يوم).`,
          involvedMolecules: [mol.code],
          source: 'IDSA Paediatric Dosing Guidelines; BNF for Children.',
          requiresReasonToOverride: true
        });
      }
    }

    return results;
  }

  toggleOverride(code: string) {
    this.showOverrideFor.update(s => ({ ...s, [code]: !s[code] }));
  }

  setOverrideReason(code: string, reason: string) {
    this.overrideReasons.update(r => ({ ...r, [code]: reason }));
  }

  signPrescription() {
    if (!this.canSign()) return;
    this.issuedCode.set(generateShortCode());
  }

  closePrescription() {
    this.issuedCode.set(null);
    this.selectedMolecule.set(null);
    this.drugSearch.set('');
    this.findings.set([]);
    this.overrideReasons.set({});
    this.showOverrideFor.set({});
  }

  severityClass(s: string) {
    return `warning-banner--${s.toLowerCase()}`;
  }

  severityIcon(s: string) {
    return { Contraindicated: '🚫', Major: '⛔', Moderate: '⚠️', Minor: 'ℹ️' }[s] ?? '⚠️';
  }

  get isRtl() { return this.lang() === 'ar'; }
}
