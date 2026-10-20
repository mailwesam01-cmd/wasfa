using Microsoft.AspNetCore.Mvc;
using Wasfa.Safety.KnowledgeBase;

namespace Wasfa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KnowledgeController : ControllerBase
{
    private readonly IDrugKnowledgeProvider _provider;

    public KnowledgeController(IDrugKnowledgeProvider provider)
    {
        _provider = provider;
    }

    /// <summary>
    /// Search molecules with ATC and RxNorm codes
    /// </summary>
    [HttpGet("molecules")]
    public async Task<IActionResult> SearchMolecules([FromQuery] string? query)
    {
        var molecules = await _provider.SearchMoleculesAsync(query ?? string.Empty);
        return Ok(molecules);
    }

    /// <summary>
    /// Search Jordanian registered brand names
    /// </summary>
    [HttpGet("brands")]
    public async Task<IActionResult> SearchBrands([FromQuery] string? query)
    {
        var brands = await _provider.SearchBrandsAsync(query ?? string.Empty);
        return Ok(brands);
    }

    /// <summary>
    /// Knowledge base statistics and provenance disclaimer
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var molecules = await _provider.GetAllMoleculesAsync();
        var brands = await _provider.GetAllBrandsAsync();

        return Ok(new
        {
            provider = "Wasfa Starter Open Knowledge Base (JFDA / WHO ATC / RxNorm)",
            moleculesCount = molecules.Count,
            brandsCount = brands.Count,
            rulesCount = 7,
            disclaimer = "Demonstration clinical dataset. See CLINICAL-DISCLAIMER.md for usage guidelines.",
            isPluggable = true
        });
    }
}
