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
using Scribo.ViewModels.Helpers;
using Scribo.ViewModels.Managers;

namespace Scribo.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly PluginManager _pluginManager;
    private readonly FileService _fileService;
    private readonly ProjectService _projectService;
    private readonly MostRecentlyUsedService _mruService;
    private readonly SearchIndexService _searchIndexService;
    private readonly DocumentLinkService _documentLinkService;
    private readonly StatisticsManager _statisticsManager;
    private readonly MarkdownRenderer _markdownRenderer;
    private readonly ApplicationSettingsService _applicationSettingsService;
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

    public MainWindowViewModel(PluginManager? pluginManager = null, FileService? fileService = null, ProjectService? projectService = null, MostRecentlyUsedService? mruService = null, SearchIndexService? searchIndexService = null, ApplicationSettingsService? applicationSettingsService = null)
    {
        _pluginManager = pluginManager ?? new PluginManager();
        _fileService = fileService ?? new FileService();
        _projectService = projectService ?? new ProjectService();
        _mruService = mruService ?? new MostRecentlyUsedService();
        _searchIndexService = searchIndexService ?? new SearchIndexService();
        _applicationSettingsService = applicationSettingsService ?? new ApplicationSettingsService();
        _documentLinkService = new DocumentLinkService();
        _statisticsManager = new StatisticsManager(_projectService);
        _markdownRenderer = new MarkdownRenderer(_documentLinkService);
        InitializeProjectTree();
        UpdateRecentProjectsList();
        InitializeIdleStatisticsTimer();
        InitializeFindReplace();
        InitializeDocumentLinkAutocomplete();
        
        // Auto-load last project if setting is enabled
        TryAutoLoadLastProject();
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
        _statisticsManager.SetCurrentProject(_currentProject, CurrentProjectPath);
        var statistics = _statisticsManager.CalculateStatistics(SelectedProjectItem?.Document, EditorText);
        UpdateStatisticsDisplay(statistics);
    }

    private void UpdateStatisticsDisplay(ProjectStatistics? statistics = null)
    {
        statistics ??= _currentProject?.Metadata?.Statistics;
        
        if (statistics == null)
        {
            TotalWordCount = 0;
            TotalCharacterCount = 0;
            TotalPageCount = 0;
            StatisticsText = "No statistics available";
            return;
        }

        TotalWordCount = statistics.TotalWordCount;
        TotalCharacterCount = statistics.TotalCharacterCount;
        TotalPageCount = statistics.TotalPageCount;
        
        var targetWordCount = _currentProject?.Metadata?.WordCountTargets?.TargetWordCount;
        StatisticsText = _statisticsManager.FormatStatisticsText(statistics, targetWordCount);
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
        var root = ProjectTreeBuilder.CreateInitialProjectTree();
        ProjectTreeItems.Add(root);
        
        // Expand the root node so Manuscript, Characters, Locations, etc. are visible
        root.IsExpanded = true;
        
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
        
        // Open project properties window to set project name
        ShowProjectProperties();
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
        if (!IsPreviewMode || string.IsNullOrWhiteSpace(EditorText))
        {
            RenderedMarkdownBlocks.Clear();
            RenderedMarkdown = string.Empty;
            return;
        }

        // Parse markdown into blocks using MarkdownRenderer
        var blocks = _markdownRenderer.RenderMarkdown(EditorText, _currentProject);
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
        
        // Also generate HTML for WebView (if needed)
        RenderedMarkdown = _markdownRenderer.RenderMarkdownToHtml(EditorText);
    }

    // RenderMarkdownToBlocks removed - now using MarkdownRenderer.RenderMarkdown()

    // Legacy method - kept for backward compatibility, now uses MarkdownRenderer
    private string ProcessInlineMarkdownText(string text, out List<DocumentLink> links)
    {
        // Use MarkdownRenderer's internal method via reflection or create a public method
        // For now, keeping this for compatibility but it's redundant with MarkdownRenderer
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

    // HTML rendering methods moved to MarkdownRenderer

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
        Console.WriteLine($"[OnSelectedProjectItemChanged] Called with item: {(value?.Name ?? "null")}, Document: {(value?.Document != null ? value.Document.Title : "null")}");
        
        RecordActivity(); // Record activity when selection changes
        
        if (value?.Document != null)
        {
            try
            {
                var document = value.Document;
                Console.WriteLine($"[OnSelectedProjectItemChanged] Document Title: {document.Title}");
                Console.WriteLine($"[OnSelectedProjectItemChanged] Document ContentFilePath: '{document.ContentFilePath}'");
                Console.WriteLine($"[OnSelectedProjectItemChanged] Document ProjectDirectory: '{document.ProjectDirectory}'");
                Console.WriteLine($"[OnSelectedProjectItemChanged] CurrentProjectPath: '{CurrentProjectPath}'");
                
                // Ensure ProjectDirectory is set if we have a current project path
                if (string.IsNullOrEmpty(document.ProjectDirectory) && !string.IsNullOrEmpty(CurrentProjectPath))
                {
                    var projectDirectory = Path.GetDirectoryName(CurrentProjectPath) ?? Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty;
                    document.ProjectDirectory = projectDirectory;
                    Console.WriteLine($"[OnSelectedProjectItemChanged] Set ProjectDirectory to: '{projectDirectory}'");
                }
                
                // Load content - this will get from _content if set, or load from file if ProjectDirectory and ContentFilePath are set
                // For unsaved projects, content should be in _content already
                Console.WriteLine($"[OnSelectedProjectItemChanged] Calling document.Content getter...");
                var content = document.Content;
                Console.WriteLine($"[OnSelectedProjectItemChanged] Content length: {content?.Length ?? 0}");
                
                // If content is empty and we have a ContentFilePath but ProjectDirectory is not set,
                // try to set ProjectDirectory from CurrentProjectPath if available
                if (string.IsNullOrEmpty(content) && !string.IsNullOrEmpty(document.ContentFilePath) && string.IsNullOrEmpty(document.ProjectDirectory))
                {
                    Console.WriteLine($"[OnSelectedProjectItemChanged] Content is empty, trying to set ProjectDirectory again...");
                    if (!string.IsNullOrEmpty(CurrentProjectPath))
                    {
                        var projectDirectory = Path.GetDirectoryName(CurrentProjectPath) ?? Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty;
                        document.ProjectDirectory = projectDirectory;
                        Console.WriteLine($"[OnSelectedProjectItemChanged] Set ProjectDirectory to: '{projectDirectory}', retrying content load...");
                        // Try loading again
                        content = document.Content;
                        Console.WriteLine($"[OnSelectedProjectItemChanged] Content length after retry: {content?.Length ?? 0}");
                    }
                }
                
                EditorText = content;
                CurrentFilePath = document.ContentFilePath;
                Console.WriteLine($"[OnSelectedProjectItemChanged] Set EditorText (length: {EditorText?.Length ?? 0}), CurrentFilePath: '{CurrentFilePath}'");
                HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OnSelectedProjectItemChanged] EXCEPTION: {ex.Message}");
                Console.WriteLine($"[OnSelectedProjectItemChanged] Stack trace: {ex.StackTrace}");
                EditorText = $"Error loading document: {ex.Message}";
                HasUnsavedChanges = false;
            }
        }
        else if (value != null && value.Children.Any())
        {
            // Selected item is a folder, clear the editor
            Console.WriteLine($"[OnSelectedProjectItemChanged] Selected item is a folder, clearing editor");
            EditorText = string.Empty;
            CurrentFilePath = string.Empty;
            HasUnsavedChanges = false;
        }
        else
        {
            Console.WriteLine($"[OnSelectedProjectItemChanged] Selected item is null or has no document/children");
        }
    }

    public void LoadProjectIntoTree(Project project)
    {
        _currentProject = project;
        
        // Update autocomplete documents when project is loaded
        UpdateAutocompleteDocuments();
        ProjectTreeItems.Clear();
        
        // Build project tree using ProjectTreeBuilder helper
        var root = ProjectTreeBuilder.BuildProjectTree(project, CurrentProjectPath);
        ProjectTreeItems.Add(root);
        
        // Expand the root node so Manuscript, Characters, Locations, etc. are visible
        root.IsExpanded = true;
        
        // Expand Trashcan folder if it has items
        var trashcanFolder = root.Children.FirstOrDefault(c => c.IsTrashcanFolder);
        if (trashcanFolder != null && trashcanFolder.Children.Any())
        {
            trashcanFolder.IsExpanded = true;
        }
        
        // Update statistics display after loading project
        UpdateStatisticsDisplay();
    }

    private Project BuildProjectFromCurrentState()
    {
        var rootItem = ProjectTreeItems.FirstOrDefault();
        if (rootItem == null)
        {
            // Create a default root item if none exists
            rootItem = ProjectTreeBuilder.CreateInitialProjectTree();
        }
        return ProjectBuilder.BuildProjectFromTree(
            _currentProject,
            rootItem,
            CurrentProjectPath,
            SelectedProjectItem?.Document,
            EditorText);
    }

    private string GetIconForDocumentType(DocumentType type)
    {
        return ProjectTreeBuilder.GetIconForDocumentType(type);
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
        {
            return item;
        }

        foreach (var child in item.Children)
        {
            var found = FindTreeItemByDocument(child, document);
            if (found != null)
            {
                return found;
            }
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

        // If document is in Trashcan, move the file back to its proper location
        var projectDirectory = !string.IsNullOrEmpty(CurrentProjectPath)
            ? Path.GetDirectoryName(CurrentProjectPath) ?? 
              Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty
            : string.Empty;

        bool wasInTrashcan = !string.IsNullOrEmpty(document.ContentFilePath) &&
                             document.ContentFilePath.StartsWith("Trashcan/", StringComparison.OrdinalIgnoreCase);
        
        if (!string.IsNullOrEmpty(projectDirectory) && wasInTrashcan)
        {
            // Store the old Trashcan folder path before moving
            var oldTrashcanPath = document.ContentFilePath;
            var trashcanFolderPath = GetTrashcanFolderPath(oldTrashcanPath);
            
            MoveFileFromTrashcan(document, targetFolder, projectDirectory);
            
            // Check if the Trashcan folder is now empty and remove it if needed
            if (!string.IsNullOrEmpty(trashcanFolderPath))
            {
                RemoveEmptyTrashcanFolder(trashcanFolderPath);
            }
        }
        else
        {
            // ContentFilePath will be regenerated on save based on the new FolderPath
            // We don't clear it here so SaveProject can detect the path change and move the file
        }

        // Rebuild the tree to reflect the changes
        if (_currentProject != null)
        {
            LoadProjectIntoTree(_currentProject);
        }

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Moves a document to Trashcan when dragged into Trashcan folder.
    /// Preserves the folder structure from the original location.
    /// </summary>
    public void MoveDocumentToTrashcan(ProjectTreeItemViewModel documentItem)
    {
        if (documentItem.Document == null || _currentProject == null)
            return;

        var document = documentItem.Document;
        
        // Don't move if already in Trashcan
        if (!string.IsNullOrEmpty(document.ContentFilePath) && 
            document.ContentFilePath.StartsWith("Trashcan/", StringComparison.OrdinalIgnoreCase))
            return;

        var projectDirectory = !string.IsNullOrEmpty(CurrentProjectPath)
            ? Path.GetDirectoryName(CurrentProjectPath) ?? 
              Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty
            : string.Empty;

        if (string.IsNullOrEmpty(projectDirectory))
            return;

        // Find the current parent folder
        ProjectTreeItemViewModel? currentParent = null;
        foreach (var rootItem in ProjectTreeItems)
        {
            currentParent = FindParentFolder(rootItem, documentItem);
            if (currentParent != null)
                break;
        }

        if (currentParent == null)
            return;

        // Remove document from current parent's children
        currentParent.Children.Remove(documentItem);

        // Move file to Trashcan (preserves folder structure)
        MoveFileToTrashcan(document, projectDirectory);

        // If it's a chapter, also move child scenes to Trashcan
        if (documentItem.IsChapter)
        {
            var scenesToMove = new List<ProjectTreeItemViewModel>(documentItem.Children.Where(c => c.IsScene));
            foreach (var sceneItem in scenesToMove)
            {
                if (sceneItem.Document != null)
                {
                    MoveFileToTrashcan(sceneItem.Document, projectDirectory);
                }
            }
        }

        // Clear editor if this document was selected
        if (documentItem == SelectedProjectItem)
        {
            EditorText = string.Empty;
            CurrentFilePath = string.Empty;
            SelectedProjectItem = null;
        }

        // Rebuild the tree to show moved items in Trashcan
        if (_currentProject != null)
        {
            LoadProjectIntoTree(_currentProject);
        }

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
    private void EmptyTrashcan(ProjectTreeItemViewModel? item)
    {
        if (item == null || _currentProject == null)
            return;

        // Only allow emptying if this is the Trashcan folder
        if (!item.IsTrashcanFolder)
            return;

        // Count documents in Trashcan
        var documentsInTrashcan = _currentProject.Documents
            .Where(d => !string.IsNullOrEmpty(d.ContentFilePath) && 
                       d.ContentFilePath.StartsWith("Trashcan/", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (documentsInTrashcan.Count == 0)
            return; // Nothing to empty

        // Permanently delete all documents in Trashcan
        var projectDirectory = !string.IsNullOrEmpty(CurrentProjectPath)
            ? Path.GetDirectoryName(CurrentProjectPath) ?? 
              Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty
            : string.Empty;

        foreach (var document in documentsInTrashcan)
        {
            // Remove document from project
            _currentProject.Documents.Remove(document);

            // Delete file permanently
            if (!string.IsNullOrEmpty(projectDirectory) && !string.IsNullOrEmpty(document.ContentFilePath))
            {
                var documentFilePath = Path.Combine(projectDirectory, document.ContentFilePath.Replace('/', Path.DirectorySeparatorChar));
                try
                {
                    if (File.Exists(documentFilePath))
                    {
                        File.Delete(documentFilePath);
                    }
                }
                catch { }
            }
        }

        // Clear editor if a Trashcan document was selected
        if (SelectedProjectItem != null && SelectedProjectItem.Document != null &&
            !string.IsNullOrEmpty(SelectedProjectItem.Document.ContentFilePath) &&
            SelectedProjectItem.Document.ContentFilePath.StartsWith("Trashcan/", StringComparison.OrdinalIgnoreCase))
        {
            EditorText = string.Empty;
            CurrentFilePath = string.Empty;
            SelectedProjectItem = null;
        }

        // Rebuild the tree
        if (_currentProject != null)
        {
            LoadProjectIntoTree(_currentProject);
        }

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
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
            item.IsLocationsFolder || item.IsResearchFolder || item.IsNotesFolder || item.IsTrashcanFolder)
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

        // Handle document deletion (move to Trashcan, or permanently delete if already in Trashcan)
        if (item.Document != null)
        {
            var document = item.Document;
            var projectDirectory = !string.IsNullOrEmpty(CurrentProjectPath)
                ? Path.GetDirectoryName(CurrentProjectPath) ?? 
                  Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty
                : string.Empty;

            // Check if document is already in Trashcan - if so, permanently delete it
            bool isInTrashcan = !string.IsNullOrEmpty(document.ContentFilePath) && 
                               document.ContentFilePath.StartsWith("Trashcan/", StringComparison.OrdinalIgnoreCase);

            if (isInTrashcan)
            {
                // Permanently delete from Trashcan
                // If it's a chapter, delete all child scenes first
                if (item.IsChapter)
                {
                    var scenesToDelete = new List<ProjectTreeItemViewModel>(item.Children.Where(c => c.IsScene));
                    foreach (var sceneItem in scenesToDelete)
                    {
                        if (sceneItem.Document != null)
                        {
                            _currentProject.Documents.Remove(sceneItem.Document);
                            if (!string.IsNullOrEmpty(projectDirectory) && !string.IsNullOrEmpty(sceneItem.Document.ContentFilePath))
                            {
                                var sceneFilePath = Path.Combine(projectDirectory, sceneItem.Document.ContentFilePath.Replace('/', Path.DirectorySeparatorChar));
                                try
                                {
                                    if (File.Exists(sceneFilePath))
                                    {
                                        File.Delete(sceneFilePath);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                }

                // Remove document from project
                _currentProject.Documents.Remove(document);

                // Delete file permanently
                if (!string.IsNullOrEmpty(projectDirectory) && !string.IsNullOrEmpty(document.ContentFilePath))
                {
                    var documentFilePath = Path.Combine(projectDirectory, document.ContentFilePath.Replace('/', Path.DirectorySeparatorChar));
                    try
                    {
                        if (File.Exists(documentFilePath))
                        {
                            File.Delete(documentFilePath);
                        }
                    }
                    catch { }
                }
            }
            else
            {
                // Move to Trashcan
                // If it's a chapter, move all child scenes to Trashcan first
                if (item.IsChapter)
                {
                    var scenesToMove = new List<ProjectTreeItemViewModel>(item.Children.Where(c => c.IsScene));
                    foreach (var sceneItem in scenesToMove)
                    {
                        if (sceneItem.Document != null && !string.IsNullOrEmpty(projectDirectory))
                        {
                            MoveFileToTrashcan(sceneItem.Document, projectDirectory);
                        }
                    }
                }

                // Move document file to Trashcan (don't remove from project)
                if (!string.IsNullOrEmpty(projectDirectory))
                {
                    MoveFileToTrashcan(document, projectDirectory);
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
        // Handle subfolder deletion (move all documents to Trashcan, or permanently delete if already in Trashcan)
        else if (item.IsSubfolder)
        {
            // Collect all documents in the subfolder recursively
            var documentsToProcess = new List<ProjectTreeItemViewModel>();
            CollectDocumentsRecursive(item, documentsToProcess);

            var projectDirectory = !string.IsNullOrEmpty(CurrentProjectPath)
                ? Path.GetDirectoryName(CurrentProjectPath) ?? 
                  Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty
                : string.Empty;

            // Check if folder is in Trashcan (check if any document is in Trashcan)
            bool isInTrashcan = documentsToProcess.Any(d => d.Document != null && 
                !string.IsNullOrEmpty(d.Document.ContentFilePath) && 
                d.Document.ContentFilePath.StartsWith("Trashcan/", StringComparison.OrdinalIgnoreCase));

            if (isInTrashcan)
            {
                // Permanently delete all documents
                foreach (var docItem in documentsToProcess)
                {
                    if (docItem.Document != null)
                    {
                        _currentProject.Documents.Remove(docItem.Document);
                        if (!string.IsNullOrEmpty(projectDirectory) && !string.IsNullOrEmpty(docItem.Document.ContentFilePath))
                        {
                            var docFilePath = Path.Combine(projectDirectory, docItem.Document.ContentFilePath.Replace('/', Path.DirectorySeparatorChar));
                            try
                            {
                                if (File.Exists(docFilePath))
                                {
                                    File.Delete(docFilePath);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            else
            {
                // Move all documents to Trashcan
                foreach (var docItem in documentsToProcess)
                {
                    if (docItem.Document != null && !string.IsNullOrEmpty(projectDirectory))
                    {
                        MoveFileToTrashcan(docItem.Document, projectDirectory);
                    }
                }
            }

            // Clear editor if a document in this folder was selected
            if (SelectedProjectItem != null && documentsToProcess.Contains(SelectedProjectItem))
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

        // Rebuild the tree to show moved items in Trashcan
        if (_currentProject != null)
        {
            LoadProjectIntoTree(_currentProject);
        }

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

    /// <summary>
    /// Moves a file from Trashcan back to its proper location based on the target folder.
    /// </summary>
    private void MoveFileFromTrashcan(Document document, ProjectTreeItemViewModel targetFolder, string projectDirectory)
    {
        if (string.IsNullOrEmpty(document.ContentFilePath) || 
            !document.ContentFilePath.StartsWith("Trashcan/", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            // Get the path without "Trashcan/" prefix
            var relativePath = document.ContentFilePath.Substring("Trashcan/".Length);
            var normalizedContentPath = relativePath.Replace('\\', '/');
            
            // Generate the new content file path based on document type and target folder
            var newContentFilePath = GenerateContentFilePath(document, targetFolder);
            
            if (string.IsNullOrEmpty(newContentFilePath))
                return;

            var sourceFilePath = Path.Combine(projectDirectory, document.ContentFilePath.Replace('/', Path.DirectorySeparatorChar));
            var targetFilePath = Path.Combine(projectDirectory, newContentFilePath.Replace('/', Path.DirectorySeparatorChar));
            var targetDirectory = Path.GetDirectoryName(targetFilePath);

            // Ensure target directory exists
            if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            // Move the file from Trashcan to the new location
            if (File.Exists(sourceFilePath))
            {
                File.Move(sourceFilePath, targetFilePath, overwrite: true);
                document.ContentFilePath = newContentFilePath;
            }

            // If it's a chapter, also move scene files
            // Note: document.ContentFilePath has already been updated to the new location above
            if (document.Type == DocumentType.Chapter)
            {
                var chapterScenes = _currentProject?.Documents.Where(d => d.ParentId == document.Id).ToList() ?? new List<Document>();
                foreach (var scene in chapterScenes)
                {
                    if (!string.IsNullOrEmpty(scene.ContentFilePath) && 
                        scene.ContentFilePath.StartsWith("Trashcan/", StringComparison.OrdinalIgnoreCase))
                    {
                        // Generate scene path based on chapter's new location (document.ContentFilePath is already updated)
                        var scenePath = GenerateSceneContentFilePath(scene, document);
                        if (!string.IsNullOrEmpty(scenePath))
                        {
                            var sceneSourcePath = Path.Combine(projectDirectory, scene.ContentFilePath.Replace('/', Path.DirectorySeparatorChar));
                            var sceneTargetPath = Path.Combine(projectDirectory, scenePath.Replace('/', Path.DirectorySeparatorChar));
                            var sceneTargetDir = Path.GetDirectoryName(sceneTargetPath);

                            if (!string.IsNullOrEmpty(sceneTargetDir) && !Directory.Exists(sceneTargetDir))
                            {
                                Directory.CreateDirectory(sceneTargetDir);
                            }

                            if (File.Exists(sceneSourcePath))
                            {
                                File.Move(sceneSourcePath, sceneTargetPath, overwrite: true);
                                scene.ContentFilePath = scenePath;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // If move fails, try copy and delete
            try
            {
                var newContentFilePath = GenerateContentFilePath(document, targetFolder);
                if (!string.IsNullOrEmpty(newContentFilePath))
                {
                    var sourceFilePath = Path.Combine(projectDirectory, document.ContentFilePath.Replace('/', Path.DirectorySeparatorChar));
                    var targetFilePath = Path.Combine(projectDirectory, newContentFilePath.Replace('/', Path.DirectorySeparatorChar));
                    var targetDirectory = Path.GetDirectoryName(targetFilePath);

                    if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    if (File.Exists(sourceFilePath))
                    {
                        File.Copy(sourceFilePath, targetFilePath, overwrite: true);
                        File.Delete(sourceFilePath);
                        document.ContentFilePath = newContentFilePath;
                    }
                }
            }
            catch
            {
                // If all else fails, just update the ContentFilePath
                var newContentFilePath = GenerateContentFilePath(document, targetFolder);
                if (!string.IsNullOrEmpty(newContentFilePath))
                {
                    document.ContentFilePath = newContentFilePath;
                }
            }
        }
    }

    /// <summary>
    /// Generates a content file path based on document type and target folder.
    /// </summary>
    private string GenerateContentFilePath(Document document, ProjectTreeItemViewModel targetFolder)
    {
        var sanitizedTitle = _projectService.SanitizeFileName(document.Title);
        var folderPath = targetFolder.IsSubfolder ? targetFolder.FolderPath : string.Empty;

        switch (document.Type)
        {
            case DocumentType.Character:
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var sanitizedFolderPath = _projectService.SanitizeFileName(folderPath);
                    return $"characters/{sanitizedFolderPath}/{sanitizedTitle}.md";
                }
                return $"characters/{sanitizedTitle}.md";

            case DocumentType.Location:
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var sanitizedFolderPath = _projectService.SanitizeFileName(folderPath);
                    return $"locations/{sanitizedFolderPath}/{sanitizedTitle}.md";
                }
                return $"locations/{sanitizedTitle}.md";

            case DocumentType.Research:
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var sanitizedFolderPath = _projectService.SanitizeFileName(folderPath);
                    return $"research/{sanitizedFolderPath}/{sanitizedTitle}.md";
                }
                return $"research/{sanitizedTitle}.md";

            case DocumentType.Note:
                return $"notes/{sanitizedTitle}.md";

            default:
                return string.Empty;
        }
    }

    /// <summary>
    /// Gets the folder path within Trashcan from a Trashcan ContentFilePath.
    /// For example, "Trashcan/locations/subfolder/doc.md" returns "locations/subfolder"
    /// </summary>
    private string GetTrashcanFolderPath(string trashcanContentFilePath)
    {
        if (string.IsNullOrEmpty(trashcanContentFilePath) || 
            !trashcanContentFilePath.StartsWith("Trashcan/", StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        // Remove "Trashcan/" prefix
        var relativePath = trashcanContentFilePath.Substring("Trashcan/".Length);
        
        // Get directory part (everything except filename)
        var lastSlash = relativePath.LastIndexOf('/');
        if (lastSlash < 0)
            return string.Empty; // Document is in root of Trashcan, no folder to remove
        
        return relativePath.Substring(0, lastSlash);
    }

    /// <summary>
    /// Removes an empty folder from Trashcan if it has no documents.
    /// </summary>
    private void RemoveEmptyTrashcanFolder(string trashcanFolderPath)
    {
        if (_currentProject == null || string.IsNullOrEmpty(trashcanFolderPath))
            return;

        // Check if there are any documents left in this Trashcan folder
        var documentsInFolder = _currentProject.Documents
            .Where(d => !string.IsNullOrEmpty(d.ContentFilePath) &&
                       d.ContentFilePath.StartsWith("Trashcan/", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.ContentFilePath.Substring("Trashcan/".Length))
            .Where(path => path.StartsWith(trashcanFolderPath + "/", StringComparison.OrdinalIgnoreCase) ||
                          path.Equals(trashcanFolderPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Console.WriteLine($"[RemoveEmptyTrashcanFolder] Checking folder '{trashcanFolderPath}', found {documentsInFolder.Count} documents");

        // If no documents remain in this folder, we can optionally clean up the folder structure
        // The tree rebuild will automatically not show empty folders, so this is mainly for file system cleanup
        if (documentsInFolder.Count == 0)
        {
            Console.WriteLine($"[RemoveEmptyTrashcanFolder] Folder '{trashcanFolderPath}' is empty");
            // Note: We don't delete the folder from the file system here as it will be cleaned up
            // when the project is saved or when the tree is rebuilt. The tree rebuild will handle
            // not showing empty folders.
        }
    }

    /// <summary>
    /// Generates a content file path for a scene based on its parent chapter.
    /// </summary>
    private string GenerateSceneContentFilePath(Document scene, Document chapter)
    {
        if (scene.Type != DocumentType.Scene || chapter.Type != DocumentType.Chapter)
            return string.Empty;

        var sanitizedSceneTitle = _projectService.SanitizeFileName(scene.Title);
        var sanitizedChapterTitle = _projectService.SanitizeFileName(chapter.Title);

        if (!string.IsNullOrEmpty(chapter.FolderPath))
        {
            var sanitizedFolderPath = _projectService.SanitizeFileName(chapter.FolderPath);
            return $"Manuscript/{sanitizedFolderPath}/{sanitizedChapterTitle}/{sanitizedSceneTitle}.md";
        }
        return $"Manuscript/{sanitizedChapterTitle}/{sanitizedSceneTitle}.md";
    }

    /// <summary>
    /// Moves a file to the Trashcan folder, preserving the directory structure.
    /// For example, locations/location1.md becomes Trashcan/locations/location1.md
    /// </summary>
    private void MoveFileToTrashcan(Document document, string projectDirectory)
    {
        Console.WriteLine($"[MoveFileToTrashcan] Starting - Document: {document.Title}, ContentFilePath: '{document.ContentFilePath}', FolderPath: '{document.FolderPath}', ProjectDirectory: '{projectDirectory}'");
        
        // Don't move if already in Trashcan
        if (!string.IsNullOrEmpty(document.ContentFilePath) && 
            document.ContentFilePath.StartsWith("Trashcan/", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[MoveFileToTrashcan] Already in Trashcan, returning");
            return;
        }

        try
        {
            string normalizedContentPath;
            
            // If ContentFilePath is empty, generate it based on current document properties to preserve folder structure
            if (string.IsNullOrEmpty(document.ContentFilePath))
            {
                Console.WriteLine($"[MoveFileToTrashcan] ContentFilePath is empty, generating from FolderPath and Type");
                // Build a dictionary of documents by ID for quick lookup (needed for scene path generation)
                var documentsById = _currentProject?.Documents.ToDictionary(d => d.Id) ?? new Dictionary<string, Document>();
                normalizedContentPath = _projectService.GenerateContentFilePath(document, documentsById);
                Console.WriteLine($"[MoveFileToTrashcan] Generated ContentFilePath: '{normalizedContentPath}'");
            }
            else
            {
                // Normalize path separators for ContentFilePath (uses forward slashes)
                normalizedContentPath = document.ContentFilePath.Replace('\\', '/');
            }
            
            // Convert forward slashes to platform-specific path separators for file system operations
            var sourceFilePath = Path.Combine(projectDirectory, normalizedContentPath.Replace('/', Path.DirectorySeparatorChar));
            Console.WriteLine($"[MoveFileToTrashcan] Source file path: '{sourceFilePath}'");
            Console.WriteLine($"[MoveFileToTrashcan] Source file exists: {File.Exists(sourceFilePath)}");
            
            // Preserve the directory structure: locations/subfolder/location1.md -> Trashcan/locations/subfolder/location1.md
            // Use forward slashes for ContentFilePath
            var trashcanPath = "Trashcan/" + normalizedContentPath;
            var targetFilePath = Path.Combine(projectDirectory, trashcanPath.Replace('/', Path.DirectorySeparatorChar));
            var targetDirectory = Path.GetDirectoryName(targetFilePath);
            Console.WriteLine($"[MoveFileToTrashcan] Target file path: '{targetFilePath}'");
            Console.WriteLine($"[MoveFileToTrashcan] Target directory: '{targetDirectory}'");

            // Update document's ContentFilePath first so it appears in Trashcan even if file move fails
            document.ContentFilePath = trashcanPath;
            // Clear cached content so it will reload from the new location
            // The Content getter will automatically reload from the new file path when accessed
            Console.WriteLine($"[MoveFileToTrashcan] Updated ContentFilePath to: '{trashcanPath}' (content will reload from new path when accessed)");

            // Only move the file if it exists
            if (File.Exists(sourceFilePath))
            {
                // Ensure target directory exists
                if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                    Console.WriteLine($"[MoveFileToTrashcan] Created target directory: '{targetDirectory}'");
                }

                // Move the file
                File.Move(sourceFilePath, targetFilePath, overwrite: true);
                Console.WriteLine($"[MoveFileToTrashcan] Successfully moved file from '{sourceFilePath}' to '{targetFilePath}'");
            }
            else
            {
                Console.WriteLine($"[MoveFileToTrashcan] Source file does not exist, skipping file move (ContentFilePath already updated)");
            }

            // If it's a chapter, also move any remaining files in the chapter folder
            // (scenes are already handled separately in DeleteItem)
            if (document.Type == DocumentType.Chapter)
            {
                var chapterSourceDir = Path.GetDirectoryName(sourceFilePath);
                var chapterTargetDir = Path.GetDirectoryName(targetFilePath);

                if (!string.IsNullOrEmpty(chapterSourceDir) && Directory.Exists(chapterSourceDir))
                {
                    // Move all remaining files in the chapter folder (in case there are any)
                    var filesInChapter = Directory.GetFiles(chapterSourceDir, "*", SearchOption.AllDirectories);
                    foreach (var file in filesInChapter)
                    {
                        var relativePath = Path.GetRelativePath(chapterSourceDir, file);
                        var targetFile = Path.Combine(chapterTargetDir ?? string.Empty, relativePath);
                        var targetFileDir = Path.GetDirectoryName(targetFile);
                        
                        if (!string.IsNullOrEmpty(targetFileDir) && !Directory.Exists(targetFileDir))
                        {
                            Directory.CreateDirectory(targetFileDir);
                        }
                        
                        if (File.Exists(file))
                        {
                            File.Move(file, targetFile, overwrite: true);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // If move fails, try copy and delete
            try
            {
                var normalizedContentPath = document.ContentFilePath.Replace('\\', '/');
                var sourceFilePath = Path.Combine(projectDirectory, normalizedContentPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(sourceFilePath))
                {
                    var trashcanPath = "Trashcan/" + normalizedContentPath;
                    var targetFilePath = Path.Combine(projectDirectory, trashcanPath.Replace('/', Path.DirectorySeparatorChar));
                    var targetDirectory = Path.GetDirectoryName(targetFilePath);

                    if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    File.Copy(sourceFilePath, targetFilePath, overwrite: true);
                    File.Delete(sourceFilePath);
                    document.ContentFilePath = trashcanPath;
                }
            }
            catch
            {
                // If all else fails, just update the ContentFilePath
                var normalizedContentPath = document.ContentFilePath.Replace('\\', '/');
                document.ContentFilePath = "Trashcan/" + normalizedContentPath;
            }
        }
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
            
            // Close menu if parent window is MainWindow
            if (_parentWindow is Views.MainWindow mainWindow)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    mainWindow.CloseAllMenus();
                }, DispatcherPriority.Loaded);
            }
        }
        catch (Exception ex)
        {
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

    private void TryAutoLoadLastProject()
    {
        try
        {
            var settings = _applicationSettingsService.LoadSettings();
            if (settings.AutoLoadLastProject)
            {
                var recentProjects = _mruService.GetRecentProjects();
                if (recentProjects.Count > 0)
                {
                    var lastProject = recentProjects[0];
                    if (!string.IsNullOrEmpty(lastProject.FilePath) && File.Exists(lastProject.FilePath))
                    {
                        Console.WriteLine($"[TryAutoLoadLastProject] Auto-loading last project: {lastProject.FilePath}");
                        // Use Dispatcher to load project after UI is initialized
                        Dispatcher.UIThread.Post(async () =>
                        {
                            await OpenRecentProject(lastProject.FilePath);
                        }, DispatcherPriority.Loaded);
                    }
                    else
                    {
                        Console.WriteLine($"[TryAutoLoadLastProject] Last project file does not exist: {lastProject.FilePath}");
                    }
                }
                else
                {
                    Console.WriteLine($"[TryAutoLoadLastProject] No recent projects found");
                }
            }
            else
            {
                Console.WriteLine($"[TryAutoLoadLastProject] Auto-load last project is disabled");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TryAutoLoadLastProject] Error auto-loading last project: {ex.Message}");
            Console.WriteLine($"[TryAutoLoadLastProject] Stack trace: {ex.StackTrace}");
        }
    }

    public void NavigateToDocument(string documentId)
    {
        
        if (_currentProject == null)
        {
            return;
        }

        var document = _currentProject.Documents.FirstOrDefault(d => d.Id == documentId);
        
        if (document == null)
        {
            return;
        }

        // Find the corresponding tree item
        ProjectTreeItemViewModel? targetItem = null;
        foreach (var rootItem in ProjectTreeItems)
        {
            targetItem = FindTreeItemByDocument(rootItem, document);
            if (targetItem != null)
            {
                break;
            }
        }

        if (targetItem != null)
        {
            SelectedProjectItem = targetItem;
        }
    }
}
