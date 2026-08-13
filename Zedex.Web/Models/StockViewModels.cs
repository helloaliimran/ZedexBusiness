using System.ComponentModel.DataAnnotations;
using Zedex.Application.Common;
using Zedex.Domain.Enums;

namespace Zedex.Web.Models;

public class StockDetailFormViewModel
{
    public int ProductId { get; set; }
    /// <summary>Loose units/pieces entered directly.</summary>
    public int? Quantity { get; set; }
    public int? Cartons { get; set; }
    public int? ItemsPerCarton { get; set; }
    /// <summary>PerFoot products only: length of each piece in this line.</summary>
    public decimal? LengthFt { get; set; }

    public int TotalQuantity => (Quantity ?? 0) + (Cartons ?? 0) * (ItemsPerCarton ?? 0);
}

public class StockHeaderFormViewModel
{
    public int Id { get; set; }
    public string? ExistingAttachmentPath { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Entry date")]
    public DateTime EntryDate { get; set; } = DateTime.Today;

    [StringLength(50)]
    [Display(Name = "Reference number")]
    public string? ReferenceNumber { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    [Display(Name = "Attachment (optional)")]
    public IFormFile? Attachment { get; set; }

    public List<StockDetailFormViewModel> Details { get; set; } = new();
}

public class StockListItemViewModel
{
    public int Id { get; set; }
    public DateTime EntryDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Remarks { get; set; }
    public int LineCount { get; set; }
    public decimal TotalItems { get; set; }
    public bool HasAttachment { get; set; }
    public bool IsPosted { get; set; }
    public string? CreatedBy { get; set; }
}

public class StockListViewModel
{
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public PagedResult<StockListItemViewModel> Items { get; set; } = new();
}

public class StockDetailRowViewModel
{
    public string Product { get; set; } = default!;
    public PricingMode Mode { get; set; }
    public int? Quantity { get; set; }
    public int? Cartons { get; set; }
    public int? ItemsPerCarton { get; set; }
    public decimal? LengthFt { get; set; }
    public decimal TotalQuantity { get; set; }
    public decimal? TotalFeet => Mode == PricingMode.PerFoot && LengthFt is not null
        ? TotalQuantity * LengthFt
        : null;
}

public class StockDetailsViewModel
{
    public int Id { get; set; }
    public DateTime EntryDate { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Remarks { get; set; }
    public string? AttachmentPath { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public bool IsPosted { get; set; }
    public string? PostedBy { get; set; }
    public DateTime? PostedDate { get; set; }
    public List<StockDetailRowViewModel> Rows { get; set; } = new();
}

public class OnHandPieceViewModel
{
    public decimal LengthFt { get; set; }
    public int Quantity { get; set; }
}

public class OnHandItemViewModel
{
    public int ProductId { get; set; }
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Color { get; set; } = default!;
    public string Gauge { get; set; } = default!;
    public PricingMode Mode { get; set; }
    public decimal CurrentStock { get; set; }
    /// <summary>True for PVC section products — tracked (and billed) by piece count,
    /// not feet, so the summary column shows <see cref="StockQty"/> instead of
    /// <see cref="CurrentStock"/>.</summary>
    public bool IsPvc { get; set; }
    public List<OnHandPieceViewModel> Pieces { get; set; } = new();
    /// <summary>Total pieces across all lengths (sum of the piece breakdown).</summary>
    public int StockQty => Pieces.Sum(p => p.Quantity);
    /// <summary>The value actually shown/sorted on: piece qty for PVC, CurrentStock otherwise.</summary>
    public decimal StockValue => IsPvc ? StockQty : CurrentStock;
}

public class OnHandViewModel
{
    public string? Search { get; set; }
    public PagedResult<OnHandItemViewModel> Items { get; set; } = new();
}

public class StockResetLogItemViewModel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = default!;
    public decimal PreviousStock { get; set; }
    public int PiecesCleared { get; set; }
    public Guid? BatchId { get; set; }
    public string? Remarks { get; set; }
    public string? ResetBy { get; set; }
    public DateTime ResetDate { get; set; }
}

public class StockResetLogListViewModel
{
    public PagedResult<StockResetLogItemViewModel> Items { get; set; } = new();
}
