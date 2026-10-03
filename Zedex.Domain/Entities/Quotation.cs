using Zedex.Domain.Common;

namespace Zedex.Domain.Entities;

/// <summary>
/// Price quotation sent to a prospective client (PDF / image). Completely separate from
/// billing: the client is free text (not a <see cref="Customer"/>), and nothing here
/// touches stock or the ledger.
/// </summary>
public class Quotation : BaseEntity
{
    /// <summary>Format: QT-yyyyMMdd-#### (per-day sequence of the day it was created). Never edited.</summary>
    public string QuotationNumber { get; set; } = default!;
    public DateTime QuotationDate { get; set; }

    public string ClientName { get; set; } = default!;
    public string? ContactNumber { get; set; }
    public string? ProjectAddress { get; set; }

    /// <summary>= sum of item amounts.</summary>
    public decimal GrandTotal { get; set; }

    public ICollection<QuotationItem> Items { get; set; } = new List<QuotationItem>();
    public ICollection<QuotationTerm> Terms { get; set; } = new List<QuotationTerm>();
}

/// <summary>One line of the "Scope and Specification" table.</summary>
public class QuotationItem : BaseEntity
{
    public int QuotationId { get; set; }
    public Quotation Quotation { get; set; } = default!;
    public int SortOrder { get; set; }
    /// <summary>May contain line breaks.</summary>
    public string Description { get; set; } = default!;
    public string? Specification { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    /// <summary>= Quantity × Rate, rounded to 2 decimals.</summary>
    public decimal Amount { get; set; }
}

/// <summary>A terms-and-conditions line printed on one quotation (copied from the defaults, then editable).</summary>
public class QuotationTerm : BaseEntity
{
    public int QuotationId { get; set; }
    public Quotation Quotation { get; set; } = default!;
    public int SortOrder { get; set; }
    public string Text { get; set; } = default!;
}

/// <summary>Admin-managed default terms (Settings); pre-filled on every new quotation.</summary>
public class QuotationDefaultTerm : BaseEntity
{
    public int SortOrder { get; set; }
    public string Text { get; set; } = default!;
}
