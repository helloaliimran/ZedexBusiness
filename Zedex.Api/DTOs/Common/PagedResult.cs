namespace Zedex.Api.DTOs.Common;

/// <summary>Generic paginated response wrapper used by all list endpoints.</summary>
public class PagedResult<T>
{
    public List<T> Items      { get; set; } = new();
    public int     Page       { get; set; }
    public int     PageSize   { get; set; }
    public int     TotalCount { get; set; }
    public int     TotalPages    => (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool    HasNextPage   => Page < TotalPages;
    public bool    HasPrevPage   => Page > 1;
}
