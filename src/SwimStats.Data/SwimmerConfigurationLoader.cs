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

public class ClubConfiguration
{
    [JsonPropertyName("clubName")]
    public string ClubName { get; set; } = string.Empty;
    
    [JsonPropertyName("swimmers")]
    public List<SwimmerConfiguration> Swimmers { get; set; } = new();
}

public static class SwimmerConfigurationLoader
{
    private static string _clubName = string.Empty;
    
    public static string ClubName => _clubName;
    
    public static List<SwimmerConfiguration> LoadSwimmers()
    {
        try
        {
            // Try to load from AppData first (user-editable location)
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SwimStats",
                "EZPCswimmers.json");

            if (File.Exists(appDataPath))
            {
                System.Diagnostics.Debug.WriteLine($"[SwimStats] Loading swimmers from AppData: {appDataPath}");
                return LoadFromFile(appDataPath);
            }

            // Fall back to the embedded configuration file in the assembly directory
            var assemblyPath = Path.GetDirectoryName(typeof(SwimmerConfigurationLoader).Assembly.Location) ?? "";
            var configPath = Path.Combine(assemblyPath, "EZPCswimmers.json");

            if (File.Exists(configPath))
            {
                System.Diagnostics.Debug.WriteLine($"[SwimStats] Found EZPCswimmers.json at assembly location, copying to AppData");
                // Copy to AppData for editing
                Directory.CreateDirectory(Path.GetDirectoryName(appDataPath) ?? "");
                File.Copy(configPath, appDataPath, overwrite: false);
                return LoadFromFile(appDataPath);
            }

            System.Diagnostics.Debug.WriteLine("[SwimStats] WARNING: No EZPCswimmers.json file found");
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
        try
        {
            var json = File.ReadAllText(filePath);
            
            // Validate JSON format
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidOperationException("Configuration file is empty");
            }

            // Try to parse to validate JSON structure
            using (JsonDocument doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;
                
                // Check if root is an object (new format with clubName) or array (old format)
                if (root.ValueKind == JsonValueKind.Object)
                {
                    // New format: { "clubName": "EZPC", "swimmers": [...] }
                    if (!root.TryGetProperty("clubName", out var clubNameProp))
                    {
                        throw new InvalidOperationException("Configuration file must contain a 'clubName' property. Example: { \"clubName\": \"EZPC\", \"swimmers\": [ { \"id\": 1, \"firstName\": \"John\", \"lastName\": \"Doe\" } ] }");
                    }
                    
                    var clubNameValue = clubNameProp.GetString();
                    if (string.IsNullOrWhiteSpace(clubNameValue))
                    {
                        throw new InvalidOperationException("'clubName' property cannot be empty");
                    }
                    
                    _clubName = clubNameValue;
                    System.Diagnostics.Debug.WriteLine($"[SwimStats] Loaded club name: {_clubName}");
                    
                    if (!root.TryGetProperty("swimmers", out var swimmersProp))
                    {
                        throw new InvalidOperationException("Configuration file must contain a 'swimmers' array. Example: { \"clubName\": \"EZPC\", \"swimmers\": [ { \"id\": 1, \"firstName\": \"John\", \"lastName\": \"Doe\" } ] }");
                    }
                    
                    if (swimmersProp.ValueKind != JsonValueKind.Array)
                    {
                        throw new InvalidOperationException("'swimmers' property must be a JSON array");
                    }
                    
                    // Validate swimmer array elements
                    ValidateSwimmerArray(swimmersProp);
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    // Old format not supported - require clubName
                    throw new InvalidOperationException("Old configuration format is no longer supported. Please update your configuration file to the new format with 'clubName' property. Example: { \"clubName\": \"YourClubName\", \"swimmers\": [ { \"id\": 1, \"firstName\": \"John\", \"lastName\": \"Doe\" } ] }");
                }
                else
                {
                    throw new InvalidOperationException("Configuration file must contain a JSON object with 'clubName' and 'swimmers' properties");
                }
            }

            // Now deserialize with confidence
            List<SwimmerConfiguration> result;
            
            var clubConfig = JsonSerializer.Deserialize<ClubConfiguration>(json);
            if (clubConfig == null)
            {
                throw new InvalidOperationException("Failed to deserialize configuration file");
            }
            
            if (string.IsNullOrWhiteSpace(clubConfig.ClubName))
            {
                throw new InvalidOperationException("Club name is required in the configuration file");
            }
            
            result = clubConfig.Swimmers;
            
            // Validate deserialized data
            foreach (var swimmer in result)
            {
                if (swimmer.Id <= 0)
                {
                    throw new InvalidOperationException($"Swimmer ID must be a positive integer, got: {swimmer.Id}");
                }
                
                if (string.IsNullOrWhiteSpace(swimmer.FirstName))
                {
                    throw new InvalidOperationException($"Swimmer with ID {swimmer.Id} is missing 'firstName'");
                }
                
                if (string.IsNullOrWhiteSpace(swimmer.LastName))
                {
                    throw new InvalidOperationException($"Swimmer with ID {swimmer.Id} is missing 'lastName'");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[SwimStats] Loaded {result.Count} swimmers successfully from club: {_clubName}");
            return result;
        }
        catch (JsonException jsonEx)
        {
            System.Diagnostics.Debug.WriteLine($"[SwimStats] JSON PARSE ERROR in {filePath}: {jsonEx.Message}");
            throw new InvalidOperationException($"Invalid JSON format in configuration file: {jsonEx.Message}\n\nPlease ensure the file contains valid JSON. Use an online JSON validator to check your file.", jsonEx);
        }
    }
    
    private static void ValidateSwimmerArray(JsonElement arrayElement)
    {
        int elementIndex = 0;
        foreach (var element in arrayElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Swimmer at index {elementIndex} is not a JSON object");
            }

            // Check required fields
            if (!element.TryGetProperty("id", out _))
            {
                throw new InvalidOperationException($"Swimmer at index {elementIndex} is missing required field 'id'");
            }
            
            if (!element.TryGetProperty("firstName", out _))
            {
                throw new InvalidOperationException($"Swimmer at index {elementIndex} is missing required field 'firstName'");
            }
            
            if (!element.TryGetProperty("lastName", out _))
            {
                throw new InvalidOperationException($"Swimmer at index {elementIndex} is missing required field 'lastName'");
            }

            elementIndex++;
        }
    }
}
