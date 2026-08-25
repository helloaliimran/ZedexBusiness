using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Entities;
using Zedex.Domain.Enums;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;

namespace Zedex.Web.Controllers;

/// <summary>
/// Stock entries follow a draft → post workflow:
/// drafts are freely editable and do NOT affect stock;
/// posting applies quantities to product stock and locks the entry.
/// </summary>
[Authorize(Policy = "Module:Stock")]
public class StockController : Controller
{
    private static readonly string[] AllowedExtensions =
        { ".pdf", ".jpg", ".jpeg", ".png", ".webp", ".xlsx", ".xls", ".docx", ".doc" };
    private const long MaxAttachmentBytes = 5 * 1024 * 1024;
    private const int PageSize = 10;

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public StockController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // ---------- Listing ----------

    public async Task<IActionResult> Index(string? search, DateTime? from, DateTime? to, int page = 1)
    {
        var query = _db.StockHeaders.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(h =>
                (h.ReferenceNumber != null && EF.Functions.ILike(h.ReferenceNumber, pattern)) ||
                (h.Remarks != null && EF.Functions.ILike(h.Remarks, pattern)));
        }
        if (from is not null)
            query = query.Where(h => h.EntryDate >= from.Value.Date);
        if (to is not null)
            query = query.Where(h => h.EntryDate < to.Value.Date.AddDays(1));

        var total = await query.CountAsync();
        page = Math.Max(1, page);
        var items = await query
            .OrderByDescending(h => h.EntryDate).ThenByDescending(h => h.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(h => new StockListItemViewModel
            {
                Id = h.Id,
                EntryDate = h.EntryDate,
                ReferenceNumber = h.ReferenceNumber,
                Remarks = h.Remarks,
                LineCount = h.Details.Count(d => !d.IsDeleted),
                TotalItems = h.Details.Where(d => !d.IsDeleted).Sum(d => d.TotalQuantity),
                HasAttachment = h.AttachmentPath != null,
                IsPosted = h.IsPosted,
                CreatedBy = h.CreatedBy
            })
            .ToListAsync();

        return View(new StockListViewModel
        {
            Search = search,
            From = from,
            To = to,
            Items = new PagedResult<StockListItemViewModel>
            {
                Items = items, Page = page, PageSize = PageSize, TotalCount = total
            }
        });
    }

    public async Task<IActionResult> OnHand(string? search, int page = 1)
    {
        var query = _db.Products.AsNoTracking()
            .Where(p => p.CurrentStock != 0 || p.StockPieces.Any(s => !s.IsDeleted && s.Quantity != 0));

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => EF.Functions.ILike(p.Name, $"%{search.Trim()}%"));

        var total = await query.CountAsync();
        page = Math.Max(1, page);
        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(p => new OnHandItemViewModel
            {
                ProductId = p.Id,
                Name = p.Name,
                Company = p.Company != null ? p.Company.Name : "—",
                Category = p.Category.Name,
                Color = p.Color.Name,
                Gauge = p.Gauge.Name,
                Mode = p.PricingMode,
                CurrentStock = p.CurrentStock,
                IsPvc = p.Category.IsPvc,
                Pieces = p.StockPieces
                    .Where(s => !s.IsDeleted && s.Quantity != 0)
                    .OrderBy(s => s.LengthFt)
                    .Select(s => new OnHandPieceViewModel { LengthFt = s.LengthFt, Quantity = s.Quantity })
                    .ToList()
            })
            .ToListAsync();

