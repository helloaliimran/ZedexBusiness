using System.ComponentModel.DataAnnotations;
using Zedex.Domain.Enums;

namespace Zedex.Web.Models;

public class ReturnLineViewModel
{
    public int InvoiceItemId { get; set; }
    /// <summary>Display only — not posted back, so it must be nullable to avoid an implicit [Required].</summary>
    public string? Product { get; set; }
    public PricingMode Mode { get; set; }
    public decimal? SizeFt { get; set; }
    public int Quantity { get; set; }
    public int Returned { get; set; }
    public decimal UnitNet { get; set; }
    public int Returnable => Quantity - Returned;

    /// <summary>User input: quantity to return now.</summary>
    [Range(0, int.MaxValue)]
    public int? ReturnQuantity { get; set; }
}

public class ReturnFormViewModel
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

    public List<ReturnLineViewModel> Lines { get; set; } = new();
}

public class ReturnRowViewModel
{
    public string Product { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal? SizeFt { get; set; }
    public decimal? TotalFeet { get; set; }
    public decimal Rate { get; set; }
    public decimal LineTotal { get; set; }
}

public class ReturnDetailsViewModel
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
    public List<ReturnRowViewModel> Rows { get; set; } = new();
}
