namespace Zedex.Api.DTOs.Customers;

/// <summary>Customer row shown in the Customers list screen.</summary>
public class CustomerSummaryDto
{
    public int     CustomerId      { get; set; }
    public string  Name            { get; set; } = default!;
    public string? Phone           { get; set; }
    public string? Address         { get; set; }
    public decimal OpeningBalance  { get; set; }

    /// <summary>
    /// OpeningBalance + Σ(Debit) − Σ(Credit).
    /// Positive  → customer owes money (show in red on mobile).
    /// Zero/neg  → customer has credit or is settled (show in green).
    /// </summary>
    public decimal ClosingBalance  { get; set; }
}
