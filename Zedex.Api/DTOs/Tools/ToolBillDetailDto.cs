namespace Zedex.Api.DTOs.Tools;

/// <summary>Full header + line items for a bill, as returned by the "get_bill" tool. Used
/// before an edit so the caller can see current lines (and their BillItemIds) and current
/// header fields. Standard (non-PVC) bills only, matching create_or_update_bill.</summary>
public class ToolBillDetailDto
{
    public int      BillId          { get; set; }
    public string   InvoiceNumber   { get; set; } = default!;
    public bool     IsPosted        { get; set; }
    public int      CustomerId      { get; set; }
    public string   CustomerName    { get; set; } = default!;
    public string?  Remarks         { get; set; }
    public decimal  SubTotal        { get; set; }
    public decimal  Discount        { get; set; }
    public decimal  FurtherDiscount { get; set; }
    public decimal  Total           { get; set; }
    public List<ToolBillItemDto> Items { get; set; } = new();
}
