using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Scribo.ViewModels;

namespace Scribo.Views.Handlers;

public class MarkdownBlockNavigationHandler
{
    private readonly MainWindow _window;

    public MarkdownBlockNavigationHandler(MainWindow window)
    {
        _window = window;
    }

    public void Setup()
    {
        var itemsControl = _window.FindControl<ItemsControl>("markdownItemsControl");
        if (itemsControl != null)
        {
            itemsControl.ContainerPrepared += OnMarkdownBlockContainerPrepared;

            // Also try to attach handlers to existing items
            Dispatcher.UIThread.Post(() =>
            {
                AttachHandlersToExistingMarkdownBlocks();
            }, DispatcherPriority.Loaded);
        }
    }

    private void AttachHandlersToExistingMarkdownBlocks()
    {
        var itemsControl = _window.FindControl<ItemsControl>("markdownItemsControl");
        if (itemsControl != null)
        {
            var controls = itemsControl.GetVisualDescendants().OfType<MarkdownBlockControl>().ToList();

            foreach (var control in controls)
            {
                control.NavigateToDocumentRequested += OnNavigateToDocument;
            }
        }
    }

    private void OnMarkdownBlockContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        // The container itself might be the MarkdownBlockControl, or it might contain it
        MarkdownBlockControl? control = null;

        // First check if the container itself is a MarkdownBlockControl
        if (e.Container is MarkdownBlockControl directControl)
        {
            control = directControl;
        }
        else
        {
            // Find MarkdownBlockControl in the visual tree
            control = e.Container.GetVisualDescendants().OfType<MarkdownBlockControl>().FirstOrDefault();
        }

        if (control != null)
        {
            control.NavigateToDocumentRequested += OnNavigateToDocument;
        }
    }

    public void OnNavigateToDocument(string documentId)
    {
        if (_window.DataContext is MainWindowViewModel vm)
        {
            vm.NavigateToDocument(documentId);
        }
    }
}
