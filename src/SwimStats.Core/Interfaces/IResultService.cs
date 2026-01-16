using SwimStats.Core.Models;

namespace SwimStats.Core.Interfaces;

public interface IResultService
{
    Task<IEnumerable<Result>> GetResultsAsync(int swimmerId, Stroke? stroke = null, int? distance = null);
    Task<double?> GetBestTimeAsync(int swimmerId, Stroke stroke, int distance);
}
