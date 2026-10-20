using System.Text.Json;
using Wasfa.Domain.KnowledgeBase;

namespace Wasfa.Safety.KnowledgeBase;

public sealed class JsonKnowledgeProvider : IDrugKnowledgeProvider
{
    private readonly Dictionary<string, MoleculeDefinition> _moleculesByCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<BrandMapping> _brands = new();
    private readonly List<InteractionRuleDefinition> _interactions = new();
    private readonly List<AllergyCrossReactivityGroup> _allergyGroups = new();
    private readonly Dictionary<string, DoseRangeDefinition> _doseRanges = new(StringComparer.OrdinalIgnoreCase);

    public JsonKnowledgeProvider(string knowledgeBaseDirectory)
    {
        LoadKnowledgeBase(knowledgeBaseDirectory);
    }

    private void LoadKnowledgeBase(string directory)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var moleculesPath = Path.Combine(directory, "molecules.json");
        if (File.Exists(moleculesPath))
        {
            var list = JsonSerializer.Deserialize<List<MoleculeDefinition>>(File.ReadAllText(moleculesPath), jsonOptions);
            if (list != null)
            {
                foreach (var mol in list)
                {
                    _moleculesByCode[mol.Code] = mol;
                }
            }
        }

        var brandsPath = Path.Combine(directory, "brands-jo.json");
        if (File.Exists(brandsPath))
        {
            var list = JsonSerializer.Deserialize<List<BrandMapping>>(File.ReadAllText(brandsPath), jsonOptions);
            if (list != null)
            {
                _brands.AddRange(list);
            }
        }

        var interactionsPath = Path.Combine(directory, "interactions.json");
        if (File.Exists(interactionsPath))
        {
            var list = JsonSerializer.Deserialize<List<InteractionRuleDefinition>>(File.ReadAllText(interactionsPath), jsonOptions);
            if (list != null)
            {
                _interactions.AddRange(list);
            }
        }

        var allergyPath = Path.Combine(directory, "allergy-groups.json");
        if (File.Exists(allergyPath))
        {
            var list = JsonSerializer.Deserialize<List<AllergyCrossReactivityGroup>>(File.ReadAllText(allergyPath), jsonOptions);
            if (list != null)
            {
                _allergyGroups.AddRange(list);
            }
        }

        var dosePath = Path.Combine(directory, "dose-ranges.json");
        if (File.Exists(dosePath))
        {
            var list = JsonSerializer.Deserialize<List<DoseRangeDefinition>>(File.ReadAllText(dosePath), jsonOptions);
            if (list != null)
            {
                foreach (var d in list)
                {
                    _doseRanges[d.MoleculeCode] = d;
                }
            }
        }
    }

    public Task<MoleculeDefinition?> GetMoleculeByCodeAsync(string code, CancellationToken ct = default)
    {
        _moleculesByCode.TryGetValue(code, out var def);
        return Task.FromResult(def);
    }

    public Task<IReadOnlyList<MoleculeDefinition>> SearchMoleculesAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<MoleculeDefinition>>(_moleculesByCode.Values.ToList());
        }

        var q = query.Trim().ToLowerInvariant();
        var matches = _moleculesByCode.Values
            .Where(m => m.NameEn.ToLowerInvariant().Contains(q) ||
                        m.NameAr.Contains(q) ||
                        m.Code.ToLowerInvariant().Contains(q) ||
                        m.Atc.ToLowerInvariant().Contains(q))
            .ToList();

        return Task.FromResult<IReadOnlyList<MoleculeDefinition>>(matches);
    }

    public Task<IReadOnlyList<BrandMapping>> SearchBrandsAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult<IReadOnlyList<BrandMapping>>(_brands);
        }

        var q = query.Trim().ToLowerInvariant();
        var matches = _brands
            .Where(b => b.BrandName.ToLowerInvariant().Contains(q) ||
                        b.BrandNameAr.Contains(q) ||
                        b.MoleculeCode.ToLowerInvariant().Contains(q))
            .ToList();

        return Task.FromResult<IReadOnlyList<BrandMapping>>(matches);
    }

    public Task<IReadOnlyList<InteractionRuleDefinition>> GetInteractionsForMoleculeAsync(string moleculeCode, CancellationToken ct = default)
    {
        var matches = _interactions
            .Where(i => string.Equals(i.MoleculeCodeA, moleculeCode, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(i.MoleculeCodeB, moleculeCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return Task.FromResult<IReadOnlyList<InteractionRuleDefinition>>(matches);
    }

    public Task<AllergyCrossReactivityGroup?> GetAllergyGroupForMoleculeAsync(string moleculeCode, CancellationToken ct = default)
    {
        var group = _allergyGroups.FirstOrDefault(g =>
            g.Subgroups.Any(sg => sg.MoleculeCodes.Any(mc => string.Equals(mc, moleculeCode, StringComparison.OrdinalIgnoreCase))));

        return Task.FromResult(group);
    }

    public Task<DoseRangeDefinition?> GetDoseRangeForMoleculeAsync(string moleculeCode, CancellationToken ct = default)
    {
        _doseRanges.TryGetValue(moleculeCode, out var def);
        return Task.FromResult(def);
    }

    public Task<IReadOnlyList<MoleculeDefinition>> GetAllMoleculesAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<MoleculeDefinition>>(_moleculesByCode.Values.ToList());
    }

    public Task<IReadOnlyList<BrandMapping>> GetAllBrandsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<BrandMapping>>(_brands);
    }
}
