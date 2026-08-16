using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Domain.Entities;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;

namespace Zedex.Web.Controllers;

/// <summary>
/// Sale returns against posted PVC invoices. Mirrors ReturnsController:
/// a return posts immediately — whole pieces go back into stock at the sold
/// length, the PVC line's returned quantity is updated, and the customer
/// ledger receives a credit entry. Shares the SaleReturn header (via nullable
/// PvcInvoiceItemId on SaleReturnItem) so ledger sync is automatic.
/// The per-piece refund includes that piece's gas kit and discount share.
/// </summary>
[Authorize(Policy = "Module:Billing")]
public class PvcReturnsController : Controller
{
    private readonly AppDbContext _db;

    public PvcReturnsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Create(int invoiceId)
    {
        var invoice = await LoadInvoiceAsync(invoiceId, tracking: false);
        if (invoice is null)
            return NotFound();
        if (!invoice.IsPosted)
        {
            TempData["Error"] = "Returns can only be made against posted invoices.";
            return RedirectToAction("Details", "PvcInvoices", new { id = invoiceId });
        }

        var vm = BuildForm(invoice);
        if (vm.Lines.All(l => l.Returnable <= 0))
        {
            TempData["Error"] = $"All items on {invoice.InvoiceNumber} have already been fully returned.";
            return RedirectToAction("Details", "PvcInvoices", new { id = invoiceId });
        }
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PvcReturnFormViewModel vm)
    {
        var invoice = await LoadInvoiceAsync(vm.InvoiceId, tracking: true);
        if (invoice is null)
            return NotFound();
        if (!invoice.IsPosted)
        {
            TempData["Error"] = "Returns can only be made against posted invoices.";
            return RedirectToAction("Details", "PvcInvoices", new { id = vm.InvoiceId });
        }

        var itemsById = invoice.PvcItems.ToDictionary(x => x.Id);
        var saleReturn = new SaleReturn
        {
            ReturnNumber = await GenerateReturnNumberAsync(vm.ReturnDate),
            InvoiceId = invoice.Id,
            CustomerId = invoice.CustomerId,
            ReturnDate = vm.ReturnDate.Date,
            Remarks = vm.Remarks?.Trim()
        };

        decimal totalRefund = 0;
        foreach (var line in vm.Lines)
        {
            var quantity = line.ReturnQuantity ?? 0;
            if (quantity == 0)
                continue;

            if (!itemsById.TryGetValue(line.PvcInvoiceItemId, out var item))
            {
                ModelState.AddModelError(string.Empty, "Invalid invoice line.");
                continue;
            }

            var returnable = (int)(item.Quantity - item.ReturnedQuantity);
            if (quantity < 0 || quantity > returnable)
            {
                ModelState.AddModelError(string.Empty,
                    $"\"{item.Product.Name}\": return quantity must be between 0 and {returnable}.");
                continue;
            }

            // Per-piece refund: the piece's share of the line total, which
            // already includes its gas kit and discount.
            var unitNet = item.Quantity > 0 ? item.LineTotal / item.Quantity : 0;
            var refund = Math.Round(unitNet * quantity, 2);

            saleReturn.Items.Add(new SaleReturnItem
            {
                PvcInvoiceItemId = item.Id,
                ProductId = item.ProductId,
                Quantity = quantity,
                SizeFt = item.LengthFt,
                TotalFeet = quantity * item.LengthFt,
                Rate = item.Rate,
                LineTotal = refund
            });

            // Restore stock: whole pieces come back at the sold length.
            item.ReturnedQuantity += quantity;
            item.Product.CurrentStock += quantity * item.LengthFt;
            await AdjustPiecesAsync(item.ProductId, item.LengthFt, quantity);
            totalRefund += refund;
        }

        if (saleReturn.Items.Count == 0)
            ModelState.AddModelError(string.Empty, "Enter a return quantity for at least one item.");

        if (!ModelState.IsValid)
        {
            // Rebuild the display data (only ReturnQuantity survives the post).
            var form = BuildForm(invoice);
            form.ReturnDate = vm.ReturnDate;
            form.Remarks = vm.Remarks;
            foreach (var line in form.Lines)
                line.ReturnQuantity = vm.Lines
                    .FirstOrDefault(l => l.PvcInvoiceItemId == line.PvcInvoiceItemId)?.ReturnQuantity;
            return View(form);
        }

        saleReturn.TotalAmount = totalRefund;
        _db.SaleReturns.Add(saleReturn);

        _db.LedgerEntries.Add(new LedgerEntry
        {
            CustomerId = invoice.CustomerId,
            EntryDate = vm.ReturnDate.Date,
            Type = LedgerEntryType.Return,
            InvoiceId = invoice.Id,
            SaleReturn = saleReturn,
            Credit = totalRefund,
            Remarks = $"Return {saleReturn.ReturnNumber} against {invoice.InvoiceNumber}"
                      + (string.IsNullOrWhiteSpace(vm.Remarks) ? "" : $" — {vm.Remarks.Trim()}")
        });

        // Single transaction; retry on the (rare) duplicate-number race.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                saleReturn.ReturnNumber = await GenerateReturnNumberAsync(vm.ReturnDate);
            }
        }

        TempData["Success"] = $"Return {saleReturn.ReturnNumber} posted — stock restored and Rs. {totalRefund:N2} credited to {invoice.Customer.Name}'s ledger.";
        return RedirectToAction(nameof(Details), new { id = saleReturn.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var saleReturn = await _db.SaleReturns.AsNoTracking()
            .Where(r => r.Id == id && r.Invoice.InvoiceType == InvoiceType.Pvc)
            .Select(r => new PvcReturnDetailsViewModel
            {
                Id = r.Id,
                ReturnNumber = r.ReturnNumber,
                InvoiceId = r.InvoiceId,
                InvoiceNumber = r.Invoice.InvoiceNumber,
                CustomerName = r.Customer.Name,
                ReturnDate = r.ReturnDate,
                TotalAmount = r.TotalAmount,
                Remarks = r.Remarks,
                CreatedBy = r.CreatedBy,
                Rows = r.Items.Where(x => !x.IsDeleted).Select(x => new PvcReturnRowViewModel
                {
                    Product = x.Product.Name
                        + " G" + x.Product.Gauge.Name
                        + (x.Product.Company != null ? " " + x.Product.Company.Name : "")
                        + " " + x.Product.Color.Name,
                    Quantity = (int)x.Quantity,
                    LengthFt = x.SizeFt ?? 0,
                    TotalFeet = x.TotalFeet,
                    Rate = x.Rate,
                    LineTotal = x.LineTotal
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (saleReturn is null)
            return NotFound();
        return View(saleReturn);
    }

    // ---------- Helpers ----------

    private async Task<Invoice?> LoadInvoiceAsync(int id, bool tracking)
    {
        var query = tracking ? _db.Invoices : _db.Invoices.AsNoTracking();
        return await query
            .Include(i => i.Customer)
            .Include(i => i.PvcItems.Where(x => !x.IsDeleted))
                .ThenInclude(x => x.Product).ThenInclude(p => p.Color)
            .Include(i => i.PvcItems.Where(x => !x.IsDeleted))
                .ThenInclude(x => x.Product).ThenInclude(p => p.Gauge)
            .Include(i => i.PvcItems.Where(x => !x.IsDeleted))
                .ThenInclude(x => x.Product).ThenInclude(p => p.Company)
            .FirstOrDefaultAsync(i => i.Id == id && i.InvoiceType == InvoiceType.Pvc);
    }

    private static PvcReturnFormViewModel BuildForm(Invoice invoice) => new()
    {
        InvoiceId = invoice.Id,
        InvoiceNumber = invoice.InvoiceNumber,
        CustomerName = invoice.Customer.Name,
        Lines = invoice.PvcItems.Where(x => !x.IsDeleted).Select(x => new PvcReturnLineViewModel
        {
            PvcInvoiceItemId = x.Id,
            // Null-safe: navigations are filtered out if soft-deleted.
            Product = x.Product.Name
                + " G" + (x.Product.Gauge?.Name ?? "—")
                + (x.Product.Company != null ? " " + x.Product.Company.Name : "")
                + " " + (x.Product.Color?.Name ?? "—"),
            SaleType = x.SaleType,
            LengthFt = x.LengthFt,
            GasKitType = x.GasKitType,
            Quantity = (int)x.Quantity,
            Returned = (int)x.ReturnedQuantity,
            UnitNet = x.Quantity > 0 ? Math.Round(x.LineTotal / x.Quantity, 2) : 0
        }).ToList()
    };

    /// <summary>PVC returns get their own per-day sequence: PRET-yyyyMMdd-####.</summary>
    private async Task<string> GenerateReturnNumberAsync(DateTime date)
    {
        var day = date.Date;
        var count = await _db.SaleReturns.IgnoreQueryFilters()
            .CountAsync(r => r.Invoice.InvoiceType == InvoiceType.Pvc
                && r.ReturnDate >= day && r.ReturnDate < day.AddDays(1));
        return $"PRET-{day:yyyyMMdd}-{count + 1:D4}";
    }

    private async Task AdjustPiecesAsync(int productId, decimal lengthFt, int delta)
    {
        var piece = await _db.StockPieces
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.ProductId == productId && s.LengthFt == lengthFt);

        if (piece is null)
        {
            piece = new StockPiece { ProductId = productId, LengthFt = lengthFt, Quantity = 0 };
            _db.StockPieces.Add(piece);
        }
        else if (piece.IsDeleted)
        {
            // Row was previously soft-deleted (e.g. by a stock reset) — its old quantity
            // must not carry forward into the resurrected row.
            piece.Quantity = 0;
        }
        piece.IsDeleted = false;
        piece.Quantity += delta;
    }
}
