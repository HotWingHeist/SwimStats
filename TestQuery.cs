using Microsoft.EntityFrameworkCore;
using SwimStats.Data;
using System;
using System.Linq;

var dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwimStats", "swimstats.db");
var optionsBuilder = new DbContextOptionsBuilder<SwimStatsDbContext>();
optionsBuilder.UseSqlite($"Data Source={dbPath}");

using var db = new SwimStatsDbContext(optionsBuilder.Options);

Console.WriteLine($"Database: {dbPath}");
Console.WriteLine();

var swimmers = await db.Swimmers.ToListAsync();
Console.WriteLine($"Total Swimmers: {swimmers.Count}");
foreach (var s in swimmers)
{
    Console.WriteLine($"  - {s.Id}: {s.Name}");
}
Console.WriteLine();

var events = await db.Events.ToListAsync();
Console.WriteLine($"Total Events: {events.Count}");
foreach (var e in events.Take(10))
{
    Console.WriteLine($"  - {e.Id}: {e.DisplayName}");
}
Console.WriteLine();

var results = await db.Results.Include(r => r.Swimmer).Include(r => r.Event).ToListAsync();
Console.WriteLine($"Total Results: {results.Count}");
foreach (var r in results.Take(10))
{
    Console.WriteLine($"  - {r.Swimmer?.Name} - {r.Event?.DisplayName} - {r.TimeSeconds}s on {r.Date:yyyy-MM-dd}");
}
