using Microsoft.EntityFrameworkCore;
using SwimStats.Data;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SwimStats.TestImporter;

public class VerifyDB
{
    public static async Task VerifyDatabase()
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwimStats_Test");
        var dbPath = Path.Combine(appDataPath, "swimstats_test.db");
        
        var options = new DbContextOptionsBuilder<SwimStatsDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        
        var db = new SwimStatsDbContext(options);
        
        var events = await db.Events.OrderBy(e => e.Stroke).ToListAsync();
        var results = await db.Results.ToListAsync();
        
        Console.WriteLine("=== Events with Course Info ===");
        foreach (var evt in events)
        {
            Console.WriteLine($"  {evt.Stroke} {evt.DistanceMeters}m - Course: {evt.Course} (value: {(int)evt.Course}m)");
        }
        
        Console.WriteLine($"\n=== Results Summary ===");
        Console.WriteLine($"Total Results: {results.Count}");
        
        var resultsByEvent = results.GroupBy(r => r.EventId).ToList();
        foreach (var group in resultsByEvent.Take(5))
        {
            var evt = events.First(e => e.Id == group.Key);
            var resultCourses = group.Select(r => r.Course).Distinct().ToList();
            Console.WriteLine($"  {evt.Stroke} {evt.DistanceMeters}m: {group.Count()} results, Courses: {string.Join(", ", resultCourses.Select(c => $"{c}({(int)c}m)"))}");
        }
    }
}
