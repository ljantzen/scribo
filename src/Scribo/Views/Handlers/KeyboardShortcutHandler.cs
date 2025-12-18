using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Scribo.Services;

namespace Scribo.Views.Handlers;

public class KeyboardShortcutHandler
{
    private readonly MainWindow _window;
    private readonly Dictionary<MenuItem, string> _originalHeaders = new();

    public KeyboardShortcutHandler(MainWindow window)
    {
        _window = window;
    }

    public void LoadKeyboardShortcuts()
    {
        var settingsService = new ApplicationSettingsService();
        var settings = settingsService.LoadSettings();

        // Store original header texts before modifying
        StoreOriginalHeaders();

        // Apply shortcuts to menu items
        ApplyShortcut("NewProject", settings.KeyboardShortcuts, _window.newProjectMenuItem);
        ApplyShortcut("OpenProject", settings.KeyboardShortcuts, _window.openProjectMenuItem);
        ApplyShortcut("SaveProject", settings.KeyboardShortcuts, _window.saveProjectMenuItem);
        ApplyShortcut("SaveProjectAs", settings.KeyboardShortcuts, _window.saveProjectAsMenuItem);
        ApplyShortcut("Preferences", settings.KeyboardShortcuts, _window.preferencesMenuItem);
        ApplyShortcut("Exit", settings.KeyboardShortcuts, _window.exitMenuItem);
        ApplyShortcut("Undo", settings.KeyboardShortcuts, _window.undoMenuItem);
        ApplyShortcut("Redo", settings.KeyboardShortcuts, _window.redoMenuItem);
        ApplyShortcut("Find", settings.KeyboardShortcuts, _window.findMenuItem);
        ApplyShortcut("Cut", settings.KeyboardShortcuts, _window.cutMenuItem);
        ApplyShortcut("Copy", settings.KeyboardShortcuts, _window.copyMenuItem);
        ApplyShortcut("Paste", settings.KeyboardShortcuts, _window.pasteMenuItem);
        ApplyShortcut("ToggleViewMode", settings.KeyboardShortcuts, _window.toggleViewModeMenuItem);
        ApplyShortcut("Search", settings.KeyboardShortcuts, _window.searchMenuItem);
    }

    private void StoreOriginalHeaders()
    {
        // Store original header texts if not already stored
        var menuItems = new[]
        {
            (_window.newProjectMenuItem, "_New Project"),
            (_window.openProjectMenuItem, "_Open Project"),
            (_window.saveProjectMenuItem, "_Save Project"),
            (_window.saveProjectAsMenuItem, "Save Project _As"),
            (_window.preferencesMenuItem, "_Preferences"),
            (_window.exitMenuItem, "E_xit"),
            (_window.undoMenuItem, "_Undo"),
            (_window.redoMenuItem, "_Redo"),
            (_window.findMenuItem, "_Find"),
            (_window.cutMenuItem, "_Cut"),
            (_window.copyMenuItem, "_Copy"),
            (_window.pasteMenuItem, "_Paste"),
            (_window.toggleViewModeMenuItem, "_Toggle View Mode"),
            (_window.searchMenuItem, "_Search")
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
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(menuTextBlock, 0);
        grid.Children.Add(menuTextBlock);

        if (!string.IsNullOrEmpty(shortcutString))
        {
            var hotkeyTextBlock = new TextBlock
            {
                Text = FormatHotkeyForDisplay(shortcutString),
                VerticalAlignment = VerticalAlignment.Center,
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
}
