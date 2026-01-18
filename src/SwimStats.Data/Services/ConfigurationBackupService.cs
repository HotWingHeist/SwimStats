using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace SwimStats.Data.Services;

/// <summary>
/// Manages backups and version history of the swimmer configuration file.
/// Automatically creates timestamped backups to maintain change history.
/// </summary>
public class ConfigurationBackupService
{
    private readonly string _configDirectory;
    private readonly string _backupDirectory;
    private const int MaxBackups = 10; // Keep last 10 backups

    public ConfigurationBackupService()
    {
        _configDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SwimStats");
        
        _backupDirectory = Path.Combine(_configDirectory, "backups");
        
        // Ensure backup directory exists
        Directory.CreateDirectory(_backupDirectory);
    }

    /// <summary>
    /// Creates a backup of the current configuration file with timestamp.
    /// Automatically cleans up old backups if limit is exceeded.
    /// </summary>
    /// <returns>Path to the backup file created, or null if no config file exists</returns>
    public string? CreateBackup()
    {
        var configPath = Path.Combine(_configDirectory, "EZPCswimmers.json");
        
        if (!File.Exists(configPath))
        {
            System.Diagnostics.Debug.WriteLine("[ConfigBackup] No configuration file to backup");
            return null;
        }

        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var backupFileName = $"EZPCswimmers_backup_{timestamp}.json";
            var backupPath = Path.Combine(_backupDirectory, backupFileName);

            File.Copy(configPath, backupPath, overwrite: false);
            System.Diagnostics.Debug.WriteLine($"[ConfigBackup] Backup created: {backupPath}");

            // Clean up old backups
            CleanupOldBackups();

            return backupPath;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigBackup] ERROR creating backup: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Gets list of all available backups, ordered by date (newest first).
    /// </summary>
    public List<BackupInfo> GetAvailableBackups()
    {
        try
        {
            var backupFiles = Directory.GetFiles(_backupDirectory, "EZPCswimmers_backup_*.json")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();

            var backups = new List<BackupInfo>();
            foreach (var file in backupFiles)
            {
                var fileInfo = new FileInfo(file);
                backups.Add(new BackupInfo
                {
                    FilePath = file,
                    FileName = fileInfo.Name,
                    CreatedTime = File.GetLastWriteTime(file),
                    FileSizeBytes = fileInfo.Length
                });
            }

            return backups;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigBackup] ERROR listing backups: {ex.Message}");
            return new List<BackupInfo>();
        }
    }

    /// <summary>
    /// Restores configuration from a specific backup file.
    /// Creates a backup of current config before restoring.
    /// </summary>
    public bool RestoreBackup(string backupFilePath)
    {
        try
        {
            if (!File.Exists(backupFilePath))
            {
                throw new FileNotFoundException($"Backup file not found: {backupFilePath}");
            }

            var configPath = Path.Combine(_configDirectory, "EZPCswimmers.json");

            // Create backup of current config before restoring
            CreateBackup();

            // Restore from backup
            File.Copy(backupFilePath, configPath, overwrite: true);
            System.Diagnostics.Debug.WriteLine($"[ConfigBackup] Configuration restored from: {backupFilePath}");

            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigBackup] ERROR restoring backup: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Deletes a specific backup file.
    /// </summary>
    public bool DeleteBackup(string backupFilePath)
    {
        try
        {
            if (File.Exists(backupFilePath))
            {
                File.Delete(backupFilePath);
                System.Diagnostics.Debug.WriteLine($"[ConfigBackup] Backup deleted: {backupFilePath}");
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigBackup] ERROR deleting backup: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Deletes all backup files.
    /// </summary>
    public void ClearAllBackups()
    {
        try
        {
            var backupFiles = Directory.GetFiles(_backupDirectory, "EZPCswimmers_backup_*.json");
            foreach (var file in backupFiles)
            {
                File.Delete(file);
            }
            System.Diagnostics.Debug.WriteLine("[ConfigBackup] All backups cleared");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigBackup] ERROR clearing backups: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the path to the backup directory.
    /// </summary>
    public string GetBackupDirectoryPath()
    {
        return _backupDirectory;
    }

    private void CleanupOldBackups()
    {
        try
        {
            var backupFiles = Directory.GetFiles(_backupDirectory, "EZPCswimmers_backup_*.json")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();

            if (backupFiles.Count > MaxBackups)
            {
                var filesToDelete = backupFiles.Skip(MaxBackups);
                foreach (var file in filesToDelete)
                {
                    File.Delete(file);
                    System.Diagnostics.Debug.WriteLine($"[ConfigBackup] Old backup deleted: {file}");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ConfigBackup] ERROR cleanup old backups: {ex.Message}");
        }
    }
}

/// <summary>
/// Information about a configuration backup file.
/// </summary>
public class BackupInfo
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedTime { get; set; }
    public long FileSizeBytes { get; set; }

    public string DisplayName => $"{CreatedTime:yyyy-MM-dd HH:mm:ss} ({FormatFileSize(FileSizeBytes)})";

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
