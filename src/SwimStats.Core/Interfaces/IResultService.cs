using SwimStats.Core.Models;

namespace SwimStats.Core.Interfaces;

public interface IResultService
{
    Task<IEnumerable<Result>> GetResultsAsync(int swimmerId, Stroke? stroke = null, int? distance = null, Course? course = null);
    Task<double?> GetBestTimeAsync(int swimmerId, Stroke stroke, int distance);
}
