using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scribo.Models;
using Scribo.Services;

namespace Scribo.ViewModels;

public partial class KeyboardShortcutViewModel : ViewModelBase
{
    [ObservableProperty]
    private string actionName = string.Empty;

    [ObservableProperty]
    private string displayName = string.Empty;

    [ObservableProperty]
    private string shortcut = string.Empty;

    public KeyboardShortcutViewModel(string actionName, string displayName, string shortcut)
    {
        ActionName = actionName;
        DisplayName = displayName;
        Shortcut = shortcut;
    }
}

public partial class PreferencesViewModel : ViewModelBase
{
    private readonly ApplicationSettingsService _settingsService;

    [ObservableProperty]
    private bool autoSave = true;

    [ObservableProperty]
    private int autoSaveIntervalMinutes = 5;

    [ObservableProperty]
    private bool backupOnSave = true;

    [ObservableProperty]
    private int maxBackups = 10;

    [ObservableProperty]
    private string defaultFontFamily = "Consolas";

    [ObservableProperty]
    private int defaultFontSize = 14;

    [ObservableProperty]
    private bool wordWrap = true;

    [ObservableProperty]
    private bool showLineNumbers = false;

    [ObservableProperty]
    private bool showRuler = false;

    [ObservableProperty]
    private string theme = "Light";

    [ObservableProperty]
    private bool showWordCountInStatusBar = true;

    [ObservableProperty]
    private bool showCharacterCountInStatusBar = true;

    [ObservableProperty]
    private bool showPageCountInStatusBar = false;

    [ObservableProperty]
    private ObservableCollection<KeyboardShortcutViewModel> keyboardShortcuts = new();

    private readonly Action<PreferencesViewModel>? _onSave;

    public PreferencesViewModel(Action<PreferencesViewModel>? onSave = null, ApplicationSettingsService? settingsService = null)
    {
        _settingsService = settingsService ?? new ApplicationSettingsService();
        _onSave = onSave;
        LoadPreferences();
    }

    private void LoadPreferences()
    {
        var settings = _settingsService.LoadSettings();
        
        AutoSave = settings.AutoSave;
        AutoSaveIntervalMinutes = settings.AutoSaveIntervalMinutes;
        BackupOnSave = settings.BackupOnSave;
        MaxBackups = settings.MaxBackups;
        DefaultFontFamily = settings.DefaultFontFamily;
        DefaultFontSize = settings.DefaultFontSize;
        WordWrap = settings.WordWrap;
        ShowLineNumbers = settings.ShowLineNumbers;
        ShowRuler = settings.ShowRuler;
        Theme = settings.Theme;
        ShowWordCountInStatusBar = settings.ShowWordCountInStatusBar;
        ShowCharacterCountInStatusBar = settings.ShowCharacterCountInStatusBar;
        ShowPageCountInStatusBar = settings.ShowPageCountInStatusBar;

        // Load keyboard shortcuts
        KeyboardShortcuts.Clear();
        var shortcutDisplayNames = GetShortcutDisplayNames();
        var defaults = ApplicationSettings.GetDefaultShortcuts();
        
        foreach (var kvp in defaults)
        {
            var shortcut = settings.KeyboardShortcuts.ContainsKey(kvp.Key) 
                ? settings.KeyboardShortcuts[kvp.Key] 
                : kvp.Value;
            
            var displayName = shortcutDisplayNames.ContainsKey(kvp.Key) 
                ? shortcutDisplayNames[kvp.Key] 
                : kvp.Key;
            
            KeyboardShortcuts.Add(new KeyboardShortcutViewModel(kvp.Key, displayName, shortcut));
        }
    }

    private Dictionary<string, string> GetShortcutDisplayNames()
    {
        return new Dictionary<string, string>
        {
            { "NewProject", "New Project" },
            { "OpenProject", "Open Project" },
            { "SaveProject", "Save Project" },
            { "SaveProjectAs", "Save Project As" },
            { "Preferences", "Preferences" },
            { "Exit", "Exit" },
            { "Undo", "Undo" },
            { "Redo", "Redo" },
            { "Cut", "Cut" },
            { "Copy", "Copy" },
            { "Paste", "Paste" },
            { "ToggleViewMode", "Toggle View Mode" },
            { "Rename", "Rename" },
            { "Find", "Find in Document" },
            { "Search", "Search All Documents" }
        };
    }

    public Dictionary<string, string> GetKeyboardShortcuts()
    {
        return KeyboardShortcuts.ToDictionary(ks => ks.ActionName, ks => ks.Shortcut);
    }

    [RelayCommand]
    private void Save()
    {
        var settings = new ApplicationSettings
        {
            AutoSave = AutoSave,
            AutoSaveIntervalMinutes = AutoSaveIntervalMinutes,
            BackupOnSave = BackupOnSave,
            MaxBackups = MaxBackups,
            DefaultFontFamily = DefaultFontFamily,
            DefaultFontSize = DefaultFontSize,
            WordWrap = WordWrap,
            ShowLineNumbers = ShowLineNumbers,
            ShowRuler = ShowRuler,
            Theme = Theme,
            ShowWordCountInStatusBar = ShowWordCountInStatusBar,
            ShowCharacterCountInStatusBar = ShowCharacterCountInStatusBar,
            ShowPageCountInStatusBar = ShowPageCountInStatusBar,
            KeyboardShortcuts = GetKeyboardShortcuts()
        };
        
        _settingsService.SaveSettings(settings);
        _onSave?.Invoke(this);
    }

    [RelayCommand]
    private void Cancel()
    {
        // Cancel is handled by closing the window
    }

    [RelayCommand]
    private void ResetShortcut(KeyboardShortcutViewModel? shortcut)
    {
        if (shortcut == null) return;
        
        var defaults = ApplicationSettings.GetDefaultShortcuts();
        if (defaults.ContainsKey(shortcut.ActionName))
        {
            shortcut.Shortcut = defaults[shortcut.ActionName];
        }
    }
}
