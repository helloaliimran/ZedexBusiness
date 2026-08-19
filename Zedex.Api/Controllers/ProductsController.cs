using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Api.DTOs.Products;
using Zedex.Api.Extensions;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;

namespace Zedex.Api.Controllers;

[ApiController]
[Route("api/products")]
[AllowAnonymous]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db) => _db = db;

    // ── POST /api/products/search ─────────────────────────────────────────────

    /// <summary>
    /// Batch product search. Accepts a list of search rows — each row's ProductName is
    /// required, Color/Gauge/Category/Company are optional refinements that may differ
    /// from row to row. Every field is matched with a case-insensitive "contains" (LIKE)
    /// search on both sides (both the stored value and the input are lower-cased), so a
    /// ProductName of "DC26" matches a stored product named "DC26C". Rows are searched
    /// independently; results come back grouped by row, in submission order.
    /// </summary>
    [HttpPost("search")]
    [ProducesResponseType(typeof(List<ProductSearchGroupDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchProducts([FromBody] List<ProductSearchRequestDto> items)
    {
        //if (!User.HasModule(AppModule.Products) &&
        //    !User.HasModule(AppModule.Billing) &&
        //    !User.HasModule(AppModule.Stock))
        //    return Forbid();

        if (items is null || items.Count == 0)
            return BadRequest(new { message = "At least one search row is required." });

        var results = new List<ProductSearchGroupDto>(items.Count);

        for (int index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var nameTerm = item.ProductName.Trim().ToLower();

            var query = _db.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Color)
                .Include(p => p.Gauge)
                .Include(p => p.Company)
                .Where(p => !p.IsDeleted && p.Name.ToLower().Contains(nameTerm));

            if (!string.IsNullOrWhiteSpace(item.Color))
            {
                var term = item.Color.Trim().ToLower();
                query = query.Where(p => p.Color.Name.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(item.Gauge))
            {
                var term = item.Gauge.Trim().ToLower();
                query = query.Where(p => p.Gauge.Name.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(item.Category))
            {
                var term = item.Category.Trim().ToLower();
                query = query.Where(p => p.Category.Name.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(item.Company))
            {
                var term = item.Company.Trim().ToLower();
                query = query.Where(p => p.Company != null && p.Company.Name.ToLower().Contains(term));
            }

            var matches = await query
                .OrderBy(p => p.Name)
                .Select(p => new ProductSearchResultDto
                {
                    ProductId = p.Id,
                    Name      = p.Name,
                    Color     = p.Color.Name,
                    Gauge     = p.Gauge.Name,
                    Category  = p.Category.Name,
                    Company   = p.Company != null ? p.Company.Name : null,
                    Rate      = p.Price
                })
                .ToListAsync();

            results.Add(new ProductSearchGroupDto
            {
                Index       = index,
                ProductName = item.ProductName,
                Matches     = matches
            });
        }

        return Ok(results);
    }
}
