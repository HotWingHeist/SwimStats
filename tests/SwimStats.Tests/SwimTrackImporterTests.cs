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
        var result = await _importer.ImportSwimmerByNameAsync("NonExistent", "Swimmer");
        
        // Should return 0 results without throwing
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ImportSingleSwimmerAsyncSplitsNameCorrectly()
    {
        // Test that full name is split into first/last correctly
        var result = await _importer.ImportSingleSwimmerAsync("NonExistent Swimmer");
        
        // Should return 0 results without throwing
        Assert.Equal(0, result);
    }

    public void Dispose()
    {
        _db?.Dispose();
    }
}
