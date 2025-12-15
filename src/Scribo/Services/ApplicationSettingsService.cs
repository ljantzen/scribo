using System;
using System.IO;
using System.Text.Json;
using Scribo.Models;

namespace Scribo.Services;

public class ApplicationSettingsService
{
    private readonly string _settingsFilePath;
    private ApplicationSettings? _cachedSettings;

    public ApplicationSettingsService()
    {
        // Store settings in user's app data directory
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appSettingsDir = Path.Combine(appDataPath, "Scribo");
        
        if (!Directory.Exists(appSettingsDir))
        {
            Directory.CreateDirectory(appSettingsDir);
        }
        
        _settingsFilePath = Path.Combine(appSettingsDir, "settings.json");
    }

    public ApplicationSettings LoadSettings()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        if (!File.Exists(_settingsFilePath))
        {
            _cachedSettings = new ApplicationSettings
            {
                KeyboardShortcuts = ApplicationSettings.GetDefaultShortcuts()
            };
            SaveSettings(_cachedSettings);
            return _cachedSettings;
        }

        try
        {
            var json = File.ReadAllText(_settingsFilePath);
            _cachedSettings = JsonSerializer.Deserialize<ApplicationSettings>(json) ?? new ApplicationSettings();
            
            // Ensure all default shortcuts exist
            var defaults = ApplicationSettings.GetDefaultShortcuts();
            foreach (var kvp in defaults)
            {
                if (!_cachedSettings.KeyboardShortcuts.ContainsKey(kvp.Key))
                {
                    _cachedSettings.KeyboardShortcuts[kvp.Key] = kvp.Value;
                }
            }
            
            return _cachedSettings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
            _cachedSettings = new ApplicationSettings
            {
                KeyboardShortcuts = ApplicationSettings.GetDefaultShortcuts()
            };
            return _cachedSettings;
        }
    }

    public void SaveSettings(ApplicationSettings settings)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };
            var json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(_settingsFilePath, json);
            _cachedSettings = settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
        }
    }

    public void ClearCache()
    {
        _cachedSettings = null;
    }
}
