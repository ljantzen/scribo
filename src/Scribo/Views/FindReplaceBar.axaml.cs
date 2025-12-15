using Avalonia.Controls;
using Avalonia.Input;
using Scribo.ViewModels;

namespace Scribo.Views;

public partial class FindReplaceBar : UserControl
{
    public FindReplaceBar()
    {
        InitializeComponent();
    }

    private void OnFindKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is FindReplaceViewModel vm)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    vm.FindPreviousCommand.Execute(null);
                }
                else
                {
                    vm.FindNextCommand.Execute(null);
                }
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                vm.CloseCommand.Execute(null);
            }
            else if (e.Key == Key.F3)
            {
                e.Handled = true;
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                {
                    vm.FindPreviousCommand.Execute(null);
                }
                else
                {
                    vm.FindNextCommand.Execute(null);
                }
            }
        }
    }
}
