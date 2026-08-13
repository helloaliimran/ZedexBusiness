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

/// <summary>
/// Audit trail row created whenever an admin force-resets a product's stock to zero
/// (single product, or as part of a "reset all" batch). CreatedBy/CreatedDate (from
/// BaseEntity) record who did it and when; this row is never edited or soft-deleted.
/// </summary>
public class StockResetLog : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;

    /// <summary>Product name at the time of reset, kept even if the product is later
    /// renamed or deleted.</summary>
    public string ProductName { get; set; } = default!;

    /// <summary>Stock quantity (units or feet) immediately before the reset.</summary>
    public decimal PreviousStock { get; set; }

    /// <summary>Per-length StockPiece rows cleared alongside the total — PerFoot/PVC
    /// products only, 0 for PerUnit products.</summary>
    public int PiecesCleared { get; set; }

    /// <summary>Groups every row from the same "Reset All" action; null for a
    /// single-product reset.</summary>
    public Guid? BatchId { get; set; }

    public string? Remarks { get; set; }
}
