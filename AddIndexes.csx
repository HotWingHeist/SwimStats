using Microsoft.EntityFrameworkCore;
using SwimStats.Data;

// Add indexes to existing database
var dbPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "SwimStats",
    "swimstats.db");

var optionsBuilder = new DbContextOptionsBuilder<SwimStatsDbContext>();
optionsBuilder.UseSqlite($"Data Source={dbPath}");

using var db = new SwimStatsDbContext(optionsBuilder.Options);

Console.WriteLine("Adding performance indexes...");

await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_Swimmers_Name"" ON ""Swimmers"" (""Name"");");
Console.WriteLine("✓ Added index on Swimmers.Name");

await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_Events_Stroke_DistanceMeters"" ON ""Events"" (""Stroke"", ""DistanceMeters"");");
Console.WriteLine("✓ Added index on Events.(Stroke, DistanceMeters)");

await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_Results_SwimmerId_EventId_Date"" ON ""Results"" (""SwimmerId"", ""EventId"", ""Date"");");
Console.WriteLine("✓ Added index on Results.(SwimmerId, EventId, Date)");

await db.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS ""IX_Results_Date"" ON ""Results"" (""Date"");");
Console.WriteLine("✓ Added index on Results.Date");

Console.WriteLine("\nAll indexes added successfully!");
