using Microsoft.EntityFrameworkCore;
using SwimStats.Data;
using SwimStats.Data.Services;
using Xunit;
using SwimStats.Core.Models;

namespace SwimStats.Tests;

public class ResultServiceTests : IDisposable
{
    private readonly SwimStatsDbContext _db;
    private readonly ResultService _service;

    public ResultServiceTests()
    {
        var options = new DbContextOptionsBuilder<SwimStatsDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new SwimStatsDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _service = new ResultService(_db);
        
        SeedTestData();
    }

    private void SeedTestData()
    {
        var swimmer1 = new Swimmer { Id = 1, Name = "Test Swimmer 1" };
        var swimmer2 = new Swimmer { Id = 2, Name = "Test Swimmer 2" };
        _db.Swimmers.AddRange(swimmer1, swimmer2);

        var event50Free = new Event { Id = 1, Stroke = Stroke.Freestyle, DistanceMeters = 50 };
        var event100Free = new Event { Id = 2, Stroke = Stroke.Freestyle, DistanceMeters = 100 };
        var event50Back = new Event { Id = 3, Stroke = Stroke.Backstroke, DistanceMeters = 50 };
        _db.Events.AddRange(event50Free, event100Free, event50Back);

        _db.Results.AddRange(
            new Result { SwimmerId = 1, EventId = 1, Date = new DateTime(2024, 1, 1), TimeSeconds = 25.5 },
            new Result { SwimmerId = 1, EventId = 1, Date = new DateTime(2024, 2, 1), TimeSeconds = 24.9 },
            new Result { SwimmerId = 1, EventId = 1, Date = new DateTime(2024, 3, 1), TimeSeconds = 25.2 },
            new Result { SwimmerId = 1, EventId = 2, Date = new DateTime(2024, 1, 1), TimeSeconds = 55.3 },
            new Result { SwimmerId = 2, EventId = 1, Date = new DateTime(2024, 1, 1), TimeSeconds = 26.1 },
            new Result { SwimmerId = 2, EventId = 1, Date = new DateTime(2024, 2, 1), TimeSeconds = 25.8 },
            new Result { SwimmerId = 1, EventId = 3, Date = new DateTime(2024, 1, 1), TimeSeconds = 28.5 }
        );

        _db.SaveChanges();
    }

    [Fact]
    public async Task GetBestTimeAsync_ReturnsLowestTime()
    {
        var best = await _service.GetBestTimeAsync(1, Stroke.Freestyle, 50);
        
        Assert.NotNull(best);
        Assert.Equal(24.9, best.Value, 1);
    }

    [Fact]
    public async Task GetBestTimeAsync_ReturnsNullWhenNoResults()
    {
        var best = await _service.GetBestTimeAsync(999, Stroke.Freestyle, 50);
        
        Assert.Null(best);
    }

    [Fact]
    public async Task GetResultsAsync_ReturnsResultsForSwimmerStrokeAndDistance()
    {
        var results = (await _service.GetResultsAsync(1, Stroke.Freestyle, 50)).ToList();
        
        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.Equal(1, r.SwimmerId));
        Assert.All(results, r => Assert.Equal(Stroke.Freestyle, r.Event.Stroke));
        Assert.All(results, r => Assert.Equal(50, r.Event.DistanceMeters));
    }

    [Fact]
    public async Task GetResultsAsync_OrdersByDateAscending()
    {
        var results = (await _service.GetResultsAsync(1, Stroke.Freestyle, 50)).ToList();
        
        Assert.Equal(new DateTime(2024, 1, 1), results[0].Date);
        Assert.Equal(new DateTime(2024, 2, 1), results[1].Date);
        Assert.Equal(new DateTime(2024, 3, 1), results[2].Date);
    }

    [Fact]
    public async Task GetResultsAsync_FiltersCorrectlyByStroke()
    {
        var freestyleResults = (await _service.GetResultsAsync(1, Stroke.Freestyle, 50)).ToList();
        var backstrokeResults = (await _service.GetResultsAsync(1, Stroke.Backstroke, 50)).ToList();
        
        Assert.Equal(3, freestyleResults.Count);
        Assert.Single(backstrokeResults);
        Assert.Equal(28.5, backstrokeResults[0].TimeSeconds, 1);
    }

    [Fact]
    public async Task GetResultsAsync_FiltersCorrectlyByDistance()
    {
        var results50 = (await _service.GetResultsAsync(1, Stroke.Freestyle, 50)).ToList();
        var results100 = (await _service.GetResultsAsync(1, Stroke.Freestyle, 100)).ToList();
        
        Assert.Equal(3, results50.Count);
        Assert.Single(results100);
        Assert.Equal(55.3, results100[0].TimeSeconds, 1);
    }

    [Fact]
    public async Task GetResultsAsync_ReturnsEmptyListWhenNoMatches()
    {
        var results = (await _service.GetResultsAsync(1, Stroke.Butterfly, 200)).ToList();
        
        Assert.Empty(results);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
