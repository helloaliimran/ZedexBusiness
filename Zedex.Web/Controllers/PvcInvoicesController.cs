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
/// PVC billing. Shares the Invoice header (InvoiceType = Pvc) so invoice numbers,
/// payments, and the customer ledger work exactly like standard billing, but uses
/// PvcInvoiceItem lines and these separate views. Same draft → post lifecycle:
/// posting deducts whole stock pieces (no cutting) and writes ledger entries.
/// </summary>
[Authorize(Policy = "Module:Billing")]
public class PvcInvoicesController : Controller
{
    private const int PageSize = 10;
    private readonly AppDbContext _db;

    public PvcInvoicesController(AppDbContext db) => _db = db;

    // ---------- Listing ----------

    public async Task<IActionResult> Index(
        string? search, DateTime? from, DateTime? to, PaymentType? type, bool? posted, int page = 1)
    {
        var query = _db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceType == InvoiceType.Pvc);

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

    public Task<IActionResult> Print(int id) => RenderDetails(id, "Print");

    public Task<IActionResult> Small(int id) => RenderDetails(id, "Small");

    private async Task<IActionResult> RenderDetails(int id, string viewName)
    {
        var invoice = await _db.Invoices.AsNoTracking()
            .Where(i => i.Id == id && i.InvoiceType == InvoiceType.Pvc)
            .Select(i => new PvcInvoiceDetailsViewModel
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
                Rows = i.PvcItems.Where(x => !x.IsDeleted).Select(x => new PvcInvoiceRowViewModel
                {
                    // Item name: section + gauge + company + color.
                    Product = x.Product.Name
                        + " G" + x.Product.Gauge.Name
                        + (x.Product.Company != null ? " " + x.Product.Company.Name : "")
                        + " " + x.Product.Color.Name,
                    Company = x.Product.Company != null ? x.Product.Company.Name : "",
                    Item = x.Product.Name
                        + " G" + x.Product.Gauge.Name
                        + " " + x.Product.Color.Name,
                    Code = x.Product.Description ?? "",
                    SaleType = x.SaleType,
                    LengthFt = x.LengthFt,
                    Quantity = (int)x.Quantity,
                    WeightPerLength = x.WeightPerLength,
                    TotalWeight = x.TotalWeight,
                    TotalFeet = x.TotalFeet,
                    Rate = x.Rate,
                    LengthsAmount = x.LengthsAmount,
                    DiscountPercent = x.DiscountPercent,
                    Discount = x.Discount,
                    GasKitType = x.GasKitType,
                    GasKitRatePerFt = x.GasKitRatePerFt,
                    GasKitAmount = x.GasKitAmount,
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

        var title = await GetStringSettingAsync(AppSetting.Keys.PvcPrintTitle);
        if (!string.IsNullOrWhiteSpace(title))
            invoice.PrintTitle = title;

        return View(viewName, invoice);
    }

    // ---------- Create / Edit drafts ----------

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadLookupsAsync();
        return View(new PvcInvoiceFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PvcInvoiceFormViewModel vm)
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
            InvoiceType = InvoiceType.Pvc,
            CustomerId = vm.CustomerId,
            InvoiceDate = vm.InvoiceDate.Date,
            Remarks = vm.Remarks?.Trim(),
            IsPosted = false
        };
        BuildItems(invoice, vm, products, await GetGasKitRateAsync());

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

        TempData["Success"] = $"PVC invoice {invoice.InvoiceNumber} saved as draft — post it to update stock and the customer ledger.";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    /// <summary>Opens the Create form pre-filled from an existing PVC invoice
    /// (draft or posted) — for repeat bills that differ only slightly. Nothing is
    /// saved until the user submits, which creates a fresh draft with a new number.</summary>
    [HttpGet]
    public async Task<IActionResult> Copy(int id)
    {
        var source = await _db.Invoices.AsNoTracking()
            .Include(i => i.PvcItems)
            .FirstOrDefaultAsync(i => i.Id == id && i.InvoiceType == InvoiceType.Pvc);
        if (source is null)
            return NotFound();

        var vm = new PvcInvoiceFormViewModel
        {
            CustomerId = source.CustomerId,
            InvoiceDate = DateTime.Today,
            Remarks = source.Remarks,
            FurtherDiscount = source.FurtherDiscount,
            Items = source.PvcItems.Where(x => !x.IsDeleted).Select(x => new PvcInvoiceLineFormViewModel
            {
                ProductId = x.ProductId,
                LengthFt = x.LengthFt,
                Quantity = (int)x.Quantity,
                WeightPerLength = x.WeightPerLength,
                Rate = x.Rate,
                DiscountPercent = x.DiscountPercent,
                GasKitType = x.GasKitType,
                LineTotal = x.LineTotal
            }).ToList()
        };

        TempData["Success"] = $"Copied from {source.InvoiceNumber} — adjust the lines and save to create a new draft.";
        await LoadLookupsAsync();
        return View("Create", vm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var invoice = await _db.Invoices.AsNoTracking()
            .Include(i => i.PvcItems)
            .FirstOrDefaultAsync(i => i.Id == id && i.InvoiceType == InvoiceType.Pvc);
        if (invoice is null)
            return NotFound();
        if (invoice.IsPosted)
        {
            TempData["Error"] = $"PVC invoice {invoice.InvoiceNumber} is posted and can no longer be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var vm = new PvcInvoiceFormViewModel
        {
            Id = invoice.Id,
            CustomerId = invoice.CustomerId,
            InvoiceDate = invoice.InvoiceDate,
            Remarks = invoice.Remarks,
            FurtherDiscount = invoice.FurtherDiscount,
            Items = invoice.PvcItems.Where(x => !x.IsDeleted).Select(x => new PvcInvoiceLineFormViewModel
            {
                ProductId = x.ProductId,
                LengthFt = x.LengthFt,
                Quantity = (int)x.Quantity,
                WeightPerLength = x.WeightPerLength,
                Rate = x.Rate,
                DiscountPercent = x.DiscountPercent,
                GasKitType = x.GasKitType,
                LineTotal = x.LineTotal
            }).ToList()
        };
        ViewBag.InvoiceNumber = invoice.InvoiceNumber;
        await LoadLookupsAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PvcInvoiceFormViewModel vm)
    {
        var invoice = await _db.Invoices
            .Include(i => i.PvcItems)
            .FirstOrDefaultAsync(i => i.Id == vm.Id && i.InvoiceType == InvoiceType.Pvc);
        if (invoice is null)
            return NotFound();
        if (invoice.IsPosted)
        {
            TempData["Error"] = $"PVC invoice {invoice.InvoiceNumber} is posted and can no longer be edited.";
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

        foreach (var old in invoice.PvcItems.Where(x => !x.IsDeleted).ToList())
            _db.PvcInvoiceItems.Remove(old);
        BuildItems(invoice, vm, products, await GetGasKitRateAsync());

        await _db.SaveChangesAsync();
        TempData["Success"] = $"PVC invoice {invoice.InvoiceNumber} updated (still a draft).";
        return RedirectToAction(nameof(Details), new { id = invoice.Id });
    }

    // ---------- Post (stock deduction + ledger) ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(PostInvoiceViewModel vm)
    {
        var invoice = await _db.Invoices
            .Include(i => i.PvcItems.Where(x => !x.IsDeleted))
            .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(i => i.Id == vm.Id && i.InvoiceType == InvoiceType.Pvc);
        if (invoice is null)
            return NotFound();
        if (invoice.IsPosted)
        {
            TempData["Error"] = $"PVC invoice {invoice.InvoiceNumber} is already posted.";
            return RedirectToAction(nameof(Details), new { id = vm.Id });
        }
        if (invoice.PvcItems.Count == 0)
        {
            TempData["Error"] = "Cannot post an invoice with no items.";
            return RedirectToAction(nameof(Details), new { id = vm.Id });
        }

        // Resolve paid amount from payment method (same rules as standard billing).
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

        // ---- Stock deduction: whole pieces only (no cutting) ----
        foreach (var item in invoice.PvcItems)
        {
            item.Product.CurrentStock -= item.LengthFt * item.Quantity;
            await AdjustPiecesAsync(item.ProductId, item.LengthFt, -(int)item.Quantity);
        }

        // ---- Ledger entries (identical shape to standard billing) ----
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

        TempData["Success"] = $"PVC invoice {invoice.InvoiceNumber} posted — stock and customer ledger updated.";
        return RedirectToAction(nameof(Details), new { id = vm.Id });
    }

    // ---------- Delete ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var invoice = await _db.Invoices
            .Include(i => i.PvcItems.Where(x => !x.IsDeleted))
            .ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(i => i.Id == id && i.InvoiceType == InvoiceType.Pvc);
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
                TempData["Error"] = $"PVC invoice {invoice.InvoiceNumber} has returns against it and cannot be deleted.";
                return RedirectToAction(nameof(Index));
            }

            // Reverse stock and ledger (mirror of Post).
            foreach (var item in invoice.PvcItems)
            {
                item.Product.CurrentStock += item.LengthFt * item.Quantity;
                await AdjustPiecesAsync(item.ProductId, item.LengthFt, (int)item.Quantity);
            }

            var ledgerEntries = await _db.LedgerEntries.Where(l => l.InvoiceId == id).ToListAsync();
            _db.LedgerEntries.RemoveRange(ledgerEntries);
        }

        foreach (var item in invoice.PvcItems)
            _db.PvcInvoiceItems.Remove(item);
        _db.Invoices.Remove(invoice); // soft delete
        await _db.SaveChangesAsync();

        TempData["Success"] = invoice.IsPosted
            ? $"Posted PVC invoice {invoice.InvoiceNumber} deleted — stock and ledger reversed."
            : $"Draft PVC invoice {invoice.InvoiceNumber} deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Helpers ----------

    private static int GasKitMultiplier(GasKitType type) => type switch
    {
        GasKitType.Single => 1,
        GasKitType.Double => 2,
        _ => 0
    };

    private async Task<Dictionary<int, Product>> ValidateAsync(PvcInvoiceFormViewModel vm)
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
            .Where(p => productIds.Contains(p.Id) && p.Category.IsPvc)
            .ToDictionaryAsync(p => p.Id);

        var gasKitRate = await GetGasKitRateAsync();
        decimal netSum = 0;
        for (var i = 0; i < vm.Items.Count; i++)
        {
            var line = vm.Items[i];
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                ModelState.AddModelError(string.Empty, $"Line {i + 1}: select a PVC product.");
                continue;
            }
            if (line.Quantity <= 0)
                ModelState.AddModelError(string.Empty, $"Line {i + 1}: quantity must be greater than zero.");
            if ((line.LengthFt ?? 0) <= 0)
                ModelState.AddModelError(string.Empty, $"Line {i + 1}: enter the length size in feet.");
            if (line.Rate < 0)
                ModelState.AddModelError(string.Empty, $"Line {i + 1}: rate cannot be negative.");

            var saleType = product.SaleType ?? PvcSaleType.PerRunningFoot;
            if (saleType == PvcSaleType.WeightPerLength)
            {
                line.WeightPerLength ??= product.WeightPerLength;
                if ((line.WeightPerLength ?? 0) <= 0)
                    ModelState.AddModelError(string.Empty,
                        $"Line {i + 1}: \"{product.Name}\" is weight-based — enter the weight per length (kg).");
            }
            else
            {
                line.WeightPerLength = null;
            }

            var qty = line.Quantity!.Value;
            var length = line.LengthFt ?? 0;
            var lengthsGross = saleType == PvcSaleType.WeightPerLength
                ? (line.WeightPerLength ?? 0) * qty * line.Rate!.Value
                : length * qty * line.Rate!.Value;
            var gasKitAmount = Math.Round(gasKitRate * GasKitMultiplier(line.GasKitType) * length * qty, 2);

            // Line total (incl. gas kit) is authoritative; the lengths part must stay within 0..gross.
            var net = line.LineTotal is not null
                ? line.LineTotal.Value - gasKitAmount
                : Math.Round(lengthsGross * (1 - line.DiscountPercent!.Value / 100m), 2);
            if (net < 0 || net > lengthsGross)
                ModelState.AddModelError(string.Empty,
                    $"Line {i + 1}: line total must be between Rs. {gasKitAmount:N2} and Rs. {lengthsGross + gasKitAmount:N2}.");
            netSum += Math.Clamp(net, 0, lengthsGross) + gasKitAmount;
        }

        if (vm.FurtherDiscount < 0 || vm.FurtherDiscount > netSum)
            ModelState.AddModelError(nameof(vm.FurtherDiscount),
                $"Further discount cannot be negative or exceed the bill amount (Rs. {netSum:N2}).");

        return products;
    }

    private static void BuildItems(
        Invoice invoice, PvcInvoiceFormViewModel vm, Dictionary<int, Product> products, decimal gasKitRate)
    {
        decimal subTotal = 0, totalDiscount = 0;
        foreach (var line in vm.Items)
        {
            var product = products[line.ProductId];
            var saleType = product.SaleType ?? PvcSaleType.PerRunningFoot;
            var weightBased = saleType == PvcSaleType.WeightPerLength;
            var quantity = line.Quantity ?? 0;
            var length = line.LengthFt ?? 0;
            var rate = line.Rate ?? 0;

            var totalFeet = weightBased ? (decimal?)null : length * quantity;
            var totalWeight = weightBased ? (line.WeightPerLength ?? 0) * quantity : (decimal?)null;
            var lengthsGross = weightBased
                ? (totalWeight ?? 0) * rate
                : (totalFeet ?? 0) * rate;

            var multiplier = GasKitMultiplier(line.GasKitType);
            var gasKitAmount = Math.Round(gasKitRate * multiplier * length * quantity, 2);

            // The user-entered (possibly rounded) line total is authoritative;
            // the lengths discount amount and percentage are derived from it.
            var net = line.LineTotal is not null
                ? line.LineTotal.Value - gasKitAmount
                : Math.Round(lengthsGross * (1 - (line.DiscountPercent ?? 0) / 100m), 2);
            net = Math.Clamp(net, 0, lengthsGross);
            var discountAmount = lengthsGross - net;
            var discountPercent = lengthsGross > 0 ? Math.Round(discountAmount / lengthsGross * 100m, 2) : 0;

            invoice.PvcItems.Add(new PvcInvoiceItem
            {
                ProductId = product.Id,
                LengthFt = length,
                Quantity = quantity,
                SaleType = saleType,
                Rate = rate,
                WeightPerLength = weightBased ? line.WeightPerLength : null,
                TotalWeight = totalWeight,
                TotalFeet = totalFeet,
                LengthsAmount = lengthsGross,
                DiscountPercent = discountPercent,
                Discount = discountAmount,
                GasKitType = line.GasKitType,
                GasKitRatePerFt = multiplier > 0 ? gasKitRate : 0,
                GasKitAmount = gasKitAmount,
                LineTotal = net + gasKitAmount
            });
            subTotal += lengthsGross + gasKitAmount;
            totalDiscount += discountAmount;
        }

        var furtherDiscount = vm.FurtherDiscount ?? 0;

        // Header amounts are derived: SubTotal is gross (lengths + gas kits),
        // Discount is Σ line discounts — same semantics as standard billing.
        invoice.SubTotal = subTotal;
        invoice.Discount = totalDiscount;
        invoice.FurtherDiscount = furtherDiscount;
        invoice.Total = subTotal - totalDiscount - furtherDiscount;
    }

    /// <summary>PVC bills get their own per-day sequence: PVC-yyyyMMdd-####.</summary>
    private async Task<string> GenerateInvoiceNumberAsync(DateTime date)
    {
        var day = date.Date;
        var count = await _db.Invoices.IgnoreQueryFilters()
            .CountAsync(i => i.InvoiceType == InvoiceType.Pvc
                && i.InvoiceDate >= day && i.InvoiceDate < day.AddDays(1));
        return $"PVC-{day:yyyyMMdd}-{count + 1:D4}";
    }

    private async Task<string?> GetStringSettingAsync(string key)
    {
        var setting = await _db.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value;
    }

    private async Task<decimal> GetGasKitRateAsync()
    {
        var setting = await _db.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSetting.Keys.GasKitRatePerFt);
        return setting is not null && decimal.TryParse(setting.Value, out var rate) ? rate : 0m;
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

        // PVC products only, with the fields the entry grid needs.
        ViewBag.ProductsJson = await _db.Products.AsNoTracking()
            .Where(p => p.Category.IsPvc)
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                id = p.Id,
                name = p.Name + " G" + p.Gauge.Name
                    + (p.Company != null ? " " + p.Company.Name : "")
                    + " " + p.Color.Name,
                product = p.Name,
                category = p.Category.Name,
                color = p.Color.Name,
                gauge = p.Gauge.Name,
                company = p.Company != null ? p.Company.Name : "",
                saleType = p.SaleType == PvcSaleType.WeightPerLength ? "Weight" : "PerFoot",
                price = p.Price,
                weightPerLength = p.WeightPerLength,
                gasKit = p.GasKitType == GasKitType.Single ? "Single"
                    : p.GasKitType == GasKitType.Double ? "Double" : "None",
                pieces = p.StockPieces
                    .Where(s => !s.IsDeleted && s.Quantity > 0)
                    .OrderBy(s => s.LengthFt)
                    .Select(s => new { len = s.LengthFt, qty = s.Quantity })
                    .ToList()
            })
            .ToListAsync();

        ViewBag.GasKitRate = await GetGasKitRateAsync();
    }
}
