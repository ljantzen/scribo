using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
        
        // Setup keyboard shortcuts
        KeyDown += OnWindowKeyDown;
        
        // Load and apply keyboard shortcuts from settings
        LoadKeyboardShortcuts();
        
        // Setup find/replace
        viewModel.SelectMatchRequested += OnSelectMatch;
        
        // Setup document link autocomplete
        SetupDocumentLinkAutocomplete();
    }

    private void SetupDocumentLinkAutocomplete()
    {
        var textBox = this.FindControl<TextBox>("sourceTextBox");
        var popup = this.FindControl<Popup>("autocompletePopup");
        var autocompleteControl = popup?.Child as DocumentLinkAutocompletePopup;
        
        if (textBox != null && popup != null && autocompleteControl != null)
        {
            // Handle keyboard events for autocomplete navigation
            textBox.KeyDown += OnEditorKeyDown;
            
            // Attach TextChanged handler (in addition to XAML binding to ensure it's called)
            textBox.TextChanged += OnEditorTextChanged;
            
            // Handle item selection from popup
            autocompleteControl.InsertDocumentRequested += OnAutocompleteItemSelected;
            
            // Handle popup visibility changes
            if (DataContext is MainWindowViewModel vm)
            {
                vm.DocumentLinkAutocompleteViewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(DocumentLinkAutocompleteViewModel.IsVisible))
                    {
                        if (vm.DocumentLinkAutocompleteViewModel.IsVisible)
                        {
                            UpdateAutocompletePopupPosition();
                        }
                    }
                };
            }
            
            // Close popup when clicking outside
            PointerPressed += (s, e) =>
            {
                if (popup.IsOpen && autocompleteControl != null)
                {
                    var hitTest = e.Source as Control;
                    if (hitTest != null && !autocompleteControl.IsPointerOver && hitTest != textBox)
                    {
                        if (DataContext is MainWindowViewModel viewModel)
                        {
                            viewModel.DocumentLinkAutocompleteViewModel.Hide();
                        }
                    }
                }
            };
        }
    }

    private void OnAutocompleteItemSelected(Document document)
    {
        var textBox = this.FindControl<TextBox>("sourceTextBox");
        if (textBox != null && DataContext is MainWindowViewModel vm)
        {
            InsertSelectedDocumentLink(textBox, vm, document);
        }
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || DataContext is not MainWindowViewModel vm)
            return;

        // Only show autocomplete in source mode
        if (!vm.IsSourceMode)
        {
            vm.DocumentLinkAutocompleteViewModel.Hide();
            return;
        }

        // Use TextBox.Text directly as it's already updated when this event fires
        var text = textBox.Text ?? string.Empty;
        var caretIndex = textBox.CaretIndex;
        
        // Ensure we have a valid caret position
        if (caretIndex < 0 || caretIndex > text.Length)
        {
            vm.DocumentLinkAutocompleteViewModel.Hide();
            return;
        }
        
        // Look backwards from caret to find [[
        // Search from the beginning or up to 200 chars back
        var searchStart = Math.Max(0, caretIndex - 200);
        var searchLength = Math.Min(caretIndex - searchStart, text.Length - searchStart);
        
        if (searchLength <= 0)
        {
            vm.DocumentLinkAutocompleteViewModel.Hide();
            return;
        }
        
        var textBeforeCaret = text.Substring(searchStart, searchLength);
        
        // Find the last [[ before the caret
        var lastBracketIndex = textBeforeCaret.LastIndexOf("[[");
        
        if (lastBracketIndex >= 0)
        {
            var absoluteBracketIndex = searchStart + lastBracketIndex;
            
            // Check if we're still inside a [[...]] block (not closed yet)
            // Look for ]] after the [[
            var textAfterBracket = text.Substring(absoluteBracketIndex + 2);
            var closingBracketIndex = textAfterBracket.IndexOf("]]");
            
            // Calculate caret position relative to the [[
            // caretIndex is the position after the last typed character
            // If we just typed [[, caretIndex would be 2 (after both brackets)
            var caretOffsetFromBracketStart = caretIndex - absoluteBracketIndex;
            
            // Only show autocomplete if:
            // 1. No closing ]] found anywhere after [[, OR
            // 2. Closing ]] exists but is after the caret position
            if (closingBracketIndex < 0 || closingBracketIndex >= caretOffsetFromBracketStart - 2)
            {
                // Extract query text between [[ and caret
                var queryStart = absoluteBracketIndex + 2; // After [[
                var queryLength = Math.Max(0, caretIndex - queryStart);
                
                if (queryStart <= text.Length)
                {
                    var actualQueryLength = Math.Min(queryLength, text.Length - queryStart);
                    var query = actualQueryLength > 0 ? text.Substring(queryStart, actualQueryLength) : string.Empty;
                    
                    // Don't show autocomplete if query contains ]]
                    if (!query.Contains("]]"))
                    {
                        // Update autocomplete with query (this will show popup if there are suggestions)
                        vm.DocumentLinkAutocompleteViewModel.UpdateQuery(query);
                        
                        // Update popup position after a brief delay to ensure UI is updated
                        Dispatcher.UIThread.Post(() =>
                        {
                            UpdateAutocompletePopupPosition();
                        }, DispatcherPriority.Loaded);
                        return;
                    }
                }
            }
        }
        
        // Hide autocomplete if we're not inside [[...]] or if we've closed it
        vm.DocumentLinkAutocompleteViewModel.Hide();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || DataContext is not MainWindowViewModel vm)
            return;

        // Only handle autocomplete in source mode
        if (!vm.IsSourceMode)
            return;

        var autocompleteVm = vm.DocumentLinkAutocompleteViewModel;
        
        // Only handle autocomplete keys if popup is visible
        if (!autocompleteVm.IsVisible)
            return;

        switch (e.Key)
        {
            case Key.Down:
                e.Handled = true;
                autocompleteVm.SelectNext();
                break;
                
            case Key.Up:
                e.Handled = true;
                autocompleteVm.SelectPrevious();
                break;
                
            case Key.Enter:
            case Key.Tab:
                e.Handled = true;
                InsertSelectedDocumentLink(textBox, vm);
                break;
                
            case Key.Escape:
                e.Handled = true;
                autocompleteVm.Hide();
                break;
        }
    }

    private void InsertSelectedDocumentLink(TextBox textBox, MainWindowViewModel vm, Document? document = null)
    {
        var autocompleteVm = vm.DocumentLinkAutocompleteViewModel;
        var selectedDoc = document ?? autocompleteVm.GetSelectedDocument();
        
        if (selectedDoc == null && autocompleteVm.Suggestions.Count > 0)
        {
            // If nothing selected, use first suggestion
            selectedDoc = autocompleteVm.Suggestions[0];
        }
        
        if (selectedDoc == null)
            return;

        var text = textBox.Text ?? string.Empty;
        var caretIndex = textBox.CaretIndex;
        
        // Find the [[ that started this link
        var searchStart = Math.Max(0, caretIndex - 100);
        var textBeforeCaret = text.Substring(searchStart, caretIndex - searchStart);
        var lastBracketIndex = textBeforeCaret.LastIndexOf("[[");
        
        if (lastBracketIndex < 0)
        {
            autocompleteVm.Hide();
            return;
        }
        
        var absoluteBracketIndex = searchStart + lastBracketIndex;
        var linkStart = absoluteBracketIndex + 2; // After [[
        
        // Replace text from [[ to caret with [[Document Title]]
        var beforeLink = text.Substring(0, absoluteBracketIndex);
        var afterCaret = text.Substring(caretIndex);
        var newText = beforeLink + "[[" + selectedDoc.Title + "]]" + afterCaret;
        
        // Update text
        textBox.Text = newText;
        
        // Position caret after the inserted link
        var newCaretIndex = absoluteBracketIndex + 2 + selectedDoc.Title.Length + 2; // [[ + title + ]]
        textBox.CaretIndex = newCaretIndex;
        
        // Hide autocomplete
        autocompleteVm.Hide();
        
        // Update view model
        vm.EditorText = newText;
    }

    private void UpdateAutocompletePopupPosition()
    {
        var textBox = this.FindControl<TextBox>("sourceTextBox");
        var popup = this.FindControl<Popup>("autocompletePopup");
        
        if (textBox == null || popup == null)
            return;

        // Get caret position
        var caretIndex = textBox.CaretIndex;
        var text = textBox.Text ?? string.Empty;
        
        // Find the [[ that started this link
        var searchStart = Math.Max(0, caretIndex - 100);
        var textBeforeCaret = text.Substring(searchStart, caretIndex - searchStart);
        var lastBracketIndex = textBeforeCaret.LastIndexOf("[[");
        
        if (lastBracketIndex < 0)
            return;
        
        var absoluteBracketIndex = searchStart + lastBracketIndex;
        
        // Calculate line and column for the [[ position
        var textToBracket = text.Substring(0, absoluteBracketIndex);
        var lines = textToBracket.Split('\n');
        var lineNumber = lines.Length - 1;
        var columnNumber = lines.LastOrDefault()?.Length ?? 0;
        
        // Approximate positioning based on line number and column
        // Note: This is approximate. For exact positioning, you'd need to measure text rendering
        var approximateLineHeight = 20; // Approximate line height in pixels
        var approximateCharWidth = 8; // Approximate character width in pixels
        
        // Position popup below the line where [[ appears
        // Using Bottom placement mode, so we adjust horizontal offset to align with [[
        popup.HorizontalOffset = 10 + (columnNumber * approximateCharWidth); // Align with [[ position
        popup.VerticalOffset = 5; // Small offset below current line
        
        // Ensure popup is visible
        popup.IsOpen = true;
    }

    private bool _isSelectingMatch = false;

    private void OnSelectMatch(int index, int length)
    {
        var textBox = this.FindControl<TextBox>("sourceTextBox");
        var findBar = this.FindControl<FindReplaceBar>("findReplaceBar");
        var findTextBox = findBar?.FindControl<TextBox>("findTextBox");
        
        if (textBox != null && index >= 0 && length > 0)
        {
            var textLength = textBox.Text?.Length ?? 0;
            if (index + length <= textLength)
            {
                var findBarVisible = findBar != null && findBar.IsVisible;
                
                if (findBarVisible)
                {
                    _isSelectingMatch = true;
                }
                
                // Set selection
                textBox.SelectionStart = index;
                textBox.SelectionEnd = index + length;
                
                // Scroll to selection using CaretIndex (this doesn't require focus)
                textBox.CaretIndex = index;
                
                // If find bar is visible, restore focus to find TextBox
                // Otherwise, focus the editor (for manual navigation)
                if (findBarVisible && findTextBox != null)
                {
                    // Restore focus to find TextBox after selection is set
                    Dispatcher.UIThread.Post(() =>
                    {
                        findTextBox.Focus();
                        _isSelectingMatch = false;
                    }, DispatcherPriority.Input);
                }
                else if (!findBarVisible)
                {
                    // Only focus editor if find bar is not visible
                    textBox.Focus();
                    _isSelectingMatch = false;
                }
            }
        }
    }

    private void OnMarkdownBlockContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        // Find MarkdownBlockControl in the visual tree
        var control = e.Container.GetVisualDescendants().OfType<MarkdownBlockControl>().FirstOrDefault();
        if (control != null)
        {
            control.NavigateToDocumentRequested += OnNavigateToDocument;
        }
    }

    private void OnNavigateToDocument(string documentId)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.NavigateToDocument(documentId);
        }
    }

    private void OnEditorTextBoxGotFocus(object? sender, Avalonia.Input.GotFocusEventArgs e)
    {
        // If we're selecting a match and find bar is visible, prevent editor from getting focus
        if (_isSelectingMatch)
        {
            var findBar = this.FindControl<FindReplaceBar>("findReplaceBar");
            var findTextBox = findBar?.FindControl<TextBox>("findTextBox");
            
            if (findBar != null && findBar.IsVisible && findTextBox != null)
            {
                // Prevent editor from stealing focus
                Dispatcher.UIThread.Post(() =>
                {
                    findTextBox.Focus();
                }, DispatcherPriority.Input);
            }
        }
    }
    
    public void ReloadKeyboardShortcuts()
    {
        LoadKeyboardShortcuts();
    }
    
    private void LoadKeyboardShortcuts()
    {
        var settingsService = new ApplicationSettingsService();
        var settings = settingsService.LoadSettings();
        
        // Apply shortcuts to menu items
        ApplyShortcut("NewProject", settings.KeyboardShortcuts, newProjectMenuItem);
        ApplyShortcut("OpenProject", settings.KeyboardShortcuts, openProjectMenuItem);
        ApplyShortcut("SaveProject", settings.KeyboardShortcuts, saveProjectMenuItem);
        ApplyShortcut("SaveProjectAs", settings.KeyboardShortcuts, saveProjectAsMenuItem);
        ApplyShortcut("Preferences", settings.KeyboardShortcuts, preferencesMenuItem);
        ApplyShortcut("Exit", settings.KeyboardShortcuts, exitMenuItem);
        ApplyShortcut("Undo", settings.KeyboardShortcuts, undoMenuItem);
        ApplyShortcut("Redo", settings.KeyboardShortcuts, redoMenuItem);
        ApplyShortcut("Find", settings.KeyboardShortcuts, findMenuItem);
        ApplyShortcut("Cut", settings.KeyboardShortcuts, cutMenuItem);
        ApplyShortcut("Copy", settings.KeyboardShortcuts, copyMenuItem);
        ApplyShortcut("Paste", settings.KeyboardShortcuts, pasteMenuItem);
        ApplyShortcut("ToggleViewMode", settings.KeyboardShortcuts, toggleViewModeMenuItem);
        ApplyShortcut("Search", settings.KeyboardShortcuts, searchMenuItem);
    }
    
    private void ApplyShortcut(string actionName, Dictionary<string, string> shortcuts, MenuItem? menuItem)
    {
        if (menuItem == null) return;
        
        if (shortcuts.ContainsKey(actionName))
        {
            try
            {
                menuItem.HotKey = KeyGesture.Parse(shortcuts[actionName]);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing shortcut '{shortcuts[actionName]}' for {actionName}: {ex.Message}");
            }
        }
    }
    
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var settingsService = new ApplicationSettingsService();
            var settings = settingsService.LoadSettings();
            
            // Handle local find shortcut (Ctrl+F)
            if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                e.Handled = true;
                vm.ShowLocalFindCommand.Execute(null);
                return;
            }
            
            // Handle search shortcut (global) - Ctrl+Shift+F
            string searchShortcut = settings.KeyboardShortcuts.ContainsKey("Search") 
                ? settings.KeyboardShortcuts["Search"] 
                : "Ctrl+Shift+F";
            
            try
            {
                var searchGesture = KeyGesture.Parse(searchShortcut);
                if (searchGesture.Matches(e))
                {
                    e.Handled = true;
                    vm.ShowSearchCommand.Execute(null);
                    return;
                }
            }
            catch
            {
                // Fallback to Ctrl+Shift+F if parsing fails
                if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    e.Handled = true;
                    vm.ShowSearchCommand.Execute(null);
                    return;
                }
            }
            
            // Handle F2 for rename (configurable shortcut)
            string renameShortcut = settings.KeyboardShortcuts.ContainsKey("Rename") 
                ? settings.KeyboardShortcuts["Rename"] 
                : "F2";
            
            // Parse and check if this matches the rename shortcut
            try
            {
                var gesture = KeyGesture.Parse(renameShortcut);
                if (gesture.Matches(e))
                {
                    if (vm.SelectedProjectItem != null && 
                        (vm.SelectedProjectItem.IsChapter || vm.SelectedProjectItem.Document != null || vm.SelectedProjectItem.IsSubfolder))
                    {
                        e.Handled = true;
                        vm.RenameChapterCommand.Execute(vm.SelectedProjectItem);
                        
                        // Focus the rename TextBox after a brief delay
                        Dispatcher.UIThread.Post(() =>
                        {
                            FocusRenameTextBox();
                        }, DispatcherPriority.Loaded);
                    }
                }
            }
            catch
            {
                // Fallback to F2 if parsing fails
                if (e.Key == Key.F2)
                {
                    if (vm.SelectedProjectItem != null && 
                        (vm.SelectedProjectItem.IsChapter || vm.SelectedProjectItem.Document != null || vm.SelectedProjectItem.IsSubfolder))
                    {
                        e.Handled = true;
                        vm.RenameChapterCommand.Execute(vm.SelectedProjectItem);
                        
                        Dispatcher.UIThread.Post(() =>
                        {
                            FocusRenameTextBox();
                        }, DispatcherPriority.Loaded);
                    }
                }
            }
        }
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

    private void OnRenameClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ProjectTreeItemViewModel item && DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            
            // Close the context menu
            var contextMenu = menuItem.Parent as ContextMenu;
            contextMenu?.Close();
            
            vm.RenameItemCommand.Execute(item);
            
            // Focus the rename TextBox after a brief delay
            Dispatcher.UIThread.Post(() =>
            {
                FocusRenameTextBox();
            }, DispatcherPriority.Loaded);
        }
    }

    private void OnDeleteClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ProjectTreeItemViewModel item && DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            
            // Close the context menu
            var contextMenu = menuItem.Parent as ContextMenu;
            contextMenu?.Close();
            
            vm.DeleteItemCommand.Execute(item);
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
