using System.ComponentModel.DataAnnotations;
using Zedex.Application.Common;
using Zedex.Domain.Enums;

namespace Zedex.Web.Models;

public class ExpenseFormViewModel
{
    public int Id { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "Select a category.")]
    [Display(Name = "Category")]
    public int? ExpenseCategoryId { get; set; }

    [Required]
    [Range(0.01, 99999999, ErrorMessage = "Enter a valid amount.")]
    [Display(Name = "Amount (Rs.)")]
    public decimal? Amount { get; set; }

    [Required]
    [Display(Name = "Paid from")]
    public PaymentSource PaymentSource { get; set; } = PaymentSource.Cash;

    [Required]
    [StringLength(500)]
    [Display(Name = "Description / details")]
    public string Description { get; set; } = default!;

    [Display(Name = "Receipt (optional)")]
    public IFormFile? Attachment { get; set; }

    public bool HasExistingAttachment { get; set; }
}

public class ExpenseRowViewModel
{
    public int Id { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string Category { get; set; } = default!;
    public decimal Amount { get; set; }
    public PaymentSource PaymentSource { get; set; }
    public string Description { get; set; } = default!;
    public bool HasAttachment { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class ExpenseCategoryTotalViewModel
{
    public string Category { get; set; } = default!;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class ExpenseListViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public int? CategoryId { get; set; }
    public PaymentSource? Source { get; set; }
    public string? Search { get; set; }

    public PagedResult<ExpenseRowViewModel> Items { get; set; } = new();

    // Totals over the whole filtered range (not just the current page).
    public decimal TotalAmount { get; set; }
    public decimal CashTotal { get; set; }
    public decimal OnlineTotal { get; set; }
    public int TotalCount { get; set; }
    public List<ExpenseCategoryTotalViewModel> ByCategory { get; set; } = new();

    /// <summary>Quick-add form shown above the list.</summary>
    public ExpenseFormViewModel NewExpense { get; set; } = new();
}
