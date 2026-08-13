using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Zedex.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef` at design time (migrations add/update/etc). There is no
/// hardcoded connection string here — it reads the same "DefaultConnection" that the
/// running app uses, from Zedex.Web/appsettings*.json and environment variables, so the
/// connection string lives in exactly one place.
///
/// (A factory is still needed rather than letting the tools boot Zedex.Web's Program.cs
/// directly, because Program.cs runs DbSeeder.SeedAsync — migrate + seed roles/admin —
/// before app.Run(), which would fire on every `dotnet ef` invocation otherwise.)
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var webProjectPath = FindWebProjectPath();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(webProjectPath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"Connection string \"DefaultConnection\" not found in {Path.Combine(webProjectPath, "appsettings.json")}.");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// `dotnet ef ... -p Zedex.Infrastructure -s Zedex.Web` sets the current directory
    /// to the startup project (Zedex.Web) before invoking this factory, so that's tried
    /// first; the walk-up-then-down fallback covers running the command from elsewhere
    /// (e.g. the solution root, or inside Zedex.Infrastructure directly).
    /// </summary>
    private static string FindWebProjectPath()
    {
        var cwd = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(cwd, "appsettings.json")))
            return cwd;

        for (var dir = new DirectoryInfo(cwd); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "Zedex.Web");
            if (File.Exists(Path.Combine(candidate, "appsettings.json")))
                return candidate;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate Zedex.Web/appsettings.json starting from \"{cwd}\". " +
            "Run `dotnet ef` from the solution root with -p Zedex.Infrastructure -s Zedex.Web.");
    }
}
