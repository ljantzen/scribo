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
using Avalonia.Media;
using Avalonia.Layout;
using Scribo.Models;
using Scribo.Services;
using Scribo.ViewModels;

namespace Scribo.Views;

public partial class MainWindow : Window
{
    // Set to false to disable all debug tracing
    private const bool ENABLE_DEBUG_TRACING = true;
    
    private void DebugTrace(string message)
    {
        if (ENABLE_DEBUG_TRACING)
        {
            Console.WriteLine($"[DEBUG] {message}");
        }
    }
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
        
        // Setup markdown block navigation
        SetupMarkdownBlockNavigation();
    }
    
    private void SetupMarkdownBlockNavigation()
    {
        DebugTrace("SetupMarkdownBlockNavigation called");
        
        // Try to find the ItemsControl and attach to its events
        var itemsControl = this.FindControl<ItemsControl>("markdownItemsControl");
        if (itemsControl != null)
        {
            DebugTrace($"Found markdownItemsControl, attaching ContainerPrepared handler");
            itemsControl.ContainerPrepared += OnMarkdownBlockContainerPrepared;
            DebugTrace("ContainerPrepared handler attached");
            
            // Also try to attach handlers to existing items
            Dispatcher.UIThread.Post(() =>
            {
                DebugTrace("Checking for existing MarkdownBlockControls");
                AttachHandlersToExistingMarkdownBlocks();
            }, DispatcherPriority.Loaded);
        }
        else
        {
            DebugTrace("markdownItemsControl not found");
        }
    }
    
    private void AttachHandlersToExistingMarkdownBlocks()
    {
        var itemsControl = this.FindControl<ItemsControl>("markdownItemsControl");
        if (itemsControl != null)
        {
            DebugTrace($"Searching for MarkdownBlockControls in itemsControl");
            var controls = itemsControl.GetVisualDescendants().OfType<MarkdownBlockControl>().ToList();
            DebugTrace($"Found {controls.Count} MarkdownBlockControls");
            
            foreach (var control in controls)
            {
                DebugTrace($"  Attaching handler to MarkdownBlockControl: DataContext={control.DataContext?.GetType().Name}");
                control.NavigateToDocumentRequested += OnNavigateToDocument;
                DebugTrace($"  Handler attached");
            }
        }
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

        // Don't show autocomplete if TextBox doesn't have focus (programmatic changes)
        // Autocomplete should only appear when user is actively typing
        if (!textBox.IsFocused)
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
            
            // Calculate caret position relative to the [[
            // caretIndex is the position after the last typed character
            // If we just typed [[, caretIndex would be 2 (after both brackets)
            var caretOffsetFromBracketStart = caretIndex - absoluteBracketIndex;
            
            // Only show autocomplete if caret is immediately after [[ (user just typed it)
            // Don't show if caret is at position 0 or far from [[ (likely programmatic load)
            // Allow showing if caret is at [[ position + 2 or more (user typed [[ and possibly more)
            if (caretOffsetFromBracketStart >= 2)
            {
                // Check if we're still inside a [[...]] block (not closed yet)
                // Look for ]] after the [[
                var textAfterBracket = text.Substring(absoluteBracketIndex + 2);
                var closingBracketIndex = textAfterBracket.IndexOf("]]");
                
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
        DebugTrace($"OnMarkdownBlockContainerPrepared called");
        DebugTrace($"  Sender: {sender?.GetType().Name}");
        DebugTrace($"  Container: {e.Container?.GetType().Name}");
        DebugTrace($"  Container DataContext: {e.Container?.DataContext?.GetType().Name}");
        
        // The container itself might be the MarkdownBlockControl, or it might contain it
        MarkdownBlockControl? control = null;
        
        // First check if the container itself is a MarkdownBlockControl
        if (e.Container is MarkdownBlockControl directControl)
        {
            DebugTrace($"  Container is directly MarkdownBlockControl");
            control = directControl;
        }
        else
        {
            DebugTrace($"  Searching for MarkdownBlockControl in visual descendants");
            // Find MarkdownBlockControl in the visual tree
            control = e.Container.GetVisualDescendants().OfType<MarkdownBlockControl>().FirstOrDefault();
        }
        
        DebugTrace($"  MarkdownBlockControl found: {control != null}");
        
        if (control != null)
        {
            DebugTrace($"  Control DataContext: {control.DataContext?.GetType().Name}");
            DebugTrace($"  Attaching NavigateToDocumentRequested handler");
            control.NavigateToDocumentRequested += OnNavigateToDocument;
            DebugTrace($"  Handler attached");
        }
        else
        {
            DebugTrace($"  MarkdownBlockControl not found in visual descendants");
            var allDescendants = e.Container.GetVisualDescendants().ToList();
            DebugTrace($"  Total visual descendants: {allDescendants.Count}");
            for (int i = 0; i < Math.Min(20, allDescendants.Count); i++)
            {
                DebugTrace($"    Descendant[{i}]: {allDescendants[i].GetType().Name}, DataContext: {allDescendants[i].DataContext?.GetType().Name}");
            }
            
            // Also check if container has children
            if (e.Container is Control containerControl)
            {
                DebugTrace($"  Container has {containerControl.GetVisualChildren().Count()} visual children");
                var children = containerControl.GetVisualChildren().ToList();
                for (int i = 0; i < Math.Min(10, children.Count); i++)
                {
                    DebugTrace($"    Child[{i}]: {children[i].GetType().Name}");
                }
            }
        }
    }

    private void OnNavigateToDocument(string documentId)
    {
        DebugTrace($"OnNavigateToDocument called with documentId: '{documentId}'");
        
        if (DataContext is MainWindowViewModel vm)
        {
            DebugTrace($"  DataContext is MainWindowViewModel, calling NavigateToDocument");
            vm.NavigateToDocument(documentId);
            DebugTrace($"  NavigateToDocument called");
        }
        else
        {
            DebugTrace($"  DataContext is not MainWindowViewModel: {DataContext?.GetType().Name}");
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
        
        // Store original header texts before modifying
        StoreOriginalHeaders();
        
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
    
    private Dictionary<MenuItem, string> _originalHeaders = new();
    
    private void StoreOriginalHeaders()
    {
        // Store original header texts if not already stored
        var menuItems = new[]
        {
            (newProjectMenuItem, "_New Project"),
            (openProjectMenuItem, "_Open Project"),
            (saveProjectMenuItem, "_Save Project"),
            (saveProjectAsMenuItem, "Save Project _As"),
            (preferencesMenuItem, "_Preferences"),
            (exitMenuItem, "E_xit"),
            (undoMenuItem, "_Undo"),
            (redoMenuItem, "_Redo"),
            (findMenuItem, "_Find"),
            (cutMenuItem, "_Cut"),
            (copyMenuItem, "_Copy"),
            (pasteMenuItem, "_Paste"),
            (toggleViewModeMenuItem, "_Toggle View Mode"),
            (searchMenuItem, "_Search")
        };
        
        foreach (var (menuItem, defaultHeader) in menuItems)
        {
            if (menuItem != null && !_originalHeaders.ContainsKey(menuItem))
            {
                // Get current header or use default
                string header = menuItem.Header?.ToString() ?? defaultHeader;
                _originalHeaders[menuItem] = header;
            }
        }
    }
    
    private string GetOriginalHeader(MenuItem menuItem)
    {
        if (_originalHeaders.TryGetValue(menuItem, out var header))
        {
            return header;
        }
        
        // Fallback: try to extract from current header
        if (menuItem.Header is Grid headerGrid)
        {
            var textBlock = headerGrid.Children.OfType<TextBlock>().FirstOrDefault();
            if (textBlock != null)
            {
                return textBlock.Text ?? string.Empty;
            }
        }
        
        return menuItem.Header?.ToString() ?? string.Empty;
    }
    
    private void ApplyShortcut(string actionName, Dictionary<string, string> shortcuts, MenuItem? menuItem)
    {
        if (menuItem == null) return;
        
        string? shortcutString = null;
        if (shortcuts.ContainsKey(actionName))
        {
            shortcutString = shortcuts[actionName];
            try
            {
                menuItem.HotKey = KeyGesture.Parse(shortcutString);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing shortcut '{shortcutString}' for {actionName}: {ex.Message}");
                shortcutString = null;
            }
        }
        
        // Update the header to include the hotkey label
        UpdateMenuItemHeader(menuItem, shortcutString);
    }
    
    private void UpdateMenuItemHeader(MenuItem menuItem, string? shortcutString)
    {
        // Get the original header text
        string headerText = GetOriginalHeader(menuItem);
        
        // Remove underscores for display (they're access key markers, not meant to be shown)
        string displayText = headerText.Replace("_", "");
        
        // Create a Grid with the menu text on the left and hotkey on the right
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        
        var menuTextBlock = new TextBlock
        {
            Text = displayText,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        Grid.SetColumn(menuTextBlock, 0);
        grid.Children.Add(menuTextBlock);
        
        if (!string.IsNullOrEmpty(shortcutString))
        {
            var hotkeyTextBlock = new TextBlock
            {
                Text = FormatHotkeyForDisplay(shortcutString),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(20, 0, 0, 0),
                Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)) // Gray color for hotkey
            };
            Grid.SetColumn(hotkeyTextBlock, 1);
            grid.Children.Add(hotkeyTextBlock);
        }
        
        menuItem.Header = grid;
    }
    
    private string FormatHotkeyForDisplay(string shortcut)
    {
        // Format the shortcut string for display
        // Replace common patterns for better readability
        return shortcut
            .Replace("Ctrl+", "Ctrl+")
            .Replace("Shift+", "Shift+")
            .Replace("Alt+", "Alt+")
            .Replace("Meta+", "Meta+");
    }
    
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        DebugTrace($"OnWindowKeyDown: Key={e.Key}, Handled={e.Handled}, KeyModifiers={e.KeyModifiers}");
        
        // Handle Enter key when Recent Projects submenu is open (check this FIRST)
        if (e.Key == Key.Enter && !e.Handled && DataContext is MainWindowViewModel viewModel)
        {
            DebugTrace("Enter key pressed, checking Recent Projects menu");
            var recentProjectsMenuItem = this.FindControl<MenuItem>("recentProjectsMenuItem");
            DebugTrace($"recentProjectsMenuItem found: {recentProjectsMenuItem != null}");
            
            if (recentProjectsMenuItem != null)
            {
                DebugTrace($"IsSubMenuOpen: {recentProjectsMenuItem.IsSubMenuOpen}");
                DebugTrace($"RecentProjects count: {viewModel.RecentProjects.Count}");
                
                if (viewModel.RecentProjects.Count > 0)
                {
                    DebugTrace($"First RecentProject: {viewModel.RecentProjects[0].ProjectName}, FilePath: {viewModel.RecentProjects[0].FilePath}");
                }
            }
            
            if (recentProjectsMenuItem != null && recentProjectsMenuItem.IsSubMenuOpen)
            {
                MenuItem? selectedItem = null;
                string? filePath = null;
                
                // Try to find the popup that contains the menu items
                var popup = recentProjectsMenuItem.GetVisualDescendants()
                    .OfType<Popup>()
                    .FirstOrDefault(p => p.IsOpen);
                    
                DebugTrace($"Popup found: {popup != null}");
                if (popup != null)
                {
                    DebugTrace($"Popup IsOpen: {popup.IsOpen}, IsVisible: {popup.IsVisible}");
                }
                    
                if (popup != null)
                {
                    // Find all menu items in the popup
                    var menuItems = popup.GetVisualDescendants()
                        .OfType<MenuItem>()
                        .Where(mi => mi.Tag is string)
                        .ToList();
                    
                    DebugTrace($"Menu items found in popup: {menuItems.Count}");
                    
                    for (int i = 0; i < menuItems.Count; i++)
                    {
                        var mi = menuItems[i];
                        DebugTrace($"  MenuItem[{i}]: Header='{mi.Header}', Tag='{mi.Tag}', IsFocused={mi.IsFocused}, IsPointerOver={mi.IsPointerOver}, IsEnabled={mi.IsEnabled}");
                    }
                    
                    // Try to find focused item
                    selectedItem = menuItems.FirstOrDefault(mi => mi.IsFocused);
                    DebugTrace($"Focused item found: {selectedItem != null}");
                    if (selectedItem != null)
                    {
                        DebugTrace($"  Focused item: Header='{selectedItem.Header}', Tag='{selectedItem.Tag}'");
                    }
                    
                    // If not focused, try pointer over
                    if (selectedItem == null)
                    {
                        selectedItem = menuItems.FirstOrDefault(mi => mi.IsPointerOver);
                        DebugTrace($"Pointer-over item found: {selectedItem != null}");
                        if (selectedItem != null)
                        {
                            DebugTrace($"  Pointer-over item: Header='{selectedItem.Header}', Tag='{selectedItem.Tag}'");
                        }
                    }
                    
                    // If still not found, use first item
                    if (selectedItem == null && menuItems.Count > 0)
                    {
                        selectedItem = menuItems[0];
                        DebugTrace($"Using first menu item as fallback: Header='{selectedItem.Header}', Tag='{selectedItem.Tag}'");
                    }
                    
                    if (selectedItem != null && selectedItem.Tag is string path)
                    {
                        filePath = path;
                        DebugTrace($"FilePath from menu item: {filePath}");
                    }
                }
                
                // Fallback: if we couldn't find menu items visually, use the first recent project from ViewModel
                if (string.IsNullOrEmpty(filePath) && viewModel.RecentProjects.Count > 0)
                {
                    filePath = viewModel.RecentProjects[0].FilePath;
                    DebugTrace($"Using fallback filePath from ViewModel: {filePath}");
                }
                
                if (!string.IsNullOrEmpty(filePath))
                {
                    DebugTrace($"Executing command with filePath: {filePath}");
                    e.Handled = true;
                    
                    if (selectedItem != null)
                    {
                        DebugTrace($"Triggering Click event on menu item: Header='{selectedItem.Header}'");
                        // Trigger the Click event on the menu item (this will call OnRecentProjectClick)
                        var clickEventArgs = new RoutedEventArgs(MenuItem.ClickEvent);
                        selectedItem.RaiseEvent(clickEventArgs);
                    }
                    else
                    {
                        DebugTrace("Executing command directly (no menu item found)");
                        // Fallback: execute command directly
                        CloseAllMenus();
                        Dispatcher.UIThread.Post(() =>
                        {
                            DebugTrace($"Executing OpenRecentProjectCommand with: {filePath}");
                            viewModel.OpenRecentProjectCommand.Execute(filePath);
                        }, DispatcherPriority.Loaded);
                    }
                    return;
                }
                else
                {
                    DebugTrace("No filePath found, cannot execute command");
                }
            }
            else
            {
                DebugTrace("Recent Projects submenu is not open");
            }
        }
        
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


    public void CloseAllMenus()
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
    
    
    private MenuItem? GetSelectedRecentProjectMenuItem(MenuItem parentMenuItem)
    {
        // Find the menu item that is currently highlighted (has pointer over or is focused)
        var allMenuItems = parentMenuItem.GetVisualDescendants()
            .OfType<MenuItem>()
            .Where(mi => mi.Tag is string)
            .ToList();
            
        // First try to find one with focus
        var focused = allMenuItems.FirstOrDefault(mi => mi.IsFocused);
        if (focused != null)
            return focused;
            
        // Then try to find one with pointer over
        var pointerOver = allMenuItems.FirstOrDefault(mi => mi.IsPointerOver);
        if (pointerOver != null)
            return pointerOver;
            
        // If submenu is open, return the first item (default selection)
        if (parentMenuItem.IsSubMenuOpen && allMenuItems.Count > 0)
            return allMenuItems[0];
            
        return null;
    }
    
    private void OnRecentProjectsSubmenuOpened(object? sender, RoutedEventArgs e)
    {
        DebugTrace($"OnRecentProjectsSubmenuOpened called, sender: {sender?.GetType().Name}");
        
        // Attach KeyDown handlers to menu items when submenu opens
        if (sender is MenuItem parentMenuItem)
        {
            DebugTrace($"Parent MenuItem: Header='{parentMenuItem.Header}', IsSubMenuOpen={parentMenuItem.IsSubMenuOpen}");
            Dispatcher.UIThread.Post(() =>
            {
                DebugTrace("Attaching handlers to menu items (dispatched)");
                AttachKeyHandlersToRecentProjectItems(parentMenuItem);
            }, DispatcherPriority.Loaded);
        }
        else
        {
            DebugTrace("Sender is not MenuItem");
        }
    }
    
    private void AttachKeyHandlersToRecentProjectItems(MenuItem parentMenuItem)
    {
        DebugTrace("AttachKeyHandlersToRecentProjectItems called");
        
        // First, try to get items from the Items collection
        DebugTrace($"Parent MenuItem Items count: {parentMenuItem.Items?.Count ?? 0}");
        if (parentMenuItem.Items != null)
        {
            for (int i = 0; i < parentMenuItem.Items.Count; i++)
            {
                var item = parentMenuItem.Items[i];
                DebugTrace($"  Items[{i}]: Type={item?.GetType().Name}, Value={item}");
                
                if (item is MenuItem menuItem)
                {
                    DebugTrace($"    MenuItem Header: '{menuItem.Header}', Tag: '{menuItem.Tag}'");
                    menuItem.KeyDown -= OnRecentProjectMenuItemKeyDown;
                    menuItem.KeyDown += OnRecentProjectMenuItemKeyDown;
                    DebugTrace($"    Attached KeyDown handler to Items[{i}]");
                }
            }
        }
        
        // Also try to find via visual tree with a delay
        Dispatcher.UIThread.Post(() =>
        {
            DebugTrace("Trying visual tree search after delay");
            FindAndAttachHandlersViaVisualTree(parentMenuItem);
        }, DispatcherPriority.Loaded);
    }
    
    private void FindAndAttachHandlersViaVisualTree(MenuItem parentMenuItem)
    {
        // Find the popup
        var popup = parentMenuItem.GetVisualDescendants()
            .OfType<Popup>()
            .FirstOrDefault(p => p.IsOpen);
            
        DebugTrace($"Popup search result: {popup != null}");
        
        if (popup != null)
        {
            DebugTrace($"Popup details - IsOpen: {popup.IsOpen}, IsVisible: {popup.IsVisible}");
            DebugTrace($"Popup Child type: {popup.Child?.GetType().Name}");
            
            // The popup's child is likely a Panel containing the menu items
            if (popup.Child != null)
            {
                DebugTrace($"Searching Popup.Child for MenuItems");
                var menuItems = popup.Child.GetVisualDescendants()
                    .OfType<MenuItem>()
                    .ToList();
                    
                DebugTrace($"Found {menuItems.Count} MenuItem objects in Popup.Child");
                
                for (int i = 0; i < menuItems.Count; i++)
                {
                    var menuItem = menuItems[i];
                    DebugTrace($"  MenuItem[{i}]: Header='{menuItem.Header}', Tag='{menuItem.Tag}', TagType={menuItem.Tag?.GetType().Name}");
                    DebugTrace($"    Parent: {menuItem.GetVisualParent()?.GetType().Name}");
                    DebugTrace($"    IsFocused: {menuItem.IsFocused}, IsPointerOver: {menuItem.IsPointerOver}");
                }
                
                // Attach handlers to ALL menu items, not just those with Tags
                // Some menu items might be wrappers or containers
                for (int i = 0; i < menuItems.Count; i++)
                {
                    var menuItem = menuItems[i];
                    DebugTrace($"  Attaching handler to MenuItem[{i}]: Header='{menuItem.Header}', Tag='{menuItem.Tag}'");
                    
                    // Remove existing handler to avoid duplicates
                    menuItem.KeyDown -= OnRecentProjectMenuItemKeyDown;
                    menuItem.KeyDown += OnRecentProjectMenuItemKeyDown;
                    DebugTrace($"    Attached KeyDown handler to MenuItem[{i}]");
                }
                
                // Also store reference to menu items with Tags for lookup
                var menuItemsWithTag = menuItems.Where(mi => mi.Tag is string && !string.IsNullOrEmpty(mi.Tag.ToString())).ToList();
                DebugTrace($"Menu items with Tag: {menuItemsWithTag.Count}");
                
            }
            
            // Try to find all controls in the popup
            var allControls = popup.GetVisualDescendants().ToList();
            DebugTrace($"All controls in popup (including popup itself): {allControls.Count}");
            
            // List first few control types for debugging
            for (int i = 0; i < Math.Min(10, allControls.Count); i++)
            {
                DebugTrace($"  Control[{i}]: {allControls[i].GetType().Name}");
            }
        }
        else
        {
            DebugTrace("Popup not found when trying to attach handlers");
            DebugTrace($"Parent MenuItem visual descendants count: {parentMenuItem.GetVisualDescendants().Count()}");
            var allPopups = parentMenuItem.GetVisualDescendants().OfType<Popup>().ToList();
            DebugTrace($"All Popups found: {allPopups.Count}");
            for (int i = 0; i < allPopups.Count; i++)
            {
                var p = allPopups[i];
                DebugTrace($"  Popup[{i}]: IsOpen={p.IsOpen}, IsVisible={p.IsVisible}");
            }
        }
    }
    
    private void OnRecentProjectMenuItemKeyDown(object? sender, KeyEventArgs e)
    {
        DebugTrace($"OnRecentProjectMenuItemKeyDown: Key={e.Key}, Handled={e.Handled}, KeyModifiers={e.KeyModifiers}");
        DebugTrace($"  Sender: {sender?.GetType().Name}");
        
        if (e.Key == Key.Enter && sender is MenuItem menuItem)
        {
            DebugTrace($"Enter pressed on menu item");
            DebugTrace($"  MenuItem Header: '{menuItem.Header}', Tag: '{menuItem.Tag}', TagType: {menuItem.Tag?.GetType().Name}");
            
            string? filePath = null;
            
            // If this menu item has a Tag, use it
            if (menuItem.Tag is string tag && !string.IsNullOrEmpty(tag))
            {
                filePath = tag;
                DebugTrace($"  Using Tag as filePath: {filePath}");
            }
            else
            {
                // Try to find a sibling menu item with a Tag
                // The focused item might be a wrapper, find the actual menu item nearby
                var parent = menuItem.GetVisualParent();
                if (parent != null)
                {
                    DebugTrace($"  Searching parent for menu items with Tag");
                    var siblings = parent.GetVisualChildren()
                        .OfType<MenuItem>()
                        .Where(mi => mi.Tag is string && !string.IsNullOrEmpty(mi.Tag.ToString()))
                        .ToList();
                    
                    DebugTrace($"  Found {siblings.Count} sibling menu items with Tag");
                    
                    // Use the first one, or try to find one that matches by Header
                    if (siblings.Count > 0)
                    {
                        // Try to match by finding the menu item that corresponds to this one
                        // In Avalonia menus, the structure might be: wrapper MenuItem -> actual MenuItem
                        var actualMenuItem = siblings.FirstOrDefault();
                        if (actualMenuItem != null)
                        {
                            filePath = actualMenuItem.Tag as string;
                            DebugTrace($"  Using sibling menu item Tag as filePath: {filePath}");
                            // Use the actual menu item for the click event
                            menuItem = actualMenuItem;
                        }
                    }
                }
                
                // Fallback: find the focused menu item's corresponding item with Tag
                if (string.IsNullOrEmpty(filePath))
                {
                    DebugTrace($"  Fallback: searching popup for focused menu item with Tag");
                    var recentProjectsMenuItem = this.FindControl<MenuItem>("recentProjectsMenuItem");
                    if (recentProjectsMenuItem != null)
                    {
                        var popup = recentProjectsMenuItem.GetVisualDescendants()
                            .OfType<Popup>()
                            .FirstOrDefault(p => p.IsOpen);
                            
                        if (popup?.Child != null)
                        {
                            var allMenuItems = popup.Child.GetVisualDescendants()
                                .OfType<MenuItem>()
                                .Where(mi => mi.Tag is string && !string.IsNullOrEmpty(mi.Tag.ToString()))
                                .ToList();
                                
                            // Find the focused one, or use first
                            var focusedWithTag = allMenuItems.FirstOrDefault(mi => mi.IsFocused);
                            if (focusedWithTag == null && allMenuItems.Count > 0)
                            {
                                focusedWithTag = allMenuItems[0];
                            }
                            
                            if (focusedWithTag != null)
                            {
                                filePath = focusedWithTag.Tag as string;
                                DebugTrace($"  Found menu item with Tag via fallback: {filePath}");
                                menuItem = focusedWithTag;
                            }
                        }
                    }
                }
            }
            
            if (!string.IsNullOrEmpty(filePath))
            {
                DebugTrace($"Executing command with filePath: {filePath}");
                e.Handled = true;
                
                // Trigger the Click event which will call OnRecentProjectClick
                DebugTrace("Raising Click event on menu item");
                var clickEventArgs = new RoutedEventArgs(MenuItem.ClickEvent);
                menuItem.RaiseEvent(clickEventArgs);
                DebugTrace("Click event raised");
            }
            else
            {
                DebugTrace("Could not determine filePath, cannot execute command");
            }
        }
        else
        {
            if (e.Key != Key.Enter)
                DebugTrace($"  Key is not Enter (Key={e.Key})");
            if (sender is not MenuItem)
                DebugTrace("  Sender is not MenuItem");
        }
    }
    
    private void OnRecentProjectClick(object? sender, RoutedEventArgs e)
    {
        DebugTrace($"OnRecentProjectClick called, sender: {sender?.GetType().Name}, RoutedEvent: {e.RoutedEvent?.Name}");
        
        if (sender is MenuItem menuItem)
        {
            DebugTrace($"MenuItem details - Header: '{menuItem.Header}', Tag: '{menuItem.Tag}', TagType: {menuItem.Tag?.GetType().Name}");
            DebugTrace($"MenuItem state - IsEnabled: {menuItem.IsEnabled}, IsVisible: {menuItem.IsVisible}, IsFocused: {menuItem.IsFocused}");
        }
        
        if (sender is MenuItem menuItem2 && menuItem2.Tag is string filePath && DataContext is MainWindowViewModel vm)
        {
            DebugTrace($"All conditions met, executing OpenRecentProjectCommand with filePath: {filePath}");
            e.Handled = true;
            DebugTrace("Closing all menus");
            CloseAllMenus();
            DebugTrace("Posting command execution to UI thread");
            Dispatcher.UIThread.Post(() =>
            {
                DebugTrace($"Inside Dispatcher.Post, executing command: {filePath}");
                vm.OpenRecentProjectCommand.Execute(filePath);
                DebugTrace("Command execution completed");
            }, DispatcherPriority.Loaded);
        }
        else
        {
            DebugTrace("OnRecentProjectClick: Conditions not met");
            if (sender is not MenuItem)
                DebugTrace("  - sender is not MenuItem");
            if (sender is MenuItem mi && !(mi.Tag is string))
                DebugTrace($"  - Tag is not string (Tag: '{mi.Tag}', Type: {mi.Tag?.GetType().Name})");
            if (DataContext is not MainWindowViewModel)
                DebugTrace($"  - DataContext is not MainWindowViewModel (Type: {DataContext?.GetType().Name})");
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
