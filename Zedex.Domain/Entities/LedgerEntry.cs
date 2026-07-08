using Zedex.Domain.Common;
using Zedex.Domain.Enums;

namespace Zedex.Domain.Entities;

/// <summary>
/// Customer ledger row. Convention: Debit increases what the customer owes
/// (bills, manual debits); Credit decreases it (payments, returns, manual credits).
/// Running balance = Customer.OpeningBalance + Σ(Debit − Credit) in date order.
/// </summary>
public class LedgerEntry : BaseEntity
{
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;
    public DateTime EntryDate { get; set; }
    public LedgerEntryType Type { get; set; }

    public int? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public int? SaleReturnId { get; set; }
    public SaleReturn? SaleReturn { get; set; }

    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Remarks { get; set; }
}
