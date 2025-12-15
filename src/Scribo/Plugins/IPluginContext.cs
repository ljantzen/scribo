using System;

namespace Scribo.Plugins;

/// <summary>
/// Provides context and services to plugins
/// </summary>
public interface IPluginContext
{
    /// <summary>
    /// Gets the application's main window
    /// </summary>
    object? MainWindow { get; }

    /// <summary>
    /// Registers a menu item in the application menu
    /// </summary>
    void RegisterMenuItem(string menuPath, string header, Action action);

    /// <summary>
    /// Gets a service from the application's service container
    /// </summary>
    T? GetService<T>() where T : class;

    /// <summary>
    /// Logs a message from the plugin
    /// </summary>
    void Log(string message, LogLevel level = LogLevel.Info);
}

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error
}
