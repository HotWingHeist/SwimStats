using Microsoft.EntityFrameworkCore;
using SwimStats.Core.Models;
using SwimStats.Data;
using Xunit;

namespace SwimStats.Tests;

public class DatabaseTests : IDisposable
{
    private readonly SwimStatsDbContext _db;

    public DatabaseTests()
    {
        var options = new DbContextOptionsBuilder<SwimStatsDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _db = new SwimStatsDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
    }

    [Fact]
    public void CanCreateSwimmer()
    {
        var swimmer = new Swimmer { FirstName = "John", LastName = "Doe" };
        _db.Swimmers.Add(swimmer);
        _db.SaveChanges();

        var retrieved = _db.Swimmers.First();
        Assert.Equal("John Doe", retrieved.DisplayName);
        Assert.True(retrieved.Id > 0);
    }

    [Fact]
    public void CanCreateEvent()
    {
        var evt = new Event { Stroke = Stroke.Freestyle, DistanceMeters = 100 };
        _db.Events.Add(evt);
        _db.SaveChanges();

        var retrieved = _db.Events.First();
        Assert.Equal(Stroke.Freestyle, retrieved.Stroke);
        Assert.Equal(100, retrieved.DistanceMeters);
    }

    [Fact]
    public void CanCreateResultWithRelationships()
    {
        var swimmer = new Swimmer { FirstName = "Jane", LastName = "Smith" };
        var evt = new Event { Stroke = Stroke.Backstroke, DistanceMeters = 50 };
        _db.Swimmers.Add(swimmer);
        _db.Events.Add(evt);
        _db.SaveChanges();

        var result = new Result
        {
            SwimmerId = swimmer.Id,
            EventId = evt.Id,
            Date = new DateTime(2024, 6, 15),
            TimeSeconds = 32.5
        };
        _db.Results.Add(result);
        _db.SaveChanges();

        var retrieved = _db.Results
            .Include(r => r.Swimmer)
            .Include(r => r.Event)
            .First();

        Assert.Equal("Jane Smith", retrieved.Swimmer.DisplayName);
        Assert.Equal(Stroke.Backstroke, retrieved.Event.Stroke);
        Assert.Equal(50, retrieved.Event.DistanceMeters);
        Assert.Equal(32.5, retrieved.TimeSeconds, 1);
    }

    [Fact]
    public void SwimmerCanHaveMultipleResults()
    {
        var swimmer = new Swimmer { FirstName = "Multi", LastName = "Swimmer" };
        var event50 = new Event { Stroke = Stroke.Freestyle, DistanceMeters = 50 };
        var event100 = new Event { Stroke = Stroke.Freestyle, DistanceMeters = 100 };
        _db.Swimmers.Add(swimmer);
        _db.Events.AddRange(event50, event100);
        _db.SaveChanges();

        _db.Results.AddRange(
            new Result { SwimmerId = swimmer.Id, EventId = event50.Id, Date = DateTime.Now, TimeSeconds = 25.0 },
            new Result { SwimmerId = swimmer.Id, EventId = event100.Id, Date = DateTime.Now, TimeSeconds = 55.0 }
        );
        _db.SaveChanges();

        var results = _db.Results.Where(r => r.SwimmerId == swimmer.Id).ToList();
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void EventCanHaveMultipleResults()
    {
        var swimmer1 = new Swimmer { FirstName = "Swimmer", LastName = "1" };
        var swimmer2 = new Swimmer { FirstName = "Swimmer", LastName = "2" };
        var evt = new Event { Stroke = Stroke.Butterfly, DistanceMeters = 100 };
        _db.Swimmers.AddRange(swimmer1, swimmer2);
        _db.Events.Add(evt);
        _db.SaveChanges();

        _db.Results.AddRange(
            new Result { SwimmerId = swimmer1.Id, EventId = evt.Id, Date = DateTime.Now, TimeSeconds = 60.0 },
            new Result { SwimmerId = swimmer2.Id, EventId = evt.Id, Date = DateTime.Now, TimeSeconds = 62.0 }
        );
        _db.SaveChanges();

        var results = _db.Results.Where(r => r.EventId == evt.Id).ToList();
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void CanQueryResultsByDateRange()
    {
        var swimmer = new Swimmer { FirstName = "Test", LastName = "" };
        var evt = new Event { Stroke = Stroke.Freestyle, DistanceMeters = 50 };
        _db.Swimmers.Add(swimmer);
        _db.Events.Add(evt);
        _db.SaveChanges();

        _db.Results.AddRange(
            new Result { SwimmerId = swimmer.Id, EventId = evt.Id, Date = new DateTime(2024, 1, 1), TimeSeconds = 25.0 },
            new Result { SwimmerId = swimmer.Id, EventId = evt.Id, Date = new DateTime(2024, 6, 1), TimeSeconds = 24.5 },
            new Result { SwimmerId = swimmer.Id, EventId = evt.Id, Date = new DateTime(2024, 12, 1), TimeSeconds = 24.0 }
        );
        _db.SaveChanges();

        var startDate = new DateTime(2024, 5, 1);
        var endDate = new DateTime(2024, 12, 31);
        var results = _db.Results
            .Where(r => r.Date >= startDate && r.Date <= endDate)
            .ToList();

        Assert.Equal(2, results.Count);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
