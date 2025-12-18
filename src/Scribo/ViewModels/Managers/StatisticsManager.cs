using System;
using System.IO;
using System.Linq;
using Scribo.Models;
using Scribo.Services;

namespace Scribo.ViewModels.Managers;

public class StatisticsManager
{
    private readonly ProjectService _projectService;
    private Project? _currentProject;
    private string _currentProjectPath = string.Empty;

    public StatisticsManager(ProjectService projectService)
    {
        _projectService = projectService;
    }

    public void SetCurrentProject(Project? project, string projectPath)
    {
        _currentProject = project;
        _currentProjectPath = projectPath;
    }

    public ProjectStatistics? CalculateStatistics(Document? selectedDocument, string editorText)
    {
        if (_currentProject == null)
            return null;

        try
        {
            // Ensure all documents have ProjectDirectory set if we have a project path
            if (!string.IsNullOrEmpty(_currentProjectPath))
            {
                var projectDirectory = Path.GetDirectoryName(_currentProjectPath) ?? Path.GetDirectoryName(Path.GetFullPath(_currentProjectPath)) ?? string.Empty;
                foreach (var document in _currentProject.Documents)
                {
                    if (string.IsNullOrEmpty(document.ProjectDirectory))
                    {
                        document.ProjectDirectory = projectDirectory;
                    }
                }
            }

            // Update the selected document's content from editor if it exists
            // This ensures statistics reflect the current editor content
            if (selectedDocument != null)
            {
                selectedDocument.Content = editorText;
            }

            // Ensure Statistics object exists
            if (_currentProject.Metadata.Statistics == null)
            {
                _currentProject.Metadata.Statistics = new ProjectStatistics();
            }

            // Calculate statistics
            _projectService.UpdateProjectStatistics(_currentProject);
            
            return _currentProject.Metadata.Statistics;
        }
        catch (Exception ex)
        {
            return null;
        }
    }

    public string FormatStatisticsText(ProjectStatistics? statistics, int? targetWordCount = null)
    {
        if (statistics == null)
        {
            return "No statistics available";
        }

        // Format statistics text for status bar
        // Show word count, character count, and page count
        var text = $"Words: {statistics.TotalWordCount:N0} | Characters: {statistics.TotalCharacterCount:N0} | Pages: {statistics.TotalPageCount}";
        
        // If there's a word count target, show progress
        if (targetWordCount.HasValue && targetWordCount.Value > 0)
        {
            var progress = (double)statistics.TotalWordCount / targetWordCount.Value * 100;
            text += $" | Progress: {progress:F1}%";
        }
        
        return text;
    }
}
