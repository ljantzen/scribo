using System;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Scribo.ViewModels;

namespace Scribo.Views;

public partial class PreferencesWindow : Window
{
    public PreferencesWindow()
    {
        InitializeComponent();
    }

    public PreferencesWindow(PreferencesViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void OnOkClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is PreferencesViewModel vm)
        {
            vm.SaveCommand.Execute(null);
            Close();
        }
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnResetShortcutClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button button && 
            button.DataContext is KeyboardShortcutViewModel shortcutVm &&
            DataContext is PreferencesViewModel vm)
        {
            vm.ResetShortcutCommand.Execute(shortcutVm);
        }
    }

    private void OnShortcutGotFocus(object? sender, Avalonia.Input.GotFocusEventArgs e)
    {
        // Focus handler kept for potential future use
    }

    private void OnShortcutLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Lost focus handler kept for potential future use
    }

    private void OnShortcutKeyUp(object? sender, KeyEventArgs e)
    {
        // Not used, but kept for potential future use
    }

    private void OnShortcutKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || 
            textBox.DataContext is not KeyboardShortcutViewModel shortcutVm)
        {
            return;
        }

        // Don't capture modifier keys alone
        if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
            e.Key == Key.LeftShift || e.Key == Key.RightShift ||
            e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
            e.Key == Key.LWin || e.Key == Key.RWin)
        {
            return;
        }

        // Check if this looks like a key combination (has modifiers or is a function/special key)
        bool isKeyCombination = e.KeyModifiers != KeyModifiers.None || 
                                (e.Key >= Key.F1 && e.Key <= Key.F24) ||
                                IsSpecialKey(e.Key);

        // Always capture key combinations when pressed
        if (isKeyCombination)
        {
            e.Handled = true;

            // Build the shortcut string
            var shortcutBuilder = new StringBuilder();
            
            // Check modifiers
            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                shortcutBuilder.Append("Ctrl+");
            }
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                shortcutBuilder.Append("Shift+");
            }
            if (e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                shortcutBuilder.Append("Alt+");
            }
            if (e.KeyModifiers.HasFlag(KeyModifiers.Meta))
            {
                shortcutBuilder.Append("Meta+");
            }

            // Add the main key
            var keyString = ConvertKeyToString(e.Key);
            if (!string.IsNullOrEmpty(keyString))
            {
                shortcutBuilder.Append(keyString);
                var shortcut = shortcutBuilder.ToString();
                shortcutVm.Shortcut = shortcut;
                textBox.Text = shortcut;
                textBox.CaretIndex = shortcut.Length;
            }
        }
        // If it's not a key combination (normal typing like "F2"), allow normal input
    }

    private bool IsSpecialKey(Key key)
    {
        return key == Key.Enter || key == Key.Space || key == Key.Tab ||
               key == Key.Escape || key == Key.Back || key == Key.Delete ||
               key == Key.Insert || key == Key.Home || key == Key.End ||
               key == Key.PageUp || key == Key.PageDown ||
               key == Key.Up || key == Key.Down || key == Key.Left || key == Key.Right;
    }

    private string ConvertKeyToString(Key key)
    {
        // Handle function keys
        if (key >= Key.F1 && key <= Key.F24)
        {
            return key.ToString();
        }

        // Handle number keys
        if (key >= Key.D0 && key <= Key.D9)
        {
            return key.ToString().Substring(1); // Remove 'D' prefix
        }

        // Handle letter keys
        if (key >= Key.A && key <= Key.Z)
        {
            return key.ToString();
        }

        // Handle special keys
        return key switch
        {
            Key.Enter => "Enter",
            Key.Space => "Space",
            Key.Tab => "Tab",
            Key.Escape => "Escape",
            Key.Back => "Back",
            Key.Delete => "Delete",
            Key.Insert => "Insert",
            Key.Home => "Home",
            Key.End => "End",
            Key.PageUp => "PageUp",
            Key.PageDown => "PageDown",
            Key.Up => "Up",
            Key.Down => "Down",
            Key.Left => "Left",
            Key.Right => "Right",
            Key.OemComma => "Comma",
            Key.OemPeriod => "Period",
            Key.OemSemicolon => "Semicolon",
            Key.OemQuotes => "Quotes",
            Key.OemOpenBrackets => "OpenBrackets",
            Key.OemCloseBrackets => "CloseBrackets",
            Key.OemPipe => "Pipe",
            Key.OemTilde => "Tilde",
            Key.OemPlus => "Plus",
            Key.OemMinus => "Minus",
            Key.OemQuestion => "Question",
            _ => string.Empty
        };
    }
}
