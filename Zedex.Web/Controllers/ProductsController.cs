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

    public async Task<IActionResult> Index(string? search, int? categoryId, int? colorId, int? gaugeId, PricingMode? mode, int page = 1)
    {
        // PVC products are managed in their own module (PvcProductsController).
        var query = _db.Products.AsNoTracking()
            .Where(p => !p.Category.IsPvc);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, pattern) ||
                EF.Functions.ILike(p.Color.Name, pattern) ||
                EF.Functions.ILike(p.Gauge.Name, pattern));
        }
        if (categoryId is > 0)
            query = query.Where(p => p.CategoryId == categoryId);
        if (colorId is > 0)
            query = query.Where(p => p.ColorId == colorId);
        if (gaugeId is > 0)
            query = query.Where(p => p.GaugeId == gaugeId);
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
            await _db.Categories.AsNoTracking()
                .Where(c => !c.IsPvc)
                .OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", categoryId);
        ViewBag.Colors = new SelectList(
            await _db.Colors.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", colorId);
        ViewBag.Gauges = new SelectList(
            await _db.Gauges.AsNoTracking().OrderBy(g => g.Name).ToListAsync(),
            "Id", "Name", gaugeId);

        return View(new ProductListViewModel
        {
            Search = search,
            CategoryId = categoryId,
            ColorId = colorId,
            GaugeId = gaugeId,
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

    // ---------- Quick entry (bulk add) ----------

    [HttpGet]
    public async Task<IActionResult> QuickEntry()
    {
        await LoadLookupsAsync();
        return View(new ProductQuickEntryViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickEntry(ProductQuickEntryViewModel vm)
    {
        // A row counts as "used" if anything meaningful was typed in it.
        vm.Rows = vm.Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Name) || r.CategoryId > 0 || r.ColorId > 0 || r.GaugeId > 0 || (r.Price ?? 0) > 0)
            .ToList();
        if (vm.Rows.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one product row.");

        var seen = new HashSet<string>();
        for (var i = 0; i < vm.Rows.Count; i++)
        {
            var row = vm.Rows[i];
            var line = $"Row {i + 1}";
            if (string.IsNullOrWhiteSpace(row.Name))
                ModelState.AddModelError(string.Empty, $"{line}: product name is required.");
            if (row.CategoryId <= 0)
                ModelState.AddModelError(string.Empty, $"{line}: select a category.");
            if (row.ColorId <= 0)
                ModelState.AddModelError(string.Empty, $"{line}: select a color.");
            if (row.GaugeId <= 0)
                ModelState.AddModelError(string.Empty, $"{line}: select a gauge.");
            if ((row.Price ?? 0) <= 0)
                ModelState.AddModelError(string.Empty, $"{line}: enter a valid price.");

            if (string.IsNullOrWhiteSpace(row.Name))
                continue;
            var name = row.Name.Trim();

            // Duplicate within this batch?
            var key = $"{name.ToLowerInvariant()}|{row.CategoryId}|{row.ColorId}|{row.GaugeId}";
            if (!seen.Add(key))
                ModelState.AddModelError(string.Empty, $"{line}: duplicates another row in this batch (\"{name}\").");

            // Duplicate against existing products?
            var exists = await _db.Products.AnyAsync(p =>
                p.CategoryId == row.CategoryId &&
                p.ColorId == row.ColorId &&
                p.GaugeId == row.GaugeId &&
                EF.Functions.ILike(p.Name, name));
            if (exists)
                ModelState.AddModelError(string.Empty,
                    $"{line}: \"{name}\" already exists with the same category, color, and gauge.");
        }

        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            return View(vm);
        }

        foreach (var row in vm.Rows)
        {
            _db.Products.Add(new Product
            {
                Name = row.Name!.Trim(),
                CategoryId = row.CategoryId,
                ColorId = row.ColorId,
                GaugeId = row.GaugeId,
                PricingMode = row.PricingMode,
                Price = row.Price!.Value
            });
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{vm.Rows.Count} product(s) added.";
        return RedirectToAction(nameof(QuickEntry)); // fresh grid — keep entering
    }

    // ---------- Bulk rate update ----------

    [HttpGet]
    public async Task<IActionResult> Rates()
    {
        var items = await _db.Products.AsNoTracking()
            .Where(p => !p.Category.IsPvc)
            .OrderBy(p => p.Name)
            .Select(p => new ProductRateItemViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Category = p.Category.Name,
                Color = p.Color.Name,
                Gauge = p.Gauge.Name,
                PricingMode = p.PricingMode,
                Price = p.Price
            })
            .ToListAsync();
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRates(List<ProductRateUpdateViewModel> changes)
    {
        changes = (changes ?? new()).Where(c => c.Id > 0 && c.Price > 0).ToList();
        if (changes.Count == 0)
        {
            TempData["Error"] = "No rate changes to save.";
            return RedirectToAction(nameof(Rates));
        }

        var ids = changes.Select(c => c.Id).ToList();
        var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var updated = 0;
        foreach (var change in changes)
        {
            if (products.TryGetValue(change.Id, out var product) && product.Price != change.Price)
            {
                product.Price = change.Price;
                updated++;
            }
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{updated} rate(s) updated.";
        return RedirectToAction(nameof(Rates));
    }

    // ---------- Delete ----------

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
            await _db.Categories.AsNoTracking()
                .Where(c => !c.IsPvc)
                .OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", vm?.CategoryId);
        ViewBag.Colors = new SelectList(
            await _db.Colors.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", vm?.ColorId);
        ViewBag.Gauges = new SelectList(
            await _db.Gauges.AsNoTracking().OrderBy(g => g.Name).ToListAsync(),
            "Id", "Name", vm?.GaugeId);
    }
}
