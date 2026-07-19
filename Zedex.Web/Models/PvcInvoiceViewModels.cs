using System.ComponentModel.DataAnnotations;
using Zedex.Application.Common;
using Zedex.Domain.Enums;

namespace Zedex.Web.Models;

/// <summary>
/// One line on the PVC billing form. Numeric fields are nullable so empty
/// inputs bind cleanly (normalized in the controller).
/// Calculation (mirrors PvcInvoiceItem):
///   lengths gross = PerRunningFoot: Length × Qty × Rate; Weight: Wt/Len × Qty × Rate
///   gas kit       = setting rate × (Single 1 / Double 2) × Length × Qty
///   line total    = lengths net (after disc %) + gas kit — editable/authoritative.
/// </summary>
public class PvcInvoiceLineFormViewModel
{
    public int ProductId { get; set; }
    /// <summary>Length (ft) of each piece — PVC sells whole lengths only.</summary>
    public decimal? LengthFt { get; set; }
    public int? Quantity { get; set; }
    /// <summary>Weight-based products: kg per piece (defaults from product, editable).</summary>
    public decimal? WeightPerLength { get; set; }
    /// <summary>Rs./ft or Rs./kg depending on the product's sale type.</summary>
    public decimal? Rate { get; set; }
    [Range(0, 100)]
    public decimal? DiscountPercent { get; set; }
    public GasKitType GasKitType { get; set; } = GasKitType.None;
    /// <summary>Net line total incl. gas kit as shown/edited by the user (may be
    /// rounded). Authoritative: the lengths discount is derived from it.</summary>
    public decimal? LineTotal { get; set; }
}

public class PvcInvoiceFormViewModel
{
    public int Id { get; set; }

    [Display(Name = "Customer")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a customer.")]
    public int CustomerId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Invoice date")]
    public DateTime InvoiceDate { get; set; } = DateTime.Today;

    [StringLength(500)]
    public string? Remarks { get; set; }

    [Display(Name = "Further discount (Rs.)")]
    [Range(0, 99999999)]
    public decimal? FurtherDiscount { get; set; }

    public List<PvcInvoiceLineFormViewModel> Items { get; set; } = new();
}

/// <summary>One printed/displayed row of a PVC invoice.</summary>
public class PvcInvoiceRowViewModel
{
    /// <summary>Item name: section + gauge + company + color.</summary>
    public string Product { get; set; } = default!;
    /// <summary>Company name — printed as its own column.</summary>
    public string Company { get; set; } = "";
    /// <summary>Item (section + gauge + color) — printed as "Item".</summary>
    public string Item { get; set; } = "";
    /// <summary>Product description — printed as "Code".</summary>
    public string Code { get; set; } = "";
    public PvcSaleType SaleType { get; set; }
    public decimal LengthFt { get; set; }
    public int Quantity { get; set; }
    public decimal? WeightPerLength { get; set; }
    public decimal? TotalWeight { get; set; }
    public decimal? TotalFeet { get; set; }
    public decimal Rate { get; set; }
    public decimal LengthsAmount { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal Discount { get; set; }
    public GasKitType GasKitType { get; set; }
    public decimal GasKitRatePerFt { get; set; }
    public decimal GasKitAmount { get; set; }
    public decimal LineTotal { get; set; }
    public int ReturnedQuantity { get; set; }

    public string GasKitLabel => GasKitType switch
    {
        GasKitType.Single => "Single",
        GasKitType.Double => "Double",
        _ => "—"
    };
    /// <summary>Rate column caption: /ft or /kg.</summary>
    public string RateUnit => SaleType == PvcSaleType.WeightPerLength ? "/kg" : "/ft";
    /// <summary>Lengths amount after discount, before gas kit.</summary>
    public decimal AmountLengths => LengthsAmount - Discount;
}

public class PvcInvoiceDetailsViewModel
{
    /// <summary>Heading on prints — from the PvcPrintTitle admin setting.</summary>
    public string PrintTitle { get; set; } = "Zedex Business";
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = default!;
    public DateTime InvoiceDate { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    /// <summary>Customer's current ledger balance including everything (negative = advance held).</summary>
    public decimal CustomerBalance { get; set; }
    /// <summary>Net ledger effect of THIS invoice (bill − its payments/returns).</summary>
    public decimal InvoiceLedgerEffect { get; set; }
    /// <summary>Balance excluding this invoice — the "previous balance" shown on the bill.</summary>
    public decimal PreviousBalance => CustomerBalance - InvoiceLedgerEffect;
    /// <summary>Draft view: what the customer would owe in total (previous + this bill).</summary>
    public decimal GrandTotalWithPrevious => PreviousBalance + Total;
    public decimal SubTotal { get; set; }
    public decimal Discount { get; set; }
    public decimal FurtherDiscount { get; set; }
    public decimal Total { get; set; }
    public decimal PaidAmount { get; set; }
    public PaymentType? PaymentType { get; set; }
    public string? Remarks { get; set; }
    public bool IsPosted { get; set; }
    public string? PostedBy { get; set; }
    public DateTime? PostedDate { get; set; }
    public string? CreatedBy { get; set; }
    public List<PvcInvoiceRowViewModel> Rows { get; set; } = new();
    public List<InvoiceReturnSummaryViewModel> Returns { get; set; } = new();

    public bool HasReturnable => IsPosted && Rows.Any(r => r.Quantity - r.ReturnedQuantity > 0);

    /// <summary>Sum of gas kit amounts — shown as its own footer row.</summary>
    public decimal TotalGasKitAmount => Rows.Sum(r => r.GasKitAmount);
    public decimal TotalFeetSum => Rows.Sum(r => r.TotalFeet ?? 0);
    public decimal TotalWeightSum => Rows.Sum(r => r.TotalWeight ?? 0);
    public decimal Outstanding => Total - PaidAmount;
    public string PaymentLabel => !IsPosted ? "—" : PaymentType switch
    {
        Domain.Enums.PaymentType.Cash => "Cash (Paid)",
        Domain.Enums.PaymentType.Partial => "Partial",
        Domain.Enums.PaymentType.Credit => "Credit",
        _ => "—"
    };
}
