using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Scribo.Views;

public partial class ConfirmDeleteWindow : Window
{
    public bool Result { get; private set; } = false;

    public ConfirmDeleteWindow()
    {
        InitializeComponent();
    }

    public ConfirmDeleteWindow(string message) : this()
    {
        MessageTextBlock.Text = message;
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        Result = true;
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = false;
        Close();
    }
}
