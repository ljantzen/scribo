namespace Scribo.Plugins;

/// <summary>
/// Base class for plugins providing default implementations
/// </summary>
public abstract class PluginBase : IPlugin
{
    public abstract string Id { get; }
    public abstract string Name { get; }
    public abstract string Version { get; }
    public abstract string Description { get; }
    public abstract string Author { get; }

    protected IPluginContext? Context { get; private set; }

    public virtual void Initialize(IPluginContext context)
    {
        Context = context;
    }

    public virtual void OnEnabled()
    {
        // Override in derived classes
    }

    public virtual void OnDisabled()
    {
        // Override in derived classes
    }

    public virtual void Shutdown()
    {
        // Override in derived classes
    }
}
