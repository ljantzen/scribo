using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Scribo.Models;

namespace Scribo.ViewModels.Helpers;

public static class ProjectTreeBuilder
{
    public static ProjectTreeItemViewModel BuildProjectTree(Project project, string currentProjectPath)
    {
        // Ensure ProjectDirectory is set on all documents if we have a project path
        if (!string.IsNullOrEmpty(currentProjectPath))
        {
            var projectDirectory = System.IO.Path.GetDirectoryName(currentProjectPath) 
                ?? System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(currentProjectPath)) 
                ?? string.Empty;
            foreach (var document in project.Documents)
            {
                if (string.IsNullOrEmpty(document.ProjectDirectory))
                {
                    document.ProjectDirectory = projectDirectory;
                }
            }
        }

        var root = new ProjectTreeItemViewModel
        {
            Name = project.Name,
            Icon = "📁",
            IsRoot = true
        };

        // Build a dictionary of documents by ID for quick lookup
        var documentsById = project.Documents.ToDictionary(d => d.Id);

        // Separate documents in Trashcan from active documents
        var documentsInTrashcan = project.Documents.Where(d => !string.IsNullOrEmpty(d.ContentFilePath) && d.ContentFilePath.StartsWith("Trashcan/", System.StringComparison.OrdinalIgnoreCase)).ToList();
        var activeDocuments = project.Documents.Where(d => string.IsNullOrEmpty(d.ContentFilePath) || !d.ContentFilePath.StartsWith("Trashcan/", System.StringComparison.OrdinalIgnoreCase)).ToList();
        
        foreach (var doc in documentsInTrashcan)
        {
        }

        // Double-check: ensure no Trashcan documents are in activeDocuments
        // This is a safety check in case ContentFilePath wasn't set correctly
        activeDocuments = activeDocuments.Where(d => 
            string.IsNullOrEmpty(d.ContentFilePath) || 
            !d.ContentFilePath.StartsWith("Trashcan/", System.StringComparison.OrdinalIgnoreCase)).ToList();
        
        // Organize active documents hierarchically
        var chapters = activeDocuments.Where(d => d.Type == DocumentType.Chapter).ToList();
        var scenes = activeDocuments.Where(d => d.Type == DocumentType.Scene).ToList();
        var otherDocuments = activeDocuments.Where(d => d.Type != DocumentType.Chapter && d.Type != DocumentType.Scene).ToList();

        // Always add Manuscript folder (even if empty)
        var manuscriptNode = new ProjectTreeItemViewModel
        {
            Name = "Manuscript",
            Icon = "📁",
            IsManuscriptFolder = true
        };

        BuildManuscriptTree(manuscriptNode, chapters, scenes);
        root.Children.Add(manuscriptNode);

        // Always add other document type folders (even if empty)
        var documentTypes = new[]
        {
            DocumentType.Character,
            DocumentType.Location,
            DocumentType.Research,
            DocumentType.Note
        };

        foreach (var docType in documentTypes)
        {
            var typeFolder = BuildDocumentTypeFolder(docType, otherDocuments);
            root.Children.Add(typeFolder);
        }

        // Always add Trashcan folder (even if empty)
        var trashcanNode = BuildTrashcanTree(documentsInTrashcan);
        root.Children.Add(trashcanNode);

