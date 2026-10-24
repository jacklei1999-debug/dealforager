using Microsoft.EntityFrameworkCore;

namespace WebUIMVC.Data;

public class Context : DbContext
{
    public DbSet<Product> Products { get; set; } = null!;

    public Context(DbContextOptions<Context> options)
        : base(options)
    {
    }

    public Context()
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite("Data Source=DealA.db");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductID);

            // Index for fast ASIN lookups during external feed ingestion and wishlist actions
            entity.HasIndex(e => e.asin)
                  .HasDatabaseName("IX_Products_asin");

            // Composite index for feed filtering and ordering: WHERE readit <= 2 ORDER BY savingspercent DESC
            entity.HasIndex(e => new { e.readit, e.savingspercent })
                  .HasDatabaseName("IX_Products_readit_savingspercent");

            // Index for admin range queries and cleanup: WHERE intertTime BETWEEN ...
            entity.HasIndex(e => e.intertTime)
                  .HasDatabaseName("IX_Products_intertTime");
        });
    }
}
