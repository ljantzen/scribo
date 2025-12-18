using System;
using System.Collections.Generic;
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

    public string FormatStatisticsText(ProjectStatistics? statistics, int? targetWordCount = null, ProjectStatistics? sessionStatistics = null, (int wordsWritten, int wordsDeleted, int charactersWritten, int charactersDeleted)? trackingStats = null, DailyStatistics? dailyStatistics = null, bool showActiveIdleTime = true)
    {
        if (statistics == null)
        {
            return "No statistics available";
        }

        // Format statistics text for status bar
        // Show word count, character count, and page count
        var text = $"Words: {statistics.TotalWordCount:N0} | Characters: {statistics.TotalCharacterCount:N0} | Pages: {statistics.TotalPageCount}";
        
        // Add daily statistics if provided
        if (dailyStatistics != null)
        {
            var dailyWords = dailyStatistics.NetWordChange;
            var dailyChars = dailyStatistics.NetCharacterChange;
            var dailyPages = dailyStatistics.NetPageChange;
            
            // Show daily stats if there's been any change or time tracked
            if (dailyWords != 0 || dailyChars != 0 || dailyPages != 0 || dailyStatistics.ActiveTimeSeconds > 0 || dailyStatistics.IdleTimeSeconds > 0)
            {
                var dailySign = dailyWords >= 0 ? "+" : "";
                text += $" | Today: {dailySign}{dailyWords:N0} words";
                
                // Add separate tracking if available
                if (dailyStatistics.WordsWritten > 0 || dailyStatistics.WordsDeleted > 0)
                {
                    text += $" (+{dailyStatistics.WordsWritten:N0}/-{dailyStatistics.WordsDeleted:N0})";
                }
                
                // Add time tracking if enabled
                if (showActiveIdleTime)
                {
                    var activeTime = dailyStatistics.ActiveTime;
                    var idleTime = dailyStatistics.IdleTime;
                    
                    if (activeTime.TotalSeconds > 0 || idleTime.TotalSeconds > 0)
                    {
                        var timeParts = new List<string>();
                        
                        if (activeTime.TotalSeconds > 0)
                        {
                            if (activeTime.TotalHours >= 1)
                            {
                                timeParts.Add($"{(int)activeTime.TotalHours}h {activeTime.Minutes}m active");
                            }
                            else if (activeTime.TotalMinutes >= 1)
                            {
                                timeParts.Add($"{activeTime.Minutes}m {activeTime.Seconds}s active");
                            }
                            else
                            {
                                timeParts.Add($"{activeTime.Seconds}s active");
                            }
                        }
                        
                        if (idleTime.TotalSeconds > 0)
                        {
                            if (idleTime.TotalHours >= 1)
                            {
                                timeParts.Add($"{(int)idleTime.TotalHours}h {idleTime.Minutes}m idle");
                            }
                            else if (idleTime.TotalMinutes >= 1)
                            {
                                timeParts.Add($"{idleTime.Minutes}m {idleTime.Seconds}s idle");
                            }
                            else
                            {
                                timeParts.Add($"{idleTime.Seconds}s idle");
                            }
                        }
                        
                        if (timeParts.Count > 0)
                        {
                            text += $" | {string.Join(", ", timeParts)}";
                        }
                    }
                }
            }
        }
        
        // Add session statistics if provided
        if (sessionStatistics != null)
        {
            var sessionWords = sessionStatistics.TotalWordCount;
            var sessionChars = sessionStatistics.TotalCharacterCount;
            var sessionPages = sessionStatistics.TotalPageCount;
            
            // Only show session stats if there's been any change
            if (sessionWords != 0 || sessionChars != 0 || sessionPages != 0)
            {
                var sessionSign = sessionWords >= 0 ? "+" : "";
                text += $" | Session: {sessionSign}{sessionWords:N0} words";
                
                // Add separate tracking if provided
                if (trackingStats.HasValue)
                {
                    var (wordsWritten, wordsDeleted, charsWritten, charsDeleted) = trackingStats.Value;
                    if (wordsWritten > 0 || wordsDeleted > 0)
                    {
                        text += $" (+{wordsWritten:N0}/-{wordsDeleted:N0})";
                    }
                }
            }
        }
        
        // If there's a word count target, show progress
        if (targetWordCount.HasValue && targetWordCount.Value > 0)
        {
            var progress = (double)statistics.TotalWordCount / targetWordCount.Value * 100;
            text += $" | Progress: {progress:F1}%";
        }
        
        return text;
    }
}
