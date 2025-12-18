using Scribo.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Scribo.Services;

public class ProjectService
{
    private const string JsonFileExtension = ".json";

    public void SaveProject(Project project, string filePath)
    {
        if (project == null)
            throw new ArgumentNullException(nameof(project));

        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        // Ensure .json extension
        if (!filePath.EndsWith(JsonFileExtension, StringComparison.OrdinalIgnoreCase))
        {
            filePath = Path.ChangeExtension(filePath, JsonFileExtension);
        }

        project.FilePath = filePath;
        project.ModifiedAt = DateTime.Now;
        
        var projectDirectory = Path.GetDirectoryName(filePath) ?? Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? string.Empty;
        
        // Build a dictionary of documents by ID for quick lookup
        var documentsById = project.Documents.ToDictionary(d => d.Id);
        
        // Save document content to markdown files
        foreach (var document in project.Documents)
        {
            document.ProjectDirectory = projectDirectory;
            
            // Store old file path and content file path before potentially regenerating
            string? oldContentFilePath = document.ContentFilePath;
            string? oldFullPath = null;
            if (!string.IsNullOrEmpty(oldContentFilePath))
            {
                // Normalize path separators - ContentFilePath uses forward slashes
                var normalizedOldPath = oldContentFilePath.Replace('/', Path.DirectorySeparatorChar);
                oldFullPath = Path.Combine(projectDirectory, normalizedOldPath);
                // Only track if file actually exists
                if (!File.Exists(oldFullPath))
                {
                    oldFullPath = null;
                }
            }
            
            // Generate what the new content file path should be based on current document properties
            string expectedContentFilePath = GenerateContentFilePath(document, documentsById);
            
            // Generate content file path if not set (this happens when ContentFilePath was cleared after moving)
            string newContentFilePath = oldContentFilePath ?? string.Empty;
            if (string.IsNullOrEmpty(newContentFilePath))
            {
                newContentFilePath = expectedContentFilePath;
            }
            
            // Check if the file path has changed by comparing current path with expected path
            bool pathChanged = !string.IsNullOrEmpty(oldContentFilePath) && 
                              !string.IsNullOrEmpty(expectedContentFilePath) &&
                              oldContentFilePath != expectedContentFilePath;
            
            // If path changed, use the expected path
            if (pathChanged)
            {
                newContentFilePath = expectedContentFilePath;
            }
            
            // If path changed and old file exists, we need to move the file
            if (pathChanged && oldFullPath != null)
            {
                // Normalize path separators - ContentFilePath uses forward slashes
                var normalizedNewPath = newContentFilePath.Replace('/', Path.DirectorySeparatorChar);
                var newFullPath = Path.Combine(projectDirectory, normalizedNewPath);
                var newDirectory = Path.GetDirectoryName(newFullPath);
                
                // Ensure target directory exists
                if (!string.IsNullOrEmpty(newDirectory) && !Directory.Exists(newDirectory))
                {
                    Directory.CreateDirectory(newDirectory);
                }
                
                // Move the file if it exists and the new path is different
                if (File.Exists(oldFullPath) && oldFullPath != newFullPath)
                {
                    try
                    {
                        // Ensure content is loaded before moving
                        var contentToMove = document.Content;
                        
                        // Write to new location
                        File.WriteAllText(newFullPath, contentToMove);
                        
                        // Delete old file
                        File.Delete(oldFullPath);
                        
                        // Update ContentFilePath
                        document.ContentFilePath = newContentFilePath;
                        
                        // Content is already saved, skip normal save
                        continue;
                    }
                    catch (Exception ex)
                    {
                        // Fall through to normal save with new path
                    }
                }
            }
            
            // Normal save path (either no path change, or path change but file didn't exist, or move failed)
            document.ContentFilePath = newContentFilePath;
            
            // Ensure content is loaded into memory before saving
            // Access the Content property to trigger loading if needed
            var contentToSave = document.Content;
            
            // Save content to markdown file
            document.SaveContent();
        }
        
        // Update metadata
        if (project.Metadata == null)
        {
            project.Metadata = new ProjectMetadata();
        }
        
        // Ensure Statistics object exists
        if (project.Metadata.Statistics == null)
        {
            project.Metadata.Statistics = new ProjectStatistics();
        }
        
        project.Metadata.Title = project.Name;
        project.Metadata.ModifiedAt = DateTime.Now;
        project.Metadata.LastOpenedAt = DateTime.Now;

        // Ensure all documents have ProjectDirectory set before calculating statistics
        // This ensures Content property can load from files if needed
        foreach (var document in project.Documents)
        {
            if (string.IsNullOrEmpty(document.ProjectDirectory))
            {
                document.ProjectDirectory = projectDirectory;
            }
        }

        // Calculate statistics (content will be loaded from files via Content property)
        UpdateProjectStatistics(project);

        var json = JsonSerializer.Serialize(project, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        });
        
