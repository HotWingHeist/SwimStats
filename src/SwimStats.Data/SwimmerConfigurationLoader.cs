using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwimStats.Data;

public class SwimmerConfiguration
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = string.Empty;
    
    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = string.Empty;
    
    // Fallback to support old format
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public static class SwimmerConfigurationLoader
{
    public static List<SwimmerConfiguration> LoadSwimmers()
    {
        try
        {
            // Try to load from AppData first (user-editable location)
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SwimStats",
                "swimmers.json");

            if (File.Exists(appDataPath))
            {
                System.Diagnostics.Debug.WriteLine($"[SwimStats] Loading swimmers from AppData: {appDataPath}");
                return LoadFromFile(appDataPath);
            }

            // Fall back to the embedded configuration file in the assembly directory
            var assemblyPath = Path.GetDirectoryName(typeof(SwimmerConfigurationLoader).Assembly.Location) ?? "";
            var configPath = Path.Combine(assemblyPath, "swimmers.json");

            if (File.Exists(configPath))
            {
                System.Diagnostics.Debug.WriteLine($"[SwimStats] Found swimmers.json at assembly location, copying to AppData");
                // Copy to AppData for editing
                Directory.CreateDirectory(Path.GetDirectoryName(appDataPath) ?? "");
                File.Copy(configPath, appDataPath, overwrite: false);
                return LoadFromFile(appDataPath);
            }

            System.Diagnostics.Debug.WriteLine("[SwimStats] WARNING: No swimmers.json file found");
            // If no file found, return empty list
            return new List<SwimmerConfiguration>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SwimStats] ERROR loading swimmers: {ex.Message}");
            return new List<SwimmerConfiguration>();
        }
    }

    private static List<SwimmerConfiguration> LoadFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var result = JsonSerializer.Deserialize<List<SwimmerConfiguration>>(json) ?? new List<SwimmerConfiguration>();
        System.Diagnostics.Debug.WriteLine($"[SwimStats] Loaded {result.Count} swimmers");
        return result;
    }
}
