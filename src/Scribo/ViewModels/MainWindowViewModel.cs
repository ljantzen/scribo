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
using Scribo;
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
    private readonly MetadataReferenceService _metadataReferenceService;
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

    [ObservableProperty]
    private bool isEditorReadOnly = false;

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
    private DispatcherTimer? _autoSaveTimer;
    private DispatcherTimer? _timeTrackingTimer;
    private DateTime _lastActivityTime = DateTime.Now;
    private DateTime _lastSaveTime = DateTime.Now;
    private DateTime _lastTimeTrackingUpdate = DateTime.Now;
    private bool _isCurrentlyActive = false;
    private const int IdleTimeoutSeconds = 2; // Calculate statistics after 2 seconds of inactivity
    
    // Session statistics tracking
    private int _sessionInitialWordCount = 0;
    private int _sessionInitialCharacterCount = 0;
    private int _sessionInitialPageCount = 0;
    
    // Separate tracking for additions and deletions
    private int _sessionWordsWritten = 0;
    private int _sessionWordsDeleted = 0;
    private int _sessionCharactersWritten = 0;
    private int _sessionCharactersDeleted = 0;
    private int _previousWordCount = -1; // Use -1 to indicate not initialized
    private int _previousCharacterCount = -1; // Use -1 to indicate not initialized
    
    // Daily statistics tracking
    private string? _currentDateKey = null;
    private DailyStatistics? _currentDayStats = null;
    
    // Store pending expansion states for restoration after tree rebuilds
    private Dictionary<string, bool>? _pendingExpansionStates = null;

    public MainWindowViewModel(PluginManager? pluginManager = null, FileService? fileService = null, ProjectService? projectService = null, MostRecentlyUsedService? mruService = null, SearchIndexService? searchIndexService = null, ApplicationSettingsService? applicationSettingsService = null)
    {
        _pluginManager = pluginManager ?? new PluginManager();
        _fileService = fileService ?? new FileService();
        _projectService = projectService ?? new ProjectService();
        _mruService = mruService ?? new MostRecentlyUsedService();
        _searchIndexService = searchIndexService ?? new SearchIndexService();
        _applicationSettingsService = applicationSettingsService ?? new ApplicationSettingsService();
        _documentLinkService = new DocumentLinkService();
        _metadataReferenceService = new MetadataReferenceService();
        _statisticsManager = new StatisticsManager(_projectService);
        _markdownRenderer = new MarkdownRenderer(_documentLinkService);
        InitializeProjectTree();
        UpdateRecentProjectsList();
        InitializeIdleStatisticsTimer();
        InitializeAutoSaveTimer();
        InitializeTimeTrackingTimer();
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

    private void InitializeAutoSaveTimer()
    {
        _autoSaveTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30) // Check every 30 seconds
        };
        _autoSaveTimer.Tick += OnAutoSaveTimerTick;
        _autoSaveTimer.Start();
    }
    
    private void InitializeTimeTrackingTimer()
    {
        _timeTrackingTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1) // Update every second
        };
        _timeTrackingTimer.Tick += OnTimeTrackingTimerTick;
        _timeTrackingTimer.Start();
        _lastTimeTrackingUpdate = DateTime.Now;
    }
    
    private void OnTimeTrackingTimerTick(object? sender, EventArgs e)
    {
        if (_currentDayStats == null || _currentProject == null)
            return;
        
        var now = DateTime.Now;
        var elapsed = now - _lastTimeTrackingUpdate;
        
        // Determine if currently active (has been active within idle timeout)
        var timeSinceLastActivity = now - _lastActivityTime;
        var wasActive = _isCurrentlyActive;
        _isCurrentlyActive = timeSinceLastActivity.TotalSeconds < IdleTimeoutSeconds;
        
        // Accumulate time for the period that just elapsed
        if (elapsed.TotalSeconds > 0)
        {
            if (wasActive)
            {
                _currentDayStats.ActiveTimeSeconds += (long)elapsed.TotalSeconds;
            }
            else
            {
                _currentDayStats.IdleTimeSeconds += (long)elapsed.TotalSeconds;
            }
        }
        
        _lastTimeTrackingUpdate = now;
    }

    private void OnAutoSaveTimerTick(object? sender, EventArgs e)
    {
        try
        {
            var settings = _applicationSettingsService.LoadSettings();
            
            // Only auto-save if enabled
            if (!settings.AutoSave)
            {
                return;
            }

            // Only save if there's a current project with unsaved changes and a file path
            if (_currentProject == null || !HasUnsavedChanges || string.IsNullOrEmpty(CurrentProjectPath))
            {
                return;
            }

            // Check if we've been idle long enough
            var timeSinceLastActivity = DateTime.Now - _lastActivityTime;
            var autoSaveIntervalSeconds = settings.AutoSaveIntervalMinutes * 60;
            
            if (timeSinceLastActivity.TotalSeconds >= autoSaveIntervalSeconds)
            {
                // Check if enough time has passed since last save
                var timeSinceLastSave = DateTime.Now - _lastSaveTime;
                if (timeSinceLastSave.TotalSeconds >= autoSaveIntervalSeconds)
                {
                    PerformAutoSave();
                }
            }
        }
        catch (Exception ex)
        {
        }
    }

    private void PerformAutoSave()
    {
        if (_currentProject == null || string.IsNullOrEmpty(CurrentProjectPath))
            return;

        try
        {
            // Build project from current state to ensure editor content is saved
            var project = BuildProjectFromCurrentState();
            _projectService.SaveProject(project, CurrentProjectPath);
            _currentProject = project;
            _lastSaveTime = DateTime.Now;
            HasUnsavedChanges = false;
        }
        catch (Exception ex)
        {
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
    
    private ProjectStatistics? CalculateStatisticsWithoutDisplay()
    {
        _statisticsManager.SetCurrentProject(_currentProject, CurrentProjectPath);
        return _statisticsManager.CalculateStatistics(SelectedProjectItem?.Document, EditorText);
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

        // Track additions and deletions by comparing to previous count
        var currentWordCount = statistics.TotalWordCount;
        var currentCharacterCount = statistics.TotalCharacterCount;
        
        // Only track changes if we have initialized session statistics (previous counts are set)
        // This prevents tracking changes during initial load
        // Check if session was initialized by checking if _previousWordCount was set (not -1)
        if (_previousWordCount >= 0 && _previousCharacterCount >= 0)
        {
            // Calculate changes since last update
            var wordChange = currentWordCount - _previousWordCount;
            var charChange = currentCharacterCount - _previousCharacterCount;
            
            if (wordChange > 0)
            {
                _sessionWordsWritten += wordChange;
            }
            else if (wordChange < 0)
            {
                _sessionWordsDeleted += Math.Abs(wordChange);
            }
            
            if (charChange > 0)
            {
                _sessionCharactersWritten += charChange;
            }
            else if (charChange < 0)
            {
                _sessionCharactersDeleted += Math.Abs(charChange);
            }
            
            // Update daily statistics
            UpdateDailyStatistics(wordChange, charChange, currentWordCount, currentCharacterCount, statistics.TotalPageCount);
        }
        
        // Update previous counts for next comparison
        _previousWordCount = currentWordCount;
        _previousCharacterCount = currentCharacterCount;
        
        // Update daily statistics end counts even if no change detected
        if (_currentDayStats != null && _currentProject?.Metadata?.Statistics != null)
        {
            _currentDayStats.EndWordCount = currentWordCount;
            _currentDayStats.EndCharacterCount = currentCharacterCount;
            _currentDayStats.EndPageCount = statistics.TotalPageCount;
            _currentDayStats.LastActivity = DateTime.Now;
        }

        TotalWordCount = statistics.TotalWordCount;
        TotalCharacterCount = statistics.TotalCharacterCount;
        TotalPageCount = statistics.TotalPageCount;
        
        var targetWordCount = _currentProject?.Metadata?.WordCountTargets?.TargetWordCount;
        var currentDayStats = GetCurrentDayStatistics();
        var settings = _applicationSettingsService?.LoadSettings();
        var showActiveIdleTime = settings?.ShowActiveIdleTimeInStatusBar ?? true;
        StatisticsText = _statisticsManager.FormatStatisticsText(statistics, targetWordCount, GetSessionStatistics(), GetSessionTrackingStats(), currentDayStats, showActiveIdleTime);
    }
    
    private void UpdateDailyStatistics(int wordChange, int charChange, int currentWordCount, int currentCharacterCount, int currentPageCount)
    {
        if (_currentDayStats == null || _currentProject?.Metadata == null)
            return;
        
        // Check if we've moved to a new day
        var today = DateTime.Today;
        var todayKey = today.ToString("yyyy-MM-dd");
        
        if (_currentDateKey != todayKey)
        {
            // New day - finalize previous day's stats and start new day
            if (_currentDayStats != null)
            {
                _currentDayStats.EndWordCount = _previousWordCount;
                _currentDayStats.EndCharacterCount = _previousCharacterCount;
                _currentDayStats.EndPageCount = _currentProject.Metadata.Statistics?.TotalPageCount ?? 0;
                _currentDayStats.LastActivity = DateTime.Now;
            }
            
            // Initialize new day
            InitializeDailyStatistics();
        }
        
        // Update current day statistics
        if (_currentDayStats != null)
        {
            if (wordChange > 0)
            {
                _currentDayStats.WordsWritten += wordChange;
            }
            else if (wordChange < 0)
            {
                _currentDayStats.WordsDeleted += Math.Abs(wordChange);
            }
            
            if (charChange > 0)
            {
                _currentDayStats.CharactersWritten += charChange;
            }
            else if (charChange < 0)
            {
                _currentDayStats.CharactersDeleted += Math.Abs(charChange);
            }
            
            // Update end counts
            _currentDayStats.EndWordCount = currentWordCount;
            _currentDayStats.EndCharacterCount = currentCharacterCount;
            _currentDayStats.EndPageCount = currentPageCount;
            _currentDayStats.LastActivity = DateTime.Now;
        }
    }
    
    private DailyStatistics? GetCurrentDayStatistics()
    {
        if (_currentDayStats != null)
        {
            // Check if we've moved to a new day
            var today = DateTime.Today;
            var todayKey = today.ToString("yyyy-MM-dd");
            
            if (_currentDateKey == todayKey)
            {
                return _currentDayStats;
            }
            else
            {
                // New day - initialize it
                InitializeDailyStatistics();
                return _currentDayStats;
            }
        }
        
        return null;
    }
    
    private ProjectStatistics GetSessionStatistics()
    {
        if (_currentProject?.Metadata?.Statistics == null)
        {
            return new ProjectStatistics();
        }
        
        var currentStats = _currentProject.Metadata.Statistics;
        return new ProjectStatistics
        {
            TotalWordCount = currentStats.TotalWordCount - _sessionInitialWordCount,
            TotalCharacterCount = currentStats.TotalCharacterCount - _sessionInitialCharacterCount,
            TotalPageCount = currentStats.TotalPageCount - _sessionInitialPageCount
        };
    }
    
    private (int wordsWritten, int wordsDeleted, int charactersWritten, int charactersDeleted) GetSessionTrackingStats()
    {
        return (_sessionWordsWritten, _sessionWordsDeleted, _sessionCharactersWritten, _sessionCharactersDeleted);
    }
    
    private void InitializeSessionStatistics()
    {
        if (_currentProject?.Metadata?.Statistics == null)
        {
            _sessionInitialWordCount = 0;
            _sessionInitialCharacterCount = 0;
            _sessionInitialPageCount = 0;
            _sessionWordsWritten = 0;
            _sessionWordsDeleted = 0;
            _sessionCharactersWritten = 0;
            _sessionCharactersDeleted = 0;
            _previousWordCount = 0; // Set to 0 (not -1) so tracking can start
            _previousCharacterCount = 0; // Set to 0 (not -1) so tracking can start
            InitializeDailyStatistics();
            return;
        }
        
        var stats = _currentProject.Metadata.Statistics;
        _sessionInitialWordCount = stats.TotalWordCount;
        _sessionInitialCharacterCount = stats.TotalCharacterCount;
        _sessionInitialPageCount = stats.TotalPageCount;
        
        // Initialize previous counts for tracking changes
        _previousWordCount = stats.TotalWordCount;
        _previousCharacterCount = stats.TotalCharacterCount;
        
        // Reset session tracking
        _sessionWordsWritten = 0;
        _sessionWordsDeleted = 0;
        _sessionCharactersWritten = 0;
        _sessionCharactersDeleted = 0;
        
        // Initialize daily statistics
        InitializeDailyStatistics();
    }
    
    private void InitializeDailyStatistics()
    {
        if (_currentProject?.Metadata == null)
            return;
        
        var today = DateTime.Today;
        var todayKey = today.ToString("yyyy-MM-dd");
        
        // If we had a previous day, finalize it
        if (_currentDayStats != null && _currentDateKey != null && _currentDateKey != todayKey)
        {
            FinalizeDailyStatistics();
        }
        
        _currentDateKey = todayKey;
        
        // Reset time tracking state
        _lastTimeTrackingUpdate = DateTime.Now;
        _isCurrentlyActive = false;
        
        // Get or create daily statistics for today
        if (!_currentProject.Metadata.DailyStatistics.ContainsKey(_currentDateKey))
        {
            // New day - create new daily statistics
            var currentStats = _currentProject.Metadata.Statistics;
            _currentDayStats = new DailyStatistics
            {
                Date = today,
                StartWordCount = currentStats?.TotalWordCount ?? 0,
                StartCharacterCount = currentStats?.TotalCharacterCount ?? 0,
                StartPageCount = currentStats?.TotalPageCount ?? 0,
                EndWordCount = currentStats?.TotalWordCount ?? 0,
                EndCharacterCount = currentStats?.TotalCharacterCount ?? 0,
                EndPageCount = currentStats?.TotalPageCount ?? 0,
                FirstActivity = DateTime.Now,
                LastActivity = DateTime.Now,
                ActiveTimeSeconds = 0,
                IdleTimeSeconds = 0
            };
            _currentProject.Metadata.DailyStatistics[_currentDateKey] = _currentDayStats;
        }
        else
        {
            // Existing day - use existing statistics
            _currentDayStats = _currentProject.Metadata.DailyStatistics[_currentDateKey];
            
            // If start counts are zero but we have current stats, initialize them
            // This handles the case where daily stats were created but start counts weren't set
            if (_currentDayStats.StartWordCount == 0 && _currentDayStats.StartCharacterCount == 0)
            {
                var currentStats = _currentProject.Metadata.Statistics;
                _currentDayStats.StartWordCount = currentStats?.TotalWordCount ?? 0;
                _currentDayStats.StartCharacterCount = currentStats?.TotalCharacterCount ?? 0;
                _currentDayStats.StartPageCount = currentStats?.TotalPageCount ?? 0;
            }
            
            // Update last activity
            _currentDayStats.LastActivity = DateTime.Now;
        }
    }
    
    public void Dispose()
    {
        // Finalize daily statistics before disposing
        FinalizeDailyStatistics();
        
        _idleTimer?.Stop();
        _idleTimer = null;
        _autoSaveTimer?.Stop();
        _autoSaveTimer = null;
        _timeTrackingTimer?.Stop();
        _timeTrackingTimer = null;
    }
    
    private void FinalizeDailyStatistics()
    {
        if (_currentDayStats != null && _currentProject?.Metadata?.Statistics != null)
        {
            // Update time tracking one final time
            var now = DateTime.Now;
            var elapsed = now - _lastTimeTrackingUpdate;
            
            if (_isCurrentlyActive)
            {
                _currentDayStats.ActiveTimeSeconds += (long)elapsed.TotalSeconds;
            }
            else
            {
                _currentDayStats.IdleTimeSeconds += (long)elapsed.TotalSeconds;
            }
            
            _lastTimeTrackingUpdate = now;
            
            _currentDayStats.EndWordCount = _currentProject.Metadata.Statistics.TotalWordCount;
            _currentDayStats.EndCharacterCount = _currentProject.Metadata.Statistics.TotalCharacterCount;
            _currentDayStats.EndPageCount = _currentProject.Metadata.Statistics.TotalPageCount;
            _currentDayStats.LastActivity = DateTime.Now;
        }
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
                _lastSaveTime = DateTime.Now; // Set save time when loading project
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
            _lastSaveTime = DateTime.Now;
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
                // Finalize daily statistics before saving
                FinalizeDailyStatistics();
                
                var project = BuildProjectFromCurrentState();
                _projectService.SaveProject(project, filePath);
                _currentProject = project;
                CurrentProjectPath = filePath;
                _lastSaveTime = DateTime.Now;
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
        // Don't mark as changed if editor is read-only (Trashcan document)
        if (!IsEditorReadOnly)
        {
            HasUnsavedChanges = true;
            RecordActivity(); // Record activity when user types
        }
        
        // Update find/replace document text
        FindReplaceViewModel.SetDocumentText(value);
        
        // Update document when content changes - parse frontmatter and update metadata
        if (SelectedProjectItem?.Document != null && _currentProject != null)
        {
            // Set RawContent which will parse frontmatter and update metadata
            SelectedProjectItem.Document.RawContent = value;
            
            // Validate metadata references after parsing
            ValidateMetadataReferences(SelectedProjectItem.Document);
            
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
        
        RecordActivity(); // Record activity when selection changes
        
        if (value?.Document != null)
        {
            try
            {
                var document = value.Document;
                
                // Check if document is in Trashcan and set read-only accordingly
                var isInTrashcan = !string.IsNullOrEmpty(document.ContentFilePath) && 
                                   document.ContentFilePath.StartsWith("Trashcan/", StringComparison.OrdinalIgnoreCase);
                IsEditorReadOnly = isInTrashcan;
                
                // Ensure ProjectDirectory is set if we have a current project path
                if (string.IsNullOrEmpty(document.ProjectDirectory) && !string.IsNullOrEmpty(CurrentProjectPath))
                {
                    var projectDirectory = Path.GetDirectoryName(CurrentProjectPath) ?? Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty;
                    document.ProjectDirectory = projectDirectory;
                }
                
                // Load raw content (with frontmatter) for editing
                // If ProjectDirectory is not set, try to set it from CurrentProjectPath
                if (string.IsNullOrEmpty(document.ProjectDirectory) && !string.IsNullOrEmpty(CurrentProjectPath))
                {
                    var projectDirectory = Path.GetDirectoryName(CurrentProjectPath) ?? Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ?? string.Empty;
                    document.ProjectDirectory = projectDirectory;
                }
                
                // Get raw content (includes frontmatter) for editing
                var rawContent = document.RawContent;
                
                EditorText = rawContent;
                CurrentFilePath = document.ContentFilePath;
                HasUnsavedChanges = false;
            }
            catch (Exception ex)
            {
                EditorText = $"Error loading document: {ex.Message}";
                HasUnsavedChanges = false;
            }
        }
        else if (value != null && value.Children.Any())
        {
            // Selected item is a folder, clear the editor and allow editing
            EditorText = string.Empty;
            CurrentFilePath = string.Empty;
            IsEditorReadOnly = false;
            HasUnsavedChanges = false;
        }
        else
        {
            // No document selected, allow editing
            IsEditorReadOnly = false;
        }
    }

    public void LoadProjectIntoTree(Project project)
    {
        _currentProject = project;
        
        // Update metadata reference service with current project
        _metadataReferenceService.SetProject(project);
        
        // Update autocomplete documents when project is loaded
        UpdateAutocompleteDocuments();
        ProjectTreeItems.Clear();
        
        // Build project tree using ProjectTreeBuilder helper
        var root = ProjectTreeBuilder.BuildProjectTree(project, CurrentProjectPath);
        ProjectTreeItems.Add(root);
        
        // Expand the root node so Manuscript, Characters, Locations, etc. are visible
        root.IsExpanded = true;
        
        // Expand Manuscript folder by default (it's the main working area)
        var manuscriptFolder = root.Children.FirstOrDefault(c => c.IsManuscriptFolder);
        if (manuscriptFolder != null)
        {
            Console.WriteLine($"[LoadProjectIntoTree] Setting Manuscript folder IsExpanded=true (was {manuscriptFolder.IsExpanded})");
            manuscriptFolder.IsExpanded = true;
            
            // Monitor IsExpanded property changes for Manuscript folder to catch TreeView collapsing it
            manuscriptFolder.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ProjectTreeItemViewModel.IsExpanded) && manuscriptFolder.IsManuscriptFolder)
                {
                    Console.WriteLine($"[LoadProjectIntoTree] Manuscript folder IsExpanded changed to: {manuscriptFolder.IsExpanded}");
                    // If it was collapsed and we have pending expansion states, restore it
                    if (!manuscriptFolder.IsExpanded && _pendingExpansionStates != null)
                    {
                        if (_pendingExpansionStates.TryGetValue("Manuscript", out var shouldBeExpanded) && shouldBeExpanded)
                        {
                            Console.WriteLine($"[LoadProjectIntoTree] Manuscript folder was collapsed, restoring to expanded");
                            Dispatcher.UIThread.Post(() =>
                            {
                                manuscriptFolder.IsExpanded = true;
                            }, DispatcherPriority.Loaded);
                        }
                    }
                }
            };
        }
        else
        {
            Console.WriteLine("[LoadProjectIntoTree] Manuscript folder not found!");
        }
        
        // Expand Trashcan folder if it has items
        var trashcanFolder = root.Children.FirstOrDefault(c => c.IsTrashcanFolder);
        if (trashcanFolder != null && trashcanFolder.Children.Any())
        {
            trashcanFolder.IsExpanded = true;
        }
        
        // Calculate initial statistics, initialize session tracking, then update display
        // First calculate statistics to ensure they're up to date (without updating display)
        _statisticsManager.SetCurrentProject(_currentProject, CurrentProjectPath);
        var initialStats = _statisticsManager.CalculateStatistics(null, string.Empty);
        // Initialize session tracking with the calculated stats
        InitializeSessionStatistics();
        // Now update display with current statistics (this will recalculate with current editor text)
        UpdateStatisticsDisplay(initialStats);
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
                        // Finalize daily statistics before saving
                        FinalizeDailyStatistics();
                        
                        _projectService.SaveProject(_currentProject, CurrentProjectPath);
                        _lastSaveTime = DateTime.Now;
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
        
        // Expand the top-level Manuscript folder if it's closed
        var rootNode = ProjectTreeItems.FirstOrDefault();
        if (rootNode != null)
        {
            var manuscriptFolder = rootNode.Children.FirstOrDefault(c => c.IsManuscriptFolder);
            if (manuscriptFolder != null)
            {
                manuscriptFolder.IsExpanded = true;
            }
        }
        
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

        // Set ProjectDirectory if project has been saved before
        if (!string.IsNullOrEmpty(_currentProject.FilePath))
        {
            var projectDirectory = Path.GetDirectoryName(_currentProject.FilePath) ?? 
                                 Path.GetDirectoryName(Path.GetFullPath(_currentProject.FilePath)) ?? 
                                 string.Empty;
            newScene.ProjectDirectory = projectDirectory;
        }

        // Generate content file path for the new scene
        // Build a dictionary of documents by ID for quick lookup (needed for parent relationships)
        var documentsById = _currentProject.Documents.ToDictionary(d => d.Id);
        newScene.ContentFilePath = _projectService.GenerateContentFilePath(newScene, documentsById);

        // Document will be saved after user renames it (in CommitRename)

        // Create a new scene node and add it directly to the chapter node's children
        var newSceneNode = new ProjectTreeItemViewModel
        {
            Name = sceneTitle,
            Icon = GetIconForDocumentType(newScene.Type),
            Document = newScene
        };
        
        // Expand the parent chapter if it's not already expanded
        chapterNode.IsExpanded = true;
        
        // Expand the top-level Manuscript folder if it's closed
        var rootNode = ProjectTreeItems.FirstOrDefault();
        if (rootNode != null)
        {
            var manuscriptFolder = rootNode.Children.FirstOrDefault(c => c.IsManuscriptFolder);
            if (manuscriptFolder != null)
            {
                manuscriptFolder.IsExpanded = true;
            }
        }
        
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

        // Set ProjectDirectory if project has been saved before
        if (!string.IsNullOrEmpty(_currentProject.FilePath))
        {
            var projectDirectory = Path.GetDirectoryName(_currentProject.FilePath) ??
                                 Path.GetDirectoryName(Path.GetFullPath(_currentProject.FilePath)) ??
                                 string.Empty;
            newCharacter.ProjectDirectory = projectDirectory;
        }

        // Generate content file path for the new character
        // Build a dictionary of documents by ID for quick lookup (needed for parent relationships)
        var documentsById = _currentProject.Documents.ToDictionary(d => d.Id);
        newCharacter.ContentFilePath = _projectService.GenerateContentFilePath(newCharacter, documentsById);

        // Document will be saved after user renames it (in CommitRename)

        // Create a new character node and add it directly to the Characters folder
        var newCharacterNode = new ProjectTreeItemViewModel
        {
            Name = characterTitle,
            Icon = GetIconForDocumentType(newCharacter.Type),
            Document = newCharacter
        };
        
        // Expand the parent folder if it's not already expanded
        folderNode.IsExpanded = true;
        
        // Expand the top-level Characters folder if it's closed
        var rootNode = ProjectTreeItems.FirstOrDefault();
        if (rootNode != null)
        {
            var charactersFolder = rootNode.Children.FirstOrDefault(c => c.IsCharactersFolder);
            if (charactersFolder != null)
            {
                charactersFolder.IsExpanded = true;
            }
        }
        
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

        // Set ProjectDirectory if project has been saved before
        if (!string.IsNullOrEmpty(_currentProject.FilePath))
        {
            var projectDirectory = Path.GetDirectoryName(_currentProject.FilePath) ??
                                 Path.GetDirectoryName(Path.GetFullPath(_currentProject.FilePath)) ??
                                 string.Empty;
            newLocation.ProjectDirectory = projectDirectory;
        }

        // Generate content file path for the new location
        // Build a dictionary of documents by ID for quick lookup (needed for parent relationships)
        var documentsById = _currentProject.Documents.ToDictionary(d => d.Id);
        newLocation.ContentFilePath = _projectService.GenerateContentFilePath(newLocation, documentsById);

        // Document will be saved after user renames it (in CommitRename)

        // Create a new location node and add it directly to the Locations folder
        var newLocationNode = new ProjectTreeItemViewModel
        {
            Name = locationTitle,
            Icon = GetIconForDocumentType(newLocation.Type),
            Document = newLocation
        };
        
        // Expand the parent folder if it's not already expanded
        folderNode.IsExpanded = true;
        
        // Expand the top-level Locations folder if it's closed
        var rootNode = ProjectTreeItems.FirstOrDefault();
        if (rootNode != null)
        {
            var locationsFolder = rootNode.Children.FirstOrDefault(c => c.IsLocationsFolder);
            if (locationsFolder != null)
            {
                locationsFolder.IsExpanded = true;
            }
        }
        
        // Add to the Locations folder's children
        folderNode.Children.Add(newLocationNode);
        
        // Trigger rename mode for the newly added location
        StartRename(newLocationNode);

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void AddOther(ProjectTreeItemViewModel? folderNode)
    {
        if (folderNode == null)
        {
            // Find the Other folder if no folder specified
            folderNode = ProjectTreeItems.FirstOrDefault()?.Children
                .FirstOrDefault(c => c.IsOtherFolder);
        }

        // Check if this is Other folder or a subfolder that can contain other documents
        if (folderNode == null || (!folderNode.IsOtherFolder && folderNode.FolderDocumentType != DocumentType.Other))
        {
            // Fallback: try to find Other folder
            folderNode = ProjectTreeItems.FirstOrDefault()?.Children
                .FirstOrDefault(c => c.IsOtherFolder);
            if (folderNode == null)
                return;
        }

        // Ensure we have a current project
        if (_currentProject == null)
        {
            _currentProject = _projectService.CreateNewProject(
                ProjectTreeItems.FirstOrDefault()?.Name ?? "Untitled Project");
        }

        // Generate a unique title
        var otherTitle = "Untitled Other";
        var counter = 1;
        while (_currentProject.Documents.Any(d => d.Type == DocumentType.Other && d.Title == otherTitle))
        {
            otherTitle = $"Untitled Other {counter}";
            counter++;
        }

        // Determine folder path
        var folderPath = folderNode.IsOtherFolder ? string.Empty : folderNode.FolderPath;

        // Determine order (number of existing other documents in this folder)
        var existingOtherInFolder = folderNode.Children
            .Where(c => c.Document?.Type == DocumentType.Other)
            .Count();

        // Create new other document
        var newOther = new Document
        {
            Title = otherTitle,
            Type = DocumentType.Other,
            FolderPath = folderPath,
            Content = string.Empty,
            Order = existingOtherInFolder
        };

        // Add to project
        _currentProject.Documents.Add(newOther);

        // Set ProjectDirectory if project has been saved before
        if (!string.IsNullOrEmpty(_currentProject.FilePath))
        {
            var projectDirectory = Path.GetDirectoryName(_currentProject.FilePath) ??
                                 Path.GetDirectoryName(Path.GetFullPath(_currentProject.FilePath)) ??
                                 string.Empty;
            newOther.ProjectDirectory = projectDirectory;
        }

        // Generate content file path for the new other document
        // Build a dictionary of documents by ID for quick lookup (needed for parent relationships)
        var documentsById = _currentProject.Documents.ToDictionary(d => d.Id);
        newOther.ContentFilePath = _projectService.GenerateContentFilePath(newOther, documentsById);

        // Document will be saved after user renames it (in CommitRename)

        // Create a new other node and add it directly to the Other folder
        var newOtherNode = new ProjectTreeItemViewModel
        {
            Name = otherTitle,
            Icon = GetIconForDocumentType(newOther.Type),
            Document = newOther
        };
        
        // Expand the parent folder if it's not already expanded
        folderNode.IsExpanded = true;
        
        // Expand the top-level Other folder if it's closed
        var rootNode = ProjectTreeItems.FirstOrDefault();
        if (rootNode != null)
        {
            var otherFolder = rootNode.Children.FirstOrDefault(c => c.IsOtherFolder);
            if (otherFolder != null)
            {
                otherFolder.IsExpanded = true;
            }
        }
        
        // Add to the Other folder's children
        folderNode.Children.Add(newOtherNode);

        // Trigger rename mode for the newly added other document
        StartRename(newOtherNode);

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    [RelayCommand]
    private void AddTimeline(ProjectTreeItemViewModel? folderNode)
    {
        AddDocumentType(folderNode, DocumentType.Timeline, "Untitled Timeline", "Timeline");
    }

    [RelayCommand]
    private void AddPlot(ProjectTreeItemViewModel? folderNode)
    {
        AddDocumentType(folderNode, DocumentType.Plot, "Untitled Plot", "Plot");
    }

    [RelayCommand]
    private void AddObject(ProjectTreeItemViewModel? folderNode)
    {
        AddDocumentType(folderNode, DocumentType.Object, "Untitled Object", "Object");
    }

    [RelayCommand]
    private void AddEntity(ProjectTreeItemViewModel? folderNode)
    {
        AddDocumentType(folderNode, DocumentType.Entity, "Untitled Entity", "Entity");
    }

    private void AddDocumentType(ProjectTreeItemViewModel? folderNode, DocumentType docType, string defaultTitle, string folderName)
    {
        if (folderNode == null)
        {
            // Find the appropriate folder if no folder specified
            folderNode = ProjectTreeItems.FirstOrDefault()?.Children
                .FirstOrDefault(c => 
                    (docType == DocumentType.Timeline && c.IsTimelineFolder) ||
                    (docType == DocumentType.Plot && c.IsPlotFolder) ||
                    (docType == DocumentType.Object && c.IsObjectFolder) ||
                    (docType == DocumentType.Entity && c.IsEntityFolder));
        }

        // Check if this is the correct folder or a subfolder that can contain this document type
        bool isValidFolder = false;
        if (docType == DocumentType.Timeline)
        {
            isValidFolder = folderNode != null && (folderNode.IsTimelineFolder || folderNode.FolderDocumentType == DocumentType.Timeline);
        }
        else if (docType == DocumentType.Plot)
        {
            isValidFolder = folderNode != null && (folderNode.IsPlotFolder || folderNode.FolderDocumentType == DocumentType.Plot);
        }
        else if (docType == DocumentType.Object)
        {
            isValidFolder = folderNode != null && (folderNode.IsObjectFolder || folderNode.FolderDocumentType == DocumentType.Object);
        }
        else if (docType == DocumentType.Entity)
        {
            isValidFolder = folderNode != null && (folderNode.IsEntityFolder || folderNode.FolderDocumentType == DocumentType.Entity);
        }

        if (!isValidFolder)
        {
            // Fallback: try to find the correct folder
            var rootNode = ProjectTreeItems.FirstOrDefault();
            if (rootNode != null)
            {
                if (docType == DocumentType.Timeline)
                    folderNode = rootNode.Children.FirstOrDefault(c => c.IsTimelineFolder);
                else if (docType == DocumentType.Plot)
                    folderNode = rootNode.Children.FirstOrDefault(c => c.IsPlotFolder);
                else if (docType == DocumentType.Object)
                    folderNode = rootNode.Children.FirstOrDefault(c => c.IsObjectFolder);
                else if (docType == DocumentType.Entity)
                    folderNode = rootNode.Children.FirstOrDefault(c => c.IsEntityFolder);
            }
            if (folderNode == null)
                return;
        }

        // Ensure we have a current project
        if (_currentProject == null)
        {
            _currentProject = _projectService.CreateNewProject(
                ProjectTreeItems.FirstOrDefault()?.Name ?? "Untitled Project");
        }

        // Generate a unique title
        var docTitle = defaultTitle;
        var counter = 1;
        while (_currentProject.Documents.Any(d => d.Type == docType && d.Title == docTitle))
        {
            docTitle = $"{defaultTitle} {counter}";
            counter++;
        }

        // Determine folder path
        var folderPath = (docType == DocumentType.Timeline && folderNode.IsTimelineFolder) ||
                         (docType == DocumentType.Plot && folderNode.IsPlotFolder) ||
                         (docType == DocumentType.Object && folderNode.IsObjectFolder) ||
                         (docType == DocumentType.Entity && folderNode.IsEntityFolder)
            ? string.Empty 
            : folderNode.FolderPath;

        // Determine order (number of existing documents in this folder)
        var existingDocsInFolder = folderNode.Children
            .Where(c => c.Document?.Type == docType)
            .Count();

        // Create new document
        var newDoc = new Document
        {
            Title = docTitle,
            Type = docType,
            FolderPath = folderPath,
            Content = string.Empty,
            Order = existingDocsInFolder
        };

        // Add to project
        _currentProject.Documents.Add(newDoc);

        // Set ProjectDirectory if project has been saved before
        string? projectDirectory = null;
        if (!string.IsNullOrEmpty(_currentProject.FilePath))
        {
            projectDirectory = Path.GetDirectoryName(_currentProject.FilePath) ??
                                 Path.GetDirectoryName(Path.GetFullPath(_currentProject.FilePath)) ??
                                 string.Empty;
            newDoc.ProjectDirectory = projectDirectory;
        }
        else if (!string.IsNullOrEmpty(CurrentProjectPath))
        {
            // Also try CurrentProjectPath as fallback
            projectDirectory = Path.GetDirectoryName(CurrentProjectPath) ??
                                 Path.GetDirectoryName(Path.GetFullPath(CurrentProjectPath)) ??
                                 string.Empty;
            newDoc.ProjectDirectory = projectDirectory;
        }

        // Generate content file path for the new document
        // Build a dictionary of documents by ID for quick lookup (needed for parent relationships)
        var documentsById = _currentProject.Documents.ToDictionary(d => d.Id);
        newDoc.ContentFilePath = _projectService.GenerateContentFilePath(newDoc, documentsById);

        // Create a new document node and add it directly to the folder
        var newDocNode = new ProjectTreeItemViewModel
        {
            Name = docTitle,
            Icon = GetIconForDocumentType(newDoc.Type),
            Document = newDoc
        };
        
        // Expand the parent folder if it's not already expanded
        folderNode.IsExpanded = true;
        
        // Expand the top-level folder if it's closed
        var rootNodeForExpansion = ProjectTreeItems.FirstOrDefault();
        if (rootNodeForExpansion != null)
        {
            ProjectTreeItemViewModel? typeFolder = null;
            if (docType == DocumentType.Timeline)
                typeFolder = rootNodeForExpansion.Children.FirstOrDefault(c => c.IsTimelineFolder);
            else if (docType == DocumentType.Plot)
                typeFolder = rootNodeForExpansion.Children.FirstOrDefault(c => c.IsPlotFolder);
            else if (docType == DocumentType.Object)
                typeFolder = rootNodeForExpansion.Children.FirstOrDefault(c => c.IsObjectFolder);
            else if (docType == DocumentType.Entity)
                typeFolder = rootNodeForExpansion.Children.FirstOrDefault(c => c.IsEntityFolder);
            
            if (typeFolder != null)
            {
                typeFolder.IsExpanded = true;
            }
        }
        
        // Add to the folder's children
        folderNode.Children.Add(newDocNode);
        
        // Trigger rename mode for the newly added document
        StartRename(newDocNode);

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

        // Set ProjectDirectory if project has been saved before
        if (!string.IsNullOrEmpty(_currentProject.FilePath))
        {
            var projectDirectory = Path.GetDirectoryName(_currentProject.FilePath) ??
                                 Path.GetDirectoryName(Path.GetFullPath(_currentProject.FilePath)) ??
                                 string.Empty;
            newNote.ProjectDirectory = projectDirectory;
        }

        // Generate content file path for the new note
        // Build a dictionary of documents by ID for quick lookup (needed for parent relationships)
        var documentsById = _currentProject.Documents.ToDictionary(d => d.Id);
        newNote.ContentFilePath = _projectService.GenerateContentFilePath(newNote, documentsById);

        // Document will be saved after user renames it (in CommitRename)

        // Create a new note node and add it directly to the folder
        var newNoteNode = new ProjectTreeItemViewModel
        {
            Name = noteTitle,
            Icon = GetIconForDocumentType(newNote.Type),
            Document = newNote
        };
        
        // Expand the parent folder if it's not already expanded
        folderNode.IsExpanded = true;
        
        // Expand the top-level Notes or Research folder if it's closed
        var rootNode = ProjectTreeItems.FirstOrDefault();
        if (rootNode != null)
        {
            if (noteType == DocumentType.Research)
            {
                var researchFolder = rootNode.Children.FirstOrDefault(c => c.IsResearchFolder);
                if (researchFolder != null)
                {
                    researchFolder.IsExpanded = true;
                }
            }
            else
            {
                var notesFolder = rootNode.Children.FirstOrDefault(c => c.IsNotesFolder);
                if (notesFolder != null)
                {
                    notesFolder.IsExpanded = true;
                }
            }
        }
        
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
        else if (parentFolder?.IsTimelineFolder == true)
        {
            folderType = DocumentType.Timeline;
        }
        else if (parentFolder?.IsPlotFolder == true)
        {
            folderType = DocumentType.Plot;
        }
        else if (parentFolder?.IsObjectFolder == true)
        {
            folderType = DocumentType.Object;
        }
        else if (parentFolder?.IsEntityFolder == true)
        {
            folderType = DocumentType.Entity;
        }
        else if (parentFolder?.IsOtherFolder == true)
        {
            folderType = DocumentType.Other;
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
        else if (docType == DocumentType.Timeline && 
                 (targetFolder.IsTimelineFolder || 
                  (targetFolder.IsSubfolder && targetFolder.FolderDocumentType == DocumentType.Timeline)))
        {
            isValidTarget = true;
        }
        else if (docType == DocumentType.Plot && 
                 (targetFolder.IsPlotFolder || 
                  (targetFolder.IsSubfolder && targetFolder.FolderDocumentType == DocumentType.Plot)))
        {
            isValidTarget = true;
        }
        else if (docType == DocumentType.Object && 
                 (targetFolder.IsObjectFolder || 
                  (targetFolder.IsSubfolder && targetFolder.FolderDocumentType == DocumentType.Object)))
        {
            isValidTarget = true;
        }
        else if (docType == DocumentType.Entity && 
                 (targetFolder.IsEntityFolder || 
                  (targetFolder.IsSubfolder && targetFolder.FolderDocumentType == DocumentType.Entity)))
        {
            isValidTarget = true;
        }
        else if (docType == DocumentType.Other && 
                 (targetFolder.IsOtherFolder || 
                  (targetFolder.IsSubfolder && targetFolder.FolderDocumentType == DocumentType.Other)))
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

        // Save expansion states before rebuilding tree
        var expansionStates = SaveExpansionStates();

        // Rebuild the tree to reflect the changes
        if (_currentProject != null)
        {
            LoadProjectIntoTree(_currentProject);
            // Restore expansion states after rebuilding - use dispatcher to ensure it happens after UI updates
            Dispatcher.UIThread.Post(() =>
            {
                RestoreExpansionStates(expansionStates);
            }, DispatcherPriority.Loaded);
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

        // Save expansion states before rebuilding tree
        var expansionStates = SaveExpansionStates();

        // Rebuild the tree to show moved items in Trashcan
        if (_currentProject != null)
        {
            LoadProjectIntoTree(_currentProject);
            // Restore expansion states after rebuilding - use dispatcher to ensure it happens after UI updates
            Dispatcher.UIThread.Post(() =>
            {
                RestoreExpansionStates(expansionStates);
            }, DispatcherPriority.Loaded);
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
            var oldTitle = item.Document.Title;
            item.Document.Title = newName;
            
            // If this is a new document (just created, not yet saved to disk), save it now after renaming
            if (!string.IsNullOrEmpty(_currentProject?.FilePath) && 
                !string.IsNullOrEmpty(item.Document.ProjectDirectory) && 
                !string.IsNullOrEmpty(item.Document.ContentFilePath))
            {
                // Check if file doesn't exist yet (new document)
                var normalizedContentPath = item.Document.ContentFilePath.Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(item.Document.ProjectDirectory, normalizedContentPath);
                
                if (!File.Exists(fullPath))
                {
                    // Regenerate ContentFilePath with new title
                    var documentsById = _currentProject.Documents.ToDictionary(d => d.Id);
                    item.Document.ContentFilePath = _projectService.GenerateContentFilePath(item.Document, documentsById);
                    
                    // Save the document now that it has its final name
                    try
                    {
                        item.Document.SaveContent();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[CommitRename] Failed to save document after rename: {ex.Message}");
                    }
                }
            }
            
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

        // Save expansion states before rebuilding tree
        var expansionStates = SaveExpansionStates();

        // Rebuild the tree
        if (_currentProject != null)
        {
            LoadProjectIntoTree(_currentProject);
            // Restore expansion states after rebuilding - use dispatcher to ensure it happens after UI updates
            Dispatcher.UIThread.Post(() =>
            {
                RestoreExpansionStates(expansionStates);
            }, DispatcherPriority.Loaded);
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
            item.IsLocationsFolder || item.IsResearchFolder || item.IsNotesFolder || item.IsOtherFolder || 
            item.IsTimelineFolder || item.IsPlotFolder || item.IsObjectFolder || item.IsEntityFolder || item.IsTrashcanFolder)
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

        // Save expansion states before rebuilding tree
        Console.WriteLine("[DeleteItem] Saving expansion states before deletion");
        var expansionStates = SaveExpansionStates();

        // Remove item from parent's children
        parentFolder.Children.Remove(item);

        // Update document orders in parent folder
        UpdateDocumentOrders(parentFolder);

        // Rebuild the tree to show moved items in Trashcan
        if (_currentProject != null)
        {
            Console.WriteLine("[DeleteItem] Rebuilding tree");
            LoadProjectIntoTree(_currentProject);
            // Restore expansion states after rebuilding - use dispatcher to ensure it happens after UI updates
            // Restore multiple times with delays to ensure it sticks (TreeView might reset expansion when items change)
            Console.WriteLine("[DeleteItem] Scheduling expansion state restoration");
            
            // Store expansion states so PropertyChanged handler can access them
            _pendingExpansionStates = expansionStates;
            
            Dispatcher.UIThread.Post(() =>
            {
                Console.WriteLine("[DeleteItem] Executing expansion state restoration (first attempt)");
                RestoreExpansionStates(expansionStates);
                
                // Restore again after a short delay to catch any TreeView updates
                Dispatcher.UIThread.Post(() =>
                {
                    Console.WriteLine("[DeleteItem] Executing expansion state restoration (second attempt)");
                    RestoreExpansionStates(expansionStates);
                    
                    // One more attempt after TreeView has fully processed
                    Dispatcher.UIThread.Post(() =>
                    {
                        Console.WriteLine("[DeleteItem] Executing expansion state restoration (third attempt)");
                        RestoreExpansionStates(expansionStates);
                        
                        // Explicitly find and expand Manuscript folder one more time
                        // Do this multiple times with increasing delays to catch any TreeView updates
                        if (ProjectTreeItems.Count > 0 && expansionStates.TryGetValue("Manuscript", out var shouldBeExpanded) && shouldBeExpanded)
                        {
                            var root = ProjectTreeItems[0];
                            var manuscriptFolder = root.Children.FirstOrDefault(c => c.IsManuscriptFolder);
                            if (manuscriptFolder != null)
                            {
                                Console.WriteLine($"[DeleteItem] Final Manuscript folder expansion check: IsExpanded={manuscriptFolder.IsExpanded}, setting to True");
                                manuscriptFolder.IsExpanded = true;
                                Console.WriteLine($"[DeleteItem] After setting: IsExpanded={manuscriptFolder.IsExpanded}");
                                
                                // Also try to find and expand the actual TreeViewItem in the UI
                                if (_parentWindow is Views.MainWindow mainWindow)
                                {
                                    var treeView = mainWindow.FindControl<Avalonia.Controls.TreeView>("projectTreeViewLeft");
                                    if (treeView != null)
                                    {
                                        // Find the TreeViewItem that corresponds to the Manuscript folder
                                        // Use Avalonia.VisualTree extension methods
                                        var treeViewItem = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(treeView)
                                            .OfType<Avalonia.Controls.TreeViewItem>()
                                            .FirstOrDefault(item => item.DataContext == manuscriptFolder);
                                        
                                        if (treeViewItem != null)
                                        {
                                            Console.WriteLine($"[DeleteItem] Found TreeViewItem for Manuscript folder, IsExpanded={treeViewItem.IsExpanded}, setting to True");
                                            treeViewItem.IsExpanded = true;
                                            Console.WriteLine($"[DeleteItem] TreeViewItem IsExpanded after setting: {treeViewItem.IsExpanded}");
                                        }
                                        else
                                        {
                                            Console.WriteLine($"[DeleteItem] TreeViewItem for Manuscript folder not found yet, will retry");
                                        }
                                    }
                                }
                                
                                // One more check after a longer delay
                                Dispatcher.UIThread.Post(() =>
                                {
                                    var root2 = ProjectTreeItems.Count > 0 ? ProjectTreeItems[0] : null;
                                    var manuscriptFolder2 = root2?.Children.FirstOrDefault(c => c.IsManuscriptFolder);
                                    if (manuscriptFolder2 != null)
                                    {
                                        Console.WriteLine($"[DeleteItem] Final+1 Manuscript folder expansion check: IsExpanded={manuscriptFolder2.IsExpanded}");
                                        if (!manuscriptFolder2.IsExpanded)
                                        {
                                            Console.WriteLine($"[DeleteItem] Manuscript folder collapsed again, forcing expansion");
                                            manuscriptFolder2.IsExpanded = true;
                                            Console.WriteLine($"[DeleteItem] After forcing: IsExpanded={manuscriptFolder2.IsExpanded}");
                                        }
                                        
                                        // Also check TreeViewItem again
                                        if (_parentWindow is Views.MainWindow mainWindow2)
                                        {
                                            var treeView = mainWindow2.FindControl<Avalonia.Controls.TreeView>("projectTreeViewLeft");
                                            if (treeView != null)
                                            {
                                                var treeViewItem = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(treeView)
                                                    .OfType<Avalonia.Controls.TreeViewItem>()
                                                    .FirstOrDefault(item => item.DataContext == manuscriptFolder2);
                                                
                                                if (treeViewItem != null && !treeViewItem.IsExpanded)
                                                {
                                                    Console.WriteLine($"[DeleteItem] TreeViewItem collapsed, forcing expansion");
                                                    treeViewItem.IsExpanded = true;
                                                }
                                            }
                                        }
                                    }
                                }, DispatcherPriority.Background);
                            }
                        }
                        
                        // Clear pending states after final restoration
                        _pendingExpansionStates = null;
                    }, DispatcherPriority.Render);
                }, DispatcherPriority.Loaded);
            }, DispatcherPriority.Loaded);
        }

        // Mark as having unsaved changes
        HasUnsavedChanges = true;
    }

    /// <summary>
    /// Saves the expansion state of all nodes in the tree, keyed by document ID or folder path.
    /// </summary>
    private Dictionary<string, bool> SaveExpansionStates()
    {
        var expansionStates = new Dictionary<string, bool>();
        var rootItem = ProjectTreeItems.FirstOrDefault();
        if (rootItem != null)
        {
            SaveExpansionStatesRecursive(rootItem, expansionStates);
            // Debug output
            Console.WriteLine($"[SaveExpansionStates] Saved {expansionStates.Count} expansion states:");
            foreach (var kvp in expansionStates)
            {
                Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
            }
        }
        return expansionStates;
    }

    /// <summary>
    /// Recursively saves expansion states of tree nodes.
    /// </summary>
    private void SaveExpansionStatesRecursive(ProjectTreeItemViewModel item, Dictionary<string, bool> expansionStates)
    {
        // Use document ID if available, otherwise use a combination of name and type
        string key;
        if (item.Document != null)
        {
            key = item.Document.Id;
        }
        else if (item.IsManuscriptFolder)
        {
            key = "Manuscript";
            Console.WriteLine($"[SaveExpansionStates] Found Manuscript folder, IsExpanded={item.IsExpanded}");
        }
        else if (item.IsCharactersFolder)
        {
            key = "Characters";
        }
        else if (item.IsLocationsFolder)
        {
            key = "Locations";
        }
        else if (item.IsResearchFolder)
        {
            key = "Research";
        }
        else if (item.IsNotesFolder)
        {
            key = "Notes";
        }
        else if (item.IsOtherFolder)
        {
            key = "Other";
        }
        else if (item.IsTimelineFolder)
        {
            key = "Timeline";
        }
        else if (item.IsPlotFolder)
        {
            key = "Plot";
        }
        else if (item.IsObjectFolder)
        {
            key = "Object";
        }
        else if (item.IsEntityFolder)
        {
            key = "Entity";
        }
        else if (item.IsTrashcanFolder)
        {
            key = "Trashcan";
        }
        else if (item.IsSubfolder)
        {
            // For subfolders, use folder path
            key = $"Folder:{item.FolderPath}:{item.FolderDocumentType}";
        }
        else
        {
            // Fallback: use name
            key = item.Name;
        }

        expansionStates[key] = item.IsExpanded;

        // Recursively save children
        foreach (var child in item.Children)
        {
            SaveExpansionStatesRecursive(child, expansionStates);
        }
    }

    /// <summary>
    /// Restores the expansion state of nodes in the tree after rebuilding.
    /// </summary>
    private void RestoreExpansionStates(Dictionary<string, bool> expansionStates)
    {
        Console.WriteLine($"[RestoreExpansionStates] Restoring {expansionStates.Count} expansion states");
        var rootItem = ProjectTreeItems.FirstOrDefault();
        if (rootItem != null)
        {
            RestoreExpansionStatesRecursive(rootItem, expansionStates);
            
            // Also restore TreeViewItem expansion states directly in the UI
            RestoreTreeViewItemExpansionStates(rootItem, expansionStates);
        }
        else
        {
            Console.WriteLine("[RestoreExpansionStates] No root item found!");
        }
    }
    
    private void RestoreTreeViewItemExpansionStates(ProjectTreeItemViewModel rootItem, Dictionary<string, bool> expansionStates)
    {
        if (_parentWindow is not Views.MainWindow mainWindow)
            return;
            
        var treeView = mainWindow.FindControl<Avalonia.Controls.TreeView>("projectTreeViewLeft");
        if (treeView == null)
            return;
        
        // Find all TreeViewItems and restore their expansion states
        RestoreTreeViewItemExpansionStatesRecursive(rootItem, treeView, expansionStates);
    }
    
    private void RestoreTreeViewItemExpansionStatesRecursive(ProjectTreeItemViewModel viewModelItem, Avalonia.Controls.TreeView treeView, Dictionary<string, bool> expansionStates)
    {
        // Generate the key for this item (same logic as in RestoreExpansionStatesRecursive)
        string key;
        if (viewModelItem.Document != null)
        {
            key = viewModelItem.Document.Id;
        }
        else if (viewModelItem.IsRoot)
        {
            key = viewModelItem.Name;
        }
        else if (viewModelItem.IsManuscriptFolder)
        {
            key = "Manuscript";
        }
        else if (viewModelItem.IsCharactersFolder)
        {
            key = "Characters";
        }
        else if (viewModelItem.IsLocationsFolder)
        {
            key = "Locations";
        }
        else if (viewModelItem.IsResearchFolder)
        {
            key = "Research";
        }
        else if (viewModelItem.IsNotesFolder)
        {
            key = "Notes";
        }
        else if (viewModelItem.IsOtherFolder)
        {
            key = "Other";
        }
        else if (viewModelItem.IsTimelineFolder)
        {
            key = "Timeline";
        }
        else if (viewModelItem.IsPlotFolder)
        {
            key = "Plot";
        }
        else if (viewModelItem.IsObjectFolder)
        {
            key = "Object";
        }
        else if (viewModelItem.IsEntityFolder)
        {
            key = "Entity";
        }
        else if (viewModelItem.IsTrashcanFolder)
        {
            key = "Trashcan";
        }
        else if (!string.IsNullOrEmpty(viewModelItem.FolderPath))
        {
            key = $"Folder:{viewModelItem.FolderPath}";
        }
        else
        {
            key = viewModelItem.Name;
        }
        
        // Check if this item should be expanded
        if (expansionStates.TryGetValue(key, out var shouldBeExpanded) && shouldBeExpanded)
        {
            // Find the TreeViewItem for this ViewModel item
            var treeViewItem = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(treeView)
                .OfType<Avalonia.Controls.TreeViewItem>()
                .FirstOrDefault(item => item.DataContext == viewModelItem);
            
            if (treeViewItem != null)
            {
                if (!treeViewItem.IsExpanded)
                {
                    Console.WriteLine($"[RestoreTreeViewItemExpansionStates] Found TreeViewItem for {key}, IsExpanded={treeViewItem.IsExpanded}, setting to True");
                    treeViewItem.IsExpanded = true;
                    Console.WriteLine($"[RestoreTreeViewItemExpansionStates] TreeViewItem IsExpanded after setting: {treeViewItem.IsExpanded}");
                }
            }
        }
        
        // Recursively process children
        foreach (var child in viewModelItem.Children)
        {
            RestoreTreeViewItemExpansionStatesRecursive(child, treeView, expansionStates);
        }
    }

    /// <summary>
    /// Recursively restores expansion states of tree nodes.
    /// </summary>
    private void RestoreExpansionStatesRecursive(ProjectTreeItemViewModel item, Dictionary<string, bool> expansionStates)
    {
        // Use document ID if available, otherwise use a combination of name and type
        string key;
        if (item.Document != null)
        {
            key = item.Document.Id;
        }
        else if (item.IsManuscriptFolder)
        {
            key = "Manuscript";
            Console.WriteLine($"[RestoreExpansionStates] Found Manuscript folder, key={key}");
        }
        else if (item.IsCharactersFolder)
        {
            key = "Characters";
        }
        else if (item.IsLocationsFolder)
        {
            key = "Locations";
        }
        else if (item.IsResearchFolder)
        {
            key = "Research";
        }
        else if (item.IsNotesFolder)
        {
            key = "Notes";
        }
        else if (item.IsOtherFolder)
        {
            key = "Other";
        }
        else if (item.IsTimelineFolder)
        {
            key = "Timeline";
        }
        else if (item.IsPlotFolder)
        {
            key = "Plot";
        }
        else if (item.IsObjectFolder)
        {
            key = "Object";
        }
        else if (item.IsEntityFolder)
        {
            key = "Entity";
        }
        else if (item.IsTrashcanFolder)
        {
            key = "Trashcan";
        }
        else if (item.IsSubfolder)
        {
            // For subfolders, use folder path
            key = $"Folder:{item.FolderPath}:{item.FolderDocumentType}";
        }
        else
        {
            // Fallback: use name
            key = item.Name;
        }

        // Restore expansion state if we have it saved
        if (expansionStates.TryGetValue(key, out var wasExpanded))
        {
            var beforeState = item.IsExpanded;
            Console.WriteLine($"[RestoreExpansionStates] Restoring {key}: {wasExpanded} (was {beforeState})");
            item.IsExpanded = wasExpanded;
            // Verify it was set correctly
            if (item.IsExpanded != wasExpanded)
            {
                Console.WriteLine($"[RestoreExpansionStates] WARNING: Failed to set {key} to {wasExpanded}, current state is {item.IsExpanded}");
            }
            else if (item.IsExpanded != beforeState)
            {
                Console.WriteLine($"[RestoreExpansionStates] Successfully changed {key} from {beforeState} to {item.IsExpanded}");
            }
        }
        // Special case: if manuscript folder doesn't have a saved state, default to expanded
        // This ensures it stays open even if expansion state wasn't captured
        else if (item.IsManuscriptFolder)
        {
            Console.WriteLine($"[RestoreExpansionStates] Manuscript folder not in saved states, defaulting to expanded (was {item.IsExpanded})");
            item.IsExpanded = true;
            Console.WriteLine($"[RestoreExpansionStates] Manuscript folder IsExpanded after setting: {item.IsExpanded}");
        }
        else
        {
            Console.WriteLine($"[RestoreExpansionStates] No saved state for {key}, keeping current state: {item.IsExpanded}");
        }

        // Recursively restore children
        foreach (var child in item.Children)
        {
            RestoreExpansionStatesRecursive(child, expansionStates);
        }
        
        // After restoring children, verify manuscript folder is still expanded
        if (item.IsManuscriptFolder)
        {
            Console.WriteLine($"[RestoreExpansionStates] Manuscript folder IsExpanded after restoring children: {item.IsExpanded}");
            // Force it to be expanded if it should be
            if (expansionStates.TryGetValue("Manuscript", out var shouldBeExpanded) && shouldBeExpanded && !item.IsExpanded)
            {
                Console.WriteLine($"[RestoreExpansionStates] WARNING: Manuscript folder was collapsed after restoring children, forcing expansion");
                item.IsExpanded = true;
                Console.WriteLine($"[RestoreExpansionStates] Manuscript folder IsExpanded after forcing: {item.IsExpanded}");
            }
        }
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


        // If no documents remain in this folder, we can optionally clean up the folder structure
        // The tree rebuild will automatically not show empty folders, so this is mainly for file system cleanup
        if (documentsInFolder.Count == 0)
        {
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
            return $"manuscript/{sanitizedFolderPath}/{sanitizedChapterTitle}/{sanitizedSceneTitle}.md";
        }
        return $"manuscript/{sanitizedChapterTitle}/{sanitizedSceneTitle}.md";
    }

    /// <summary>
    /// Moves a file to the Trashcan folder, preserving the directory structure.
    /// For example, locations/location1.md becomes Trashcan/locations/location1.md
    /// </summary>
    private void MoveFileToTrashcan(Document document, string projectDirectory)
    {
        
        // Don't move if already in Trashcan
        if (!string.IsNullOrEmpty(document.ContentFilePath) && 
            document.ContentFilePath.StartsWith("Trashcan/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            string normalizedContentPath;
            
            // If ContentFilePath is empty, generate it based on current document properties to preserve folder structure
            if (string.IsNullOrEmpty(document.ContentFilePath))
            {
                // Build a dictionary of documents by ID for quick lookup (needed for scene path generation)
                var documentsById = _currentProject?.Documents.ToDictionary(d => d.Id) ?? new Dictionary<string, Document>();
                normalizedContentPath = _projectService.GenerateContentFilePath(document, documentsById);
            }
            else
            {
                // Normalize path separators for ContentFilePath (uses forward slashes)
                normalizedContentPath = document.ContentFilePath.Replace('\\', '/');
            }
            
            // Convert forward slashes to platform-specific path separators for file system operations
            var sourceFilePath = Path.Combine(projectDirectory, normalizedContentPath.Replace('/', Path.DirectorySeparatorChar));
            
            // Preserve the directory structure: locations/subfolder/location1.md -> Trashcan/locations/subfolder/location1.md
            // Use forward slashes for ContentFilePath
            var trashcanPath = "Trashcan/" + normalizedContentPath;
            var targetFilePath = Path.Combine(projectDirectory, trashcanPath.Replace('/', Path.DirectorySeparatorChar));
            var targetDirectory = Path.GetDirectoryName(targetFilePath);

            // Update document's ContentFilePath first so it appears in Trashcan even if file move fails
            document.ContentFilePath = trashcanPath;
            // Clear cached content so it will reload from the new location
            // The Content getter will automatically reload from the new file path when accessed

            // Only move the file if it exists
            if (File.Exists(sourceFilePath))
            {
                // Ensure target directory exists
                if (!string.IsNullOrEmpty(targetDirectory) && !Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                // Move the file
                File.Move(sourceFilePath, targetFilePath, overwrite: true);
            }
            else
            {
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
            _lastSaveTime = DateTime.Now; // Set save time when loading project
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
            
            // Apply theme change
            if (Application.Current is App app)
            {
                app.ApplyTheme(prefs.Theme);
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
                        // Use Dispatcher to load project after UI is initialized
                        Dispatcher.UIThread.Post(async () =>
                        {
                            await OpenRecentProject(lastProject.FilePath);
                        }, DispatcherPriority.Loaded);
                    }
                    else
                    {
                    }
                }
                else
                {
                }
            }
            else
            {
            }
        }
        catch (Exception ex)
        {
        }
    }

    /// <summary>
    /// Validates metadata references in a document to ensure they point to actual documents.
    /// Logs warnings for invalid references but doesn't prevent saving.
    /// </summary>
    private void ValidateMetadataReferences(Document document)
    {
        if (_currentProject == null)
            return;

        // Validate POV reference (should be a Character)
        if (!string.IsNullOrWhiteSpace(document.Pov))
        {
            if (!_metadataReferenceService.IsValidCharacterReference(document.Pov, _currentProject))
            {
                Console.WriteLine($"[MetadataValidation] Warning: POV '{document.Pov}' does not reference a valid Character document");
            }
        }

        // Validate Focus reference (should be a Character)
        if (!string.IsNullOrWhiteSpace(document.Focus))
        {
            if (!_metadataReferenceService.IsValidCharacterReference(document.Focus, _currentProject))
            {
                Console.WriteLine($"[MetadataValidation] Warning: Focus '{document.Focus}' does not reference a valid Character document");
            }
        }

        // Validate Characters array (should all be Characters)
        if (document.Characters != null && document.Characters.Count > 0)
        {
            var invalidCharacters = _metadataReferenceService.GetInvalidCharacterReferences(document.Characters, _currentProject);
            if (invalidCharacters.Count > 0)
            {
                Console.WriteLine($"[MetadataValidation] Warning: The following character references are invalid: {string.Join(", ", invalidCharacters)}");
            }
        }

        // Validate Timeline reference (should be a Timeline document)
        if (!string.IsNullOrWhiteSpace(document.Timeline))
        {
            if (!_metadataReferenceService.IsValidTimelineReference(document.Timeline, _currentProject))
            {
                Console.WriteLine($"[MetadataValidation] Warning: Timeline '{document.Timeline}' does not reference a valid Timeline document");
            }
        }

        // Validate Plot reference (should be a Plot document)
        if (!string.IsNullOrWhiteSpace(document.Plot))
        {
            if (!_metadataReferenceService.IsValidPlotReference(document.Plot, _currentProject))
            {
                Console.WriteLine($"[MetadataValidation] Warning: Plot '{document.Plot}' does not reference a valid Plot document");
            }
        }

        // Validate Objects array (should all be Object documents)
        if (document.Objects != null && document.Objects.Count > 0)
        {
            var invalidObjects = _metadataReferenceService.GetInvalidObjectReferences(document.Objects, _currentProject);
            if (invalidObjects.Count > 0)
            {
                Console.WriteLine($"[MetadataValidation] Warning: The following object references are invalid: {string.Join(", ", invalidObjects)}");
            }
        }

        // Validate Entities array (should all be Entity documents)
        if (document.Entities != null && document.Entities.Count > 0)
        {
            var invalidEntities = _metadataReferenceService.GetInvalidEntityReferences(document.Entities, _currentProject);
            if (invalidEntities.Count > 0)
            {
                Console.WriteLine($"[MetadataValidation] Warning: The following entity references are invalid: {string.Join(", ", invalidEntities)}");
            }
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
