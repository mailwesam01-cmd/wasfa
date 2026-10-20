using Microsoft.EntityFrameworkCore;
using Wasfa.Infrastructure.Persistence;
using Wasfa.Safety.Contracts;
using Wasfa.Safety.Engine;
using Wasfa.Safety.KnowledgeBase;

var builder = WebApplication.CreateBuilder(args);

// Configure Database (PostgreSQL if connection string or env var is set, otherwise SQLite for zero-setup local dev)
var postgresConn = builder.Configuration.GetConnectionString("PostgreSqlConnection")
    ?? Environment.GetEnvironmentVariable("WASFA_POSTGRES_CONNECTION");

builder.Services.AddDbContext<WasfaDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(postgresConn))
    {
        options.UseNpgsql(postgresConn);
    }
    else
    {
        var dbPath = Path.Combine(AppContext.BaseDirectory, "wasfa.db");
        options.UseSqlite($"Data Source={dbPath}");
    }
});

// Configure Knowledge Base & Safety Engine
var kbDir = FindDirectoryUp(AppContext.BaseDirectory, "knowledge-base")
    ?? Path.Combine(Directory.GetCurrentDirectory(), "knowledge-base");

builder.Services.AddSingleton<IDrugKnowledgeProvider>(new JsonKnowledgeProvider(kbDir));
builder.Services.AddScoped<ISafetyEngine>(sp => new SafetyEngine(sp.GetRequiredService<IDrugKnowledgeProvider>()));

// Add Controllers with JSON formatting
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Configure CORS for Angular prescriber and Flutter apps
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

// Auto-migrate & seed demo data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WasfaDbContext>();
    await DbSeeder.SeedAsync(db);
}

app.UseCors();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();

// Root metadata & health check endpoint
app.MapGet("/", () => Results.Ok(new
{
    name = "Wasfa (وصفة) HL7 FHIR Medication-Safety Platform",
    tagline = "One patient. One medication list. Checked before it's dispensed.",
    version = "1.0.0",
    fhirStandard = "HL7 FHIR Release 4 (R4)",
    clinicalDisclaimer = "/CLINICAL-DISCLAIMER.md",
    endpoints = new
    {
        fhirMetadata = "/fhir/metadata",
        fhirPatients = "/fhir/Patient",
        fhirMedicationRequests = "/fhir/MedicationRequest",
        fhirSafetyCheck = "POST /fhir/MedicationRequest/$check-safety",
        knowledgeStats = "/api/knowledge/stats",
        knowledgeMolecules = "/api/knowledge/molecules",
        knowledgeBrands = "/api/knowledge/brands",
        prescriptions = "/api/prescription/{shortCode}",
        auditLogs = "/api/audit"
    },
    demoPatients = new[]
    {
        new { id = "pat-1001", name = "Ahmad Al-Majali (أحمد المجالي)", scenario = "Warfarin + Ibuprofen (DDI Major Bleeding Risk)" },
        new { id = "pat-1002", name = "Fatima Al-Zoubi (فاطمة الزعبي)", scenario = "Panadol + Adol (Duplicate Active Molecule)" },
        new { id = "pat-1003", name = "Tareq Al-Khatib (طارق الخطيب)", scenario = "Penicillin Allergy + Amoxicillin (Cross-Reactivity)" },
        new { id = "pat-1004", name = "Noor Al-Husseini (نور الحسيني)", scenario = "Pregnant + Roaccutane (Category X Contraindicated)" },
        new { id = "pat-1005", name = "Zaid Haddad (زيد حداد)", scenario = "Child 5yo + Adult Dose 1000mg Amoxil (Paediatric Overdose)" },
        new { id = "pat-1006", name = "Abu Omar Al-Nabulsi (أبو عمر)", scenario = "Simvastatin + Clarithromycin (Rhabdomyolysis Contraindicated)" },
        new { id = "pat-1007", name = "Um Youssef Al-Qadi (أم يوسف)", scenario = "Severe Renal Impairment + Metformin (Lactic Acidosis Contraindicated)" },
        new { id = "pat-1008", name = "Leen Al-Shawabkeh (لين)", scenario = "Child 8yo + Aspirin (Reye Syndrome Contraindicated)" },
        new { id = "pat-1009", name = "Khalil Al-Abbadi (خليل)", scenario = "Metformin + Paracetamol (Safe Regimen - 0 Warnings)" }
    }
}));

app.MapControllers();

app.Run();

static string? FindDirectoryUp(string startPath, string targetName)
{
    var current = new DirectoryInfo(startPath);
    while (current != null)
    {
        var candidate = Path.Combine(current.FullName, targetName);
        if (Directory.Exists(candidate))
        {
            return candidate;
        }
        current = current.Parent;
    }
    return null;
}
