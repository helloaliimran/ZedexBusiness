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

    /// <summary>Override to show the entity-specific extra fields section (e.g. the
    /// "Is PVC" checkbox on Categories) in the Create/Edit forms and Index table.</summary>
    protected virtual bool ShowIsPvcOption => false;

    /// <summary>Copies entity-specific extra fields (e.g. IsPvc) from the form into the
    /// entity before it is saved. No-op unless overridden.</summary>
    protected virtual void ApplyFormFields(TEntity entity, MasterItemViewModel vm) { }

    /// <summary>Copies entity-specific extra fields (e.g. IsPvc) from the entity into the
    /// Edit form. No-op unless overridden.</summary>
    protected virtual void PopulateFormFields(MasterItemViewModel vm, TEntity entity) { }

    /// <summary>Extra cross-row validation beyond the name-uniqueness check (e.g. "only one
    /// category can be marked PVC"). Return an error message to block the save, or null.</summary>
    protected virtual Task<string?> ValidateFormAsync(MasterItemViewModel vm) => Task.FromResult<string?>(null);

    /// <summary>Populates entity-specific extra fields onto already-projected Index rows
    /// (avoids trying to translate entity-specific properties generically in the EF query).</summary>
    protected virtual Task ApplyIndexExtrasAsync(List<MasterItemViewModel> items) => Task.CompletedTask;

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
        await ApplyIndexExtrasAsync(items);

        var vm = new MasterListViewModel
        {
            EntityTitle = Title,
            EntityTitlePlural = TitlePlural,
            Icon = Icon,
            Search = search,
            ShowIsPvcOption = ShowIsPvcOption,
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

        var extraError = await ValidateFormAsync(vm);
        if (extraError is not null)
        {
            ModelState.AddModelError(string.Empty, extraError);
            return View("~/Views/MasterData/Create.cshtml", vm);
        }

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
            ApplyFormFields(existing, vm);
            await Db.SaveChangesAsync();
            TempData["Success"] = $"{Title} \"{name}\" restored.";
        }
        else
        {
            var entity = new TEntity { Name = name };
            ApplyFormFields(entity, vm);
            Db.Set<TEntity>().Add(entity);
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
        var vm = new MasterItemViewModel { Id = entity.Id, Name = entity.Name };
        PopulateFormFields(vm, entity);
        return View("~/Views/MasterData/Edit.cshtml", vm);
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

        var extraError = await ValidateFormAsync(vm);
        if (extraError is not null)
        {
            ModelState.AddModelError(string.Empty, extraError);
            return View("~/Views/MasterData/Edit.cshtml", vm);
        }

        var name = vm.Name.Trim();
        var duplicate = await Db.Set<TEntity>()
            .AnyAsync(e => e.Id != vm.Id && EF.Functions.ILike(EF.Property<string>(e, "Name"), name));
        if (duplicate)
        {
            ModelState.AddModelError(nameof(vm.Name), $"{Title} \"{name}\" already exists.");
            return View("~/Views/MasterData/Edit.cshtml", vm);
        }

        entity.Name = name;
        ApplyFormFields(entity, vm);
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
        ViewData["ShowIsPvcOption"] = ShowIsPvcOption;
    }
}

public class CategoriesController : MasterDataController<Category>
{
    public CategoriesController(AppDbContext db) : base(db) { }
    protected override string Title => "Category";
    protected override string TitlePlural => "Categories";
    protected override string Icon => "bi-tags";
    protected override bool ShowIsPvcOption => true;
    protected override Task<bool> IsInUseAsync(int id) =>
        Db.Products.AnyAsync(p => p.CategoryId == id);

    protected override void ApplyFormFields(Category entity, MasterItemViewModel vm) =>
        entity.IsPvc = vm.IsPvc;

    protected override void PopulateFormFields(MasterItemViewModel vm, Category entity) =>
        vm.IsPvc = entity.IsPvc;

    protected override async Task ApplyIndexExtrasAsync(List<MasterItemViewModel> items)
    {
        var ids = items.Select(i => i.Id).ToList();
        var flags = await Db.Categories.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.IsPvc);
        foreach (var item in items)
            if (flags.TryGetValue(item.Id, out var isPvc))
                item.IsPvc = isPvc;
    }
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
