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
    private DocumentType? _filterDocumentType = null; // Filter by document type for metadata fields

    public void SetDocuments(List<Document> documents)
    {
        _allDocuments = documents ?? new List<Document>();
        // Don't update suggestions or show popup when documents are set
        // Only update when UpdateQuery is explicitly called (user typing)
        // Hide popup if it was visible
        if (IsVisible)
        {
            IsVisible = false;
            Suggestions.Clear();
        }
    }

    /// <summary>
    /// Sets the document type filter for metadata field autocomplete.
    /// </summary>
    public void SetDocumentTypeFilter(DocumentType? documentType)
    {
        _filterDocumentType = documentType;
    }

    public void UpdateQuery(string query)
    {
        var newQuery = query ?? string.Empty;
        
        // Only update if query actually changed (to avoid unnecessary updates)
        if (_currentQuery == newQuery)
            return;
            
        _currentQuery = newQuery;
        SelectedIndex = -1;
        UpdateSuggestions();
    }

    private void UpdateSuggestions()
    {
        Suggestions.Clear();

        // Filter documents by type if filter is set
        var documentsToSearch = _allDocuments;
        if (_filterDocumentType.HasValue)
        {
            documentsToSearch = _allDocuments
                .Where(d => d.Type == _filterDocumentType.Value)
                .ToList();
        }

        if (documentsToSearch.Count == 0)
        {
            IsVisible = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentQuery))
        {
            // Show all documents if no query (limit to 20)
            foreach (var doc in documentsToSearch.OrderBy(d => d.Title).Take(20))
            {
                Suggestions.Add(doc);
            }
        }
        else
        {
            // Filter documents by query (case-insensitive)
            var queryLower = _currentQuery.ToLowerInvariant();
            var query = _currentQuery;
            
            // Prioritize: exact matches, then starts with, then contains
            var exactMatches = documentsToSearch
                .Where(d => d.Title.Equals(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Title);
                
            var startsWithMatches = documentsToSearch
                .Where(d => !d.Title.Equals(query, StringComparison.OrdinalIgnoreCase) &&
                           d.Title.StartsWith(queryLower, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Title);
                
            var containsMatches = documentsToSearch
                .Where(d => !d.Title.Equals(query, StringComparison.OrdinalIgnoreCase) &&
                           !d.Title.StartsWith(queryLower, StringComparison.OrdinalIgnoreCase) &&
                           d.Title.Contains(queryLower, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Title);
            
            // Combine and limit to 20 suggestions
            var filtered = exactMatches
                .Concat(startsWithMatches)
                .Concat(containsMatches)
                .Take(20);

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
            var doc = Suggestions[SelectedIndex];
            return doc;
        }
        return null;
    }

    public void SelectNext()
    {
        if (Suggestions.Count > 0)
        {
            var oldIndex = SelectedIndex;
            SelectedIndex = (SelectedIndex + 1) % Suggestions.Count;
        }
    }

    public void SelectPrevious()
    {
        if (Suggestions.Count > 0)
        {
            var oldIndex = SelectedIndex;
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
