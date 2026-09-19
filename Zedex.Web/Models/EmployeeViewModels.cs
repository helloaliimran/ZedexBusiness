using System.ComponentModel.DataAnnotations;
using Zedex.Application.Common;
using Zedex.Domain.Enums;

namespace Zedex.Web.Models;

public class EmployeeFormViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    [Display(Name = "Employee name")]
    public string Name { get; set; } = default!;

    [StringLength(200)]
    [Display(Name = "Father name")]
    public string? FatherName { get; set; }

    [StringLength(30)]
    [Phone]
    public string? Phone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(15)]
    [Display(Name = "CNIC")]
    public string? Cnic { get; set; }

    [StringLength(100)]
    public string? Designation { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Joining date")]
    public DateTime JoiningDate { get; set; } = DateTime.Today;

    [Required]
    [Display(Name = "Pay type")]
    public EmployeePayType PayType { get; set; } = EmployeePayType.Monthly;

    [Required]
    [Range(0, 99999999, ErrorMessage = "Enter a valid rate.")]
    [Display(Name = "Pay rate (Rs.)")]
    public decimal? PayRate { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [StringLength(1000)]
    public string? Remarks { get; set; }

    [Display(Name = "Photo")]
    public IFormFile? Photo { get; set; }
    [Display(Name = "CNIC front")]
    public IFormFile? CnicFront { get; set; }
    [Display(Name = "CNIC back")]
    public IFormFile? CnicBack { get; set; }

    public bool HasPhoto { get; set; }
    public bool HasCnicFront { get; set; }
    public bool HasCnicBack { get; set; }
}

public class EmployeeListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Cnic { get; set; }
    public string? Designation { get; set; }
    public EmployeePayType PayType { get; set; }
    public decimal PayRate { get; set; }
    public bool IsActive { get; set; }
    public bool HasPhoto { get; set; }
    public decimal AdvanceBalance { get; set; }
}

public class EmployeeListViewModel
{
    public string? Search { get; set; }
    public bool ShowInactive { get; set; }
    public PagedResult<EmployeeListItemViewModel> Items { get; set; } = new();
    public decimal TotalAdvanceOutstanding { get; set; }
}

public class EmployeeStatementRowViewModel
{
    public int Id { get; set; }
    public DateTime Date { get; set; }
    public EmployeeTransactionType Type { get; set; }
    public DateTime? PeriodFrom { get; set; }
    public DateTime? PeriodTo { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal AdvanceGiven { get; set; }
    public decimal AdvanceDeducted { get; set; }
    public decimal NetPaid { get; set; }
    public PaymentSource PaymentSource { get; set; }
    public string? Remarks { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    /// <summary>Outstanding advance after this row.</summary>
    public decimal AdvanceBalance { get; set; }

    public string TypeLabel => Type switch
    {
        EmployeeTransactionType.SalaryPayment => "Salary",
        EmployeeTransactionType.Advance => "Advance",
        EmployeeTransactionType.Bonus => "Bonus",
        _ => Type.ToString()
    };
}

public class EmployeeDetailsViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? FatherName { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Cnic { get; set; }
    public string? Designation { get; set; }
    public DateTime JoiningDate { get; set; }
    public EmployeePayType PayType { get; set; }
    public decimal PayRate { get; set; }
    public bool IsActive { get; set; }
    public string? Remarks { get; set; }
    public bool HasPhoto { get; set; }
    public bool HasCnicFront { get; set; }
    public bool HasCnicBack { get; set; }
    public DateTime? LastSalaryPeriodTo { get; set; }

    // Statement
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal OpeningAdvanceBalance { get; set; }
    public List<EmployeeStatementRowViewModel> Rows { get; set; } = new();
    /// <summary>Outstanding advance as of today (all history).</summary>
    public decimal CurrentAdvanceBalance { get; set; }

    public decimal PeriodGross => Rows.Sum(r => r.GrossAmount);
    public decimal PeriodAdvanceGiven => Rows.Sum(r => r.AdvanceGiven);
    public decimal PeriodDeducted => Rows.Sum(r => r.AdvanceDeducted);
    public decimal PeriodNetPaid => Rows.Sum(r => r.NetPaid);

    public string PayLabel => $"Rs. {PayRate:N0} / {PayType switch { EmployeePayType.Daily => "day", EmployeePayType.Weekly => "week", _ => "month" }}";

    // Quick "give advance" form defaults
    public EmployeeAdvanceFormViewModel NewAdvance { get; set; } = new();
}

public class EmployeeAdvanceFormViewModel
{
    public int EmployeeId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; } = DateTime.Today;

    [Required]
    public EmployeeTransactionType Type { get; set; } = EmployeeTransactionType.Advance;

    [Required]
    [Range(0.01, 99999999, ErrorMessage = "Enter a valid amount.")]
    public decimal? Amount { get; set; }

    public PaymentSource PaymentSource { get; set; } = PaymentSource.Cash;

    [StringLength(500)]
    public string? Remarks { get; set; }
}

public class SalaryPaymentFormViewModel
{
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = "";
    public EmployeePayType PayType { get; set; }
    public decimal PayRate { get; set; }
    public decimal OutstandingAdvance { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Payment date")]
    public DateTime Date { get; set; } = DateTime.Today;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Period from")]
    public DateTime PeriodFrom { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Period to")]
    public DateTime PeriodTo { get; set; }

    [Required]
    [Range(0, 99999999, ErrorMessage = "Enter a valid amount.")]
    [Display(Name = "Gross salary (Rs.)")]
    public decimal? GrossAmount { get; set; }

    [Range(0, 99999999, ErrorMessage = "Enter a valid amount.")]
    [Display(Name = "Deduct from advance (Rs.)")]
    public decimal? AdvanceDeducted { get; set; }

    [Display(Name = "Paid from")]
    public PaymentSource PaymentSource { get; set; } = PaymentSource.Cash;

    [StringLength(500)]
    public string? Remarks { get; set; }
}

public class EmployeeSummaryRowViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Designation { get; set; }
    public bool IsActive { get; set; }
    public decimal SalaryGross { get; set; }
    public decimal AdvanceGiven { get; set; }
    public decimal AdvanceDeducted { get; set; }
    public decimal SalaryPaid { get; set; }
    public decimal Bonus { get; set; }
    /// <summary>Cash actually handed over in the period = advances + net salary + bonus.</summary>
    public decimal TotalPaid => AdvanceGiven + SalaryPaid + Bonus;
    public decimal AdvanceBalance { get; set; }
}

public class EmployeeSummaryViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<EmployeeSummaryRowViewModel> Rows { get; set; } = new();
}
