using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Domain.Entities;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;

namespace Zedex.Web.Controllers;

/// <summary>Admin-only application settings (PVC billing, quotation header and default terms).</summary>
[Authorize(Roles = DbSeeder.AdminRole)]
public class SettingsController : Controller
{
    private readonly AppDbContext _db;

    public SettingsController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        return View(new SettingsViewModel
        {
            GasKitRatePerFt = await GetDecimalAsync(AppSetting.Keys.GasKitRatePerFt),
            PvcPrintTitle = await GetStringAsync(AppSetting.Keys.PvcPrintTitle),
            QuotationTitle = await GetStringAsync(AppSetting.Keys.QuotationTitle),
            QuotationHeaderDetails = await GetStringAsync(AppSetting.Keys.QuotationHeaderDetails),
            QuotationDefaultTerms = string.Join("\n", await _db.QuotationDefaultTerms.AsNoTracking()
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
                .Select(t => t.Text)
                .ToListAsync())
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SettingsViewModel vm)
    {
        if (!ModelState.IsValid)
            return View(vm);

        await SetAsync(AppSetting.Keys.GasKitRatePerFt,
            vm.GasKitRatePerFt.ToString("0.####"),
            "Gas kit price in Rs. per foot (PVC billing). Single kit = rate × length × qty; double = ×2.");
        await SetAsync(AppSetting.Keys.PvcPrintTitle,
            vm.PvcPrintTitle?.Trim() ?? "",
            "Heading printed on PVC invoices (full + small).");
        await SetAsync(AppSetting.Keys.QuotationTitle,
            vm.QuotationTitle?.Trim() ?? "",
            "Company name centred at the top of quotations.");
        await SetAsync(AppSetting.Keys.QuotationHeaderDetails,
            vm.QuotationHeaderDetails?.Trim() ?? "",
            "Line(s) under the quotation title (address, phone, email).");
        await SaveDefaultTermsAsync(vm.QuotationDefaultTerms);

        TempData["Success"] = "Settings saved.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Replaces the default quotation terms with one row per non-empty line.
    /// Existing quotations keep their own copy of the terms.</summary>
    private async Task SaveDefaultTermsAsync(string? text)
    {
        var lines = (text ?? "").Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Select(l => l.Length > 1000 ? l[..1000] : l)
            .ToList();

        _db.QuotationDefaultTerms.RemoveRange(await _db.QuotationDefaultTerms.ToListAsync());
        _db.QuotationDefaultTerms.AddRange(lines.Select((l, i) => new QuotationDefaultTerm { SortOrder = i + 1, Text = l }));
        await _db.SaveChangesAsync();
    }

    private async Task<string?> GetStringAsync(string key)
    {
        var setting = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key);
        return setting?.Value;
    }

    private async Task<decimal> GetDecimalAsync(string key)
    {
        var setting = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key);
        return setting is not null && decimal.TryParse(setting.Value, out var value) ? value : 0m;
    }

    private async Task SetAsync(string key, string value, string? description)
    {
        var setting = await _db.AppSettings.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Key == key);
        if (setting is null)
        {
            _db.AppSettings.Add(new AppSetting { Key = key, Value = value, Description = description });
        }
        else
        {
            setting.Value = value;
            setting.IsDeleted = false;
            setting.Description ??= description;
        }
        await _db.SaveChangesAsync();
    }
}
