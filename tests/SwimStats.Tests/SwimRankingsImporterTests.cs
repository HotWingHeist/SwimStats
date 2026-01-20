using Microsoft.EntityFrameworkCore;
using SwimStats.Core.Models;
using SwimStats.Data;
using SwimStats.Data.Services;
using Xunit;

namespace SwimStats.Tests;

public class SwimRankingsImporterTests : IDisposable
{
    private readonly SwimStatsDbContext _db;
    private readonly SwimRankingsImporter _importer;

    public SwimRankingsImporterTests()
    {
        var options = new DbContextOptionsBuilder<SwimStatsDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new SwimStatsDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _importer = new SwimRankingsImporter(_db, null);
    }

    [Fact]
    public void CanCreateSwimRankingsImporter()
    {
        Assert.NotNull(_importer);
    }

    [Fact]
    public async Task IsWebsiteReachableAsyncReturnsBoolean()
    {
        var result = await _importer.IsWebsiteReachableAsync();
        Assert.IsType<bool>(result);
    }

    [Fact]
    public async Task ImportSwimmerByNameAsyncHandlesNonexistentSwimmer()
    {
        // Attempt to import a swimmer with a name that won't be found
        await Assert.ThrowsAsync<Exception>(async () =>
        {
            await _importer.ImportSwimmerByNameAsync("Nonexistent", "SwimmerXYZ123");
        });
    }

    [Fact(Skip = "Tessa Vermeulen (EZPC, ID 4977625) has no personal ranking data available on swimrankings.net - test with a different swimmer")]
    public async Task ImportTessaVermeulenSelectsEZPCSwimmer()
    {
        // This test verifies that when multiple swimmers named "Tessa Vermeulen" exist,
        // the importer correctly selects the one from EZPC club
        
        try
        {
            // First, add Tessa Vermeulen to the database
            var tessa = new Swimmer
            {
                FirstName = "Tessa",
                LastName = "Vermeulen"
            };
            _db.Swimmers.Add(tessa);
            await _db.SaveChangesAsync();

            // Import data for Tessa - should select the EZPC swimmer
            var (retrieved, added, existing) = await _importer.ImportSwimmerByNameAsync("Tessa", "Vermeulen");

            // Log what was returned
            System.Diagnostics.Debug.WriteLine($"Import results: retrieved={retrieved}, added={added}, existing={existing}");
            
            // Verify that some results were retrieved
            Assert.True(retrieved > 0, $"Should have retrieved results for Tessa Vermeulen from EZPC. Got: retrieved={retrieved}, added={added}, existing={existing}");
            
            // Verify that results were added to the database
            var results = await _db.Results.Include(r => r.Event).Where(r => r.SwimmerId == tessa.Id).ToListAsync();
            Assert.NotEmpty(results);
            
            // Log the number of results found for debugging
            System.Diagnostics.Debug.WriteLine($"Found {results.Count} results for Tessa Vermeulen (EZPC)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Test failed with exception: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"Inner exception: {ex.InnerException.Message}");
            }
            throw;
        }
    }

    [Fact(Skip = "Manual test - use to verify club filtering with debug output")]
    public async Task ManualTestTessaVermeulenClubFiltering()
    {
        // This is a manual test to check debug output
        // Remove the Skip attribute and check Debug output to see club filtering in action
        
        var (retrieved, added, existing) = await _importer.ImportSwimmerByNameAsync("Tessa", "Vermeulen");
        
        // Check debug console for messages like:
        // "[SwimRankings] Multiple swimmers found, filtering by club EZPC..."
        // "[SwimRankings] Found EZPC swimmer!"
        
        Assert.True(retrieved >= 0);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
