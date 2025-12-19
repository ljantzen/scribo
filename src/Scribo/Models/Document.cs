using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Scribo.Services;

namespace Scribo.Models;

public class Document
{
    private string? _content;
    private string? _rawContent; // Stores raw content with frontmatter
    private Scribo.Services.FrontmatterService? _frontmatterService;

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

    // Frontmatter metadata fields
    /// <summary>
    /// Point of view character for this document. Should reference a Character document by name.
    /// </summary>
    [JsonIgnore]
    public string? Pov { get; set; }

    /// <summary>
    /// Focus or main subject of this document. Should reference a Character document by name.
    /// </summary>
    [JsonIgnore]
    public string? Focus { get; set; }

    /// <summary>
    /// List of characters mentioned or featured in this document. Should reference Character documents by name.
    /// </summary>
    [JsonIgnore]
    public List<string> Characters { get; set; } = new();

    /// <summary>
    /// Timeline reference for this document. Should reference a Timeline document by name.
    /// </summary>
    [JsonIgnore]
    public string? Timeline { get; set; }

    /// <summary>
    /// Plot thread or arc this document belongs to. Should reference a Plot document by name.
    /// </summary>
    [JsonIgnore]
    public string? Plot { get; set; }

    /// <summary>
    /// List of objects mentioned in this document. Should reference Object documents by name.
    /// </summary>
    [JsonIgnore]
    public List<string> Objects { get; set; } = new();

    /// <summary>
    /// List of entities (organizations, groups, etc.) mentioned in this document. Should reference Entity documents by name.
    /// </summary>
    [JsonIgnore]
    public List<string> Entities { get; set; } = new();

    /// <summary>
    /// Custom metadata fields (key-value pairs).
    /// </summary>
    [JsonIgnore]
    public Dictionary<string, string> Custom { get; set; } = new();

    /// <summary>
    /// List of documents or items mentioned in this document.
    /// </summary>
    [JsonIgnore]
    public List<string> Mentions { get; set; } = new();
    
    /// <summary>
    /// Document content. When ContentFilePath is set, this will load from the file.
    /// Setting this property will store the value in memory but not persist it until SaveContent is called.
    /// This property returns content without frontmatter. Frontmatter is parsed and stored in metadata properties.
    /// </summary>
    [JsonIgnore]
    public string Content
    {
        get
        {
            // If content is already loaded in memory, check if we should reload
            // If cached content is empty and we have a valid file path, try to reload from file
            if (_content != null)
            {
                // If cached content is empty and we have a file path, try to reload
                if (_content.Length == 0 && !string.IsNullOrEmpty(ContentFilePath) && !string.IsNullOrEmpty(ProjectDirectory))
                {
                    var normalizedContentPath = ContentFilePath.Replace('/', Path.DirectorySeparatorChar);
                    var fullPath = Path.Combine(ProjectDirectory, normalizedContentPath);
                    
                    if (File.Exists(fullPath))
                    {
                        _content = null; // Clear cache to force reload - this is safe as _content is nullable
                        _rawContent = null;
                    }
                    else
                    {
                        return _content;
                    }
                }
                else
                {
                    return _content;
                }
            }
            
            // If ContentFilePath is set, try to load from file
            if (!string.IsNullOrEmpty(ContentFilePath) && !string.IsNullOrEmpty(ProjectDirectory))
            {
                // Normalize path separators - ContentFilePath uses forward slashes, convert to platform-specific
                var normalizedContentPath = ContentFilePath.Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(ProjectDirectory, normalizedContentPath);
                
                if (File.Exists(fullPath))
                {
                    try
                    {
                        _rawContent = File.ReadAllText(fullPath);
                        return ParseContentFromRaw(_rawContent);
                    }
                    catch
                    {
                        return string.Empty;
                    }
                }
            }
            
            // If we get here, return empty string and ensure _content is set
            _content = string.Empty;
            return string.Empty;
        }
        set
        {
            _content = value ?? string.Empty; // Ensure _content is never null
            _rawContent = null; // Clear raw content when setting content directly
        }
    }

