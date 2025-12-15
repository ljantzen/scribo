using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Scribo.Models;

namespace Scribo.ViewModels;

public partial class ProjectPropertiesViewModel : ViewModelBase
{
    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string author = string.Empty;

    [ObservableProperty]
    private string publisher = string.Empty;

    [ObservableProperty]
    private string isbn = string.Empty;

    [ObservableProperty]
    private string copyright = string.Empty;

    [ObservableProperty]
    private string comments = string.Empty;

    [ObservableProperty]
    private string projectNotes = string.Empty;

    [ObservableProperty]
    private int targetWordCount = 0;

    [ObservableProperty]
    private int targetPageCount = 0;

    [ObservableProperty]
    private DateTime? targetDate;

    private ProjectMetadata _originalMetadata;
    private readonly Action<ProjectMetadata>? _onSave;

    public ProjectPropertiesViewModel(ProjectMetadata metadata, Action<ProjectMetadata>? onSave = null)
    {
        _originalMetadata = metadata;
        _onSave = onSave;
        LoadFromMetadata(metadata);
    }

    private void LoadFromMetadata(ProjectMetadata metadata)
    {
        Title = metadata.Title;
        Author = metadata.Author;
        Publisher = metadata.Publisher;
        Isbn = metadata.ISBN;
        Copyright = metadata.Copyright;
        Comments = metadata.Comments;
        ProjectNotes = metadata.ProjectNotes;
        TargetWordCount = metadata.WordCountTargets?.TargetWordCount ?? 0;
        TargetPageCount = metadata.WordCountTargets?.TargetPageCount ?? 0;
        TargetDate = metadata.WordCountTargets?.TargetDate;
    }

    [RelayCommand]
    private void Save()
    {
        var updatedMetadata = new ProjectMetadata
        {
            Title = Title,
            Author = Author,
            Publisher = Publisher,
            ISBN = Isbn,
            Copyright = Copyright,
            Comments = Comments,
            ProjectNotes = ProjectNotes,
            WordCountTargets = new WordCountTargets
            {
                TargetWordCount = TargetWordCount,
                TargetPageCount = TargetPageCount,
                TargetDate = TargetDate
            },
            // Preserve other metadata
            Statistics = _originalMetadata.Statistics,
            CurrentSession = _originalMetadata.CurrentSession,
            SessionHistory = _originalMetadata.SessionHistory,
            Keywords = _originalMetadata.Keywords,
            Tags = _originalMetadata.Tags,
            CustomFields = _originalMetadata.CustomFields,
            CompileSettings = _originalMetadata.CompileSettings,
            DocumentOrganization = _originalMetadata.DocumentOrganization,
            ResearchSettings = _originalMetadata.ResearchSettings,
            Settings = _originalMetadata.Settings,
            Version = _originalMetadata.Version,
            CreatedAt = _originalMetadata.CreatedAt,
            ModifiedAt = DateTime.Now,
            LastOpenedAt = _originalMetadata.LastOpenedAt
        };

        _onSave?.Invoke(updatedMetadata);
    }

    [RelayCommand]
    private void Cancel()
    {
        // Cancel is handled by closing the window
    }
}
