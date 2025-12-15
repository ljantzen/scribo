using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Scribo.ViewModels;

public partial class FindReplaceViewModel : ViewModelBase
{
    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private string replaceText = string.Empty;

    [ObservableProperty]
    private bool caseSensitive = false;

    [ObservableProperty]
    private bool wholeWordsOnly = false;

    [ObservableProperty]
    private bool isVisible = false;

    [ObservableProperty]
    private int currentMatchIndex = 0;

    [ObservableProperty]
    private int totalMatches = 0;

    [ObservableProperty]
    private string statusText = string.Empty;

    private List<MatchInfo> _matches = new();
    private string? _documentText;
    private int _currentCursorPosition = 0;

    public event Action<int, int>? SelectMatchRequested;
    public event Action<string>? ReplaceTextRequested;
    public event Func<int>? GetCurrentCursorPositionRequested;

    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            _matches.Clear();
            CurrentMatchIndex = 0;
            TotalMatches = 0;
            StatusText = string.Empty;
            SelectMatchRequested?.Invoke(0, 0);
        }
        else
        {
            // Get current cursor position before performing search
            _currentCursorPosition = GetCurrentCursorPositionRequested?.Invoke() ?? 0;
            PerformSearch();
        }
    }

    partial void OnCaseSensitiveChanged(bool value)
    {
        if (!string.IsNullOrEmpty(SearchText))
        {
            PerformSearch();
        }
    }

    partial void OnWholeWordsOnlyChanged(bool value)
    {
        if (!string.IsNullOrEmpty(SearchText))
        {
            PerformSearch();
        }
    }

    public void SetDocumentText(string? text)
    {
        _documentText = text;
        if (!string.IsNullOrEmpty(SearchText))
        {
            PerformSearch();
        }
    }

    [RelayCommand]
    private void FindNext()
    {
        if (_matches.Count == 0)
        {
            PerformSearch();
        }

        if (_matches.Count == 0)
            return;

        CurrentMatchIndex = (CurrentMatchIndex + 1) % _matches.Count;
        if (CurrentMatchIndex == 0 && _matches.Count > 1)
            CurrentMatchIndex = 1; // Skip 0, go to 1

        NavigateToCurrentMatch();
    }

    [RelayCommand]
    private void FindPrevious()
    {
        if (_matches.Count == 0)
        {
            PerformSearch();
        }

        if (_matches.Count == 0)
            return;

        CurrentMatchIndex = CurrentMatchIndex <= 1 ? _matches.Count : CurrentMatchIndex - 1;
        NavigateToCurrentMatch();
    }

    [RelayCommand]
    private void Replace()
    {
        if (_matches.Count == 0 || CurrentMatchIndex < 1 || CurrentMatchIndex > _matches.Count)
            return;

        var match = _matches[CurrentMatchIndex - 1];
        ReplaceTextRequested?.Invoke(ReplaceText);
        
        // After replace, search again
        PerformSearch();
    }

    [RelayCommand]
    private void ReplaceAll()
    {
        if (_matches.Count == 0 || string.IsNullOrEmpty(_documentText))
            return;

        var result = _documentText;
        var pattern = BuildSearchPattern();
        var options = CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;

        try
        {
            result = Regex.Replace(result, pattern, ReplaceText, options);
            ReplaceTextRequested?.Invoke(result);
            
            // Clear search after replace all
            SearchText = string.Empty;
        }
        catch
        {
            // If regex fails, do simple replace
            var comparison = CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            result = _documentText.Replace(SearchText, ReplaceText, comparison);
            ReplaceTextRequested?.Invoke(result);
            SearchText = string.Empty;
        }
    }

    [RelayCommand]
    private void Close()
    {
        IsVisible = false;
        SearchText = string.Empty;
        _matches.Clear();
        CurrentMatchIndex = 0;
        TotalMatches = 0;
        SelectMatchRequested?.Invoke(0, 0);
    }

    private void PerformSearch()
    {
        _matches.Clear();
        CurrentMatchIndex = 0;
        TotalMatches = 0;

        if (string.IsNullOrEmpty(SearchText) || string.IsNullOrEmpty(_documentText))
        {
            StatusText = string.Empty;
            SelectMatchRequested?.Invoke(0, 0);
            return;
        }

        var pattern = BuildSearchPattern();
        var options = CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;

        try
        {
            var regex = new Regex(pattern, options);
            var matches = regex.Matches(_documentText);

            foreach (Match match in matches)
            {
                _matches.Add(new MatchInfo
                {
                    Index = match.Index,
                    Length = match.Length
                });
            }

            TotalMatches = _matches.Count;
            
            if (_matches.Count > 0)
            {
                // Find the first match after the current cursor position
                var nextMatchIndex = _matches.FindIndex(m => m.Index >= _currentCursorPosition);
                if (nextMatchIndex >= 0)
                {
                    CurrentMatchIndex = nextMatchIndex + 1; // 1-based index
                }
                else
                {
                    // Wrap around to the first match
                    CurrentMatchIndex = 1;
                }
            }
            else
            {
                CurrentMatchIndex = 0;
            }

            UpdateStatusText();
            NavigateToCurrentMatch();
        }
        catch
        {
            // Fallback to simple string search
            var comparison = CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var index = 0;
            
            while ((index = _documentText.IndexOf(SearchText, index, comparison)) >= 0)
            {
                _matches.Add(new MatchInfo
                {
                    Index = index,
                    Length = SearchText.Length
                });
                index += SearchText.Length;
            }

            TotalMatches = _matches.Count;
            
            if (_matches.Count > 0)
            {
                // Find the first match after the current cursor position
                var nextMatchIndex = _matches.FindIndex(m => m.Index >= _currentCursorPosition);
                if (nextMatchIndex >= 0)
                {
                    CurrentMatchIndex = nextMatchIndex + 1; // 1-based index
                }
                else
                {
                    // Wrap around to the first match
                    CurrentMatchIndex = 1;
                }
            }
            else
            {
                CurrentMatchIndex = 0;
            }

            UpdateStatusText();
            NavigateToCurrentMatch();
        }
    }

    private string BuildSearchPattern()
    {
        var escaped = Regex.Escape(SearchText);
        if (WholeWordsOnly)
        {
            return $@"\b{escaped}\b";
        }
        return escaped;
    }

    private void NavigateToCurrentMatch()
    {
        if (_matches.Count == 0 || CurrentMatchIndex < 1 || CurrentMatchIndex > _matches.Count)
        {
            SelectMatchRequested?.Invoke(0, 0);
            return;
        }

        var match = _matches[CurrentMatchIndex - 1];
        _currentCursorPosition = match.Index + match.Length; // Update cursor position to end of match
        SelectMatchRequested?.Invoke(match.Index, match.Length);
        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        if (TotalMatches == 0)
        {
            StatusText = "No matches";
        }
        else
        {
            StatusText = $"{CurrentMatchIndex} of {TotalMatches}";
        }
    }

    private class MatchInfo
    {
        public int Index { get; set; }
        public int Length { get; set; }
    }
}
