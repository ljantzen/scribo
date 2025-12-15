using System;
using System.Collections.Generic;
using Scribo.Plugins;

namespace Scribo.Services;

public class PluginContext : IPluginContext
{
    private readonly Dictionary<string, object> _services = new();
    private object? _mainWindow;

    public object? MainWindow => _mainWindow;

    public void SetMainWindow(object mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public void RegisterService<T>(T service) where T : class
    {
        _services[typeof(T).FullName ?? typeof(T).Name] = service;
    }

    public void RegisterMenuItem(string menuPath, string header, Action action)
    {
        // TODO: Implement menu item registration
        // This would need to be integrated with the main window's menu system
        System.Diagnostics.Debug.WriteLine($"Register menu item: {menuPath}/{header}");
    }

    public T? GetService<T>() where T : class
    {
        var key = typeof(T).FullName ?? typeof(T).Name;
        return _services.TryGetValue(key, out var service) ? service as T : null;
    }

    public void Log(string message, LogLevel level = LogLevel.Info)
    {
        var levelStr = level switch
        {
            LogLevel.Debug => "DEBUG",
            LogLevel.Info => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            _ => "INFO"
        };
        System.Diagnostics.Debug.WriteLine($"[{levelStr}] {message}");
    }
}
