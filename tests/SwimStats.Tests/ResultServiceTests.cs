using Microsoft.EntityFrameworkCore;
using SwimStats.Data;
using SwimStats.Data.Services;
using Xunit;
using SwimStats.Core.Models;

namespace SwimStats.Tests;

public class ResultServiceTests
{
    [Fact]
    public async Task GetBestTime_ReturnsSeededBest()
    {
        var options = new DbContextOptionsBuilder<SwimStatsDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        using var db = new SwimStatsDbContext(options);
        db.Database.OpenConnection();
        db.Database.EnsureCreated();

        var svc = new ResultService(db);
        var best = await svc.GetBestTimeAsync(1, Stroke.Freestyle, 50);
        Assert.NotNull(best);
        Assert.Equal(24.9, best.Value, 1);
    }
}
