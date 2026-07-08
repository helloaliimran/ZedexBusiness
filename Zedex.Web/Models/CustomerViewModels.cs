using System.ComponentModel.DataAnnotations;
using Zedex.Application.Common;

namespace Zedex.Web.Models;

public class CustomerFormViewModel
{
    public int Id { get; set; }
    public string? ExistingImagePath { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Customer name")]
    public string Name { get; set; } = default!;

    [StringLength(30)]
    [Phone]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(1000)]
    [Display(Name = "Description / Remarks")]
    public string? Remarks { get; set; }

    [Display(Name = "Opening balance (Rs.)")]
    [Range(-99999999, 99999999)]
    public decimal? OpeningBalance { get; set; }

    [Display(Name = "Profile image (optional)")]
    public IFormFile? Image { get; set; }
}

public class CustomerListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? ImagePath { get; set; }
    public decimal Balance { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class CustomerListViewModel
{
    public string? Search { get; set; }
    public PagedResult<CustomerListItemViewModel> Items { get; set; } = new();
}

public class CustomerDetailsViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Remarks { get; set; }
    public string? ImagePath { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal Balance { get; set; }
    public int InvoiceCount { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
}
