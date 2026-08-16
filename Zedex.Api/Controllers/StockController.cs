using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Api.DTOs.Stock;
using Zedex.Api.Extensions;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;

namespace Zedex.Api.Controllers;

[ApiController]
[Route("api/stock")]
[Authorize]
[Produces("application/json")]
public class StockController : ControllerBase
{
    private readonly AppDbContext _db;

    public StockController(AppDbContext db) => _db = db;

    // ── GET /api/stock ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns current stock for all products, sorted by Category → Name.
    /// PerFoot products include a StockPieces breakdown; PerUnit products have StockPieces = null.
    /// Supports optional filtering by categoryId and/or a search term (matches product name).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<StockProductDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStock(
        [FromQuery] string? search,
        [FromQuery] int?    categoryId)
    {
        if (!User.HasModule(AppModule.Stock)) return Forbid();

        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Color)
            .Include(p => p.Gauge)
            .Include(p => p.StockPieces.OrderByDescending(sp => sp.LengthFt))
            .Where(p => !p.IsDeleted);

        if (categoryId.HasValue)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term));
        }

        var products = await query
            .OrderBy(p => p.Category.Name)
            .ThenBy(p => p.Name)
            .ToListAsync();

        var result = products.Select(p => new StockProductDto
        {
            ProductId    = p.Id,
            Name         = p.Name,
            Category     = p.Category.Name,
            CategoryId   = p.CategoryId,
            Color        = p.Color.Name,
            Gauge        = p.Gauge.Name,
            PricingMode  = p.PricingMode.ToString(),
            CurrentStock = p.CurrentStock,
            Price        = p.Price,
            // Only PerFoot products get the piece breakdown — filter out zero-qty pieces.
            StockPieces  = p.PricingMode == PricingMode.PerFoot
                ? p.StockPieces
                    .Where(sp => sp.Quantity > 0)
                    .OrderByDescending(sp => sp.LengthFt)
                    .Select(sp => new StockPieceDto
                    {
                        LengthFt = sp.LengthFt,
                        Quantity = sp.Quantity
                    }).ToList()
                : null
        }).ToList();

        return Ok(result);
    }

    // ── GET /api/stock/{id} ───────────────────────────────────────────────────

    /// <summary>
    /// Returns full detail for a single product, including all piece lengths
    /// (even zero-quantity ones, so the mobile app can show a complete breakdown).
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(StockProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProduct(int id)
    {
        if (!User.HasModule(AppModule.Stock)) return Forbid();

        var p = await _db.Products
            .AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Color)
            .Include(x => x.Gauge)
            .Include(x => x.StockPieces.OrderByDescending(sp => sp.LengthFt))
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted);

        if (p is null) return NotFound(new { message = "Product not found." });

        return Ok(new StockProductDto
        {
            ProductId    = p.Id,
            Name         = p.Name,
            Category     = p.Category.Name,
            CategoryId   = p.CategoryId,
            Color        = p.Color.Name,
            Gauge        = p.Gauge.Name,
            PricingMode  = p.PricingMode.ToString(),
            CurrentStock = p.CurrentStock,
            Price        = p.Price,
            StockPieces  = p.StockPieces
                .OrderByDescending(sp => sp.LengthFt)
                .Select(sp => new StockPieceDto
                {
                    LengthFt = sp.LengthFt,
                    Quantity = sp.Quantity
                }).ToList()
        });
    }
}
