using Zedex.Domain.Entities;
using Zedex.Domain.Enums;

namespace Zedex.Web.Services;

/// <summary>
/// Which day money and sales belong to in reports.
///
/// A bill can be saved as a draft today and posted days later when the customer
/// comes back and pays. Reports therefore use the day the bill was POSTED
/// (<see cref="Invoice.PostedDate"/>), not the draft's <see cref="Invoice.InvoiceDate"/>.
/// Payments taken at posting follow the same rule — older rows were stamped with the
/// invoice date, so for those the invoice's PostedDate is used. Payments entered in the
/// customer ledger use their own EntryDate.
///
/// Keep the inline copies of these expressions (used in GroupBy) in sync:
///   bill day:    (i.PostedDate ?? i.InvoiceDate).Date
///   payment day: (l.InvoiceId != null &amp;&amp; l.Invoice!.PostedDate != null ? l.Invoice.PostedDate.Value : l.EntryDate).Date
/// </summary>
public static class ReportDateFilters
{
    /// <summary>Posted bills whose posting day falls in [from, end).</summary>
    public static IQueryable<Invoice> PostedBetween(this IQueryable<Invoice> query, DateTime from, DateTime end) =>
        query.Where(i => i.IsPosted
                         && (i.PostedDate ?? i.InvoiceDate) >= from
                         && (i.PostedDate ?? i.InvoiceDate) < end);

    /// <summary>Customer payments (at billing or in the ledger) received in [from, end).</summary>
    public static IQueryable<LedgerEntry> PaymentsBetween(this IQueryable<LedgerEntry> query, DateTime from, DateTime end) =>
        query.Where(l => l.Type == LedgerEntryType.Payment
                         && (l.InvoiceId != null && l.Invoice!.PostedDate != null ? l.Invoice.PostedDate.Value : l.EntryDate) >= from
                         && (l.InvoiceId != null && l.Invoice!.PostedDate != null ? l.Invoice.PostedDate.Value : l.EntryDate) < end);
}
