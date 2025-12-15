using System;

namespace Scribo.Models;

public class PluginInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsInstalled { get; set; } = true;
    public DateTime InstalledAt { get; set; } = DateTime.Now;
    public DateTime? LastLoadedAt { get; set; }
}
