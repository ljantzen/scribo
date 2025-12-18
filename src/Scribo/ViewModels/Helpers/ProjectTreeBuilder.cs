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

        // Organize documents hierarchically
        var chapters = project.Documents.Where(d => d.Type == DocumentType.Chapter).ToList();
        var scenes = project.Documents.Where(d => d.Type == DocumentType.Scene).ToList();
        var otherDocuments = project.Documents.Where(d => d.Type != DocumentType.Chapter && d.Type != DocumentType.Scene).ToList();

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
