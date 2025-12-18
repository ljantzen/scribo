using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
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
using Scribo.Views.Handlers;

namespace Scribo.Views;

public partial class MainWindow : Window
{
    // Set to false to disable all debug tracing
    private const bool ENABLE_DEBUG_TRACING = true;
    
    private void DebugTrace(string message)
    {
        // Debug tracing disabled
    }
    
    private readonly PluginManager _pluginManager;
    private readonly PluginContext _pluginContext;
    
    // Handlers
    private readonly DocumentLinkAutocompleteHandler _autocompleteHandler;
    private readonly FindReplaceHandler _findReplaceHandler;
    private readonly MarkdownBlockNavigationHandler _markdownNavigationHandler;
    private readonly KeyboardShortcutHandler _keyboardShortcutHandler;
    private readonly KeyboardEventHandler _keyboardEventHandler;

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
        
        // Subscribe to collection changes to expand root node when project is loaded
        viewModel.ProjectTreeItems.CollectionChanged += OnProjectTreeItemsChanged;
        
        // Initialize handlers
        _autocompleteHandler = new DocumentLinkAutocompleteHandler(this, DebugTrace);
        _findReplaceHandler = new FindReplaceHandler(this);
        _markdownNavigationHandler = new MarkdownBlockNavigationHandler(this);
        _keyboardShortcutHandler = new KeyboardShortcutHandler(this);
        _keyboardEventHandler = new KeyboardEventHandler(this, DebugTrace, _autocompleteHandler, FocusRenameTextBox);
        
        // Setup drag and drop handlers
        AddHandler(DragDrop.DropEvent, OnTreeViewDrop);
        AddHandler(DragDrop.DragOverEvent, OnTreeViewDragOver);
        
