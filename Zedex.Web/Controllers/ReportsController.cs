using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;

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
                TotalReceived = c.LedgerEntries
                    .Where(l => !l.IsDeleted)
                    .Sum(l => (decimal?)l.Credit) ?? 0,
                Balance = c.OpeningBalance + (c.LedgerEntries
                    .Where(l => !l.IsDeleted)
                    .Sum(l => (decimal?)(l.Debit - l.Credit)) ?? 0)
            })
            .ToListAsync();

        return onlyOutstanding ? rows.Where(r => r.Balance > 0).ToList() : rows;
    }

    // =========================================================
    // 2. Daily Bill Report
    // =========================================================

    public async Task<IActionResult> DailyBills(
        DateTime? from, DateTime? to, PaymentType? type, string? userName, string? search)
    {
        var (f, t) = Range(from, to);
        await LoadWorkersAsync(userName);
        return View(new DailyBillReportViewModel
        {
            From = f, To = t, Type = type, UserName = userName, Search = search,
            Rows = await QueryDailyBillsAsync(f, t, type, userName, search)
        });
    }

    public async Task<IActionResult> DailyBillsExcel(
        DateTime? from, DateTime? to, PaymentType? type, string? userName, string? search)
    {
        var (f, t) = Range(from, to);
        var rows = await QueryDailyBillsAsync(f, t, type, userName, search);
        var bytes = _export.ToExcel("Daily Bill Report", Subtitle(f, t),
            new[] { "Bill #", "Customer", "Total Amount", "Paid", "Payment Type", "User", "Date & Time" },
            rows.Select(r => new object?[]
                { r.InvoiceNumber, r.Customer, r.Total, r.PaidAmount, r.PaymentType.ToString(), r.PostedBy, r.PostedDate }));
        return File(bytes, ExcelContentType, $"daily-bills-{f:yyyyMMdd}-{t:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> DailyBillsPdf(
        DateTime? from, DateTime? to, PaymentType? type, string? userName, string? search)
    {
        var (f, t) = Range(from, to);
        var rows = await QueryDailyBillsAsync(f, t, type, userName, search);
        var bytes = _export.ToPdf("Daily Bill Report", Subtitle(f, t),
            new[] { "Bill #", "Customer", "Total", "Paid", "Type", "User", "Date & Time" },
            rows.Select(r => new object?[]
                { r.InvoiceNumber, r.Customer, r.Total, r.PaidAmount, r.PaymentType.ToString(), r.PostedBy, r.PostedDate }));
        return File(bytes, "application/pdf", $"daily-bills-{f:yyyyMMdd}-{t:yyyyMMdd}.pdf");
    }

    private async Task<List<DailyBillRowViewModel>> QueryDailyBillsAsync(
        DateTime from, DateTime to, PaymentType? type, string? userName, string? search)
    {
        var query = _db.Invoices.AsNoTracking()
            .Where(i => i.IsPosted && i.InvoiceDate >= from && i.InvoiceDate < to.AddDays(1));

        if (type is not null)
            query = query.Where(i => i.PaymentType == type);
        if (!string.IsNullOrWhiteSpace(userName))
            query = query.Where(i => i.PostedBy == userName);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(i => EF.Functions.ILike(i.InvoiceNumber, pattern) ||
                                     EF.Functions.ILike(i.Customer.Name, pattern));
        }

        return await query
            .OrderBy(i => i.PostedDate)
            .Select(i => new DailyBillRowViewModel
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                Customer = i.Customer.Name,
                Total = i.Total,
                PaidAmount = i.PaidAmount,
                PaymentType = i.PaymentType,
                PostedBy = i.PostedBy,
                PostedDate = i.PostedDate
            })
            .ToListAsync();
    }

    // =========================================================
    // 3. Daily Sales Report
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

    public async Task<IActionResult> DailySalesExcel(DateTime? from, DateTime? to, string? userName, int? customerId)
    {
        var (f, t) = Range(from, to);
        var rows = await QueryDailySalesAsync(f, t, userName, customerId);
        var bytes = _export.ToExcel("Daily Sales Report", Subtitle(f, t),
            new[] { "Date", "Total Bills", "Total Sales", "Cash Sales", "Credit Sales", "Partial", "Collection", "Outstanding" },
            rows.Select(r => new object?[] { r.Date, r.Bills, r.Sales, r.Cash, r.Credit, r.Partial, r.Collection, r.Outstanding }));
        return File(bytes, ExcelContentType, $"daily-sales-{f:yyyyMMdd}-{t:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> DailySalesPdf(DateTime? from, DateTime? to, string? userName, int? customerId)
    {
        var (f, t) = Range(from, to);
        var rows = await QueryDailySalesAsync(f, t, userName, customerId);
        var bytes = _export.ToPdf("Daily Sales Report", Subtitle(f, t),
            new[] { "Date", "Bills", "Sales", "Cash", "Credit", "Partial", "Collection", "Outstanding" },
            rows.Select(r => new object?[] { r.Date, r.Bills, r.Sales, r.Cash, r.Credit, r.Partial, r.Collection, r.Outstanding }));
        return File(bytes, "application/pdf", $"daily-sales-{f:yyyyMMdd}-{t:yyyyMMdd}.pdf");
    }

    private async Task<List<DailySalesRowViewModel>> QueryDailySalesAsync(
        DateTime from, DateTime to, string? userName, int? customerId)
    {
        var query = _db.Invoices.AsNoTracking()
            .Where(i => i.IsPosted && i.InvoiceDate >= from && i.InvoiceDate < to.AddDays(1));

        if (!string.IsNullOrWhiteSpace(userName))
            query = query.Where(i => i.PostedBy == userName);
        if (customerId is > 0)
            query = query.Where(i => i.CustomerId == customerId);

        return await query
            .GroupBy(i => i.InvoiceDate)
            .OrderBy(g => g.Key)
            .Select(g => new DailySalesRowViewModel
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
