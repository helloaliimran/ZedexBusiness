using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;

namespace Zedex.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPermissionService _permissions;

    public HomeController(AppDbContext db, IPermissionService permissions)
    {
        _db = db;
        _permissions = permissions;
    }

    public async Task<IActionResult> Index()
    {
        var modules = await _permissions.GetVisibleModulesAsync(User);
        if (!modules.Contains(AppModule.Dashboard))
            return View("Welcome");

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        // ---- Today's billing (posted invoices, by invoice date) ----
        var todayInvoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.IsPosted && i.InvoiceDate >= today && i.InvoiceDate < tomorrow)
            .GroupBy(i => 1)
            .Select(g => new
            {
                Sales = g.Sum(i => i.Total),
                Bills = g.Count(),
                Cash = g.Where(i => i.PaymentType == PaymentType.Cash).Sum(i => i.Total),
                Credit = g.Where(i => i.PaymentType == PaymentType.Credit).Sum(i => i.Total),
                Partial = g.Where(i => i.PaymentType == PaymentType.Partial).Sum(i => i.Total)
            })
            .FirstOrDefaultAsync();

        // ---- Today's collection: every payment credited today (invoice + standalone) ----
        var todayCollection = await _db.LedgerEntries.AsNoTracking()
            .Where(l => l.Type == LedgerEntryType.Payment && l.EntryDate >= today && l.EntryDate < tomorrow)
            .SumAsync(l => (decimal?)l.Credit) ?? 0;

        // ---- Balances: receivables vs advances ----
        var balances = await _db.Customers.AsNoTracking()
            .Select(c => c.OpeningBalance + (c.LedgerEntries
                .Where(l => !l.IsDeleted)
                .Sum(l => (decimal?)(l.Debit - l.Credit)) ?? 0))
            .ToListAsync();

        // ---- Stock summary ----
        var stockStats = await _db.Products.AsNoTracking()
            .GroupBy(p => 1)
            .Select(g => new
            {
                Total = g.Count(),
                InStock = g.Count(p => p.CurrentStock > 0),
                Negative = g.Count(p => p.CurrentStock < 0)
            })
            .FirstOrDefaultAsync();

        var lowStock = await _db.Products.AsNoTracking()
            .OrderBy(p => p.CurrentStock)
            .Take(8)
            .Select(p => new LowStockItemViewModel
            {
                ProductId = p.Id,
                Name = p.Name + " (" + p.Color.Name + ", G" + p.Gauge.Name + ")",
                Mode = p.PricingMode,
                CurrentStock = p.CurrentStock
            })
            .ToListAsync();

        var recentInvoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.IsPosted)
            .OrderByDescending(i => i.PostedDate)
            .Take(6)
            .Select(i => new RecentInvoiceViewModel
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                Customer = i.Customer.Name,
                InvoiceDate = i.InvoiceDate,
                Total = i.Total,
                PaymentType = i.PaymentType
            })
            .ToListAsync();

        var vm = new DashboardViewModel
        {
            TodaySales = todayInvoices?.Sales ?? 0,
            TodayBills = todayInvoices?.Bills ?? 0,
            TodayCashSales = todayInvoices?.Cash ?? 0,
            TodayCreditSales = todayInvoices?.Credit ?? 0,
            TodayPartialSales = todayInvoices?.Partial ?? 0,
            TodayCollection = todayCollection,
            OutstandingReceivables = balances.Where(b => b > 0).Sum(),
            AdvancesHeld = -balances.Where(b => b < 0).Sum(),
            CustomerCount = balances.Count,
            ProductCount = stockStats?.Total ?? 0,
            InStockProducts = stockStats?.InStock ?? 0,
            NegativeStockProducts = stockStats?.Negative ?? 0,
            LowStock = lowStock,
            RecentInvoices = recentInvoices,
            CanBill = modules.Contains(AppModule.Billing),
            CanStock = modules.Contains(AppModule.Stock),
            CanCustomers = modules.Contains(AppModule.Customers)
        };
        return View(vm);
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
