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
/// PVC section products. Rows live in the shared Products table, under whichever
/// category (or categories) are flagged Category.IsPvc — see the Categories admin
/// screen — with PricingMode = PerFoot so the stock module tracks them as pieces ×
/// lengths, but are managed through these separate screens with PVC-specific fields.
/// More than one category can be flagged IsPvc at once; the product forms let the
/// user pick which one a given product belongs to.
/// </summary>
[Authorize(Policy = "Module:Products")]
public class PvcProductsController : Controller
{
    private readonly AppDbContext _db;
    private const int PageSize = 10;

    public PvcProductsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? search, int? categoryId, int? companyId, int? colorId, int? gaugeId, PvcSaleType? saleType, int page = 1)
    {
        var pvcCategoryIds = await GetPvcCategoryIdsAsync();
        var query = _db.Products.AsNoTracking().Where(p => pvcCategoryIds.Contains(p.CategoryId));

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
        if (companyId is > 0)
            query = query.Where(p => p.CompanyId == companyId);
        if (colorId is > 0)
            query = query.Where(p => p.ColorId == colorId);
        if (gaugeId is > 0)
            query = query.Where(p => p.GaugeId == gaugeId);
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
                Category = p.Category.Name,
                Company = p.Company != null ? p.Company.Name : "—",
                Color = p.Color.Name,
                Gauge = p.Gauge.Name,
                GasKitType = p.GasKitType ?? GasKitType.None,
                SaleType = p.SaleType ?? PvcSaleType.PerRunningFoot,
                Price = p.Price,
                WeightPerLength = p.WeightPerLength,
                CurrentStock = p.CurrentStock,
                StockQty = p.StockPieces.Where(s => !s.IsDeleted).Sum(s => (int?)s.Quantity) ?? 0
            })
            .ToListAsync();

        ViewBag.Categories = new SelectList(
            await _db.Categories.AsNoTracking()
                .Where(c => pvcCategoryIds.Contains(c.Id))
                .OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", categoryId);
        ViewBag.Companies = new SelectList(
            await _db.Companies.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", companyId);
        ViewBag.Colors = new SelectList(
            await _db.Colors.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", colorId);
        ViewBag.Gauges = new SelectList(
            await _db.Gauges.AsNoTracking().OrderBy(g => g.Name).ToListAsync(),
            "Id", "Name", gaugeId);

        return View(new PvcProductListViewModel
        {
            Search = search,
            CategoryId = categoryId,
            CompanyId = companyId,
            ColorId = colorId,
            GaugeId = gaugeId,
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
        await ValidateCategoryAsync(vm);
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(vm);
            return View(vm);
        }

        if (await IsDuplicateAsync(vm))
        {
            ModelState.AddModelError(nameof(vm.Name),
                "A PVC product with the same name, category, company, color, and gauge already exists.");
            await LoadLookupsAsync(vm);
            return View(vm);
        }

        var product = new Product
        {
            Name = vm.Name.Trim(),
            CategoryId = vm.CategoryId,
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
            CategoryId = product.CategoryId,
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
        await ValidateCategoryAsync(vm);
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
                "A PVC product with the same name, category, company, color, and gauge already exists.");
            await LoadLookupsAsync(vm);
            return View(vm);
        }

        product.Name = vm.Name.Trim();
        product.CategoryId = vm.CategoryId;
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
        var pvcCategoryIds = await GetPvcCategoryIdsAsync();

        // A row counts as "used" if anything meaningful was typed in it.
        vm.Rows = vm.Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Name) || r.CategoryId > 0 || r.CompanyId > 0 || r.ColorId > 0
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
            if (row.CategoryId <= 0 || !pvcCategoryIds.Contains(row.CategoryId))
                ModelState.AddModelError(string.Empty, $"{line}: select a category.");
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
            var key = $"{name.ToLowerInvariant()}|{row.CategoryId}|{row.CompanyId}|{row.ColorId}|{row.GaugeId}";
            if (!seen.Add(key))
                ModelState.AddModelError(string.Empty, $"{line}: duplicates another row in this batch (\"{name}\").");

            // Duplicate against existing PVC products?
            var exists = await _db.Products.AnyAsync(p =>
                p.CategoryId == row.CategoryId &&
                p.CompanyId == row.CompanyId &&
                p.ColorId == row.ColorId &&
                p.GaugeId == row.GaugeId &&
                EF.Functions.ILike(p.Name, name));
            if (exists)
                ModelState.AddModelError(string.Empty,
                    $"{line}: \"{name}\" already exists with the same category, company, color, and gauge.");
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
        var pvcCategoryIds = await GetPvcCategoryIdsAsync();
        var items = await _db.Products.AsNoTracking()
            .Where(p => pvcCategoryIds.Contains(p.CategoryId))
            .OrderBy(p => p.Name)
            .Select(p => new PvcProductListItemViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Category = p.Category.Name,
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
        var pvcCategoryIds = await GetPvcCategoryIdsAsync();
        changes = (changes ?? new()).Where(c => c.Id > 0 && c.Price > 0).ToList();
        if (changes.Count == 0)
        {
            TempData["Error"] = "No rate changes to save.";
            return RedirectToAction(nameof(Rates));
        }

        var ids = changes.Select(c => c.Id).ToList();
        var products = await _db.Products
            .Where(p => ids.Contains(p.Id) && pvcCategoryIds.Contains(p.CategoryId))
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

        var stockQty = await _db.StockPieces.IgnoreQueryFilters()
            .Where(s => s.ProductId == id && !s.IsDeleted)
            .SumAsync(s => (int?)s.Quantity) ?? 0;
        if (stockQty != 0)
        {
            TempData["Error"] = $"\"{product.Name}\" still has stock ({stockQty:N0} qty) and cannot be deleted.";
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

    /// <summary>The chosen category must actually be one of the categories flagged IsPvc.</summary>
    private async Task ValidateCategoryAsync(PvcProductFormViewModel vm)
    {
        var pvcCategoryIds = await GetPvcCategoryIdsAsync();
        if (vm.CategoryId <= 0 || !pvcCategoryIds.Contains(vm.CategoryId))
            ModelState.AddModelError(nameof(vm.CategoryId), "Please select a valid PVC category.");
    }

    private async Task<Product?> FindPvcProductAsync(int id)
    {
        var pvcCategoryIds = await GetPvcCategoryIdsAsync();
        var product = await _db.Products.FindAsync(id);
        return product is null || product.IsDeleted || !pvcCategoryIds.Contains(product.CategoryId)
            ? null
            : product;
    }

    private async Task<bool> IsDuplicateAsync(PvcProductFormViewModel vm)
    {
        var name = vm.Name.Trim();
        return await _db.Products.AnyAsync(p =>
            p.Id != vm.Id &&
            p.CategoryId == vm.CategoryId &&
            p.CompanyId == vm.CompanyId &&
            p.ColorId == vm.ColorId &&
            p.GaugeId == vm.GaugeId &&
            EF.Functions.ILike(p.Name, name));
    }

    /// <summary>All categories flagged IsPvc (set from the Categories admin screen — more
    /// than one is allowed); a default is created here if none exists yet so the module
    /// never breaks on a fresh/edited database.</summary>
    private async Task<List<int>> GetPvcCategoryIdsAsync()
    {
        var categories = await _db.Categories.IgnoreQueryFilters()
            .Where(c => c.IsPvc)
            .ToListAsync();

        if (categories.Count == 0)
        {
            var category = new Category { Name = "PVC", IsPvc = true };
            _db.Categories.Add(category);
            await _db.SaveChangesAsync();
            return new List<int> { category.Id };
        }

        var restored = false;
        foreach (var category in categories.Where(c => c.IsDeleted))
        {
            category.IsDeleted = false;
            restored = true;
        }
        if (restored)
            await _db.SaveChangesAsync();

        return categories.Select(c => c.Id).ToList();
    }

    private async Task LoadLookupsAsync(PvcProductFormViewModel? vm = null)
    {
        var pvcCategoryIds = await GetPvcCategoryIdsAsync();
        ViewBag.Categories = new SelectList(
            await _db.Categories.AsNoTracking()
                .Where(c => pvcCategoryIds.Contains(c.Id))
                .OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", vm?.CategoryId);
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