        // Setup handlers
        _keyboardEventHandler.Setup();
        _keyboardShortcutHandler.LoadKeyboardShortcuts();
        _findReplaceHandler.Setup();
        _autocompleteHandler.Setup();
        _markdownNavigationHandler.Setup();
    }
    
    public void ReloadKeyboardShortcuts()
    {
        _keyboardShortcutHandler.LoadKeyboardShortcuts();
    }
    
    public void OnNavigateToDocument(string documentId)
    {
        _markdownNavigationHandler.OnNavigateToDocument(documentId);
    }
    
    // Autocomplete, keyboard shortcuts, find/replace, and markdown navigation methods 
    // have been moved to handler classes in the Handlers/ directory:
    // - DocumentLinkAutocompleteHandler
    // - KeyboardShortcutHandler  
    // - KeyboardEventHandler
    // - FindReplaceHandler
    // - MarkdownBlockNavigationHandler
        
    private void OnAutocompleteItemSelected(Document document)
    {
        var textBox = this.FindControl<TextBox>("sourceTextBox");
        if (textBox != null && DataContext is MainWindowViewModel vm)
        {
            InsertSelectedDocumentLink(textBox, vm, document);
        }
    }
    
    private void UpdateAcceptsTabForAutocomplete(TextBox textBox)
    {
        if (textBox == null || DataContext is not MainWindowViewModel vm)
            return;
            
        var autocompleteVm = vm.DocumentLinkAutocompleteViewModel;
        
        // Check if we're inside a [[...]] block
        var text = textBox.Text ?? string.Empty;
        var caretIndex = textBox.CaretIndex;
        bool shouldDisableTab = false;
        
        if (caretIndex >= 2 && autocompleteVm.IsVisible)
        {
            var searchStart = Math.Max(0, caretIndex - 200);
            var textBeforeCaret = text.Substring(searchStart, caretIndex - searchStart);
            var lastBracketIndex = textBeforeCaret.LastIndexOf("[[");
            
            if (lastBracketIndex >= 0)
            {
                var absoluteBracketIndex = searchStart + lastBracketIndex;
                var textAfterBracket = text.Substring(absoluteBracketIndex + 2);
                var closingBracketIndex = textAfterBracket.IndexOf("]]");
                var caretOffsetFromBracketStart = caretIndex - absoluteBracketIndex;
                
                // If we're inside [[...]] and autocomplete is visible, disable Tab insertion
                if ((closingBracketIndex < 0 || closingBracketIndex >= caretOffsetFromBracketStart - 2) && 
                    caretOffsetFromBracketStart >= 2)
                {
                    shouldDisableTab = true;
                }
            }
        }
        
        // Temporarily disable AcceptsTab when autocomplete is active
        // This allows our KeyDown handler to catch Tab
        if (shouldDisableTab && textBox.AcceptsTab)
        {
            textBox.AcceptsTab = false;
        }
        else if (!shouldDisableTab && !textBox.AcceptsTab)
        {
            textBox.AcceptsTab = true;
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
        
        // Update AcceptsTab based on autocomplete state
        UpdateAcceptsTabForAutocomplete(textBox);
        
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
        {
            return;
        }

        // Only handle autocomplete in source mode
        if (!vm.IsSourceMode)
        {
            return;
        }

        var autocompleteVm = vm.DocumentLinkAutocompleteViewModel;
        
        // Handle Tab key even when popup is not visible, to check if we're in a [[...]] block
        // This allows Tab to complete autocomplete if popup should be visible
        if (e.Key == Key.Tab)
        {
            // Check if we're inside a [[...]] block that should show autocomplete
            var text = textBox.Text ?? string.Empty;
            var caretIndex = textBox.CaretIndex;
            
            if (caretIndex >= 2)
            {
                var searchStart = Math.Max(0, caretIndex - 200);
                var textBeforeCaret = text.Substring(searchStart, caretIndex - searchStart);
                var lastBracketIndex = textBeforeCaret.LastIndexOf("[[");
                
                if (lastBracketIndex >= 0)
                {
                    var absoluteBracketIndex = searchStart + lastBracketIndex;
                    var textAfterBracket = text.Substring(absoluteBracketIndex + 2);
                    var closingBracketIndex = textAfterBracket.IndexOf("]]");
                    var caretOffsetFromBracketStart = caretIndex - absoluteBracketIndex;
                    
                    // If we're inside [[...]] and no closing ]] before caret, show/use autocomplete
                    if ((closingBracketIndex < 0 || closingBracketIndex >= caretOffsetFromBracketStart - 2) && 
                        caretOffsetFromBracketStart >= 2)
                    {
                        // If popup is visible, complete the selection
                        if (autocompleteVm.IsVisible)
                        {
                            e.Handled = true;
                            InsertSelectedDocumentLink(textBox, vm);
                            return;
                        }
                        // If popup should be visible but isn't, try to complete anyway
                        else if (autocompleteVm.Suggestions.Count > 0)
                        {
                            e.Handled = true;
                            InsertSelectedDocumentLink(textBox, vm);
                            return;
                        }
                    }
                }
            }
        }
        
        // Handle autocomplete navigation keys when popup is visible
        // This MUST happen before any other key processing
        if (autocompleteVm.IsVisible)
        {
            switch (e.Key)
            {
                case Key.Down:
                    e.Handled = true;
                    autocompleteVm.SelectNext();
                    return;
                    
                case Key.Up:
                    e.Handled = true;
                    autocompleteVm.SelectPrevious();
                    return;
                    
                case Key.Enter:
                    e.Handled = true;
                    InsertSelectedDocumentLink(textBox, vm);
                    return;
                    
                case Key.Tab:
                    e.Handled = true;
                    InsertSelectedDocumentLink(textBox, vm);
                    return;
                    
                case Key.Escape:
                    e.Handled = true;
                    autocompleteVm.Hide();
                    return;
            }
        }
        else
        {
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
        
        // Get accurate font metrics from TextBox
        var fontSize = textBox.FontSize;
        var fontFamily = textBox.FontFamily;
        var typeface = new Typeface(fontFamily);
        
        // Calculate line height: font size + some padding for line spacing
        // For Consolas at 14pt, typical line height is around 1.2-1.4x font size
        var lineHeight = fontSize * 1.4;
        
        // Calculate vertical position: start from TextBox top + padding + (line number * line height)
        // The TextBox has Padding="10", so we need to account for that
        var textBoxPadding = 10.0; // From XAML Padding="10"
        var verticalPositionFromTop = textBoxPadding + (lineNumber * lineHeight);
        
        // Calculate horizontal position by measuring text from the start of the current line up to [[
        // Find the text of the current line up to the bracket position
        var fullTextToBracket = text.Substring(0, absoluteBracketIndex);
        var lastNewlineIndex = fullTextToBracket.LastIndexOf('\n');
        var lineTextUpToBracket = lastNewlineIndex >= 0
            ? fullTextToBracket.Substring(lastNewlineIndex + 1)
            : fullTextToBracket;
        
        double horizontalPosition;
        if (lineTextUpToBracket.Length > 0)
        {
            try
            {
                // Measure the width of text on current line up to [[
                var formattedLinePart = new FormattedText(
                    lineTextUpToBracket,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    Brushes.Black);
                var linePartWidth = formattedLinePart.Width;
                
                horizontalPosition = textBoxPadding + linePartWidth;
            }
            catch
            {
                // Fallback: use column-based estimate
                var charWidth = fontSize * 0.6; // Approximate monospace char width
                horizontalPosition = textBoxPadding + (columnNumber * charWidth);
            }
        }
        else
        {
            // If line text is empty, use column-based estimate
            var charWidth = fontSize * 0.6; // Approximate monospace char width
            horizontalPosition = textBoxPadding + (columnNumber * charWidth);
        }
        
        // Use Bottom placement mode (which we know works for vertical) and calculate vertical offset from bottom
        popup.Placement = PlacementMode.Bottom;
        var verticalOffsetFromTop = verticalPositionFromTop + lineHeight + 2;
        var textBoxHeight = textBox.Bounds.Height;
        
        if (textBoxHeight > 0)
        {
            // For Bottom placement: offset from bottom = desired position from top - textBox height
            popup.VerticalOffset = verticalOffsetFromTop - textBoxHeight;
        }
        else
        {
            popup.VerticalOffset = -50; // Fallback
        }
        
        // Set horizontal offset to align with [[ position
        popup.HorizontalOffset = horizontalPosition;
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
    
    private void OnWindowKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        // Tunnel handler - called BEFORE bubble handlers
        // This intercepts Up/Down/Enter/Escape keys before TextBox can consume them
        if ((e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Enter || e.Key == Key.Escape) && DataContext is MainWindowViewModel vm)
        {
            var textBox = this.FindControl<TextBox>("sourceTextBox");
            if (textBox != null && textBox.IsFocused && vm.IsSourceMode)
            {
                var autocompleteVm = vm.DocumentLinkAutocompleteViewModel;
                if (autocompleteVm.IsVisible)
                {
                    e.Handled = true;
                    
                    if (e.Key == Key.Down)
                    {
                        var oldIndex = autocompleteVm.SelectedIndex;
                        autocompleteVm.SelectNext();
                    }
                    else if (e.Key == Key.Up)
                    {
                        var oldIndex = autocompleteVm.SelectedIndex;
                        autocompleteVm.SelectPrevious();
                    }
                    else if (e.Key == Key.Enter)
                    {
                        InsertSelectedDocumentLink(textBox, vm);
                    }
                    else if (e.Key == Key.Escape)
                    {
                        autocompleteVm.Hide();
                    }
                }
                else
                {
                }
            }
            else
            {
            }
        }
        else
        {
        }
    }
    
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        
        // Handle other shortcuts (menu navigation, etc.)
        
        // Handle Tab key for autocomplete completion when TextBox has focus
        // This catches Tab before TextBox consumes it (when AcceptsTab=True)
        if (e.Key == Key.Tab && !e.Handled)
        {
            var textBox = this.FindControl<TextBox>("sourceTextBox");
            if (textBox != null && textBox.IsFocused && DataContext is MainWindowViewModel viewModelForTab)
            {
                if (viewModelForTab.IsSourceMode)
                {
                    var autocompleteVm = viewModelForTab.DocumentLinkAutocompleteViewModel;
                    
                    // Check if we're inside a [[...]] block
                    var text = textBox.Text ?? string.Empty;
                    var caretIndex = textBox.CaretIndex;
                    
                    if (caretIndex >= 2)
                    {
                        var searchStart = Math.Max(0, caretIndex - 200);
                        var textBeforeCaret = text.Substring(searchStart, caretIndex - searchStart);
                        var lastBracketIndex = textBeforeCaret.LastIndexOf("[[");
                        
                        if (lastBracketIndex >= 0)
                        {
                            var absoluteBracketIndex = searchStart + lastBracketIndex;
                            var textAfterBracket = text.Substring(absoluteBracketIndex + 2);
                            var closingBracketIndex = textAfterBracket.IndexOf("]]");
                            var caretOffsetFromBracketStart = caretIndex - absoluteBracketIndex;
                            
                            // If we're inside [[...]] and autocomplete is active, complete it
                            if ((closingBracketIndex < 0 || closingBracketIndex >= caretOffsetFromBracketStart - 2) && 
                                caretOffsetFromBracketStart >= 2)
                            {
                                if (autocompleteVm.IsVisible || autocompleteVm.Suggestions.Count > 0)
                                {
                                    e.Handled = true;
                                    InsertSelectedDocumentLink(textBox, viewModelForTab);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }
        
        // Handle Enter key when Recent Projects submenu is open (check this FIRST)
        if (e.Key == Key.Enter && !e.Handled && DataContext is MainWindowViewModel viewModel)
        {
            var recentProjectsMenuItem = this.FindControl<MenuItem>("recentProjectsMenuItem");
            
            if (recentProjectsMenuItem != null && recentProjectsMenuItem.IsSubMenuOpen)
            {
                MenuItem? selectedItem = null;
                string? filePath = null;
                
                // Try to find the popup that contains the menu items
                var popup = recentProjectsMenuItem.GetVisualDescendants()
                    .OfType<Popup>()
                    .FirstOrDefault(p => p.IsOpen);
                    
                if (popup != null)
                {
                    // Find all menu items in the popup
                    var menuItems = popup.GetVisualDescendants()
                        .OfType<MenuItem>()
                        .Where(mi => mi.Tag is string)
                        .ToList();
                    
                    // Try to find focused item
                    selectedItem = menuItems.FirstOrDefault(mi => mi.IsFocused);
                    
                    // If not focused, try pointer over
                    if (selectedItem == null)
                    {
                        selectedItem = menuItems.FirstOrDefault(mi => mi.IsPointerOver);
                    }
                    
                    // If still not found, use first item
                    if (selectedItem == null && menuItems.Count > 0)
                    {
                        selectedItem = menuItems[0];
                    }
                    
                    if (selectedItem != null && selectedItem.Tag is string path)
                    {
                        filePath = path;
                    }
                }
                
                // Fallback: if we couldn't find menu items visually, use the first recent project from ViewModel
                if (string.IsNullOrEmpty(filePath) && viewModel.RecentProjects.Count > 0)
                {
                    filePath = viewModel.RecentProjects[0].FilePath;
                }
                
                if (!string.IsNullOrEmpty(filePath))
                {
                    e.Handled = true;
                    
                    if (selectedItem != null)
                    {
                        // Trigger the Click event on the menu item (this will call OnRecentProjectClick)
                        var clickEventArgs = new RoutedEventArgs(MenuItem.ClickEvent);
                        selectedItem.RaiseEvent(clickEventArgs);
                    }
                    else
                    {
                        // Fallback: execute command directly
                        CloseAllMenus();
                        Dispatcher.UIThread.Post(() =>
                        {
                            viewModel.OpenRecentProjectCommand.Execute(filePath);
                        }, DispatcherPriority.Loaded);
                    }
                    return;
                }
            }
        }
        
        if (DataContext is MainWindowViewModel viewModelForShortcuts)
        {
            var settingsService = new ApplicationSettingsService();
            var settings = settingsService.LoadSettings();
            
            // Handle ToggleViewMode shortcut (configurable, default Ctrl+P)
            string toggleViewModeShortcut = settings.KeyboardShortcuts.ContainsKey("ToggleViewMode") 
                ? settings.KeyboardShortcuts["ToggleViewMode"] 
                : "Ctrl+P";
            
            try
            {
                var toggleViewModeGesture = KeyGesture.Parse(toggleViewModeShortcut);
                if (toggleViewModeGesture.Matches(e))
                {
                    e.Handled = true;
                    viewModelForShortcuts.ToggleViewModeCommand.Execute(null);
                    return;
                }
            }
            catch
            {
                // Fallback to Ctrl+P if parsing fails
                if (e.Key == Key.P && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    e.Handled = true;
                    viewModelForShortcuts.ToggleViewModeCommand.Execute(null);
                    return;
                }
            }
            
            // Handle local find shortcut (Ctrl+F)
            if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control) && !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                e.Handled = true;
                viewModelForShortcuts.ShowLocalFindCommand.Execute(null);
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
                    viewModelForShortcuts.ShowSearchCommand.Execute(null);
                    return;
                }
            }
            catch
            {
                // Fallback to Ctrl+Shift+F if parsing fails
                if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    e.Handled = true;
                    viewModelForShortcuts.ShowSearchCommand.Execute(null);
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
                    if (viewModelForShortcuts.SelectedProjectItem != null && 
                        (viewModelForShortcuts.SelectedProjectItem.IsChapter || viewModelForShortcuts.SelectedProjectItem.Document != null || viewModelForShortcuts.SelectedProjectItem.IsSubfolder))
                    {
                        e.Handled = true;
                        viewModelForShortcuts.RenameChapterCommand.Execute(viewModelForShortcuts.SelectedProjectItem);
                        
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
                    if (viewModelForShortcuts.SelectedProjectItem != null && 
                        (viewModelForShortcuts.SelectedProjectItem.IsChapter || viewModelForShortcuts.SelectedProjectItem.Document != null || viewModelForShortcuts.SelectedProjectItem.IsSubfolder))
                    {
                        e.Handled = true;
                        viewModelForShortcuts.RenameChapterCommand.Execute(viewModelForShortcuts.SelectedProjectItem);
                        
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
        // Find the project tree TreeView
        var treeView = this.FindControl<TreeView>("projectTreeViewLeft");
        
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
        
        // Attach KeyDown handlers to menu items when submenu opens
        if (sender is MenuItem parentMenuItem)
        {
            Dispatcher.UIThread.Post(() =>
            {
                AttachKeyHandlersToRecentProjectItems(parentMenuItem);
            }, DispatcherPriority.Loaded);
        }
        else
        {
        }
    }
    
    private void AttachKeyHandlersToRecentProjectItems(MenuItem parentMenuItem)
    {
        
        // First, try to get items from the Items collection
        if (parentMenuItem.Items != null)
        {
            for (int i = 0; i < parentMenuItem.Items.Count; i++)
            {
                var item = parentMenuItem.Items[i];
                
                if (item is MenuItem menuItem)
                {
                    menuItem.KeyDown -= OnRecentProjectMenuItemKeyDown;
                    menuItem.KeyDown += OnRecentProjectMenuItemKeyDown;
                }
            }
        }
        
        // Also try to find via visual tree with a delay
        Dispatcher.UIThread.Post(() =>
        {
            FindAndAttachHandlersViaVisualTree(parentMenuItem);
        }, DispatcherPriority.Loaded);
    }
    
    private void FindAndAttachHandlersViaVisualTree(MenuItem parentMenuItem)
    {
        // Find the popup
        var popup = parentMenuItem.GetVisualDescendants()
            .OfType<Popup>()
            .FirstOrDefault(p => p.IsOpen);
            
        
        if (popup != null)
        {
            
            // The popup's child is likely a Panel containing the menu items
            if (popup.Child != null)
            {
                var menuItems = popup.Child.GetVisualDescendants()
                    .OfType<MenuItem>()
                    .ToList();
                    
                
                for (int i = 0; i < menuItems.Count; i++)
                {
                    var menuItem = menuItems[i];
                }
                
                // Attach handlers to ALL menu items, not just those with Tags
                // Some menu items might be wrappers or containers
                for (int i = 0; i < menuItems.Count; i++)
                {
                    var menuItem = menuItems[i];
                    
                    // Remove existing handler to avoid duplicates
                    menuItem.KeyDown -= OnRecentProjectMenuItemKeyDown;
                    menuItem.KeyDown += OnRecentProjectMenuItemKeyDown;
                }
                
                // Also store reference to menu items with Tags for lookup
                var menuItemsWithTag = menuItems.Where(mi => mi.Tag is string && !string.IsNullOrEmpty(mi.Tag.ToString())).ToList();
                
            }
            
            // Try to find all controls in the popup
            var allControls = popup.GetVisualDescendants().ToList();
            
            // List first few control types for debugging
            for (int i = 0; i < Math.Min(10, allControls.Count); i++)
            {
            }
        }
        else
        {
            var allPopups = parentMenuItem.GetVisualDescendants().OfType<Popup>().ToList();
            for (int i = 0; i < allPopups.Count; i++)
            {
                var p = allPopups[i];
            }
        }
    }
    
    private void OnRecentProjectMenuItemKeyDown(object? sender, KeyEventArgs e)
    {
        
        if (e.Key == Key.Enter && sender is MenuItem menuItem)
        {
            
            string? filePath = null;
            
            // If this menu item has a Tag, use it
            if (menuItem.Tag is string tag && !string.IsNullOrEmpty(tag))
            {
                filePath = tag;
            }
            else
            {
                // Try to find a sibling menu item with a Tag
                // The focused item might be a wrapper, find the actual menu item nearby
                var parent = menuItem.GetVisualParent();
                if (parent != null)
                {
                    var siblings = parent.GetVisualChildren()
                        .OfType<MenuItem>()
                        .Where(mi => mi.Tag is string && !string.IsNullOrEmpty(mi.Tag.ToString()))
                        .ToList();
                    
                    
                    // Use the first one, or try to find one that matches by Header
                    if (siblings.Count > 0)
                    {
                        // Try to match by finding the menu item that corresponds to this one
                        // In Avalonia menus, the structure might be: wrapper MenuItem -> actual MenuItem
                        var actualMenuItem = siblings.FirstOrDefault();
                        if (actualMenuItem != null)
                        {
                            filePath = actualMenuItem.Tag as string;
                            // Use the actual menu item for the click event
                            menuItem = actualMenuItem;
                        }
                    }
                }
                
                // Fallback: find the focused menu item's corresponding item with Tag
                if (string.IsNullOrEmpty(filePath))
                {
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
                                menuItem = focusedWithTag;
                            }
                        }
                    }
                }
            }
            
            if (!string.IsNullOrEmpty(filePath))
            {
                e.Handled = true;
                
                // Trigger the Click event which will call OnRecentProjectClick
                var clickEventArgs = new RoutedEventArgs(MenuItem.ClickEvent);
                menuItem.RaiseEvent(clickEventArgs);
            }
        }
    }
    
    private void OnRecentProjectClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem2 && menuItem2.Tag is string filePath && DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            CloseAllMenus();
            Dispatcher.UIThread.Post(() =>
            {
                vm.OpenRecentProjectCommand.Execute(filePath);
            }, DispatcherPriority.Loaded);
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

    private void OnEmptyTrashcanClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.DataContext is ProjectTreeItemViewModel item && DataContext is MainWindowViewModel vm)
        {
            e.Handled = true;
            vm.EmptyTrashcanCommand.Execute(item);
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
                        
                        // Don't allow dropping into Trashcan via drag-and-drop (use delete instead)
                        if (targetItem.IsTrashcanFolder)
                        {
                            e.DragEffects = DragDropEffects.None;
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
                        // Don't allow dropping on Trashcan folder
                        if (targetItem.IsFolder && !targetItem.IsRoot && !targetItem.IsTrashcanFolder)
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
    
    private void OnProjectTreeItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null && e.NewItems.Count > 0)
        {
            // Expand root node after project tree is loaded
            Dispatcher.UIThread.Post(() =>
            {
                ExpandRootNode();
            }, DispatcherPriority.Loaded);
        }
        else if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // When collection is cleared and then repopulated, wait a bit longer
            Dispatcher.UIThread.Post(() =>
            {
                ExpandRootNode();
            }, DispatcherPriority.Loaded);
        }
    }
    
    private void ExpandRootNode()
    {
        var treeView = this.FindControl<TreeView>("projectTreeViewLeft");
        if (treeView == null)
        {
            return;
        }
        
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }
        
        var rootItem = vm.ProjectTreeItems.FirstOrDefault();
        if (rootItem == null)
        {
            return;
        }
        
        // Set IsExpanded on the view model first
        rootItem.IsExpanded = true;
        
        // Try to find and expand the TreeViewItem container
        // Retry a few times if not found immediately (UI might not be rendered yet)
        int retryCount = 0;
        const int maxRetries = 5;
        
        void TryExpandTreeViewItem()
        {
            var treeViewItem = treeView.GetVisualDescendants()
                .OfType<TreeViewItem>()
                .FirstOrDefault(item => item.DataContext == rootItem);
            
            if (treeViewItem != null)
            {
                treeViewItem.IsExpanded = true;
            }
            else
            {
                retryCount++;
                if (retryCount < maxRetries)
                {
                    Dispatcher.UIThread.Post(TryExpandTreeViewItem, DispatcherPriority.Loaded);
                }
            }
        }
        
        // Start trying to find the TreeViewItem
        Dispatcher.UIThread.Post(TryExpandTreeViewItem, DispatcherPriority.Loaded);
        
        // Force a refresh
        treeView.InvalidateVisual();
    }
}
