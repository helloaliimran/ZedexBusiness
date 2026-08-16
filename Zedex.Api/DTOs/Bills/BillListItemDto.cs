namespace Zedex.Api.DTOs.Bills;

/// <summary>Summary row shown in the Bills list screen.</summary>
public class BillListItemDto
{
    public int      InvoiceId       { get; set; }
    public string   InvoiceNumber   { get; set; } = default!;
    public string   InvoiceType     { get; set; } = default!;  // "Standard" | "Pvc"
    public int      CustomerId      { get; set; }
    public string   CustomerName    { get; set; } = default!;
    public DateTime InvoiceDate     { get; set; }
    public decimal  SubTotal        { get; set; }
    public decimal  Discount        { get; set; }
    public decimal  FurtherDiscount { get; set; }
    public decimal  Total           { get; set; }
    public decimal  PaidAmount      { get; set; }
    public decimal  Balance         => Total - PaidAmount;
    public string?  PaymentType     { get; set; }  // "Cash" | "Partial" | "Credit"
}
