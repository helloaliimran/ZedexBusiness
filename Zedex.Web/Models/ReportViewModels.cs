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
