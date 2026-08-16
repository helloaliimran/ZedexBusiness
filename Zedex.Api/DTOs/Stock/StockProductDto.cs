namespace Zedex.Api.DTOs.Stock;

public class StockProductDto
{
    public int     ProductId    { get; set; }
    public string  Name         { get; set; } = default!;
    public string  Category     { get; set; } = default!;
    public int     CategoryId   { get; set; }
    public string  Color        { get; set; } = default!;
    public string  Gauge        { get; set; } = default!;
    public string  PricingMode  { get; set; } = default!;  // "PerUnit" | "PerFoot"
    public decimal CurrentStock { get; set; }
    public decimal Price        { get; set; }

    /// <summary>
    /// Piece-level breakdown — only present for PerFoot products.
    /// Null for PerUnit products (saves bandwidth).
    /// </summary>
    public List<StockPieceDto>? StockPieces { get; set; }
}

public class StockPieceDto
{
    public decimal LengthFt   { get; set; }
    public int     Quantity   { get; set; }
    /// <summary>LengthFt × Quantity — total feet held in this piece length.</summary>
    public decimal TotalFeet  => LengthFt * Quantity;
}