        return View(new OnHandViewModel
        {
            Search = search,
            Items = new PagedResult<OnHandItemViewModel>
            {
                Items = items, Page = page, PageSize = PageSize, TotalCount = total
            }
        });
    }

    // ---------- Reset to zero (single product / all) ----------
    // Admin only — force-zeroes stock outside the normal draft → post workflow
    // (stock counts, damage write-offs, etc). Always logged to StockResetLogs.

    [HttpPost]
    [Authorize(Roles = DbSeeder.AdminRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetProduct(int productId, string? remarks)
    {
        var product = await _db.Products
            .Include(p => p.StockPieces.Where(s => !s.IsDeleted))
            .FirstOrDefaultAsync(p => p.Id == productId);
        if (product is null)
            return NotFound();

        if (product.CurrentStock == 0 && !product.StockPieces.Any())
        {
            TempData["Error"] = $"\"{product.Name}\" already has zero stock.";
            return RedirectToAction(nameof(OnHand));
        }

        ResetProductStock(product, remarks?.Trim(), batchId: null);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Stock for \"{product.Name}\" reset to zero.";
        return RedirectToAction(nameof(OnHand));
    }

    [HttpPost]
    [Authorize(Roles = DbSeeder.AdminRole)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetAll(string? remarks)
    {
        var products = await _db.Products
            .Include(p => p.StockPieces.Where(s => !s.IsDeleted))
            .Where(p => p.CurrentStock != 0 || p.StockPieces.Any(s => !s.IsDeleted && s.Quantity != 0))
            .ToListAsync();

        if (products.Count == 0)
        {
            TempData["Error"] = "No products currently have stock to reset.";
            return RedirectToAction(nameof(OnHand));
        }

        var batchId = Guid.NewGuid();
        var trimmedRemarks = remarks?.Trim();
        foreach (var product in products)
            ResetProductStock(product, trimmedRemarks, batchId);

        await _db.SaveChangesAsync();

        TempData["Success"] = $"Stock reset to zero for {products.Count} product(s).";
        return RedirectToAction(nameof(OnHand));
    }

    /// <summary>Zeroes CurrentStock (and, for per-foot/PVC products, clears the piece
    /// breakdown), and stages a StockResetLog row. Does not save — caller batches the
    /// SaveChangesAsync so "reset all" runs as one transaction.</summary>
    private void ResetProductStock(Product product, string? remarks, Guid? batchId)
    {
        var previousStock = product.CurrentStock;
        var piecesCleared = 0;

        if (product.PricingMode == PricingMode.PerFoot)
        {
            piecesCleared = product.StockPieces.Count(s => !s.IsDeleted);
            foreach (var piece in product.StockPieces.Where(s => !s.IsDeleted))
                _db.StockPieces.Remove(piece); // soft-deleted via ApplyAudit
        }

        product.CurrentStock = 0;

        _db.StockResetLogs.Add(new StockResetLog
        {
            ProductId = product.Id,
            ProductName = product.Name,
            PreviousStock = previousStock,
            PiecesCleared = piecesCleared,
            BatchId = batchId,
            Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks
        });
    }

    public async Task<IActionResult> ResetLog(int page = 1)
    {
        var query = _db.StockResetLogs.AsNoTracking();

        var total = await query.CountAsync();
        page = Math.Max(1, page);
        var items = await query
            .OrderByDescending(l => l.CreatedDate).ThenByDescending(l => l.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(l => new StockResetLogItemViewModel
            {
                Id = l.Id,
                ProductId = l.ProductId,
                ProductName = l.ProductName,
                PreviousStock = l.PreviousStock,
                PiecesCleared = l.PiecesCleared,
                BatchId = l.BatchId,
                Remarks = l.Remarks,
                ResetBy = l.CreatedBy,
                ResetDate = l.CreatedDate
            })
            .ToListAsync();

        return View(new StockResetLogListViewModel
        {
            Items = new PagedResult<StockResetLogItemViewModel>
            {
                Items = items, Page = page, PageSize = PageSize, TotalCount = total
            }
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var header = await _db.StockHeaders.AsNoTracking()
            .Where(h => h.Id == id)
            .Select(h => new StockDetailsViewModel
            {
                Id = h.Id,
                EntryDate = h.EntryDate,
                ReferenceNumber = h.ReferenceNumber,
                Remarks = h.Remarks,
                AttachmentPath = h.AttachmentPath,
                CreatedBy = h.CreatedBy,
                CreatedDate = h.CreatedDate,
                IsPosted = h.IsPosted,
                PostedBy = h.PostedBy,
                PostedDate = h.PostedDate,
                Rows = h.Details.Where(d => !d.IsDeleted).Select(d => new StockDetailRowViewModel
                {
                    Product = d.Product.Name
                        + (d.Product.Company != null ? " " + d.Product.Company.Name : "")
                        + " (" + d.Product.Color.Name + ", G" + d.Product.Gauge.Name + ")",
                    Mode = d.Product.PricingMode,
                    Quantity = d.Quantity,
                    Cartons = d.Cartons,
                    ItemsPerCarton = d.ItemsPerCarton,
                    LengthFt = d.LengthFt,
                    TotalQuantity = d.TotalQuantity
                }).ToList()
            })
            .FirstOrDefaultAsync();

        if (header is null)
            return NotFound();
        return View(header);
    }

    // ---------- Create (saves a draft) ----------

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await LoadProductsAsync();
        return View(new StockHeaderFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StockHeaderFormViewModel vm)
    {
        var products = await ValidateLinesAsync(vm);
        var attachmentPath = await ValidateAndSaveAttachmentAsync(vm);

        if (!ModelState.IsValid)
        {
            await LoadProductsAsync();
            return View(vm);
        }

        var header = new StockHeader
        {
            EntryDate = vm.EntryDate.Date,
            ReferenceNumber = vm.ReferenceNumber?.Trim(),
            Remarks = vm.Remarks?.Trim(),
            AttachmentPath = attachmentPath,
            IsPosted = false
        };
        ApplyLines(header, vm, products);

        _db.StockHeaders.Add(header);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Stock entry #{header.Id} saved as draft — post it to update stock.";
        return RedirectToAction(nameof(Details), new { id = header.Id });
    }

    // ---------- Edit (drafts only) ----------

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var header = await _db.StockHeaders.AsNoTracking()
            .Include(h => h.Details)
            .FirstOrDefaultAsync(h => h.Id == id);
        if (header is null)
            return NotFound();
        if (header.IsPosted)
        {
            TempData["Error"] = $"Stock entry #{id} is posted and can no longer be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var vm = new StockHeaderFormViewModel
        {
            Id = header.Id,
            EntryDate = header.EntryDate,
            ReferenceNumber = header.ReferenceNumber,
            Remarks = header.Remarks,
            ExistingAttachmentPath = header.AttachmentPath,
            Details = header.Details.Where(d => !d.IsDeleted).Select(d => new StockDetailFormViewModel
            {
                ProductId = d.ProductId,
                Quantity = d.Quantity,
                Cartons = d.Cartons,
                ItemsPerCarton = d.ItemsPerCarton,
                LengthFt = d.LengthFt
            }).ToList()
        };
        await LoadProductsAsync();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(StockHeaderFormViewModel vm)
    {
        var header = await _db.StockHeaders
            .Include(h => h.Details)
            .FirstOrDefaultAsync(h => h.Id == vm.Id);
        if (header is null)
            return NotFound();
        if (header.IsPosted)
        {
            TempData["Error"] = $"Stock entry #{vm.Id} is posted and can no longer be edited.";
            return RedirectToAction(nameof(Details), new { id = vm.Id });
        }

        var products = await ValidateLinesAsync(vm);
        var newAttachmentPath = await ValidateAndSaveAttachmentAsync(vm);

        if (!ModelState.IsValid)
        {
            vm.ExistingAttachmentPath = header.AttachmentPath;
            await LoadProductsAsync();
            return View(vm);
        }

        header.EntryDate = vm.EntryDate.Date;
        header.ReferenceNumber = vm.ReferenceNumber?.Trim();
        header.Remarks = vm.Remarks?.Trim();
        if (newAttachmentPath is not null)
            header.AttachmentPath = newAttachmentPath;

        // Replace lines: old rows are soft-deleted, new rows added.
        foreach (var old in header.Details.Where(d => !d.IsDeleted).ToList())
            _db.StockDetails.Remove(old);
        ApplyLines(header, vm, products);

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Stock entry #{header.Id} updated (still a draft — post it to update stock).";
        return RedirectToAction(nameof(Details), new { id = header.Id });
    }

    // ---------- Post (applies quantities, locks entry) ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Post(int id)
    {
        var header = await _db.StockHeaders
            .Include(h => h.Details.Where(d => !d.IsDeleted))
            .ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(h => h.Id == id);
        if (header is null)
            return NotFound();
        if (header.IsPosted)
        {
            TempData["Error"] = $"Stock entry #{id} is already posted.";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (header.Details.Count == 0)
        {
            TempData["Error"] = "Cannot post an entry with no lines.";
            return RedirectToAction(nameof(Details), new { id });
        }

        foreach (var detail in header.Details)
        {
            var product = detail.Product;
            if (product.PricingMode == PricingMode.PerFoot)
            {
                if ((detail.LengthFt ?? 0) <= 0)
                {
                    TempData["Error"] = $"\"{product.Name}\" is a per-foot product but the line has no length. Edit the entry first.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                product.CurrentStock += detail.TotalQuantity * detail.LengthFt!.Value;
                await AddPiecesAsync(product.Id, detail.LengthFt.Value, detail.TotalQuantity);
            }
            else
            {
                product.CurrentStock += detail.TotalQuantity;
            }
        }

        header.IsPosted = true;
        header.PostedBy = User.Identity?.Name;
        header.PostedDate = DateTime.Now;
        await _db.SaveChangesAsync(); // single transaction

        TempData["Success"] = $"Stock entry #{id} posted — stock updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ---------- Delete ----------
    // Drafts: any stock user, nothing to reverse.
    // Posted entries: admin only, quantities are reversed.

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var header = await _db.StockHeaders
            .Include(h => h.Details.Where(d => !d.IsDeleted))
            .ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(h => h.Id == id);
        if (header is null)
            return NotFound();

        if (header.IsPosted)
        {
            if (!User.IsInRole(DbSeeder.AdminRole))
            {
                TempData["Error"] = "Only an admin can delete a posted stock entry.";
                return RedirectToAction(nameof(Index));
            }

            foreach (var detail in header.Details)
            {
                var product = detail.Product;
                if (product.PricingMode == PricingMode.PerFoot && detail.LengthFt is not null)
                {
                    product.CurrentStock -= detail.TotalQuantity * detail.LengthFt.Value;
                    await AddPiecesAsync(product.Id, detail.LengthFt.Value, -detail.TotalQuantity, clampAtZero: true);
                }
                else
                {
                    product.CurrentStock -= detail.TotalQuantity;
                }
            }
        }

        foreach (var detail in header.Details)
            _db.StockDetails.Remove(detail);
        _db.StockHeaders.Remove(header); // soft delete
        await _db.SaveChangesAsync();

        TempData["Success"] = header.IsPosted
            ? "Posted stock entry deleted and quantities reversed."
            : "Draft stock entry deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Helpers ----------

    private async Task<Dictionary<int, Product>> ValidateLinesAsync(StockHeaderFormViewModel vm)
    {
        vm.Details = vm.Details.Where(d => d.ProductId > 0 || d.TotalQuantity != 0).ToList();

        if (vm.Details.Count == 0)
            ModelState.AddModelError(string.Empty, "Add at least one stock line.");

        var productIds = vm.Details.Select(d => d.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        for (var i = 0; i < vm.Details.Count; i++)
        {
            var line = vm.Details[i];
            if (!products.TryGetValue(line.ProductId, out var product))
            {
                ModelState.AddModelError(string.Empty, $"Line {i + 1}: select a product.");
                continue;
            }
            if (line.TotalQuantity <= 0)
                ModelState.AddModelError(string.Empty, $"Line {i + 1}: quantity must be greater than zero (units, or cartons × items per carton).");
            if ((line.Cartons ?? 0) > 0 && (line.ItemsPerCarton ?? 0) <= 0)
                ModelState.AddModelError(string.Empty, $"Line {i + 1}: enter items per carton.");
            if (product.PricingMode == PricingMode.PerFoot && (line.LengthFt ?? 0) <= 0)
                ModelState.AddModelError(string.Empty, $"Line {i + 1}: \"{product.Name}\" is a per-foot product — enter the piece length in feet.");
        }

        return products;
    }

    private void ApplyLines(StockHeader header, StockHeaderFormViewModel vm, Dictionary<int, Product> products)
    {
        foreach (var line in vm.Details)
        {
            var product = products[line.ProductId];
            header.Details.Add(new StockDetail
            {
                ProductId = product.Id,
                Quantity = line.Quantity,
                Cartons = line.Cartons,
                ItemsPerCarton = line.ItemsPerCarton,
                LengthFt = product.PricingMode == PricingMode.PerFoot ? line.LengthFt : null,
                TotalQuantity = line.TotalQuantity
            });
        }
    }

    /// <summary>Validates and stores the uploaded attachment; returns its path or null.</summary>
    private async Task<string?> ValidateAndSaveAttachmentAsync(StockHeaderFormViewModel vm)
    {
        if (vm.Attachment is not { Length: > 0 })
            return null;

        var ext = Path.GetExtension(vm.Attachment.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            ModelState.AddModelError(nameof(vm.Attachment), "Allowed file types: pdf, images, Word, Excel.");
            return null;
        }
        if (vm.Attachment.Length > MaxAttachmentBytes)
        {
            ModelState.AddModelError(nameof(vm.Attachment), "Attachment must be 5 MB or smaller.");
            return null;
        }

        var dir = Path.Combine(_env.WebRootPath, "uploads", "stock");
        Directory.CreateDirectory(dir);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        await using var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create);
        await vm.Attachment.CopyToAsync(stream);
        return $"/uploads/stock/{fileName}";
    }

    /// <summary>Upserts a StockPiece row (per product + length). Negative delta reverses.</summary>
    private async Task AddPiecesAsync(int productId, decimal lengthFt, decimal delta, bool clampAtZero = false)
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
        piece.Quantity += (int)delta;
        if (clampAtZero && piece.Quantity < 0)
            piece.Quantity = 0;
    }

    private async Task LoadProductsAsync()
    {
        ViewBag.ProductsJson = await _db.Products.AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                id = p.Id,
                name = p.Name
                    + (p.Company != null ? " " + p.Company.Name : "")
                    + " (" + p.Category.Name + ", " + p.Color.Name + ", G" + p.Gauge.Name + ")",
                product = p.Name,
                category = p.Category.Name,
                color = p.Color.Name,
                gauge = p.Gauge.Name,
                company = p.Company != null ? p.Company.Name : "",
                mode = p.PricingMode == PricingMode.PerFoot ? "PerFoot" : "PerUnit"
            })
            .ToListAsync();
    }
}
