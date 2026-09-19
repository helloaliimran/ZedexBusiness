using Zedex.Domain.Common;
using Zedex.Domain.Enums;

namespace Zedex.Domain.Entities;

/// <summary>Expense head (Staff Food, Water, Internal Purchase, ...). Admin-managed lookup.</summary>
public class ExpenseCategory : BaseEntity, INamedEntity
{
    public string Name { get; set; } = default!;
    public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
}

/// <summary>
/// Money paid out for running the shop (staff lunch/dinner, water refill, items bought
/// for internal use, ...). Employee salaries and advances are NOT recorded here — they
/// live in <see cref="EmployeeTransaction"/> so they are never entered twice; the Cash
/// Book report combines both.
/// </summary>
public class Expense : BaseEntity
{
    public DateTime ExpenseDate { get; set; }
    public int ExpenseCategoryId { get; set; }
    public ExpenseCategory ExpenseCategory { get; set; } = default!;
    public decimal Amount { get; set; }
    /// <summary>Cash = taken from the daily sales cash; Online = bank / wallet.</summary>
    public PaymentSource PaymentSource { get; set; } = PaymentSource.Cash;
    /// <summary>Free-text details (e.g. "20 water bottles", "lunch for 4 staff").</summary>
    public string Description { get; set; } = default!;
    /// <summary>Optional receipt image/PDF (stored privately, served via ExpensesController).</summary>
    public string? AttachmentPath { get; set; }
}
