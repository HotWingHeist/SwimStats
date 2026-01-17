using Microsoft.EntityFrameworkCore;
using SwimStats.Data;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwimStats_Test");
        var dbPath = Path.Combine(appDataPath, "swimstats_test.db");
        
        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"Database not found at {dbPath}");
            return;
        }
        
        var options = new DbContextOptionsBuilder<SwimStatsDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
            
        using (var db = new SwimStatsDbContext(options))
        {
            var events = await db.Events.OrderBy(e => e.Stroke).ThenBy(e => e.DistanceMeters).ToListAsync();
            var results = await db.Results.ToListAsync();
            
            Console.WriteLine("=== Events with Course Information ===");
            Console.WriteLine($"Total Events: {events.Count}\n");
            foreach (var evt in events)
            {
                Console.WriteLine($"  Event {evt.Id}: {evt.Stroke} {evt.DistanceMeters}m - Course: {evt.Course} ({(int)evt.Course}m)");
            }
            
            Console.WriteLine($"\n=== Results by Course ===");
            Console.WriteLine($"Total Results: {results.Count}\n");
            
            var longCourseResults = results.Where(r => r.Course == SwimStats.Core.Models.Course.LongCourse).Count();
            var shortCourseResults = results.Where(r => r.Course == SwimStats.Core.Models.Course.ShortCourse).Count();
            
            Console.WriteLine($"Long Course (50m): {longCourseResults} results");
            Console.WriteLine($"Short Course (25m): {shortCourseResults} results");
            
            Console.WriteLine($"\n=== Sample Results ===");
            var samples = results.GroupBy(r => r.EventId).Take(3).ToList();
            foreach (var group in samples)
            {
                var evt = events.First(e => e.Id == group.Key);
                var resultsByCourse = group.GroupBy(r => r.Course);
                foreach (var courseGroup in resultsByCourse)
                {
                    Console.WriteLine($"  {evt.Stroke} {evt.DistanceMeters}m - {courseGroup.Key} ({(int)courseGroup.Key}m): {courseGroup.Count()} results");
                }
            }
        }
    }
}
