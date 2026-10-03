using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Entities;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;
using Zedex.Web.Services;

namespace Zedex.Web.Controllers;

/// <summary>
/// Price quotations for prospective clients, shared as PDF or image. Independent of
/// billing: the client is free text, and nothing here affects stock or the ledger.
/// </summary>
[Authorize(Policy = "Module:Quotations")]
public class QuotationsController : Controller
{
    private const int PageSize = 50;
    private const string DefaultTitle = "Zedex Business";

    private readonly AppDbContext _db;

    public QuotationsController(AppDbContext db) => _db = db;

    // =========================================================
    // List
    // =========================================================

    public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? search, int page = 1)
    {
        var query = _db.Quotations.AsNoTracking();
        if (from is not null)
            query = query.Where(q => q.QuotationDate >= from.Value.Date);
        if (to is not null)
            query = query.Where(q => q.QuotationDate < to.Value.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = $"%{search.Trim()}%";
            query = query.Where(q => EF.Functions.ILike(q.ClientName, s)
                                     || EF.Functions.ILike(q.QuotationNumber, s)
                                     || (q.ContactNumber != null && EF.Functions.ILike(q.ContactNumber, s))
                                     || (q.ProjectAddress != null && EF.Functions.ILike(q.ProjectAddress, s)));
        }

        var count = await query.CountAsync();
        var total = count == 0 ? 0 : await query.SumAsync(q => q.GrandTotal);

        page = Math.Max(1, page);
        var items = await query
            .OrderByDescending(q => q.QuotationDate).ThenByDescending(q => q.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(q => new QuotationRowViewModel
            {
                Id = q.Id,
                QuotationNumber = q.QuotationNumber,
                QuotationDate = q.QuotationDate,
                ClientName = q.ClientName,
                ContactNumber = q.ContactNumber,
                ProjectAddress = q.ProjectAddress,
                ItemCount = q.Items.Count(),
                GrandTotal = q.GrandTotal,
                CreatedBy = q.CreatedBy
            })
            .ToListAsync();

        return View(new QuotationListViewModel
        {
            From = from, To = to, Search = search, TotalAmount = total,
            Items = new PagedResult<QuotationRowViewModel>
            {
                Items = items, Page = page, PageSize = PageSize, TotalCount = count
            }
        });
    }

    // =========================================================
    // Create / Edit / Delete
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Create(int? copyFrom)
    {
        var vm = new QuotationFormViewModel();
        var source = copyFrom is null ? null : await LoadAsync(copyFrom.Value);
        if (source is not null)
        {
            // "Duplicate": same client, scope and terms; new number and today's date.
            vm.ClientName = source.ClientName;
            vm.ContactNumber = source.ContactNumber;
            vm.ProjectAddress = source.ProjectAddress;
            vm.Items = source.Items.OrderBy(i => i.SortOrder).Select(i => new QuotationItemInput
            {
                Description = i.Description, Specification = i.Specification, Quantity = i.Quantity, Rate = i.Rate
            }).ToList();
            vm.Terms = source.Terms.OrderBy(t => t.SortOrder).Select(t => (string?)t.Text).ToList();
        }

        await FillDisplayAsync(vm);
        if (source is null)
            vm.Terms = vm.DefaultTerms.Select(t => (string?)t).ToList();
        vm.QuotationNumber = await NextNumberAsync();
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(QuotationFormViewModel vm)
    {
        var items = CleanItems(vm);
        if (!ModelState.IsValid)
        {
            await FillDisplayAsync(vm);
            vm.QuotationNumber = await NextNumberAsync();
            return View(vm);
        }

        var quotation = new Quotation();
        Apply(quotation, vm, items);
        _db.Quotations.Add(quotation);

        // The number is assigned at save time; retry if another user took it in the meantime.
        for (var attempt = 1; ; attempt++)
        {
            quotation.QuotationNumber = await NextNumberAsync();
            try
            {
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException ex) when (attempt < 3 && ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
            }
        }

        TempData["Success"] = $"Quotation {quotation.QuotationNumber} saved.";
        return RedirectToAction(nameof(Details), new { id = quotation.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var quotation = await LoadAsync(id);
        if (quotation is null)
            return NotFound();

        var vm = new QuotationFormViewModel
        {
            Id = quotation.Id,
            QuotationNumber = quotation.QuotationNumber,
            QuotationDate = quotation.QuotationDate,
            ClientName = quotation.ClientName,
            ContactNumber = quotation.ContactNumber,
            ProjectAddress = quotation.ProjectAddress,
            Items = quotation.Items.OrderBy(i => i.SortOrder).Select(i => new QuotationItemInput
            {
                Description = i.Description, Specification = i.Specification, Quantity = i.Quantity, Rate = i.Rate
            }).ToList(),
            Terms = quotation.Terms.OrderBy(t => t.SortOrder).Select(t => (string?)t.Text).ToList()
        };
        await FillDisplayAsync(vm);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(QuotationFormViewModel vm)
    {
        var quotation = await _db.Quotations
            .Include(q => q.Items)
            .Include(q => q.Terms)
            .FirstOrDefaultAsync(q => q.Id == vm.Id);
        if (quotation is null)
            return NotFound();

        var items = CleanItems(vm);
        if (!ModelState.IsValid)
        {
            vm.QuotationNumber = quotation.QuotationNumber;
            await FillDisplayAsync(vm);
            return View(vm);
        }

        // Replace lines and terms wholesale (old rows are soft-deleted by AppDbContext).
        _db.QuotationItems.RemoveRange(quotation.Items);
        _db.QuotationTerms.RemoveRange(quotation.Terms);
        quotation.Items.Clear();
        quotation.Terms.Clear();
        Apply(quotation, vm, items);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Quotation {quotation.QuotationNumber} updated.";
        return RedirectToAction(nameof(Details), new { id = quotation.Id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var quotation = await _db.Quotations.FindAsync(id);
        if (quotation is null || quotation.IsDeleted)
            return NotFound();

        _db.Quotations.Remove(quotation); // soft delete (AppDbContext)
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Quotation {quotation.QuotationNumber} deleted.";
        return RedirectToAction(nameof(Index));
    }

    // =========================================================
    // View / PDF / Image
    // =========================================================

    public async Task<IActionResult> Details(int id)
    {
        var doc = await BuildDocumentAsync(id);
        return doc is null ? NotFound() : View(doc);
    }

    /// <summary>A4 PDF. <paramref name="download"/>=false opens it in the browser (for printing).</summary>
    public async Task<IActionResult> Pdf(int id, bool download = true)
    {
        var doc = await BuildDocumentAsync(id);
        if (doc is null)
            return NotFound();

        var bytes = QuotationDocument.ToPdf(doc);
        if (!download)
            return File(bytes, "application/pdf");
        return File(bytes, "application/pdf", $"{doc.FileBaseName}.pdf");
    }

    /// <summary>The whole quotation as one PNG (for WhatsApp / messaging).</summary>
    public async Task<IActionResult> Image(int id, bool download = true)
    {
        var doc = await BuildDocumentAsync(id);
        if (doc is null)
            return NotFound();

        var bytes = QuotationDocument.ToPng(doc);
        if (!download)
            return File(bytes, "image/png");
        return File(bytes, "image/png", $"{doc.FileBaseName}.png");
    }

    // =========================================================
    // Helpers
    // =========================================================

    private Task<Quotation?> LoadAsync(int id) =>
        _db.Quotations.AsNoTracking()
            .Include(q => q.Items)
            .Include(q => q.Terms)
            .FirstOrDefaultAsync(q => q.Id == id);

    private async Task<QuotationDocumentViewModel?> BuildDocumentAsync(int id)
    {
        var q = await LoadAsync(id);
        if (q is null)
            return null;

        var (title, details) = await GetHeaderAsync();
        var no = 0;
        return new QuotationDocumentViewModel
        {
            Id = q.Id,
            CompanyTitle = title,
            HeaderDetails = details,
            QuotationNumber = q.QuotationNumber,
            QuotationDate = q.QuotationDate,
            ClientName = q.ClientName,
            ContactNumber = q.ContactNumber,
            ProjectAddress = q.ProjectAddress,
            Lines = q.Items.OrderBy(i => i.SortOrder).Select(i => new QuotationLineViewModel
            {
                No = ++no,
                Description = i.Description,
                Specification = i.Specification,
                Quantity = i.Quantity,
                Rate = i.Rate,
                Amount = i.Amount
            }).ToList(),
            GrandTotal = q.GrandTotal,
            Terms = q.Terms.OrderBy(t => t.SortOrder).Select(t => t.Text).ToList(),
            CreatedBy = q.CreatedBy,
            CreatedDate = q.CreatedDate
        };
    }

    /// <summary>Drops fully blank rows and validates the rest. Returns the rows to save.</summary>
    private List<QuotationItemInput> CleanItems(QuotationFormViewModel vm)
    {
        // Index-based ModelState keys (Items[3].Quantity) no longer match once blank rows are
        // dropped, so validation messages are reported against the visible line number instead.
        foreach (var key in ModelState.Keys.Where(k => k.StartsWith("Items[")).ToList())
            ModelState.Remove(key);

        var items = vm.Items.Where(i => !i.IsBlank).ToList();
        if (items.Count == 0)
            ModelState.AddModelError("", "Add at least one line to the scope.");

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var line = $"Line {i + 1}";
            if (string.IsNullOrWhiteSpace(item.Description))
                ModelState.AddModelError("", $"{line}: enter a description.");
            if (item.Quantity is null or <= 0)
                ModelState.AddModelError("", $"{line}: enter a quantity greater than 0.");
            if (item.Rate is null or < 0)
                ModelState.AddModelError("", $"{line}: enter a rate.");
            if (item.Description?.Length > 2000 || item.Specification?.Length > 2000)
                ModelState.AddModelError("", $"{line}: text is too long (max 2000 characters).");
        }

        vm.Items = vm.Items.Where(i => !i.IsBlank).ToList();
        vm.Terms = vm.Terms.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t!.Trim()).ToList();
        if (vm.Terms.Any(t => t!.Length > 1000))
            ModelState.AddModelError("", "A term is too long (max 1000 characters).");
        return items;
    }

    private static void Apply(Quotation quotation, QuotationFormViewModel vm, List<QuotationItemInput> items)
    {
        quotation.QuotationDate = vm.QuotationDate.Date;
        quotation.ClientName = vm.ClientName.Trim();
        quotation.ContactNumber = string.IsNullOrWhiteSpace(vm.ContactNumber) ? null : vm.ContactNumber.Trim();
        quotation.ProjectAddress = string.IsNullOrWhiteSpace(vm.ProjectAddress) ? null : vm.ProjectAddress.Trim();

        var order = 0;
        foreach (var item in items)
            quotation.Items.Add(new QuotationItem
            {
                SortOrder = ++order,
                Description = item.Description!.Trim(),
                Specification = string.IsNullOrWhiteSpace(item.Specification) ? null : item.Specification.Trim(),
                Quantity = item.Quantity!.Value,
                Rate = item.Rate!.Value,
                Amount = item.Amount
            });
        quotation.GrandTotal = quotation.Items.Sum(i => i.Amount);

        order = 0;
        foreach (var term in vm.Terms)
            quotation.Terms.Add(new QuotationTerm { SortOrder = ++order, Text = term! });
    }

    private async Task FillDisplayAsync(QuotationFormViewModel vm)
    {
        (vm.CompanyTitle, vm.HeaderDetails) = await GetHeaderAsync();
        vm.DefaultTerms = await _db.QuotationDefaultTerms.AsNoTracking()
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
            .Select(t => t.Text)
            .ToListAsync();
    }

    private async Task<(string Title, string? Details)> GetHeaderAsync()
    {
        var settings = await _db.AppSettings.AsNoTracking()
            .Where(s => s.Key == AppSetting.Keys.QuotationTitle
                        || s.Key == AppSetting.Keys.QuotationHeaderDetails
                        || s.Key == AppSetting.Keys.PvcPrintTitle)
            .ToDictionaryAsync(s => s.Key, s => s.Value);

        string? Get(string key) =>
            settings.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

        var title = Get(AppSetting.Keys.QuotationTitle) ?? Get(AppSetting.Keys.PvcPrintTitle) ?? DefaultTitle;
        return (title, Get(AppSetting.Keys.QuotationHeaderDetails));
    }

    private Task<string> NextNumberAsync() =>
        DocumentNumbers.NextAsync(
            _db.Quotations.IgnoreQueryFilters().Select(q => q.QuotationNumber),
            $"QT-{DateTime.Today:yyyyMMdd}-");
}
