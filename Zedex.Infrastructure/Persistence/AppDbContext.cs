using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Zedex.Application.Common;
using Zedex.Domain.Common;
using Zedex.Domain.Entities;
using Zedex.Infrastructure.Identity;

namespace Zedex.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    private readonly ICurrentUserService? _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService? currentUser = null)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Color> Colors => Set<Color>();
    public DbSet<Gauge> Gauges => Set<Gauge>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<StockPiece> StockPieces => Set<StockPiece>();
    public DbSet<StockHeader> StockHeaders => Set<StockHeader>();
    public DbSet<StockDetail> StockDetails => Set<StockDetail>();
    public DbSet<StockResetLog> StockResetLogs => Set<StockResetLog>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<PvcInvoiceItem> PvcInvoiceItems => Set<PvcInvoiceItem>();
    public DbSet<SaleReturn> SaleReturns => Set<SaleReturn>();
    public DbSet<SaleReturnItem> SaleReturnItems => Set<SaleReturnItem>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
        base.ConfigureConventions(configurationBuilder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ---- Master data ----
        builder.Entity<Category>().HasIndex(c => c.Name).IsUnique();
        builder.Entity<Color>().HasIndex(c => c.Name).IsUnique();
        builder.Entity<Gauge>().HasIndex(g => g.Name).IsUnique();
        builder.Entity<Company>().HasIndex(c => c.Name).IsUnique();
        builder.Entity<Category>().Property(c => c.Name).HasMaxLength(100);
        builder.Entity<Color>().Property(c => c.Name).HasMaxLength(100);
        builder.Entity<Gauge>().Property(g => g.Name).HasMaxLength(100);
        builder.Entity<Company>().Property(c => c.Name).HasMaxLength(100);

        // ---- App settings ----
        builder.Entity<AppSetting>(e =>
        {
            e.Property(s => s.Key).HasMaxLength(100);
            e.HasIndex(s => s.Key).IsUnique();
            e.Property(s => s.Value).HasMaxLength(500);
        });

        // ---- Product ----
        builder.Entity<Product>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(200);
            e.HasIndex(p => p.Name);
            e.HasOne(p => p.Category).WithMany(c => c.Products)
                .HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Color).WithMany(c => c.Products)
                .HasForeignKey(p => p.ColorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Gauge).WithMany(g => g.Products)
                .HasForeignKey(p => p.GaugeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.Company).WithMany(c => c.Products)
                .HasForeignKey(p => p.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Stock ----
        builder.Entity<StockPiece>(e =>
        {
            e.HasIndex(s => new { s.ProductId, s.LengthFt }).IsUnique();
            e.HasOne(s => s.Product).WithMany(p => p.StockPieces)
                .HasForeignKey(s => s.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockDetail>(e =>
        {
            e.HasOne(d => d.StockHeader).WithMany(h => h.Details)
                .HasForeignKey(d => d.StockHeaderId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Product).WithMany()
                .HasForeignKey(d => d.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StockResetLog>(e =>
        {
            e.Property(l => l.ProductName).HasMaxLength(200);
            e.HasOne(l => l.Product).WithMany()
                .HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => l.CreatedDate);
            e.HasIndex(l => l.BatchId);
        });

        // ---- Customer ----
        builder.Entity<Customer>(e =>
        {
            e.Property(c => c.Name).HasMaxLength(200);
            e.Property(c => c.Phone).HasMaxLength(30);
            e.HasIndex(c => c.Name);
            e.HasIndex(c => c.Phone);
        });

        // ---- Invoice ----
        builder.Entity<Invoice>(e =>
        {
            e.Property(i => i.InvoiceNumber).HasMaxLength(30);
            e.HasIndex(i => i.InvoiceNumber).IsUnique();
            // Backfills existing rows as Standard when the column is added.
            e.Property(i => i.InvoiceType).HasDefaultValue(Zedex.Domain.Enums.InvoiceType.Standard);
            e.HasIndex(i => i.InvoiceType);
            e.HasOne(i => i.Customer).WithMany(c => c.Invoices)
                .HasForeignKey(i => i.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<InvoiceItem>(e =>
        {
            e.HasOne(ii => ii.Invoice).WithMany(i => i.Items)
                .HasForeignKey(ii => ii.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ii => ii.Product).WithMany()
                .HasForeignKey(ii => ii.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<PvcInvoiceItem>(e =>
        {
            e.HasOne(ii => ii.Invoice).WithMany(i => i.PvcItems)
                .HasForeignKey(ii => ii.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ii => ii.Product).WithMany()
                .HasForeignKey(ii => ii.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Returns ----
        builder.Entity<SaleReturn>(e =>
        {
            e.Property(r => r.ReturnNumber).HasMaxLength(30);
            e.HasIndex(r => r.ReturnNumber).IsUnique();
            e.HasOne(r => r.Invoice).WithMany(i => i.Returns)
                .HasForeignKey(r => r.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(r => r.Customer).WithMany()
                .HasForeignKey(r => r.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SaleReturnItem>(e =>
        {
            e.HasOne(ri => ri.SaleReturn).WithMany(r => r.Items)
                .HasForeignKey(ri => ri.SaleReturnId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(ri => ri.InvoiceItem).WithMany()
                .HasForeignKey(ri => ri.InvoiceItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(ri => ri.PvcInvoiceItem).WithMany()
                .HasForeignKey(ri => ri.PvcInvoiceItemId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(ri => ri.Product).WithMany()
                .HasForeignKey(ri => ri.ProductId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---- Ledger ----
        builder.Entity<LedgerEntry>(e =>
        {
            e.HasOne(l => l.Customer).WithMany(c => c.LedgerEntries)
                .HasForeignKey(l => l.CustomerId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.Invoice).WithMany()
                .HasForeignKey(l => l.InvoiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.SaleReturn).WithMany()
                .HasForeignKey(l => l.SaleReturnId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(l => new { l.CustomerId, l.EntryDate });
        });

        // ---- Permissions ----
        builder.Entity<UserPermission>(e =>
        {
            e.HasIndex(p => p.UserId).IsUnique();
            e.HasOne<ApplicationUser>().WithOne()
                .HasForeignKey<UserPermission>(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Global soft-delete query filter for every BaseEntity ----
        foreach (var entityType in builder.Model.GetEntityTypes()
                     .Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var body = Expression.Not(Expression.Property(parameter, nameof(BaseEntity.IsDeleted)));
            builder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAudit();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAudit();
        return base.SaveChanges();
    }

    private void ApplyAudit()
    {
        var now = DateTime.Now;
        var user = _currentUser?.UserName;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedDate = now;
                    entry.Entity.CreatedBy = user;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedDate = now;
                    entry.Entity.UpdatedBy = user;
                    break;
                case EntityState.Deleted when entry.Entity is not null:
                    // Convert hard deletes into soft deletes.
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.UpdatedDate = now;
                    entry.Entity.UpdatedBy = user;
                    break;
            }
        }
    }
}
