using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Entities;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;

namespace Zedex.Web.Controllers;

[Authorize(Policy = "Module:Customers")]
public class CustomersController : Controller
{
    private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private const long MaxImageBytes = 2 * 1024 * 1024;
    private const int PageSize = 10;

    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    public CustomersController(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var query = _db.Customers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.Name, pattern) ||
                (c.Phone != null && EF.Functions.ILike(c.Phone, pattern)) ||
                (c.Address != null && EF.Functions.ILike(c.Address, pattern)));
        }

        var total = await query.CountAsync();
        page = Math.Max(1, page);
        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .Select(c => new CustomerListItemViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Phone = c.Phone,
                Address = c.Address,
                ImagePath = c.ImagePath,
                CreatedDate = c.CreatedDate,
                Balance = c.OpeningBalance + (c.LedgerEntries
                    .Where(l => !l.IsDeleted)
                    .Sum(l => (decimal?)(l.Debit - l.Credit)) ?? 0)
            })
            .ToListAsync();

        return View(new CustomerListViewModel
        {
            Search = search,
            Items = new PagedResult<CustomerListItemViewModel>
            {
                Items = items, Page = page, PageSize = PageSize, TotalCount = total
            }
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        var customer = await _db.Customers.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CustomerDetailsViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Phone = c.Phone,
                Address = c.Address,
                Remarks = c.Remarks,
                ImagePath = c.ImagePath,
                OpeningBalance = c.OpeningBalance,
                Balance = c.OpeningBalance + (c.LedgerEntries
                    .Where(l => !l.IsDeleted)
                    .Sum(l => (decimal?)(l.Debit - l.Credit)) ?? 0),
                InvoiceCount = c.Invoices.Count(i => !i.IsDeleted),
                CreatedBy = c.CreatedBy,
                CreatedDate = c.CreatedDate
            })
            .FirstOrDefaultAsync();

        if (customer is null)
            return NotFound();
        return View(customer);
    }

    [HttpGet]
    public IActionResult Create() => View(new CustomerFormViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel vm)
    {
        var imagePath = await ValidateAndSaveImageAsync(vm);
        if (!ModelState.IsValid)
            return View(vm);

        var customer = new Customer
        {
            Name = vm.Name.Trim(),
            Phone = vm.Phone?.Trim(),
            Address = vm.Address?.Trim(),
            Remarks = vm.Remarks?.Trim(),
            OpeningBalance = vm.OpeningBalance ?? 0,
            ImagePath = imagePath
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Customer \"{customer.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null || customer.IsDeleted)
            return NotFound();

        return View(new CustomerFormViewModel
        {
            Id = customer.Id,
            Name = customer.Name,
            Phone = customer.Phone,
            Address = customer.Address,
            Remarks = customer.Remarks,
            OpeningBalance = customer.OpeningBalance,
            ExistingImagePath = customer.ImagePath
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CustomerFormViewModel vm)
    {
        var customer = await _db.Customers.FindAsync(vm.Id);
        if (customer is null || customer.IsDeleted)
            return NotFound();

        var imagePath = await ValidateAndSaveImageAsync(vm);
        if (!ModelState.IsValid)
        {
            vm.ExistingImagePath = customer.ImagePath;
            return View(vm);
        }

        customer.Name = vm.Name.Trim();
        customer.Phone = vm.Phone?.Trim();
        customer.Address = vm.Address?.Trim();
        customer.Remarks = vm.Remarks?.Trim();
        customer.OpeningBalance = vm.OpeningBalance ?? 0;
        if (imagePath is not null)
            customer.ImagePath = imagePath;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Customer \"{customer.Name}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _db.Customers.FindAsync(id);
        if (customer is null || customer.IsDeleted)
            return NotFound();

        var hasHistory = await _db.Invoices.AnyAsync(i => i.CustomerId == id)
                         || await _db.LedgerEntries.AnyAsync(l => l.CustomerId == id);
        if (hasHistory)
        {
            TempData["Error"] = $"\"{customer.Name}\" has bills or ledger history and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _db.Customers.Remove(customer); // soft delete
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Customer \"{customer.Name}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> ValidateAndSaveImageAsync(CustomerFormViewModel vm)
    {
        if (vm.Image is not { Length: > 0 })
            return null;

        var ext = Path.GetExtension(vm.Image.FileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(ext))
        {
            ModelState.AddModelError(nameof(vm.Image), "Allowed image types: jpg, png, webp.");
            return null;
        }
        if (vm.Image.Length > MaxImageBytes)
        {
            ModelState.AddModelError(nameof(vm.Image), "Image must be 2 MB or smaller.");
            return null;
        }

        var dir = Path.Combine(_env.WebRootPath, "uploads", "customers");
        Directory.CreateDirectory(dir);
        var fileName = $"{Guid.NewGuid():N}{ext}";
        await using var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create);
        await vm.Image.CopyToAsync(stream);
        return $"/uploads/customers/{fileName}";
    }
}
