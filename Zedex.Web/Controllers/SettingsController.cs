using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Zedex.Domain.Entities;
using Zedex.Infrastructure.Persistence;
using Zedex.Web.Models;

namespace Zedex.Web.Controllers;

/// <summary>Admin-only application settings (currently: PVC gas kit rate).</summary>
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
            PvcPrintTitle = await GetStringAsync(AppSetting.Keys.PvcPrintTitle)
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

        TempData["Success"] = "Settings saved.";
        return RedirectToAction(nameof(Index));
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
