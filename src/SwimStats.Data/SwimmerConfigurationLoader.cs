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
                
                // Check that root is an array
                if (root.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException("Configuration file must contain a JSON array of swimmers at the root level. Example: [ { \"id\": 1, \"firstName\": \"John\", \"lastName\": \"Doe\" } ]");
                }

                // Validate each element
                int elementIndex = 0;
                foreach (var element in root.EnumerateArray())
                {
                    if (element.ValueKind != JsonValueKind.Object)
                    {
                        throw new InvalidOperationException($"Array element {elementIndex} is not a JSON object");
                    }

                    // Check required fields
                    if (!element.TryGetProperty("id", out _))
                    {
                        throw new InvalidOperationException($"Array element {elementIndex} is missing required field 'id'");
                    }

                    elementIndex++;
                }
            }

            // Now deserialize with confidence
            var result = JsonSerializer.Deserialize<List<SwimmerConfiguration>>(json) ?? new List<SwimmerConfiguration>();
            
            // Validate deserialized data
            foreach (var swimmer in result)
            {
                if (swimmer.Id <= 0)
                {
                    throw new InvalidOperationException($"Swimmer ID must be a positive integer, got: {swimmer.Id}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[SwimStats] Loaded {result.Count} swimmers successfully");
            return result;
        }
        catch (JsonException jsonEx)
        {
            System.Diagnostics.Debug.WriteLine($"[SwimStats] JSON PARSE ERROR in {filePath}: {jsonEx.Message}");
            throw new InvalidOperationException($"Invalid JSON format in configuration file: {jsonEx.Message}\n\nPlease ensure the file contains valid JSON. Use an online JSON validator to check your file.", jsonEx);
        }
        catch (InvalidOperationException)
        {
            // Re-throw validation errors as-is
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SwimStats] ERROR loading from {filePath}: {ex.Message}");
            throw new InvalidOperationException($"Error loading configuration file: {ex.Message}", ex);
        }
    }
}
