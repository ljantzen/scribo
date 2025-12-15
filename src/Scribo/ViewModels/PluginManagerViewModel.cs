using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scribo.Models;
using Scribo.Services;

namespace Scribo.ViewModels;

public partial class PluginManagerViewModel : ViewModelBase
{
    private readonly PluginManager _pluginManager;

    [ObservableProperty]
    private ObservableCollection<PluginInfoViewModel> plugins = new();

    [ObservableProperty]
    private PluginInfoViewModel? selectedPlugin;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public bool HasSelectedPlugin => SelectedPlugin != null;

    public PluginManagerViewModel(PluginManager pluginManager)
    {
        _pluginManager = pluginManager;
        RefreshPlugins();
    }

    private void RefreshPlugins()
    {
        Plugins.Clear();
        foreach (var pluginInfo in _pluginManager.GetPlugins())
        {
            Plugins.Add(new PluginInfoViewModel(pluginInfo, _pluginManager));
        }
    }

    [RelayCommand]
    private void Refresh()
    {
        RefreshPlugins();
        StatusMessage = "Plugins refreshed";
    }

    [RelayCommand]
    private async Task InstallPlugin()
    {
        // This would typically open a file dialog
        // For now, we'll just show a message
        StatusMessage = "Use File > Open to select a plugin file (.dll)";
    }

    [RelayCommand]
    private void EnablePlugin()
    {
        if (SelectedPlugin == null)
            return;

        try
        {
            _pluginManager.EnablePlugin(SelectedPlugin.Id);
            SelectedPlugin.IsEnabled = true;
            StatusMessage = $"Plugin '{SelectedPlugin.Name}' enabled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void DisablePlugin()
    {
        if (SelectedPlugin == null)
            return;

        try
        {
            _pluginManager.DisablePlugin(SelectedPlugin.Id);
            SelectedPlugin.IsEnabled = false;
            StatusMessage = $"Plugin '{SelectedPlugin.Name}' disabled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RemovePlugin()
    {
        if (SelectedPlugin == null)
            return;

        try
        {
            _pluginManager.RemovePlugin(SelectedPlugin.Id);
            Plugins.Remove(SelectedPlugin);
            SelectedPlugin = null;
            StatusMessage = "Plugin removed";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    partial void OnSelectedPluginChanged(PluginInfoViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedPlugin));
    }
}

public partial class PluginInfoViewModel : ViewModelBase
{
    private readonly PluginManager _pluginManager;
    private readonly PluginInfo _pluginInfo;

    public string Id => _pluginInfo.Id;
    public string Name => _pluginInfo.Name;
    public string Version => _pluginInfo.Version;
    public string Description => _pluginInfo.Description;
    public string Author => _pluginInfo.Author;
    public bool IsInstalled => _pluginInfo.IsInstalled;
    public DateTime InstalledAt => _pluginInfo.InstalledAt;

    [ObservableProperty]
    private bool isEnabled;

    public PluginInfoViewModel(PluginInfo pluginInfo, PluginManager pluginManager)
    {
        _pluginInfo = pluginInfo;
        _pluginManager = pluginManager;
        IsEnabled = pluginInfo.IsEnabled;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        _pluginInfo.IsEnabled = value;
    }
}
