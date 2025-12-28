using Microsoft.EntityFrameworkCore;

namespace PatternExample.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Models.Discount> Discounts { get; set; }
    public DbSet<Models.ProcessedMessage> ProcessedMessages { get; set; }
    public DbSet<Models.IdempotencyRecord> IdempotencyRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Models.ProcessedMessage>()
            .HasIndex(p => p.MessageId)
            .IsUnique();

        modelBuilder.Entity<Models.ProcessedMessage>()
            .HasIndex(p => p.IdempotencyKey)
            .IsUnique();

        modelBuilder.Entity<Models.IdempotencyRecord>()
            .HasIndex(i => i.IdempotencyKey)
            .IsUnique();

        modelBuilder.Entity<Models.IdempotencyRecord>()
            .HasIndex(i => i.MessageId);
    }
}
