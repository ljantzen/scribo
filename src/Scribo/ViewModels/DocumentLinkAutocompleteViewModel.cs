using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Scribo.Models;

namespace Scribo.ViewModels;

public partial class DocumentLinkAutocompleteViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<Document> suggestions = new();

    [ObservableProperty]
    private int selectedIndex = -1;

    [ObservableProperty]
    private bool isVisible;

    private List<Document> _allDocuments = new();
    private string _currentQuery = string.Empty;

    public void SetDocuments(List<Document> documents)
    {
        _allDocuments = documents ?? new List<Document>();
        UpdateSuggestions();
    }

    public void UpdateQuery(string query)
    {
        _currentQuery = query ?? string.Empty;
        SelectedIndex = -1;
        UpdateSuggestions();
    }

    private void UpdateSuggestions()
    {
        Suggestions.Clear();

        if (string.IsNullOrWhiteSpace(_currentQuery))
        {
            // Show all documents if no query
            foreach (var doc in _allDocuments.OrderBy(d => d.Title))
            {
                Suggestions.Add(doc);
            }
        }
        else
        {
            // Filter documents by query (case-insensitive)
            var queryLower = _currentQuery.ToLowerInvariant();
            var filtered = _allDocuments
                .Where(d => d.Title.Contains(queryLower, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Title)
                .Take(20); // Limit to 20 suggestions

            foreach (var doc in filtered)
            {
                Suggestions.Add(doc);
            }
        }

        IsVisible = Suggestions.Count > 0;
    }

    public Document? GetSelectedDocument()
    {
        if (SelectedIndex >= 0 && SelectedIndex < Suggestions.Count)
        {
            return Suggestions[SelectedIndex];
        }
        return null;
    }

    public void SelectNext()
    {
        if (Suggestions.Count > 0)
        {
            SelectedIndex = (SelectedIndex + 1) % Suggestions.Count;
        }
    }

    public void SelectPrevious()
    {
        if (Suggestions.Count > 0)
        {
            SelectedIndex = SelectedIndex <= 0 ? Suggestions.Count - 1 : SelectedIndex - 1;
        }
    }

    public void Hide()
    {
        IsVisible = false;
        SelectedIndex = -1;
        _currentQuery = string.Empty;
    }
}
