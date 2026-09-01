namespace Zedex.Api.DTOs.Tools;

/// <summary>Compact result returned by the "create_or_update_bill" tool.</summary>
public class ToolBillResultDto
{
    public int      BillId        { get; set; }
    public string   InvoiceNumber { get; set; } = default!;
    public bool     WasCreated    { get; set; }
    public int      CustomerId    { get; set; }
    public string   CustomerName  { get; set; } = default!;
    public decimal  Total         { get; set; }
}
