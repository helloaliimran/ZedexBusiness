using Zedex.Domain.Common;

namespace Zedex.Domain.Entities;

/// <summary>
/// Per-length inventory for PerFoot products (e.g. aluminum: 10 pieces of 18 ft).
/// Cutting a sale from a piece decrements this row and increments/creates the
/// remainder-length row (18 ft piece, 10 ft sold → +1 piece of 8 ft).
/// </summary>
public class StockPiece : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public decimal LengthFt { get; set; }
    public int Quantity { get; set; }
}

/// <summary>Bulk stock entry header. Draft until posted; posting applies the
/// quantities to product stock and locks the entry against further edits.</summary>
public class StockHeader : BaseEntity
{
    public DateTime EntryDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? AttachmentPath { get; set; }
    public string? Remarks { get; set; }

    public bool IsPosted { get; set; }
    public string? PostedBy { get; set; }
    public DateTime? PostedDate { get; set; }

    public ICollection<StockDetail> Details { get; set; } = new List<StockDetail>();
}

/// <summary>One product line inside a stock entry.</summary>
public class StockDetail : BaseEntity
{
    public int StockHeaderId { get; set; }
    public StockHeader StockHeader { get; set; } = default!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;

    /// <summary>Loose units entered directly (nullable when carton-based).</summary>
    public int? Quantity { get; set; }
    public int? Cartons { get; set; }
    public int? ItemsPerCarton { get; set; }

    /// <summary>For PerFoot products: length of each piece in this line.</summary>
    public decimal? LengthFt { get; set; }

    /// <summary>Auto-computed: Quantity + (Cartons × ItemsPerCarton). Pieces or units.</summary>
    public decimal TotalQuantity { get; set; }
}
