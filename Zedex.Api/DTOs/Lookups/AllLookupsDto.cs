namespace Zedex.Api.DTOs.Lookups;

/// <summary>All product master-data lists in one payload, each sorted by Name.</summary>
public class AllLookupsDto
{
    public List<LookupItemDto> Colors     { get; set; } = new();
    public List<LookupItemDto> Gauges     { get; set; } = new();
    public List<LookupItemDto> Categories { get; set; } = new();
    public List<LookupItemDto> Companies  { get; set; } = new();
}
