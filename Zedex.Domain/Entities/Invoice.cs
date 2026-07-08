using Zedex.Domain.Common;
using Zedex.Domain.Enums;

namespace Zedex.Domain.Entities;

public class Invoice : BaseEntity
{
    /// <summary>Format: INV-yyyyMMdd-#### (per-day sequence).</summary>
    public string InvoiceNumber { get; set; } = default!;
    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = default!;
    public DateTime InvoiceDate { get; set; }

    public decimal SubTotal { get; set; }
    /// <summary>Sum of line discounts (Rs.).</summary>
    public decimal Discount { get; set; }
    /// <summary>Flat additional discount (Rs.) subtracted from the bill total.</summary>
    public decimal FurtherDiscount { get; set; }
    /// <summary>= SubTotal − Discount − FurtherDiscount.</summary>
    public decimal Total { get; set; }
    /// <summary>Amount received at posting (Cash: = Total, Partial: entered, Credit: 0).</summary>
    public decimal PaidAmount { get; set; }
    /// <summary>Null until the invoice is posted.</summary>
    public PaymentType? PaymentType { get; set; }
    public string? Remarks { get; set; }

    /// <summary>Draft until posted; posting deducts stock, writes ledger entries, and locks the invoice.</summary>
    public bool IsPosted { get; set; }
    public string? PostedBy { get; set; }
    public DateTime? PostedDate { get; set; }

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
    public ICollection<SaleReturn> Returns { get; set; } = new List<SaleReturn>();
}

public class InvoiceItem : BaseEntity
{
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = default!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public decimal Quantity { get; set; }

    /// <summary>PerFoot only: size (ft) sold per piece — entered on the line.</summary>
    public decimal? SizeFt { get; set; }
    /// <summary>PerFoot only: Quantity × SizeFt.</summary>
    public decimal? TotalFeet { get; set; }
    /// <summary>PerFoot only: stock piece length the sale is cut from (e.g. 18 ft piece,
    /// 10 ft sold → remainder 8 ft goes back to StockPiece).</summary>
    public decimal? CutFromLengthFt { get; set; }

    public decimal Rate { get; set; }
    /// <summary>Line discount percentage entered by the user (0–100).</summary>
    public decimal DiscountPercent { get; set; }
    /// <summary>Computed line discount in Rs. (= gross × DiscountPercent / 100).</summary>
    public decimal Discount { get; set; }
    /// <summary>Net amount: (PerFoot: TotalFeet × Rate; PerUnit: Quantity × Rate) − Discount.</summary>
    public decimal LineTotal { get; set; }

    /// <summary>Quantity already returned against this line.</summary>
    public decimal ReturnedQuantity { get; set; }
}
