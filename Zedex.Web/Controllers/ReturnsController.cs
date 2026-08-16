using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Domain.Entities;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;

namespace Zedex.Web.Controllers;

/// <summary>
/// Sale returns against posted invoices. A return posts immediately:
/// stock is restored (per-foot pieces come back at the sold size),
/// the invoice line's returned quantity is updated, and the customer
/// ledger receives a credit entry.
/// </summary>
[Authorize(Policy = "Module:Billing")]
public class ReturnsController : Controller
{
    private readonly AppDbContext _db;

    public ReturnsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Create(int invoiceId)
    {
        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Items.Where(x => !x.IsDeleted))
                .ThenInclude(x => x.Product).ThenInclude(p => p.Color)
            .Include(i => i.Items.Where(x => !x.IsDeleted))
                .ThenInclude(x => x.Product).ThenInclude(p => p.Gauge)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (invoice is null)
            return NotFound();
        if (invoice.InvoiceType == InvoiceType.Pvc)
            return RedirectToAction("Create", "PvcReturns", new { invoiceId });
        if (!invoice.IsPosted)
        {
            TempData["Error"] = "Returns can only be made against posted invoices.";
            return RedirectToAction("Details", "Invoices", new { id = invoiceId });
        }

        var vm = BuildForm(invoice);
        if (vm.Lines.All(l => l.Returnable <= 0))
        {
            TempData["Error"] = $"All items on {invoice.InvoiceNumber} have already been fully returned.";
            return RedirectToAction("Details", "Invoices", new { id = invoiceId });
        }
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReturnFormViewModel vm)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.Items.Where(x => !x.IsDeleted))
                .ThenInclude(x => x.Product).ThenInclude(p => p.Color)
            .Include(i => i.Items.Where(x => !x.IsDeleted))
                .ThenInclude(x => x.Product).ThenInclude(p => p.Gauge)
            .FirstOrDefaultAsync(i => i.Id == vm.InvoiceId);
        if (invoice is null)
            return NotFound();
        if (invoice.InvoiceType == InvoiceType.Pvc)
            return RedirectToAction("Create", "PvcReturns", new { invoiceId = vm.InvoiceId });
        if (!invoice.IsPosted)
        {
            TempData["Error"] = "Returns can only be made against posted invoices.";
            return RedirectToAction("Details", "Invoices", new { id = vm.InvoiceId });
        }

        var itemsById = invoice.Items.ToDictionary(x => x.Id);
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

            if (!itemsById.TryGetValue(line.InvoiceItemId, out var item))
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

            var unitNet = item.Quantity > 0 ? item.LineTotal / item.Quantity : 0;
            var refund = Math.Round(unitNet * quantity, 2);
            var perFoot = item.Product.PricingMode == PricingMode.PerFoot;

            saleReturn.Items.Add(new SaleReturnItem
            {
                InvoiceItemId = item.Id,
                ProductId = item.ProductId,
                Quantity = quantity,
                SizeFt = item.SizeFt,
                TotalFeet = perFoot ? quantity * item.SizeFt : null,
                Rate = item.Rate,
                LineTotal = refund
            });

            // Restore stock: per-foot pieces come back at the size that was sold.
            item.ReturnedQuantity += quantity;
            if (perFoot && item.SizeFt is not null)
            {
                item.Product.CurrentStock += quantity * item.SizeFt.Value;
                await AdjustPiecesAsync(item.ProductId, item.SizeFt.Value, quantity);
            }
            else
            {
                item.Product.CurrentStock += quantity;
            }
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
                    .FirstOrDefault(l => l.InvoiceItemId == line.InvoiceItemId)?.ReturnQuantity;
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
        // Route PVC returns to their own module (links may point here generically).
        var isPvc = await _db.SaleReturns.AsNoTracking()
            .AnyAsync(r => r.Id == id && r.Invoice.InvoiceType == InvoiceType.Pvc);
        if (isPvc)
            return RedirectToAction("Details", "PvcReturns", new { id });

        var saleReturn = await _db.SaleReturns.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new ReturnDetailsViewModel
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
                Rows = r.Items.Where(x => !x.IsDeleted).Select(x => new ReturnRowViewModel
                {
                    Product = x.Product.Name + " (" + x.Product.Color.Name + ", G" + x.Product.Gauge.Name + ")",
                    Quantity = (int)x.Quantity,
                    SizeFt = x.SizeFt,
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

    private static ReturnFormViewModel BuildForm(Invoice invoice) => new()
    {
        InvoiceId = invoice.Id,
        InvoiceNumber = invoice.InvoiceNumber,
        CustomerName = invoice.Customer.Name,
        Lines = invoice.Items.Where(x => !x.IsDeleted).Select(x => new ReturnLineViewModel
        {
            InvoiceItemId = x.Id,
            // Null-safe: Color/Gauge navigations are filtered out if soft-deleted.
            Product = x.Product.Name + " (" + (x.Product.Color?.Name ?? "—") + ", G" + (x.Product.Gauge?.Name ?? "—") + ")",
            Mode = x.Product.PricingMode,
            SizeFt = x.SizeFt,
            Quantity = (int)x.Quantity,
            Returned = (int)x.ReturnedQuantity,
            UnitNet = x.Quantity > 0 ? Math.Round(x.LineTotal / x.Quantity, 2) : 0
        }).ToList()
    };

    private async Task<string> GenerateReturnNumberAsync(DateTime date)
    {
        var day = date.Date;
        var count = await _db.SaleReturns.IgnoreQueryFilters()
            .CountAsync(r => r.ReturnDate >= day && r.ReturnDate < day.AddDays(1));
        return $"RET-{day:yyyyMMdd}-{count + 1:D4}";
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