    /// <summary>
    /// Parses raw content (with frontmatter) and extracts both content and metadata.
    /// </summary>
    private string ParseContentFromRaw(string rawContent)
    {
        _frontmatterService ??= new Scribo.Services.FrontmatterService();

        var (frontmatter, content) = _frontmatterService.ParseFrontmatter(rawContent);
        
        if (frontmatter != null)
        {
            // Extract metadata from frontmatter
            var name = _frontmatterService.GetValue<string>(frontmatter, "name");
            if (!string.IsNullOrEmpty(name) && string.IsNullOrEmpty(Title))
            {
                Title = name;
            }

            var created = _frontmatterService.GetValue<DateTime?>(frontmatter, "created");
            if (created.HasValue && CreatedAt == default)
            {
                CreatedAt = created.Value;
            }

            var updated = _frontmatterService.GetValue<DateTime?>(frontmatter, "updated");
            if (updated.HasValue)
            {
                ModifiedAt = updated.Value;
            }

            // Parse metadata fields, extracting document names from [[DocumentLink]] syntax
            Pov = ExtractDocumentNameFromLink(_frontmatterService.GetValue<string>(frontmatter, "pov"));
            Focus = ExtractDocumentNameFromLink(_frontmatterService.GetValue<string>(frontmatter, "focus"));
            Timeline = ExtractDocumentNameFromLink(_frontmatterService.GetValue<string>(frontmatter, "timeline"));
            Plot = ExtractDocumentNameFromLink(_frontmatterService.GetValue<string>(frontmatter, "plot"));

            var characters = _frontmatterService.GetValue<List<string>>(frontmatter, "characters");
            if (characters != null)
            {
                Characters = characters.Select(ExtractDocumentNameFromLink).ToList();
            }

            var objects = _frontmatterService.GetValue<List<string>>(frontmatter, "objects");
            if (objects != null)
            {
                Objects = objects.Select(ExtractDocumentNameFromLink).ToList();
            }

            var entities = _frontmatterService.GetValue<List<string>>(frontmatter, "entities");
            if (entities != null)
            {
                Entities = entities.Select(ExtractDocumentNameFromLink).ToList();
            }

            var mentions = _frontmatterService.GetValue<List<string>>(frontmatter, "mentions");
            if (mentions != null)
            {
                Mentions = mentions;
            }

            // Handle custom fields - they can be a dictionary or list
            if (frontmatter.ContainsKey("custom"))
            {
                var customValue = frontmatter["custom"];
                if (customValue is Dictionary<object, object> customDict)
                {
                    Custom = customDict.ToDictionary(
                        kvp => kvp.Key?.ToString() ?? string.Empty,
                        kvp => kvp.Value?.ToString() ?? string.Empty
                    );
                }
            }
        }

        _content = content;
        return content;
    }
    
    /// <summary>
    /// Project directory path. Set by ProjectService when loading/saving projects.
    /// Used to resolve relative ContentFilePath.
    /// </summary>
    [JsonIgnore]
    public string? ProjectDirectory { get; set; }

    /// <summary>
    /// Gets the raw content including frontmatter for editing.
    /// This is what should be displayed in the editor.
    /// </summary>
    [JsonIgnore]
    public string RawContent
    {
        get
        {
            // If we have raw content cached, return it
            if (_rawContent != null)
                return _rawContent;

            // If ContentFilePath is set, try to load raw content from file
            if (!string.IsNullOrEmpty(ContentFilePath) && !string.IsNullOrEmpty(ProjectDirectory))
            {
                var normalizedContentPath = ContentFilePath.Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(ProjectDirectory, normalizedContentPath);
                
                if (File.Exists(fullPath))
                {
                    try
                    {
                        _rawContent = File.ReadAllText(fullPath);
                        return _rawContent;
                    }
                    catch
                    {
                        // Fall through to generate from current state
                    }
                }
            }

            // Generate raw content from current content and metadata
            return GenerateRawContent();
        }
        set
        {
            _rawContent = value;
            // Parse frontmatter and update metadata
            ParseAndUpdateFromRawContent(value);
        }
    }

    /// <summary>
    /// Generates raw content (with frontmatter) from current content and metadata.
    /// </summary>
    private string GenerateRawContent()
    {
        var content = _content ?? string.Empty;
        var frontmatter = BuildFrontmatter();
        
        if (frontmatter != null && frontmatter.Count > 0)
        {
            _frontmatterService ??= new Scribo.Services.FrontmatterService();
            return _frontmatterService.WriteFrontmatter(frontmatter, content);
        }
        
        return content;
    }

