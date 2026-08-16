namespace Zedex.Api.DTOs.Bills;

/// <summary>A single line on a Standard invoice.</summary>
public class StandardLineItemDto
{
    public int     ItemId           { get; set; }
    public int     ProductId        { get; set; }
    public string  ProductName      { get; set; } = default!;
    public string  PricingMode      { get; set; } = default!;   // "PerUnit" | "PerFoot"

    public decimal Quantity         { get; set; }

    // PerFoot only
    public decimal? SizeFt          { get; set; }
    public decimal? TotalFeet       { get; set; }
    public decimal? CutFromLengthFt { get; set; }

    public decimal  Rate            { get; set; }
    public decimal  DiscountPercent { get; set; }
    public decimal  Discount        { get; set; }
    public decimal  LineTotal       { get; set; }
    public decimal  ReturnedQty     { get; set; }
}
