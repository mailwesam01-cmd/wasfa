using Wasfa.Domain.KnowledgeBase;

namespace Wasfa.Safety.KnowledgeBase;

public interface IDrugKnowledgeProvider
{
    Task<MoleculeDefinition?> GetMoleculeByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<MoleculeDefinition>> SearchMoleculesAsync(string query, CancellationToken ct = default);
    Task<IReadOnlyList<BrandMapping>> SearchBrandsAsync(string query, CancellationToken ct = default);
    Task<IReadOnlyList<InteractionRuleDefinition>> GetInteractionsForMoleculeAsync(string moleculeCode, CancellationToken ct = default);
    Task<AllergyCrossReactivityGroup?> GetAllergyGroupForMoleculeAsync(string moleculeCode, CancellationToken ct = default);
    Task<DoseRangeDefinition?> GetDoseRangeForMoleculeAsync(string moleculeCode, CancellationToken ct = default);
    Task<IReadOnlyList<MoleculeDefinition>> GetAllMoleculesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<BrandMapping>> GetAllBrandsAsync(CancellationToken ct = default);
}
