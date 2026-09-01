namespace Zedex.Api.DTOs.Tools;

/// <summary>A single line on a bill, as returned by the "get_bill" tool. Carries BillItemId
/// so the caller can round-trip it into a BillItemUpdateDto for create_or_update_bill.</summary>
public class ToolBillItemDto
{
    public int     BillItemId      { get; set; }
    public int     ProductId       { get; set; }
    public string  ProductName     { get; set; } = default!;
    public decimal Quantity        { get; set; }
    public decimal? SizeFt         { get; set; }
    public decimal  Rate           { get; set; }
    public decimal  DiscountPercent { get; set; }
    public decimal  LineTotal      { get; set; }
}
