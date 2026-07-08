using System.ComponentModel.DataAnnotations;
using Zedex.Application.Common;
using Zedex.Domain.Entities;

namespace Zedex.Web.Models;

public class UserListItemViewModel
{
    public string Id { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Role { get; set; } = default!;
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class UserListViewModel
{
    public string? Search { get; set; }
    public PagedResult<UserListItemViewModel> Items { get; set; } = new();
}

public class UserFormViewModel
{
    public string? Id { get; set; }
    public bool IsEdit => Id is not null;

    [Required]
    [StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = default!;

    [Required]
    [StringLength(50)]
    [Display(Name = "Username")]
    public string UserName { get; set; } = default!;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = default!;

    /// <summary>Required on create only (enforced in controller). Ignored on edit.</summary>
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Required]
    public string Role { get; set; } = "Worker";

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    // ---- Module permissions (Workers only; Admins bypass) ----
    [Display(Name = "Dashboard")] public bool PermDashboard { get; set; }
    [Display(Name = "Product Management")] public bool PermProducts { get; set; }
    [Display(Name = "Stock Management")] public bool PermStock { get; set; }
    [Display(Name = "Billing")] public bool PermBilling { get; set; }
    [Display(Name = "Customer Management")] public bool PermCustomers { get; set; }
    [Display(Name = "Customer Ledger Access")] public bool PermCustomerLedger { get; set; }
    [Display(Name = "Reports")] public bool PermReports { get; set; }

    public void ApplyTo(UserPermission permission)
    {
        permission.Dashboard = PermDashboard;
        permission.Products = PermProducts;
        permission.Stock = PermStock;
        permission.Billing = PermBilling;
        permission.Customers = PermCustomers;
        permission.CustomerLedger = PermCustomerLedger;
        permission.Reports = PermReports;
    }

    public void LoadFrom(UserPermission permission)
    {
        PermDashboard = permission.Dashboard;
        PermProducts = permission.Products;
        PermStock = permission.Stock;
        PermBilling = permission.Billing;
        PermCustomers = permission.Customers;
        PermCustomerLedger = permission.CustomerLedger;
        PermReports = permission.Reports;
    }
}

public class ResetPasswordViewModel
{
    public string Id { get; set; } = default!;
    public string UserName { get; set; } = default!;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "New password")]
    public string NewPassword { get; set; } = default!;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = default!;
}
