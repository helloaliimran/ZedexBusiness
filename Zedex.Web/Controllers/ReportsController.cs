using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;
using Zedex.Web.Services;

namespace Zedex.Web.Controllers;

[Authorize(Policy = "Module:Reports")]
public class ReportsController : Controller
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private readonly AppDbContext _db;
    private readonly IReportExportService _export;

    public ReportsController(AppDbContext db, IReportExportService export)
    {
        _db = db;
        _export = export;
    }

    public IActionResult Index() => View();

    // =========================================================
    // 1. Customer Credit Report
    // =========================================================

    public async Task<IActionResult> CustomerCredit(string? search, bool onlyOutstanding = true)
    {
        return View(new CustomerCreditReportViewModel
        {
            Search = search,
            OnlyOutstanding = onlyOutstanding,
            Rows = await QueryCustomerCreditAsync(search, onlyOutstanding)
        });
    }

    public async Task<IActionResult> CustomerCreditExcel(string? search, bool onlyOutstanding = true)
    {
        var rows = await QueryCustomerCreditAsync(search, onlyOutstanding);
        var bytes = _export.ToExcel("Customer Credit Report", Subtitle(),
            new[] { "Customer", "Phone", "Opening Balance", "Total Billed", "Total Received", "Remaining Balance" },
            rows.Select(r => new object?[] { r.Name, r.Phone, r.OpeningBalance, r.TotalBilled, r.TotalReceived, r.Balance }));
        return File(bytes, ExcelContentType, $"customer-credit-{DateTime.Today:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> CustomerCreditPdf(string? search, bool onlyOutstanding = true)
    {
        var rows = await QueryCustomerCreditAsync(search, onlyOutstanding);
        var bytes = _export.ToPdf("Customer Credit Report", Subtitle(),
            new[] { "Customer", "Phone", "Opening", "Billed", "Received", "Balance" },
            rows.Select(r => new object?[] { r.Name, r.Phone, r.OpeningBalance, r.TotalBilled, r.TotalReceived, r.Balance }));
        return File(bytes, "application/pdf", $"customer-credit-{DateTime.Today:yyyyMMdd}.pdf");
    }

    private async Task<List<CustomerCreditRowViewModel>> QueryCustomerCreditAsync(string? search, bool onlyOutstanding)
    {
        var query = _db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(c => EF.Functions.ILike(c.Name, pattern) ||
                                     (c.Phone != null && EF.Functions.ILike(c.Phone, pattern)));
        }

        var rows = await query
            .OrderBy(c => c.Name)
            .Select(c => new CustomerCreditRowViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Phone = c.Phone,
                OpeningBalance = c.OpeningBalance,
                TotalBilled = c.LedgerEntries
                    .Where(l => !l.IsDeleted && l.Type == LedgerEntryType.Bill)
                    .Sum(l => (decimal?)l.Debit) ?? 0,
                // A reversed payment is a Payment row with a Debit — net it out of "received".
                TotalReceived = c.LedgerEntries
                    .Where(l => !l.IsDeleted)
                    .Sum(l => (decimal?)(l.Type == LedgerEntryType.Payment ? l.Credit - l.Debit : l.Credit)) ?? 0,
                Balance = c.OpeningBalance + (c.LedgerEntries
                    .Where(l => !l.IsDeleted)
                    .Sum(l => (decimal?)(l.Debit - l.Credit)) ?? 0)
            })
            .ToListAsync();

        return onlyOutstanding ? rows.Where(r => r.Balance > 0).ToList() : rows;
    }

    // =========================================================
    // 2. Daily Bill Report
    //    Bills posted in the period + customer payments received in the ledger
    //    (e.g. a credit bill settled later the same day), split Cash / Online.
    // =========================================================

    public async Task<IActionResult> DailyBills(
        DateTime? from, DateTime? to, PaymentType? type, string? userName, string? search)
    {
        var (f, t) = Range(from, to);
        await LoadWorkersAsync(userName);
        return View(await BuildDailyBillsAsync(f, t, type, userName, search));
    }

    public async Task<IActionResult> DailyBillsExcel(
        DateTime? from, DateTime? to, PaymentType? type, string? userName, string? search)
    {
        var (f, t) = Range(from, to);
        var vm = await BuildDailyBillsAsync(f, t, type, userName, search);
        var bytes = _export.ToExcel("Daily Bill Report", Subtitle(f, t), DailyBillHeaders, DailyBillExportRows(vm));
        return File(bytes, ExcelContentType, $"daily-bills-{f:yyyyMMdd}-{t:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> DailyBillsPdf(
        DateTime? from, DateTime? to, PaymentType? type, string? userName, string? search)
    {
        var (f, t) = Range(from, to);
        var vm = await BuildDailyBillsAsync(f, t, type, userName, search);
        var bytes = _export.ToPdf("Daily Bill Report", Subtitle(f, t), DailyBillHeaders, DailyBillExportRows(vm));
        return File(bytes, "application/pdf", $"daily-bills-{f:yyyyMMdd}-{t:yyyyMMdd}.pdf");
    }

    private static readonly string[] DailyBillHeaders =
        { "Ref", "Customer", "Bill Total", "Received", "Cash", "Online", "Type", "User", "Date & Time" };

    private static IEnumerable<object?[]> DailyBillExportRows(DailyBillReportViewModel vm)
    {
        foreach (var r in vm.Rows)
            yield return new object?[]
                { r.InvoiceNumber, r.Customer, r.Total, r.PaidAmount, r.PaidCash, r.PaidOnline, r.PaymentType.ToString(), r.PostedBy, r.PostedDate };
        if (vm.Payments.Any())
        {
            yield return new object?[] { "LEDGER PAYMENTS", null, null, null, null, null, null, null, null };
            foreach (var p in vm.Payments)
                yield return new object?[]
                {
                    "Payment", p.Customer, null, p.Amount,
                    p.Source == PaymentSource.Cash ? p.Amount : 0m,
                    p.Source == PaymentSource.Online ? p.Amount : 0m,
                    p.Remarks, p.CreatedBy, p.CreatedDate
                };
        }
        yield return new object?[] { "TOTAL", null, vm.TotalAmount, vm.TotalReceived, vm.TotalCash, vm.TotalOnline, null, null, null };
    }

    private async Task<DailyBillReportViewModel> BuildDailyBillsAsync(
        DateTime from, DateTime to, PaymentType? type, string? userName, string? search)
    {
        var end = to.AddDays(1);
        // Bills are reported on the day they were POSTED (a draft may be posted days later).
        var bills = _db.Invoices.AsNoTracking().PostedBetween(from, end);

        if (type is not null)
            bills = bills.Where(i => i.PaymentType == type);
        if (!string.IsNullOrWhiteSpace(userName))
            bills = bills.Where(i => i.PostedBy == userName);

        var payments = _db.LedgerEntries.AsNoTracking()
            .Where(l => l.Type == LedgerEntryType.Payment && l.InvoiceId == null
                        && l.EntryDate >= from && l.EntryDate < end);
        if (!string.IsNullOrWhiteSpace(userName))
            payments = payments.Where(l => l.CreatedBy == userName);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            bills = bills.Where(i => EF.Functions.ILike(i.InvoiceNumber, pattern) ||
                                     EF.Functions.ILike(i.Customer.Name, pattern));
            payments = payments.Where(l => EF.Functions.ILike(l.Customer.Name, pattern));
        }

        var rows = await bills
            .OrderBy(i => i.PostedDate)
            .Select(i => new DailyBillRowViewModel
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceType = i.InvoiceType,
                BillDate = i.InvoiceDate,
                Customer = i.Customer.Name,
                Total = i.Total,
                PaidAmount = i.PaidAmount,
                PaidOnline = _db.LedgerEntries
                    .Where(l => l.InvoiceId == i.Id && l.Type == LedgerEntryType.Payment
                                && l.PaymentSource == PaymentSource.Online)
                    .Sum(l => (decimal?)(l.Credit - l.Debit)) ?? 0,
                PaymentType = i.PaymentType,
                PostedBy = i.PostedBy,
                PostedDate = i.PostedDate
            })
            .ToListAsync();

        // A bill-type filter (e.g. "Credit") narrows the bills only; ledger payments are
        // still listed so the day's money received is complete.
        var paymentRows = await payments
            .OrderBy(l => l.EntryDate).ThenBy(l => l.CreatedDate)
            .Select(l => new LedgerPaymentRowViewModel
            {
                Id = l.Id,
                CustomerId = l.CustomerId,
                Customer = l.Customer.Name,
                Date = l.EntryDate,
                Amount = l.Credit - l.Debit,
                Source = l.PaymentSource,
                Remarks = l.Remarks,
                HasAttachment = l.AttachmentPath != null,
                CreatedBy = l.CreatedBy,
                CreatedDate = l.CreatedDate
            })
            .ToListAsync();

        return new DailyBillReportViewModel
        {
            From = from, To = to, Type = type, UserName = userName, Search = search,
            Rows = rows,
            Payments = paymentRows
        };
    }

    // =========================================================
    // 3. Daily Sales Report
    //    Collection = paid at billing; Recovery = paid later in the ledger.
    // =========================================================

    public async Task<IActionResult> DailySales(DateTime? from, DateTime? to, string? userName, int? customerId)
    {
        var (f, t) = Range(from, to);
        await LoadWorkersAsync(userName);
        await LoadCustomersAsync(customerId);
        return View(new DailySalesReportViewModel
        {
            From = f, To = t, UserName = userName, CustomerId = customerId,
            Rows = await QueryDailySalesAsync(f, t, userName, customerId)
        });
    }

    private static readonly string[] DailySalesHeaders =
        { "Date", "Bills", "Sales", "Cash Sales", "Credit Sales", "Partial", "Paid at Billing", "Received Later", "Total Received", "Cash", "Online", "Outstanding" };

    private static object?[] DailySalesCells(DailySalesRowViewModel r) =>
        new object?[] { r.Date, r.Bills, r.Sales, r.Cash, r.Credit, r.Partial, r.Collection, r.Recovery, r.TotalReceived, r.ReceivedCash, r.ReceivedOnline, r.Outstanding };

    public async Task<IActionResult> DailySalesExcel(DateTime? from, DateTime? to, string? userName, int? customerId)
    {
        var (f, t) = Range(from, to);
        var rows = await QueryDailySalesAsync(f, t, userName, customerId);
        var bytes = _export.ToExcel("Daily Sales Report", Subtitle(f, t), DailySalesHeaders, rows.Select(DailySalesCells));
        return File(bytes, ExcelContentType, $"daily-sales-{f:yyyyMMdd}-{t:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> DailySalesPdf(DateTime? from, DateTime? to, string? userName, int? customerId)
    {
        var (f, t) = Range(from, to);
        var rows = await QueryDailySalesAsync(f, t, userName, customerId);
        var bytes = _export.ToPdf("Daily Sales Report", Subtitle(f, t), DailySalesHeaders, rows.Select(DailySalesCells));
        return File(bytes, "application/pdf", $"daily-sales-{f:yyyyMMdd}-{t:yyyyMMdd}.pdf");
    }

    private async Task<List<DailySalesRowViewModel>> QueryDailySalesAsync(
        DateTime from, DateTime to, string? userName, int? customerId)
    {
        var end = to.AddDays(1);
        // Sales by posting day; payments by the day the money was received.
        var query = _db.Invoices.AsNoTracking().PostedBetween(from, end);
        var ledger = _db.LedgerEntries.AsNoTracking().PaymentsBetween(from, end);

        if (!string.IsNullOrWhiteSpace(userName))
        {
            query = query.Where(i => i.PostedBy == userName);
            // Billing payments follow their bill's poster; later payments their entering user.
            ledger = ledger.Where(l => l.InvoiceId != null ? l.Invoice!.PostedBy == userName : l.CreatedBy == userName);
        }
        if (customerId is > 0)
        {
            query = query.Where(i => i.CustomerId == customerId);
            ledger = ledger.Where(l => l.CustomerId == customerId);
        }

        var sales = await query
            .GroupBy(i => (i.PostedDate ?? i.InvoiceDate).Date)
            .Select(g => new
            {
                Date = g.Key,
                Bills = g.Count(),
                Sales = g.Sum(i => i.Total),
                Cash = g.Where(i => i.PaymentType == PaymentType.Cash).Sum(i => i.Total),
                Credit = g.Where(i => i.PaymentType == PaymentType.Credit).Sum(i => i.Total),
                Partial = g.Where(i => i.PaymentType == PaymentType.Partial).Sum(i => i.Total),
                Collection = g.Sum(i => i.PaidAmount)
            })
            .ToListAsync();

        var received = await ledger
            .GroupBy(l => new { Date = (l.InvoiceId != null && l.Invoice!.PostedDate != null ? l.Invoice.PostedDate.Value : l.EntryDate).Date, AtBilling = l.InvoiceId != null, l.PaymentSource })
            .Select(g => new { g.Key.Date, g.Key.AtBilling, g.Key.PaymentSource, Amount = g.Sum(l => l.Credit - l.Debit) })
            .ToListAsync();

        var days = sales.Select(x => x.Date).Concat(received.Select(x => x.Date)).Distinct().OrderBy(d => d);
        return days.Select(d =>
        {
            var s = sales.FirstOrDefault(x => x.Date == d);
            return new DailySalesRowViewModel
            {
                Date = d,
                Bills = s?.Bills ?? 0,
                Sales = s?.Sales ?? 0,
                Cash = s?.Cash ?? 0,
                Credit = s?.Credit ?? 0,
                Partial = s?.Partial ?? 0,
                Collection = s?.Collection ?? 0,
                Recovery = received.Where(x => x.Date == d && !x.AtBilling).Sum(x => x.Amount),
                ReceivedOnline = received.Where(x => x.Date == d && x.PaymentSource == PaymentSource.Online).Sum(x => x.Amount)
            };
        }).ToList();
    }

    // =========================================================
    // 4. Stock Status Report
    // =========================================================

    public async Task<IActionResult> StockStatus(string? search, bool onlyInStock = false, int? companyId = null)
    {
        await LoadCompaniesAsync(companyId);
        return View(new StockStatusReportViewModel
        {
            Search = search,
            OnlyInStock = onlyInStock,
            CompanyId = companyId,
            Rows = await QueryStockStatusAsync(search, onlyInStock, companyId)
        });
    }

    public async Task<IActionResult> StockStatusExcel(string? search, bool onlyInStock = false, int? companyId = null)
    {
        var rows = await QueryStockStatusAsync(search, onlyInStock, companyId);
        var bytes = _export.ToExcel("Stock Status Report", Subtitle(),
            new[] { "Product", "Category", "Company", "Color", "Gauge", "Mode", "Current Stock" },
            rows.Select(r => new object?[] { r.Name, r.Category, r.Company, r.Color, r.Gauge, r.ModeLabel, r.StockValue }));
        return File(bytes, ExcelContentType, $"stock-status-{DateTime.Today:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> StockStatusPdf(string? search, bool onlyInStock = false, int? companyId = null)
    {
        var rows = await QueryStockStatusAsync(search, onlyInStock, companyId);
        var bytes = _export.ToPdf("Stock Status Report", Subtitle(),
            new[] { "Product", "Category", "Company", "Color", "Gauge", "Mode", "Stock" },
            rows.Select(r => new object?[] { r.Name, r.Category, r.Company, r.Color, r.Gauge, r.ModeLabel, r.StockValue }));
        return File(bytes, "application/pdf", $"stock-status-{DateTime.Today:yyyyMMdd}.pdf");
    }

    private async Task<List<StockStatusRowViewModel>> QueryStockStatusAsync(string? search, bool onlyInStock, int? companyId)
    {
        var query = _db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(p => EF.Functions.ILike(p.Name, pattern) ||
                                     EF.Functions.ILike(p.Color.Name, pattern) ||
                                     EF.Functions.ILike(p.Category.Name, pattern) ||
                                     (p.Company != null && EF.Functions.ILike(p.Company.Name, pattern)));
        }
        if (companyId is > 0)
            query = query.Where(p => p.CompanyId == companyId);

        var rows = await query
            .OrderBy(p => p.Category.Name).ThenBy(p => p.Name)
            .Select(p => new StockStatusRowViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Category = p.Category.Name,
                Company = p.Company != null ? p.Company.Name : null,
                Color = p.Color.Name,
                Gauge = p.Gauge.Name,
                Mode = p.PricingMode,
                IsPvc = p.Category.IsPvc,
                CurrentStock = p.CurrentStock,
                PieceQty = p.StockPieces.Where(s => !s.IsDeleted).Sum(s => (int?)s.Quantity) ?? 0
            })
            .ToListAsync();

        return onlyInStock ? rows.Where(r => r.StockValue != 0).ToList() : rows;
    }

    private async Task LoadCompaniesAsync(int? selected)
    {
        ViewBag.Companies = new SelectList(
            await _db.Companies.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", selected);
    }

    // =========================================================
    // Helpers
    // =========================================================

    private static (DateTime from, DateTime to) Range(DateTime? from, DateTime? to)
    {
        var f = (from ?? DateTime.Today).Date;
        var t = (to ?? f).Date;
        return t < f ? (t, f) : (f, t);
    }

    private static string Subtitle(DateTime? from = null, DateTime? to = null) =>
        from is null
            ? $"As of {DateTime.Now:dd MMM yyyy HH:mm}"
            : from == to
                ? $"{from:dd MMM yyyy}"
                : $"{from:dd MMM yyyy} — {to:dd MMM yyyy}";

    private async Task LoadWorkersAsync(string? selected)
    {
        var users = await _db.Users.AsNoTracking()
            .OrderBy(u => u.UserName)
            .Select(u => u.UserName!)
            .ToListAsync();
        ViewBag.Workers = new SelectList(users, selected);
    }

    private async Task LoadCustomersAsync(int? selected)
    {
        ViewBag.Customers = new SelectList(
            await _db.Customers.AsNoTracking().OrderBy(c => c.Name).ToListAsync(),
            "Id", "Name", selected);
    }
}
