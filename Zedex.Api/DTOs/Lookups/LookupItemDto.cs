namespace Zedex.Api.DTOs.Lookups;

/// <summary>A single master-data option (color, gauge, category, or company).</summary>
public class LookupItemDto
{
    public int    Id   { get; set; }
    public string Name { get; set; } = default!;
}
