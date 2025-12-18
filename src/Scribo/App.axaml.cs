using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Scribo.Services;
using Scribo.Views;

namespace Scribo;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Load and apply theme from settings
        ApplyThemeFromSettings();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void ApplyTheme(string theme)
    {
        RequestedThemeVariant = theme switch
        {
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            _ => ThemeVariant.Light
        };
    }

    private void ApplyThemeFromSettings()
    {
        try
        {
            var settingsService = new ApplicationSettingsService();
            var settings = settingsService.LoadSettings();
            ApplyTheme(settings.Theme);
        }
        catch
        {
            // If settings can't be loaded, use default theme
            ApplyTheme("Light");
        }
    }
}
