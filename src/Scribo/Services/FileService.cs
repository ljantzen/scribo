using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Scribo.Services;

public class FileService
{
    public async Task<string> LoadFileAsync(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        try
        {
            return await File.ReadAllTextAsync(filePath, Encoding.UTF8);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access denied to file: {filePath}", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new IOException($"Directory not found for file: {filePath}", ex);
        }
    }

    public string LoadFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        try
        {
            return File.ReadAllText(filePath, Encoding.UTF8);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access denied to file: {filePath}", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new IOException($"Directory not found for file: {filePath}", ex);
        }
    }

    public async Task SaveFileAsync(string filePath, string content)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (content == null)
            throw new ArgumentNullException(nameof(content));

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access denied to file: {filePath}", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new IOException($"Directory not found for file: {filePath}", ex);
        }
    }

    public void SaveFile(string filePath, string content)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

        if (content == null)
            throw new ArgumentNullException(nameof(content));

        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(filePath, content, Encoding.UTF8);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new IOException($"Access denied to file: {filePath}", ex);
        }
        catch (DirectoryNotFoundException ex)
        {
            throw new IOException($"Directory not found for file: {filePath}", ex);
        }
    }

    public bool FileExists(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        return File.Exists(filePath);
    }

    public string GetFileName(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return string.Empty;

        return Path.GetFileName(filePath);
    }

    public string GetFileNameWithoutExtension(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return string.Empty;

        return Path.GetFileNameWithoutExtension(filePath);
    }

    public string GetDirectoryName(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return string.Empty;

        return Path.GetDirectoryName(filePath) ?? string.Empty;
    }

    public string CombinePath(string path1, string path2)
    {
        return Path.Combine(path1, path2);
    }
}
