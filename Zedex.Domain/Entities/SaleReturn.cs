using Zedex.Domain.Common;

namespace Zedex.Domain.Entities;

/// <summary>Return against a posted invoice: restores stock and credits the ledger.</summary>
public class SaleReturn : BaseEntity
{
    /// <summary>Format: RET-yyyyMMdd-####.</summary>
    public string ReturnNumber { get; set; } = default!;
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = default!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;
    public DateTime ReturnDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Remarks { get; set; }

    public ICollection<SaleReturnItem> Items { get; set; } = new List<SaleReturnItem>();
}

public class SaleReturnItem : BaseEntity
{
    public int SaleReturnId { get; set; }
    public SaleReturn SaleReturn { get; set; } = default!;
    public int InvoiceItemId { get; set; }
    public InvoiceItem InvoiceItem { get; set; } = default!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public decimal Quantity { get; set; }
    /// <summary>PerFoot only: size (ft) of each returned piece.</summary>
    public decimal? SizeFt { get; set; }
    public decimal? TotalFeet { get; set; }
    public decimal Rate { get; set; }
    public decimal LineTotal { get; set; }
}
