using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Entities;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;
using Zedex.Web.Services;

namespace Zedex.Web.Controllers;

/// <summary>
/// Shop expenses (staff food, water, internal purchases, ...). Employee salary/advances
/// are recorded under Employees instead, so they are never counted twice.
/// Edit rule: Admins any time; workers only entries created today.
/// </summary>
[Authorize(Policy = "Module:Expenses")]
public class ExpensesController : Controller
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string UploadFolder = "expenses";
    private const int PageSize = 50;

    private readonly AppDbContext _db;
    private readonly IPrivateFileStore _files;
    private readonly IReportExportService _export;

    public ExpensesController(AppDbContext db, IPrivateFileStore files, IReportExportService export)
    {
        _db = db;
        _files = files;
        _export = export;
    }

    // =========================================================
    // List + quick add
    // =========================================================

    public async Task<IActionResult> Index(DateTime? from, DateTime? to, int? categoryId,
        PaymentSource? source, string? search, int page = 1)
    {
        var (f, t) = Range(from, to);
        var query = Filter(f, t, categoryId, source, search);

        var totals = await query
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Total = g.Sum(e => e.Amount),
                Cash = g.Where(e => e.PaymentSource == PaymentSource.Cash).Sum(e => e.Amount),
                Online = g.Where(e => e.PaymentSource == PaymentSource.Online).Sum(e => e.Amount)
            })
            .FirstOrDefaultAsync();

        var byCategory = await query
            .GroupBy(e => e.ExpenseCategory.Name)
            .Select(g => new ExpenseCategoryTotalViewModel { Category = g.Key, Count = g.Count(), Amount = g.Sum(e => e.Amount) })
            .OrderByDescending(c => c.Amount)
            .ToListAsync();

        page = Math.Max(1, page);
        var items = await Project(query)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        await LoadCategoriesAsync(categoryId);
        return View(new ExpenseListViewModel
        {
            From = f, To = t, CategoryId = categoryId, Source = source, Search = search,
            Items = new PagedResult<ExpenseRowViewModel>
            {
                Items = items, Page = page, PageSize = PageSize, TotalCount = totals?.Count ?? 0
            },
            TotalCount = totals?.Count ?? 0,
            TotalAmount = totals?.Total ?? 0,
            CashTotal = totals?.Cash ?? 0,
            OnlineTotal = totals?.Online ?? 0,
            ByCategory = byCategory
        });
    }

    // =========================================================
    // Create / Edit / Delete
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadCategoriesAsync(null);
        return View(new ExpenseFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ExpenseFormViewModel vm, bool quick = false)
    {
        await ValidateCategoryAsync(vm);
        var (path, error) = await SaveAttachmentAsync(vm);

        if (!ModelState.IsValid)
        {
            if (quick)
            {
                TempData["Error"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return RedirectToAction(nameof(Index));
            }
            await LoadCategoriesAsync(vm.ExpenseCategoryId);
            return View(vm);
        }

        var expense = new Expense
        {
            ExpenseDate = vm.ExpenseDate.Date,
            ExpenseCategoryId = vm.ExpenseCategoryId!.Value,
            Amount = vm.Amount!.Value,
            PaymentSource = vm.PaymentSource,
            Description = vm.Description.Trim(),
            AttachmentPath = path
        };
        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Expense of Rs. {expense.Amount:N2} recorded ({expense.PaymentSource}).";
        return RedirectToAction(nameof(Index), new { from = vm.ExpenseDate.ToString("yyyy-MM-dd"), to = vm.ExpenseDate.ToString("yyyy-MM-dd") });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var expense = await _db.Expenses.FindAsync(id);
        if (expense is null || expense.IsDeleted)
            return NotFound();
        if (!CanModify(expense))
            return LockedRedirect();

        await LoadCategoriesAsync(expense.ExpenseCategoryId);
        return View(new ExpenseFormViewModel
        {
            Id = expense.Id,
            ExpenseDate = expense.ExpenseDate,
            ExpenseCategoryId = expense.ExpenseCategoryId,
            Amount = expense.Amount,
            PaymentSource = expense.PaymentSource,
            Description = expense.Description,
            HasExistingAttachment = expense.AttachmentPath is not null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ExpenseFormViewModel vm)
    {
        var expense = await _db.Expenses.FindAsync(vm.Id);
        if (expense is null || expense.IsDeleted)
            return NotFound();
        if (!CanModify(expense))
            return LockedRedirect();

        await ValidateCategoryAsync(vm);
        var (path, _) = await SaveAttachmentAsync(vm);
        if (!ModelState.IsValid)
        {
            vm.HasExistingAttachment = expense.AttachmentPath is not null;
            await LoadCategoriesAsync(vm.ExpenseCategoryId);
            return View(vm);
        }

        expense.ExpenseDate = vm.ExpenseDate.Date;
        expense.ExpenseCategoryId = vm.ExpenseCategoryId!.Value;
        expense.Amount = vm.Amount!.Value;
        expense.PaymentSource = vm.PaymentSource;
        expense.Description = vm.Description.Trim();
        if (path is not null)
            expense.AttachmentPath = path;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Expense updated.";
        return RedirectToAction(nameof(Index), new { from = expense.ExpenseDate.ToString("yyyy-MM-dd"), to = expense.ExpenseDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var expense = await _db.Expenses.FindAsync(id);
        if (expense is null || expense.IsDeleted)
            return NotFound();
        if (!CanModify(expense))
            return LockedRedirect();

        _db.Expenses.Remove(expense); // soft delete (AppDbContext)
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Expense of Rs. {expense.Amount:N2} deleted.";
        return RedirectToAction(nameof(Index), new { from = expense.ExpenseDate.ToString("yyyy-MM-dd"), to = expense.ExpenseDate.ToString("yyyy-MM-dd") });
    }

    /// <summary>Streams a receipt from private storage (never exposed under wwwroot).</summary>
    public async Task<IActionResult> Attachment(int id)
    {
        var path = await _db.Expenses.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => e.AttachmentPath)
            .FirstOrDefaultAsync();
        var file = _files.Resolve(path);
        if (file is null)
            return NotFound();
        return PhysicalFile(file.Value.PhysicalPath, file.Value.ContentType);
    }

    // =========================================================
    // Export
    // =========================================================

    public async Task<IActionResult> Excel(DateTime? from, DateTime? to, int? categoryId, PaymentSource? source, string? search)
    {
        var (f, t) = Range(from, to);
        var rows = await Project(Filter(f, t, categoryId, source, search)).ToListAsync();
        var bytes = _export.ToExcel("Expense Report", Subtitle(f, t),
            new[] { "Date", "Category", "Description", "Paid From", "Amount", "Entered By" },
            rows.Select(r => new object?[] { r.ExpenseDate, r.Category, r.Description, r.PaymentSource.ToString(), r.Amount, r.CreatedBy }));
        return File(bytes, ExcelContentType, $"expenses-{f:yyyyMMdd}-{t:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> Pdf(DateTime? from, DateTime? to, int? categoryId, PaymentSource? source, string? search)
    {
        var (f, t) = Range(from, to);
        var rows = await Project(Filter(f, t, categoryId, source, search)).ToListAsync();
        var bytes = _export.ToPdf("Expense Report", Subtitle(f, t),
            new[] { "Date", "Category", "Description", "From", "Amount", "By" },
            rows.Select(r => new object?[] { r.ExpenseDate, r.Category, r.Description, r.PaymentSource.ToString(), r.Amount, r.CreatedBy }));
        return File(bytes, "application/pdf", $"expenses-{f:yyyyMMdd}-{t:yyyyMMdd}.pdf");
    }

    // =========================================================
    // Helpers
    // =========================================================

    private IQueryable<Expense> Filter(DateTime from, DateTime to, int? categoryId, PaymentSource? source, string? search)
    {
        var query = _db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate < to.AddDays(1));
        if (categoryId is > 0)
            query = query.Where(e => e.ExpenseCategoryId == categoryId);
        if (source is not null)
            query = query.Where(e => e.PaymentSource == source);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(e => EF.Functions.ILike(e.Description, pattern) ||
                                     EF.Functions.ILike(e.ExpenseCategory.Name, pattern));
        }
        return query;
    }

    private static IQueryable<ExpenseRowViewModel> Project(IQueryable<Expense> query) =>
        query
            .OrderByDescending(e => e.ExpenseDate).ThenByDescending(e => e.Id)
            .Select(e => new ExpenseRowViewModel
            {
                Id = e.Id,
                ExpenseDate = e.ExpenseDate,
                Category = e.ExpenseCategory.Name,
                Amount = e.Amount,
                PaymentSource = e.PaymentSource,
                Description = e.Description,
                HasAttachment = e.AttachmentPath != null,
                CreatedBy = e.CreatedBy,
                CreatedDate = e.CreatedDate
            });

    private bool CanModify(Expense expense) =>
        User.IsInRole(DbSeeder.AdminRole) || expense.CreatedDate.Date == DateTime.Today;

    private IActionResult LockedRedirect()
    {
        TempData["Error"] = "Only an admin can change or delete an expense after the day it was entered.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateCategoryAsync(ExpenseFormViewModel vm)
    {
        if (vm.ExpenseCategoryId is > 0 &&
            !await _db.ExpenseCategories.AnyAsync(c => c.Id == vm.ExpenseCategoryId))
            ModelState.AddModelError(nameof(vm.ExpenseCategoryId), "Select a valid category.");
    }

    private async Task<(string? Path, string? Error)> SaveAttachmentAsync(ExpenseFormViewModel vm)
    {
        // Don't write a file for a form that is already invalid.
        if (!ModelState.IsValid)
            return (null, null);
        var result = await _files.SaveAsync(vm.Attachment, UploadFolder, allowPdf: true);
        if (result.Error is not null)
            ModelState.AddModelError(nameof(vm.Attachment), result.Error);
        return result;
    }

    private async Task LoadCategoriesAsync(int? selected)
    {
        ViewBag.Categories = new SelectList(
            await _db.ExpenseCategories.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", selected);
    }

    private static (DateTime from, DateTime to) Range(DateTime? from, DateTime? to)
    {
        var f = (from ?? DateTime.Today).Date;
        var t = (to ?? f).Date;
        return t < f ? (t, f) : (f, t);
    }

    private static string Subtitle(DateTime from, DateTime to) =>
        from == to ? $"{from:dd MMM yyyy}" : $"{from:dd MMM yyyy} — {to:dd MMM yyyy}";
}
