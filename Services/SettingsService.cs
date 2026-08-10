using System;
using System.IO;
using System.Text.Json;
using YtDlpGui.Models;

namespace YtDlpGui.Services;

public class SettingsService
{
    private readonly string _settingsFilePath;

    public SettingsService()
    {
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataFolder, "YtDlpGui");
        _settingsFilePath = Path.Combine(appFolder, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            // Ignore and return defaults
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}
