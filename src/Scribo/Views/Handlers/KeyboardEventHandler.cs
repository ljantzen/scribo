using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Scribo.Services;
using Scribo.ViewModels;

namespace Scribo.Views.Handlers;

public class KeyboardEventHandler
{
    private readonly MainWindow _window;
    private readonly Action<string> _debugTrace;
    private readonly DocumentLinkAutocompleteHandler? _autocompleteHandler;
    private readonly Action? _focusRenameTextBox;

    public KeyboardEventHandler(MainWindow window, Action<string> debugTrace, DocumentLinkAutocompleteHandler? autocompleteHandler = null, Action? focusRenameTextBox = null)
    {
        _window = window;
        _debugTrace = debugTrace;
        _autocompleteHandler = autocompleteHandler;
        _focusRenameTextBox = focusRenameTextBox;
    }

    public void Setup()
    {
        _window.KeyDown += OnWindowKeyDown;
        _window.AddHandler(InputElement.KeyDownEvent, OnWindowKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    private void OnWindowKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        // Tunnel handler - called BEFORE bubble handlers
        // This intercepts Up/Down/Enter/Escape keys before TextBox can consume them

        if ((e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.Enter || e.Key == Key.Escape) && _window.DataContext is MainWindowViewModel vm)
        {
            var textBox = _window.FindControl<TextBox>("sourceTextBox");

            if (textBox != null && textBox.IsFocused && vm.IsSourceMode)
            {
                var autocompleteVm = vm.DocumentLinkAutocompleteViewModel;

                if (autocompleteVm.IsVisible && _autocompleteHandler != null)
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
                        _autocompleteHandler.InsertSelectedDocumentLink(textBox, vm);
                    }
                    else if (e.Key == Key.Escape)
                    {
                        autocompleteVm.Hide();
                    }
                }
            }
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Handle Tab key for autocomplete completion when TextBox has focus
        // This catches Tab before TextBox consumes it (when AcceptsTab=True)
        if (e.Key == Key.Tab && !e.Handled && _autocompleteHandler != null)
        {
            var textBox = _window.FindControl<TextBox>("sourceTextBox");
            if (textBox != null && textBox.IsFocused && _window.DataContext is MainWindowViewModel viewModelForTab)
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
                                    _autocompleteHandler.InsertSelectedDocumentLink(textBox, viewModelForTab);
                                    return;
                                }
                            }
                        }
                    }
                }
            }
        }

        if (_window.DataContext is MainWindowViewModel viewModelForShortcuts)
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
                        if (_focusRenameTextBox != null)
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                _focusRenameTextBox();
                            }, DispatcherPriority.Loaded);
                        }
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

                        if (_focusRenameTextBox != null)
                        {
                            Dispatcher.UIThread.Post(() =>
                            {
                                _focusRenameTextBox();
                            }, DispatcherPriority.Loaded);
                        }
                    }
                }
            }
        }
    }
}
