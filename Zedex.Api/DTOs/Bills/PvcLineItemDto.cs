namespace Zedex.Api.DTOs.Bills;

/// <summary>A single line on a PVC invoice.</summary>
public class PvcLineItemDto
{
    public int     ItemId           { get; set; }
    public int     ProductId        { get; set; }
    public string  ProductName      { get; set; } = default!;
    public string? CompanyName      { get; set; }

    public decimal LengthFt         { get; set; }
    public decimal Quantity          { get; set; }
    public string  SaleType          { get; set; } = default!;  // "PerRunningFoot" | "WeightPerLength" | "RatePerLength"
    public decimal Rate              { get; set; }

    // WeightPerLength only
    public decimal? WeightPerLength  { get; set; }
    public decimal? TotalWeight      { get; set; }

    // PerRunningFoot / RatePerLength
    public decimal? TotalFeet        { get; set; }

    public decimal  LengthsAmount    { get; set; }

    public string   GasKitType       { get; set; } = default!;  // "None" | "Single" | "Double"
    public decimal  GasKitAmount     { get; set; }

    public decimal  DiscountPercent  { get; set; }
    public decimal  Discount         { get; set; }
    public decimal  LineTotal        { get; set; }
    public decimal  ReturnedQty      { get; set; }
}
