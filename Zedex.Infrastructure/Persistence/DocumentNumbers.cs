using Microsoft.EntityFrameworkCore;

namespace Zedex.Infrastructure.Persistence;

/// <summary>
/// Per-day document numbers such as INV-20260920-0007.
///
/// The next number is (highest existing sequence for that prefix) + 1 — NOT
/// "count of documents on that day + 1". Counting breaks as soon as a bill's date is
/// edited (or a PVC/standard bill is moved between days): the count for a day drops
/// below the highest number already used, the "next" number already exists, and
/// Postgres rejects the insert with 23505 on IX_Invoices_InvoiceNumber — and every
/// retry produced the same number again.
///
/// Soft-deleted rows are included (callers pass an IgnoreQueryFilters() query) because
/// the unique index covers them too.
/// </summary>
public static class DocumentNumbers
{
    public static async Task<string> NextAsync(IQueryable<string> existingNumbers, string prefix)
    {
        var used = await existingNumbers
            .Where(n => n.StartsWith(prefix))
            .ToListAsync();

        var max = 0;
        foreach (var number in used)
            if (int.TryParse(number.AsSpan(prefix.Length), out var seq) && seq > max)
                max = seq;

        return $"{prefix}{max + 1:D4}";
    }
}
