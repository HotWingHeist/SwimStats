namespace SwimStats.Core.Interfaces;

public interface ISwimTrackImporter
{
    Task<int> ImportSwimmersAsync(string url);
    Task<int> ImportResultsAsync(string url);
}
