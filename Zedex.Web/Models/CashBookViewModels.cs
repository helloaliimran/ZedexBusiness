namespace Zedex.Web.Models;

/// <summary>One day of the Cash Book.</summary>
public class CashBookRowViewModel
{
    public DateTime Date { get; set; }
    public int Bills { get; set; }
    /// <summary>Posted bill totals (cash + credit + partial) dated this day.</summary>
    public decimal Sales { get; set; }
    /// <summary>Sale returns dated this day.</summary>
    public decimal Returns { get; set; }
    /// <summary>Money received in cash: bill payments + customer ledger payments.</summary>
    public decimal CashInCash { get; set; }
    /// <summary>Money received online: bill payments + customer ledger payments.</summary>
    public decimal CashInOnline { get; set; }
    /// <summary>All money received (ledger Payment entries, Credit − Debit).</summary>
    public decimal CashIn => CashInCash + CashInOnline;
    public decimal ExpenseCash { get; set; }
    public decimal ExpenseOnline { get; set; }
    /// <summary>Salaries (net), advances and bonuses paid to employees.</summary>
    public decimal StaffCash { get; set; }
    public decimal StaffOnline { get; set; }

    public decimal NetSales => Sales - Returns;
    public decimal Expenses => ExpenseCash + ExpenseOnline;
    public decimal StaffPayments => StaffCash + StaffOnline;
    public decimal TotalOut => Expenses + StaffPayments;
    /// <summary>Cash In − Cash Out.</summary>
    public decimal NetCash => CashIn - TotalOut;
    /// <summary>Net sales − all expenses (incl. staff payments).</summary>
    public decimal SaleMinusExpense => NetSales - TotalOut;
    /// <summary>Cash drawer: cash received − cash expenses − cash staff payments.</summary>
    public decimal CashDrawer => CashInCash - ExpenseCash - StaffCash;
    /// <summary>Online (bank / wallet): online received − online expenses − online staff payments.</summary>
    public decimal OnlineNet => CashInOnline - ExpenseOnline - StaffOnline;
}

public class CashBookBreakdownViewModel
{
    public string Label { get; set; } = default!;
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public class CashBookViewModel
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public List<CashBookRowViewModel> Rows { get; set; } = new();
    public List<CashBookBreakdownViewModel> ExpenseByCategory { get; set; } = new();
    public List<CashBookBreakdownViewModel> StaffByType { get; set; } = new();

    public int TotalBills => Rows.Sum(r => r.Bills);
    public decimal TotalSales => Rows.Sum(r => r.Sales);
    public decimal TotalReturns => Rows.Sum(r => r.Returns);
    public decimal TotalNetSales => Rows.Sum(r => r.NetSales);
    public decimal TotalCashIn => Rows.Sum(r => r.CashIn);
    public decimal TotalExpenseCash => Rows.Sum(r => r.ExpenseCash);
    public decimal TotalExpenseOnline => Rows.Sum(r => r.ExpenseOnline);
    public decimal TotalExpenses => Rows.Sum(r => r.Expenses);
    public decimal TotalStaffCash => Rows.Sum(r => r.StaffCash);
    public decimal TotalStaffOnline => Rows.Sum(r => r.StaffOnline);
    public decimal TotalStaff => Rows.Sum(r => r.StaffPayments);
    public decimal TotalOut => Rows.Sum(r => r.TotalOut);
    public decimal TotalNetCash => Rows.Sum(r => r.NetCash);
    public decimal TotalSaleMinusExpense => Rows.Sum(r => r.SaleMinusExpense);
    public decimal TotalCashDrawer => Rows.Sum(r => r.CashDrawer);
    public decimal TotalOnlineNet => Rows.Sum(r => r.OnlineNet);
    public decimal TotalCashInCash => Rows.Sum(r => r.CashInCash);
    public decimal TotalCashInOnline => Rows.Sum(r => r.CashInOnline);
}
