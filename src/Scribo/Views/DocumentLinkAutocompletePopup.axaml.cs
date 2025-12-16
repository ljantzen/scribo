using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Scribo.Models;
using Scribo.ViewModels;

namespace Scribo.Views;

public partial class DocumentLinkAutocompletePopup : UserControl
{
    public DocumentLinkAutocompletePopup()
    {
        InitializeComponent();
        
        // Handle item selection
        var listBox = this.FindControl<ListBox>("autocompleteListBox");
        if (listBox != null)
        {
            listBox.DoubleTapped += OnItemDoubleTapped;
            listBox.KeyDown += OnListBoxKeyDown;
        }
    }

    private void OnItemDoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (DataContext is DocumentLinkAutocompleteViewModel vm)
        {
            var selectedDoc = vm.GetSelectedDocument();
            if (selectedDoc != null)
            {
                InsertDocumentRequested?.Invoke(selectedDoc);
            }
        }
    }

    private void OnListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is DocumentLinkAutocompleteViewModel vm)
        {
            var selectedDoc = vm.GetSelectedDocument();
            if (selectedDoc != null)
            {
                e.Handled = true;
                InsertDocumentRequested?.Invoke(selectedDoc);
            }
        }
    }

    public event Action<Document>? InsertDocumentRequested;
}
