using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia;
using Scribo.Models;
using Scribo.Services;
using Scribo.ViewModels;

namespace Scribo.Views;

public partial class MainWindow : Window
{
    private readonly PluginManager _pluginManager;
    private readonly PluginContext _pluginContext;

    public MainWindow()
    {
        InitializeComponent();
        
        // Initialize plugin system
        _pluginManager = new PluginManager();
        _pluginContext = new PluginContext();
        _pluginContext.SetMainWindow(this);
        _pluginManager.SetContext(_pluginContext);
        
        // Load plugins
        _pluginManager.LoadPlugins();
        
        var viewModel = new MainWindowViewModel(_pluginManager);
        viewModel.SetParentWindow(this);
        DataContext = viewModel;
        
        // Setup drag and drop handlers
        AddHandler(DragDrop.DropEvent, OnTreeViewDrop);
        AddHandler(DragDrop.DragOverEvent, OnTreeViewDragOver);
    }

    private void OnProjectPropertiesClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ShowProjectPropertiesCommand.Execute(null);
        }
    }

    private void OnAddChapterClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ProjectTreeItemViewModel item && DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            
            // Close the context menu
            var contextMenu = menuItem.Parent as ContextMenu;
            contextMenu?.Close();
            
            vm.AddChapterCommand.Execute(item);
            
            // Focus the rename TextBox after a brief delay
            Dispatcher.UIThread.Post(() =>
            {
                FocusRenameTextBox();
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnRenameChapterClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ProjectTreeItemViewModel item && DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            
            // Close the context menu
            var contextMenu = menuItem.Parent as ContextMenu;
            contextMenu?.Close();
            
            vm.RenameChapterCommand.Execute(item);
            
            // Focus the rename TextBox after a brief delay
            Dispatcher.UIThread.Post(() =>
            {
                FocusRenameTextBox();
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnAddSceneClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ProjectTreeItemViewModel item && DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            
            // Close the context menu
            var contextMenu = menuItem.Parent as ContextMenu;
            contextMenu?.Close();
            
            vm.AddSceneCommand.Execute(item);
            
            // Focus the rename TextBox after a brief delay
            Dispatcher.UIThread.Post(() =>
            {
                FocusRenameTextBox();
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnAddCharacterClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ProjectTreeItemViewModel item && DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            
            // Close the context menu
            var contextMenu = menuItem.Parent as ContextMenu;
            contextMenu?.Close();
            
            vm.AddCharacterCommand.Execute(item);
            
            // Focus the rename TextBox after a brief delay
            Dispatcher.UIThread.Post(() =>
            {
                FocusRenameTextBox();
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnAddLocationClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ProjectTreeItemViewModel item && DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            
            // Close the context menu
            var contextMenu = menuItem.Parent as ContextMenu;
            contextMenu?.Close();
            
            vm.AddLocationCommand.Execute(item);
            
            // Focus the rename TextBox after a brief delay
            Dispatcher.UIThread.Post(() =>
            {
                FocusRenameTextBox();
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnAddNoteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ProjectTreeItemViewModel item && DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            
            // Close the context menu
            var contextMenu = menuItem.Parent as ContextMenu;
            contextMenu?.Close();
            
            vm.AddNoteCommand.Execute(item);
            
            // Focus the rename TextBox after a brief delay
            Dispatcher.UIThread.Post(() =>
            {
                FocusRenameTextBox();
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnCreateSubfolderClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ProjectTreeItemViewModel item && DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            
            // Close the context menu
            var contextMenu = menuItem.Parent as ContextMenu;
            contextMenu?.Close();
            
            vm.CreateSubfolderCommand.Execute(item);
            
            // Focus the rename TextBox after a brief delay
            Dispatcher.UIThread.Post(() =>
            {
                FocusRenameTextBox();
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnRenameLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is ProjectTreeItemViewModel item && DataContext is MainWindowViewModel vm)
        {
            vm.CommitRename(item);
        }
    }

    private void OnRenameKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is ProjectTreeItemViewModel item && DataContext is MainWindowViewModel vm)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                vm.CommitRename(item);
                textBox.Focusable = false;
                textBox.Focusable = true;
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                vm.CancelRename(item);
            }
        }
    }

    private void FocusRenameTextBox()
    {
        // Find the TextBox that is currently visible (IsRenaming = true)
        // Try both TreeViews - left and right
        var treeViewLeft = this.FindControl<TreeView>("projectTreeViewLeft");
        var treeViewRight = this.FindControl<TreeView>("projectTreeViewRight");
        
        TreeView? treeView = null;
        if (treeViewLeft?.IsVisible == true)
        {
            treeView = treeViewLeft;
        }
        else if (treeViewRight?.IsVisible == true)
        {
            treeView = treeViewRight;
        }
        
        if (treeView == null)
        {
            // Fallback: Try to find any TreeView in the window
            treeView = this.GetVisualDescendants().OfType<TreeView>().FirstOrDefault();
        }

        if (treeView != null)
        {
            var textBox = treeView.GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(tb => 
                {
                    if (!tb.IsVisible) return false;
                    if (tb.DataContext is ProjectTreeItemViewModel item)
                    {
                        return item.IsRenaming;
                    }
                    return false;
                });
            
            if (textBox != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    textBox.Focus();
                    textBox.SelectAll();
                }, DispatcherPriority.Input);
            }
        }
    }

    private void OnRecentProjectClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string filePath && DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            
            // Close all menus immediately
            CloseAllMenus();
            
            // Execute the command after a brief delay to ensure menu closes
            Dispatcher.UIThread.Post(() =>
            {
                vm.OpenRecentProjectCommand.Execute(filePath);
            }, DispatcherPriority.Loaded);
        }
    }

    private void CloseAllMenus()
    {
        var menu = this.FindControl<Menu>("mainMenu");
        if (menu != null)
        {
            // Close all top-level menu items
            foreach (var item in menu.Items)
            {
                if (item is MenuItem topLevelMenuItem)
                {
                    topLevelMenuItem.IsSubMenuOpen = false;
                }
            }
        }
    }

    private void OnMoveUpClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ProjectTreeItemViewModel item && DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            vm.MoveDocumentUpCommand.Execute(item);
        }
    }

    private void OnMoveDownClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ProjectTreeItemViewModel item && DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            vm.MoveDocumentDownCommand.Execute(item);
        }
    }

    private ProjectTreeItemViewModel? _draggedItem;
    private Point? _dragStartPoint;
    private bool _isDragging = false;
    private const double DragThreshold = 5.0;

    private void OnTreeItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border border && border.DataContext is ProjectTreeItemViewModel item && item.Document != null)
        {
            // Only allow dragging documents (not folders or root)
            // Only start drag on left mouse button
            if (!item.IsFolder && !item.IsRoot && e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            {
                // Store the item and start point, but don't handle the event yet
                // This allows normal click selection to work
                _draggedItem = item;
                _dragStartPoint = e.GetPosition(border);
                _isDragging = false;
                // Don't capture pointer or handle event yet - wait for movement
            }
        }
    }

    private void OnTreeItemPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedItem != null && _dragStartPoint.HasValue && sender is Border border)
        {
            var currentPoint = e.GetPosition(border);
            var delta = currentPoint - _dragStartPoint.Value;
            
            // Only start drag if moved beyond threshold and we haven't started dragging yet
            if (!_isDragging && (Math.Abs(delta.X) > DragThreshold || Math.Abs(delta.Y) > DragThreshold))
            {
                _isDragging = true;
                e.Pointer.Capture(border);
                e.Handled = true;
                
                var dragData = new DataObject();
                dragData.Set("TreeViewItem", _draggedItem);
                
                var result = DragDrop.DoDragDrop(e, dragData, DragDropEffects.Move);
                
                // Reset drag state
                _draggedItem = null;
                _dragStartPoint = null;
                _isDragging = false;
                e.Pointer.Capture(null);
            }
        }
    }

    private void OnTreeItemPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        // If we were dragging, reset state
        if (_isDragging)
        {
            _draggedItem = null;
            _dragStartPoint = null;
            _isDragging = false;
            if (sender is Border border)
            {
                e.Pointer.Capture(null);
            }
        }
        // If we weren't dragging (just a click), clear the drag state
        else if (_draggedItem != null)
        {
            _draggedItem = null;
            _dragStartPoint = null;
        }
    }

    private void OnTreeViewDragOver(object? sender, DragEventArgs e)
    {
        if (e.Source is Control control)
        {
            // Find the Border that contains the data context by traversing visual tree
            Border? border = null;
            var current = control.GetVisualParent();
            while (current != null)
            {
                if (current is Border b && b.DataContext is ProjectTreeItemViewModel)
                {
                    border = b;
                    break;
                }
                current = current.GetVisualParent();
            }
            
            if (border?.DataContext is ProjectTreeItemViewModel targetItem)
            {
                if (e.Data.Contains("TreeViewItem"))
                {
                    var draggedItem = e.Data.Get("TreeViewItem") as ProjectTreeItemViewModel;
                    if (draggedItem != null && draggedItem.Document != null)
                    {
                        // Allow dropping scenes on chapters (to move scene to different chapter)
                        if (draggedItem.IsScene && targetItem.IsChapter)
                        {
                            e.DragEffects = DragDropEffects.Move;
                            return;
                        }
                        
                        // Allow dropping characters on Characters folder or its subfolders
                        if (draggedItem.Document.Type == DocumentType.Character && 
                            (targetItem.IsCharactersFolder || 
                             (targetItem.IsSubfolder && targetItem.FolderDocumentType == DocumentType.Character)))
                        {
                            e.DragEffects = DragDropEffects.Move;
                            return;
                        }
                        
                        // Allow dropping locations on Locations folder or its subfolders
                        if (draggedItem.Document.Type == DocumentType.Location && 
                            (targetItem.IsLocationsFolder || 
                             (targetItem.IsSubfolder && targetItem.FolderDocumentType == DocumentType.Location)))
                        {
                            e.DragEffects = DragDropEffects.Move;
                            return;
                        }
                        
                        // Allow dropping research on Research folder or its subfolders
                        if (draggedItem.Document.Type == DocumentType.Research && 
                            (targetItem.IsResearchFolder || 
                             (targetItem.IsSubfolder && targetItem.FolderDocumentType == DocumentType.Research)))
                        {
                            e.DragEffects = DragDropEffects.Move;
                            return;
                        }
                        
                        // Allow dropping notes on Notes/Research folder or their subfolders
                        if (draggedItem.Document.Type == DocumentType.Note && 
                            (targetItem.IsNotesFolder || targetItem.IsResearchFolder || 
                             (targetItem.IsSubfolder && (targetItem.FolderDocumentType == DocumentType.Note || targetItem.FolderDocumentType == DocumentType.Research))))
                        {
                            e.DragEffects = DragDropEffects.Move;
                            return;
                        }
                        
                        // Allow dropping on folders (for reordering within folder)
                        if (targetItem.IsFolder && !targetItem.IsRoot)
                        {
                            e.DragEffects = DragDropEffects.Move;
                            return;
                        }
                        
                        // Allow dropping on documents to reorder within same parent
                        if (targetItem.Document != null)
                        {
                            var targetParent = FindParentFolder(targetItem);
                            var draggedParent = FindParentFolder(draggedItem);
                            if (targetParent != null && targetParent == draggedParent)
                            {
                                e.DragEffects = DragDropEffects.Move;
                                return;
                            }
                        }
                    }
                }
                
                e.DragEffects = DragDropEffects.None;
            }
        }
    }

    private void OnTreeViewDrop(object? sender, DragEventArgs e)
    {
        if (e.Source is Control control && DataContext is MainWindowViewModel vm)
        {
            // Find the Border that contains the data context by traversing visual tree
            Border? border = null;
            var current = control.GetVisualParent();
            while (current != null)
            {
                if (current is Border b && b.DataContext is ProjectTreeItemViewModel)
                {
                    border = b;
                    break;
                }
                current = current.GetVisualParent();
            }
            
            if (border?.DataContext is ProjectTreeItemViewModel targetItem && e.Data.Contains("TreeViewItem"))
            {
                var draggedItem = e.Data.Get("TreeViewItem") as ProjectTreeItemViewModel;
                if (draggedItem != null && draggedItem.Document != null)
                {
                    // Handle moving scene to a different chapter
                    if (draggedItem.IsScene && targetItem.IsChapter)
                    {
                        vm.MoveSceneToChapter(draggedItem, targetItem);
                        return;
                    }
                    
                    // Handle moving characters to Characters folder or subfolder
                    if (draggedItem.Document.Type == DocumentType.Character && 
                        (targetItem.IsCharactersFolder || 
                         (targetItem.IsSubfolder && targetItem.FolderDocumentType == DocumentType.Character)))
                    {
                        vm.MoveDocumentToFolder(draggedItem, targetItem);
                        return;
                    }
                    
                    // Handle moving locations to Locations folder or subfolder
                    if (draggedItem.Document.Type == DocumentType.Location && 
                        (targetItem.IsLocationsFolder || 
                         (targetItem.IsSubfolder && targetItem.FolderDocumentType == DocumentType.Location)))
                    {
                        vm.MoveDocumentToFolder(draggedItem, targetItem);
                        return;
                    }
                    
                    // Handle moving research to Research folder or subfolder
                    if (draggedItem.Document.Type == DocumentType.Research && 
                        (targetItem.IsResearchFolder || 
                         (targetItem.IsSubfolder && targetItem.FolderDocumentType == DocumentType.Research)))
                    {
                        vm.MoveDocumentToFolder(draggedItem, targetItem);
                        return;
                    }
                    
                    // Handle moving notes to Notes/Research folder or subfolder
                    if (draggedItem.Document.Type == DocumentType.Note && 
                        (targetItem.IsNotesFolder || targetItem.IsResearchFolder || 
                         (targetItem.IsSubfolder && (targetItem.FolderDocumentType == DocumentType.Note || targetItem.FolderDocumentType == DocumentType.Research))))
                    {
                        vm.MoveDocumentToFolder(draggedItem, targetItem);
                        return;
                    }
                    
                    // Determine target parent and position
                    ProjectTreeItemViewModel? targetParent = null;
                    int targetIndex = -1;

                    if (targetItem.IsFolder && !targetItem.IsRoot)
                    {
                        // Dropped on a folder - add to end of that folder
                        targetParent = targetItem;
                        targetIndex = targetItem.Children.Count;
                    }
                    else if (targetItem.Document != null)
                    {
                        // Dropped on a document - reorder within same parent
                        targetParent = FindParentFolder(targetItem);
                        if (targetParent != null)
                        {
                            targetIndex = targetParent.Children.IndexOf(targetItem);
                        }
                    }

                    if (targetParent != null && targetIndex >= 0)
                    {
                        vm.ReorderDocumentToPosition(draggedItem, targetParent, targetIndex);
                    }
                }
            }
        }
    }


    private ProjectTreeItemViewModel? FindParentFolder(ProjectTreeItemViewModel item)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            foreach (var rootItem in vm.ProjectTreeItems)
            {
                var parent = FindParentRecursive(rootItem, item);
                if (parent != null)
                    return parent;
            }
        }
        return null;
    }

    private ProjectTreeItemViewModel? FindParentRecursive(ProjectTreeItemViewModel parent, ProjectTreeItemViewModel child)
    {
        if (parent.Children.Contains(child))
        {
            return parent;
        }

        foreach (var item in parent.Children)
        {
            var found = FindParentRecursive(item, child);
            if (found != null)
                return found;
        }

        return null;
    }
}
