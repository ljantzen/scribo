using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Scribo.Models;
using Scribo.ViewModels;
using Scribo.Views;

namespace Scribo.Views.Handlers;

public class DocumentLinkAutocompleteHandler
{
    private readonly MainWindow _window;
    private readonly Action<string> _debugTrace;

    public DocumentLinkAutocompleteHandler(MainWindow window, Action<string> debugTrace)
    {
        _window = window;
        _debugTrace = debugTrace;
    }

    public void Setup()
    {
        var textBox = _window.FindControl<TextBox>("sourceTextBox");
        var popup = _window.FindControl<Popup>("autocompletePopup");
        var autocompleteControl = popup?.Child as DocumentLinkAutocompletePopup;

        if (textBox != null && popup != null && autocompleteControl != null)
        {
            // Handle keyboard events for autocomplete navigation
            textBox.KeyDown += OnEditorKeyDown;

            // Also attach to GotFocus to dynamically control AcceptsTab
            textBox.GotFocus += (s, e) =>
            {
                UpdateAcceptsTabForAutocomplete(textBox);
            };

            // Attach TextChanged handler (in addition to XAML binding to ensure it's called)
            textBox.TextChanged += OnEditorTextChanged;

            // Handle item selection from popup
            autocompleteControl.InsertDocumentRequested += OnAutocompleteItemSelected;

            // Handle popup visibility changes
            if (_window.DataContext is MainWindowViewModel vm)
            {
                vm.DocumentLinkAutocompleteViewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(DocumentLinkAutocompleteViewModel.IsVisible))
                    {
                        if (vm.DocumentLinkAutocompleteViewModel.IsVisible)
                        {
                            UpdateAutocompletePopupPosition();
                        }
                        // Update AcceptsTab when autocomplete visibility changes
                        UpdateAcceptsTabForAutocomplete(textBox);
                    }
                };
            }

            // Close popup when clicking outside
            _window.PointerPressed += (s, e) =>
            {
                if (popup.IsOpen && autocompleteControl != null)
                {
                    var hitTest = e.Source as Control;
                    if (hitTest != null && !autocompleteControl.IsPointerOver && hitTest != textBox)
                    {
                        if (_window.DataContext is MainWindowViewModel viewModel)
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
        var textBox = _window.FindControl<TextBox>("sourceTextBox");
        if (textBox != null && _window.DataContext is MainWindowViewModel vm)
        {
            InsertSelectedDocumentLink(textBox, vm, document);
        }
    }

    private void UpdateAcceptsTabForAutocomplete(TextBox textBox)
    {
        if (textBox == null || _window.DataContext is not MainWindowViewModel vm)
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
        if (sender is not TextBox textBox || _window.DataContext is not MainWindowViewModel vm)
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
                    // Check if we're in a metadata field (frontmatter) and determine document type filter
                    DocumentType? documentTypeFilter = DetectMetadataFieldContext(text, absoluteBracketIndex);
                    vm.DocumentLinkAutocompleteViewModel.SetDocumentTypeFilter(documentTypeFilter);
                    
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
        
        // Clear document type filter when not in a [[...]] block
        vm.DocumentLinkAutocompleteViewModel.SetDocumentTypeFilter(null);

        // Hide autocomplete if we're not inside [[...]] or if we've closed it
        vm.DocumentLinkAutocompleteViewModel.Hide();
    }

    public void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || _window.DataContext is not MainWindowViewModel vm)
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
    }

    public void InsertSelectedDocumentLink(TextBox textBox, MainWindowViewModel vm, Document? document = null)
    {
        var autocompleteVm = vm.DocumentLinkAutocompleteViewModel;
        var selectedDoc = document ?? autocompleteVm.GetSelectedDocument();

        if (selectedDoc == null && autocompleteVm.Suggestions.Count > 0)
        {
            // If nothing selected, use first suggestion
            selectedDoc = autocompleteVm.Suggestions[0];
        }

        if (selectedDoc == null)
        {
            return;
        }

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
        var textBox = _window.FindControl<TextBox>("sourceTextBox");
        var popup = _window.FindControl<Popup>("autocompletePopup");

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
        var typeface = new Avalonia.Media.Typeface(fontFamily);

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
        var currentLineStart = lastNewlineIndex >= 0 ? lastNewlineIndex + 1 : 0;
        var textOnCurrentLineToBracket = text.Substring(currentLineStart, absoluteBracketIndex - currentLineStart);

        // Measure the text width
        var formattedText = new Avalonia.Media.FormattedText(
            textOnCurrentLineToBracket,
            System.Globalization.CultureInfo.CurrentCulture,
            Avalonia.Media.FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Avalonia.Media.Brushes.Black);

        var horizontalPositionFromLeft = textBoxPadding + formattedText.Width;

        // Position popup below the line where [[ appears
        popup.HorizontalOffset = horizontalPositionFromLeft;
        popup.VerticalOffset = verticalPositionFromTop + lineHeight + 2; // Small gap below line

        // Ensure popup is visible
        popup.IsOpen = true;
    }

    /// <summary>
    /// Detects if we're in a metadata field context and returns the appropriate document type filter.
    /// </summary>
    private DocumentType? DetectMetadataFieldContext(string text, int bracketIndex)
    {
        // Check if we're in frontmatter (between --- delimiters)
        var textBeforeBracket = text.Substring(0, bracketIndex);
        var frontmatterStart = textBeforeBracket.LastIndexOf("---");
        if (frontmatterStart < 0)
            return null; // Not in frontmatter
        
        // Check if there's a closing --- after the bracket (if so, we're not in frontmatter)
        var textAfterBracket = text.Substring(bracketIndex);
        var frontmatterEnd = textAfterBracket.IndexOf("---");
        if (frontmatterEnd >= 0)
            return null; // Past frontmatter
        
        // We're in frontmatter, now check which field we're in
        // Look backwards from bracket to find the field name
        var frontmatterSection = text.Substring(frontmatterStart, bracketIndex - frontmatterStart);
        
        // Find the last newline before the bracket
        var lastNewlineIndex = frontmatterSection.LastIndexOf('\n');
        if (lastNewlineIndex < 0)
            return null;
        
        // Get the line containing the bracket
        var lineStart = lastNewlineIndex + 1;
        var lineEnd = frontmatterSection.Length;
        var line = frontmatterSection.Substring(lineStart, lineEnd - lineStart);
        
        // Check for metadata field patterns
        // Format: fieldname: value or fieldname: [value1, value2] or fieldname: [[value]]
        if (line.Contains("pov:", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentType.Character;
        }
        
        if (line.Contains("focus:", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentType.Character;
        }
        
        if (line.Contains("characters:", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentType.Character;
        }
        
        if (line.Contains("timeline:", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentType.Timeline;
        }
        
        if (line.Contains("plot:", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentType.Plot;
        }
        
        if (line.Contains("objects:", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentType.Object;
        }
        
        if (line.Contains("entities:", StringComparison.OrdinalIgnoreCase))
        {
            return DocumentType.Entity;
        }
        
        return null; // No specific filter
    }
}
