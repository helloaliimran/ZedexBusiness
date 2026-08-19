using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Api.DTOs.Lookups;
using Zedex.Infrastructure.Persistence;

namespace Zedex.Api.Controllers;

[ApiController]
[Route("api/lookups")]
[AllowAnonymous]
[Produces("application/json")]
public class LookupsController : ControllerBase
{
    private readonly AppDbContext _db;

    public LookupsController(AppDbContext db) => _db = db;

    // ── GET /api/lookups ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns all product master-data options — Colors, Gauges, Categories, Companies —
    /// each sorted by Name. Used to populate filter/dropdown pickers on the mobile app
    /// (e.g. for the fields accepted by POST /api/products/search).
    /// No module gate: this is reference data shared across Products, Stock, and Billing,
    /// so any authenticated user may read it.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(AllLookupsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLookups()
    {
        var colors = await _db.Colors
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new LookupItemDto { Id = c.Id, Name = c.Name })
            .ToListAsync();

        var gauges = await _db.Gauges
            .AsNoTracking()
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.Name)
            .Select(g => new LookupItemDto { Id = g.Id, Name = g.Name })
            .ToListAsync();

        var categories = await _db.Categories
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new LookupItemDto { Id = c.Id, Name = c.Name })
            .ToListAsync();

        var companies = await _db.Companies
            .AsNoTracking()
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.Name)
            .Select(c => new LookupItemDto { Id = c.Id, Name = c.Name })
            .ToListAsync();

        return Ok(new AllLookupsDto
        {
            Colors     = colors,
            Gauges     = gauges,
            Categories = categories,
            Companies  = companies
        });
    }
}
