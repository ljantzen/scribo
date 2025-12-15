using System;
using System.Collections.Generic;

namespace Scribo.Models;

/// <summary>
/// Comprehensive project metadata modeled after Scrivener's project structure
/// </summary>
public class ProjectMetadata
{
    // Basic Project Information
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string ISBN { get; set; } = string.Empty;
    public string Copyright { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public string ProjectNotes { get; set; } = string.Empty;

    // Word Count Targets and Goals
    public WordCountTargets WordCountTargets { get; set; } = new();

    // Project Statistics
    public ProjectStatistics Statistics { get; set; } = new();

    // Session Information
    public SessionInfo CurrentSession { get; set; } = new();
    public List<SessionInfo> SessionHistory { get; set; } = new();

    // Keywords and Tags
    public List<string> Keywords { get; set; } = new();
    public List<string> Tags { get; set; } = new();

    // Custom Metadata Fields
    public Dictionary<string, string> CustomFields { get; set; } = new();

    // Compile Settings
    public CompileSettings CompileSettings { get; set; } = new();

    // Document Organization
    public DocumentOrganizationSettings DocumentOrganization { get; set; } = new();

    // Research Folder Settings
    public ResearchFolderSettings ResearchSettings { get; set; } = new();

    // Project Settings
    public ProjectSettings Settings { get; set; } = new();

    // Version Information
    public string Version { get; set; } = "1.0";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
    public DateTime? LastOpenedAt { get; set; }
}

public class WordCountTargets
{
    public int TargetWordCount { get; set; } = 0;
    public int TargetCharacterCount { get; set; } = 0;
    public int TargetPageCount { get; set; } = 0;
    public DateTime? TargetDate { get; set; }
    public bool ShowTargetInStatusBar { get; set; } = true;
    public bool IncludeNotesInCount { get; set; } = false;
    public bool IncludeResearchInCount { get; set; } = false;
}

public class ProjectStatistics
{
    public int TotalWordCount { get; set; } = 0;
    public int TotalCharacterCount { get; set; } = 0;
    public int TotalCharacterCountNoSpaces { get; set; } = 0;
    public int TotalPageCount { get; set; } = 0;
    public int ParagraphCount { get; set; } = 0;
    public int SentenceCount { get; set; } = 0;
    public DateTime? LastCalculatedAt { get; set; }
}

public class SessionInfo
{
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public int WordsWritten { get; set; } = 0;
    public int CharactersWritten { get; set; } = 0;
    public string Notes { get; set; } = string.Empty;
}

public class CompileSettings
{
    public string OutputFormat { get; set; } = "PDF";
    public string PageSize { get; set; } = "A4";
    public string FontFamily { get; set; } = "Times New Roman";
    public int FontSize { get; set; } = 12;
    public double LineSpacing { get; set; } = 1.5;
    public double MarginTop { get; set; } = 2.54; // cm
    public double MarginBottom { get; set; } = 2.54;
    public double MarginLeft { get; set; } = 3.18;
    public double MarginRight { get; set; } = 3.18;
    public bool IncludeTitlePage { get; set; } = true;
    public bool IncludeTableOfContents { get; set; } = true;
    public bool IncludePageNumbers { get; set; } = true;
    public string PageNumberPosition { get; set; } = "Bottom Center";
}

public class DocumentOrganizationSettings
{
    public bool ShowBinderInSidebar { get; set; } = true;
    public bool ShowCorkboard { get; set; } = false;
    public bool ShowOutliner { get; set; } = false;
    public string DefaultDocumentType { get; set; } = "Chapter";
    public bool AutoNumberDocuments { get; set; } = false;
    public string NumberingFormat { get; set; } = "Chapter {n}";
}

public class ResearchFolderSettings
{
    public bool ShowResearchInSidebar { get; set; } = true;
    public bool AllowWebResearch { get; set; } = true;
    public int MaxResearchFileSize { get; set; } = 50; // MB
    public List<string> AllowedResearchExtensions { get; set; } = new()
    {
        ".pdf", ".doc", ".docx", ".txt", ".md", ".rtf", ".html", ".jpg", ".png", ".gif"
    };
}

public class ProjectSettings
{
    public bool AutoSave { get; set; } = true;
    public int AutoSaveIntervalMinutes { get; set; } = 5;
    public bool BackupOnSave { get; set; } = true;
    public int MaxBackups { get; set; } = 10;
    public string BackupLocation { get; set; } = string.Empty;
    public bool ShowWordCountInStatusBar { get; set; } = true;
    public bool ShowCharacterCountInStatusBar { get; set; } = true;
    public bool ShowPageCountInStatusBar { get; set; } = false;
    public string DefaultFontFamily { get; set; } = "Consolas";
    public int DefaultFontSize { get; set; } = 14;
    public bool WordWrap { get; set; } = true;
    public bool ShowLineNumbers { get; set; } = false;
    public bool ShowRuler { get; set; } = false;
    public string Theme { get; set; } = "Light";
}
