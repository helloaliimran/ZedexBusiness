namespace Zedex.Api.DTOs.Products;

/// <summary>A single matched product returned from a batch search row.</summary>
public class ProductSearchResultDto
{
    public int     ProductId { get; set; }
    public string  Name      { get; set; } = default!;
    public string  Color     { get; set; } = default!;
    public string  Gauge     { get; set; } = default!;
    public string  Category  { get; set; } = default!;
    /// <summary>Null for standard products — only PVC products carry a Company.</summary>
    public string? Company   { get; set; }
    public decimal Rate      { get; set; }
}
