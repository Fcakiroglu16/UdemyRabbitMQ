using Microsoft.EntityFrameworkCore;

namespace PatternExample.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Models.Discount> Discounts { get; set; }
    public DbSet<Models.ProcessedMessage> ProcessedMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Models.ProcessedMessage>()
            .HasIndex(p => p.MessageId)
            .IsUnique();
    }
}
