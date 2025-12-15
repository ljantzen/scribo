using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Scribo.ViewModels;

public partial class PreferencesViewModel : ViewModelBase
{
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

    private readonly Action<PreferencesViewModel>? _onSave;

    public PreferencesViewModel(Action<PreferencesViewModel>? onSave = null)
    {
        _onSave = onSave;
        LoadPreferences();
    }

    private void LoadPreferences()
    {
        // TODO: Load from application settings file
        // For now, use defaults
    }

    [RelayCommand]
    private void Save()
    {
        _onSave?.Invoke(this);
    }

    [RelayCommand]
    private void Cancel()
    {
        // Cancel is handled by closing the window
    }
}
