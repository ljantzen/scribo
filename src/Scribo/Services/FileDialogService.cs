using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace Scribo.Services;

public class FileDialogService
{
    private readonly Window? _parentWindow;

    public FileDialogService(Window? parentWindow = null)
    {
        _parentWindow = parentWindow;
    }

    public async Task<string?> OpenFileDialogAsync(string? title = null, IReadOnlyList<FilePickerFileType>? fileTypes = null)
    {
        if (_parentWindow == null)
            return null;

        var topLevel = TopLevel.GetTopLevel(_parentWindow);
        if (topLevel == null)
            return null;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title ?? "Open File",
            AllowMultiple = false,
            FileTypeFilter = fileTypes ?? new[]
            {
                FilePickerFileTypes.All,
                new FilePickerFileType("Text Files")
                {
                    Patterns = new[] { "*.txt", "*.md", "*.rtf" }
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        if (files.Count > 0)
        {
            return files[0].Path.LocalPath;
        }

        return null;
    }

    public async Task<string?> SaveFileDialogAsync(string? title = null, string? suggestedFileName = null, IReadOnlyList<FilePickerFileType>? fileTypes = null)
    {
        if (_parentWindow == null)
            return null;

        var topLevel = TopLevel.GetTopLevel(_parentWindow);
        if (topLevel == null)
            return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title ?? "Save File",
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = fileTypes ?? new[]
            {
                new FilePickerFileType("Text Files")
                {
                    Patterns = new[] { "*.txt" }
                },
                new FilePickerFileType("Markdown Files")
                {
                    Patterns = new[] { "*.md" }
                },
                new FilePickerFileType("All Files")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        return file?.Path.LocalPath;
    }

    public IReadOnlyList<FilePickerFileType> GetTextFileTypes()
    {
        return new[]
        {
            new FilePickerFileType("Text Files")
            {
                Patterns = new[] { "*.txt" }
            },
            new FilePickerFileType("Markdown Files")
            {
                Patterns = new[] { "*.md" }
            },
            new FilePickerFileType("All Files")
            {
                Patterns = new[] { "*.*" }
            }
        };
    }

    public IReadOnlyList<FilePickerFileType> GetProjectFileTypes()
    {
        return new[]
        {
            new FilePickerFileType("Scribo Project Files")
            {
                Patterns = new[] { "*.json" }
            },
            new FilePickerFileType("All Files")
            {
                Patterns = new[] { "*.*" }
            }
        };
    }
}
