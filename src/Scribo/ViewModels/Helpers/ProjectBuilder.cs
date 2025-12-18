using System.Collections.Generic;
using System.Linq;
using Scribo.Models;

namespace Scribo.ViewModels.Helpers;

public static class ProjectBuilder
{
    public static Project BuildProjectFromTree(
        Project? currentProject,
        ProjectTreeItemViewModel rootItem,
        string currentProjectPath,
        Document? selectedDocument,
        string editorText)
    {
        // Start with the current project if it exists, otherwise create a new one
        var project = currentProject ?? new Project
        {
            Name = rootItem?.Name ?? "Untitled Project",
            FilePath = currentProjectPath
        };

        // Update the project name from the tree
        project.Name = rootItem?.Name ?? "Untitled Project";
        project.FilePath = currentProjectPath;

        // If there's a selected document, update its content with the editor text
        if (selectedDocument != null)
        {
            selectedDocument.Content = editorText;
        }

        // Ensure all documents from the current project are included
        // (they should already be there if currentProject exists)
        if (currentProject == null && rootItem != null)
        {
            // If no current project, we need to build it from the tree
            // This shouldn't normally happen, but handle it as a fallback
            CollectDocumentsFromTree(rootItem, project.Documents, selectedDocument, editorText);
        }
        else if (currentProject != null)
        {
            // Update selected document content if it exists in the project
            if (selectedDocument != null)
            {
                var docInProject = project.Documents.FirstOrDefault(d => d.Id == selectedDocument.Id);
                if (docInProject != null)
                {
                    docInProject.Content = editorText;
                }
            }
        }

        return project;
    }

    private static void CollectDocumentsFromTree(
        ProjectTreeItemViewModel item,
        List<Document> documents,
        Document? selectedDocument,
        string editorText)
    {
        if (item.Document != null)
        {
            // Update content if this is the selected item
            if (selectedDocument != null && item.Document.Id == selectedDocument.Id)
            {
                item.Document.Content = editorText;
            }

            // Add document if not already in the list
            if (!documents.Any(d => d.Id == item.Document.Id))
            {
                documents.Add(item.Document);
            }
        }

        foreach (var child in item.Children)
        {
            CollectDocumentsFromTree(child, documents, selectedDocument, editorText);
        }
    }
}
