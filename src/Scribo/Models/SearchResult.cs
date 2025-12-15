using System.Collections.Generic;

namespace Scribo.Models;

public class SearchResult
{
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentTitle { get; set; } = string.Empty;
    public DocumentType DocumentType { get; set; }
    public string ContentFilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// List of line numbers where matches were found (1-based)
    /// </summary>
    public List<int> MatchLines { get; set; } = new();
    
    /// <summary>
    /// List of match contexts (surrounding text for each match)
    /// </summary>
    public List<MatchContext> MatchContexts { get; set; } = new();
    
    /// <summary>
    /// Total number of matches in this document
    /// </summary>
    public int MatchCount { get; set; }
    
    /// <summary>
    /// Relevance score (higher is more relevant)
    /// </summary>
    public double RelevanceScore { get; set; }
}

public class MatchContext
{
    public int LineNumber { get; set; }
    public string Text { get; set; } = string.Empty;
    public int MatchStartIndex { get; set; }
    public int MatchLength { get; set; }
}
