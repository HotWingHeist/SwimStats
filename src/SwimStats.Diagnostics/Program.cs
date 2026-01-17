using Microsoft.EntityFrameworkCore;
using SwimStats.Data;
using SwimStats.Core.Models;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SwimStats.Diagnostics;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== SwimStats Database Diagnostics ===\n");

        // Get app data path (same as app uses - must be LocalApplicationData)
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SwimStats"
        );
        
        var dbPath = Path.Combine(appDataPath, "swimstats.db");
        
        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"❌ Database not found at {dbPath}");
            return;
        }

        Console.WriteLine($"✓ Database found at: {dbPath}\n");

        var options = new DbContextOptionsBuilder<SwimStatsDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        using (var db = new SwimStatsDbContext(options))
        {
            // Check swimmers
            var swimmers = await db.Swimmers.ToListAsync();
            Console.WriteLine($"=== Swimmers ({swimmers.Count}) ===");
            foreach (var s in swimmers)
            {
                Console.WriteLine($"  ID: {s.Id}, Name: {s.Name}");
            }
            Console.WriteLine();

            // Check events
            var events = await db.Events.ToListAsync();
            Console.WriteLine($"=== Events ({events.Count}) ===");
            foreach (var e in events.OrderBy(x => x.Stroke).ThenBy(x => x.DistanceMeters).ThenBy(x => x.Course))
            {
                Console.WriteLine($"  ID: {e.Id}, {e.Stroke} {e.DistanceMeters}m, Course: {e.Course} (value: {(int)e.Course}m)");
            }
            Console.WriteLine();

            // Check results
            var results = await db.Results.Include(r => r.Event).Include(r => r.Swimmer).ToListAsync();
            Console.WriteLine($"=== Results ({results.Count}) ===");
            foreach (var r in results.Take(10))
            {
                Console.WriteLine($"  Swimmer: {r.Swimmer.Name}, Event: {r.Event.Stroke} {r.Event.DistanceMeters}m, " +
                    $"Course: {r.Course} (value: {(int)r.Course}m), Time: {r.TimeSeconds}, Date: {r.Date:yyyy-MM-dd}");
            }
            if (results.Count > 10)
            {
                Console.WriteLine($"  ... and {results.Count - 10} more");
            }
            Console.WriteLine();

            // Test specific query for Freestyle 50m
            Console.WriteLine("=== Test Query: Freestyle 50m, Long Course ===");
            var testResults = await db.Results
                .Include(r => r.Event)
                .Include(r => r.Swimmer)
                .Where(r => r.Event!.Stroke == Stroke.Freestyle && 
                           r.Event.DistanceMeters == 50 &&
                           r.Course == Course.LongCourse)
                .ToListAsync();
            
            Console.WriteLine($"Found {testResults.Count} results");
            foreach (var r in testResults)
            {
                Console.WriteLine($"  {r.Swimmer.Name}: {r.TimeSeconds}s on {r.Date:yyyy-MM-dd}");
            }
            Console.WriteLine();

            // Test query for all Freestyle 50m (both courses)
            Console.WriteLine("=== Test Query: Freestyle 50m, All Courses ===");
            var allCourseResults = await db.Results
                .Include(r => r.Event)
                .Include(r => r.Swimmer)
                .Where(r => r.Event!.Stroke == Stroke.Freestyle && 
                           r.Event.DistanceMeters == 50)
                .ToListAsync();
            
            Console.WriteLine($"Found {allCourseResults.Count} results");
            foreach (var r in allCourseResults)
            {
                Console.WriteLine($"  {r.Swimmer.Name}: {r.TimeSeconds}s on {r.Date:yyyy-MM-dd}, Course: {r.Course}");
            }
        }
    }
}
