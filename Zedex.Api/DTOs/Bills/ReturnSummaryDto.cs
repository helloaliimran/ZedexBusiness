namespace Zedex.Api.DTOs.Bills;

/// <summary>Sale return summary shown inside a Bill detail screen.</summary>
public class ReturnSummaryDto
{
    public int      ReturnId     { get; set; }
    public string   ReturnNumber { get; set; } = default!;
    public DateTime ReturnDate   { get; set; }
    public decimal  TotalAmount  { get; set; }
    public string?  Remarks      { get; set; }
}
