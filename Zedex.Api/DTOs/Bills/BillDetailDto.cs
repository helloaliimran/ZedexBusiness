namespace Zedex.Api.DTOs.Bills;

/// <summary>Full invoice detail including line items and returns.</summary>
public class BillDetailDto
{
    // ── Header ────────────────────────────────────────────────────────────────
    public int      InvoiceId       { get; set; }
    public string   InvoiceNumber   { get; set; } = default!;
    public string   InvoiceType     { get; set; } = default!;   // "Standard" | "Pvc"
    public DateTime InvoiceDate     { get; set; }
    public bool     IsPosted        { get; set; }
    public DateTime? PostedDate     { get; set; }
    public string?  Remarks         { get; set; }

    // ── Customer ──────────────────────────────────────────────────────────────
    public int      CustomerId      { get; set; }
    public string   CustomerName    { get; set; } = default!;
    public string?  CustomerPhone   { get; set; }
    public string?  CustomerAddress { get; set; }

    // ── Totals ────────────────────────────────────────────────────────────────
    public decimal  SubTotal        { get; set; }
    public decimal  Discount        { get; set; }
    public decimal  FurtherDiscount { get; set; }
    public decimal  Total           { get; set; }
    public decimal  PaidAmount      { get; set; }
    public decimal  Balance         => Total - PaidAmount;
    public string?  PaymentType     { get; set; }

    // ── Line items — only one list will have data based on InvoiceType ────────
    public List<StandardLineItemDto> Items    { get; set; } = new();
    public List<PvcLineItemDto>      PvcItems { get; set; } = new();

    // ── Returns ───────────────────────────────────────────────────────────────
    public List<ReturnSummaryDto> Returns       { get; set; } = new();
    public decimal                TotalReturned { get; set; }
}
