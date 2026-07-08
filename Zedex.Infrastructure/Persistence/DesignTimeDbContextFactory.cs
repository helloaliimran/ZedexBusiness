using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Zedex.Infrastructure.Persistence;

/// <summary>Used only by `dotnet ef` at design time.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=zedex;Username=postgres;Password=postgres")
            .Options;
        return new AppDbContext(options);
    }
}
