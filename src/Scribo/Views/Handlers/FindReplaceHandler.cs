using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Scribo.ViewModels;

namespace Scribo.Views.Handlers;

public class FindReplaceHandler
{
    private readonly MainWindow _window;
    private bool _isSelectingMatch = false;

    public FindReplaceHandler(MainWindow window)
    {
        _window = window;
    }

    public void Setup()
    {
        var textBox = _window.FindControl<TextBox>("sourceTextBox");
        if (textBox != null)
        {
            textBox.GotFocus += OnEditorTextBoxGotFocus;
        }

        if (_window.DataContext is MainWindowViewModel vm)
        {
            vm.SelectMatchRequested += OnSelectMatch;
        }
    }

    private void OnSelectMatch(int index, int length)
    {
        var textBox = _window.FindControl<TextBox>("sourceTextBox");
        var findBar = _window.FindControl<FindReplaceBar>("findReplaceBar");
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

    private void OnEditorTextBoxGotFocus(object? sender, GotFocusEventArgs e)
    {
        // If we're selecting a match and find bar is visible, prevent editor from getting focus
        if (_isSelectingMatch)
        {
            var findBar = _window.FindControl<FindReplaceBar>("findReplaceBar");
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
}
