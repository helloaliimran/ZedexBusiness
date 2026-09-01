using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Api.DTOs.Bills;
using Zedex.Api.DTOs.Common;
using Zedex.Api.Extensions;
using Zedex.Domain.Entities;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;

namespace Zedex.Api.Controllers;

[ApiController]
[Route("api/bills")]

[Produces("application/json")]
public class BillsController : ControllerBase
{
    private readonly AppDbContext _db;

    public BillsController(AppDbContext db) => _db = db;

    // ── GET /api/bills ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a paginated list of posted invoices, newest first.
    /// Query params:
    ///   type       = "standard" | "pvc" | (omit = both)
    ///   customerId = filter to one customer
    ///   search     = matches invoice number or customer name (case-insensitive)
    ///   from / to  = date range (inclusive, local date)
    ///   page / pageSize = pagination (pageSize max 100)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<BillListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBills(
        [FromQuery] string?   type,
        [FromQuery] int?      customerId,
        [FromQuery] string?   search,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int       page     = 1,
        [FromQuery] int       pageSize = 20)
    {
        if (!User.HasModule(AppModule.Billing)) return Forbid();

        pageSize = Math.Clamp(pageSize, 1, 100);
        page     = Math.Max(1, page);

        var query = _db.Invoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .Where(i => i.IsPosted);

        // Invoice type filter
        if (type?.Trim().ToLower() == "standard")
            query = query.Where(i => i.InvoiceType == InvoiceType.Standard);
        else if (type?.Trim().ToLower() == "pvc")
            query = query.Where(i => i.InvoiceType == InvoiceType.Pvc);

        if (customerId.HasValue)
            query = query.Where(i => i.CustomerId == customerId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(i =>
                i.InvoiceNumber.ToLower().Contains(term) ||
                i.Customer.Name.ToLower().Contains(term));
        }

        if (from.HasValue)
            query = query.Where(i => i.InvoiceDate >= from.Value.Date);

        if (to.HasValue)
            query = query.Where(i => i.InvoiceDate < to.Value.Date.AddDays(1));

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new BillListItemDto
            {
                InvoiceId       = i.Id,
                InvoiceNumber   = i.InvoiceNumber,
                InvoiceType     = i.InvoiceType.ToString(),
                CustomerId      = i.CustomerId,
                CustomerName    = i.Customer.Name,
                InvoiceDate     = i.InvoiceDate,
                SubTotal        = i.SubTotal,
                Discount        = i.Discount,
                FurtherDiscount = i.FurtherDiscount,
                Total           = i.Total,
                PaidAmount      = i.PaidAmount,
                PaymentType     = i.PaymentType.HasValue
                                      ? i.PaymentType.Value.ToString()
                                      : null
            })
            .ToListAsync();

        return Ok(new PagedResult<BillListItemDto>
        {
            Items      = items,
            Page       = page,
            PageSize   = pageSize,
            TotalCount = totalCount
        });
    }

    // ── GET /api/bills/{id} ───────────────────────────────────────────────────

    /// <summary>
    /// Returns full detail for a single invoice (draft or posted).
    /// Standard invoices → Items list is populated; PvcItems is empty.
    /// PVC invoices      → PvcItems is populated; Items is empty.
    /// Both include the Returns list and a TotalReturned sum.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BillDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [Authorize]
    public async Task<IActionResult> GetBill(int id)
    {
        if (!User.HasModule(AppModule.Billing)) return Forbid();

        var invoice = await LoadFullInvoiceAsync(id);
        if (invoice is null)
            return NotFound(new { message = "Invoice not found." });

        return Ok(MapToDetailDto(invoice));
    }

    /// <summary>
    /// Returns full detail for a single invoice (draft or posted).
    /// Standard invoices → Items list is populated; PvcItems is empty.
    /// PVC invoices      → PvcItems is populated; Items is empty.
    /// Both include the Returns list and a TotalReturned sum.
    /// </summary>
    [HttpGet("{invoicenumber}")]
    [ProducesResponseType(typeof(BillDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [AllowAnonymous]
    public async Task<IActionResult> GetBillByInvoice(string invoicenumber)
    {
        //if (!User.HasModule(AppModule.Billing)) return Forbid();

        var invoice = await LoadFullInvoicebyNumberAsync(invoicenumber);
        if (invoice is null)
            return NotFound(new { message = "Invoice not found." });

        return Ok(MapToDetailDto(invoice));
    }

    // ── POST /api/bills ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new draft bill (Standard or PVC, decided automatically from the
    /// products referenced by Items) and returns its full detail, including the new
    /// InvoiceId/InvoiceNumber. This only saves a draft — stock and the customer
    /// ledger are untouched, exactly like Invoices/Create and PvcInvoices/Create in
    /// the web app. Posting is a separate step not covered here.
    /// </summary>
    [HttpPost]
    
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [AllowAnonymous]
    public async Task<IActionResult> CreateBill([FromBody] BillSaveRequestDto request)
    {
       // if (!User.HasModule(AppModule.Billing)) return Forbid();

        var (errors, products, isPvc) = await ValidateHeaderAndProductsAsync(request);
        if (errors.Count > 0) return BadRequest(new { errors });

        var gasKitRate = isPvc ? await GetGasKitRateAsync() : 0m;
        
        if (errors.Count > 0) return BadRequest(new { errors });

        var invoice = new Invoice
        {
            InvoiceNumber = await GenerateInvoiceNumberAsync(isPvc, DateTime.Now),
            InvoiceType   = isPvc ? InvoiceType.Pvc : InvoiceType.Standard,
            CustomerId    = request.CustomerId,
            InvoiceDate   = DateTime.Now,
            Remarks       = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
            IsPosted      = false
        };

        if (isPvc) {}
        else       BuildStandardItems(invoice, request, products);

        _db.Invoices.Add(invoice);
        // Retry on the (rare) duplicate-number race when two callers save simultaneously.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                invoice.InvoiceNumber = await GenerateInvoiceNumberAsync(isPvc, DateTime.Now);
            }
        }

        var dto = MapToDetailDto((await LoadFullInvoiceAsync(invoice.Id))!);
        return Ok(invoice.InvoiceNumber);
    }

    // ── PUT /api/bills/{id} ───────────────────────────────────────────────────

    /// <summary>
    /// Updates an existing draft bill by merging the submitted line items with the
    /// invoice's current lines, keyed on <see cref="BillItemUpdateDto.BillItemId"/>:
    ///   • BillItemId null or 0  → a NEW line is added.
    ///   • BillItemId &gt; 0      → that existing line is UPDATED (must belong to this
    ///     invoice and be a standard line, otherwise the request is rejected).
    ///   • Existing non-deleted line whose id is NOT in the request → REMOVED.
    /// Line discounts are recomputed and invoice totals/subtotal are refreshed.
    /// FurtherDiscount is reset to 0. Posted bills cannot be edited (409).
    /// NOTE: Only Standard (non-PVC) bills are editable via this endpoint for now;
    /// PVC bills are rejected with 400 — their merge logic will be added later.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [AllowAnonymous]
    public async Task<IActionResult> UpdateBill(int id, [FromBody] BillSaveRequestDto request)
    {
        //if (!User.HasModule(AppModule.Billing)) return Forbid();

        var invoice = await _db.Invoices
            .Include(i => i.Items)
                .ThenInclude(ii => ii.Product)
                    .ThenInclude(p => p.Category)
            .Include(i => i.PvcItems)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (invoice is null)
            return NotFound(new { message = "Invoice not found." });
        if (invoice.IsPosted)
            return Conflict(new { message = $"Invoice {invoice.InvoiceNumber} is posted and can no longer be edited." });

        // Standard-only for now — PVC bill editing is scheduled for a later task.
        if (invoice.InvoiceType == InvoiceType.Pvc)
            return BadRequest(new { message = "Editing PVC bills is not supported yet." });

        // Validate the header + products and make sure the bill stays Standard.
        // Customer is optional on update — the invoice keeps its existing customer.
        var (errors, products, isPvc) = await ValidateHeaderAndProductsAsync(request, validateCustomer: false);
        if (isPvc)
            errors.Add("Invoice is a Standard bill — it cannot contain PVC products.");
        if (errors.Count > 0)
            return BadRequest(new { errors });

        // Validate every provided BillItemId refers to an existing non-deleted
        // Standard line of THIS invoice, and collect the ids being kept.
        var keptItemIds = new HashSet<int>();
        foreach (var line in request.Items)
        {
            if (line.BillItemId is null || line.BillItemId <= 0)
                continue; // new line

            var idOf = line.BillItemId.Value;
            if (!invoice.Items.Any(x => !x.IsDeleted && x.Id == idOf))
                errors.Add($"Line item {idOf} does not belong to this invoice.");
            else
                keptItemIds.Add(idOf);
        }
        if (errors.Count > 0)
            return BadRequest(new { errors });

        // 1) Remove existing standard lines whose id is missing from the request.
        foreach (var old in invoice.Items.Where(x => !x.IsDeleted && !keptItemIds.Contains(x.Id)).ToList())
            _db.InvoiceItems.Remove(old);

        // 2) Add new lines / update existing lines.
        ApplyStandardItemMerge(invoice, request.Items, products);

        // 3) Refresh invoice totals. FurtherDiscount is reset to 0 on every edit.
        invoice.FurtherDiscount = 0;
        RecomputeStandardTotals(invoice);

        await _db.SaveChangesAsync();

        var dto = MapToDetailDto((await LoadFullInvoiceAsync(invoice.Id))!);
        return Ok(dto.InvoiceNumber);
    }

    // ── Helpers: loading / mapping ───────────────────────────────────────────
    [Authorize]
    private Task<Invoice?> LoadFullInvoiceAsync(int id) =>
        _db.Invoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Items)
                .ThenInclude(ii => ii.Product)
            .Include(i => i.PvcItems)
                .ThenInclude(pi => pi.Product)
                    .ThenInclude(p => p.Company)
            .Include(i => i.Returns)
            .FirstOrDefaultAsync(i => i.Id == id);
    [AllowAnonymous]
    private Task<Invoice?> LoadFullInvoicebyNumberAsync(string invoicenumber) =>
       _db.Invoices
           .AsNoTracking()
           .Include(i => i.Customer)
           .Include(i => i.Items)
               .ThenInclude(ii => ii.Product)
           .Include(i => i.PvcItems)
               .ThenInclude(pi => pi.Product)
                   .ThenInclude(p => p.Company)
           .Include(i => i.Returns)
           .FirstOrDefaultAsync(i => i.InvoiceNumber == invoicenumber);
    [Authorize]
    private static BillDetailDto MapToDetailDto(Invoice invoice)
    {
        var dto = new BillDetailDto
        {
            InvoiceId       = invoice.Id,
            InvoiceNumber   = invoice.InvoiceNumber,
            InvoiceType     = invoice.InvoiceType.ToString(),
            InvoiceDate     = invoice.InvoiceDate,
            IsPosted        = invoice.IsPosted,
            PostedDate      = invoice.PostedDate,
            Remarks         = invoice.Remarks,
            CustomerId      = invoice.CustomerId,
            CustomerName    = invoice.Customer.Name,
            CustomerPhone   = invoice.Customer.Phone,
            CustomerAddress = invoice.Customer.Address,
            SubTotal        = invoice.SubTotal,
            Discount        = invoice.Discount,
            FurtherDiscount = invoice.FurtherDiscount,
            Total           = invoice.Total,
            PaidAmount      = invoice.PaidAmount,
            PaymentType     = invoice.PaymentType?.ToString(),
            TotalReturned   = invoice.Returns.Sum(r => r.TotalAmount)
        };

        if (invoice.InvoiceType == InvoiceType.Standard)
        {
            dto.Items = invoice.Items
                .Where(ii => !ii.IsDeleted)
                .OrderBy(ii => ii.Id)
                .Select(ii => new StandardLineItemDto
                {
                    ItemId          = ii.Id,
                    ProductId       = ii.ProductId,
                    ProductName     = ii.Product.Name,
                    PricingMode     = ii.Product.PricingMode.ToString(),
                    Quantity        = ii.Quantity,
                    SizeFt          = ii.SizeFt,
                    TotalFeet       = ii.TotalFeet,
                    CutFromLengthFt = ii.CutFromLengthFt,
                    Rate            = ii.Rate,
                    DiscountPercent = ii.DiscountPercent,
                    Discount        = ii.Discount,
                    LineTotal       = ii.LineTotal,
                    ReturnedQty     = ii.ReturnedQuantity
                }).ToList();
        }
        else
        {
            dto.PvcItems = invoice.PvcItems
                .Where(pi => !pi.IsDeleted)
                .OrderBy(pi => pi.Id)
                .Select(pi => new PvcLineItemDto
                {
                    ItemId          = pi.Id,
                    ProductId       = pi.ProductId,
                    ProductName     = pi.Product.Name,
                    CompanyName     = pi.Product.Company?.Name,
                    LengthFt        = pi.LengthFt,
                    Quantity        = pi.Quantity,
                    SaleType        = pi.SaleType.ToString(),
                    Rate            = pi.Rate,
                    WeightPerLength = pi.WeightPerLength,
                    TotalWeight     = pi.TotalWeight,
                    TotalFeet       = pi.TotalFeet,
                    LengthsAmount   = pi.LengthsAmount,
                    GasKitType      = pi.GasKitType.ToString(),
                    GasKitAmount    = pi.GasKitAmount,
                    DiscountPercent = pi.DiscountPercent,
                    Discount        = pi.Discount,
                    LineTotal       = pi.LineTotal,
                    ReturnedQty     = pi.ReturnedQuantity
                }).ToList();
        }

        dto.Returns = invoice.Returns
            .Where(r => !r.IsDeleted)
            .OrderByDescending(r => r.ReturnDate)
            .Select(r => new ReturnSummaryDto
            {
                ReturnId     = r.Id,
                ReturnNumber = r.ReturnNumber,
                ReturnDate   = r.ReturnDate,
                TotalAmount  = r.TotalAmount,
                Remarks      = r.Remarks
            }).ToList();

        return dto;
    }

    // ── Helpers: validation ───────────────────────────────────────────────────
    [Authorize]
    /// <summary>
    /// Validates the header (customer/date/non-empty items) and resolves every
    /// referenced ProductId. Also decides Standard vs PVC from the products found —
    /// a bill mixing the two is rejected here, before any line-level math runs.
    /// </summary>
    private async Task<(List<string> Errors, Dictionary<int, Product> Products, bool IsPvc)>
        ValidateHeaderAndProductsAsync(BillSaveRequestDto request, bool validateCustomer = true)
    {
        var errors = new List<string>();

        if (request.Items is null || request.Items.Count == 0)
            errors.Add("Add at least one item.");
        if (validateCustomer &&
            (request.CustomerId <= 0 || !await _db.Customers.AnyAsync(c => c.Id == request.CustomerId)))
            errors.Add("Please select a valid customer.");
        var products = new Dictionary<int, Product>();
        var isPvc = false;
        if (errors.Count == 0)
        {
            var productIds = request.Items!.Select(x => x.ProductId).Distinct().ToList();
            products = await _db.Products
                .Include(p => p.Category)
                .Include(p => p.Color)
                .Include(p => p.Gauge)
                .Include(p => p.Company)
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            for (var i = 0; i < request.Items.Count; i++)
                if (!products.ContainsKey(request.Items[i].ProductId))
                    errors.Add($"Line {i + 1}: product {request.Items[i].ProductId} not found.");

            var distinctTypes = products.Values.Select(p => p.Category.IsPvc).Distinct().ToList();
            if (distinctTypes.Count > 1)
                errors.Add("A single bill cannot mix Standard and PVC products — create separate bills.");
            else
                isPvc = distinctTypes.Count == 1 && distinctTypes[0];
        }

        return (errors, products, isPvc);
    }
  
    // ── Helpers: building line items (assumes validation already passed) ────────
    [Authorize]
    private static void BuildStandardItems(
        Invoice invoice, BillSaveRequestDto request, Dictionary<int, Product> products)
    {
        decimal subTotal = 0, totalDiscount = 0;
        foreach (var line in request.Items)
        {
            var product = products[line.ProductId];
            var perFoot = product.PricingMode == PricingMode.PerFoot;
            var quantity = line.Quantity;
            var rate = product.Price;
            var totalFeet = perFoot ? quantity * (line.SizeFt ?? 0) : (decimal?)null;
            var gross = perFoot ? (totalFeet ?? 0) * rate : quantity * rate;

            // The caller-supplied (possibly rounded) line total is authoritative;
            // discount amount and percentage are derived from it.
            var net = Math.Round(gross * (1 - (line.DiscountPercent ?? 0) / 100m), 2);
            net = Math.Clamp(net, 0, gross);
            var discountAmount = gross - net;
            var discountPercent = gross > 0 ? Math.Round(discountAmount / gross * 100m, 2) : 0;

            invoice.Items.Add(new InvoiceItem
            {
                ProductId       = product.Id,
                Quantity        = quantity,
                SizeFt          = perFoot ? line.SizeFt : null,
                TotalFeet       = totalFeet,
                CutFromLengthFt = null,
                Rate            = rate,
                DiscountPercent = discountPercent,
                Discount        = discountAmount,
                LineTotal       = net
            });
            subTotal += gross;
            totalDiscount += discountAmount;
        }

        var furtherDiscount =  0;
        invoice.SubTotal        = subTotal;
        invoice.Discount        = totalDiscount;
        invoice.FurtherDiscount = furtherDiscount;
        invoice.Total           = subTotal - totalDiscount - furtherDiscount;
    }

    // ── Helpers: merging standard line items on edit ──────────────────────────

    /// <summary>
    /// Merges the submitted lines into the invoice's standard line set. Each line is
    /// either a new item (BillItemId null/0) or an update to an existing line
    /// (BillItemId set — already validated to belong to this invoice). Callers are
    /// responsible for removing lines whose id is absent from the request beforehand.
    /// </summary>
    private static void ApplyStandardItemMerge(
        Invoice invoice, List<BillItemUpdateDto> lines, Dictionary<int, Product> products)
    {
        foreach (var line in lines)
        {
            var product = products[line.ProductId];

            if (line.BillItemId is null || line.BillItemId <= 0)
            {
                var target = new InvoiceItem { InvoiceId = invoice.Id };
                ApplyStandardLine(target, product, line);
                invoice.Items.Add(target);
            }
            else
            {
                var target = invoice.Items.FirstOrDefault(x => !x.IsDeleted && x.Id == line.BillItemId.Value);
                if (target is not null)
                    ApplyStandardLine(target, product, line);
            }
        }
    }

    /// <summary>
    /// (Re)computes a standard line's price, discount and totals from the product and
    /// the submitted Quantity / SizeFt / DiscountPercent, then writes them onto the
    /// given target item. The caller-supplied discount % is authoritative; the stored
    /// discount % is derived back from the resulting amount, matching BuildStandardItems.
    /// </summary>
    private static void ApplyStandardLine(InvoiceItem target, Product product, BillItemUpdateDto line)
    {
        var perFoot  = product.PricingMode == PricingMode.PerFoot;
        var quantity = line.Quantity;
        var rate     = product.Price;
        var totalFeet = perFoot ? quantity * (line.SizeFt ?? 0) : (decimal?)null;
        var gross     = perFoot ? (totalFeet ?? 0) * rate : quantity * rate;

        var net = Math.Round(gross * (1 - (line.DiscountPercent ?? 0) / 100m), 2);
        net = Math.Clamp(net, 0, gross);
        var discountAmount   = gross - net;
        var discountPercent  = gross > 0 ? Math.Round(discountAmount / gross * 100m, 2) : 0;

        target.ProductId       = product.Id;
        target.Quantity        = quantity;
        target.SizeFt          = perFoot ? line.SizeFt : null;
        target.TotalFeet       = totalFeet;
        target.CutFromLengthFt = null;   // cut source is resolved at posting, not billing
        target.Rate            = rate;
        target.DiscountPercent = discountPercent;
        target.Discount        = discountAmount;
        target.LineTotal       = net;
    }

    /// <summary>
    /// Recomputes the standard invoice header totals from its (already merged) lines:
    /// SubTotal = Σ (line gross = LineTotal + Discount), Discount = Σ line discounts,
    /// Total = SubTotal − Discount − FurtherDiscount.
    /// </summary>
    private static void RecomputeStandardTotals(Invoice invoice)
    {
        var subTotal       = invoice.Items.Where(x => !x.IsDeleted).Sum(x => x.LineTotal + x.Discount);
        var totalDiscount  = invoice.Items.Where(x => !x.IsDeleted).Sum(x => x.Discount);
        invoice.SubTotal        = subTotal;
        invoice.Discount        = totalDiscount;
        invoice.Total           = subTotal - totalDiscount - invoice.FurtherDiscount;
    }

    // ── Helpers: misc ─────────────────────────────────────────────────────────

    private static bool TryResolveGasKitType(string? raw, GasKitType fallback, out GasKitType resolved)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            resolved = fallback;
            return true;
        }
        return Enum.TryParse(raw, true, out resolved);
    }

    private static int GasKitMultiplier(GasKitType type) => type switch
    {
        GasKitType.Single => 1,
        GasKitType.Double => 2,
        _ => 0
    };

    private async Task<decimal> GetGasKitRateAsync()
    {
        var setting = await _db.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == AppSetting.Keys.GasKitRatePerFt);
        return setting is not null && decimal.TryParse(setting.Value, out var rate) ? rate : 0m;
    }

    /// <summary>
    /// Standard bills: INV-yyyyMMdd-#### — NOTE this counts ALL invoices (Standard and
    /// PVC) dated that day, matching Zedex.Web's InvoicesController exactly (a
    /// pre-existing quirk carried over intentionally, not a bug introduced here).
    /// PVC bills: PVC-yyyyMMdd-#### — counts only PVC invoices dated that day.
    /// </summary>
    private async Task<string> GenerateInvoiceNumberAsync(bool isPvc, DateTime date)
    {
        var day = date.Date;
        if (isPvc)
        {
            var count = await _db.Invoices.IgnoreQueryFilters()
                .CountAsync(i => i.InvoiceType == InvoiceType.Pvc
                    && i.InvoiceDate >= day && i.InvoiceDate < day.AddDays(1));
            return $"PVC-{day:yyyyMMdd}-{count + 1:D4}";
        }
        else
        {
            var count = await _db.Invoices.IgnoreQueryFilters()
                .CountAsync(i => i.InvoiceDate >= day && i.InvoiceDate < day.AddDays(1));
            return $"INV-{day:yyyyMMdd}-{count + 1:D4}";
        }
    }
}
