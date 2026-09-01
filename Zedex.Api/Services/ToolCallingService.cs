using Microsoft.EntityFrameworkCore;
using Zedex.Api.DTOs.Bills;
using Zedex.Api.DTOs.Lookups;
using Zedex.Api.DTOs.Products;
using Zedex.Api.DTOs.Tools;
using Zedex.Domain.Entities;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;

namespace Zedex.Api.Services;

public class ToolCallingService : IToolCallingService
{
    private readonly AppDbContext _db;

    public ToolCallingService(AppDbContext db) => _db = db;

    // ── Search product ───────────────────────────────────────────────────────

    public async Task<List<ProductSearchGroupDto>> SearchProductsAsync(List<ProductSearchRequestDto> items)
    {
        var results = new List<ProductSearchGroupDto>(items.Count);

        for (int index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var nameTerm = item.ProductName.Trim().ToLower();

            var query = _db.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Include(p => p.Color)
                .Include(p => p.Gauge)
                .Include(p => p.Company)
                .Where(p => !p.IsDeleted && p.Name.ToLower().Contains(nameTerm));

            if (!string.IsNullOrWhiteSpace(item.Color))
            {
                var term = item.Color.Trim().ToLower();
                query = query.Where(p => p.Color.Name.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(item.Gauge))
            {
                var term = item.Gauge.Trim().ToLower();
                query = query.Where(p => p.Gauge.Name.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(item.Category))
            {
                var term = item.Category.Trim().ToLower();
                query = query.Where(p => p.Category.Name.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(item.Company))
            {
                var term = item.Company.Trim().ToLower();
                query = query.Where(p => p.Company != null && p.Company.Name.ToLower().Contains(term));
            }

            var matches = await query
                .OrderBy(p => p.Name)
                .Select(p => new ProductSearchResultDto
                {
                    ProductId = p.Id,
                    Name      = p.Name,
                    Color     = p.Color.Name,
                    Gauge     = p.Gauge.Name,
                    Category  = p.Category.Name,
                    Company   = p.Company != null ? p.Company.Name : null,
                    Rate      = p.Price
                })
                .ToListAsync();

            results.Add(new ProductSearchGroupDto
            {
                Index       = index,
                ProductName = item.ProductName,
                Matches     = matches
            });
        }

        return results;
    }

    // ── Lookup ────────────────────────────────────────────────────────────────

    public async Task<AllLookupsDto> GetLookupsAsync()
    {
        var colors = await _db.Colors.AsNoTracking()
            .Where(c => !c.IsDeleted).OrderBy(c => c.Name)
            .Select(c => new LookupItemDto { Id = c.Id, Name = c.Name })
            .ToListAsync();

        var gauges = await _db.Gauges.AsNoTracking()
            .Where(g => !g.IsDeleted).OrderBy(g => g.Name)
            .Select(g => new LookupItemDto { Id = g.Id, Name = g.Name })
            .ToListAsync();

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => !c.IsDeleted).OrderBy(c => c.Name)
            .Select(c => new LookupItemDto { Id = c.Id, Name = c.Name })
            .ToListAsync();

        var companies = await _db.Companies.AsNoTracking()
            .Where(c => !c.IsDeleted).OrderBy(c => c.Name)
            .Select(c => new LookupItemDto { Id = c.Id, Name = c.Name })
            .ToListAsync();

        return new AllLookupsDto
        {
            Colors     = colors,
            Gauges     = gauges,
            Categories = categories,
            Companies  = companies
        };
    }

    // ── Find customer ────────────────────────────────────────────────────────

    public async Task<List<ToolCustomerLookupDto>> FindCustomersAsync(string? search)
    {
        var query = _db.Customers.AsNoTracking().Where(c => !c.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.Name.ToLower().Contains(term) ||
                (c.Phone != null && c.Phone.Contains(search.Trim())));
        }

        return await query
            .OrderBy(c => c.Name)
            .Take(20)
            .Select(c => new ToolCustomerLookupDto
            {
                CustomerId = c.Id,
                Name       = c.Name,
                Phone      = c.Phone
            })
            .ToListAsync();
    }

    // ── Create / update bill (Standard only — PVC out of scope for tools) ──────

    public async Task<ToolSaveBillResult> SaveBillAsync(ToolBillRequestDto request)
    {
        var errors = new List<string>();

        if (request.Items is null || request.Items.Count == 0)
            errors.Add("Add at least one item.");

        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId && !c.IsDeleted);
        if (customer is null)
            errors.Add("Please select a valid customer.");

        var products = new Dictionary<int, Product>();
        if (errors.Count == 0)
        {
            var items = request.Items!;
            var productIds = items.Select(x => x.ProductId).Distinct().ToList();
            products = await _db.Products
                .Include(p => p.Category)
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            for (var i = 0; i < items.Count; i++)
                if (!products.ContainsKey(items[i].ProductId))
                    errors.Add($"Line {i + 1}: product {items[i].ProductId} not found.");

            if (errors.Count == 0 && products.Values.Any(p => p.Category.IsPvc))
                errors.Add("PVC products are not supported by this tool yet — Standard products only.");
        }

        if (errors.Count > 0)
            return new ToolSaveBillResult(false, errors, null);

        var lines = request.Items!;

        Invoice invoice;
        var isNew = request.BillId is null or 0;

        if (isNew)
        {
            invoice = new Invoice
            {
                InvoiceNumber = await GenerateInvoiceNumberAsync(DateTime.Now),
                InvoiceType   = InvoiceType.Standard,
                CustomerId    = customer!.Id,
                InvoiceDate   = DateTime.Now,
                Remarks       = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim(),
                IsPosted      = false
            };

            BuildStandardItems(invoice, lines, products);
            _db.Invoices.Add(invoice);

            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await _db.SaveChangesAsync();
                    break;
                }
                catch (DbUpdateException) when (attempt < 3)
                {
                    invoice.InvoiceNumber = await GenerateInvoiceNumberAsync(DateTime.Now);
                }
            }
        }
        else
        {
            var existing = await _db.Invoices
                .Include(i => i.Items)
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.Id == request.BillId!.Value);

            if (existing is null)
                return new ToolSaveBillResult(false, new List<string> { "Bill not found." }, null);
            if (existing.IsPosted)
                return new ToolSaveBillResult(false, new List<string> { $"Bill {existing.InvoiceNumber} is posted and can no longer be edited." }, null);
            if (existing.InvoiceType == InvoiceType.Pvc)
                return new ToolSaveBillResult(false, new List<string> { "Editing PVC bills is not supported by this tool." }, null);

            var keptItemIds = new HashSet<int>();
            foreach (var line in lines)
            {
                if (line.BillItemId is null or 0) continue;
                if (!existing.Items.Any(x => !x.IsDeleted && x.Id == line.BillItemId.Value))
                    errors.Add($"Line item {line.BillItemId} does not belong to this bill.");
                else
                    keptItemIds.Add(line.BillItemId.Value);
            }
            if (errors.Count > 0)
                return new ToolSaveBillResult(false, errors, null);

            foreach (var old in existing.Items.Where(x => !x.IsDeleted && !keptItemIds.Contains(x.Id)).ToList())
            {
                // Set the domain flag now (not just EF state) so RecomputeStandardTotals,
                // which reads IsDeleted below and runs before SaveChangesAsync, excludes it too.
                old.IsDeleted = true;
                _db.InvoiceItems.Remove(old);
            }

            MergeStandardItems(existing, lines, products);

            existing.CustomerId = customer!.Id;
            existing.Remarks    = string.IsNullOrWhiteSpace(request.Remarks) ? null : request.Remarks.Trim();
            existing.FurtherDiscount = 0;
            RecomputeStandardTotals(existing);

            await _db.SaveChangesAsync();
            invoice = existing;
        }

        // Reload with Customer for the response (create path doesn't have it tracked yet).
        var full = await _db.Invoices.AsNoTracking()
            .Include(i => i.Customer)
            .FirstAsync(i => i.Id == invoice.Id);

        return new ToolSaveBillResult(true, new List<string>(), new ToolBillResultDto
        {
            BillId        = full.Id,
            InvoiceNumber = full.InvoiceNumber,
            WasCreated    = isNew,
            CustomerId    = full.CustomerId,
            CustomerName  = full.Customer.Name,
            Total         = full.Total
        });
    }

    // ── Get bill (by BillId or invoice number — for viewing before an edit) ────

    public async Task<ToolGetBillResult> GetBillAsync(string idOrInvoiceNumber)
    {
        var term = idOrInvoiceNumber.Trim();

        var query = _db.Invoices.AsNoTracking()
            .Include(i => i.Customer)
            .Include(i => i.Items)
                .ThenInclude(ii => ii.Product);

        var invoice = int.TryParse(term, out var id)
            ? await query.FirstOrDefaultAsync(i => i.Id == id)
            : await query.FirstOrDefaultAsync(i => i.InvoiceNumber.ToLower() == term.ToLower());

        if (invoice is null)
            return new ToolGetBillResult(false, "Bill not found.", null);
        if (invoice.InvoiceType == InvoiceType.Pvc)
            return new ToolGetBillResult(false, "PVC bills are not supported by this tool yet.", null);

        var dto = new ToolBillDetailDto
        {
            BillId          = invoice.Id,
            InvoiceNumber   = invoice.InvoiceNumber,
            IsPosted        = invoice.IsPosted,
            CustomerId      = invoice.CustomerId,
            CustomerName    = invoice.Customer.Name,
            Remarks         = invoice.Remarks,
            SubTotal        = invoice.SubTotal,
            Discount        = invoice.Discount,
            FurtherDiscount = invoice.FurtherDiscount,
            Total           = invoice.Total,
            Items = invoice.Items
                .Where(x => !x.IsDeleted)
                .Select(x => new ToolBillItemDto
                {
                    BillItemId      = x.Id,
                    ProductId       = x.ProductId,
                    ProductName     = x.Product.Name,
                    Quantity        = x.Quantity,
                    SizeFt          = x.SizeFt,
                    Rate            = x.Rate,
                    DiscountPercent = x.DiscountPercent,
                    LineTotal       = x.LineTotal
                })
                .ToList()
        };

        return new ToolGetBillResult(true, null, dto);
    }

    // ── Helpers (Standard-only line math — mirrors BillsController) ─────────────

    private static void BuildStandardItems(Invoice invoice, List<BillItemUpdateDto> lines, Dictionary<int, Product> products)
    {
        decimal subTotal = 0, totalDiscount = 0;
        foreach (var line in lines)
        {
            var product = products[line.ProductId];
            var perFoot = product.PricingMode == PricingMode.PerFoot;
            var quantity = line.Quantity;
            var rate = product.Price;
            var totalFeet = perFoot ? quantity * (line.SizeFt ?? 0) : (decimal?)null;
            var gross = perFoot ? (totalFeet ?? 0) * rate : quantity * rate;

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
                Rate            = rate,
                DiscountPercent = discountPercent,
                Discount        = discountAmount,
                LineTotal       = net
            });
            subTotal += gross;
            totalDiscount += discountAmount;
        }

        invoice.SubTotal        = subTotal;
        invoice.Discount        = totalDiscount;
        invoice.FurtherDiscount = 0;
        invoice.Total           = subTotal - totalDiscount;
    }

    private static void MergeStandardItems(Invoice invoice, List<BillItemUpdateDto> lines, Dictionary<int, Product> products)
    {
        foreach (var line in lines)
        {
            var product = products[line.ProductId];

            if (line.BillItemId is null or 0)
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

    private static void ApplyStandardLine(InvoiceItem target, Product product, BillItemUpdateDto line)
    {
        var perFoot  = product.PricingMode == PricingMode.PerFoot;
        var quantity = line.Quantity;
        var rate     = product.Price;
        var totalFeet = perFoot ? quantity * (line.SizeFt ?? 0) : (decimal?)null;
        var gross     = perFoot ? (totalFeet ?? 0) * rate : quantity * rate;

        var net = Math.Round(gross * (1 - (line.DiscountPercent ?? 0) / 100m), 2);
        net = Math.Clamp(net, 0, gross);
        var discountAmount  = gross - net;
        var discountPercent = gross > 0 ? Math.Round(discountAmount / gross * 100m, 2) : 0;

        target.ProductId       = product.Id;
        target.Quantity        = quantity;
        target.SizeFt          = perFoot ? line.SizeFt : null;
        target.TotalFeet       = totalFeet;
        target.CutFromLengthFt = null;
        target.Rate            = rate;
        target.DiscountPercent = discountPercent;
        target.Discount        = discountAmount;
        target.LineTotal       = net;
    }

    private static void RecomputeStandardTotals(Invoice invoice)
    {
        var subTotal      = invoice.Items.Where(x => !x.IsDeleted).Sum(x => x.LineTotal + x.Discount);
        var totalDiscount = invoice.Items.Where(x => !x.IsDeleted).Sum(x => x.Discount);
        invoice.SubTotal = subTotal;
        invoice.Discount = totalDiscount;
        invoice.Total    = subTotal - totalDiscount - invoice.FurtherDiscount;
    }

    /// <summary>INV-yyyyMMdd-#### — counts ALL invoices (Standard and PVC) dated that day,
    /// matching Zedex.Web's and BillsController's numbering exactly.</summary>
    private async Task<string> GenerateInvoiceNumberAsync(DateTime date)
    {
        var day = date.Date;
        var count = await _db.Invoices.IgnoreQueryFilters()
            .CountAsync(i => i.InvoiceDate >= day && i.InvoiceDate < day.AddDays(1));
        return $"INV-{day:yyyyMMdd}-{count + 1:D4}";
    }
}
