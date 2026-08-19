namespace Zedex.Api.DTOs.Products;

/// <summary>
/// Matches for one input search row. The response is a list of these — one per row of the
/// request, in the same order the rows were submitted — since each row's filters (and
/// therefore its results) can differ from the others.
/// </summary>
public class ProductSearchGroupDto
{
    /// <summary>0-based position of this row in the request list.</summary>
    public int Index { get; set; }

    /// <summary>Echoes the ProductName that was searched for in this row.</summary>
    public string ProductName { get; set; } = default!;

    public List<ProductSearchResultDto> Matches { get; set; } = new();
}
