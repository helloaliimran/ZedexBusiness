using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Entities;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Authorization;
using Zedex.Web.Models;

namespace Zedex.Web.Controllers;

[Authorize(Policy = "Module:Products")]
public class ProductsController : Controller
{
    private readonly AppDbContext _db;
    private const int PageSize = 10;

    public ProductsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? search, int? categoryId, PricingMode? mode, int page = 1)
    {
        var query = _db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{search.Trim()}%"));
        if (categoryId is > 0)
            query = query.Where(p => p.CategoryId == categoryId);
        if (mode is not null)
            query = query.Where(p => p.PricingMode == mode);

        var total = await query.CountAsync();
        page = Math.Max(1, page);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(p => new ProductListItemViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Category = p.Category.Name,
                Color = p.Color.Name,
                Gauge = p.Gauge.Name,
                PricingMode = p.PricingMode,
                Price = p.Price,
                CurrentStock = p.CurrentStock
            })
            .ToListAsync();

        ViewBag.Categories = new SelectList(
            await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", categoryId);

        return View(new ProductListViewModel
        {
            Search = search,
            CategoryId = categoryId,
            Mode = mode,
            Items = new PagedResult<ProductListItemViewModel>
            {
                Items = items, Page = page, PageSize = PageSize, TotalCount = total
            }
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadLookupsAsync();
        return View(new ProductFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(vm);
            return View(vm);
        }

        if (await IsDuplicateAsync(vm))
        {
            ModelState.AddModelError(nameof(vm.Name),
                "A product with the same name, category, color, and gauge already exists.");
            await LoadLookupsAsync(vm);
            return View(vm);
        }

        var product = new Product
        {
            Name = vm.Name.Trim(),
            CategoryId = vm.CategoryId,
            ColorId = vm.ColorId,
            GaugeId = vm.GaugeId,
            PricingMode = vm.PricingMode,
            Price = vm.Price,
            Description = vm.Description?.Trim()
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Product \"{product.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null || product.IsDeleted)
            return NotFound();

        var vm = new ProductFormViewModel
        {
            Id = product.Id,
            Name = product.Name,
            CategoryId = product.CategoryId,
            ColorId = product.ColorId,
            GaugeId = product.GaugeId,
            PricingMode = product.PricingMode,
            Price = product.Price,
            Description = product.Description
        };
        await LoadLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ProductFormViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(vm);
            return View(vm);
        }

        var product = await _db.Products.FindAsync(vm.Id);
        if (product is null || product.IsDeleted)
            return NotFound();

        if (await IsDuplicateAsync(vm))
        {
            ModelState.AddModelError(nameof(vm.Name),
                "A product with the same name, category, color, and gauge already exists.");
            await LoadLookupsAsync(vm);
            return View(vm);
        }

        // Changing pricing mode with stock on hand would corrupt stock semantics
        // (units vs feet), so it is blocked until stock is zero.
        if (product.PricingMode != vm.PricingMode && product.CurrentStock != 0)
        {
            ModelState.AddModelError(nameof(vm.PricingMode),
                $"Cannot change pricing mode while the product has stock ({product.CurrentStock:N2}). Adjust stock to zero first.");
            await LoadLookupsAsync(vm);
            return View(vm);
        }

        product.Name = vm.Name.Trim();
        product.CategoryId = vm.CategoryId;
        product.ColorId = vm.ColorId;
        product.GaugeId = vm.GaugeId;
        product.PricingMode = vm.PricingMode;
        product.Price = vm.Price;
        product.Description = vm.Description?.Trim();
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Product \"{product.Name}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product is null || product.IsDeleted)
            return NotFound();

        if (product.CurrentStock != 0)
        {
            TempData["Error"] = $"\"{product.Name}\" still has stock ({product.CurrentStock:N2}) and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _db.Products.Remove(product); // soft delete via AppDbContext
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Product \"{product.Name}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> IsDuplicateAsync(ProductFormViewModel vm)
    {
        var name = vm.Name.Trim();
        return await _db.Products.AnyAsync(p =>
            p.Id != vm.Id &&
            p.CategoryId == vm.CategoryId &&
            p.ColorId == vm.ColorId &&
            p.GaugeId == vm.GaugeId &&
            EF.Functions.ILike(p.Name, name));
    }

    private async Task LoadLookupsAsync(ProductFormViewModel? vm = null)
    {
        ViewBag.Categories = new SelectList(
            await _db.Categories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", vm?.CategoryId);
        ViewBag.Colors = new SelectList(
            await _db.Colors.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", vm?.ColorId);
        ViewBag.Gauges = new SelectList(
            await _db.Gauges.AsNoTracking().OrderBy(g => g.Name).ToListAsync(),
            "Id", "Name", vm?.GaugeId);
    }
}
