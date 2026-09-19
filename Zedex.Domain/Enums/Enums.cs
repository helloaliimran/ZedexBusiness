namespace Zedex.Domain.Enums;

/// <summary>How a product's Price field is interpreted.</summary>
public enum PricingMode
{
    PerUnit = 1,
    PerFoot = 2
}

/// <summary>Payment method chosen when posting an invoice.</summary>
public enum PaymentType
{
    Cash = 1,
    Partial = 2,
    Credit = 3
}

/// <summary>Type of a customer ledger entry.</summary>
public enum LedgerEntryType
{
    Bill = 1,      // debit — increases receivable
    Payment = 2,   // credit — decreases receivable
    Credit = 3,    // manual credit adjustment
    Debit = 4,     // manual debit adjustment
    Return = 5     // credit — sale return
}

/// <summary>Gas kit (gasket) option on a PVC product / bill line.</summary>
public enum GasKitType
{
    None = 1,
    Single = 2,
    Double = 3
}

/// <summary>How a PVC product's Price field is interpreted when billing.</summary>
public enum PvcSaleType
{
    /// <summary>Price = Rs. per running foot; amount = total feet × rate.</summary>
    PerRunningFoot = 1,
    /// <summary>Price = Rs. per kg; amount = total weight × rate
    /// (weight per length comes from <c>Product.WeightPerLength</c>).</summary>
    WeightPerLength = 2,
    /// <summary>Price = Rs. per whole length (piece), regardless of its size in feet;
    /// amount = quantity × rate. Length is still recorded (it identifies which stocked
    /// piece length is deducted) but does not factor into the amount.</summary>
    RatePerLength = 3
}

/// <summary>Which billing module an invoice belongs to. PVC invoices share the
/// Invoice header (so ledger/customer sync is automatic) but use PvcInvoiceItem
/// lines and separate views.</summary>
public enum InvoiceType
{
    Standard = 1,
    Pvc = 2
}

/// <summary>Modules a Worker can be granted access to.</summary>
public enum AppModule
{
    Dashboard = 1,
    Products = 2,
    Stock = 3,
    Billing = 4,
    Customers = 5,
    CustomerLedger = 6,
    Reports = 7,
    Expenses = 8,
    Employees = 9,
    CashBook = 10
}

/// <summary>Where money for an expense / employee payment came from.</summary>
public enum PaymentSource
{
    /// <summary>Taken from the shop's cash (daily sales drawer).</summary>
    Cash = 1,
    /// <summary>Bank transfer, JazzCash, EasyPaisa, etc.</summary>
    Online = 2
}

/// <summary>How an employee's pay rate is interpreted.</summary>
public enum EmployeePayType
{
    Daily = 1,
    Weekly = 2,
    Monthly = 3
}

/// <summary>Kind of money movement recorded against an employee.</summary>
public enum EmployeeTransactionType
{
    /// <summary>Cash given in advance; recovered later via AdvanceDeducted on a salary payment.</summary>
    Advance = 1,
    /// <summary>Salary/wage payout for a period: NetPaid = GrossAmount − AdvanceDeducted.</summary>
    SalaryPayment = 2,
    /// <summary>Extra one-off payment (Eid, overtime, reward).</summary>
    Bonus = 3
}
