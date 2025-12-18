using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Scribo.Models;
using Scribo.Plugins;

namespace Scribo.Services;

public class PluginManager : IDisposable
{
    private readonly Dictionary<string, IPlugin> _loadedPlugins = new();
    private readonly Dictionary<string, PluginInfo> _pluginInfos = new();
    private readonly List<Assembly> _pluginAssemblies = new();
    private readonly string _pluginsDirectory;
    private readonly string _pluginConfigFile;
    private IPluginContext? _context;

    public PluginManager(string? pluginsDirectory = null)
    {
        _pluginsDirectory = pluginsDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Scribo",
            "Plugins");

        _pluginConfigFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Scribo",
            "plugins.json");

        EnsurePluginsDirectoryExists();
        LoadPluginConfiguration();
    }

    public ReadOnlyCollection<PluginInfo> GetPlugins()
    {
        return _pluginInfos.Values.ToList().AsReadOnly();
    }

    public PluginInfo? GetPluginInfo(string pluginId)
    {
        return _pluginInfos.TryGetValue(pluginId, out var info) ? info : null;
    }

    public bool IsPluginLoaded(string pluginId)
    {
        return _loadedPlugins.ContainsKey(pluginId);
    }

    public bool IsPluginEnabled(string pluginId)
    {
        return _pluginInfos.TryGetValue(pluginId, out var info) && info.IsEnabled;
    }

    public void SetContext(IPluginContext context)
    {
        _context = context;
    }

    public void LoadPlugins()
    {
        if (_context == null)
            throw new InvalidOperationException("Plugin context must be set before loading plugins");

        var pluginFiles = Directory.GetFiles(_pluginsDirectory, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var pluginFile in pluginFiles)
        {
            try
            {
                LoadPlugin(pluginFile);
            }
            catch (Exception ex)
            {
            }
        }

        SavePluginConfiguration();
    }

    public void LoadPlugin(string pluginFilePath)
    {
        if (!File.Exists(pluginFilePath))
            throw new FileNotFoundException("Plugin file not found", pluginFilePath);

        var assembly = Assembly.LoadFrom(pluginFilePath);
        _pluginAssemblies.Add(assembly);

        var pluginTypes = assembly.GetTypes()
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .ToList();

        foreach (var pluginType in pluginTypes)
        {
            if (Activator.CreateInstance(pluginType) is IPlugin plugin)
            {
                var pluginId = plugin.Id;

                if (_loadedPlugins.ContainsKey(pluginId))
                {
                    throw new InvalidOperationException($"Plugin with ID '{pluginId}' is already loaded");
                }

                // Check if plugin is enabled
                if (!_pluginInfos.TryGetValue(pluginId, out var info))
                {
                    info = new PluginInfo
                    {
                        Id = pluginId,
                        Name = plugin.Name,
                        Version = plugin.Version,
                        Description = plugin.Description,
                        Author = plugin.Author,
                        FilePath = pluginFilePath,
                        IsEnabled = true,
                        IsInstalled = true,
                        InstalledAt = DateTime.Now
                    };
                    _pluginInfos[pluginId] = info;
                }

                if (info.IsEnabled && _context != null)
                {
                    plugin.Initialize(_context);
                    plugin.OnEnabled();
                    info.LastLoadedAt = DateTime.Now;
                }

                _loadedPlugins[pluginId] = plugin;
            }
        }

        SavePluginConfiguration();
    }

    public void UnloadPlugin(string pluginId)
    {
        if (!_loadedPlugins.TryGetValue(pluginId, out var plugin))
            return;

        try
        {
            plugin.OnDisabled();
            plugin.Shutdown();
        }
        catch (Exception ex)
        {
        }

        _loadedPlugins.Remove(pluginId);
    }

    public void EnablePlugin(string pluginId)
    {
        if (!_pluginInfos.TryGetValue(pluginId, out var info))
            throw new ArgumentException($"Plugin '{pluginId}' not found", nameof(pluginId));

        if (info.IsEnabled)
            return;

        info.IsEnabled = true;

        if (_loadedPlugins.TryGetValue(pluginId, out var plugin) && _context != null)
        {
            plugin.Initialize(_context);
            plugin.OnEnabled();
            info.LastLoadedAt = DateTime.Now;
        }

        SavePluginConfiguration();
    }

    public void DisablePlugin(string pluginId)
    {
        if (!_pluginInfos.TryGetValue(pluginId, out var info))
            throw new ArgumentException($"Plugin '{pluginId}' not found", nameof(pluginId));

        if (!info.IsEnabled)
            return;

        info.IsEnabled = false;

        if (_loadedPlugins.TryGetValue(pluginId, out var plugin))
        {
            plugin.OnDisabled();
        }

        SavePluginConfiguration();
    }

    public void InstallPlugin(string pluginFilePath)
    {
        if (!File.Exists(pluginFilePath))
            throw new FileNotFoundException("Plugin file not found", pluginFilePath);

        var fileName = Path.GetFileName(pluginFilePath);
        var destinationPath = Path.Combine(_pluginsDirectory, fileName);

        File.Copy(pluginFilePath, destinationPath, overwrite: true);

        var pluginId = ExtractPluginId(pluginFilePath);
        if (pluginId != null && !_pluginInfos.ContainsKey(pluginId))
        {
            _pluginInfos[pluginId] = new PluginInfo
            {
                Id = pluginId,
                FilePath = destinationPath,
                IsInstalled = true,
                InstalledAt = DateTime.Now
            };
        }

        SavePluginConfiguration();
    }

    public void RemovePlugin(string pluginId)
    {
        if (!_pluginInfos.TryGetValue(pluginId, out var info))
            throw new ArgumentException($"Plugin '{pluginId}' not found", nameof(pluginId));

        // Unload if currently loaded
        if (_loadedPlugins.ContainsKey(pluginId))
        {
            UnloadPlugin(pluginId);
        }

        // Delete plugin file
        if (File.Exists(info.FilePath))
        {
            try
            {
                File.Delete(info.FilePath);
            }
            catch (Exception ex)
            {
            }
        }

        _pluginInfos.Remove(pluginId);
        SavePluginConfiguration();
    }

    private string? ExtractPluginId(string pluginFilePath)
    {
        try
        {
            var assembly = Assembly.LoadFrom(pluginFilePath);
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            if (pluginTypes.Count > 0 && Activator.CreateInstance(pluginTypes[0]) is IPlugin plugin)
            {
                return plugin.Id;
            }
        }
        catch
        {
            // Ignore errors during extraction
        }

        return null;
    }

    private void EnsurePluginsDirectoryExists()
    {
        if (!Directory.Exists(_pluginsDirectory))
        {
            Directory.CreateDirectory(_pluginsDirectory);
        }
    }

    private void LoadPluginConfiguration()
    {
        if (!File.Exists(_pluginConfigFile))
            return;

        try
        {
            var json = File.ReadAllText(_pluginConfigFile);
            var config = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, PluginInfo>>(json);
            if (config != null)
            {
                foreach (var kvp in config)
                {
                    _pluginInfos[kvp.Key] = kvp.Value;
                }
            }
        }
        catch (Exception ex)
        {
        }
    }

    private void SavePluginConfiguration()
    {
        try
        {
            var directory = Path.GetDirectoryName(_pluginConfigFile);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = System.Text.Json.JsonSerializer.Serialize(_pluginInfos, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_pluginConfigFile, json);
        }
        catch (Exception ex)
        {
        }
    }

    public void Dispose()
    {
        foreach (var plugin in _loadedPlugins.Values)
        {
            try
            {
                plugin.OnDisabled();
                plugin.Shutdown();
            }
            catch
            {
                // Ignore errors during shutdown
            }
        }

        _loadedPlugins.Clear();
        _pluginAssemblies.Clear();
    }
}
