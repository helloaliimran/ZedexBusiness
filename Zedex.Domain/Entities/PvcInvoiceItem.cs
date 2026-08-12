using Zedex.Domain.Common;
using Zedex.Domain.Enums;

namespace Zedex.Domain.Entities;

/// <summary>
/// Line on a PVC invoice (Invoice.InvoiceType == Pvc). PVC sections sell in full
/// lengths only (no cutting): posting deducts <c>Quantity</c> pieces of
/// <c>LengthFt</c> from StockPiece.
///
/// Calculation (all values snapshotted at entry time):
///   PerRunningFoot: TotalFeet   = LengthFt × Quantity; LengthsAmount = TotalFeet × Rate
///   WeightPerLength: TotalWeight = WeightPerLength × Quantity; LengthsAmount = TotalWeight × Rate
///   RatePerLength: TotalFeet    = LengthFt × Quantity (informational only); LengthsAmount = Quantity × Rate
///   GasKitAmount = GasKitRatePerFt × (Single: 1, Double: 2) × LengthFt × Quantity (default; editable)
///   Discount     = (LengthsAmount + GasKitAmount) × DiscountPercent / 100 — the discount
///                  applies to the combined total, gas kit included
///   LineTotal    = LengthsAmount + GasKitAmount − Discount
/// </summary>
public class PvcInvoiceItem : BaseEntity
{
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = default!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;

    /// <summary>Length (ft) of each piece sold; must match a stocked length.</summary>
    public decimal LengthFt { get; set; }
    /// <summary>Number of pieces.</summary>
    public decimal Quantity { get; set; }

    /// <summary>Snapshot of the product's sale type at billing time.</summary>
    public PvcSaleType SaleType { get; set; }
    /// <summary>Rs./ft (PerRunningFoot) or Rs./kg (WeightPerLength).</summary>
    public decimal Rate { get; set; }

    /// <summary>WeightPerLength only: kg per piece (defaults from product, editable).</summary>
    public decimal? WeightPerLength { get; set; }
    /// <summary>WeightPerLength only: WeightPerLength × Quantity.</summary>
    public decimal? TotalWeight { get; set; }
    /// <summary>PerRunningFoot only: LengthFt × Quantity.</summary>
    public decimal? TotalFeet { get; set; }

    /// <summary>Lengths amount before discount (gas kit excluded).</summary>
    public decimal LengthsAmount { get; set; }

    /// <summary>Line discount percentage (0–100), applied to LengthsAmount + GasKitAmount combined.</summary>
    public decimal DiscountPercent { get; set; }
    /// <summary>Computed discount in Rs. (= (LengthsAmount + GasKitAmount) × DiscountPercent / 100).</summary>
    public decimal Discount { get; set; }

    public GasKitType GasKitType { get; set; } = GasKitType.None;
    /// <summary>Snapshot of the admin gas kit rate (Rs./ft) at billing time.</summary>
    public decimal GasKitRatePerFt { get; set; }
    /// <summary>Defaults to GasKitRatePerFt × multiplier × LengthFt × Quantity but is
    /// directly editable on the bill line; before discount.</summary>
    public decimal GasKitAmount { get; set; }

    /// <summary>LengthsAmount + GasKitAmount − Discount.</summary>
    public decimal LineTotal { get; set; }

    /// <summary>Pieces already returned against this line.</summary>
    public decimal ReturnedQuantity { get; set; }
}
