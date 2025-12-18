using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scribo.Models;
using Scribo.Services;

namespace Scribo.ViewModels;

public partial class SearchViewModel : ViewModelBase
{
    private readonly SearchIndexService _searchIndexService;
    private readonly ProjectService _projectService;
    private Project? _currentProject;

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private ObservableCollection<SearchResult> searchResults = new();

    [ObservableProperty]
    private SearchResult? selectedResult;

    [ObservableProperty]
    private bool isSearching = false;

    [ObservableProperty]
    private int resultCount = 0;

    [ObservableProperty]
    private bool caseSensitive = false;

    [ObservableProperty]
    private bool wholeWordsOnly = false;

    [ObservableProperty]
    private string statusText = "Ready";

    public SearchViewModel(SearchIndexService? searchIndexService = null, ProjectService? projectService = null)
    {
        _searchIndexService = searchIndexService ?? new SearchIndexService();
        _projectService = projectService ?? new ProjectService();
    }

    public void SetProject(Project? project)
    {
        _currentProject = project;
        if (project != null)
        {
            _searchIndexService.IndexProject(project);
            UpdateStatusText();
        }
        else
        {
            _searchIndexService.ClearIndex();
            SearchResults.Clear();
            StatusText = "No project loaded";
        }
    }

    public void UpdateDocument(Document document)
    {
        _searchIndexService.UpdateDocument(document);
    }

    partial void OnSearchQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults.Clear();
            ResultCount = 0;
            StatusText = "Ready";
        }
        else
        {
            PerformSearch();
        }
    }

    partial void OnCaseSensitiveChanged(bool value)
    {
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            PerformSearch();
        }
    }

    partial void OnWholeWordsOnlyChanged(bool value)
    {
        if (!string.IsNullOrWhiteSpace(SearchQuery))
        {
            PerformSearch();
        }
    }

    [RelayCommand]
    private void PerformSearch()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery) || _currentProject == null)
        {
            SearchResults.Clear();
            ResultCount = 0;
            StatusText = "Ready";
            return;
        }

        IsSearching = true;
        StatusText = "Searching...";

        try
        {
            var options = new SearchOptions
            {
                CaseSensitive = CaseSensitive,
                WholeWordsOnly = WholeWordsOnly,
                MaxResults = 500,
                MaxContextsPerResult = 5
            };

            var results = _searchIndexService.Search(SearchQuery, options);
            
            SearchResults.Clear();
            foreach (var result in results)
            {
                SearchResults.Add(result);
            }

            ResultCount = results.Sum(r => r.MatchCount);
            StatusText = $"Found {results.Count} document(s) with {ResultCount} match(es)";
        }
        catch (Exception ex)
        {
            StatusText = $"Search error: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    [RelayCommand]
    private void NavigateToResult(SearchResult? result)
    {
        if (result == null)
            result = SelectedResult;

        if (result == null || _currentProject == null)
            return;

        // This will be handled by the window to navigate to the document
        NavigateToResultRequested?.Invoke(result);
    }

    public event Action<SearchResult>? NavigateToResultRequested;

    private void UpdateStatusText()
    {
        var docCount = _searchIndexService.GetDocumentCount();
        var wordCount = _searchIndexService.GetTotalWordCount();
        StatusText = $"Indexed {docCount} document(s), {wordCount:N0} words";
    }
}
