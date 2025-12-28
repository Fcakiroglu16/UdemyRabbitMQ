using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PatternExample.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Models.Discount> Discounts { get; set; }
    public DbSet<Models.IdempotencyRecord> IdempotencyRecords { get; set; }
    public DbSet<Models.OutboxMessage> OutboxMessages { get; set; }
    public DbSet<Models.InboxMessage> InboxMessages { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(warnings =>
            warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Models.IdempotencyRecord>()
            .HasIndex(i => i.IdempotencyKey)
            .IsUnique();

        modelBuilder.Entity<Models.IdempotencyRecord>()
            .HasIndex(i => i.MessageId);

        modelBuilder.Entity<Models.OutboxMessage>()
            .HasIndex(o => new { o.IsProcessed, o.CreatedAt });

        modelBuilder.Entity<Models.OutboxMessage>()
            .HasIndex(o => o.MessageId);

        modelBuilder.Entity<Models.InboxMessage>()
            .HasIndex(i => i.MessageId);

        modelBuilder.Entity<Models.InboxMessage>()
            .HasIndex(i => i.IdempotencyKey)
            .IsUnique();

        modelBuilder.Entity<Models.InboxMessage>()
            .HasIndex(i => new { i.Status, i.CreatedAt });
    }
}
