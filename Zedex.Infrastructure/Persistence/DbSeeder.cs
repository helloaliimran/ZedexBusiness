using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Zedex.Domain.Entities;
using Zedex.Infrastructure.Identity;

namespace Zedex.Infrastructure.Persistence;

public static class DbSeeder
{
    public const string AdminRole = "Admin";
    public const string WorkerRole = "Worker";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();

        // ---- Roles ----
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { AdminRole, WorkerRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // ---- Default admin ----
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        const string adminEmail = "admin@zedex.local";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = "admin",
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "System Administrator",
                IsActive = true,
                CreatedDate = DateTime.Now
            };
            var result = await userManager.CreateAsync(admin, "Admin@123");
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, AdminRole);
        }

        // ---- Master data samples ----
        if (!await db.Categories.AnyAsync())
            db.Categories.AddRange(new Category { Name = "Hardware" }, new Category { Name = "Aluminum" });
        if (!await db.Colors.AnyAsync())
            db.Colors.AddRange(new Color { Name = "White" }, new Color { Name = "Black" }, new Color { Name = "Silver" });
        if (!await db.Gauges.AnyAsync())
            db.Gauges.AddRange(new Gauge { Name = "18" }, new Gauge { Name = "20" }, new Gauge { Name = "22" });

        // ---- PVC ----
        if (!await db.Categories.IgnoreQueryFilters().AnyAsync(c => c.Name == "PVC"))
            db.Categories.Add(new Category { Name = "PVC" });
        if (!await db.AppSettings.IgnoreQueryFilters().AnyAsync(s => s.Key == AppSetting.Keys.GasKitRatePerFt))
            db.AppSettings.Add(new AppSetting
            {
                Key = AppSetting.Keys.GasKitRatePerFt,
                Value = "16",
                Description = "Gas kit price in Rs. per foot (PVC billing). Single kit = rate × length × qty; double = ×2."
            });
        if (!await db.AppSettings.IgnoreQueryFilters().AnyAsync(s => s.Key == AppSetting.Keys.PvcPrintTitle))
            db.AppSettings.Add(new AppSetting
            {
                Key = AppSetting.Keys.PvcPrintTitle,
                Value = "Zedex Business",
                Description = "Heading printed on PVC invoices (full + small)."
            });

        await db.SaveChangesAsync();
    }
}
