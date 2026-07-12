using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Common;
using Zedex.Domain.Entities;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;

namespace Zedex.Web.Controllers;

/// <summary>
/// Shared admin-only CRUD for simple Name-only lookups (Category, Color, Gauge).
/// Views live in Views/MasterData and are reused by all three controllers.
/// </summary>
[Authorize(Roles = DbSeeder.AdminRole)]
public abstract class MasterDataController<TEntity> : Controller
    where TEntity : BaseEntity, INamedEntity, new()
{
    protected readonly AppDbContext Db;
    protected MasterDataController(AppDbContext db) => Db = db;

    protected abstract string Title { get; }
    protected abstract string TitlePlural { get; }
    protected abstract string Icon { get; }
    /// <summary>True if any product references this lookup (blocks delete).</summary>
    protected abstract Task<bool> IsInUseAsync(int id);

    private const int PageSize = 10;

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var query = Db.Set<TEntity>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(e => EF.Functions.ILike(EF.Property<string>(e, "Name"), $"%{search.Trim()}%"));

        var total = await query.CountAsync();
        page = Math.Max(1, page);
        var items = await query
            .OrderBy(e => EF.Property<string>(e, "Name"))
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(e => new MasterItemViewModel
            {
                Id = e.Id,
                Name = EF.Property<string>(e, "Name"),
                CreatedBy = e.CreatedBy,
                CreatedDate = e.CreatedDate
            })
            .ToListAsync();

        var vm = new MasterListViewModel
        {
            EntityTitle = Title,
            EntityTitlePlural = TitlePlural,
            Icon = Icon,
            Search = search,
            Items = new PagedResult<MasterItemViewModel>
            {
                Items = items, Page = page, PageSize = PageSize, TotalCount = total
            }
        };
        return View("~/Views/MasterData/Index.cshtml", vm);
    }

    [HttpGet]
    public IActionResult Create()
    {
        SetTitles();
        return View("~/Views/MasterData/Create.cshtml", new MasterItemViewModel { Name = string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MasterItemViewModel vm)
    {
        SetTitles();
        if (!ModelState.IsValid)
            return View("~/Views/MasterData/Create.cshtml", vm);

        var name = vm.Name.Trim();

        // Includes soft-deleted rows: recreating a deleted name restores it.
        var existing = await Db.Set<TEntity>()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => EF.Functions.ILike(EF.Property<string>(e, "Name"), name));

        if (existing is not null && !existing.IsDeleted)
        {
            ModelState.AddModelError(nameof(vm.Name), $"{Title} \"{name}\" already exists.");
            return View("~/Views/MasterData/Create.cshtml", vm);
        }

        if (existing is not null)
        {
            existing.IsDeleted = false;
            await Db.SaveChangesAsync();
            TempData["Success"] = $"{Title} \"{name}\" restored.";
        }
        else
        {
            Db.Set<TEntity>().Add(new TEntity { Name = name });
            await Db.SaveChangesAsync();
            TempData["Success"] = $"{Title} \"{name}\" created.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await Db.Set<TEntity>().FindAsync(id);
        if (entity is null || entity.IsDeleted)
            return NotFound();

        SetTitles();
        return View("~/Views/MasterData/Edit.cshtml",
            new MasterItemViewModel { Id = entity.Id, Name = entity.Name });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(MasterItemViewModel vm)
    {
        SetTitles();
        if (!ModelState.IsValid)
            return View("~/Views/MasterData/Edit.cshtml", vm);

        var entity = await Db.Set<TEntity>().FindAsync(vm.Id);
        if (entity is null || entity.IsDeleted)
            return NotFound();

        var name = vm.Name.Trim();
        var duplicate = await Db.Set<TEntity>()
            .AnyAsync(e => e.Id != vm.Id && EF.Functions.ILike(EF.Property<string>(e, "Name"), name));
        if (duplicate)
        {
            ModelState.AddModelError(nameof(vm.Name), $"{Title} \"{name}\" already exists.");
            return View("~/Views/MasterData/Edit.cshtml", vm);
        }

        entity.Name = name;
        await Db.SaveChangesAsync();
        TempData["Success"] = $"{Title} updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await Db.Set<TEntity>().FindAsync(id);
        if (entity is null || entity.IsDeleted)
            return NotFound();

        if (await IsInUseAsync(id))
        {
            TempData["Error"] = $"{Title} \"{entity.Name}\" is used by one or more products and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        Db.Set<TEntity>().Remove(entity); // AppDbContext converts to soft delete
        await Db.SaveChangesAsync();
        TempData["Success"] = $"{Title} \"{entity.Name}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    private void SetTitles()
    {
        ViewData["EntityTitle"] = Title;
        ViewData["EntityTitlePlural"] = TitlePlural;
        ViewData["EntityIcon"] = Icon;
    }
}

public class CategoriesController : MasterDataController<Category>
{
    public CategoriesController(AppDbContext db) : base(db) { }
    protected override string Title => "Category";
    protected override string TitlePlural => "Categories";
    protected override string Icon => "bi-tags";
    protected override Task<bool> IsInUseAsync(int id) =>
        Db.Products.AnyAsync(p => p.CategoryId == id);
}

public class ColorsController : MasterDataController<Color>
{
    public ColorsController(AppDbContext db) : base(db) { }
    protected override string Title => "Color";
    protected override string TitlePlural => "Colors";
    protected override string Icon => "bi-palette";
    protected override Task<bool> IsInUseAsync(int id) =>
        Db.Products.AnyAsync(p => p.ColorId == id);
}

public class GaugesController : MasterDataController<Gauge>
{
    public GaugesController(AppDbContext db) : base(db) { }
    protected override string Title => "Gauge";
    protected override string TitlePlural => "Gauges";
    protected override string Icon => "bi-rulers";
    protected override Task<bool> IsInUseAsync(int id) =>
        Db.Products.AnyAsync(p => p.GaugeId == id);
}

/// <summary>PVC section manufacturer/brand.</summary>
public class CompaniesController : MasterDataController<Company>
{
    public CompaniesController(AppDbContext db) : base(db) { }
    protected override string Title => "Company";
    protected override string TitlePlural => "Companies";
    protected override string Icon => "bi-building";
    protected override Task<bool> IsInUseAsync(int id) =>
        Db.Products.AnyAsync(p => p.CompanyId == id);
}
