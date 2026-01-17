using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SwimStats.Data;
using SwimStats.Data.Services;
using SwimStats.Core.Models;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SwimStats.TestImporter;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== SwimRankings Importer Test ===");
        Console.WriteLine("Testing with swimmer: Zhifeng Sheng\n");

        // Setup database
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SwimStats_Test");
        if (!Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
        }

        var dbPath = Path.Combine(appDataPath, "swimstats_test.db");
        
        // Remove old database for fresh test
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
            Console.WriteLine($"Deleted old test database: {dbPath}\n");
        }

        var services = new ServiceCollection();
        services.AddDbContext<SwimStatsDbContext>(opts => opts.UseSqlite($"Data Source={dbPath}"));

        var serviceProvider = services.BuildServiceProvider();

        using (var scope = serviceProvider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SwimStatsDbContext>();
            db.Database.EnsureCreated();
            Console.WriteLine($"✓ Database created at: {dbPath}\n");

            // Create importer with progress callback
            var importer = new SwimRankingsImporter(db, (current, total, status) =>
            {
                Console.WriteLine($"  Progress: [{current}/{total}] {status}");
            });

            try
            {
                // Since search pages use AJAX and require browser automation to discover athletes,
                // we manually add test swimmers for testing. In production, swimmers would be discovered
                // via the search endpoint or imported from another source.
                Console.WriteLine("--- Step 1: Adding Test Athletes ---");
                Console.WriteLine("Note: Search pages are AJAX-based. Adding test swimmers directly for testing.\n");
                
                var swimmer1 = new SwimStats.Core.Models.Swimmer { Name = "Zhifeng Sheng" };
                db.Swimmers.Add(swimmer1);
                await db.SaveChangesAsync();
                
                var swimmers = await db.Swimmers.ToListAsync();
                Console.WriteLine("Swimmers in database:");
                foreach (var s in swimmers)
                {
                    Console.WriteLine($"  - ID: {s.Id}, Name: {s.Name}");
                }
                Console.WriteLine();

                Console.WriteLine("--- Step 2: Importing Results ---");
                Console.WriteLine("Searching for swimming results via SwimRankings...\n");
                
                // Call ImportResultsAsync - this will search for each swimmer by name
                // and fetch their personal bests from their detail page
                var baseResultUrl = "https://www.swimrankings.net/index.php?page=athleteDetail";
                var resultCount = await importer.ImportResultsAsync(baseResultUrl);
                
                Console.WriteLine($"\n✓ Results imported: {resultCount}\n");

                // Display results
                var results = await db.Results.Include(r => r.Swimmer).Include(r => r.Event).ToListAsync();
                Console.WriteLine($"Results in database: {results.Count}");
                foreach (var result in results.Take(10))
                {
                    var timeFormatted = FormatTime(result.TimeSeconds);
                    Console.WriteLine($"  - {result.Swimmer.Name}: {result.Event.Stroke} {result.Event.DistanceMeters}m = {timeFormatted} ({result.Date:yyyy-MM-dd})");
                }
                if (results.Count > 10)
                {
                    Console.WriteLine($"  ... and {results.Count - 10} more results");
                }
                Console.WriteLine();

                // Summary
                Console.WriteLine("=== Import Summary ===");
                Console.WriteLine($"Total Swimmers: {swimmers.Count}");
                Console.WriteLine($"Total Results: {results.Count}");
                Console.WriteLine($"Events: {await db.Events.CountAsync()}");
                Console.WriteLine($"\n✓ Test completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Error during import:");
                Console.WriteLine($"  {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"  Inner: {ex.InnerException.Message}");
                }
            }
        }
    }

    static string FormatTime(double totalSeconds)
    {
        int minutes = (int)(totalSeconds / 60);
        double seconds = totalSeconds % 60;
        
        if (minutes > 0)
        {
            return $"{minutes}:{seconds:00.00}";
        }
        else
        {
            return $"{seconds:00.00}";
        }
    }
}
