namespace Zedex.Api.DTOs.Customers;

/// <summary>Full ledger response for a customer — header + paginated entries.</summary>
public class LedgerResponseDto
{
    // ── Customer header ───────────────────────────────────────────────────────
    public int     CustomerId      { get; set; }
    public string  CustomerName    { get; set; } = default!;
    public string? CustomerPhone   { get; set; }
    public decimal OpeningBalance  { get; set; }

    /// <summary>Current balance (positive = owes money). Pinned at top of ledger screen.</summary>
    public decimal ClosingBalance  { get; set; }

    // ── Paged entries (newest first, each carrying its running balance) ────────
    public List<LedgerEntryDto> Entries      { get; set; } = new();
    public int                  TotalEntries { get; set; }
    public int                  Page         { get; set; }
    public int                  PageSize     { get; set; }
    public int                  TotalPages   => (int)Math.Ceiling((double)TotalEntries / PageSize);
    public bool                 HasNextPage  => Page < TotalPages;
    public bool                 HasPrevPage  => Page > 1;
}

/// <summary>Single ledger row with running balance at that point in time.</summary>
public class LedgerEntryDto
{
    public int      EntryId        { get; set; }
    public DateTime EntryDate      { get; set; }

    /// <summary>"Bill" | "Payment" | "Credit" | "Debit" | "Return"</summary>
    public string   Type           { get; set; } = default!;

    public decimal  Debit          { get; set; }
    public decimal  Credit         { get; set; }

    /// <summary>Cumulative balance AFTER this entry (OpeningBalance + all Debit − Credit up to here).</summary>
    public decimal  RunningBalance { get; set; }

    public string?  Remarks        { get; set; }

    /// <summary>
    /// Non-null for Bill and Return entries.
    /// Mobile app uses this to navigate → Bill Detail screen.
    /// </summary>
    public int?     InvoiceId      { get; set; }
    public string?  InvoiceNumber  { get; set; }
}
