using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Api.DTOs.Customers;
using Zedex.Api.Extensions;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;

namespace Zedex.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Authorize]
[Produces("application/json")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _db;

    public CustomersController(AppDbContext db) => _db = db;

    // ── GET /api/customers ────────────────────────────────────────────────────

    /// <summary>
    /// Returns all customers with their current closing balance.
    /// ClosingBalance = OpeningBalance + Σ(Debit) − Σ(Credit).
    /// Positive = customer owes money (red on mobile). Zero/negative = settled or in credit (green).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<CustomerSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomers([FromQuery] string? search)
    {
        // Allow access if the user has either Customers or CustomerLedger module
        if (!User.HasModule(AppModule.Customers) && !User.HasModule(AppModule.CustomerLedger))
            return Forbid();

        var query = _db.Customers
            .AsNoTracking()
            .Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                (c.Phone != null && c.Phone.Contains(search.Trim())));
        }

        // Compute TotalDebit and TotalCredit in a single DB round-trip via projection.
        var rows = await query
            .OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Phone,
                c.Address,
                c.OpeningBalance,
                TotalDebit  = c.LedgerEntries
                                .Where(l => !l.IsDeleted)
                                .Sum(l => (decimal?)l.Debit) ?? 0m,
                TotalCredit = c.LedgerEntries
                                .Where(l => !l.IsDeleted)
                                .Sum(l => (decimal?)l.Credit) ?? 0m
            })
            .ToListAsync();

        var result = rows.Select(r => new CustomerSummaryDto
        {
            CustomerId     = r.Id,
            Name           = r.Name,
            Phone          = r.Phone,
            Address        = r.Address,
            OpeningBalance = r.OpeningBalance,
            ClosingBalance = r.OpeningBalance + r.TotalDebit - r.TotalCredit
        }).ToList();

        return Ok(result);
    }

    // ── GET /api/customers/{id}/ledger ────────────────────────────────────────

    /// <summary>
    /// Returns the full ledger for a customer, newest entry first.
    /// Each entry carries its running balance at that point in time.
    /// Bill and Return entries include InvoiceId → the mobile app navigates to Bill Detail on tap.
    /// Date range filters the displayed entries but closing balance always reflects ALL entries.
    /// </summary>
    [HttpGet("{id:int}/ledger")]
    [ProducesResponseType(typeof(LedgerResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLedger(
        int               id,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int       page     = 1,
        [FromQuery] int       pageSize = 30)
    {
        if (!User.HasModule(AppModule.CustomerLedger)) return Forbid();

        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);

        var customer = await _db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id && !c.IsDeleted);

        if (customer is null)
            return NotFound(new { message = "Customer not found." });

        // Load ALL entries (oldest → newest) to compute running balances correctly.
        // A business ledger typically has hundreds of rows — fine to materialise in memory.
        var allEntries = await _db.LedgerEntries
            .AsNoTracking()
            .Include(l => l.Invoice)
            .Where(l => l.CustomerId == id && !l.IsDeleted)
            .OrderBy(l => l.EntryDate)
            .ThenBy(l => l.Id)          // stable sort within the same date
            .ToListAsync();

        // Walk oldest → newest, accumulate running balance on each entry.
        decimal running = customer.OpeningBalance;
        var withBalance = allEntries.Select(l =>
        {
            running += l.Debit - l.Credit;
            return (Entry: l, Balance: running);
        }).ToList();

        // ClosingBalance = the final running value (reflects every entry, no filter applied).
        var closingBalance = running;

        // Apply optional date range filter for what the user SEES.
        var filtered = withBalance.AsEnumerable();
        if (from.HasValue)
            filtered = filtered.Where(x => x.Entry.EntryDate.Date >= from.Value.Date);
        if (to.HasValue)
            filtered = filtered.Where(x => x.Entry.EntryDate.Date <= to.Value.Date);

        // Reverse to newest-first, then page.
        var reversed   = filtered.Reverse().ToList();
        var totalCount = reversed.Count;

        var paged = reversed
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LedgerEntryDto
            {
                EntryId        = x.Entry.Id,
                EntryDate      = x.Entry.EntryDate,
                Type           = x.Entry.Type.ToString(),
                Debit          = x.Entry.Debit,
                Credit         = x.Entry.Credit,
                RunningBalance = x.Balance,
                Remarks        = x.Entry.Remarks,
                InvoiceId      = x.Entry.InvoiceId,
                InvoiceNumber  = x.Entry.Invoice?.InvoiceNumber
            })
            .ToList();

        return Ok(new LedgerResponseDto
        {
            CustomerId     = customer.Id,
            CustomerName   = customer.Name,
            CustomerPhone  = customer.Phone,
            OpeningBalance = customer.OpeningBalance,
            ClosingBalance = closingBalance,
            Entries        = paged,
            TotalEntries   = totalCount,
            Page           = page,
            PageSize       = pageSize
        });
    }
}
