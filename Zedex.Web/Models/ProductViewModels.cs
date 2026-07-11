using System.ComponentModel.DataAnnotations;
using Zedex.Application.Common;
using Zedex.Domain.Enums;

namespace Zedex.Web.Models;

public class ProductFormViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Product name")]
    public string Name { get; set; } = default!;

    [Display(Name = "Category")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a category.")]
    public int CategoryId { get; set; }

    [Display(Name = "Color")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a color.")]
    public int ColorId { get; set; }

    [Display(Name = "Gauge")]
    [Range(1, int.MaxValue, ErrorMessage = "Please select a gauge.")]
    public int GaugeId { get; set; }

    [Display(Name = "Pricing mode")]
    public PricingMode PricingMode { get; set; } = PricingMode.PerUnit;

    [Display(Name = "Price (Rs.)")]
    [Range(0.01, 99999999, ErrorMessage = "Enter a valid price.")]
    public decimal Price { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }
}

public class ProductListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Color { get; set; } = default!;
    public string Gauge { get; set; } = default!;
    public PricingMode PricingMode { get; set; }
    public decimal Price { get; set; }
    public decimal CurrentStock { get; set; }

    public string PricingLabel => PricingMode == PricingMode.PerFoot ? "Per Foot" : "Per Unit";
    public string StockLabel => PricingMode == PricingMode.PerFoot
        ? $"{CurrentStock:N2} ft"
        : $"{CurrentStock:N0} units";
}

public class ProductListViewModel
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public PricingMode? Mode { get; set; }
    public PagedResult<ProductListItemViewModel> Items { get; set; } = new();
}

/// <summary>One row on the quick-entry grid. Validated manually in the controller
/// (blank rows are simply skipped), so no data annotations here.</summary>
public class ProductQuickRowViewModel
{
    public string? Name { get; set; }
    public int CategoryId { get; set; }
    public int ColorId { get; set; }
    public int GaugeId { get; set; }
    public PricingMode PricingMode { get; set; } = PricingMode.PerUnit;
    public decimal? Price { get; set; }
}

public class ProductQuickEntryViewModel
{
    public List<ProductQuickRowViewModel> Rows { get; set; } = new();
}

/// <summary>One product on the bulk rate-update screen.</summary>
public class ProductRateItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Color { get; set; } = default!;
    public string Gauge { get; set; } = default!;
    public PricingMode PricingMode { get; set; }
    public decimal Price { get; set; }

    public string PricingLabel => PricingMode == PricingMode.PerFoot ? "Per Foot" : "Per Unit";
}

/// <summary>Posted back from the rate-update screen — only changed rows are sent.</summary>
public class ProductRateUpdateViewModel
{
    public int Id { get; set; }
    public decimal Price { get; set; }
}
