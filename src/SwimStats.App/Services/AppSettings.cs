using System.IO;
using System.Text.Json;
using SwimStats.Core.Models;

namespace SwimStats.App.Services;

public class AppSettings
{
    private static AppSettings? _instance;
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SwimStats",
        "settings.json"
    );

    public static AppSettings Instance => _instance ??= Load();

    public string Language { get; set; } = "en";
    public string? SelectedStroke { get; set; }
    public int? SelectedDistance { get; set; }
    public List<int> SelectedSwimmerIds { get; set; } = new();

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Ignore load errors
        }

        return new AppSettings();
    }
}
