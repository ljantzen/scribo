using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Scribo.Models;

namespace Scribo.Services;

/// <summary>
/// Efficient search index service capable of handling 100,000+ words.
/// Uses an inverted index for fast full-text search.
/// </summary>
public class SearchIndexService
{
    private readonly Dictionary<string, HashSet<string>> _invertedIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DocumentIndexEntry> _documentIndex = new();
    private readonly object _lockObject = new();
    
    /// <summary>
    /// Indexes all documents in a project
    /// </summary>
    public void IndexProject(Project project)
    {
        lock (_lockObject)
        {
            ClearIndex();
            
            if (project?.Documents == null)
                return;
            
            foreach (var document in project.Documents)
            {
                IndexDocument(document);
            }
        }
    }
    
    /// <summary>
    /// Indexes a single document
    /// </summary>
    public void IndexDocument(Document document)
    {
        if (document == null || string.IsNullOrEmpty(document.Id))
            return;
        
        lock (_lockObject)
        {
            // Remove old index entries for this document
            RemoveDocumentFromIndex(document.Id);
            
            var content = document.Content ?? string.Empty;
            var words = Tokenize(content);
            
            // Create document index entry
            var docEntry = new DocumentIndexEntry
            {
                DocumentId = document.Id,
                DocumentTitle = document.Title,
                DocumentType = document.Type,
                ContentFilePath = document.ContentFilePath,
                WordCount = words.Count,
                Lines = SplitIntoLines(content)
            };
            
            _documentIndex[document.Id] = docEntry;
            
            // Build inverted index
            var wordPositions = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            
            for (int i = 0; i < words.Count; i++)
            {
                var word = NormalizeWord(words[i]);
                if (string.IsNullOrEmpty(word))
                    continue;
                
                if (!wordPositions.ContainsKey(word))
                    wordPositions[word] = new List<int>();
                
                wordPositions[word].Add(i);
                
                // Add to inverted index
                if (!_invertedIndex.ContainsKey(word))
                    _invertedIndex[word] = new HashSet<string>();
                
                _invertedIndex[word].Add(document.Id);
            }
            
            docEntry.WordPositions = wordPositions;
        }
    }
    
    /// <summary>
    /// Removes a document from the index
    /// </summary>
    public void RemoveDocument(string documentId)
    {
        lock (_lockObject)
        {
            RemoveDocumentFromIndex(documentId);
        }
    }
    
    /// <summary>
    /// Updates the index for a document (removes old, adds new)
    /// </summary>
    public void UpdateDocument(Document document)
    {
        IndexDocument(document);
    }
    
    /// <summary>
    /// Searches for a query string across all indexed documents
    /// </summary>
    public List<SearchResult> Search(string query, SearchOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new List<SearchResult>();
        
        options ??= new SearchOptions();
        
        lock (_lockObject)
        {
            var queryWords = Tokenize(query);
            if (queryWords.Count == 0)
                return new List<SearchResult>();
            
            // Normalize query words
            var normalizedQueryWords = queryWords
                .Select(NormalizeWord)
                .Where(w => !string.IsNullOrEmpty(w))
                .ToList();
            
            if (normalizedQueryWords.Count == 0)
                return new List<SearchResult>();
            
            // Find documents containing all query words (AND search)
            HashSet<string>? candidateDocuments = null;
            
            foreach (var word in normalizedQueryWords)
            {
                if (_invertedIndex.TryGetValue(word, out var docIds))
                {
                    if (candidateDocuments == null)
                    {
                        candidateDocuments = new HashSet<string>(docIds);
                    }
                    else
                    {
                        // Intersect with existing candidates (AND operation)
                        candidateDocuments.IntersectWith(docIds);
                    }
                }
                else
                {
                    // Word not found in any document
                    return new List<SearchResult>();
                }
            }
            
            if (candidateDocuments == null || candidateDocuments.Count == 0)
                return new List<SearchResult>();
            
            // Build search results with match details
            var results = new List<SearchResult>();
            
            foreach (var docId in candidateDocuments)
            {
                if (!_documentIndex.TryGetValue(docId, out var docEntry))
                    continue;
                
                var result = BuildSearchResult(docEntry, normalizedQueryWords, query, options);
                if (result != null && result.MatchCount > 0)
                {
                    results.Add(result);
                }
            }
            
            // Sort by relevance score
            results.Sort((a, b) => b.RelevanceScore.CompareTo(a.RelevanceScore));
            
            // Apply result limit
            if (options.MaxResults > 0 && results.Count > options.MaxResults)
            {
                results = results.Take(options.MaxResults).ToList();
            }
            
            return results;
        }
    }
    
    /// <summary>
    /// Clears the entire index
    /// </summary>
    public void ClearIndex()
    {
        lock (_lockObject)
        {
            _invertedIndex.Clear();
            _documentIndex.Clear();
        }
    }
    
    /// <summary>
    /// Gets the total number of indexed words
    /// </summary>
    public int GetTotalWordCount()
    {
        lock (_lockObject)
        {
            return _documentIndex.Values.Sum(d => d.WordCount);
        }
    }
    
