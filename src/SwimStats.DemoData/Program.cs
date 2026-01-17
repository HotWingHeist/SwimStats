using Microsoft.EntityFrameworkCore;
using SwimStats.Data;
using SwimStats.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SwimStats.DemoData;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== SwimStats Demo Data Creator ===\n");

        // Get app data path (same as app uses - must be LocalApplicationData)
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SwimStats"
        );
        
        if (!Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
        }

        var dbPath = Path.Combine(appDataPath, "swimstats.db");
        
        // Delete old database
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
            Console.WriteLine($"✓ Deleted old database\n");
        }

        // Create fresh database
        var options = new DbContextOptionsBuilder<SwimStatsDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        using (var db = new SwimStatsDbContext(options))
        {
            db.Database.EnsureCreated();
            Console.WriteLine($"✓ Database created at: {dbPath}\n");

            // Add swimmers
            var zhifeng = new Swimmer { FirstName = "Zhifeng", LastName = "Sheng" };
            db.Swimmers.Add(zhifeng);
            await db.SaveChangesAsync();

            Console.WriteLine("--- Added Swimmers ---");
            Console.WriteLine($"  - Zhifeng Sheng (ID: {zhifeng.Id})\n");

            // Add events with both courses
            var events50mLong = new Event { Stroke = Stroke.Freestyle, DistanceMeters = 50, Course = Course.LongCourse };
            var events50mShort = new Event { Stroke = Stroke.Freestyle, DistanceMeters = 50, Course = Course.ShortCourse };
            var events100mLong = new Event { Stroke = Stroke.Backstroke, DistanceMeters = 100, Course = Course.LongCourse };
            var events100mShort = new Event { Stroke = Stroke.Backstroke, DistanceMeters = 100, Course = Course.ShortCourse };
            var events200mLong = new Event { Stroke = Stroke.Breaststroke, DistanceMeters = 200, Course = Course.LongCourse };
            var events200mShort = new Event { Stroke = Stroke.Breaststroke, DistanceMeters = 200, Course = Course.ShortCourse };

            db.Events.AddRange(events50mLong, events50mShort, events100mLong, events100mShort, events200mLong, events200mShort);
            await db.SaveChangesAsync();

            Console.WriteLine("--- Added Events ---");
            Console.WriteLine($"  - 50m Freestyle (Long Course 50m)");
            Console.WriteLine($"  - 50m Freestyle (Short Course 25m)");
            Console.WriteLine($"  - 100m Backstroke (Long Course 50m)");
            Console.WriteLine($"  - 100m Backstroke (Short Course 25m)");
            Console.WriteLine($"  - 200m Breaststroke (Long Course 50m)");
            Console.WriteLine($"  - 200m Breaststroke (Short Course 25m)\n");

            // Add demo results
            var results = new List<Result>
            {
                // 50m Freestyle - Long Course
                new Result { SwimmerId = zhifeng.Id, EventId = events50mLong.Id, TimeSeconds = 24.50, Date = new DateTime(2025, 1, 15), Course = Course.LongCourse },
                new Result { SwimmerId = zhifeng.Id, EventId = events50mLong.Id, TimeSeconds = 24.80, Date = new DateTime(2025, 2, 10), Course = Course.LongCourse },
                new Result { SwimmerId = zhifeng.Id, EventId = events50mLong.Id, TimeSeconds = 25.10, Date = new DateTime(2025, 3, 05), Course = Course.LongCourse },
                
                // 50m Freestyle - Short Course
                new Result { SwimmerId = zhifeng.Id, EventId = events50mShort.Id, TimeSeconds = 23.20, Date = new DateTime(2025, 1, 20), Course = Course.ShortCourse },
                new Result { SwimmerId = zhifeng.Id, EventId = events50mShort.Id, TimeSeconds = 23.50, Date = new DateTime(2025, 2, 15), Course = Course.ShortCourse },
                new Result { SwimmerId = zhifeng.Id, EventId = events50mShort.Id, TimeSeconds = 23.80, Date = new DateTime(2025, 3, 12), Course = Course.ShortCourse },
                
                // 100m Backstroke - Long Course
                new Result { SwimmerId = zhifeng.Id, EventId = events100mLong.Id, TimeSeconds = 58.30, Date = new DateTime(2025, 1, 25), Course = Course.LongCourse },
                new Result { SwimmerId = zhifeng.Id, EventId = events100mLong.Id, TimeSeconds = 57.80, Date = new DateTime(2025, 2, 20), Course = Course.LongCourse },
                new Result { SwimmerId = zhifeng.Id, EventId = events100mLong.Id, TimeSeconds = 57.50, Date = new DateTime(2025, 3, 15), Course = Course.LongCourse },
                
                // 100m Backstroke - Short Course
                new Result { SwimmerId = zhifeng.Id, EventId = events100mShort.Id, TimeSeconds = 56.20, Date = new DateTime(2025, 1, 28), Course = Course.ShortCourse },
                new Result { SwimmerId = zhifeng.Id, EventId = events100mShort.Id, TimeSeconds = 55.90, Date = new DateTime(2025, 2, 22), Course = Course.ShortCourse },
                new Result { SwimmerId = zhifeng.Id, EventId = events100mShort.Id, TimeSeconds = 55.40, Date = new DateTime(2025, 3, 18), Course = Course.ShortCourse },
                
                // 200m Breaststroke - Long Course
                new Result { SwimmerId = zhifeng.Id, EventId = events200mLong.Id, TimeSeconds = 134.50, Date = new DateTime(2025, 2, 01), Course = Course.LongCourse },
                new Result { SwimmerId = zhifeng.Id, EventId = events200mLong.Id, TimeSeconds = 133.20, Date = new DateTime(2025, 2, 28), Course = Course.LongCourse },
                new Result { SwimmerId = zhifeng.Id, EventId = events200mLong.Id, TimeSeconds = 132.80, Date = new DateTime(2025, 3, 20), Course = Course.LongCourse },
                
                // 200m Breaststroke - Short Course
                new Result { SwimmerId = zhifeng.Id, EventId = events200mShort.Id, TimeSeconds = 128.70, Date = new DateTime(2025, 2, 05), Course = Course.ShortCourse },
                new Result { SwimmerId = zhifeng.Id, EventId = events200mShort.Id, TimeSeconds = 127.50, Date = new DateTime(2025, 3, 01), Course = Course.ShortCourse },
                new Result { SwimmerId = zhifeng.Id, EventId = events200mShort.Id, TimeSeconds = 126.90, Date = new DateTime(2025, 3, 25), Course = Course.ShortCourse },
            };

            db.Results.AddRange(results);
            await db.SaveChangesAsync();

            Console.WriteLine("--- Added Demo Results ---");
            Console.WriteLine($"  Total: {results.Count} results");
            Console.WriteLine($"  - 50m Freestyle: 6 results (3 long course, 3 short course)");
            Console.WriteLine($"  - 100m Backstroke: 6 results (3 long course, 3 short course)");
            Console.WriteLine($"  - 200m Breaststroke: 6 results (3 long course, 3 short course)\n");

            Console.WriteLine("=== Demo Data Setup Complete ===");
            Console.WriteLine($"Database ready at: {dbPath}");
            Console.WriteLine("Run the app now to see the demo data!");
        }
    }
}
