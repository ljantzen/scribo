using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Scribo.Views;

public partial class ConfirmDeleteDialog : UserControl
{
    public bool Result { get; private set; } = false;

    public ConfirmDeleteDialog()
    {
        InitializeComponent();
    }

    public ConfirmDeleteDialog(string message) : this()
    {
        if (MessageTextBlock != null)
        {
            MessageTextBlock.Text = message;
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        Result = true;
        // Find parent window and close it
        var window = this.GetVisualRoot() as Window;
        window?.Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Result = false;
        // Find parent window and close it
        var window = this.GetVisualRoot() as Window;
        window?.Close(false);
    }
}