    /// <summary>
    /// Parses raw content (with frontmatter) and updates document metadata and content.
    /// </summary>
    private void ParseAndUpdateFromRawContent(string rawContent)
    {
        _frontmatterService ??= new Scribo.Services.FrontmatterService();
        var (frontmatter, content) = _frontmatterService.ParseFrontmatter(rawContent);
        
        // Update content
        _content = content;
        
        // Update metadata from frontmatter if present
        if (frontmatter != null)
        {
            var name = _frontmatterService.GetValue<string>(frontmatter, "name");
            if (!string.IsNullOrEmpty(name))
            {
                Title = name;
            }

            var created = _frontmatterService.GetValue<DateTime?>(frontmatter, "created");
            if (created.HasValue)
            {
                CreatedAt = created.Value;
            }

            var updated = _frontmatterService.GetValue<DateTime?>(frontmatter, "updated");
            if (updated.HasValue)
            {
                ModifiedAt = updated.Value;
            }

            // Parse metadata fields, extracting document names from [[DocumentLink]] syntax
            Pov = ExtractDocumentNameFromLink(_frontmatterService.GetValue<string>(frontmatter, "pov"));
            Focus = ExtractDocumentNameFromLink(_frontmatterService.GetValue<string>(frontmatter, "focus"));
            Timeline = ExtractDocumentNameFromLink(_frontmatterService.GetValue<string>(frontmatter, "timeline"));
            Plot = ExtractDocumentNameFromLink(_frontmatterService.GetValue<string>(frontmatter, "plot"));

            var characters = _frontmatterService.GetValue<List<string>>(frontmatter, "characters");
            if (characters != null)
            {
                Characters = characters.Select(ExtractDocumentNameFromLink).ToList();
            }

            var objects = _frontmatterService.GetValue<List<string>>(frontmatter, "objects");
            if (objects != null)
            {
                Objects = objects.Select(ExtractDocumentNameFromLink).ToList();
            }

            var entities = _frontmatterService.GetValue<List<string>>(frontmatter, "entities");
            if (entities != null)
            {
                Entities = entities.Select(ExtractDocumentNameFromLink).ToList();
            }

            var mentions = _frontmatterService.GetValue<List<string>>(frontmatter, "mentions");
            if (mentions != null)
            {
                Mentions = mentions;
            }

            // Handle custom fields
            if (frontmatter.ContainsKey("custom"))
            {
                var customValue = frontmatter["custom"];
                if (customValue is Dictionary<object, object> customDict)
                {
                    Custom = customDict.ToDictionary(
                        kvp => kvp.Key?.ToString() ?? string.Empty,
                        kvp => kvp.Value?.ToString() ?? string.Empty
                    );
                }
            }
        }
    }

    /// <summary>
    /// Updates the "updated" timestamp in raw content frontmatter.
    /// </summary>
    private string UpdateTimestampInRawContent(string rawContent)
    {
        _frontmatterService ??= new Scribo.Services.FrontmatterService();
        var (frontmatter, content) = _frontmatterService.ParseFrontmatter(rawContent);
        
        if (frontmatter != null && frontmatter.Count > 0)
        {
            // Update the "updated" timestamp
            frontmatter["updated"] = ModifiedAt.ToString("yyyy-MM-dd HH:mm");
            return _frontmatterService.WriteFrontmatter(frontmatter, content);
        }
        
        // No frontmatter, just return as-is (shouldn't happen if we're saving)
        return rawContent;
    }
    
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
    /// Includes frontmatter metadata if any metadata fields are set.
    /// </summary>
    public void SaveContent()
    {
        if (string.IsNullOrEmpty(ContentFilePath) || string.IsNullOrEmpty(ProjectDirectory))
        {
            Console.WriteLine($"[Document.SaveContent] Skipping save - ContentFilePath: '{ContentFilePath}', ProjectDirectory: '{ProjectDirectory}'");
            return;
        }
        
        Console.WriteLine($"[Document.SaveContent] Saving document '{Title}' (Type: {Type})");
        Console.WriteLine($"  ContentFilePath: {ContentFilePath}");
        Console.WriteLine($"  ProjectDirectory: {ProjectDirectory}");
        
        // Normalize path separators - ContentFilePath uses forward slashes, convert to platform-specific
        var normalizedContentPath = ContentFilePath.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(ProjectDirectory, normalizedContentPath);
        Console.WriteLine($"[Document.SaveContent] Full path: {fullPath}");
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Console.WriteLine($"[Document.SaveContent] Creating directory: {directory}");
            Directory.CreateDirectory(directory);
        }
        
