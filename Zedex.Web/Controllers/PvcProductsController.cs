using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Entities;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;

namespace Zedex.Web.Controllers;

/// <summary>
/// PVC section products. Rows live in the shared Products table (Category = PVC,
/// PricingMode = PerFoot so the stock module tracks them as pieces × lengths),
/// but are managed through these separate screens with PVC-specific fields.
/// </summary>
[Authorize(Policy = "Module:Products")]
public class PvcProductsController : Controller
{
    public const string PvcCategoryName = "PVC";

    private readonly AppDbContext _db;
    private const int PageSize = 10;

    public PvcProductsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? search, int? companyId, PvcSaleType? saleType, int page = 1)
    {
        var pvcCategoryId = await GetPvcCategoryIdAsync();
        var query = _db.Products.AsNoTracking().Where(p => p.CategoryId == pvcCategoryId);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{search.Trim()}%"));
        if (companyId is > 0)
            query = query.Where(p => p.CompanyId == companyId);
        if (saleType is not null)
            query = query.Where(p => p.SaleType == saleType);

        var total = await query.CountAsync();
        page = Math.Max(1, page);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(p => new PvcProductListItemViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Company = p.Company != null ? p.Company.Name : "—",
                Color = p.Color.Name,
                Gauge = p.Gauge.Name,
                GasKitType = p.GasKitType ?? GasKitType.None,
                SaleType = p.SaleType ?? PvcSaleType.PerRunningFoot,
                Price = p.Price,
                WeightPerLength = p.WeightPerLength,
                CurrentStock = p.CurrentStock
            })
            .ToListAsync();

        ViewBag.Companies = new SelectList(
            await _db.Companies.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", companyId);

        return View(new PvcProductListViewModel
        {
            Search = search,
            CompanyId = companyId,
            SaleType = saleType,
            Items = new PagedResult<PvcProductListItemViewModel>
            {
                Items = items, Page = page, PageSize = PageSize, TotalCount = total
            }
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadLookupsAsync();
        return View(new PvcProductFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PvcProductFormViewModel vm)
    {
        ValidateWeight(vm);
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(vm);
            return View(vm);
        }

        if (await IsDuplicateAsync(vm))
        {
            ModelState.AddModelError(nameof(vm.Name),
                "A PVC product with the same name, company, color, and gauge already exists.");
            await LoadLookupsAsync(vm);
            return View(vm);
        }

        var product = new Product
        {
            Name = vm.Name.Trim(),
            CategoryId = await GetPvcCategoryIdAsync(),
            ColorId = vm.ColorId,
            GaugeId = vm.GaugeId,
            CompanyId = vm.CompanyId,
            // Length-based stock (pieces × feet) — same plumbing as PerFoot products.
            PricingMode = PricingMode.PerFoot,
            SaleType = vm.SaleType,
            GasKitType = vm.GasKitType,
            Price = vm.Price,
            WeightPerLength = vm.SaleType == PvcSaleType.WeightPerLength ? vm.WeightPerLength : null,
            Description = vm.Description?.Trim()
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"PVC product \"{product.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await FindPvcProductAsync(id);
        if (product is null)
            return NotFound();

        var vm = new PvcProductFormViewModel
        {
            Id = product.Id,
            Name = product.Name,
            ColorId = product.ColorId,
            GaugeId = product.GaugeId,
            CompanyId = product.CompanyId ?? 0,
            GasKitType = product.GasKitType ?? GasKitType.None,
            SaleType = product.SaleType ?? PvcSaleType.PerRunningFoot,
            Price = product.Price,
            WeightPerLength = product.WeightPerLength,
            Description = product.Description
        };
        await LoadLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PvcProductFormViewModel vm)
    {
        ValidateWeight(vm);
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(vm);
            return View(vm);
        }

        var product = await FindPvcProductAsync(vm.Id);
        if (product is null)
            return NotFound();

        if (await IsDuplicateAsync(vm))
        {
            ModelState.AddModelError(nameof(vm.Name),
                "A PVC product with the same name, company, color, and gauge already exists.");
            await LoadLookupsAsync(vm);
            return View(vm);
        }

        product.Name = vm.Name.Trim();
        product.ColorId = vm.ColorId;
        product.GaugeId = vm.GaugeId;
        product.CompanyId = vm.CompanyId;
        product.SaleType = vm.SaleType;
        product.GasKitType = vm.GasKitType;
        product.Price = vm.Price;
        product.WeightPerLength = vm.SaleType == PvcSaleType.WeightPerLength ? vm.WeightPerLength : null;
        product.Description = vm.Description?.Trim();
        await _db.SaveChangesAsync();

        TempData["Success"] = $"PVC product \"{product.Name}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Quick entry (bulk add) ----------

    [HttpGet]
    public async Task<IActionResult> QuickEntry()
    {
        await LoadLookupsAsync();
        return View(new PvcProductQuickEntryViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickEntry(PvcProductQuickEntryViewModel vm)
    {
        var pvcCategoryId = await GetPvcCategoryIdAsync();

        // A row counts as "used" if anything meaningful was typed in it.
        vm.Rows = vm.Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Name) || r.CompanyId > 0 || r.ColorId > 0
                || r.GaugeId > 0 || (r.Price ?? 0) > 0 || (r.WeightPerLength ?? 0) > 0)
            .ToList();
        if (vm.Rows.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one product row.");

        var seen = new HashSet<string>();
        for (var i = 0; i < vm.Rows.Count; i++)
        {
            var row = vm.Rows[i];
            var line = $"Row {i + 1}";
            if (string.IsNullOrWhiteSpace(row.Name))
                ModelState.AddModelError(string.Empty, $"{line}: section name is required.");
            if (row.CompanyId <= 0)
                ModelState.AddModelError(string.Empty, $"{line}: select a company.");
            if (row.ColorId <= 0)
                ModelState.AddModelError(string.Empty, $"{line}: select a color.");
            if (row.GaugeId <= 0)
                ModelState.AddModelError(string.Empty, $"{line}: select a gauge.");
            if ((row.Price ?? 0) <= 0)
                ModelState.AddModelError(string.Empty, $"{line}: enter a valid rate.");
            if (row.SaleType == PvcSaleType.WeightPerLength && (row.WeightPerLength ?? 0) <= 0)
                ModelState.AddModelError(string.Empty, $"{line}: weight per length is required for weight-based products.");

            if (string.IsNullOrWhiteSpace(row.Name))
                continue;
            var name = row.Name.Trim();

            // Duplicate within this batch?
            var key = $"{name.ToLowerInvariant()}|{row.CompanyId}|{row.ColorId}|{row.GaugeId}";
            if (!seen.Add(key))
                ModelState.AddModelError(string.Empty, $"{line}: duplicates another row in this batch (\"{name}\").");

            // Duplicate against existing PVC products?
            var exists = await _db.Products.AnyAsync(p =>
                p.CategoryId == pvcCategoryId &&
                p.CompanyId == row.CompanyId &&
                p.ColorId == row.ColorId &&
                p.GaugeId == row.GaugeId &&
                EF.Functions.ILike(p.Name, name));
            if (exists)
                ModelState.AddModelError(string.Empty,
                    $"{line}: \"{name}\" already exists with the same company, color, and gauge.");
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
                CategoryId = pvcCategoryId,
                CompanyId = row.CompanyId,
                ColorId = row.ColorId,
                GaugeId = row.GaugeId,
                PricingMode = PricingMode.PerFoot,
                SaleType = row.SaleType,
                GasKitType = row.GasKitType,
                Price = row.Price!.Value,
                WeightPerLength = row.SaleType == PvcSaleType.WeightPerLength ? row.WeightPerLength : null
            });
        }
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{vm.Rows.Count} PVC product(s) added.";
        return RedirectToAction(nameof(QuickEntry)); // fresh grid — keep entering
    }

    // ---------- Bulk rate update ----------

    [HttpGet]
    public async Task<IActionResult> Rates()
    {
        var pvcCategoryId = await GetPvcCategoryIdAsync();
        var items = await _db.Products.AsNoTracking()
            .Where(p => p.CategoryId == pvcCategoryId)
            .OrderBy(p => p.Name)
            .Select(p => new PvcProductListItemViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Company = p.Company != null ? p.Company.Name : "—",
                Color = p.Color.Name,
                Gauge = p.Gauge.Name,
                GasKitType = p.GasKitType ?? GasKitType.None,
                SaleType = p.SaleType ?? PvcSaleType.PerRunningFoot,
                Price = p.Price,
                WeightPerLength = p.WeightPerLength,
                CurrentStock = p.CurrentStock
            })
            .ToListAsync();
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRates(List<ProductRateUpdateViewModel> changes)
    {
        var pvcCategoryId = await GetPvcCategoryIdAsync();
        changes = (changes ?? new()).Where(c => c.Id > 0 && c.Price > 0).ToList();
        if (changes.Count == 0)
        {
            TempData["Error"] = "No rate changes to save.";
            return RedirectToAction(nameof(Rates));
        }

        var ids = changes.Select(c => c.Id).ToList();
        var products = await _db.Products
            .Where(p => ids.Contains(p.Id) && p.CategoryId == pvcCategoryId)
            .ToDictionaryAsync(p => p.Id);

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await FindPvcProductAsync(id);
        if (product is null)
            return NotFound();

        if (product.CurrentStock != 0)
        {
            TempData["Error"] = $"\"{product.Name}\" still has stock ({product.CurrentStock:N2} ft) and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _db.Products.Remove(product); // soft delete via AppDbContext
        await _db.SaveChangesAsync();
        TempData["Success"] = $"PVC product \"{product.Name}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Helpers ----------

    /// <summary>WeightPerLength sales need the default kg-per-piece value.</summary>
    private void ValidateWeight(PvcProductFormViewModel vm)
    {
        if (vm.SaleType == PvcSaleType.WeightPerLength && (vm.WeightPerLength ?? 0) <= 0)
            ModelState.AddModelError(nameof(vm.WeightPerLength),
                "Weight per length is required for weight-based products.");
    }

    private async Task<Product?> FindPvcProductAsync(int id)
    {
        var pvcCategoryId = await GetPvcCategoryIdAsync();
        var product = await _db.Products.FindAsync(id);
        return product is null || product.IsDeleted || product.CategoryId != pvcCategoryId
            ? null
            : product;
    }

    private async Task<bool> IsDuplicateAsync(PvcProductFormViewModel vm)
    {
        var pvcCategoryId = await GetPvcCategoryIdAsync();
        var name = vm.Name.Trim();
        return await _db.Products.AnyAsync(p =>
            p.Id != vm.Id &&
            p.CategoryId == pvcCategoryId &&
            p.CompanyId == vm.CompanyId &&
            p.ColorId == vm.ColorId &&
            p.GaugeId == vm.GaugeId &&
            EF.Functions.ILike(p.Name, name));
    }

    /// <summary>The seeded PVC category; recreated here if missing so the module
    /// never breaks on a fresh/edited database.</summary>
    private async Task<int> GetPvcCategoryIdAsync()
    {
        var category = await _db.Categories.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Name == PvcCategoryName);
        if (category is null)
        {
            category = new Category { Name = PvcCategoryName };
            _db.Categories.Add(category);
            await _db.SaveChangesAsync();
        }
        else if (category.IsDeleted)
        {
            category.IsDeleted = false;
            await _db.SaveChangesAsync();
        }
        return category.Id;
    }

    private async Task LoadLookupsAsync(PvcProductFormViewModel? vm = null)
    {
        ViewBag.Colors = new SelectList(
            await _db.Colors.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", vm?.ColorId);
        ViewBag.Gauges = new SelectList(
            await _db.Gauges.AsNoTracking().OrderBy(g => g.Name).ToListAsync(),
            "Id", "Name", vm?.GaugeId);
        ViewBag.Companies = new SelectList(
            await _db.Companies.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", vm?.CompanyId);
    }
}
