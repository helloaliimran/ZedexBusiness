using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Api.DTOs.Bills;
using Zedex.Api.DTOs.Common;
using Zedex.Api.Extensions;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;

namespace Zedex.Api.Controllers;

[ApiController]
[Route("api/bills")]
[Authorize]
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
    /// Returns full detail for a single invoice.
    /// Standard invoices → Items list is populated; PvcItems is empty.
    /// PVC invoices      → PvcItems is populated; Items is empty.
    /// Both include the Returns list and a TotalReturned sum.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(BillDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBill(int id)
    {
        if (!User.HasModule(AppModule.Billing)) return Forbid();

        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Items)
                .ThenInclude(ii => ii.Product)
            .Include(i => i.PvcItems)
                .ThenInclude(pi => pi.Product)
                    .ThenInclude(p => p.Company)
            .Include(i => i.Returns)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (invoice is null)
            return NotFound(new { message = "Invoice not found." });

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

        // Populate Standard line items
        if (invoice.InvoiceType == InvoiceType.Standard)
        {
            dto.Items = invoice.Items
                .OrderBy(ii => ii.Id)
                .Select(ii => new StandardLineItemDto
                {
                    ItemId           = ii.Id,
                    ProductId        = ii.ProductId,
                    ProductName      = ii.Product.Name,
                    PricingMode      = ii.Product.PricingMode.ToString(),
                    Quantity         = ii.Quantity,
                    SizeFt           = ii.SizeFt,
                    TotalFeet        = ii.TotalFeet,
                    CutFromLengthFt  = ii.CutFromLengthFt,
                    Rate             = ii.Rate,
                    DiscountPercent  = ii.DiscountPercent,
                    Discount         = ii.Discount,
                    LineTotal        = ii.LineTotal,
                    ReturnedQty      = ii.ReturnedQuantity
                }).ToList();
        }
        else
        {
            // Populate PVC line items
            dto.PvcItems = invoice.PvcItems
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

        // Returns (newest first)
        dto.Returns = invoice.Returns
            .OrderByDescending(r => r.ReturnDate)
            .Select(r => new ReturnSummaryDto
            {
                ReturnId     = r.Id,
                ReturnNumber = r.ReturnNumber,
                ReturnDate   = r.ReturnDate,
                TotalAmount  = r.TotalAmount,
                Remarks      = r.Remarks
            }).ToList();

        return Ok(dto);
    }
}
