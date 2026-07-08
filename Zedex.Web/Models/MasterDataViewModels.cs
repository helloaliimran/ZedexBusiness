using System.ComponentModel.DataAnnotations;
using Zedex.Application.Common;

namespace Zedex.Web.Models;

public class MasterItemViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = default!;

    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class MasterListViewModel
{
    public string EntityTitle { get; set; } = default!;
    public string EntityTitlePlural { get; set; } = default!;
    public string Icon { get; set; } = "bi-tags";
    public string? Search { get; set; }
    public PagedResult<MasterItemViewModel> Items { get; set; } = new();
}
