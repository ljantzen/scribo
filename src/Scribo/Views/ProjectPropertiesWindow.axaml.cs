using Avalonia.Controls;
using Scribo.ViewModels;

namespace Scribo.Views;

public partial class ProjectPropertiesWindow : Window
{
    public ProjectPropertiesWindow()
    {
        InitializeComponent();
    }

    public ProjectPropertiesWindow(ProjectPropertiesViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnOkClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is ProjectPropertiesViewModel vm)
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
