using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Entities;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;

namespace Zedex.Web.Controllers;

/// <summary>
/// Billing. Invoices follow draft → post: drafts are editable and have no
/// stock/ledger effect; posting deducts stock (with per-foot cutting),
/// writes ledger entries per the chosen payment method, and locks the invoice.
/// </summary>
[Authorize(Policy = "Module:Billing")]
public class InvoicesController : Controller
{
    private const int PageSize = 10;
    private readonly AppDbContext _db;

    public InvoicesController(AppDbContext db) => _db = db;

    // ---------- Listing ----------

    public async Task<IActionResult> Index(
        string? search, DateTime? from, DateTime? to, PaymentType? type, bool? posted, int page = 1)
    {
        // PVC invoices live in their own module (PvcInvoicesController).
        var query = _db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceType == InvoiceType.Standard);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(i =>
                EF.Functions.ILike(i.InvoiceNumber, pattern) ||
                EF.Functions.ILike(i.Customer.Name, pattern));
        }
        if (from is not null)
            query = query.Where(i => i.InvoiceDate >= from.Value.Date);
        if (to is not null)
            query = query.Where(i => i.InvoiceDate < to.Value.Date.AddDays(1));
        if (type is not null)
            query = query.Where(i => i.PaymentType == type);
        if (posted is not null)
            query = query.Where(i => i.IsPosted == posted);

        var total = await query.CountAsync();
        page = Math.Max(1, page);
        var items = await query
            .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(i => new InvoiceListItemViewModel
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                Customer = i.Customer.Name,
                InvoiceDate = i.InvoiceDate,
                Total = i.Total,
                PaidAmount = i.PaidAmount,
                PaymentType = i.PaymentType,
                IsPosted = i.IsPosted,
                CreatedBy = i.CreatedBy
            })
            .ToListAsync();

        return View(new InvoiceListViewModel
        {
            Search = search, From = from, To = to, Type = type, Posted = posted,
            Items = new PagedResult<InvoiceListItemViewModel>
            {
                Items = items, Page = page, PageSize = PageSize, TotalCount = total
            }
        });
    }

    // ---------- Detail views (large + small) ----------

    public Task<IActionResult> Details(int id) => RenderDetails(id, "Details");

    public Task<IActionResult> Small(int id) => RenderDetails(id, "Small");

    public Task<IActionResult> PickList(int id) => RenderDetails(id, "PickList");

    private async Task<IActionResult> RenderDetails(int id, string viewName)
    {
        // Ledger/report links point here for every invoice — route PVC bills
        // to their own module.
        var invoiceType = await _db.Invoices.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => (InvoiceType?)i.InvoiceType)
            .FirstOrDefaultAsync();
        if (invoiceType == InvoiceType.Pvc)
            return RedirectToAction(viewName, "PvcInvoices", new { id });

        var invoice = await _db.Invoices.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => new InvoiceDetailsViewModel
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate,
                CustomerId = i.CustomerId,
                CustomerName = i.Customer.Name,
                CustomerPhone = i.Customer.Phone,
                CustomerAddress = i.Customer.Address,
                CustomerBalance = i.Customer.OpeningBalance + (i.Customer.LedgerEntries
                    .Where(l => !l.IsDeleted)
                    .Sum(l => (decimal?)(l.Debit - l.Credit)) ?? 0),
                InvoiceLedgerEffect = i.Customer.LedgerEntries
                    .Where(l => !l.IsDeleted && l.InvoiceId == i.Id)
                    .Sum(l => (decimal?)(l.Debit - l.Credit)) ?? 0,
                SubTotal = i.SubTotal,
                Discount = i.Discount,
                FurtherDiscount = i.FurtherDiscount,
                Total = i.Total,
                PaidAmount = i.PaidAmount,
                PaymentType = i.PaymentType,
                Remarks = i.Remarks,
                IsPosted = i.IsPosted,
                PostedBy = i.PostedBy,
                PostedDate = i.PostedDate,
                CreatedBy = i.CreatedBy,
                Rows = i.Items.Where(x => !x.IsDeleted).Select(x => new InvoiceRowViewModel
                {
                    Product = x.Product.Name + " (" + x.Product.Color.Name + ", G" + x.Product.Gauge.Name + ")",
                    Mode = x.Product.PricingMode,
                    Quantity = (int)x.Quantity,
                    SizeFt = x.SizeFt,
                    TotalFeet = x.TotalFeet,
                    CutFromLengthFt = x.CutFromLengthFt,
                    Rate = x.Rate,
                    DiscountPercent = x.DiscountPercent,
                    Discount = x.Discount,
                    LineTotal = x.LineTotal,
                    ReturnedQuantity = (int)x.ReturnedQuantity
                }).ToList(),
                Returns = i.Returns.Where(r => !r.IsDeleted).Select(r => new InvoiceReturnSummaryViewModel
                {
                    Id = r.Id,
                    Number = r.ReturnNumber,
                    Date = r.ReturnDate,
                    Amount = r.TotalAmount
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (invoice is null)
            return NotFound();
        return View(viewName, invoice);
    }

    // ---------- Create / Edit drafts ----------

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadLookupsAsync();
        return View(new InvoiceFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InvoiceFormViewModel vm)
    {
        var products = await ValidateAsync(vm);
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync();
            return View(vm);
        }

        var invoice = new Invoice
        {
            InvoiceNumber = await GenerateInvoiceNumberAsync(vm.InvoiceDate),
            CustomerId = vm.CustomerId,
            InvoiceDate = vm.InvoiceDate.Date,
            Remarks = vm.Remarks?.Trim(),
            IsPosted = false
        };
        BuildItems(invoice, vm, products);

        _db.Invoices.Add(invoice);
        // Retry on the (rare) duplicate-number race when two users save simultaneously.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                invoice.InvoiceNumber = await GenerateInvoiceNumberAsync(vm.InvoiceDate);
            }
        }

        TempData["Success"] = $"Invoice {invoice.InvoiceNumber} saved as draft — post it to update stock and the customer ledger.";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.InvoiceType == InvoiceType.Standard);
        if (invoice is null)
            return NotFound();
        if (invoice.IsPosted)
        {
            TempData["Error"] = $"Invoice {invoice.InvoiceNumber} is posted and can no longer be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var vm = new InvoiceFormViewModel
        {
            Id = invoice.Id,
            CustomerId = invoice.CustomerId,
            InvoiceDate = invoice.InvoiceDate,
            Remarks = invoice.Remarks,
            FurtherDiscount = invoice.FurtherDiscount,
            Items = invoice.Items.Where(x => !x.IsDeleted).Select(x => new InvoiceItemFormViewModel
            {
                ProductId = x.ProductId,
                Quantity = (int)x.Quantity,
                SizeFt = x.SizeFt,
                CutFromLengthFt = x.CutFromLengthFt,
                Rate = x.Rate,
                DiscountPercent = x.DiscountPercent,
                LineTotal = x.LineTotal
            }).ToList()
        };
        ViewBag.InvoiceNumber = invoice.InvoiceNumber;
        await LoadLookupsAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(InvoiceFormViewModel vm)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == vm.Id && i.InvoiceType == InvoiceType.Standard);
        if (invoice is null)
            return NotFound();
        if (invoice.IsPosted)
        {
            TempData["Error"] = $"Invoice {invoice.InvoiceNumber} is posted and can no longer be edited.";
            return RedirectToAction(nameof(Details), new { id = vm.Id });
        }

        var products = await ValidateAsync(vm);
        if (!ModelState.IsValid)
        {
            ViewBag.InvoiceNumber = invoice.InvoiceNumber;
            await LoadLookupsAsync();
            return View(vm);
        }

        invoice.CustomerId = vm.CustomerId;
        invoice.InvoiceDate = vm.InvoiceDate.Date;
        invoice.Remarks = vm.Remarks?.Trim();

        foreach (var old in invoice.Items.Where(x => !x.IsDeleted).ToList())
            _db.InvoiceItems.Remove(old);
        BuildItems(invoice, vm, products);

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Invoice {invoice.InvoiceNumber} updated (still a draft).";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    // ---------- Post (stock deduction + cutting + ledger) ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(PostInvoiceViewModel vm)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Items.Where(x => !x.IsDeleted))
            .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(i => i.Id == vm.Id && i.InvoiceType == InvoiceType.Standard);
        if (invoice is null)
            return NotFound();
        if (invoice.IsPosted)
        {
            TempData["Error"] = $"Invoice {invoice.InvoiceNumber} is already posted.";
            return RedirectToAction(nameof(Details), new { id = vm.Id });
        }
        if (invoice.Items.Count == 0)
        {
            TempData["Error"] = "Cannot post an invoice with no items.";
            return RedirectToAction(nameof(Details), new { id = vm.Id });
        }

        // Resolve paid amount from payment method.
        decimal paid;
        switch (vm.PaymentType)
        {
            case PaymentType.Cash:
                paid = invoice.Total;
                break;
            case PaymentType.Partial:
                if (vm.PaidAmount is null or <= 0 || vm.PaidAmount >= invoice.Total)
                {
                    TempData["Error"] = $"Partial payment must be between Rs. 0.01 and Rs. {invoice.Total - 0.01m:N2}.";
                    return RedirectToAction(nameof(Details), new { id = vm.Id });
                }
                paid = vm.PaidAmount.Value;
                break;
            default: // Credit
                paid = 0;
                break;
        }

        // ---- Stock deduction with per-foot cutting ----
        foreach (var item in invoice.Items)
        {
            var product = item.Product;
            if (product.PricingMode == PricingMode.PerFoot)
            {
                var totalFeet = item.TotalFeet ?? 0;
                product.CurrentStock -= totalFeet;

                if (item.CutFromLengthFt is not null && item.SizeFt is not null)
                {
                    // Take qty pieces of the source length...
                    await AdjustPiecesAsync(product.Id, item.CutFromLengthFt.Value, -(int)item.Quantity);
                    // ...and return the cut remainders to stock (e.g. 18 ft − 10 ft → 8 ft piece).
                    var remainder = item.CutFromLengthFt.Value - item.SizeFt.Value;
                    if (remainder > 0)
                        await AdjustPiecesAsync(product.Id, remainder, (int)item.Quantity);
                }
                else if (item.SizeFt is not null)
                {
                    // Whole length sold (no cutting): deduct qty pieces of that exact length.
                    await AdjustPiecesAsync(product.Id, item.SizeFt.Value, -(int)item.Quantity);
                }
            }
            else
            {
                product.CurrentStock -= item.Quantity;
            }
        }

        // ---- Ledger entries ----
        var remarks = string.IsNullOrWhiteSpace(vm.Remarks) ? null : vm.Remarks.Trim();
        _db.LedgerEntries.Add(new LedgerEntry
        {
            CustomerId = invoice.CustomerId,
            EntryDate = invoice.InvoiceDate,
            Type = LedgerEntryType.Bill,
            InvoiceId = invoice.Id,
            Debit = invoice.Total,
            Remarks = $"Bill {invoice.InvoiceNumber}" + (remarks is null ? "" : $" — {remarks}")
        });
        if (paid > 0)
        {
            _db.LedgerEntries.Add(new LedgerEntry
            {
                CustomerId = invoice.CustomerId,
                EntryDate = invoice.InvoiceDate,
                Type = LedgerEntryType.Payment,
                InvoiceId = invoice.Id,
                Credit = paid,
                Remarks = $"Payment for {invoice.InvoiceNumber} ({vm.PaymentType})" + (remarks is null ? "" : $" — {remarks}")
            });
        }

        invoice.PaymentType = vm.PaymentType;
        invoice.PaidAmount = paid;
        if (remarks is not null)
            invoice.Remarks = invoice.Remarks is null ? remarks : $"{invoice.Remarks} | {remarks}";
        invoice.IsPosted = true;
        invoice.PostedBy = User.Identity?.Name;
        invoice.PostedDate = DateTime.Now;

        await _db.SaveChangesAsync(); // single transaction

        TempData["Success"] = $"Invoice {invoice.InvoiceNumber} posted — stock and customer ledger updated.";
        return RedirectToAction(nameof(Details), new { id = vm.Id });
    }

    // ---------- Delete ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Items.Where(x => !x.IsDeleted))
            .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(i => i.Id == id && i.InvoiceType == InvoiceType.Standard);
        if (invoice is null)
            return NotFound();

        if (invoice.IsPosted)
        {
            if (!User.IsInRole(DbSeeder.AdminRole))
            {
                TempData["Error"] = "Only an admin can delete a posted invoice. Use a sale return for corrections.";
                return RedirectToAction(nameof(Index));
            }
            if (await _db.SaleReturns.AnyAsync(r => r.InvoiceId == id))
            {
                TempData["Error"] = $"Invoice {invoice.InvoiceNumber} has returns against it and cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            // Reverse stock (including cutting) and ledger.
            foreach (var item in invoice.Items)
            {
                var product = item.Product;
                if (product.PricingMode == PricingMode.PerFoot)
                {
                    product.CurrentStock += item.TotalFeet ?? 0;
                    if (item.CutFromLengthFt is not null && item.SizeFt is not null)
                    {
                        await AdjustPiecesAsync(product.Id, item.CutFromLengthFt.Value, (int)item.Quantity);
                        var remainder = item.CutFromLengthFt.Value - item.SizeFt.Value;
                        if (remainder > 0)
                            await AdjustPiecesAsync(product.Id, remainder, -(int)item.Quantity);
                    }
                    else if (item.SizeFt is not null)
                    {
                        // Whole length was sold: return qty pieces of that exact length.
                        await AdjustPiecesAsync(product.Id, item.SizeFt.Value, (int)item.Quantity);
                    }
                }
                else
                {
                    product.CurrentStock += item.Quantity;
                }
            }

            var ledgerEntries = await _db.LedgerEntries.Where(l => l.InvoiceId == id).ToListAsync();
            _db.LedgerEntries.RemoveRange(ledgerEntries);
        }

        foreach (var item in invoice.Items)
            _db.InvoiceItems.Remove(item);
        _db.Invoices.Remove(invoice); // soft delete
        await _db.SaveChangesAsync();

        TempData["Success"] = invoice.IsPosted
            ? $"Posted invoice {invoice.InvoiceNumber} deleted — stock and ledger reversed."
            : $"Draft invoice {invoice.InvoiceNumber} deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Helpers ----------

    private async Task<Dictionary<int, Product>> ValidateAsync(InvoiceFormViewModel vm)
    {
        // Normalize empty numeric inputs.
        vm.FurtherDiscount ??= 0;
        foreach (var item in vm.Items)
        {
            item.Quantity ??= 0;
            item.Rate ??= 0;
            item.DiscountPercent ??= 0;
        }

        vm.Items = vm.Items.Where(x => x.ProductId > 0 || x.Quantity != 0).ToList();
        if (vm.Items.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one item.");

        if (!await _db.Customers.AnyAsync(c => c.Id == vm.CustomerId))
            ModelState.AddModelError(nameof(vm.CustomerId), "Please select a customer.");

        var productIds = vm.Items.Select(x => x.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        decimal netSum = 0;
        for (var i = 0; i < vm.Items.Count; i++)
        {
            var line = vm.Items[i];
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                ModelState.AddModelError(string.Empty, $"Line {i + 1}: select a product.");
                continue;
            }
            if (line.Quantity <= 0)
                ModelState.AddModelError(string.Empty, $"Line {i + 1}: quantity must be greater than zero.");
            if (line.Rate < 0)
                ModelState.AddModelError(string.Empty, $"Line {i + 1}: rate cannot be negative.");

            decimal gross;
            if (product.PricingMode == PricingMode.PerFoot)
            {
                if ((line.SizeFt ?? 0) <= 0)
                    ModelState.AddModelError(string.Empty, $"Line {i + 1}: \"{product.Name}\" is per-foot — enter the size in feet.");
                else if (line.CutFromLengthFt is not null && line.SizeFt > line.CutFromLengthFt)
                    ModelState.AddModelError(string.Empty,
                        $"Line {i + 1}: size ({line.SizeFt:0.##} ft) cannot exceed the piece it is cut from ({line.CutFromLengthFt:0.##} ft).");
                gross = line.Quantity!.Value * (line.SizeFt ?? 0) * line.Rate!.Value;
            }
            else
            {
                line.SizeFt = null;
                line.CutFromLengthFt = null;
                gross = line.Quantity!.Value * line.Rate!.Value;
            }

            // The (possibly rounded) line total is authoritative; it must stay within 0..gross.
            var net = line.LineTotal ?? Math.Round(gross * (1 - line.DiscountPercent!.Value / 100m), 2);
            if (net < 0 || net > gross)
                ModelState.AddModelError(string.Empty,
                    $"Line {i + 1}: line total must be between Rs. 0 and Rs. {gross:N2}.");
            netSum += Math.Clamp(net, 0, gross);
        }

        if (vm.FurtherDiscount < 0 || vm.FurtherDiscount > netSum)
            ModelState.AddModelError(nameof(vm.FurtherDiscount),
                $"Further discount cannot be negative or exceed the bill amount (Rs. {netSum:N2}).");

        return products;
    }

    private static void BuildItems(Invoice invoice, InvoiceFormViewModel vm, Dictionary<int, Product> products)
    {
        decimal subTotal = 0, totalDiscount = 0;
        foreach (var line in vm.Items)
        {
            var product = products[line.ProductId];
            var perFoot = product.PricingMode == PricingMode.PerFoot;
            var quantity = line.Quantity ?? 0;
            var rate = line.Rate ?? 0;
            var totalFeet = perFoot ? quantity * line.SizeFt : null;
            var gross = perFoot
                ? (totalFeet ?? 0) * rate
                : quantity * rate;

            // The user-entered (possibly rounded) line total is authoritative;
            // discount amount and percentage are derived from it.
            var net = line.LineTotal ?? Math.Round(gross * (1 - (line.DiscountPercent ?? 0) / 100m), 2);
            net = Math.Clamp(net, 0, gross);
            var discountAmount = gross - net;
            var discountPercent = gross > 0 ? Math.Round(discountAmount / gross * 100m, 2) : 0;

            invoice.Items.Add(new InvoiceItem
            {
                ProductId = product.Id,
                Quantity = quantity,
                SizeFt = perFoot ? line.SizeFt : null,
                TotalFeet = totalFeet,
                CutFromLengthFt = perFoot ? line.CutFromLengthFt : null,
                Rate = rate,
                DiscountPercent = discountPercent,
                Discount = discountAmount,
                LineTotal = net
            });
            subTotal += gross;
            totalDiscount += discountAmount;
        }

        var furtherDiscount = vm.FurtherDiscount ?? 0;

        // Header amounts are derived: SubTotal is gross, Discount is Σ line discounts.
        invoice.SubTotal = subTotal;
        invoice.Discount = totalDiscount;
        invoice.FurtherDiscount = furtherDiscount;
        invoice.Total = subTotal - totalDiscount - furtherDiscount;
    }

    private async Task<string> GenerateInvoiceNumberAsync(DateTime date)
    {
        var day = date.Date;
        var count = await _db.Invoices.IgnoreQueryFilters()
            .CountAsync(i => i.InvoiceDate >= day && i.InvoiceDate < day.AddDays(1));
        return $"INV-{day:yyyyMMdd}-{count + 1:D4}";
    }

    /// <summary>Adjusts the per-length piece stock; negative piece counts are allowed
    /// (overselling is permitted by business rule).</summary>
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
        piece.IsDeleted = false;
        piece.Quantity += delta;
    }

    private async Task LoadLookupsAsync()
    {
        ViewBag.Customers = new SelectList(
            await _db.Customers.AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, Name = c.Phone == null ? c.Name : c.Name + " (" + c.Phone + ")" })
                .ToListAsync(),
            "Id", "Name");

        ViewBag.CustomersJson = await _db.Customers.AsNoTracking()
            .Select(c => new
            {
                id = c.Id,
                balance = c.OpeningBalance + (c.LedgerEntries
                    .Where(l => !l.IsDeleted)
                    .Sum(l => (decimal?)(l.Debit - l.Credit)) ?? 0)
            })
            .ToListAsync();

        // PVC products are billed through the separate PVC billing module.
        ViewBag.ProductsJson = await _db.Products.AsNoTracking()
            .Where(p => !p.Category.IsPvc)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                id = p.Id,
                name = p.Name + " (" + p.Category.Name + ", " + p.Color.Name + ", G" + p.Gauge.Name + ")",
                product = p.Name,
                category = p.Category.Name,
                color = p.Color.Name,
                gauge = p.Gauge.Name,
                mode = p.PricingMode == PricingMode.PerFoot ? "PerFoot" : "PerUnit",
                price = p.Price,
                pieces = p.StockPieces
                    .Where(s => !s.IsDeleted && s.Quantity > 0)
                    .OrderBy(s => s.LengthFt)
                    .Select(s => new { len = s.LengthFt, qty = s.Quantity })
                    .ToList()
            })
            .ToListAsync();
    }
}
