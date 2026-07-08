using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Entities;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;

namespace Zedex.Web.Controllers;

[Authorize(Policy = "Module:CustomerLedger")]
public class LedgerController : Controller
{
    private const int PageSize = 10;
    private readonly AppDbContext _db;

    public LedgerController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var query = _db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, pattern) ||
                (c.Phone != null && EF.Functions.ILike(c.Phone, pattern)));
        }

        var total = await query.CountAsync();
        page = Math.Max(1, page);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(c => new LedgerCustomerItemViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Phone = c.Phone,
                TotalBills = c.LedgerEntries
                    .Where(l => !l.IsDeleted && l.Type == LedgerEntryType.Bill)
                    .Sum(l => (decimal?)l.Debit) ?? 0,
                TotalPaid = c.LedgerEntries
                    .Where(l => !l.IsDeleted && (l.Type == LedgerEntryType.Payment || l.Type == LedgerEntryType.Return || l.Type == LedgerEntryType.Credit))
                    .Sum(l => (decimal?)l.Credit) ?? 0,
                Balance = c.OpeningBalance + (c.LedgerEntries
                    .Where(l => !l.IsDeleted)
                    .Sum(l => (decimal?)(l.Debit - l.Credit)) ?? 0)
            })
            .ToListAsync();

        return View(new LedgerIndexViewModel
        {
            Search = search,
            Items = new PagedResult<LedgerCustomerItemViewModel>
            {
                Items = items, Page = page, PageSize = PageSize, TotalCount = total
            }
        });
    }

    public async Task<IActionResult> Ledger(int id, DateTime? from, DateTime? to)
    {
        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (customer is null)
            return NotFound();

        // Opening balance = customer opening balance + everything before the From date.
        var opening = customer.OpeningBalance;
        if (from is not null)
        {
            opening += await _db.LedgerEntries
                .Where(l => l.CustomerId == id && l.EntryDate < from.Value.Date)
                .SumAsync(l => (decimal?)(l.Debit - l.Credit)) ?? 0;
        }

        var entriesQuery = _db.LedgerEntries.AsNoTracking()
            .Where(l => l.CustomerId == id);
        if (from is not null)
            entriesQuery = entriesQuery.Where(l => l.EntryDate >= from.Value.Date);
        if (to is not null)
            entriesQuery = entriesQuery.Where(l => l.EntryDate < to.Value.Date.AddDays(1));

        var entries = await entriesQuery
            .OrderBy(l => l.EntryDate).ThenBy(l => l.Id)
            .Select(l => new LedgerRowViewModel
            {
                Id = l.Id,
                Date = l.EntryDate,
                Type = l.Type,
                Remarks = l.Remarks,
                InvoiceId = l.InvoiceId,
                SaleReturnId = l.SaleReturnId,
                Debit = l.Debit,
                Credit = l.Credit,
                CreatedBy = l.CreatedBy
            })
            .ToListAsync();

        var running = opening;
        foreach (var row in entries)
        {
            running += row.Debit - row.Credit;
            row.Balance = running;
        }

        return View(new LedgerViewModel
        {
            CustomerId = customer.Id,
            CustomerName = customer.Name,
            Phone = customer.Phone,
            From = from,
            To = to,
            OpeningBalance = opening,
            Rows = entries,
            TotalDebit = entries.Sum(e => e.Debit),
            TotalCredit = entries.Sum(e => e.Credit),
            ClosingBalance = running
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEntry(LedgerEntryFormViewModel vm)
    {
        var customer = await _db.Customers.FindAsync(vm.CustomerId);
        if (customer is null || customer.IsDeleted)
            return NotFound();

        var allowedTypes = new[] { LedgerEntryType.Payment, LedgerEntryType.Credit, LedgerEntryType.Debit };
        if (!allowedTypes.Contains(vm.EntryType))
            ModelState.AddModelError(nameof(vm.EntryType), "Invalid entry type.");

        if (!ModelState.IsValid)
        {
            TempData["Error"] = string.Join(" ", ModelState.Values
                .SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction(nameof(Ledger), new { id = vm.CustomerId });
        }

        var amount = vm.Amount!.Value;
        _db.LedgerEntries.Add(new LedgerEntry
        {
            CustomerId = vm.CustomerId,
            EntryDate = vm.EntryDate.Date,
            Type = vm.EntryType,
            // Debit increases what the customer owes; Payment/Credit decrease it.
            Debit = vm.EntryType == LedgerEntryType.Debit ? amount : 0,
            Credit = vm.EntryType == LedgerEntryType.Debit ? 0 : amount,
            Remarks = vm.Remarks.Trim()
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"{vm.EntryType} of Rs. {amount:N2} recorded for {customer.Name}.";
        return RedirectToAction(nameof(Ledger), new { id = vm.CustomerId });
    }

    /// <summary>
    /// Proper accounting correction: instead of deleting a wrong manual entry,
    /// an opposite (contra) entry is posted so the audit trail stays intact.
    /// Invoice/return-linked entries are corrected through their source document.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = DbSeeder.AdminRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReverseEntry(int id)
    {
        var entry = await _db.LedgerEntries.FindAsync(id);
        if (entry is null || entry.IsDeleted)
            return NotFound();

        if (entry.InvoiceId is not null || entry.SaleReturnId is not null)
        {
            TempData["Error"] = "Entries linked to an invoice or return cannot be reversed here. Correct the source document (delete the invoice or make a return) instead.";
            return RedirectToAction(nameof(Ledger), new { id = entry.CustomerId });
        }

        var isCredit = entry.Credit > 0;
        var amount = isCredit ? entry.Credit : entry.Debit;

        _db.LedgerEntries.Add(new LedgerEntry
        {
            CustomerId = entry.CustomerId,
            EntryDate = DateTime.Today,
            Type = isCredit ? LedgerEntryType.Debit : LedgerEntryType.Credit,
            Debit = isCredit ? amount : 0,
            Credit = isCredit ? 0 : amount,
            Remarks = $"Reversal of {entry.Type} (Rs. {amount:N2}, {entry.EntryDate:dd MMM yyyy})"
                      + (string.IsNullOrWhiteSpace(entry.Remarks) ? "" : $" — \"{entry.Remarks}\"")
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Reversal entry of Rs. {amount:N2} posted. The original entry stays visible for the audit trail.";
        return RedirectToAction(nameof(Ledger), new { id = entry.CustomerId });
    }
}
