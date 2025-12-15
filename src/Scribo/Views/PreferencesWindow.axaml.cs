using Avalonia.Controls;
using Scribo.ViewModels;

namespace Scribo.Views;

public partial class PreferencesWindow : Window
{
    public PreferencesWindow()
    {
        InitializeComponent();
    }

    public PreferencesWindow(PreferencesViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnOkClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is PreferencesViewModel vm)
        {
            vm.SaveCommand.Execute(null);
            Close();
        }
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }
}
