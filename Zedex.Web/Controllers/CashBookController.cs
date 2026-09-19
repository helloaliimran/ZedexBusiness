using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;

namespace Zedex.Web.Controllers;

/// <summary>
/// Daily / date-range money summary:
/// Sales vs Expenses (business view) and Cash In vs Cash Out (money view).
/// Cash In = ledger Payment entries (bill payments at posting + later customer payments).
/// Cash Out = Expenses + employee payments (net salary, advances, bonuses).
/// </summary>
[Authorize(Policy = "Module:CashBook")]
public class CashBookController : Controller
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private readonly AppDbContext _db;
    private readonly IReportExportService _export;

    public CashBookController(AppDbContext db, IReportExportService export)
    {
        _db = db;
        _export = export;
    }

    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        var (f, t) = Range(from, to);
        return View(await BuildAsync(f, t, withBreakdown: true));
    }

    public async Task<IActionResult> Excel(DateTime? from, DateTime? to)
    {
        var (f, t) = Range(from, to);
        var vm = await BuildAsync(f, t, withBreakdown: false);
        var bytes = _export.ToExcel("Cash Book", Subtitle(f, t), Headers, ExportRows(vm));
        return File(bytes, ExcelContentType, $"cash-book-{f:yyyyMMdd}-{t:yyyyMMdd}.xlsx");
    }

    public async Task<IActionResult> Pdf(DateTime? from, DateTime? to)
    {
        var (f, t) = Range(from, to);
        var vm = await BuildAsync(f, t, withBreakdown: false);
        var bytes = _export.ToPdf("Cash Book", Subtitle(f, t), Headers, ExportRows(vm));
        return File(bytes, "application/pdf", $"cash-book-{f:yyyyMMdd}-{t:yyyyMMdd}.pdf");
    }

    private static readonly string[] Headers =
        { "Date", "Bills", "Sales", "Returns", "Cash In", "Expenses", "Staff Paid", "Total Out", "Cash In − Out", "Sale − Expense" };

    private static IEnumerable<object?[]> ExportRows(CashBookViewModel vm)
    {
        foreach (var r in vm.Rows)
            yield return new object?[] { r.Date, r.Bills, r.Sales, r.Returns, r.CashIn, r.Expenses, r.StaffPayments, r.TotalOut, r.NetCash, r.SaleMinusExpense };
        yield return new object?[] { "TOTAL", vm.TotalBills, vm.TotalSales, vm.TotalReturns, vm.TotalCashIn, vm.TotalExpenses, vm.TotalStaff, vm.TotalOut, vm.TotalNetCash, vm.TotalSaleMinusExpense };
    }

    private async Task<CashBookViewModel> BuildAsync(DateTime from, DateTime to, bool withBreakdown)
    {
        var end = to.AddDays(1);

        var sales = await _db.Invoices.AsNoTracking()
            .Where(i => i.IsPosted && i.InvoiceDate >= from && i.InvoiceDate < end)
            .GroupBy(i => i.InvoiceDate.Date)
            .Select(g => new { Date = g.Key, Bills = g.Count(), Total = g.Sum(i => i.Total) })
            .ToListAsync();

        var returns = await _db.SaleReturns.AsNoTracking()
            .Where(r => r.ReturnDate >= from && r.ReturnDate < end)
            .GroupBy(r => r.ReturnDate.Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(r => r.TotalAmount) })
            .ToListAsync();

        var cashIn = await _db.LedgerEntries.AsNoTracking()
            .Where(l => l.Type == LedgerEntryType.Payment && l.EntryDate >= from && l.EntryDate < end)
            .GroupBy(l => l.EntryDate.Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(l => l.Credit) })
            .ToListAsync();

        var expenses = await _db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate < end)
            .GroupBy(e => new { Date = e.ExpenseDate.Date, e.PaymentSource })
            .Select(g => new { g.Key.Date, g.Key.PaymentSource, Total = g.Sum(e => e.Amount) })
            .ToListAsync();

        var staff = await _db.EmployeeTransactions.AsNoTracking()
            .Where(x => x.TransactionDate >= from && x.TransactionDate < end)
            .GroupBy(x => new { Date = x.TransactionDate.Date, x.PaymentSource })
            .Select(g => new { g.Key.Date, g.Key.PaymentSource, Total = g.Sum(x => x.NetPaid) })
            .ToListAsync();

        var days = sales.Select(x => x.Date)
            .Concat(returns.Select(x => x.Date))
            .Concat(cashIn.Select(x => x.Date))
            .Concat(expenses.Select(x => x.Date))
            .Concat(staff.Select(x => x.Date))
            .Distinct()
            .OrderBy(d => d);

        var rows = days.Select(d => new CashBookRowViewModel
        {
            Date = d,
            Bills = sales.Where(x => x.Date == d).Sum(x => x.Bills),
            Sales = sales.Where(x => x.Date == d).Sum(x => x.Total),
            Returns = returns.Where(x => x.Date == d).Sum(x => x.Total),
            CashIn = cashIn.Where(x => x.Date == d).Sum(x => x.Total),
            ExpenseCash = expenses.Where(x => x.Date == d && x.PaymentSource == PaymentSource.Cash).Sum(x => x.Total),
            ExpenseOnline = expenses.Where(x => x.Date == d && x.PaymentSource == PaymentSource.Online).Sum(x => x.Total),
            StaffCash = staff.Where(x => x.Date == d && x.PaymentSource == PaymentSource.Cash).Sum(x => x.Total),
            StaffOnline = staff.Where(x => x.Date == d && x.PaymentSource == PaymentSource.Online).Sum(x => x.Total)
        }).ToList();

        var vm = new CashBookViewModel { From = from, To = to, Rows = rows };
        if (!withBreakdown)
            return vm;

        vm.ExpenseByCategory = await _db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate < end)
            .GroupBy(e => e.ExpenseCategory.Name)
            .Select(g => new CashBookBreakdownViewModel { Label = g.Key, Count = g.Count(), Amount = g.Sum(e => e.Amount) })
            .OrderByDescending(x => x.Amount)
            .ToListAsync();

        var staffByType = await _db.EmployeeTransactions.AsNoTracking()
            .Where(x => x.TransactionDate >= from && x.TransactionDate < end)
            .GroupBy(x => x.Type)
            .Select(g => new { Type = g.Key, Count = g.Count(), Amount = g.Sum(x => x.NetPaid) })
            .ToListAsync();
        vm.StaffByType = staffByType
            .Select(x => new CashBookBreakdownViewModel
            {
                Label = x.Type switch
                {
                    EmployeeTransactionType.SalaryPayment => "Salary (net paid)",
                    EmployeeTransactionType.Advance => "Advances",
                    EmployeeTransactionType.Bonus => "Bonus",
                    _ => x.Type.ToString()
                },
                Count = x.Count,
                Amount = x.Amount
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        return vm;
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
