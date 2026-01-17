namespace SwimStats.Core.Interfaces;

public interface ISwimTrackImporter
{
    Task<int> ImportSwimmersAsync(string url);
    Task<int> ImportResultsAsync(string url);
    Task<int> ImportSingleSwimmerAsync(string swimmerName);
    Task<int> ImportSwimmerByNameAsync(string firstName, string lastName);
    Task<bool> IsWebsiteReachableAsync();
}

