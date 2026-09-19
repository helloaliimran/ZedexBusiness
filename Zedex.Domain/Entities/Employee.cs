using Zedex.Domain.Common;
using Zedex.Domain.Enums;

namespace Zedex.Domain.Entities;

/// <summary>Shop employee. Photo / CNIC images are stored outside wwwroot (private) and
/// served only through the permission-checked EmployeesController.File action.</summary>
public class Employee : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? FatherName { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    /// <summary>Pakistani CNIC, stored formatted: 12345-1234567-1. Unique when present.</summary>
    public string? Cnic { get; set; }
    public string? Designation { get; set; }
    public DateTime JoiningDate { get; set; }

    public EmployeePayType PayType { get; set; } = EmployeePayType.Monthly;
    /// <summary>Rs. per day / week / month depending on <see cref="PayType"/>.</summary>
    public decimal PayRate { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Remarks { get; set; }

    // Private file paths (relative to the private uploads root, e.g. "employees/abc.jpg").
    public string? PhotoPath { get; set; }
    public string? CnicFrontPath { get; set; }
    public string? CnicBackPath { get; set; }

    public ICollection<EmployeeTransaction> Transactions { get; set; } = new List<EmployeeTransaction>();
}

/// <summary>
/// One money movement for an employee.
/// <list type="bullet">
/// <item>Advance: NetPaid = amount given; GrossAmount = AdvanceDeducted = 0.</item>
/// <item>SalaryPayment: GrossAmount = earned for the period; AdvanceDeducted = advance recovered;
///       NetPaid = GrossAmount − AdvanceDeducted (actual cash handed over).</item>
/// <item>Bonus: NetPaid = amount given.</item>
/// </list>
/// Cash out = NetPaid. Outstanding advance = Σ Advance.NetPaid − Σ AdvanceDeducted.
/// </summary>
public class EmployeeTransaction : BaseEntity
{
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = default!;
    public DateTime TransactionDate { get; set; }
    public EmployeeTransactionType Type { get; set; }

    /// <summary>SalaryPayment only: the pay period covered.</summary>
    public DateTime? PeriodFrom { get; set; }
    public DateTime? PeriodTo { get; set; }

    public decimal GrossAmount { get; set; }
    public decimal AdvanceDeducted { get; set; }
    public decimal NetPaid { get; set; }

    public PaymentSource PaymentSource { get; set; } = PaymentSource.Cash;
    public string? Remarks { get; set; }
}
