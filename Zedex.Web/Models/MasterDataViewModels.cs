using System.ComponentModel.DataAnnotations;
using Zedex.Application.Common;

namespace Zedex.Web.Models;

public class MasterItemViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = default!;

    /// <summary>Category-only: marks this as the category the PVC module (products,
    /// invoices, returns) uses. Ignored for Color/Gauge/Company.</summary>
    public bool IsPvc { get; set; }

    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class MasterListViewModel
{
    public string EntityTitle { get; set; } = default!;
    public string EntityTitlePlural { get; set; } = default!;
    public string Icon { get; set; } = "bi-tags";
    public string? Search { get; set; }
    /// <summary>Shows the "Is PVC" checkbox/column — true only for the Categories screen.</summary>
    public bool ShowIsPvcOption { get; set; }
    public PagedResult<MasterItemViewModel> Items { get; set; } = new();
}