        File.WriteAllText(filePath, json);
    }

    public Project LoadProject(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Project file not found", filePath);

        var json = File.ReadAllText(filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        
        var project = JsonSerializer.Deserialize<Project>(json, options);

        if (project == null)
            throw new InvalidOperationException("Failed to deserialize project");

        var projectDirectory = Path.GetDirectoryName(filePath) ?? Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? string.Empty;
        
        // Set project directory on all documents so they can load content from files
        foreach (var document in project.Documents)
        {
            document.ProjectDirectory = projectDirectory;
            
            // Check if ContentFilePath points to a non-existent file but file exists in Trashcan
            // This handles cases where ContentFilePath was lost or incorrectly set
            if (!string.IsNullOrEmpty(document.ContentFilePath))
            {
                var normalizedContentPath = document.ContentFilePath.Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(projectDirectory, normalizedContentPath);
                
                // If file doesn't exist at the expected path, check if it's in Trashcan
                if (!File.Exists(fullPath) && !document.ContentFilePath.StartsWith("Trashcan/", StringComparison.OrdinalIgnoreCase))
                {
                    var trashcanDir = Path.Combine(projectDirectory, "Trashcan");
                    if (Directory.Exists(trashcanDir))
                    {
                        // Search for files matching this document's title in Trashcan
                        var sanitizedTitle = SanitizeFileName(document.Title);
                        var searchPattern = $"{sanitizedTitle}.md";
                        var foundFiles = Directory.GetFiles(trashcanDir, searchPattern, SearchOption.AllDirectories);
                        
                        if (foundFiles.Length > 0)
                        {
                            // Found file in Trashcan, update ContentFilePath
                            var trashcanFile = foundFiles[0];
                            var relativePath = Path.GetRelativePath(projectDirectory, trashcanFile);
                            // Normalize to forward slashes for ContentFilePath
                            var newTrashcanPath = relativePath.Replace(Path.DirectorySeparatorChar, '/');
                            Console.WriteLine($"[LoadProject] Document '{document.Title}' file not found at '{document.ContentFilePath}', found in Trashcan, updating to: '{newTrashcanPath}'");
                            document.ContentFilePath = newTrashcanPath;
                        }
                    }
                }
            }
            
            // Migrate old projects: if ContentFilePath is empty but Content exists in JSON (backward compatibility)
            // This shouldn't happen with JsonIgnore, but handle it just in case
            // IMPORTANT: Don't regenerate ContentFilePath if document was in Trashcan - preserve Trashcan path
            if (string.IsNullOrEmpty(document.ContentFilePath) && !string.IsNullOrEmpty(document.Content))
            {
                // Check if file exists in Trashcan - if so, preserve that path
                // Search for the file in Trashcan directory structure
                var trashcanDir = Path.Combine(projectDirectory, "Trashcan");
                
                if (Directory.Exists(trashcanDir))
                {
                    // Search for files matching this document's title in Trashcan
                    var sanitizedTitle = SanitizeFileName(document.Title);
                    var searchPattern = $"{sanitizedTitle}.md";
                    var foundFiles = Directory.GetFiles(trashcanDir, searchPattern, SearchOption.AllDirectories);
                    
                    if (foundFiles.Length > 0)
                    {
                        // Found file in Trashcan, calculate relative path
                        var trashcanFile = foundFiles[0];
                        var relativePath = Path.GetRelativePath(projectDirectory, trashcanFile);
                        // Normalize to forward slashes for ContentFilePath
                        document.ContentFilePath = relativePath.Replace(Path.DirectorySeparatorChar, '/');
                        Console.WriteLine($"[LoadProject] Found document '{document.Title}' in Trashcan, preserving path: '{document.ContentFilePath}'");
                    }
                    else
                    {
                        // Build a dictionary of documents by ID for quick lookup
                        var documentsById = project.Documents.ToDictionary(d => d.Id);
                        // Generate content file path using the new structure
                        document.ContentFilePath = GenerateContentFilePath(document, documentsById);
                        // Save the content to the file
                        document.SaveContent();
                    }
                }
                else
                {
                    // Build a dictionary of documents by ID for quick lookup
                    var documentsById = project.Documents.ToDictionary(d => d.Id);
                    // Generate content file path using the new structure
                    document.ContentFilePath = GenerateContentFilePath(document, documentsById);
                    // Save the content to the file
                    document.SaveContent();
                }
            }
        }

        // Ensure metadata exists (for backward compatibility)
        if (project.Metadata == null)
        {
            project.Metadata = new ProjectMetadata
            {
                Title = project.Name,
                CreatedAt = project.CreatedAt,
                ModifiedAt = project.ModifiedAt
            };
        }

        // Update last opened time
        project.Metadata.LastOpenedAt = DateTime.Now;
        project.FilePath = filePath;

        return project;
    }

    public Project CreateNewProject(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name cannot be null or empty", nameof(name));

        var now = DateTime.Now;
        var project = new Project
        {
            Name = name,
            CreatedAt = now,
            ModifiedAt = now,
            Metadata = new ProjectMetadata
            {
                Title = name,
                CreatedAt = now,
                ModifiedAt = now
            }
        };
        
        // Add sample documents for dummy project
        AddSampleDocuments(project);
        
        return project;
    }

    private void AddSampleDocuments(Project project)
    {
        var now = DateTime.Now;
        
        // Add a sample chapter
        var chapter1 = new Document
        {
            Title = "Chapter 1",
            Type = DocumentType.Chapter,
            Content = "# Chapter 1\n\nThis is the beginning of your story.\n\nStart writing here...",
            CreatedAt = now,
            ModifiedAt = now,
            Order = 0
        };
        project.Documents.Add(chapter1);
        
        // Add a sample scene to the chapter
        var scene1 = new Document
        {
            Title = "Scene 1",
            Type = DocumentType.Scene,
            ParentId = chapter1.Id,
            Content = "# Scene 1\n\nDescribe what happens in this scene.\n\n",
            CreatedAt = now,
            ModifiedAt = now,
            Order = 0
        };
        project.Documents.Add(scene1);
        
        // Add a sample character
        var character1 = new Document
        {
            Title = "Sample character",
            Type = DocumentType.Character,
            Content = "# Character name \n\n## Description\n\n## Background\n\nAdd character background here.\n\n## Traits\n\n",
            CreatedAt = now,
            ModifiedAt = now,
            Order = 0
        };
        project.Documents.Add(character1);
        
        // Add a sample location
        var location1 = new Document
        {
            Title = "Sample location",
            Type = DocumentType.Location,
            Content = "# Location name\n\n## Description\n\n",
            CreatedAt = now,
            ModifiedAt = now,
            Order = 0
        };
        project.Documents.Add(location1);
        
        // Add a sample research document
        var research1 = new Document
        {
            Title = "Sample research note",
            Type = DocumentType.Research,
            Content = "# Sample research note\n\nAdd your research notes here.\n\n## Sources\n\n- Source 1\n- Source 2\n",
            CreatedAt = now,
            ModifiedAt = now,
            Order = 0
        };
        project.Documents.Add(research1);
        
        // Add a sample note
        var note1 = new Document
        {
            Title = "Sample note",
            Type = DocumentType.Note,
            Content = "# Ideas or insights\n\n- Idea 1\n- Idea 2\n- Idea 3\n\n## Notes\n\nAdditional notes and thoughts.\n",
            CreatedAt = now,
            ModifiedAt = now,
            Order = 0
        };
        project.Documents.Add(note1);
    }

    public void UpdateProjectStatistics(Project project)
    {
        if (project.Metadata?.Statistics == null)
            return;

        var stats = project.Metadata.Statistics;
        stats.TotalWordCount = 0;
        stats.TotalCharacterCount = 0;
        stats.TotalCharacterCountNoSpaces = 0;
        stats.ParagraphCount = 0;
        stats.SentenceCount = 0;

        foreach (var doc in project.Documents)
        {
            // Access Content property to ensure it's loaded (will load from file if needed)
            var content = doc.Content;
            
            // Calculate word count, character count, etc. using the Content property
            // These properties access Content, which will load from file if needed
            stats.TotalWordCount += doc.WordCount;
            stats.TotalCharacterCount += doc.CharacterCount;
            stats.TotalCharacterCountNoSpaces += doc.CharacterCountNoSpaces;
            
            // Simple paragraph and sentence counting
            if (!string.IsNullOrEmpty(content))
            {
                var paragraphs = content.Split(new[] { "\r\n\r\n", "\n\n", "\r\r" }, StringSplitOptions.RemoveEmptyEntries);
                stats.ParagraphCount += paragraphs.Length;
                
                var sentences = content.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Count();
                stats.SentenceCount += sentences;
            }
        }

        // Estimate page count (assuming ~250 words per page)
        stats.TotalPageCount = stats.TotalWordCount > 0 ? (int)Math.Ceiling(stats.TotalWordCount / 250.0) : 0;
        stats.LastCalculatedAt = DateTime.Now;
    }

    /// <summary>
    /// Generates a content file path based on document type and title.
    /// Chapters become folders, scenes go into chapter folders, other types go into type-specific folders.
    /// </summary>
    public string GenerateContentFilePath(Document document, Dictionary<string, Document> documentsById)
    {
        var sanitizedTitle = SanitizeFileName(document.Title);
        
        switch (document.Type)
        {
            case DocumentType.Chapter:
                // Chapters are folders in Manuscript folder, content goes in a file inside the folder
                if (!string.IsNullOrEmpty(document.FolderPath))
                {
                    var sanitizedFolderPath = SanitizeFileName(document.FolderPath);
                    return $"Manuscript/{sanitizedFolderPath}/{sanitizedTitle}/content.md";
                }
                return $"Manuscript/{sanitizedTitle}/content.md";
            
            case DocumentType.Scene:
                // Scenes go into their parent chapter folder in Manuscript
                if (!string.IsNullOrEmpty(document.ParentId) && documentsById.TryGetValue(document.ParentId, out var parentChapter))
                {
                    var parentTitle = SanitizeFileName(parentChapter.Title);
                    if (!string.IsNullOrEmpty(parentChapter.FolderPath))
                    {
                        var sanitizedFolderPath = SanitizeFileName(parentChapter.FolderPath);
                        return $"Manuscript/{sanitizedFolderPath}/{parentTitle}/{sanitizedTitle}.md";
                    }
                    return $"Manuscript/{parentTitle}/{sanitizedTitle}.md";
                }
                // Fallback: if no parent, put in scenes folder
                return $"scenes/{sanitizedTitle}.md";
            
            case DocumentType.Character:
                if (!string.IsNullOrEmpty(document.FolderPath))
                {
                    var sanitizedFolderPath = SanitizeFileName(document.FolderPath);
                    return $"characters/{sanitizedFolderPath}/{sanitizedTitle}.md";
                }
                return $"characters/{sanitizedTitle}.md";
            
            case DocumentType.Location:
                if (!string.IsNullOrEmpty(document.FolderPath))
                {
                    var sanitizedFolderPath = SanitizeFileName(document.FolderPath);
                    return $"locations/{sanitizedFolderPath}/{sanitizedTitle}.md";
                }
                return $"locations/{sanitizedTitle}.md";
            
            case DocumentType.Research:
                if (!string.IsNullOrEmpty(document.FolderPath))
                {
                    var sanitizedFolderPath = SanitizeFileName(document.FolderPath);
                    return $"research/{sanitizedFolderPath}/{sanitizedTitle}.md";
                }
                return $"research/{sanitizedTitle}.md";
            
            case DocumentType.Note:
                return $"notes/{sanitizedTitle}.md";
            
            case DocumentType.Other:
            default:
                return $"other/{sanitizedTitle}.md";
        }
    }

    /// <summary>
    /// Sanitizes a string to be used as a filename or folder name.
    /// Removes invalid characters and replaces spaces with hyphens.
    /// </summary>
    public string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "untitled";

        // Remove invalid characters
        var invalidChars = Path.GetInvalidFileNameChars().Concat(Path.GetInvalidPathChars()).Distinct();
        var sanitized = fileName;
        foreach (var c in invalidChars)
        {
            sanitized = sanitized.Replace(c, '-');
        }

        // Replace multiple spaces/hyphens with single hyphen
        sanitized = Regex.Replace(sanitized, @"[\s\-]+", "-");
        
        // Remove leading/trailing hyphens and dots
        sanitized = sanitized.Trim('-', '.', ' ');
        
        // Ensure it's not empty
        if (string.IsNullOrWhiteSpace(sanitized))
            sanitized = "untitled";

        return sanitized;
    }
}
