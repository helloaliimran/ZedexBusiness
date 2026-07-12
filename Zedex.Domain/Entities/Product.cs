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

    /// <summary>Interpreted per <see cref="PricingMode"/> (standard products: per unit
    /// or per foot) or per <see cref="SaleType"/> (PVC products: Rs./ft or Rs./kg).</summary>
    public decimal Price { get; set; }
    public PricingMode PricingMode { get; set; } = PricingMode.PerUnit;
    public string? Description { get; set; }

    // ---- PVC-only fields (null for standard products) ----

    /// <summary>PVC: manufacturer/brand. Part of the billing display name.</summary>
    public int? CompanyId { get; set; }
    public Company? Company { get; set; }
    /// <summary>PVC: default gas kit option; auto-fills the bill line (overridable).</summary>
    public GasKitType? GasKitType { get; set; }
    /// <summary>PVC: how <see cref="Price"/> is interpreted when billing.</summary>
    public PvcSaleType? SaleType { get; set; }
    /// <summary>PVC, WeightPerLength sales only: default weight (kg) per full-length
    /// piece; auto-fills the bill line and is editable there.</summary>
    public decimal? WeightPerLength { get; set; }

    /// <summary>
    /// Cached total stock. PerUnit products: units. PerFoot products: total feet
    /// (the per-length piece breakdown lives in <see cref="StockPiece"/>).
    /// May go negative (overselling allowed by business rule).
    /// </summary>
    public decimal CurrentStock { get; set; }

    public ICollection<StockPiece> StockPieces { get; set; } = new List<StockPiece>();
}
