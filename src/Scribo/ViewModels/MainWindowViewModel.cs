using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scribo.Models;
using Scribo.Services;
using Scribo.Views;

namespace Scribo.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly PluginManager _pluginManager;
    private readonly FileService _fileService;
    private readonly ProjectService _projectService;
    private readonly MostRecentlyUsedService _mruService;
    private readonly SearchIndexService _searchIndexService;
    private readonly DocumentLinkService _documentLinkService;
    private FileDialogService? _fileDialogService;
    private Window? _parentWindow;
    private SearchWindow? _searchWindow;

    [ObservableProperty]
    private string editorText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ProjectTreeItemViewModel> projectTreeItems = new();

    [ObservableProperty]
    private ProjectTreeItemViewModel? selectedProjectItem;

    [ObservableProperty]
    private bool isProjectTreeOnLeft = true;

    [ObservableProperty]
    private bool isProjectTreeOnRight = false;

    [ObservableProperty]
    private string currentFilePath = string.Empty;

    [ObservableProperty]
    private string currentProjectPath = string.Empty;

    [ObservableProperty]
    private bool hasUnsavedChanges = false;

    [ObservableProperty]
    private ObservableCollection<RecentProject> recentProjects = new();

    public bool HasRecentProjects => RecentProjects.Count > 0;

    // Statistics properties for status bar display
    [ObservableProperty]
    private int totalWordCount = 0;

    [ObservableProperty]
    private int totalCharacterCount = 0;

    [ObservableProperty]
    private int totalPageCount = 0;

    [ObservableProperty]
    private string statisticsText = string.Empty;

    // View mode properties
    [ObservableProperty]
    private bool isSourceMode = true;

    [ObservableProperty]
    private bool isPreviewMode = false;

    [ObservableProperty]
    private string renderedMarkdown = string.Empty;

    [ObservableProperty]
    private ObservableCollection<MarkdownBlock> renderedMarkdownBlocks = new();

    [ObservableProperty]
    private FindReplaceViewModel findReplaceViewModel = new();

    [ObservableProperty]
    private DocumentLinkAutocompleteViewModel documentLinkAutocompleteViewModel = new();

    private Project? _currentProject;
    private DispatcherTimer? _idleTimer;
    private DateTime _lastActivityTime = DateTime.Now;
    private const int IdleTimeoutSeconds = 2; // Calculate statistics after 2 seconds of inactivity

    public MainWindowViewModel(PluginManager? pluginManager = null, FileService? fileService = null, ProjectService? projectService = null, MostRecentlyUsedService? mruService = null, SearchIndexService? searchIndexService = null)
    {
        _pluginManager = pluginManager ?? new PluginManager();
        _fileService = fileService ?? new FileService();
        _projectService = projectService ?? new ProjectService();
        _mruService = mruService ?? new MostRecentlyUsedService();
        _searchIndexService = searchIndexService ?? new SearchIndexService();
        _documentLinkService = new DocumentLinkService();
        InitializeProjectTree();
        UpdateRecentProjectsList();
        InitializeIdleStatisticsTimer();
        InitializeFindReplace();
        InitializeDocumentLinkAutocomplete();
    }

    private void InitializeDocumentLinkAutocomplete()
    {
        // Update autocomplete documents when project changes
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_currentProject))
            {
                UpdateAutocompleteDocuments();
            }
        };
    }

    private void UpdateAutocompleteDocuments()
    {
        if (_currentProject?.Documents != null)
        {
            DocumentLinkAutocompleteViewModel.SetDocuments(_currentProject.Documents.ToList());
        }
        else
        {
            DocumentLinkAutocompleteViewModel.SetDocuments(new List<Document>());
        }
    }

    private void InitializeFindReplace()
    {
        FindReplaceViewModel.SelectMatchRequested += OnSelectMatch;
        FindReplaceViewModel.ReplaceTextRequested += OnReplaceText;
        FindReplaceViewModel.GetCurrentCursorPositionRequested += GetCurrentCursorPosition;
        
        // Update find text when editor text changes
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(EditorText))
            {
                FindReplaceViewModel.SetDocumentText(EditorText);
            }
        };
    }

    private int GetCurrentCursorPosition()
    {
        if (_parentWindow is Views.MainWindow mainWindow)
        {
            var textBox = mainWindow.FindControl<TextBox>("sourceTextBox");
            if (textBox != null)
            {
                // Return the end of the current selection, or the caret position
                return textBox.SelectionEnd > 0 ? textBox.SelectionEnd : textBox.CaretIndex;
            }
        }
        return 0;
    }

    private void OnSelectMatch(int index, int length)
    {
        SelectMatchRequested?.Invoke(index, length);
    }

    private void OnReplaceText(string newText)
    {
        EditorText = newText;
        HasUnsavedChanges = true;
    }

    public event Action<int, int>? SelectMatchRequested;

    private void InitializeIdleStatisticsTimer()
    {
        _idleTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(0.5) // Check every 500ms
        };
        _idleTimer.Tick += OnIdleTimerTick;
        _idleTimer.Start();
    }

    private void OnIdleTimerTick(object? sender, EventArgs e)
    {
        var timeSinceLastActivity = DateTime.Now - _lastActivityTime;
        
        // If idle for more than the timeout, calculate statistics
        if (timeSinceLastActivity.TotalSeconds >= IdleTimeoutSeconds && _currentProject != null)
        {
            CalculateStatistics();
        }
    }

    private void RecordActivity()
    {
        _lastActivityTime = DateTime.Now;
    }

    private void CalculateStatistics()
    {
        if (_currentProject == null)
            return;

        try
        {
            // Ensure all documents have ProjectDirectory set if we have a project path
            if (!string.IsNullOrEmpty(CurrentProjectPath))
            {
                var projectDirectory = Path.GetDirectoryName(CurrentProjectPath) ?? Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty;
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
            if (SelectedProjectItem?.Document != null)
            {
                SelectedProjectItem.Document.Content = EditorText;
            }

            // Ensure Statistics object exists
            if (_currentProject.Metadata.Statistics == null)
            {
                _currentProject.Metadata.Statistics = new ProjectStatistics();
            }

            // Calculate statistics
            _projectService.UpdateProjectStatistics(_currentProject);
            
            // Update observable properties for status bar
            UpdateStatisticsDisplay();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error calculating statistics: {ex.Message}");
        }
    }

    private void UpdateStatisticsDisplay()
    {
        if (_currentProject?.Metadata?.Statistics == null)
        {
            TotalWordCount = 0;
            TotalCharacterCount = 0;
            TotalPageCount = 0;
            StatisticsText = "No statistics available";
            return;
        }

        var stats = _currentProject.Metadata.Statistics;
        TotalWordCount = stats.TotalWordCount;
        TotalCharacterCount = stats.TotalCharacterCount;
        TotalPageCount = stats.TotalPageCount;
        
        // Format statistics text for status bar
        // Show word count, character count, and page count
        StatisticsText = $"Words: {TotalWordCount:N0} | Characters: {TotalCharacterCount:N0} | Pages: {TotalPageCount}";
        
        // If there's a word count target, show progress
        if (_currentProject.Metadata.WordCountTargets?.TargetWordCount > 0)
        {
            var progress = (double)TotalWordCount / _currentProject.Metadata.WordCountTargets.TargetWordCount * 100;
            StatisticsText += $" | Progress: {progress:F1}%";
        }
    }
    
    public void Dispose()
    {
        _idleTimer?.Stop();
        _idleTimer = null;
    }

    public void SetParentWindow(Window window)
    {
        _parentWindow = window;
        _fileDialogService = new FileDialogService(window);
    }

    private void InitializeProjectTree()
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
        ProjectTreeItems.Add(root);
        
        // Initialize statistics display
        UpdateStatisticsDisplay();
    }

    [RelayCommand]
    private async Task NewProject()
    {
        if (HasUnsavedChanges)
        {
            // TODO: Show confirmation dialog
        }

        var project = _projectService.CreateNewProject("New Project");
        _currentProject = project;
        CurrentProjectPath = string.Empty;
        EditorText = string.Empty;
        CurrentFilePath = string.Empty;
        HasUnsavedChanges = false;
        LoadProjectIntoTree(project);
        
        // Index the project for search
        _searchIndexService.IndexProject(project);
        
        // Calculate initial statistics for new project
        RecordActivity();
        CalculateStatistics();
    }

    [RelayCommand]
    private async Task OpenProject()
    {
        if (_fileDialogService == null)
            return;

        try
        {
            var filePath = await _fileDialogService.OpenFileDialogAsync(
                "Open Project",
                _fileDialogService.GetProjectFileTypes());

            if (!string.IsNullOrEmpty(filePath))
            {
                var project = _projectService.LoadProject(filePath);
                _currentProject = project;
                CurrentProjectPath = filePath;
                EditorText = string.Empty;
                CurrentFilePath = string.Empty;
                LoadProjectIntoTree(project);
                HasUnsavedChanges = false;
                
                // Index the project for search
                _searchIndexService.IndexProject(project);
                
                // Add to MRU list
                _mruService.AddProject(filePath, project.Name);
                UpdateRecentProjectsList();
                
                // Calculate initial statistics
                RecordActivity(); // Reset activity timer
                CalculateStatistics();
            }
        }
        catch (Exception ex)
        {
            // TODO: Show error dialog
            System.Diagnostics.Debug.WriteLine($"Error opening project: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            // Clear the editor to show something went wrong
            EditorText = $"Error loading project: {ex.Message}";
            CurrentProjectPath = string.Empty;
        }
    }

    [RelayCommand]
    private async Task SaveProject()
    {
        if (string.IsNullOrEmpty(CurrentProjectPath))
        {
            await SaveProjectAs();
            return;
        }

        try
        {
            var project = BuildProjectFromCurrentState();
            _projectService.SaveProject(project, CurrentProjectPath);
            _currentProject = project;
            HasUnsavedChanges = false;
            
            // Add to MRU list
            _mruService.AddProject(CurrentProjectPath, project.Name);
            UpdateRecentProjectsList();
        }
        catch (Exception ex)
        {
            // TODO: Show error dialog
            System.Diagnostics.Debug.WriteLine($"Error saving project: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveProjectAs()
    {
        if (_fileDialogService == null)
            return;

        try
        {
            var filePath = await _fileDialogService.SaveFileDialogAsync(
                "Save Project As",
                "project.json",
                _fileDialogService.GetProjectFileTypes());

            if (!string.IsNullOrEmpty(filePath))
            {
                var project = BuildProjectFromCurrentState();
                _projectService.SaveProject(project, filePath);
                _currentProject = project;
                CurrentProjectPath = filePath;
                HasUnsavedChanges = false;
                
                // Add to MRU list
                _mruService.AddProject(filePath, project.Name);
                UpdateRecentProjectsList();
            }
        }
        catch (Exception ex)
        {
            // TODO: Show error dialog
            System.Diagnostics.Debug.WriteLine($"Error saving project: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        if (_fileDialogService == null)
            return;

        if (HasUnsavedChanges)
        {
            // TODO: Show confirmation dialog
        }

        try
        {
            var filePath = await _fileDialogService.OpenFileDialogAsync(
                "Open File",
                _fileDialogService.GetTextFileTypes());

            if (!string.IsNullOrEmpty(filePath))
            {
                var content = await _fileService.LoadFileAsync(filePath);
                EditorText = content;
                CurrentFilePath = filePath;
                HasUnsavedChanges = false;
            }
        }
        catch (Exception ex)
        {
            // TODO: Show error dialog
            System.Diagnostics.Debug.WriteLine($"Error opening file: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveFile()
    {
        if (string.IsNullOrEmpty(CurrentFilePath))
        {
            await SaveFileAs();
            return;
        }

        try
        {
            await _fileService.SaveFileAsync(CurrentFilePath, EditorText);
            HasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
            // TODO: Show error dialog
            System.Diagnostics.Debug.WriteLine($"Error saving file: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task SaveFileAs()
    {
        if (_fileDialogService == null)
            return;

        try
        {
            var suggestedFileName = string.IsNullOrEmpty(CurrentFilePath)
                ? "document.txt"
                : Path.GetFileName(CurrentFilePath);

            var filePath = await _fileDialogService.SaveFileDialogAsync(
                "Save File As",
                suggestedFileName,
                _fileDialogService.GetTextFileTypes());

            if (!string.IsNullOrEmpty(filePath))
            {
                await _fileService.SaveFileAsync(filePath, EditorText);
                CurrentFilePath = filePath;
                HasUnsavedChanges = false;
            }
        }
        catch (Exception ex)
        {
            // TODO: Show error dialog
            System.Diagnostics.Debug.WriteLine($"Error saving file: {ex.Message}");
        }
    }

    partial void OnEditorTextChanged(string value)
    {
        HasUnsavedChanges = true;
        RecordActivity(); // Record activity when user types
        
        // Update find/replace document text
        FindReplaceViewModel.SetDocumentText(value);
        
        // Update search index when content changes
        if (SelectedProjectItem?.Document != null)
        {
            SelectedProjectItem.Document.Content = value;
            _searchIndexService.UpdateDocument(SelectedProjectItem.Document);
        }
        
        // Update rendered markdown if in preview mode
        if (IsPreviewMode)
        {
            UpdateRenderedMarkdown();
        }
    }

    partial void OnIsPreviewModeChanged(bool value)
    {
        if (value)
        {
            // Use Dispatcher to ensure UI is ready before updating
            Dispatcher.UIThread.Post(() =>
            {
                UpdateRenderedMarkdown();
            }, DispatcherPriority.Loaded);
        }
    }

    private void UpdateRenderedMarkdown()
    {
        var blocks = RenderMarkdownToBlocks(EditorText);
        RenderedMarkdownBlocks.Clear();
        
        // Add blocks, but skip empty paragraphs unless they're needed for spacing
        foreach (var block in blocks)
        {
            // Skip empty paragraphs unless it's the only content
            if (block.Type == MarkdownBlockType.Paragraph && string.IsNullOrWhiteSpace(block.Content) && blocks.Count > 1)
            {
                continue;
            }
            RenderedMarkdownBlocks.Add(block);
        }
        
        // If no blocks were added and we have content, add a default paragraph
        if (RenderedMarkdownBlocks.Count == 0 && !string.IsNullOrWhiteSpace(EditorText))
        {
            RenderedMarkdownBlocks.Add(new MarkdownBlock 
            { 
                Type = MarkdownBlockType.Paragraph, 
                Content = EditorText 
            });
        }
    }

    private List<MarkdownBlock> RenderMarkdownToBlocks(string markdown)
    {
        var blocks = new List<MarkdownBlock>();
        if (string.IsNullOrEmpty(markdown))
            return blocks;

        var lines = markdown.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        bool inCodeBlock = false;
        var codeBlockLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimEnd();

            // Code blocks
            if (trimmedLine.StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    if (codeBlockLines.Count > 0)
                    {
                        blocks.Add(new MarkdownBlock 
                        { 
                            Type = MarkdownBlockType.CodeBlock, 
                            Content = string.Join("\n", codeBlockLines) 
                        });
                    }
                    codeBlockLines.Clear();
                    inCodeBlock = false;
                }
                else
                {
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBlockLines.Add(line);
                continue;
            }

            // Headers
            if (trimmedLine.StartsWith("# "))
            {
                var headingBlock = new MarkdownBlock 
                { 
                    Type = MarkdownBlockType.Heading1, 
                    Content = ProcessInlineMarkdownText(trimmedLine.Substring(2), out var h1Links)
                };
                headingBlock.Links = h1Links;
                blocks.Add(headingBlock);
                continue;
            }
            if (trimmedLine.StartsWith("## "))
            {
                var headingBlock = new MarkdownBlock 
                { 
                    Type = MarkdownBlockType.Heading2, 
                    Content = ProcessInlineMarkdownText(trimmedLine.Substring(3), out var h2Links)
                };
                headingBlock.Links = h2Links;
                blocks.Add(headingBlock);
                continue;
            }
            if (trimmedLine.StartsWith("### "))
            {
                var headingBlock = new MarkdownBlock 
                { 
                    Type = MarkdownBlockType.Heading3, 
                    Content = ProcessInlineMarkdownText(trimmedLine.Substring(4), out var h3Links)
                };
                headingBlock.Links = h3Links;
                blocks.Add(headingBlock);
                continue;
            }
            if (trimmedLine.StartsWith("#### "))
            {
                var headingBlock = new MarkdownBlock 
                { 
                    Type = MarkdownBlockType.Heading4, 
                    Content = ProcessInlineMarkdownText(trimmedLine.Substring(5), out var h4Links)
                };
                headingBlock.Links = h4Links;
                blocks.Add(headingBlock);
                continue;
            }

            // Lists
            if (trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("* "))
            {
                var listBlock = new MarkdownBlock 
                { 
                    Type = MarkdownBlockType.ListItem, 
                    Content = ProcessInlineMarkdownText(trimmedLine.Substring(2), out var listLinks)
                };
                listBlock.Links = listLinks;
                blocks.Add(listBlock);
                continue;
            }

            // Empty line
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Paragraph, Content = "" });
                continue;
            }

            // Regular paragraph
            var paragraphBlock = new MarkdownBlock 
            { 
                Type = MarkdownBlockType.Paragraph, 
                Content = ProcessInlineMarkdownText(trimmedLine, out var links)
            };
            paragraphBlock.Links = links;
            blocks.Add(paragraphBlock);
        }

        if (inCodeBlock && codeBlockLines.Count > 0)
        {
            blocks.Add(new MarkdownBlock 
            { 
                Type = MarkdownBlockType.CodeBlock, 
                Content = string.Join("\n", codeBlockLines) 
            });
        }

        return blocks;
    }

    private string ProcessInlineMarkdownText(string text, out List<DocumentLink> links)
    {
        links = new List<DocumentLink>();
        
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Parse double bracket links first (before processing other markdown)
        var documentLinks = _documentLinkService.ParseLinks(text);
        
        // Resolve links if we have a current project
        if (_currentProject != null)
        {
            _documentLinkService.ResolveLinks(documentLinks, _currentProject);
        }
        
        links = documentLinks;

        // Store original link positions before text processing
        var linkPositions = new Dictionary<DocumentLink, int>();
        foreach (var link in documentLinks)
        {
            linkPositions[link] = link.StartIndex;
        }

        // Remove markdown syntax for now - in a full implementation we'd parse and format
        // Bold: **text** -> text (will be formatted by TextBlock styling)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"__(.+?)__", "$1");
        
        // Italic: *text* -> text
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<!\*)\*(?!\*)([^\*]+?)(?<!\*)\*(?!\*)", "$1");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<!_)_([^_]+?)_(?!_)", "$1");
        
        // Code: `code` -> code
        text = System.Text.RegularExpressions.Regex.Replace(text, @"`([^`]+?)`", "$1");
        
        // Links: [text](url) -> text (url)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]+?)\]\(([^\)]+?)\)", "$1 ($2)");
        
        // Replace double bracket links with display text (in reverse order to maintain indices)
        foreach (var link in documentLinks.OrderByDescending(l => l.StartIndex))
        {
            var originalStart = link.StartIndex;
            var originalLength = link.Length;
            
            if (originalStart + originalLength <= text.Length)
            {
                text = text.Substring(0, originalStart) + 
                      link.DisplayText + 
                      text.Substring(originalStart + originalLength);
                
                // Update link position to point to the replaced display text
                link.StartIndex = originalStart;
                link.Length = link.DisplayText.Length;
            }
        }
        
        return text;
    }

    private string RenderMarkdownToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        var html = new System.Text.StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: 'Inter', 'Segoe UI', sans-serif; font-size: 14px; line-height: 1.6; padding: 20px; margin: 0; }");
        html.AppendLine("h1 { font-size: 2em; margin-top: 0.67em; margin-bottom: 0.67em; font-weight: bold; }");
        html.AppendLine("h2 { font-size: 1.5em; margin-top: 0.83em; margin-bottom: 0.83em; font-weight: bold; }");
        html.AppendLine("h3 { font-size: 1.17em; margin-top: 1em; margin-bottom: 1em; font-weight: bold; }");
        html.AppendLine("h4 { font-size: 1em; margin-top: 1.33em; margin-bottom: 1.33em; font-weight: bold; }");
        html.AppendLine("p { margin-top: 1em; margin-bottom: 1em; }");
        html.AppendLine("ul, ol { margin-top: 1em; margin-bottom: 1em; padding-left: 2em; }");
        html.AppendLine("li { margin-top: 0.5em; margin-bottom: 0.5em; }");
        html.AppendLine("code { background-color: #f4f4f4; padding: 2px 4px; border-radius: 3px; font-family: 'Consolas', monospace; }");
        html.AppendLine("pre { background-color: #f4f4f4; padding: 10px; border-radius: 5px; overflow-x: auto; }");
        html.AppendLine("pre code { background-color: transparent; padding: 0; }");
        html.AppendLine("a { color: #0066cc; text-decoration: none; }");
        html.AppendLine("a:hover { text-decoration: underline; }");
        html.AppendLine("strong { font-weight: bold; }");
        html.AppendLine("em { font-style: italic; }");
        html.AppendLine("</style>");
        html.AppendLine("</head><body>");

        var lines = markdown.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        bool inCodeBlock = false;
        bool inList = false;
        bool inParagraph = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimEnd();

            // Code blocks
            if (trimmedLine.StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    html.AppendLine("</code></pre>");
                    inCodeBlock = false;
                }
                else
                {
                    var language = trimmedLine.Length > 3 ? trimmedLine.Substring(3).Trim() : "";
                    html.AppendLine($"<pre><code class=\"language-{HtmlEncode(language)}\">");
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                html.AppendLine(HtmlEncode(line));
                continue;
            }

            // Headers
            if (trimmedLine.StartsWith("# "))
            {
                CloseParagraph(ref inParagraph, html);
                html.AppendLine($"<h1>{ProcessInlineMarkdown(trimmedLine.Substring(2))}</h1>");
                continue;
            }
            if (trimmedLine.StartsWith("## "))
            {
                CloseParagraph(ref inParagraph, html);
                html.AppendLine($"<h2>{ProcessInlineMarkdown(trimmedLine.Substring(3))}</h2>");
                continue;
            }
            if (trimmedLine.StartsWith("### "))
            {
                CloseParagraph(ref inParagraph, html);
                html.AppendLine($"<h3>{ProcessInlineMarkdown(trimmedLine.Substring(4))}</h3>");
                continue;
            }
            if (trimmedLine.StartsWith("#### "))
            {
                CloseParagraph(ref inParagraph, html);
                html.AppendLine($"<h4>{ProcessInlineMarkdown(trimmedLine.Substring(5))}</h4>");
                continue;
            }

            // Lists
            if (trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("* "))
            {
                CloseParagraph(ref inParagraph, html);
                if (!inList)
                {
                    html.AppendLine("<ul>");
                    inList = true;
                }
                var listItem = ProcessInlineMarkdown(trimmedLine.Substring(2));
                html.AppendLine($"<li>{listItem}</li>");
                continue;
            }

            // Close list if needed
            if (inList && string.IsNullOrWhiteSpace(trimmedLine))
            {
                html.AppendLine("</ul>");
                inList = false;
                continue;
            }

            // Empty line
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                CloseParagraph(ref inParagraph, html);
                continue;
            }

            // Regular paragraph
            if (!inParagraph)
            {
                html.Append("<p>");
                inParagraph = true;
            }
            else
            {
                html.Append(" ");
            }
            html.Append(ProcessInlineMarkdown(trimmedLine));
        }

        CloseParagraph(ref inParagraph, html);
        if (inList)
        {
            html.AppendLine("</ul>");
        }
        if (inCodeBlock)
        {
            html.AppendLine("</code></pre>");
        }

        html.AppendLine("</body></html>");
        return html.ToString();
    }

    private void CloseParagraph(ref bool inParagraph, System.Text.StringBuilder html)
    {
        if (inParagraph)
        {
            html.AppendLine("</p>");
            inParagraph = false;
        }
    }

    private string ProcessInlineMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Escape HTML first
        text = HtmlEncode(text);

        // Bold: **text** or __text__
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"__(.+?)__", "<strong>$1</strong>");

        // Italic: *text* or _text_ (but not if part of **)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<![\*_])(?<!\*)\*(?!\*)([^\*]+?)(?<!\*)\*(?!\*)(?![\*_])", "<em>$1</em>");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<!_)_([^_]+?)_(?!_)", "<em>$1</em>");

        // Code: `code`
        text = System.Text.RegularExpressions.Regex.Replace(text, @"`([^`]+?)`", "<code>$1</code>");

        // Links: [text](url)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]+?)\]\(([^\)]+?)\)", "<a href=\"$2\">$1</a>");

        return text;
    }

    private string HtmlEncode(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    [RelayCommand]
    private void ToggleViewMode()
    {
        IsSourceMode = !IsSourceMode;
        IsPreviewMode = !IsPreviewMode;
        
        if (IsPreviewMode)
        {
            UpdateRenderedMarkdown();
        }
    }

    partial void OnSelectedProjectItemChanged(ProjectTreeItemViewModel? value)
    {
        RecordActivity(); // Record activity when selection changes
        
        if (value?.Document != null)
        {
            try
            {
                var document = value.Document;
                
                // Ensure ProjectDirectory is set if we have a current project path
                if (string.IsNullOrEmpty(document.ProjectDirectory) && !string.IsNullOrEmpty(CurrentProjectPath))
                {
                    var projectDirectory = Path.GetDirectoryName(CurrentProjectPath) ?? Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty;
                    document.ProjectDirectory = projectDirectory;
                }
                
                // Load content - this will get from _content if set, or load from file if ProjectDirectory and ContentFilePath are set
                // For unsaved projects, content should be in _content already
                var content = document.Content;
                
                // If content is empty and we have a ContentFilePath but ProjectDirectory is not set,
                // try to set ProjectDirectory from CurrentProjectPath if available
                if (string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(document.ContentFilePath) && string.IsNullOrEmpty(document.ProjectDirectory))
                {
                    if (!string.IsNullOrEmpty(CurrentProjectPath))
                    {
                        var projectDirectory = Path.GetDirectoryName(CurrentProjectPath) ?? Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty;
                        document.ProjectDirectory = projectDirectory;
                        // Try loading again
                        content = document.Content;
                    }
                }
                
                EditorText = content;
                CurrentFilePath = document.ContentFilePath;
                HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading document content: {ex.Message}");
                EditorText = $"Error loading document: {ex.Message}";
                HasUnsavedChanges = false;
            }
        }
        else if (value != null && value.Children.Any())
        {
            // Selected item is a folder, clear the editor
            EditorText = string.Empty;
            CurrentFilePath = string.Empty;
            HasUnsavedChanges = false;
        }
    }

    public void LoadProjectIntoTree(Project project)
    {
        _currentProject = project;
        
        // Update autocomplete documents when project is loaded
        UpdateAutocompleteDocuments();
        ProjectTreeItems.Clear();
        
        // Ensure ProjectDirectory is set on all documents if we have a project path
        if (!string.IsNullOrEmpty(CurrentProjectPath))
        {
            var projectDirectory = Path.GetDirectoryName(CurrentProjectPath) ?? Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty;
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
            var typeFolder = new ProjectTreeItemViewModel
            {
                Name = GetTypeFolderName(docType),
                Icon = "📁"
            };

            // Mark folder types
            if (docType == DocumentType.Character)
            {
                typeFolder.IsCharactersFolder = true;
            }
            else if (docType == DocumentType.Location)
            {
                typeFolder.IsLocationsFolder = true;
            }
            else if (docType == DocumentType.Research)
            {
                typeFolder.IsResearchFolder = true;
            }
            else if (docType == DocumentType.Note)
            {
                typeFolder.IsNotesFolder = true;
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

            root.Children.Add(typeFolder);
        }

        ProjectTreeItems.Add(root);
        
        // Update statistics display after loading project
        UpdateStatisticsDisplay();
    }

    private string GetTypeFolderName(DocumentType type)
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

    private Project BuildProjectFromCurrentState()
    {
        // Start with the current project if it exists, otherwise create a new one
        var project = _currentProject ?? new Project
        {
            Name = ProjectTreeItems.FirstOrDefault()?.Name ?? "Untitled Project",
            FilePath = CurrentProjectPath
        };

        // Update the project name from the tree
        project.Name = ProjectTreeItems.FirstOrDefault()?.Name ?? "Untitled Project";
        project.FilePath = CurrentProjectPath;

        // If there's a selected document, update its content with the editor text
        if (SelectedProjectItem?.Document != null)
        {
            SelectedProjectItem.Document.Content = EditorText;
        }

        // Ensure all documents from the current project are included
        // (they should already be there if _currentProject exists)
        if (_currentProject == null)
        {
            // If no current project, we need to build it from the tree
            // This shouldn't normally happen, but handle it as a fallback
            foreach (var rootItem in ProjectTreeItems)
            {
                CollectDocumentsFromTree(rootItem, project.Documents);
            }
        }

        return project;
    }

    private void CollectDocumentsFromTree(ProjectTreeItemViewModel item, List<Document> documents)
    {
        if (item.Document != null)
        {
            // Update content if this is the selected item
            if (item == SelectedProjectItem)
            {
                item.Document.Content = EditorText;
            }
            
            // Add document if not already in the list
            if (!documents.Any(d => d.Id == item.Document.Id))
            {
                documents.Add(item.Document);
            }
        }

        foreach (var child in item.Children)
        {
            CollectDocumentsFromTree(child, documents);
        }
    }

    private string GetIconForDocumentType(DocumentType type)
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

    [RelayCommand]
    private void Exit()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }

    [RelayCommand]
    private void Undo()
    {
        // TODO: Implement undo
    }

    [RelayCommand]
    private void Redo()
    {
        // TODO: Implement redo
    }

    [RelayCommand]
    private void Cut()
    {
        // TODO: Implement cut
    }

    [RelayCommand]
    private void Copy()
    {
        // TODO: Implement copy
    }

    [RelayCommand]
    private void Paste()
    {
        // TODO: Implement paste
    }

    [RelayCommand]
    private void ToggleProjectTreeSide()
    {
        // Toggle between left and right
        if (IsProjectTreeOnLeft)
        {
            IsProjectTreeOnLeft = false;
            IsProjectTreeOnRight = true;
        }
        else
        {
            IsProjectTreeOnLeft = true;
            IsProjectTreeOnRight = false;
        }
    }

    [RelayCommand]
    private void ShowWordCount()
    {
        // TODO: Implement word count display
    }

    [RelayCommand]
    private void ShowCharacterCount()
    {
        // TODO: Implement character count display
    }

    [RelayCommand]
    private void ShowAbout()
    {
        // TODO: Implement about dialog
    }

    [RelayCommand]
    private void ShowPluginManager()
    {
        var pluginManagerWindow = new PluginManagerWindow(new PluginManagerViewModel(_pluginManager));
        pluginManagerWindow.Show();
    }

    [RelayCommand]
    private void ShowLocalFind()
    {
        if (!IsSourceMode)
            return; // Only show find in source mode
        
        FindReplaceViewModel.IsVisible = true;
        FindReplaceViewModel.SetDocumentText(EditorText);
        
        // Focus the find text box after a brief delay
        Dispatcher.UIThread.Post(() =>
        {
            FocusFindTextBox();
        }, DispatcherPriority.Loaded);
    }

    private void FocusFindTextBox()
    {
        if (_parentWindow is Views.MainWindow mainWindow)
        {
            var findBar = mainWindow.FindControl<Views.FindReplaceBar>("findReplaceBar");
            var findTextBox = findBar?.FindControl<TextBox>("findTextBox");
            findTextBox?.Focus();
            findTextBox?.SelectAll();
        }
    }

    [RelayCommand]
    private void ShowSearch()
    {
        if (_currentProject == null)
        {
            // TODO: Show message that no project is loaded
            return;
        }

        // Close existing search window if open
        _searchWindow?.Close();

        var searchViewModel = new SearchViewModel(_searchIndexService, _projectService);
        searchViewModel.SetProject(_currentProject);
        
        _searchWindow = new SearchWindow(searchViewModel)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        _searchWindow.NavigateToResultRequested += OnNavigateToSearchResult;
        
        if (_parentWindow != null)
        {
            _searchWindow.ShowDialog(_parentWindow);
        }
        else
        {
            _searchWindow.Show();
        }
        
        _searchWindow.Closed += (s, e) => _searchWindow = null;
    }

    private void OnNavigateToSearchResult(SearchResult result)
    {
        if (_currentProject == null)
            return;

        // Find the document in the project tree
        var document = _currentProject.Documents.FirstOrDefault(d => d.Id == result.DocumentId);
        if (document == null)
            return;

        // Find the corresponding tree item
        ProjectTreeItemViewModel? targetItem = null;
        foreach (var rootItem in ProjectTreeItems)
        {
            targetItem = FindTreeItemByDocument(rootItem, document);
            if (targetItem != null)
                break;
        }

        if (targetItem != null)
        {
            // Select the document in the tree
            SelectedProjectItem = targetItem;
            
            // Scroll to the first match if possible
            // This would require additional UI work to scroll the editor
        }
    }

    private ProjectTreeItemViewModel? FindTreeItemByDocument(ProjectTreeItemViewModel item, Document document)
    {
        if (item.Document?.Id == document.Id)
            return item;

        foreach (var child in item.Children)
        {
            var found = FindTreeItemByDocument(child, document);
            if (found != null)
                return found;
        }

        return null;
    }

    [RelayCommand]
    private void ShowProjectProperties()
    {
        if (_currentProject == null)
        {
            // Create a new project if none exists
            _currentProject = _projectService.CreateNewProject("Untitled Project");
        }

        var viewModel = new ProjectPropertiesViewModel(
            _currentProject.Metadata ?? new ProjectMetadata(),
            (updatedMetadata) =>
            {
                _currentProject.Metadata = updatedMetadata;
                _currentProject.Name = updatedMetadata.Title;
                
                // Update the root node name in the tree
                if (ProjectTreeItems.Count > 0)
                {
                    ProjectTreeItems[0].Name = updatedMetadata.Title;
                }

                // If project is saved, update the saved file
                if (!string.IsNullOrEmpty(CurrentProjectPath))
                {
                    try
                    {
                        _projectService.SaveProject(_currentProject, CurrentProjectPath);
                        HasUnsavedChanges = false;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error saving project properties: {ex.Message}");
                    }
                }
                else
                {
                    HasUnsavedChanges = true;
                }
            });

        var dialog = new ProjectPropertiesWindow(viewModel)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        if (_parentWindow != null)
        {
            dialog.ShowDialog(_parentWindow);
        }
        else
        {
            dialog.Show();
        }
    }

    [RelayCommand]
    private void AddChapter(ProjectTreeItemViewModel? folderNode)
    {
        if (folderNode == null)
        {
            // Find the Manuscript folder if no folder specified
            folderNode = ProjectTreeItems.FirstOrDefault()?.Children
                .FirstOrDefault(c => c.IsManuscriptFolder);
        }

        // Check if this is Manuscript folder or a subfolder that can contain chapters
        if (folderNode == null || (!folderNode.IsManuscriptFolder && folderNode.FolderDocumentType != DocumentType.Chapter))
        {
            // Fallback: try to find Manuscript folder
            folderNode = ProjectTreeItems.FirstOrDefault()?.Children
                .FirstOrDefault(c => c.IsManuscriptFolder);
            if (folderNode == null)
                return;
        }

        // Ensure we have a current project
        if (_currentProject == null)
        {
            _currentProject = _projectService.CreateNewProject(
                ProjectTreeItems.FirstOrDefault()?.Name ?? "Untitled Project");
        }

        // Generate a default chapter name based on existing chapters
        var existingChapters = _currentProject.Documents
            .Where(d => d.Type == DocumentType.Chapter)
            .ToList();
        
        var chapterNumber = existingChapters.Count + 1;
        var chapterTitle = $"Chapter {chapterNumber}";
        
        // Ensure unique name
        while (_currentProject.Documents.Any(d => d.Title == chapterTitle))
        {
            chapterNumber++;
            chapterTitle = $"Chapter {chapterNumber}";
        }

        // Determine folder path if in a subfolder
        var folderPath = folderNode.IsManuscriptFolder ? string.Empty : folderNode.FolderPath;

        // Determine order (number of existing chapters in this folder)
        var existingChaptersInFolder = folderNode.Children
            .Where(c => c.Document?.Type == DocumentType.Chapter)
            .Count();

        // Create new chapter document
        var newChapter = new Document
        {
            Title = chapterTitle,
            Type = DocumentType.Chapter,
            FolderPath = folderPath,
            Content = string.Empty,
            Order = existingChaptersInFolder
        };

        // Add to project
        _currentProject.Documents.Add(newChapter);

        // Create a new chapter node and add it directly to the folder
        var newChapterNode = new ProjectTreeItemViewModel
        {
            Name = chapterTitle,
            Icon = GetIconForDocumentType(newChapter.Type),
            Document = newChapter
        };
        
        // Expand the parent folder if it's not already expanded
        folderNode.IsExpanded = true;
        
        // Add to the folder's children
        folderNode.Children.Add(newChapterNode);
        
        // Trigger rename mode for the newly added chapter
        StartRename(newChapterNode);

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void AddScene(ProjectTreeItemViewModel? chapterNode)
    {
        if (chapterNode == null)
        {
            chapterNode = SelectedProjectItem;
        }

        if (chapterNode == null || !chapterNode.IsChapter || chapterNode.Document == null)
            return;

        // Ensure we have a current project
        if (_currentProject == null)
        {
            _currentProject = _projectService.CreateNewProject(
                ProjectTreeItems.FirstOrDefault()?.Name ?? "Untitled Project");
        }

        var chapter = chapterNode.Document;

        // Generate a default scene name based on existing scenes in this chapter
        var existingScenes = _currentProject.Documents
            .Where(d => d.Type == DocumentType.Scene && d.ParentId == chapter.Id)
            .ToList();
        
        var sceneNumber = existingScenes.Count + 1;
        var sceneTitle = $"Scene {sceneNumber}";
        
        // Ensure unique name within the chapter
        while (_currentProject.Documents.Any(d => d.Type == DocumentType.Scene && d.ParentId == chapter.Id && d.Title == sceneTitle))
        {
            sceneNumber++;
            sceneTitle = $"Scene {sceneNumber}";
        }

        // Determine order (number of existing scenes in this chapter)
        var existingScenesInChapter = chapterNode.Children
            .Where(c => c.Document?.Type == DocumentType.Scene)
            .Count();

        // Create new scene document
        var newScene = new Document
        {
            Title = sceneTitle,
            Type = DocumentType.Scene,
            ParentId = chapter.Id,
            Content = string.Empty,
            Order = existingScenesInChapter
        };

        // Add to project
        _currentProject.Documents.Add(newScene);

        // Create a new scene node and add it directly to the chapter node's children
        var newSceneNode = new ProjectTreeItemViewModel
        {
            Name = sceneTitle,
            Icon = GetIconForDocumentType(newScene.Type),
            Document = newScene
        };
        
        // Expand the parent chapter if it's not already expanded
        chapterNode.IsExpanded = true;
        
        // Add to the chapter node's children
        chapterNode.Children.Add(newSceneNode);
        
        // Trigger rename mode for the newly added scene
        StartRename(newSceneNode);

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void AddCharacter(ProjectTreeItemViewModel? folderNode)
    {
        if (folderNode == null)
        {
            folderNode = SelectedProjectItem;
        }

        // Check if this is a Characters folder or a subfolder that can contain characters
        if (folderNode == null || (!folderNode.IsCharactersFolder && folderNode.FolderDocumentType != DocumentType.Character))
            return;

        // Ensure we have a current project
        if (_currentProject == null)
        {
            _currentProject = _projectService.CreateNewProject(
                ProjectTreeItems.FirstOrDefault()?.Name ?? "Untitled Project");
        }

        // Generate a default character name based on existing characters
        var existingCharacters = _currentProject.Documents
            .Where(d => d.Type == DocumentType.Character)
            .ToList();
        
        var characterNumber = existingCharacters.Count + 1;
        var characterTitle = $"Character {characterNumber}";
        
        // Ensure unique name
        while (_currentProject.Documents.Any(d => d.Type == DocumentType.Character && d.Title == characterTitle))
        {
            characterNumber++;
            characterTitle = $"Character {characterNumber}";
        }

        // Determine folder path if in a subfolder
        var folderPath = folderNode.IsCharactersFolder ? string.Empty : folderNode.FolderPath;

        // Determine order (number of existing characters in this folder)
        var existingCharactersInFolder = folderNode.Children
            .Where(c => c.Document?.Type == DocumentType.Character)
            .Count();

        // Create new character document
        var newCharacter = new Document
        {
            Title = characterTitle,
            Type = DocumentType.Character,
            FolderPath = folderPath,
            Content = string.Empty,
            Order = existingCharactersInFolder
        };

        // Add to project
        _currentProject.Documents.Add(newCharacter);

        // Create a new character node and add it directly to the Characters folder
        var newCharacterNode = new ProjectTreeItemViewModel
        {
            Name = characterTitle,
            Icon = GetIconForDocumentType(newCharacter.Type),
            Document = newCharacter
        };
        
        // Expand the parent folder if it's not already expanded
        folderNode.IsExpanded = true;
        
        // Add to the Characters folder's children
        folderNode.Children.Add(newCharacterNode);
        
        // Trigger rename mode for the newly added character
        StartRename(newCharacterNode);

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void AddLocation(ProjectTreeItemViewModel? folderNode)
    {
        if (folderNode == null)
        {
            folderNode = SelectedProjectItem;
        }

        // Check if this is a Locations folder or a subfolder that can contain locations
        if (folderNode == null || (!folderNode.IsLocationsFolder && folderNode.FolderDocumentType != DocumentType.Location))
            return;

        // Ensure we have a current project
        if (_currentProject == null)
        {
            _currentProject = _projectService.CreateNewProject(
                ProjectTreeItems.FirstOrDefault()?.Name ?? "Untitled Project");
        }

        // Generate a default location name based on existing locations
        var existingLocations = _currentProject.Documents
            .Where(d => d.Type == DocumentType.Location)
            .ToList();
        
        var locationNumber = existingLocations.Count + 1;
        var locationTitle = $"Location {locationNumber}";
        
        // Ensure unique name
        while (_currentProject.Documents.Any(d => d.Type == DocumentType.Location && d.Title == locationTitle))
        {
            locationNumber++;
            locationTitle = $"Location {locationNumber}";
        }

        // Determine folder path if in a subfolder
        var folderPath = folderNode.IsLocationsFolder ? string.Empty : folderNode.FolderPath;

        // Determine order (number of existing locations in this folder)
        var existingLocationsInFolder = folderNode.Children
            .Where(c => c.Document?.Type == DocumentType.Location)
            .Count();

        // Create new location document
        var newLocation = new Document
        {
            Title = locationTitle,
            Type = DocumentType.Location,
            FolderPath = folderPath,
            Content = string.Empty,
            Order = existingLocationsInFolder
        };

        // Add to project
        _currentProject.Documents.Add(newLocation);

        // Create a new location node and add it directly to the Locations folder
        var newLocationNode = new ProjectTreeItemViewModel
        {
            Name = locationTitle,
            Icon = GetIconForDocumentType(newLocation.Type),
            Document = newLocation
        };
        
        // Expand the parent folder if it's not already expanded
        folderNode.IsExpanded = true;
        
        // Add to the Locations folder's children
        folderNode.Children.Add(newLocationNode);
        
        // Trigger rename mode for the newly added location
        StartRename(newLocationNode);

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void AddNote(ProjectTreeItemViewModel? folderNode)
    {
        if (folderNode == null)
        {
            folderNode = SelectedProjectItem;
        }

        // Check if this is a Notes folder, Research folder, or a subfolder that can contain notes
        if (folderNode == null || (!folderNode.IsNotesFolder && !folderNode.IsResearchFolder && 
            folderNode.FolderDocumentType != DocumentType.Note && folderNode.FolderDocumentType != DocumentType.Research))
            return;

        // Ensure we have a current project
        if (_currentProject == null)
        {
            _currentProject = _projectService.CreateNewProject(
                ProjectTreeItems.FirstOrDefault()?.Name ?? "Untitled Project");
        }

        // Determine document type based on folder type
        DocumentType noteType;
        if (folderNode.IsResearchFolder || folderNode.FolderDocumentType == DocumentType.Research)
        {
            noteType = DocumentType.Research;
        }
        else
        {
            noteType = DocumentType.Note;
        }

        // Generate a default note name based on existing notes of this type
        var existingNotes = _currentProject.Documents
            .Where(d => d.Type == noteType)
            .ToList();
        
        var noteNumber = existingNotes.Count + 1;
        var noteTitle = $"Note {noteNumber}";
        
        // Ensure unique name
        while (_currentProject.Documents.Any(d => d.Type == noteType && d.Title == noteTitle))
        {
            noteNumber++;
            noteTitle = $"Note {noteNumber}";
        }

        // Determine folder path if in a subfolder
        var folderPath = (folderNode.IsNotesFolder || folderNode.IsResearchFolder) ? string.Empty : folderNode.FolderPath;

        // Determine order (number of existing notes of this type in this folder)
        var existingNotesInFolder = folderNode.Children
            .Where(c => c.Document?.Type == noteType)
            .Count();

        // Create new note document
        var newNote = new Document
        {
            Title = noteTitle,
            Type = noteType,
            FolderPath = folderPath,
            Content = string.Empty,
            Order = existingNotesInFolder
        };

        // Add to project
        _currentProject.Documents.Add(newNote);

        // Create a new note node and add it directly to the folder
        var newNoteNode = new ProjectTreeItemViewModel
        {
            Name = noteTitle,
            Icon = GetIconForDocumentType(newNote.Type),
            Document = newNote
        };
        
        // Expand the parent folder if it's not already expanded
        folderNode.IsExpanded = true;
        
        // Add to the folder's children
        folderNode.Children.Add(newNoteNode);
        
        // Trigger rename mode for the newly added note
        StartRename(newNoteNode);

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void CreateSubfolder(ProjectTreeItemViewModel? parentFolder)
    {
        if (parentFolder == null)
        {
            parentFolder = SelectedProjectItem;
        }

        // Determine what type of folder this is
        DocumentType? folderType = null;
        if (parentFolder?.IsManuscriptFolder == true)
        {
            folderType = DocumentType.Chapter; // Manuscript subfolders contain chapters
        }
        else if (parentFolder?.IsCharactersFolder == true)
        {
            folderType = DocumentType.Character;
        }
        else if (parentFolder?.IsLocationsFolder == true)
        {
            folderType = DocumentType.Location;
        }
        else if (parentFolder?.IsResearchFolder == true)
        {
            folderType = DocumentType.Research;
        }
        else if (parentFolder?.IsNotesFolder == true)
        {
            folderType = DocumentType.Note;
        }
        else if (parentFolder?.FolderDocumentType != null)
        {
            // It's a subfolder, use the same type
            folderType = parentFolder.FolderDocumentType;
        }

        if (folderType == null)
            return;

        // Generate a default subfolder name
        var existingSubfolders = parentFolder?.Children
            .Where(c => c.IsSubfolder && c.FolderDocumentType == folderType)
            .ToList() ?? new List<ProjectTreeItemViewModel>();
        
        var subfolderNumber = existingSubfolders.Count + 1;
        var subfolderName = $"Subfolder {subfolderNumber}";
        
        // Ensure unique name
        while (parentFolder?.Children.Any(c => c.Name == subfolderName) == true)
        {
            subfolderNumber++;
            subfolderName = $"Subfolder {subfolderNumber}";
        }

        // Build folder path - use sanitized name for filesystem
        var sanitizedSubfolderName = _projectService.SanitizeFileName(subfolderName);
        string folderPath;
        
        if (parentFolder?.IsManuscriptFolder == true || parentFolder?.IsCharactersFolder == true || 
            parentFolder?.IsLocationsFolder == true || parentFolder?.IsResearchFolder == true || parentFolder?.IsNotesFolder == true)
        {
            // Direct subfolder of main type folder
            folderPath = sanitizedSubfolderName;
        }
        else if (!string.IsNullOrEmpty(parentFolder?.FolderPath))
        {
            // Nested subfolder
            folderPath = $"{parentFolder.FolderPath}/{sanitizedSubfolderName}";
        }
        else
        {
            // Fallback
            folderPath = sanitizedSubfolderName;
        }

        // Create subfolder node
        var subfolderNode = new ProjectTreeItemViewModel
        {
            Name = subfolderName,
            Icon = "📁",
            FolderPath = folderPath,
            FolderDocumentType = folderType
        };

        // Expand the parent folder if it's not already expanded
        if (parentFolder != null)
        {
            parentFolder.IsExpanded = true;
        }
        
        // Add to parent folder's children
        parentFolder?.Children.Add(subfolderNode);

        // Trigger rename mode for the newly created subfolder
        StartRename(subfolderNode);

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void MoveDocumentUp(ProjectTreeItemViewModel? item)
    {
        if (item == null || item.Document == null || _currentProject == null)
            return;

        // Find the parent folder
        ProjectTreeItemViewModel? parent = null;
        int currentIndex = -1;

        foreach (var rootItem in ProjectTreeItems)
        {
            var found = FindParentFolder(rootItem, item);
            if (found != null)
            {
                parent = found;
                currentIndex = found.Children.IndexOf(item);
                break;
            }
        }

        if (parent == null || currentIndex <= 0)
            return;

        // Swap with previous item
        var previousItem = parent.Children[currentIndex - 1];
        parent.Children.RemoveAt(currentIndex);
        parent.Children.Insert(currentIndex - 1, item);

        // Update Order properties
        UpdateDocumentOrders(parent);

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void MoveDocumentDown(ProjectTreeItemViewModel? item)
    {
        if (item == null || item.Document == null || _currentProject == null)
            return;

        // Find the parent folder
        ProjectTreeItemViewModel? parent = null;
        int currentIndex = -1;

        foreach (var rootItem in ProjectTreeItems)
        {
            var found = FindParentFolder(rootItem, item);
            if (found != null)
            {
                parent = found;
                currentIndex = found.Children.IndexOf(item);
                break;
            }
        }

        if (parent == null || currentIndex < 0 || currentIndex >= parent.Children.Count - 1)
            return;

        // Swap with next item
        parent.Children.RemoveAt(currentIndex);
        parent.Children.Insert(currentIndex + 1, item);

        // Update Order properties
        UpdateDocumentOrders(parent);

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    private ProjectTreeItemViewModel? FindParentFolder(ProjectTreeItemViewModel parent, ProjectTreeItemViewModel child)
    {
        if (parent.Children.Contains(child))
        {
            return parent;
        }

        foreach (var item in parent.Children)
        {
            var found = FindParentFolder(item, child);
            if (found != null)
                return found;
        }

        return null;
    }

    public void MoveSceneToChapter(ProjectTreeItemViewModel sceneItem, ProjectTreeItemViewModel targetChapter)
    {
        if (sceneItem.Document == null || !sceneItem.IsScene || 
            targetChapter.Document == null || !targetChapter.IsChapter || 
            _currentProject == null)
            return;

        var scene = sceneItem.Document;
        var targetChapterDoc = targetChapter.Document;

        // Find the current parent chapter of the scene
        ProjectTreeItemViewModel? currentChapter = null;
        foreach (var rootItem in ProjectTreeItems)
        {
            var found = FindChapterContainingScene(rootItem, sceneItem);
            if (found != null)
            {
                currentChapter = found;
                break;
            }
        }

        if (currentChapter == null)
            return;

        // Don't do anything if moving to the same chapter
        if (currentChapter == targetChapter)
            return;

        // Remove scene from current chapter's children
        currentChapter.Children.Remove(sceneItem);

        // Update scene's ParentId to point to the new chapter
        scene.ParentId = targetChapterDoc.Id;

        // Update scene's FolderPath to match the chapter's FolderPath
        scene.FolderPath = targetChapterDoc.FolderPath;

        // Determine order (add to end of scenes in target chapter)
        var existingScenesInTargetChapter = targetChapter.Children
            .Where(c => c.Document?.Type == DocumentType.Scene)
            .Count();
        scene.Order = existingScenesInTargetChapter;

        // Add scene to target chapter's children
        targetChapter.Children.Add(sceneItem);

        // Expand target chapter if not already expanded
        targetChapter.IsExpanded = true;

        // Update Order properties for all scenes in both chapters
        UpdateDocumentOrders(currentChapter);
        UpdateDocumentOrders(targetChapter);

        // ContentFilePath will be regenerated on save based on the new ParentId and FolderPath
        // We don't clear it here so SaveProject can detect the path change and move the file

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    private ProjectTreeItemViewModel? FindChapterContainingScene(ProjectTreeItemViewModel parent, ProjectTreeItemViewModel sceneItem)
    {
        // Check if this is a chapter that contains the scene
        if (parent.IsChapter && parent.Children.Contains(sceneItem))
        {
            return parent;
        }

        // Recursively search children
        foreach (var child in parent.Children)
        {
            var found = FindChapterContainingScene(child, sceneItem);
            if (found != null)
                return found;
        }

        return null;
    }

    public void MoveDocumentToFolder(ProjectTreeItemViewModel documentItem, ProjectTreeItemViewModel targetFolder)
    {
        if (documentItem.Document == null || _currentProject == null)
            return;

        var document = documentItem.Document;
        var docType = document.Type;

        // Validate that the document type matches the target folder
        bool isValidTarget = false;
        if (docType == DocumentType.Character && 
            (targetFolder.IsCharactersFolder || 
             (targetFolder.IsSubfolder && targetFolder.FolderDocumentType == DocumentType.Character)))
        {
            isValidTarget = true;
        }
        else if (docType == DocumentType.Location && 
                 (targetFolder.IsLocationsFolder || 
                  (targetFolder.IsSubfolder && targetFolder.FolderDocumentType == DocumentType.Location)))
        {
            isValidTarget = true;
        }
        else if (docType == DocumentType.Research && 
                 (targetFolder.IsResearchFolder || 
                  (targetFolder.IsSubfolder && targetFolder.FolderDocumentType == DocumentType.Research)))
        {
            isValidTarget = true;
        }
        else if (docType == DocumentType.Note && 
                 (targetFolder.IsNotesFolder || targetFolder.IsResearchFolder || 
                  (targetFolder.IsSubfolder && (targetFolder.FolderDocumentType == DocumentType.Note || targetFolder.FolderDocumentType == DocumentType.Research))))
        {
            isValidTarget = true;
        }

        if (!isValidTarget)
            return;

        // Find the current parent folder of the document
        ProjectTreeItemViewModel? currentParent = null;
        foreach (var rootItem in ProjectTreeItems)
        {
            var found = FindParentFolder(rootItem, documentItem);
            if (found != null)
            {
                currentParent = found;
                break;
            }
        }

        if (currentParent == null)
            return;

        // Don't do anything if moving to the same folder
        if (currentParent == targetFolder)
            return;

        // Remove document from current parent's children
        currentParent.Children.Remove(documentItem);

        // Determine the new folder path
        string newFolderPath = string.Empty;
        if (targetFolder.IsSubfolder)
        {
            newFolderPath = targetFolder.FolderPath;
        }
        // If targetFolder is a main type folder (IsCharactersFolder, IsLocationsFolder, etc.), FolderPath stays empty

        // Update document's FolderPath
        document.FolderPath = newFolderPath;

        // Determine order (add to end of documents in target folder)
        var existingDocsInTargetFolder = targetFolder.Children
            .Where(c => c.Document?.Type == docType)
            .Count();
        document.Order = existingDocsInTargetFolder;

        // Add document to target folder's children
        targetFolder.Children.Add(documentItem);

        // Expand target folder if not already expanded
        targetFolder.IsExpanded = true;

        // Update Order properties for all documents in both folders
        UpdateDocumentOrders(currentParent);
        UpdateDocumentOrders(targetFolder);

        // ContentFilePath will be regenerated on save based on the new FolderPath
        // We don't clear it here so SaveProject can detect the path change and move the file

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    public void ReorderDocumentToPosition(ProjectTreeItemViewModel draggedItem, ProjectTreeItemViewModel targetParent, int targetIndex)
    {
        if (draggedItem.Document == null || _currentProject == null)
            return;

        // Find the current parent of the dragged item
        ProjectTreeItemViewModel? currentParent = null;
        int currentIndex = -1;

        foreach (var rootItem in ProjectTreeItems)
        {
            var found = FindParentFolder(rootItem, draggedItem);
            if (found != null)
            {
                currentParent = found;
                currentIndex = found.Children.IndexOf(draggedItem);
                break;
            }
        }

        if (currentParent == null || currentIndex < 0)
            return;

        // Remove from current parent
        currentParent.Children.RemoveAt(currentIndex);

        // Adjust target index if moving within the same parent
        int adjustedTargetIndex = targetIndex;
        if (currentParent == targetParent && currentIndex < targetIndex)
        {
            adjustedTargetIndex--;
        }

        // Insert at new position
        if (adjustedTargetIndex < 0)
            adjustedTargetIndex = 0;
        if (adjustedTargetIndex > targetParent.Children.Count)
            adjustedTargetIndex = targetParent.Children.Count;

        targetParent.Children.Insert(adjustedTargetIndex, draggedItem);

        // Update Order property for all documents in the target parent
        UpdateDocumentOrders(targetParent);

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    private void UpdateDocumentOrders(ProjectTreeItemViewModel folder)
    {
        if (_currentProject == null)
            return;

        int order = 0;
        foreach (var child in folder.Children)
        {
            if (child.Document != null)
            {
                child.Document.Order = order++;
            }
            else if (child.IsSubfolder)
            {
                // Recursively update orders in subfolders
                UpdateDocumentOrders(child);
            }
        }
    }

    [RelayCommand]
    private void RenameChapter(ProjectTreeItemViewModel? item)
    {
        if (item == null)
        {
            item = SelectedProjectItem;
        }

        if (item != null && item.IsChapter)
        {
            StartRename(item);
        }
    }

    [RelayCommand]
    private void RenameItem(ProjectTreeItemViewModel? item)
    {
        if (item == null)
        {
            item = SelectedProjectItem;
        }

        // Allow renaming for documents (chapters, scenes, characters, locations, notes, research) and subfolders
        if (item != null && (item.Document != null || item.IsSubfolder))
        {
            StartRename(item);
        }
    }

    private void StartRename(ProjectTreeItemViewModel item)
    {
        // Cancel any existing rename
        CancelAllRenames();

        // Start rename for this item
        item.IsRenaming = true;
        item.RenameText = item.Name;
    }

    private void CancelAllRenames()
    {
        foreach (var rootItem in ProjectTreeItems)
        {
            CancelRenameRecursive(rootItem);
        }
    }

    private void CancelRenameRecursive(ProjectTreeItemViewModel item)
    {
        item.IsRenaming = false;
        foreach (var child in item.Children)
        {
            CancelRenameRecursive(child);
        }
    }

    public void CommitRename(ProjectTreeItemViewModel item)
    {
        RecordActivity(); // Record activity when renaming
        
        if (!item.IsRenaming)
            return;

        var newName = item.RenameText?.Trim() ?? string.Empty;
        
        if (string.IsNullOrWhiteSpace(newName))
        {
            // Cancel rename if name is empty
            item.IsRenaming = false;
            return;
        }

        // Update the document title if this is a document node
        if (item.Document != null)
        {
            item.Document.Title = newName;
            
            // Don't clear ContentFilePath - let SaveProject detect the path change
            // by comparing the current ContentFilePath with the expected path generated
            // from the new title. This allows the file to be moved properly.
        }

        // Update folder path if this is a subfolder
        if (item.IsSubfolder && !string.IsNullOrEmpty(item.FolderPath))
        {
            var oldFolderPath = item.FolderPath;
            var sanitizedNewName = _projectService.SanitizeFileName(newName);
            
            // Update folder path - replace the last segment with the new name
            var pathParts = oldFolderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (pathParts.Length > 0)
            {
                pathParts[pathParts.Length - 1] = sanitizedNewName;
                item.FolderPath = string.Join("/", pathParts);
            }
            else
            {
                item.FolderPath = sanitizedNewName;
            }

            // Update FolderPath for all documents in this subfolder
            UpdateDocumentsFolderPath(item, oldFolderPath, item.FolderPath);
        }

        // Update the tree item name
        item.Name = newName;
        item.IsRenaming = false;

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    private void UpdateDocumentsFolderPath(ProjectTreeItemViewModel folderNode, string oldPath, string newPath)
    {
        if (_currentProject == null)
            return;

        foreach (var child in folderNode.Children)
        {
            if (child.Document != null && child.Document.FolderPath.StartsWith(oldPath))
            {
                child.Document.FolderPath = child.Document.FolderPath.Replace(oldPath, newPath);
                child.Document.ContentFilePath = string.Empty; // Force regeneration
            }
            else if (child.IsSubfolder)
            {
                // Recursively update nested subfolders
                if (child.FolderPath.StartsWith(oldPath))
                {
                    child.FolderPath = child.FolderPath.Replace(oldPath, newPath);
                    UpdateDocumentsFolderPath(child, oldPath, newPath);
                }
            }
        }
    }

    public void CancelRename(ProjectTreeItemViewModel item)
    {
        item.IsRenaming = false;
        item.RenameText = string.Empty;
    }

    [RelayCommand]
    private void DeleteItem(ProjectTreeItemViewModel? item)
    {
        if (item == null)
        {
            item = SelectedProjectItem;
        }

        if (item == null || _currentProject == null)
            return;

        // Don't allow deleting root or main type folders
        if (item.IsRoot || item.IsManuscriptFolder || item.IsCharactersFolder || 
            item.IsLocationsFolder || item.IsResearchFolder || item.IsNotesFolder)
            return;

        // Ask for confirmation
        var confirmed = ShowDeleteConfirmationDialog(item);
        if (!confirmed)
            return;

        // Find the parent folder
        ProjectTreeItemViewModel? parentFolder = null;
        foreach (var rootItem in ProjectTreeItems)
        {
            parentFolder = FindParentFolder(rootItem, item);
            if (parentFolder != null)
                break;
        }

        if (parentFolder == null)
            return;

        // Handle document deletion
        if (item.Document != null)
        {
            var document = item.Document;

            // If it's a chapter, delete all child scenes first
            if (item.IsChapter)
            {
                var scenesToDelete = new List<ProjectTreeItemViewModel>(item.Children.Where(c => c.IsScene));
                foreach (var sceneItem in scenesToDelete)
                {
                    if (sceneItem.Document != null)
                    {
                        // Remove scene from project
                        _currentProject.Documents.Remove(sceneItem.Document);

                        // Delete scene content file if project is saved
                        if (!string.IsNullOrEmpty(CurrentProjectPath) && !string.IsNullOrEmpty(sceneItem.Document.ContentFilePath))
                        {
                            var projectDirectory = Path.GetDirectoryName(CurrentProjectPath) ?? 
                                                  Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty;
                            var sceneFilePath = Path.Combine(projectDirectory, sceneItem.Document.ContentFilePath);
                            try
                            {
                                if (File.Exists(sceneFilePath))
                                {
                                    File.Delete(sceneFilePath);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error deleting scene file {sceneFilePath}: {ex.Message}");
                            }
                        }
                    }
                }
            }

            // Remove document from project
            _currentProject.Documents.Remove(document);

            // Delete content file if project is saved
            if (!string.IsNullOrEmpty(CurrentProjectPath) && !string.IsNullOrEmpty(document.ContentFilePath))
            {
                var projectDirectory = Path.GetDirectoryName(CurrentProjectPath) ?? 
                                      Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty;
                var documentFilePath = Path.Combine(projectDirectory, document.ContentFilePath);
                try
                {
                    if (File.Exists(documentFilePath))
                    {
                        File.Delete(documentFilePath);
                    }

                    // If it's a chapter, also delete the chapter folder
                    if (item.IsChapter)
                    {
                        var chapterFolder = Path.GetDirectoryName(documentFilePath);
                        if (!string.IsNullOrEmpty(chapterFolder) && Directory.Exists(chapterFolder))
                        {
                            try
                            {
                                Directory.Delete(chapterFolder, true);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error deleting chapter folder {chapterFolder}: {ex.Message}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting document file {documentFilePath}: {ex.Message}");
                }
            }

            // Clear editor if this document was selected
            if (item == SelectedProjectItem)
            {
                EditorText = string.Empty;
                CurrentFilePath = string.Empty;
                SelectedProjectItem = null;
            }
        }
        // Handle subfolder deletion
        else if (item.IsSubfolder)
        {
            // Delete all documents in the subfolder recursively (including nested subfolders)
            var documentsToDelete = new List<ProjectTreeItemViewModel>();
            CollectDocumentsRecursive(item, documentsToDelete);

            foreach (var docItem in documentsToDelete)
            {
                if (docItem.Document != null)
                {
                    // Remove document from project
                    _currentProject.Documents.Remove(docItem.Document);

                    // Delete content file if project is saved
                    if (!string.IsNullOrEmpty(CurrentProjectPath) && !string.IsNullOrEmpty(docItem.Document.ContentFilePath))
                    {
                        var projectDirectory = Path.GetDirectoryName(CurrentProjectPath) ?? 
                                              Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty;
                        var docFilePath = Path.Combine(projectDirectory, docItem.Document.ContentFilePath);
                        try
                        {
                            if (File.Exists(docFilePath))
                            {
                                File.Delete(docFilePath);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error deleting document file {docFilePath}: {ex.Message}");
                        }
                    }
                }
            }

            // Clear editor if a document in this folder was selected
            if (SelectedProjectItem != null && documentsToDelete.Contains(SelectedProjectItem))
            {
                EditorText = string.Empty;
                CurrentFilePath = string.Empty;
                SelectedProjectItem = null;
            }
        }

        // Remove item from parent's children
        parentFolder.Children.Remove(item);

        // Update document orders in parent folder
        UpdateDocumentOrders(parentFolder);

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    private void CollectDocumentsRecursive(ProjectTreeItemViewModel item, List<ProjectTreeItemViewModel> documents)
    {
        if (item.Document != null)
        {
            documents.Add(item);
        }

        foreach (var child in item.Children)
        {
            CollectDocumentsRecursive(child, documents);
        }
    }

    private bool ShowDeleteConfirmationDialog(ProjectTreeItemViewModel item)
    {
        if (_parentWindow == null)
            return false;

        // Build confirmation message
        string message;
        if (item.Document != null)
        {
            var itemType = item.Document.Type switch
            {
                DocumentType.Chapter => "chapter",
                DocumentType.Scene => "scene",
                DocumentType.Character => "character",
                DocumentType.Location => "location",
                DocumentType.Note => "note",
                DocumentType.Research => "research",
                _ => "item"
            };

            if (item.IsChapter && item.Children.Any(c => c.IsScene))
            {
                var sceneCount = item.Children.Count(c => c.IsScene);
                message = $"Are you sure you want to delete the {itemType} \"{item.Name}\" and all {sceneCount} scene{(sceneCount == 1 ? "" : "s")} in it?\n\nThis action cannot be undone.";
            }
            else
            {
                message = $"Are you sure you want to delete the {itemType} \"{item.Name}\"?\n\nThis action cannot be undone.";
            }
        }
        else if (item.IsSubfolder)
        {
            var documentsToDelete = new List<ProjectTreeItemViewModel>();
            CollectDocumentsRecursive(item, documentsToDelete);
            var docCount = documentsToDelete.Count;
            
            if (docCount > 0)
            {
                message = $"Are you sure you want to delete the folder \"{item.Name}\" and all {docCount} document{(docCount == 1 ? "" : "s")} in it?\n\nThis action cannot be undone.";
            }
            else
            {
                message = $"Are you sure you want to delete the folder \"{item.Name}\"?\n\nThis action cannot be undone.";
            }
        }
        else
        {
            message = $"Are you sure you want to delete \"{item.Name}\"?\n\nThis action cannot be undone.";
        }

        // Create confirmation dialog using XAML-based window
        var dialog = new ConfirmDeleteWindow(message)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        // Show dialog synchronously - ShowDialog blocks until dialog closes
        if (_parentWindow != null)
        {
            dialog.ShowDialog(_parentWindow);
        }
        else
        {
            dialog.Show();
        }

        // Return the result
        return dialog.Result;
    }

    [RelayCommand]
    private async Task OpenRecentProject(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            // Remove invalid entry from MRU
            _mruService.RemoveProject(filePath);
            UpdateRecentProjectsList();
            return;
        }

        try
        {
            var project = _projectService.LoadProject(filePath);
            _currentProject = project;
            CurrentProjectPath = filePath;
            EditorText = string.Empty;
            CurrentFilePath = string.Empty;
            LoadProjectIntoTree(project);
            HasUnsavedChanges = false;
            
            // Update MRU list
            _mruService.AddProject(filePath, project.Name);
            UpdateRecentProjectsList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error opening recent project: {ex.Message}");
            // Remove invalid entry from MRU
            _mruService.RemoveProject(filePath);
            UpdateRecentProjectsList();
        }
    }

    [RelayCommand]
    private void ShowPreferences()
    {
        var settingsService = new Services.ApplicationSettingsService();
        var viewModel = new PreferencesViewModel((prefs) =>
        {
            // Settings are saved automatically by PreferencesViewModel
            // Reload shortcuts in MainWindow
            if (_parentWindow is Views.MainWindow mainWindow)
            {
                mainWindow.ReloadKeyboardShortcuts();
            }
        }, settingsService);

        var dialog = new Views.PreferencesWindow(viewModel)
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        
        if (_parentWindow != null)
        {
            dialog.ShowDialog(_parentWindow);
        }
        else
        {
            dialog.Show();
        }
    }

    private void UpdateRecentProjectsList()
    {
        RecentProjects.Clear();
        foreach (var project in _mruService.GetRecentProjects())
        {
            RecentProjects.Add(project);
        }
        OnPropertyChanged(nameof(HasRecentProjects));
    }

    public void NavigateToDocument(string documentId)
    {
        if (_currentProject == null)
            return;

        var document = _currentProject.Documents.FirstOrDefault(d => d.Id == documentId);
        if (document == null)
            return;

        // Find the corresponding tree item
        ProjectTreeItemViewModel? targetItem = null;
        foreach (var rootItem in ProjectTreeItems)
        {
            targetItem = FindTreeItemByDocument(rootItem, document);
            if (targetItem != null)
                break;
        }

        if (targetItem != null)
        {
            SelectedProjectItem = targetItem;
        }
    }
}
