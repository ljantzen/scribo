namespace Scribo.Plugins;

/// <summary>
/// Base interface for all Scribo plugins
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Gets the unique identifier of the plugin
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the display name of the plugin
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the version of the plugin
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Gets the description of the plugin
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the author of the plugin
    /// </summary>
    string Author { get; }

    /// <summary>
    /// Called when the plugin is initialized
    /// </summary>
    void Initialize(IPluginContext context);

    /// <summary>
    /// Called when the plugin is enabled
    /// </summary>
    void OnEnabled();

    /// <summary>
    /// Called when the plugin is disabled
    /// </summary>
    void OnDisabled();

    /// <summary>
    /// Called when the plugin is being unloaded
    /// </summary>
    void Shutdown();
}
