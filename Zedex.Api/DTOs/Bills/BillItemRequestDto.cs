namespace Zedex.Api.DTOs.Bills;

/// <summary>
/// One line of a bill being created or edited. Color/Gauge/Category/Company are NOT
/// separate fields here — they're intrinsic to whichever ProductId you send (every
/// product has exactly one color, gauge and category, and PVC products optionally one
/// company). Use POST /api/products/search to resolve the right ProductId from those
/// attributes first, then reference it here.
/// </summary>
public class BillItemRequestDto
{
    public int ProductId { get; set; }

    public decimal Quantity { get; set; }

    /// <summary>
    /// Standard PerFoot products: length (ft) sold per piece.
    /// PVC products: the stocked piece length (ft) — required, must match a stocked length.
    /// Ignored for Standard PerUnit products.
    /// </summary>
    public decimal? SizeFt { get; set; }

    /// <summary>Line discount percentage (0–100). Defaults to 0.</summary>
    public decimal? DiscountPercent { get; set; }

}
