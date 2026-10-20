using System.Text.Json;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Wasfa.Domain.Entities;
using Wasfa.Infrastructure.Security;
using Task = System.Threading.Tasks.Task;

namespace Wasfa.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(WasfaDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (db.Patients.Any())
        {
            return; // Already seeded
        }

        var serializer = new FhirJsonSerializer(new SerializerSettings { Pretty = false });

        var seedPatients = new List<(
            string FhirId,
            string NationalId,
            string NameEn,
            string NameAr,
            DateOnly BirthDate,
            string Gender,
            decimal WeightKg,
            bool IsPregnant,
            string RenalStatus,
            string Phone,
            List<(string Code, string Name, string Criticality)> Allergies,
            List<(string Code, string Display)> Conditions,
            List<(string RequestId, string MoleculeCode, string Brand, string Strength, string Freq, int Days, string ShortCode)> ActiveMeds
        )>
        {
            (
                "pat-1001",
                "9581023456",
                "Ahmad Al-Majali",
                "أحمد المجالي",
                new DateOnly(1958, 4, 12),
                "male",
                75.0m,
                false,
                "Normal",
                "+962791234567",
                new() {},
                new() { ("I48.0", "Atrial Fibrillation"), ("I10", "Essential Hypertension") },
                new() { ("req-1001-1", "B01AA03", "Coumadin", "5 mg", "QD", 90, "WASFA-8931") }
            ),
            (
                "pat-1002",
                "9822019485",
                "Fatima Al-Zoubi",
                "فاطمة الزعبي",
                new DateOnly(1982, 8, 24),
                "female",
                68.0m,
                false,
                "Normal",
                "+962788765432",
                new() {},
                new() { ("R51", "Tension Headache") },
                new() { ("req-1002-1", "N02BE01", "Panadol", "500 mg", "TID", 14, "WASFA-4421") }
            ),
            (
                "pat-1003",
                "9931084729",
                "Tareq Al-Khatib",
                "طارق الخطيب",
                new DateOnly(1993, 2, 17),
                "male",
                70.0m,
                false,
                "Normal",
                "+962777123987",
                new() { ("ALLERGY_BETA_LACTAMS", "Penicillins & Beta-Lactams", "high") },
                new() { ("J02.9", "Acute Pharyngitis") },
                new() {}
            ),
            (
                "pat-1004",
                "0012093847",
                "Noor Al-Husseini",
                "نور الحسيني",
                new DateOnly(2001, 11, 5),
                "female",
                58.0m,
                true, // Pregnant
                "Normal",
                "+962795551234",
                new() {},
                new() { ("L70.0", "Acne Vulgaris (Severe Cystic)") },
                new() {}
            ),
            (
                "pat-1005",
                "0211048291",
                "Zaid Haddad",
                "زيد حداد",
                new DateOnly(2021, 5, 19), // 5 years old
                "male",
                18.0m,
                false,
                "Normal",
                "+962799988776",
                new() {},
                new() { ("H66.9", "Acute Otitis Media") },
                new() {}
            ),
            (
                "pat-1006",
                "9551029384",
                "Abu Omar Al-Nabulsi",
                "أبو عمر النابلسي",
                new DateOnly(1955, 3, 30),
                "male",
                80.0m,
                false,
                "Normal",
                "+962780123456",
                new() {},
                new() { ("E78.0", "Primary Hypercholesterolemia") },
                new() { ("req-1006-1", "C10AA01", "Zocor", "40 mg", "QD", 90, "WASFA-7712") }
            ),
            (
                "pat-1007",
                "9622039481",
                "Um Youssef Al-Qadi",
                "أم يوسف القاضي",
                new DateOnly(1962, 9, 14),
                "female",
                72.0m,
                false,
                "Severe", // Severe Renal Failure
                "+962796443322",
                new() {},
                new() { ("N18.4", "Chronic Kidney Disease Stage 4"), ("E11.9", "Type 2 Diabetes") },
                new() {}
            ),
            (
                "pat-1008",
                "0182049182",
                "Leen Al-Shawabkeh",
                "لين الشوابكة",
                new DateOnly(2018, 7, 21), // 8 years old
                "female",
                24.0m,
                false,
                "Normal",
                "+962770998877",
                new() {},
                new() { ("B34.9", "Viral Upper Respiratory Infection") },
                new() {}
            ),
            (
                "pat-1009",
                "9741092837",
                "Khalil Al-Abbadi",
                "خليل العبادي",
                new DateOnly(1974, 12, 1),
                "male",
                82.0m,
                false,
                "Normal",
                "+962791112233",
                new() {},
                new() { ("E11.9", "Type 2 Diabetes Mellitus") },
                new() { ("req-1009-1", "A10BA02", "Glucophage", "850 mg", "BID", 90, "WASFA-9104") }
            )
        };

        foreach (var p in seedPatients)
        {
            var natHash = NationalIdHasher.HashNationalId(p.NationalId);

            // Relational projection
            var patientProj = new PatientProjection
            {
                FhirId = p.FhirId,
                NationalIdHash = natHash,
                FullNameEn = p.NameEn,
                FullNameAr = p.NameAr,
                BirthDate = p.BirthDate,
                Gender = p.Gender,
                WeightKg = p.WeightKg,
                IsPregnant = p.IsPregnant,
                RenalImpairment = p.RenalStatus
            };
            db.Patients.Add(patientProj);

            // HL7 FHIR Patient resource
            var fhirPatient = new Patient
            {
                Id = p.FhirId,
                Active = true,
                Gender = p.Gender == "male" ? AdministrativeGender.Male : AdministrativeGender.Female,
                BirthDate = p.BirthDate.ToString("yyyy-MM-dd"),
                Name = new List<HumanName>
                {
                    new()
                    {
                        Use = HumanName.NameUse.Official,
                        Text = p.NameEn,
                        Family = p.NameEn.Split(' ').Last(),
                        Given = p.NameEn.Split(' ').Take(1)
                    },
                    new()
                    {
                        Use = HumanName.NameUse.Usual,
                        Text = p.NameAr
                    }
                },
                Identifier = new List<Identifier>
                {
                    new()
                    {
                        System = "http://wasfa.jo/fhir/sid/national-id-hash",
                        Value = natHash
                    }
                },
                Telecom = new List<ContactPoint>
                {
                    new() { System = ContactPoint.ContactPointSystem.Phone, Value = p.Phone, Use = ContactPoint.ContactPointUse.Mobile }
                }
            };

            // Extensions for weight, pregnancy, renal
            fhirPatient.Extension.Add(new Extension("http://wasfa.jo/fhir/StructureDefinition/patient-weight", new FhirDecimal(p.WeightKg)));
            fhirPatient.Extension.Add(new Extension("http://wasfa.jo/fhir/StructureDefinition/patient-pregnancy", new FhirBoolean(p.IsPregnant)));
            fhirPatient.Extension.Add(new Extension("http://wasfa.jo/fhir/StructureDefinition/patient-renal-status", new FhirString(p.RenalStatus)));

            var fhirPatientJson = serializer.SerializeToString(fhirPatient);
            db.FhirResources.Add(new FhirResourceRecord
            {
                ResourceType = "Patient",
                FhirId = p.FhirId,
                VersionId = 1,
                Content = fhirPatientJson,
                LastUpdated = DateTimeOffset.UtcNow
            });

            // Seed Allergies
            foreach (var allergy in p.Allergies)
            {
                var allergyRes = new AllergyIntolerance
                {
                    Id = $"all-{p.FhirId}-{allergy.Code.ToLowerInvariant()}",
                    Patient = new ResourceReference($"Patient/{p.FhirId}"),
                    ClinicalStatus = new CodeableConcept("http://terminology.hl7.org/CodeSystem/allergyintolerance-clinical", "active"),
                    VerificationStatus = new CodeableConcept("http://terminology.hl7.org/CodeSystem/allergyintolerance-verification", "confirmed"),
                    Type = AllergyIntolerance.AllergyIntoleranceType.Allergy,
                    Category = new List<AllergyIntolerance.AllergyIntoleranceCategory?> { AllergyIntolerance.AllergyIntoleranceCategory.Medication },
                    Criticality = allergy.Criticality == "high" ? AllergyIntolerance.AllergyIntoleranceCriticality.High : AllergyIntolerance.AllergyIntoleranceCriticality.Low,
                    Code = new CodeableConcept("http://wasfa.jo/fhir/allergy-groups", allergy.Code, allergy.Name)
                };

                db.FhirResources.Add(new FhirResourceRecord
                {
                    ResourceType = "AllergyIntolerance",
                    FhirId = allergyRes.Id,
                    VersionId = 1,
                    Content = serializer.SerializeToString(allergyRes),
                    LastUpdated = DateTimeOffset.UtcNow
                });
            }

            // Seed Conditions
            foreach (var cond in p.Conditions)
            {
                var conditionRes = new Condition
                {
                    Id = $"cond-{p.FhirId}-{cond.Code.Replace(".", "").ToLowerInvariant()}",
                    Subject = new ResourceReference($"Patient/{p.FhirId}"),
                    ClinicalStatus = new CodeableConcept("http://terminology.hl7.org/CodeSystem/condition-clinical", "active"),
                    VerificationStatus = new CodeableConcept("http://terminology.hl7.org/CodeSystem/condition-ver-status", "confirmed"),
                    Code = new CodeableConcept("http://hl7.org/fhir/sid/icd-10", cond.Code, cond.Display)
                };

                db.FhirResources.Add(new FhirResourceRecord
                {
                    ResourceType = "Condition",
                    FhirId = conditionRes.Id,
                    VersionId = 1,
                    Content = serializer.SerializeToString(conditionRes),
                    LastUpdated = DateTimeOffset.UtcNow
                });
            }

            // Seed Active Medications
            foreach (var med in p.ActiveMeds)
            {
                var medProj = new MedicationRequestProjection
                {
                    FhirId = med.RequestId,
                    PatientFhirId = p.FhirId,
                    PrescriberId = "doc-dr-omar",
                    MoleculeCode = med.MoleculeCode,
                    BrandName = med.Brand,
                    Strength = med.Strength,
                    DoseText = med.Strength,
                    Frequency = med.Freq,
                    DurationDays = med.Days,
                    Status = "active",
                    AuthoredOn = DateTimeOffset.UtcNow.AddDays(-5),
                    ShortCode = med.ShortCode
                };
                db.MedicationRequests.Add(medProj);

                var medRequest = new MedicationRequest
                {
                    Id = med.RequestId,
                    Status = MedicationRequest.MedicationrequestStatus.Active,
                    Intent = MedicationRequest.MedicationRequestIntent.Order,
                    Subject = new ResourceReference($"Patient/{p.FhirId}"),
                    Requester = new ResourceReference("Practitioner/doc-dr-omar", "Dr. Omar Al-Kayyali"),
                    AuthoredOn = DateTimeOffset.UtcNow.AddDays(-5).ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    Medication = new CodeableConcept
                    {
                        Text = $"{med.Brand} ({med.Strength})",
                        Coding = new List<Coding>
                        {
                            new("http://www.whocc.no/atc", med.MoleculeCode),
                            new("http://wasfa.jo/fhir/brands", med.Brand)
                        }
                    },
                    Identifier = new List<Identifier>
                    {
                        new("http://wasfa.jo/fhir/prescription-code", med.ShortCode)
                    }
                };

                db.FhirResources.Add(new FhirResourceRecord
                {
                    ResourceType = "MedicationRequest",
                    FhirId = med.RequestId,
                    VersionId = 1,
                    Content = serializer.SerializeToString(medRequest),
                    LastUpdated = DateTimeOffset.UtcNow
                });
            }

            // Seed Initial Consent
            db.Consents.Add(new ConsentRecord
            {
                PatientFhirId = p.FhirId,
                GranteeOrgId = "org-amman-polyclinic",
                Scope = "read_all",
                GrantedAt = DateTimeOffset.UtcNow.AddMonths(-1)
            });
        }

        // Add Initial Audit Record
        db.AuditLogs.Add(new AuditLogRecord
        {
            ActorId = "system-bootstrap",
            Action = "seed",
            ResourceType = "Patient",
            OccurredAt = DateTimeOffset.UtcNow,
            Detail = "Seeded 9 synthetic demonstration patients and scenarios."
        });

        await db.SaveChangesAsync();
    }
}