        return root;
    }

    private static void BuildManuscriptTree(ProjectTreeItemViewModel manuscriptNode, List<Document> chapters, List<Document> scenes)
    {
        // Organize chapters into folders and subfolders
        var chaptersInRoot = chapters.Where(c => string.IsNullOrEmpty(c.FolderPath)).OrderBy(c => c.CreatedAt);
        var chaptersInSubfolders = chapters.Where(c => !string.IsNullOrEmpty(c.FolderPath)).ToList();

        // Add chapters directly in the root Manuscript folder
        foreach (var chapter in chaptersInRoot)
        {
            var chapterNode = new ProjectTreeItemViewModel
            {
                Name = chapter.Title,
                Icon = GetIconForDocumentType(chapter.Type),
                Document = chapter
            };

            // Add scenes that belong to this chapter
            var chapterScenes = scenes.Where(s => s.ParentId == chapter.Id).OrderBy(s => s.Order).ThenBy(s => s.CreatedAt);
            foreach (var scene in chapterScenes)
            {
                chapterNode.Children.Add(new ProjectTreeItemViewModel
                {
                    Name = scene.Title,
                    Icon = GetIconForDocumentType(scene.Type),
                    Document = scene
                });
            }

            manuscriptNode.Children.Add(chapterNode);
        }

        // Group chapters by subfolder path
        var subfolderGroups = chaptersInSubfolders
            .GroupBy(c => c.FolderPath.Split('/')[0]) // First level subfolder
            .OrderBy(g => g.Key);

        foreach (var subfolderGroup in subfolderGroups)
        {
            var subfolderName = subfolderGroup.Key;
            var subfolderNode = new ProjectTreeItemViewModel
            {
                Name = subfolderName,
                Icon = "📁",
                FolderPath = subfolderName,
                FolderDocumentType = DocumentType.Chapter
            };

            // Add chapters in this subfolder
            foreach (var chapter in subfolderGroup.OrderBy(c => c.Order).ThenBy(c => c.CreatedAt))
            {
                var chapterNode = new ProjectTreeItemViewModel
                {
                    Name = chapter.Title,
                    Icon = GetIconForDocumentType(chapter.Type),
                    Document = chapter
                };

                // Add scenes that belong to this chapter
                var chapterScenes = scenes.Where(s => s.ParentId == chapter.Id).OrderBy(s => s.CreatedAt);
                foreach (var scene in chapterScenes)
                {
                    chapterNode.Children.Add(new ProjectTreeItemViewModel
                    {
                        Name = scene.Title,
                        Icon = GetIconForDocumentType(scene.Type),
                        Document = scene
                    });
                }

                subfolderNode.Children.Add(chapterNode);
            }

            manuscriptNode.Children.Add(subfolderNode);
        }
    }

    private static ProjectTreeItemViewModel BuildDocumentTypeFolder(DocumentType docType, List<Document> otherDocuments)
    {
        var typeFolder = new ProjectTreeItemViewModel
        {
            Name = GetTypeFolderName(docType),
            Icon = "📁"
        };

        // Mark folder types
        switch (docType)
        {
            case DocumentType.Character:
                typeFolder.IsCharactersFolder = true;
                break;
            case DocumentType.Location:
                typeFolder.IsLocationsFolder = true;
                break;
            case DocumentType.Research:
                typeFolder.IsResearchFolder = true;
                break;
            case DocumentType.Note:
                typeFolder.IsNotesFolder = true;
                break;
        }

        // Organize documents into folders and subfolders
        var docsInRoot = otherDocuments.Where(d => d.Type == docType && string.IsNullOrEmpty(d.FolderPath)).OrderBy(d => d.Order).ThenBy(d => d.Title);
        var docsInSubfolders = otherDocuments.Where(d => d.Type == docType && !string.IsNullOrEmpty(d.FolderPath)).ToList();

        // Add documents directly in the root folder
        foreach (var doc in docsInRoot)
        {
            typeFolder.Children.Add(new ProjectTreeItemViewModel
            {
                Name = doc.Title,
                Icon = GetIconForDocumentType(doc.Type),
                Document = doc
            });
        }

        // Group documents by subfolder path
        var docSubfolderGroups = docsInSubfolders
            .GroupBy(d => d.FolderPath.Split('/')[0]) // First level subfolder
            .OrderBy(g => g.Key);

        foreach (var subfolderGroup in docSubfolderGroups)
        {
            var subfolderName = subfolderGroup.Key;
            var subfolderNode = new ProjectTreeItemViewModel
            {
                Name = subfolderName,
                Icon = "📁",
                FolderPath = subfolderName,
                FolderDocumentType = docType
            };

            // Add documents in this subfolder
            foreach (var doc in subfolderGroup.OrderBy(d => d.Order).ThenBy(d => d.Title))
            {
                subfolderNode.Children.Add(new ProjectTreeItemViewModel
                {
                    Name = doc.Title,
                    Icon = GetIconForDocumentType(doc.Type),
                    Document = doc
                });
            }

            typeFolder.Children.Add(subfolderNode);
        }

        return typeFolder;
    }

    private static ProjectTreeItemViewModel BuildTrashcanTree(List<Document> documentsInTrashcan)
    {
        
        var trashcanNode = new ProjectTreeItemViewModel
        {
            Name = "Trashcan",
            Icon = "🗑️",
            IsTrashcanFolder = true
        };

        // Build a tree structure preserving nested folders
        // Documents in Trashcan have paths like "Trashcan/locations/location1.md" or "Trashcan/locations/subfolder/doc.md"
        var folderNodes = new Dictionary<string, ProjectTreeItemViewModel>();

        foreach (var doc in documentsInTrashcan)
        {
            
            if (string.IsNullOrEmpty(doc.ContentFilePath))
            {
                continue;
            }

            // Remove "Trashcan/" prefix to get the relative path
            var relativePath = doc.ContentFilePath.StartsWith("Trashcan/", System.StringComparison.OrdinalIgnoreCase)
                ? doc.ContentFilePath.Substring("Trashcan/".Length)
                : doc.ContentFilePath;

            // Split path into parts (e.g., "locations/subfolder/doc.md" -> ["locations", "subfolder"])
            var pathParts = relativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (pathParts.Length == 0)
                continue;

            // Get the filename (last part)
            var fileName = pathParts[pathParts.Length - 1];
            
            // Build folder path up to the document
            var folderPath = string.Join("/", pathParts.Take(pathParts.Length - 1));
            
            // Get or create the folder node
            ProjectTreeItemViewModel folderNode;
            if (string.IsNullOrEmpty(folderPath))
            {
                // Document is in root of Trashcan
                folderNode = trashcanNode;
            }
            else
            {
                if (!folderNodes.ContainsKey(folderPath))
                {
                    // Create folder nodes recursively
                    var currentPath = "";
                    ProjectTreeItemViewModel parentNode = trashcanNode;
                    
                    foreach (var part in pathParts.Take(pathParts.Length - 1))
                    {
                        currentPath = string.IsNullOrEmpty(currentPath) ? part : currentPath + "/" + part;
                        
                        if (!folderNodes.ContainsKey(currentPath))
                        {
                            var newFolderNode = new ProjectTreeItemViewModel
                            {
                                Name = part,
                                Icon = "📁",
                                FolderPath = currentPath
                            };
                            folderNodes[currentPath] = newFolderNode;
                            parentNode.Children.Add(newFolderNode);
                        }
                        
                        parentNode = folderNodes[currentPath];
                    }
                }
                
                folderNode = folderNodes[folderPath];
            }

            // Add document to the folder
            var docNode = new ProjectTreeItemViewModel
            {
                Name = doc.Title,
                Icon = GetIconForDocumentType(doc.Type),
                Document = doc
            };
            folderNode.Children.Add(docNode);
        }

        // Sort children in each folder
        SortTrashcanNode(trashcanNode);
        
        // Remove empty folders (folders with no children)
        RemoveEmptyFolders(trashcanNode);
        
        return trashcanNode;
    }

    private static void RemoveEmptyFolders(ProjectTreeItemViewModel node)
    {
        // Recursively remove empty folders from children
        var childrenToRemove = new List<ProjectTreeItemViewModel>();
        
        foreach (var child in node.Children)
        {
            // Recursively process subfolders first
            if (child.IsFolder)
            {
                RemoveEmptyFolders(child);
                
                // If folder is now empty (no children), mark it for removal
                if (child.Children.Count == 0)
                {
                    childrenToRemove.Add(child);
                }
            }
        }
        
        // Remove empty folders
        foreach (var emptyFolder in childrenToRemove)
        {
            node.Children.Remove(emptyFolder);
        }
    }

    private static void SortTrashcanNode(ProjectTreeItemViewModel node)
    {
        // Sort: folders first, then documents, both alphabetically
        var sortedChildren = node.Children
            .OrderBy(c => c.Document == null ? 0 : 1) // Folders first (Document == null)
            .ThenBy(c => c.Name)
            .ToList();
        
        node.Children.Clear();
        foreach (var child in sortedChildren)
        {
            node.Children.Add(child);
            // Recursively sort subfolders
            if (child.Document == null)
            {
                SortTrashcanNode(child);
            }
        }
    }

    public static ProjectTreeItemViewModel CreateInitialProjectTree()
    {
        var root = new ProjectTreeItemViewModel
        {
            Name = "Untitled Project",
            Icon = "📁",
            IsRoot = true,
            Children = new ObservableCollection<ProjectTreeItemViewModel>
            {
                new()
                {
                    Name = "Manuscript",
                    Icon = "📁",
                    IsManuscriptFolder = true,
                    Children = new ObservableCollection<ProjectTreeItemViewModel>()
                },
                new()
                {
                    Name = "Characters",
                    Icon = "📁",
                    IsCharactersFolder = true,
                    Children = new ObservableCollection<ProjectTreeItemViewModel>()
                },
                new()
                {
                    Name = "Locations",
                    Icon = "📁",
                    IsLocationsFolder = true,
                    Children = new ObservableCollection<ProjectTreeItemViewModel>()
                },
                new()
                {
                    Name = "Research",
                    Icon = "📁",
                    IsResearchFolder = true,
                    Children = new ObservableCollection<ProjectTreeItemViewModel>()
                },
                new()
                {
                    Name = "Notes",
                    Icon = "📁",
                    IsNotesFolder = true,
                    Children = new ObservableCollection<ProjectTreeItemViewModel>()
                },
                new()
                {
                    Name = "Trashcan",
                    Icon = "🗑️",
                    IsTrashcanFolder = true,
                    Children = new ObservableCollection<ProjectTreeItemViewModel>()
                }
            }
        };
        return root;
    }

    private static string GetTypeFolderName(DocumentType type)
    {
        return type switch
        {
            DocumentType.Character => "Characters",
            DocumentType.Location => "Locations",
            DocumentType.Research => "Research",
            DocumentType.Note => "Notes",
            DocumentType.Other => "Other",
            _ => "Documents"
        };
    }

    public static string GetIconForDocumentType(DocumentType type)
    {
        return type switch
        {
            DocumentType.Chapter => "📄",
            DocumentType.Scene => "🎬",
            DocumentType.Note => "📝",
            DocumentType.Research => "🔬",
            DocumentType.Character => "👤",
            DocumentType.Location => "📍",
            _ => "📄"
        };
    }
}
