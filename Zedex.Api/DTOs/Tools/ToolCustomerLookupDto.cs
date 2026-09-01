namespace Zedex.Api.DTOs.Tools;

/// <summary>Minimal customer match returned by the "find_customer" tool.</summary>
public class ToolCustomerLookupDto
{
    public int     CustomerId { get; set; }
    public string  Name       { get; set; } = default!;
    public string? Phone      { get; set; }
}
