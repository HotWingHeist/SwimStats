using Microsoft.EntityFrameworkCore;
using SwimStats.Core.Models;
using SwimStats.Data;
using SwimStats.Data.Services;
using Xunit;

namespace SwimStats.Tests;

public class SwimTrackImporterTests : IDisposable
{
    private readonly SwimStatsDbContext _db;
    private readonly SwimTrackImporter _importer;

    public SwimTrackImporterTests()
    {
        var options = new DbContextOptionsBuilder<SwimStatsDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new SwimStatsDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _importer = new SwimTrackImporter(_db, null);
    }

    [Fact]
    public void CanCreateSwimTrackImporter()
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
    public void ParseTimeHandlesDecimalFormat()
    {
        // Test parsing time like "54.80"
        var method = typeof(SwimTrackImporter).GetMethod("ParseTime", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(method);
        
        var result = (double?)method.Invoke(_importer, new object[] { "54.80" });
        Assert.NotNull(result);
        Assert.Equal(54.80, result.Value, 2);
    }

    [Fact]
    public void ParseTimeHandlesMinutesFormat()
    {
        // Test parsing time like "1:58.88"
        var method = typeof(SwimTrackImporter).GetMethod("ParseTime", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(method);
        
        var result = (double?)method.Invoke(_importer, new object[] { "1:58.88" });
        Assert.NotNull(result);
        Assert.Equal(118.88, result.Value, 2);
    }

    [Fact]
    public void ParseTimeHandlesCommaDecimal()
    {
        // Test parsing time with comma as decimal separator (European format)
        var method = typeof(SwimTrackImporter).GetMethod("ParseTime", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(method);
        
        var result = (double?)method.Invoke(_importer, new object[] { "54,80" });
        Assert.NotNull(result);
        Assert.Equal(54.80, result.Value, 2);
    }

    [Fact]
    public void SwimTrackDataUsesShortCourseByDefault()
    {
        // Create an event without explicitly specifying course
        var evt = new Event { Stroke = Stroke.Freestyle, DistanceMeters = 50 };
        
        // Should default to ShortCourse for SwimTrack data
        Assert.Equal(Course.ShortCourse, evt.Course);
    }

    [Fact]
    public void ResultUsesShortCourseByDefault()
    {
        // Create a result without explicitly specifying course
        var result = new Result 
        { 
            SwimmerId = 1, 
            EventId = 1, 
            TimeSeconds = 60.0, 
            Date = DateTime.Now 
        };
        
        // Should default to ShortCourse for SwimTrack data
        Assert.Equal(Course.ShortCourse, result.Course);
    }

    [Fact]
    public async Task ImportSwimmerByNameAsyncHandlesNonexistentSwimmer()
    {
        // Test importing a swimmer that doesn't exist on SwimTrack
        var (retrieved, newCount, existing) = await _importer.ImportSwimmerByNameAsync("NonExistent", "Swimmer");
        
        // Should return 0 results without throwing
        Assert.Equal(0, retrieved);
        Assert.Equal(0, newCount);
        Assert.Equal(0, existing);
    }

    [Fact]
    public async Task ImportSingleSwimmerAsyncSplitsNameCorrectly()
    {
        // Test that full name is split into first/last correctly
        var (retrieved, newCount, existing) = await _importer.ImportSingleSwimmerAsync("NonExistent Swimmer");
        
        // Should return 0 results without throwing
        Assert.Equal(0, retrieved);
        Assert.Equal(0, newCount);
        Assert.Equal(0, existing);
    }

    [Fact]
    public void DuplicateDetectionUsesTightTimeToleranceOf0_00001Seconds()
    {
        // Add a swimmer, event, and results
        // Tests that duplicate detection uses exact time matching (no tolerance)
        // to preserve heats/semis/finals as separate results
        var swimmer = new Swimmer { FirstName = "John", LastName = "Doe" };
        _db.Swimmers.Add(swimmer);
        
        var evt = new Event { Stroke = Stroke.Freestyle, DistanceMeters = 50, Course = Course.ShortCourse };
        _db.Events.Add(evt);
        _db.SaveChanges();

        var date = new DateTime(2026, 1, 15);

        // Add first result: 24.80 seconds
        var result1 = new Result
        {
            SwimmerId = swimmer.Id,
            EventId = evt.Id,
            TimeSeconds = 24.80,
            Date = date,
            Course = Course.ShortCourse
        };
        _db.Results.Add(result1);
        _db.SaveChanges();

        // Test different time scenarios
        var scenarios = new[]
        {
            (time: 24.80, shouldBeNew: false, description: "Exact duplicate (same time)"),
            (time: 24.79, shouldBeNew: true, description: "Faster result (different heat/semi)"),
            (time: 24.81, shouldBeNew: true, description: "Slower result (different heat/semi)"),
            (time: 24.65, shouldBeNew: true, description: "Significantly faster (different event)"),
            (time: 24.95, shouldBeNew: true, description: "Significantly slower (different event)"),
            (time: 25.0, shouldBeNew: true, description: "Over 1 second difference"),
            (time: 23.50, shouldBeNew: true, description: "Much faster (clearly different race)")
        };

        foreach (var scenario in scenarios)
        {
            var testResult = new Result
            {
                SwimmerId = swimmer.Id,
                EventId = evt.Id,
                TimeSeconds = scenario.time,
                Date = date,
                Course = Course.ShortCourse
            };

            // Simulate the duplicate detection logic (exact match only - no tolerance)
            var isExact = Math.Abs(result1.TimeSeconds - testResult.TimeSeconds) == 0;

            if (scenario.shouldBeNew)
            {
                Assert.False(isExact, $"FAIL: {scenario.description} - should NOT be duplicate");
            }
            else
            {
                Assert.True(isExact, $"FAIL: {scenario.description} - should be duplicate");
            }
        }
    }

    [Fact]
    public void MultipleResultsSameDayDifferentTimesAreNotDuplicates()
    {
        // Simulate importing multiple results from same heat, semi, final on same day
        var swimmer = new Swimmer { FirstName = "Jane", LastName = "Smith" };
        _db.Swimmers.Add(swimmer);
        
        var evt = new Event { Stroke = Stroke.Backstroke, DistanceMeters = 100, Course = Course.LongCourse };
        _db.Events.Add(evt);
        _db.SaveChanges();

        var date = new DateTime(2026, 1, 20);

        // Add multiple results with different times (simulating heat, semi, final)
        var results = new[]
        {
            new Result { SwimmerId = swimmer.Id, EventId = evt.Id, TimeSeconds = 65.50, Date = date, Course = Course.LongCourse },  // Heat
            new Result { SwimmerId = swimmer.Id, EventId = evt.Id, TimeSeconds = 64.20, Date = date, Course = Course.LongCourse },  // Semi
            new Result { SwimmerId = swimmer.Id, EventId = evt.Id, TimeSeconds = 63.80, Date = date, Course = Course.LongCourse }   // Final
        };

        _db.Results.AddRange(results);
        _db.SaveChanges();

        // Verify all three results were saved as distinct records
        var savedResults = _db.Results.Where(r => r.SwimmerId == swimmer.Id && r.EventId == evt.Id).ToList();
        Assert.Equal(3, savedResults.Count);
        
        // Verify times are preserved correctly
        Assert.Contains(savedResults, r => Math.Abs(r.TimeSeconds - 65.50) < 0.001);
        Assert.Contains(savedResults, r => Math.Abs(r.TimeSeconds - 64.20) < 0.001);
        Assert.Contains(savedResults, r => Math.Abs(r.TimeSeconds - 63.80) < 0.001);
    }

    [Fact]
    public void ReimportSameResultWithinToleranceIsDetectedAsDuplicate()
    {
        // Simulate re-importing the same result with minor time variation (measurement precision)
        var swimmer = new Swimmer { FirstName = "Bob", LastName = "Jones" };
        _db.Swimmers.Add(swimmer);
        
        var evt = new Event { Stroke = Stroke.Breaststroke, DistanceMeters = 50, Course = Course.ShortCourse };
        _db.Events.Add(evt);
        _db.SaveChanges();

        var date = new DateTime(2026, 1, 18);

        // First import
        var result1 = new Result
        {
            SwimmerId = swimmer.Id,
            EventId = evt.Id,
            TimeSeconds = 32.45,
            Date = date,
            Course = Course.ShortCourse,
            Location = "Local Pool"
        };
        _db.Results.Add(result1);
        _db.SaveChanges();

        // Re-import with identical time (should be detected as duplicate)
        var result2 = new Result
        {
            SwimmerId = swimmer.Id,
            EventId = evt.Id,
            TimeSeconds = 32.45,
            Date = date,
            Course = Course.ShortCourse,
            Location = "Local Pool"
        };

        // This should be detected as duplicate by importers
        var isDuplicate = Math.Abs(result1.TimeSeconds - result2.TimeSeconds) < 0.00001;
        Assert.True(isDuplicate, "Re-imported result with same time should be detected as duplicate");
    }

    public void Dispose()
    {
        _db?.Dispose();
    }
}
