using Zedex.Domain.Enums;

namespace Zedex.Web.Models;

// ---- Customer Credit Report ----

public class CustomerCreditRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Phone { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal TotalBilled { get; set; }
    public decimal TotalReceived { get; set; }
    public decimal Balance { get; set; }
}

public class CustomerCreditReportViewModel
{
    public string? Search { get; set; }
    public bool OnlyOutstanding { get; set; } = true;
    public List<CustomerCreditRowViewModel> Rows { get; set; } = new();
    public decimal TotalBilled => Rows.Sum(r => r.TotalBilled);
    public decimal TotalReceived => Rows.Sum(r => r.TotalReceived);
    public decimal TotalBalance => Rows.Sum(r => r.Balance);
}

// ---- Daily Bill Report ----

public class DailyBillRowViewModel
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = default!;
    public string Customer { get; set; } = default!;
    public decimal Total { get; set; }
    public decimal PaidAmount { get; set; }
    public PaymentType? PaymentType { get; set; }
    public string? PostedBy { get; set; }
    public DateTime? PostedDate { get; set; }
}

public class DailyBillReportViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public PaymentType? Type { get; set; }
    public string? UserName { get; set; }
    public string? Search { get; set; }
    public List<DailyBillRowViewModel> Rows { get; set; } = new();
    public decimal TotalAmount => Rows.Sum(r => r.Total);
    public decimal TotalPaid => Rows.Sum(r => r.PaidAmount);
}

// ---- Daily Sales Report ----

public class DailySalesRowViewModel
{
    public DateTime Date { get; set; }
    public int Bills { get; set; }
    public decimal Sales { get; set; }
    public decimal Cash { get; set; }
    public decimal Credit { get; set; }
    public decimal Partial { get; set; }
    public decimal Collection { get; set; }
    public decimal Outstanding => Sales - Collection;
}

public class DailySalesReportViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public string? UserName { get; set; }
    public int? CustomerId { get; set; }
    public List<DailySalesRowViewModel> Rows { get; set; } = new();

    public int TotalBills => Rows.Sum(r => r.Bills);
    public decimal TotalSales => Rows.Sum(r => r.Sales);
    public decimal TotalCash => Rows.Sum(r => r.Cash);
    public decimal TotalCredit => Rows.Sum(r => r.Credit);
    public decimal TotalPartial => Rows.Sum(r => r.Partial);
    public decimal TotalCollection => Rows.Sum(r => r.Collection);
    public decimal TotalOutstanding => Rows.Sum(r => r.Outstanding);
}

// ---- Stock Status Report ----

public class StockStatusRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Color { get; set; } = default!;
    public string Gauge { get; set; } = default!;
    public PricingMode Mode { get; set; }
    /// <summary>PVC section products are tracked (and billed) by piece count, not
    /// feet, so <see cref="PieceQty"/> is shown instead of <see cref="CurrentStock"/>.</summary>
    public bool IsPvc { get; set; }
    public decimal CurrentStock { get; set; }
    /// <summary>Total pieces in stock across all lengths (sum of StockPiece.Quantity).</summary>
    public int PieceQty { get; set; }
    /// <summary>The value actually shown/filtered on: piece qty for PVC, CurrentStock otherwise.</summary>
    public decimal StockValue => IsPvc ? PieceQty : CurrentStock;
    public string ModeLabel => IsPvc ? "PVC" : Mode == PricingMode.PerFoot ? "Per Foot" : "Per Unit";
    public string StockLabel => IsPvc ? $"{PieceQty:N0} qty" : Mode == PricingMode.PerFoot ? $"{CurrentStock:N2} ft" : $"{CurrentStock:N0} units";
}

public class StockStatusReportViewModel
{
    public string? Search { get; set; }
    public bool OnlyInStock { get; set; }
    public List<StockStatusRowViewModel> Rows { get; set; } = new();
    public int TotalProducts => Rows.Count;
    public int InStockCount => Rows.Count(r => r.StockValue != 0);
    public int OutOfStockCount => Rows.Count(r => r.StockValue == 0);
}
