using Zedex.Domain.Common;

namespace Zedex.Domain.Entities;

public class Customer : BaseEntity
{
    public string Name { get; set; } = default!;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Remarks { get; set; }
    public string? ImagePath { get; set; }
    public decimal OpeningBalance { get; set; }

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public ICollection<LedgerEntry> LedgerEntries { get; set; } = new List<LedgerEntry>();
}
