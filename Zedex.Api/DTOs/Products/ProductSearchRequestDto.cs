using System.ComponentModel.DataAnnotations;

namespace Zedex.Api.DTOs.Products;

/// <summary>
/// One row of a batch product search. ProductName is required; Color/Gauge/Category/Company
/// are optional refinements and may differ from row to row. Every field is matched with a
/// case-insensitive "contains" (LIKE) search on both sides — a ProductName of "DC26" matches
/// a stored product named "DC26C" because both the input and the stored value are lower-cased
/// before comparing.
/// </summary>
public class ProductSearchRequestDto
{
    [Required(ErrorMessage = "Product name is required.")]
    [MinLength(1, ErrorMessage = "Product name is required.")]
    public string ProductName { get; set; } = default!;

    /// <summary>Optional. Matches Color.Name (contains, case-insensitive).</summary>
    public string? Color { get; set; }

    /// <summary>Optional. Matches Gauge.Name (contains, case-insensitive).</summary>
    public string? Gauge { get; set; }

    /// <summary>Optional. Matches Category.Name (contains, case-insensitive).</summary>
    public string? Category { get; set; }

    /// <summary>Optional. Matches Company.Name (contains, case-insensitive). Only PVC products have a Company.</summary>
    public string? Company { get; set; }
}