    /// <summary>
    /// Gets the number of indexed documents
    /// </summary>
    public int GetDocumentCount()
    {
        lock (_lockObject)
        {
            return _documentIndex.Count;
        }
    }
    
    private SearchResult? BuildSearchResult(DocumentIndexEntry docEntry, List<string> queryWords, string originalQuery, SearchOptions options)
    {
        var result = new SearchResult
        {
            DocumentId = docEntry.DocumentId,
            DocumentTitle = docEntry.DocumentTitle,
            DocumentType = docEntry.DocumentType,
            ContentFilePath = docEntry.ContentFilePath
        };
        
        // Find matches in the document content
        var content = string.Join("\n", docEntry.Lines);
        var matches = FindMatches(content, originalQuery, options);
        
        result.MatchCount = matches.Count;
        result.MatchLines = matches.Select(m => m.LineNumber).Distinct().OrderBy(l => l).ToList();
        
        // Build match contexts
        foreach (var match in matches.Take(options.MaxContextsPerResult))
        {
            result.MatchContexts.Add(match);
        }
        
        // Calculate relevance score
        result.RelevanceScore = CalculateRelevanceScore(docEntry, queryWords, matches);
        
        return result;
    }
    
    private List<MatchContext> FindMatches(string content, string query, SearchOptions options)
    {
        var matches = new List<MatchContext>();
        var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        
        var regexOptions = options.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
        var pattern = options.WholeWordsOnly 
            ? $@"\b{Regex.Escape(query)}\b" 
            : Regex.Escape(query);
        
        try
        {
            var regex = new Regex(pattern, regexOptions);
            
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                var lineMatches = regex.Matches(line);
                
                foreach (Match match in lineMatches)
                {
                    var context = new MatchContext
                    {
                        LineNumber = lineIndex + 1, // 1-based line numbers
                        Text = line,
                        MatchStartIndex = match.Index,
                        MatchLength = match.Length
                    };
                    
                    matches.Add(context);
                }
            }
        }
        catch
        {
            // If regex fails, fall back to simple string search
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                var comparison = options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                var index = line.IndexOf(query, comparison);
                
                if (index >= 0)
                {
                    var context = new MatchContext
                    {
                        LineNumber = lineIndex + 1,
                        Text = line,
                        MatchStartIndex = index,
                        MatchLength = query.Length
                    };
                    
                    matches.Add(context);
                }
            }
        }
        
        return matches;
    }
    
    private double CalculateRelevanceScore(DocumentIndexEntry docEntry, List<string> queryWords, List<MatchContext> matches)
    {
        if (matches.Count == 0)
            return 0;
        
        // Base score from match count
        double score = matches.Count * 10;
        
        // Boost for title matches
        var titleLower = docEntry.DocumentTitle.ToLowerInvariant();
        foreach (var word in queryWords)
        {
            if (titleLower.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                score += 50; // Significant boost for title matches
            }
        }
        
        // Boost for multiple matches in same line (phrase matches)
        var lineGroups = matches.GroupBy(m => m.LineNumber);
        foreach (var group in lineGroups)
        {
            if (group.Count() > 1)
            {
                score += group.Count() * 5; // Bonus for multiple matches per line
            }
        }
        
        // Normalize by document size (prefer shorter documents with matches)
        if (docEntry.WordCount > 0)
        {
            score *= (1000.0 / (1000.0 + docEntry.WordCount));
        }
        
        return score;
    }
    
    private List<string> Tokenize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new List<string>();
        
        // Split on whitespace and punctuation
        var words = Regex.Split(text, @"[\s\p{P}]+")
            .Where(w => !string.IsNullOrEmpty(w))
            .ToList();
        
        return words;
    }
    
    private string NormalizeWord(string word)
    {
        if (string.IsNullOrEmpty(word))
            return string.Empty;
        
        // Convert to lowercase and remove diacritics for better matching
        return word.ToLowerInvariant().Trim();
    }
    
    private List<string> SplitIntoLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new List<string>();
        
        return text.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None).ToList();
    }
    
    private void RemoveDocumentFromIndex(string documentId)
    {
        if (!_documentIndex.TryGetValue(documentId, out var docEntry))
            return;
        
        // Remove from inverted index
        foreach (var word in docEntry.WordPositions.Keys)
        {
            if (_invertedIndex.TryGetValue(word, out var docIds))
            {
                docIds.Remove(documentId);
                if (docIds.Count == 0)
                {
                    _invertedIndex.Remove(word);
                }
            }
        }
        
        // Remove from document index
        _documentIndex.Remove(documentId);
    }
    
    private class DocumentIndexEntry
    {
        public string DocumentId { get; set; } = string.Empty;
        public string DocumentTitle { get; set; } = string.Empty;
        public DocumentType DocumentType { get; set; }
        public string ContentFilePath { get; set; } = string.Empty;
        public int WordCount { get; set; }
        public List<string> Lines { get; set; } = new();
        public Dictionary<string, List<int>> WordPositions { get; set; } = new();
    }
}

public class SearchOptions
{
    public bool CaseSensitive { get; set; } = false;
    public bool WholeWordsOnly { get; set; } = false;
    public int MaxResults { get; set; } = 100;
    public int MaxContextsPerResult { get; set; } = 5;
}
