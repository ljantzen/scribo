using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Scribo.Models;
using Scribo.ViewModels;

namespace Scribo.Views;

public partial class SearchWindow : Window
{
    public SearchWindow()
    {
        InitializeComponent();
    }

    public SearchWindow(SearchViewModel viewModel) : this()
    {
        DataContext = viewModel;
        viewModel.NavigateToResultRequested += OnNavigateToResult;
        
        // Focus search box when window opens
        this.Opened += (s, e) =>
        {
            var searchBox = this.FindControl<TextBox>("searchTextBox");
            searchBox?.Focus();
            searchBox?.SelectAll();
        };
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is SearchViewModel vm)
        {
            e.Handled = true;
            vm.PerformSearchCommand.Execute(null);
        }
        else if (e.Key == Key.Escape)
        {
            Close();
        }
    }

    private void OnResultsListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is SearchViewModel vm && vm.SelectedResult != null)
        {
            e.Handled = true;
            vm.NavigateToResultCommand.Execute(vm.SelectedResult);
        }
    }

    private void OnResultsListBoxDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is ListBox listBox && DataContext is SearchViewModel vm)
        {
            // Try to find the ListBoxItem that was double-tapped
            var source = e.Source as Control;
            var listBoxItem = source?.FindAncestorOfType<ListBoxItem>();
            
            SearchResult? resultToNavigate = null;
            
            if (listBoxItem != null && listBoxItem.DataContext is SearchResult tappedResult)
            {
                // User double-tapped on a specific result
                resultToNavigate = tappedResult;
            }
            else if (vm.SelectedResult != null)
            {
                // Fall back to selected result
                resultToNavigate = vm.SelectedResult;
            }
            
            if (resultToNavigate != null)
            {
                vm.NavigateToResultCommand.Execute(resultToNavigate);
            }
        }
    }

    private void OnNavigateToResult(SearchResult result)
    {
        // Close the search window
        Close();
        
        // The result will be handled by MainWindow
        NavigateToResultRequested?.Invoke(result);
    }

    public event Action<SearchResult>? NavigateToResultRequested;
}