        // Update ModifiedAt before saving (will be reflected in frontmatter if user edits it)
        ModifiedAt = DateTime.Now;
        
        // Use raw content if available (includes user-edited frontmatter), otherwise generate it
        string fileContent;
        if (_rawContent != null)
        {
            // User has edited the raw content (including frontmatter), use it directly
            // But update the "updated" timestamp in the frontmatter
            fileContent = UpdateTimestampInRawContent(_rawContent);
        }
        else
        {
            // Generate raw content from current content and metadata
            fileContent = GenerateRawContent();
        }
        
        Console.WriteLine($"[Document.SaveContent] Writing {fileContent.Length} characters to file");
        File.WriteAllText(fullPath, fileContent);
        Console.WriteLine($"[Document.SaveContent] Successfully wrote file: {fullPath}");
    }

    /// <summary>
    /// Builds frontmatter dictionary from document metadata.
    /// </summary>
    private Dictionary<string, object>? BuildFrontmatter()
    {
        var frontmatter = new Dictionary<string, object>();

        // Standard metadata
        frontmatter["name"] = Title;
        // Format timestamps as simple date strings (YYYY-MM-DD HH:mm)
        frontmatter["created"] = CreatedAt.ToString("yyyy-MM-dd HH:mm");
        frontmatter["updated"] = ModifiedAt.ToString("yyyy-MM-dd HH:mm");

        // Optional metadata fields (only include if they have values)
        // Convert document names to [[DocumentLink]] syntax for better UX
        if (!string.IsNullOrEmpty(Pov))
            frontmatter["pov"] = FormatDocumentNameAsLink(Pov);

        if (!string.IsNullOrEmpty(Focus))
            frontmatter["focus"] = FormatDocumentNameAsLink(Focus);

        if (!string.IsNullOrEmpty(Timeline))
            frontmatter["timeline"] = FormatDocumentNameAsLink(Timeline);

        if (!string.IsNullOrEmpty(Plot))
            frontmatter["plot"] = FormatDocumentNameAsLink(Plot);

        if (Characters != null && Characters.Count > 0)
            frontmatter["characters"] = Characters.Select(FormatDocumentNameAsLink).ToList();

        if (Objects != null && Objects.Count > 0)
            frontmatter["objects"] = Objects.Select(FormatDocumentNameAsLink).ToList();

        if (Entities != null && Entities.Count > 0)
            frontmatter["entities"] = Entities.Select(FormatDocumentNameAsLink).ToList();

        if (Mentions != null && Mentions.Count > 0)
            frontmatter["mentions"] = Mentions;

        if (Custom != null && Custom.Count > 0)
            frontmatter["custom"] = Custom;

        return frontmatter;
    }

    /// <summary>
    /// Extracts document name from [[DocumentLink]] syntax or returns the string as-is.
    /// </summary>
    private string ExtractDocumentNameFromLink(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value ?? string.Empty;

        // Check if it's in [[DocumentLink]] format
        if (value.StartsWith("[[") && value.EndsWith("]]"))
        {
            // Extract the document name from [[DocumentName]]
            var name = value.Substring(2, value.Length - 4).Trim();
            // Handle [[DocumentName|Display Text]] format - take the first part
            var pipeIndex = name.IndexOf('|');
            if (pipeIndex >= 0)
            {
                name = name.Substring(0, pipeIndex).Trim();
            }
            return name;
        }

        // Not in link format, return as-is
        return value;
    }

    /// <summary>
    /// Formats a document name as [[DocumentLink]] syntax.
    /// </summary>
    private string FormatDocumentNameAsLink(string? documentName)
    {
        if (string.IsNullOrWhiteSpace(documentName))
            return documentName ?? string.Empty;

        // If it's already in [[...]] format, return as-is
        if (documentName.StartsWith("[[") && documentName.EndsWith("]]"))
            return documentName;

        // Convert to [[DocumentName]] format
        return $"[[" + documentName + "]]";
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
    Timeline,
    Plot,
    Object,
    Entity,
    Other
}
