using System.ComponentModel.DataAnnotations;
using Zedex.Application.Common;
using Zedex.Domain.Enums;

namespace Zedex.Web.Models;

public class InvoiceItemFormViewModel
{
    // Numeric fields are nullable so empty inputs bind cleanly (normalized in the controller).
    public int ProductId { get; set; }
    public int? Quantity { get; set; }
    /// <summary>PerFoot only: size (ft) sold per piece.</summary>
    public decimal? SizeFt { get; set; }
    /// <summary>PerFoot only: stock piece length the sale is cut from.</summary>
    public decimal? CutFromLengthFt { get; set; }
    public decimal? Rate { get; set; }
    /// <summary>Line discount percentage (0–100). UI helper — the server derives it from LineTotal.</summary>
    [Range(0, 100)]
    public decimal? DiscountPercent { get; set; }
    /// <summary>Net line total as shown/edited by the user (may be rounded, e.g. 255 → 250).
    /// Authoritative: discount amount and percentage are derived from it.</summary>
    public decimal? LineTotal { get; set; }
}

public class InvoiceFormViewModel
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

    public List<InvoiceItemFormViewModel> Items { get; set; } = new();
}

public class InvoiceListItemViewModel
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = default!;
    public string Customer { get; set; } = default!;
    public DateTime InvoiceDate { get; set; }
    public decimal Total { get; set; }
    public decimal PaidAmount { get; set; }
    public PaymentType? PaymentType { get; set; }
    public bool IsPosted { get; set; }
    public string? CreatedBy { get; set; }

    public decimal Outstanding => Total - PaidAmount;
}

public class InvoiceListViewModel
{
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public PaymentType? Type { get; set; }
    public bool? Posted { get; set; }
    public PagedResult<InvoiceListItemViewModel> Items { get; set; } = new();
}

public class InvoiceRowViewModel
{
    public string Product { get; set; } = default!;
    public PricingMode Mode { get; set; }
    public int Quantity { get; set; }
    public decimal? SizeFt { get; set; }
    public decimal? TotalFeet { get; set; }
    public decimal? CutFromLengthFt { get; set; }
    public decimal Rate { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal Discount { get; set; }
    public decimal LineTotal { get; set; }
    public int ReturnedQuantity { get; set; }
}

public class InvoiceReturnSummaryViewModel
{
    public int Id { get; set; }
    public string Number { get; set; } = default!;
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
}

public class InvoiceDetailsViewModel
{
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
    public List<InvoiceRowViewModel> Rows { get; set; } = new();
    public List<InvoiceReturnSummaryViewModel> Returns { get; set; } = new();

    public bool HasReturnable => IsPosted && Rows.Any(r => r.Quantity - r.ReturnedQuantity > 0);
    public decimal Outstanding => Total - PaidAmount;
    public string PaymentLabel => !IsPosted ? "—" : PaymentType switch
    {
        Domain.Enums.PaymentType.Cash => "Cash (Paid)",
        Domain.Enums.PaymentType.Partial => "Partial",
        Domain.Enums.PaymentType.Credit => "Credit",
        _ => "—"
    };
}

public class PostInvoiceViewModel
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Payment method")]
    public PaymentType PaymentType { get; set; } = PaymentType.Cash;

    [Display(Name = "Amount received (Rs.)")]
    [Range(0.01, 99999999, ErrorMessage = "Enter the amount received.")]
    public decimal? PaidAmount { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }
}
