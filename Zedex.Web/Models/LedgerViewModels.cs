using System.ComponentModel.DataAnnotations;
using Zedex.Application.Common;
using Zedex.Domain.Enums;

namespace Zedex.Web.Models;

public class LedgerCustomerItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Phone { get; set; }
    public decimal TotalBills { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal Balance { get; set; }
}

public class LedgerIndexViewModel
{
    public string? Search { get; set; }
    public PagedResult<LedgerCustomerItemViewModel> Items { get; set; } = new();
}

public class LedgerRowViewModel
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public LedgerEntryType Type { get; set; }
    public string? Remarks { get; set; }
    public int? InvoiceId { get; set; }
    public int? SaleReturnId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
    public string? CreatedBy { get; set; }
    /// <summary>Manual entries (no invoice/return link) can be deleted by admins.</summary>
    public bool IsManual => InvoiceId is null && SaleReturnId is null;
}

public class LedgerViewModel
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = default!;
    public string? Phone { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    /// <summary>Customer opening balance plus all activity before the From date.</summary>
    public decimal OpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<LedgerRowViewModel> Rows { get; set; } = new();
}

public class LedgerEntryFormViewModel
{
    public int CustomerId { get; set; }

    /// <summary>Payment (credit), Credit adjustment, or Debit adjustment.</summary>
    [Required]
    [Display(Name = "Entry type")]
    public LedgerEntryType EntryType { get; set; } = LedgerEntryType.Payment;

    [Required]
    [Range(0.01, 99999999, ErrorMessage = "Enter a valid amount.")]
    [Display(Name = "Amount (Rs.)")]
    public decimal? Amount { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Date")]
    public DateTime EntryDate { get; set; } = DateTime.Today;

    [Required]
    [StringLength(500)]
    public string Remarks { get; set; } = default!;
}
