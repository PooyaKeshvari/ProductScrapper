using Microsoft.EntityFrameworkCore;
using PriceMonitor.Models;

namespace PriceMonitor.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<PriceEntry> Prices => Set<PriceEntry>();
    public DbSet<ScrapeRun> Runs => Set<ScrapeRun>();
    public DbSet<CompetitorProductMatch> Matches => Set<CompetitorProductMatch>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>()
            .HasMany(p => p.Prices)
            .WithOne(p => p.Product)
            .HasForeignKey(p => p.ProductId);

        modelBuilder.Entity<Product>()
            .HasMany(p => p.Runs)
            .WithOne(r => r.Product)
            .HasForeignKey(r => r.ProductId);

        modelBuilder.Entity<Product>()
            .HasMany<CompetitorProductMatch>()
            .WithOne(m => m.Product)
            .HasForeignKey(m => m.ProductId);
    }
}
