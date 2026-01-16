using Microsoft.EntityFrameworkCore;
using SwimStats.Core.Interfaces;
using SwimStats.Core.Models;

namespace SwimStats.Data.Services;

public class ResultService : IResultService
{
    private readonly SwimStatsDbContext _db;

    public ResultService(SwimStatsDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<Result>> GetResultsAsync(int swimmerId, Stroke? stroke = null, int? distance = null)
    {
        var q = _db.Results.Include(r => r.Event).Where(r => r.SwimmerId == swimmerId).AsQueryable();
        if (stroke != null)
            q = q.Where(r => r.Event!.Stroke == stroke);
        if (distance != null)
            q = q.Where(r => r.Event!.DistanceMeters == distance);
        return await q.OrderBy(r => r.Date).ToListAsync();
    }

    public async Task<double?> GetBestTimeAsync(int swimmerId, Stroke stroke, int distance)
    {
        var res = await _db.Results.Include(r => r.Event)
            .Where(r => r.SwimmerId == swimmerId && r.Event!.Stroke == stroke && r.Event.DistanceMeters == distance)
            .OrderBy(r => r.TimeSeconds)
            .FirstOrDefaultAsync();
        return res?.TimeSeconds;
    }
}
