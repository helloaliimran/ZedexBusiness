using System.ComponentModel.DataAnnotations;
using Zedex.Application.Common;
using Zedex.Domain.Enums;

namespace Zedex.Web.Models;

public class PvcProductFormViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Section name")]
    public string Name { get; set; } = default!;

    [Display(Name = "Category")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a category.")]
    public int CategoryId { get; set; }

    [Display(Name = "Gauge")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a gauge.")]
    public int GaugeId { get; set; }

    [Display(Name = "Color")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a color.")]
    public int ColorId { get; set; }

    [Display(Name = "Company")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a company.")]
    public int CompanyId { get; set; }

    [Display(Name = "Gas kit")]
    public GasKitType GasKitType { get; set; } = GasKitType.None;

    [Display(Name = "Sale type")]
    public PvcSaleType SaleType { get; set; } = PvcSaleType.PerRunningFoot;

    /// <summary>Rs./ft (PerRunningFoot) or Rs./kg (WeightPerLength).</summary>
    [Display(Name = "Rate (Rs.)")]
    [Range(0.01, 99999999, ErrorMessage = "Enter a valid rate.")]
    public decimal Price { get; set; }

    /// <summary>Required when SaleType = WeightPerLength; validated in the controller.</summary>
    [Display(Name = "Weight per length (kg)")]
    [Range(0, 99999999, ErrorMessage = "Enter a valid weight.")]
    public decimal? WeightPerLength { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }
}

public class PvcProductListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Company { get; set; } = default!;
    public string Color { get; set; } = default!;
    public string Gauge { get; set; } = default!;
    public GasKitType GasKitType { get; set; }
    public PvcSaleType SaleType { get; set; }
    public decimal Price { get; set; }
    public decimal? WeightPerLength { get; set; }
    public decimal CurrentStock { get; set; }
    /// <summary>Total pieces in stock across all lengths (sum of StockPiece.Quantity) —
    /// what's actually deducted/restored when a PVC bill is posted/returned.</summary>
    public int StockQty { get; set; }

    public string SaleTypeLabel => SaleType == PvcSaleType.WeightPerLength ? "Weight / Length" : "Per Running Ft";
    public string RateLabel => SaleType == PvcSaleType.WeightPerLength
        ? $"Rs. {Price:N2} / kg"
        : $"Rs. {Price:N2} / ft";
    public string GasKitLabel => GasKitType switch
    {
        GasKitType.Single => "Single",
        GasKitType.Double => "Double",
        _ => "None"
    };
}

public class PvcProductListViewModel
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public int? CompanyId { get; set; }
    public int? ColorId { get; set; }
    public int? GaugeId { get; set; }
    public PvcSaleType? SaleType { get; set; }
    public PagedResult<PvcProductListItemViewModel> Items { get; set; } = new();
}

/// <summary>One row on the PVC quick-entry grid. Validated manually in the controller
/// (blank rows are simply skipped), so no data annotations here.</summary>
public class PvcProductQuickRowViewModel
{
    public string? Name { get; set; }
    public int CategoryId { get; set; }
    public int CompanyId { get; set; }
    public int ColorId { get; set; }
    public int GaugeId { get; set; }
    public PvcSaleType SaleType { get; set; } = PvcSaleType.PerRunningFoot;
    public GasKitType GasKitType { get; set; } = GasKitType.None;
    public decimal? Price { get; set; }
    public decimal? WeightPerLength { get; set; }
}

public class PvcProductQuickEntryViewModel
{
    public List<PvcProductQuickRowViewModel> Rows { get; set; } = new();
}
