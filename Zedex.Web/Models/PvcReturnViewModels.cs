using System.ComponentModel.DataAnnotations;
using Zedex.Domain.Enums;

namespace Zedex.Web.Models;

public class PvcReturnLineViewModel
{
    public int PvcInvoiceItemId { get; set; }
    /// <summary>Display only — not posted back, so it must be nullable to avoid an implicit [Required].</summary>
    public string? Product { get; set; }
    public PvcSaleType SaleType { get; set; }
    public decimal LengthFt { get; set; }
    public GasKitType GasKitType { get; set; }
    public int Quantity { get; set; }
    public int Returned { get; set; }
    /// <summary>Refund per piece: LineTotal / Quantity — includes the piece's
    /// discount share and its gas kit.</summary>
    public decimal UnitNet { get; set; }
    public int Returnable => Quantity - Returned;

    public string GasKitLabel => GasKitType switch
    {
        GasKitType.Single => "Single",
        GasKitType.Double => "Double",
        _ => "—"
    };

    /// <summary>User input: pieces to return now.</summary>
    [Range(0, int.MaxValue)]
    public int? ReturnQuantity { get; set; }
}

public class PvcReturnFormViewModel
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = default!;
    public string CustomerName { get; set; } = default!;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Return date")]
    public DateTime ReturnDate { get; set; } = DateTime.Today;

    [StringLength(500)]
    public string? Remarks { get; set; }

    public List<PvcReturnLineViewModel> Lines { get; set; } = new();
}

public class PvcReturnRowViewModel
{
    public string Product { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal LengthFt { get; set; }
    public decimal? TotalFeet { get; set; }
    public decimal Rate { get; set; }
    public decimal LineTotal { get; set; }
}

public class PvcReturnDetailsViewModel
{
    public int Id { get; set; }
    public string ReturnNumber { get; set; } = default!;
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = default!;
    public string CustomerName { get; set; } = default!;
    public DateTime ReturnDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Remarks { get; set; }
    public string? CreatedBy { get; set; }
    public List<PvcReturnRowViewModel> Rows { get; set; } = new();
}
