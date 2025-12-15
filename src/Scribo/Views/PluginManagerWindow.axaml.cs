using Avalonia.Controls;
using Scribo.ViewModels;

namespace Scribo.Views;

public partial class PluginManagerWindow : Window
{
    public PluginManagerWindow()
    {
        InitializeComponent();
    }

    public PluginManagerWindow(PluginManagerViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnOkClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
