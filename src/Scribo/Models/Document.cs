using System;
using System.IO;
using System.Text.Json.Serialization;

namespace Scribo.Models;

public class Document
{
    private string? _content;

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    
    /// <summary>
    /// ID of the parent document. For scenes, this is the chapter ID they belong to.
    /// For documents in subfolders, this can be the folder node ID.
    /// </summary>
    public string? ParentId { get; set; }
    
    /// <summary>
    /// Folder path within the document type folder (e.g., "Main Characters" for characters in a subfolder).
    /// Empty string means the document is directly in the type folder.
    /// </summary>
    public string FolderPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Path to the markdown file containing the document content, relative to the project directory.
    /// </summary>
    public string ContentFilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Document content. When ContentFilePath is set, this will load from the file.
    /// Setting this property will store the value in memory but not persist it until SaveContent is called.
    /// </summary>
    [JsonIgnore]
    public string Content
    {
        get
        {
            Console.WriteLine($"[Document.Content GET] Title: {Title}, ContentFilePath: '{ContentFilePath}', ProjectDirectory: '{ProjectDirectory}'");
            
            // If content is already loaded in memory, check if we should reload
            // If cached content is empty and we have a valid file path, try to reload from file
            if (_content != null)
            {
                // If cached content is empty and we have a file path, try to reload
                if (_content.Length == 0 && !string.IsNullOrEmpty(ContentFilePath) && !string.IsNullOrEmpty(ProjectDirectory))
                {
                    var normalizedContentPath = ContentFilePath.Replace('/', Path.DirectorySeparatorChar);
                    var fullPath = Path.Combine(ProjectDirectory, normalizedContentPath);
                    Console.WriteLine($"[Document.Content GET] Cached content is empty, checking if file exists: '{fullPath}'");
                    
                    if (File.Exists(fullPath))
                    {
                        Console.WriteLine($"[Document.Content GET] File exists, clearing cache and reloading...");
                        _content = null; // Clear cache to force reload - this is safe as _content is nullable
                    }
                    else
                    {
                        Console.WriteLine($"[Document.Content GET] File does not exist, returning cached empty content");
                        return _content;
                    }
                }
                else
                {
                    Console.WriteLine($"[Document.Content GET] Returning cached content (length: {_content.Length})");
                    return _content;
                }
            }
            
            // If ContentFilePath is set, try to load from file
            if (!string.IsNullOrEmpty(ContentFilePath) && !string.IsNullOrEmpty(ProjectDirectory))
            {
                // Normalize path separators - ContentFilePath uses forward slashes, convert to platform-specific
                var normalizedContentPath = ContentFilePath.Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(ProjectDirectory, normalizedContentPath);
                Console.WriteLine($"[Document.Content GET] Attempting to load from file: '{fullPath}'");
                Console.WriteLine($"[Document.Content GET] File exists: {File.Exists(fullPath)}");
                
                if (File.Exists(fullPath))
                {
                    try
                    {
                        _content = File.ReadAllText(fullPath);
                        Console.WriteLine($"[Document.Content GET] Successfully loaded content (length: {_content.Length})");
                        return _content;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Document.Content GET] ERROR loading file: {ex.Message}");
                        Console.WriteLine($"[Document.Content GET] Stack trace: {ex.StackTrace}");
                        return string.Empty;
                    }
                }
                else
                {
                    Console.WriteLine($"[Document.Content GET] File does not exist at path: '{fullPath}'");
                }
            }
            else
            {
                Console.WriteLine($"[Document.Content GET] Missing ContentFilePath or ProjectDirectory - ContentFilePath: '{ContentFilePath}', ProjectDirectory: '{ProjectDirectory}'");
            }
            
            Console.WriteLine($"[Document.Content GET] Returning empty string");
            return string.Empty;
        }
        set => _content = value;
    }
    
    /// <summary>
    /// Project directory path. Set by ProjectService when loading/saving projects.
    /// Used to resolve relative ContentFilePath.
    /// </summary>
    [JsonIgnore]
    public string? ProjectDirectory { get; set; }
    
    public DocumentType Type { get; set; } = DocumentType.Chapter;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
    
    /// <summary>
    /// Order of the document within its parent folder. Used for sorting and reordering.
    /// Lower values appear first.
    /// </summary>
    public int Order { get; set; } = 0;
    
    public int WordCount => Content.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
    public int CharacterCount => Content.Length;
    public int CharacterCountNoSpaces => Content.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "").Length;
    
    /// <summary>
    /// Saves the current content to the markdown file specified by ContentFilePath.
    /// </summary>
    public void SaveContent()
    {
        if (string.IsNullOrEmpty(ContentFilePath) || string.IsNullOrEmpty(ProjectDirectory))
            return;
        
        // Normalize path separators - ContentFilePath uses forward slashes, convert to platform-specific
        var normalizedContentPath = ContentFilePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(ProjectDirectory, normalizedContentPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        // Use Content property getter to ensure we get the current content
        // This will return _content if set, or load from file if ContentFilePath is set
        // For new documents, _content should be set from the Content property setter
        var contentToSave = Content;
        
        File.WriteAllText(fullPath, contentToSave);
        ModifiedAt = DateTime.Now;
    }
}

public enum DocumentType
{
    Chapter,
    Scene,
    Note,
    Research,
    Character,
    Location,
    Other
}
