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
            .HasIndex(e => new { e.Stroke, e.DistanceMeters, e.Course });

        modelBuilder.Entity<Result>()
            .HasIndex(r => new { r.SwimmerId, r.EventId, r.Date, r.Course });
        modelBuilder.Entity<Result>()
            .HasIndex(r => r.Date);
    }

    /// <summary>
    /// Seeds the database with swimmers from the configuration file on first creation.
    /// This is called after the database is created by the application startup logic.
    /// </summary>
    public void SeedSwimmersFromConfiguration()
    {
        try
        {
            // If we have swimmers with empty names, delete them and reseed
            var emptyNameSwimmers = Swimmers.Where(s => string.IsNullOrWhiteSpace(s.Name)).ToList();
            if (emptyNameSwimmers.Any())
            {
                foreach (var swimmer in emptyNameSwimmers)
                {
                    Swimmers.Remove(swimmer);
                }
                SaveChanges();
            }

            // Check if swimmers are already seeded with names
            var namedSwimmers = Swimmers.Where(s => !string.IsNullOrWhiteSpace(s.FirstName)).ToList();
            if (namedSwimmers.Any())
            {
                System.Diagnostics.Debug.WriteLine($"[SwimStats] Database already seeded with {namedSwimmers.Count} swimmers");
                return;
            }

            var configSwimmers = SwimmerConfigurationLoader.LoadSwimmers();
            var swimmers = configSwimmers
                .Select(sc => new Swimmer 
                { 
                    Id = sc.Id, 
                    FirstName = !string.IsNullOrWhiteSpace(sc.FirstName) ? sc.FirstName : ExtractFirstName(sc.Name),
                    LastName = !string.IsNullOrWhiteSpace(sc.LastName) ? sc.LastName : ExtractLastName(sc.Name)
                })
                .ToList();

            if (swimmers.Any())
            {
                System.Diagnostics.Debug.WriteLine($"[SwimStats] Seeding database with {swimmers.Count} swimmers");
                Swimmers.AddRange(swimmers);
                SaveChanges();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SwimStats] ERROR seeding database: {ex.Message}");
        }
    }

    private static string ExtractFirstName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return string.Empty;
        
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : string.Empty;
    }

    private static string ExtractLastName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return string.Empty;
        
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : string.Empty;
    }
}

