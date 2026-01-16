using Microsoft.EntityFrameworkCore;
using SwimStats.Core.Models;

namespace SwimStats.Data;

public class SwimStatsDbContext : DbContext
{
    public SwimStatsDbContext(DbContextOptions<SwimStatsDbContext> options) : base(options) { }

    public DbSet<Swimmer> Swimmers { get; set; } = null!;
    public DbSet<Event> Events { get; set; } = null!;
    public DbSet<Result> Results { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Add indexes for performance optimization
        modelBuilder.Entity<Swimmer>()
            .HasIndex(s => s.Name);

        modelBuilder.Entity<Event>()
            .HasIndex(e => new { e.Stroke, e.DistanceMeters });

        modelBuilder.Entity<Result>()
            .HasIndex(r => new { r.SwimmerId, r.EventId, r.Date });
        
        modelBuilder.Entity<Result>()
            .HasIndex(r => r.Date);

        // No seed data - will import from SwimTrack website
    }
}
