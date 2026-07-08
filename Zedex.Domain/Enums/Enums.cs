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

/// <summary>Modules a Worker can be granted access to.</summary>
public enum AppModule
{
    Dashboard = 1,
    Products = 2,
    Stock = 3,
    Billing = 4,
    Customers = 5,
    CustomerLedger = 6,
    Reports = 7
}
