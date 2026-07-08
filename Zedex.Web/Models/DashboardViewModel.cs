using Zedex.Domain.Enums;

namespace Zedex.Web.Models;

public class LowStockItemViewModel
{
    public int ProductId { get; set; }
    public string Name { get; set; } = default!;
    public PricingMode Mode { get; set; }
    public decimal CurrentStock { get; set; }
    public string StockLabel => Mode == PricingMode.PerFoot
        ? $"{CurrentStock:N2} ft"
        : $"{CurrentStock:N0} units";
}

public class RecentInvoiceViewModel
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = default!;
    public string Customer { get; set; } = default!;
    public DateTime InvoiceDate { get; set; }
    public decimal Total { get; set; }
    public PaymentType? PaymentType { get; set; }
}

public class DashboardViewModel
{
    // Today
    public decimal TodaySales { get; set; }
    public int TodayBills { get; set; }
    public decimal TodayCashSales { get; set; }
    public decimal TodayCreditSales { get; set; }
    public decimal TodayPartialSales { get; set; }
    /// <summary>All money received today (invoice payments + standalone ledger payments).</summary>
    public decimal TodayCollection { get; set; }

    // Overall
    public decimal OutstandingReceivables { get; set; }
    public decimal AdvancesHeld { get; set; }
    public int CustomerCount { get; set; }
    public int ProductCount { get; set; }
    public int InStockProducts { get; set; }
    public int NegativeStockProducts { get; set; }

    public List<LowStockItemViewModel> LowStock { get; set; } = new();
    public List<RecentInvoiceViewModel> RecentInvoices { get; set; } = new();

    // Quick-action visibility (mirrors module permissions)
    public bool CanBill { get; set; }
    public bool CanStock { get; set; }
    public bool CanCustomers { get; set; }
}
