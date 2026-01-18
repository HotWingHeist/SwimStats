// Quick test to verify the import works
using Microsoft.EntityFrameworkCore;
using SwimStats.Core.Models;
using SwimStats.Data;
using SwimStats.Data.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

var dbPath = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\\SwimStats_QuickTest\\test.db";
if (System.IO.File.Exists(dbPath))
{
    System.IO.File.Delete(dbPath);
}

var options = new DbContextOptionsBuilder<SwimStatsDbContext>()
    .UseSqlite($"Data Source={dbPath}")
    .Options;

var db = new SwimStatsDbContext(options);
db.Database.EnsureCreated();

// Add a test swimmer
var swimmer = new Swimmer { FirstName = "Zhifeng", LastName = "Sheng" };
db.Swimmers.Add(swimmer);
await db.SaveChangesAsync();

Console.WriteLine($"✓ Added swimmer: {swimmer.FirstName} {swimmer.LastName}");

// Try to import
var importer = new SwimRankingsImporter(db, (current, total, status) =>
{
    Console.WriteLine($"[{current}/{total}] {status}");
});

try
{
    var (retrieved, newCount, existing) = await importer.ImportSwimmerByNameAsync(swimmer.FirstName, swimmer.LastName);
    Console.WriteLine($"✓ Import successful!");
    Console.WriteLine($"  Retrieved: {retrieved}");
    Console.WriteLine($"  New: {newCount}");
    Console.WriteLine($"  Existing: {existing}");
    
    var results = await db.Results.CountAsync();
    Console.WriteLine($"  Total results in DB: {results}");
}
catch (Exception ex)
{
    Console.WriteLine($"✗ Import failed: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
}
