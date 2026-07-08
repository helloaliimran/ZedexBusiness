using Zedex.Domain.Common;
using Zedex.Domain.Enums;

namespace Zedex.Domain.Entities;

public class Product : BaseEntity
{
    public string Name { get; set; } = default!;
    public int CategoryId { get; set; }
    public Category Category { get; set; } = default!;
    public int ColorId { get; set; }
    public Color Color { get; set; } = default!;
    public int GaugeId { get; set; }
    public Gauge Gauge { get; set; } = default!;

    /// <summary>Interpreted per <see cref="PricingMode"/>: price per unit or per foot.</summary>
    public decimal Price { get; set; }
    public PricingMode PricingMode { get; set; } = PricingMode.PerUnit;
    public string? Description { get; set; }

    /// <summary>
    /// Cached total stock. PerUnit products: units. PerFoot products: total feet
    /// (the per-length piece breakdown lives in <see cref="StockPiece"/>).
    /// May go negative (overselling allowed by business rule).
    /// </summary>
    public decimal CurrentStock { get; set; }

    public ICollection<StockPiece> StockPieces { get; set; } = new List<StockPiece>();
}
