namespace SwimStats.Core.Interfaces;

public interface ISwimTrackImporter
{
    Task<int> ImportSwimmersAsync(string url);
    Task<int> ImportResultsAsync(string url);
    Task<(int retrieved, int newCount, int existing)> ImportSingleSwimmerAsync(string swimmerName);
    Task<(int retrieved, int newCount, int existing)> ImportSwimmerByNameAsync(string firstName, string lastName);
    Task<bool> IsWebsiteReachableAsync();
}

