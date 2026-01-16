using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SwimStats.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<SwimStatsDbContext>
{
    public SwimStatsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SwimStatsDbContext>();
        
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SwimStats",
            "swimstats.db");
        
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        
        return new SwimStatsDbContext(optionsBuilder.Options);
    }
}
