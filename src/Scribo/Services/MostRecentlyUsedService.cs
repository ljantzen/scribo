using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Scribo.Services;

public class MostRecentlyUsedService
{
    private const int MaxRecentProjects = 10;
    private readonly string _mruFilePath;
    private List<RecentProject> _recentProjects = new();

    public MostRecentlyUsedService()
    {
        // Store MRU list in user's app data directory
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var scriboDataPath = Path.Combine(appDataPath, "Scribo");
        
        if (!Directory.Exists(scriboDataPath))
        {
            Directory.CreateDirectory(scriboDataPath);
        }

        _mruFilePath = Path.Combine(scriboDataPath, "recent-projects.json");
        LoadRecentProjects();
    }

    public IReadOnlyList<RecentProject> GetRecentProjects()
    {
        return _recentProjects.OrderByDescending(p => p.LastOpenedAt).ToList().AsReadOnly();
    }

    public void AddProject(string filePath, string projectName)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        // Remove if already exists
        _recentProjects.RemoveAll(p => p.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));

        // Add to the beginning
        _recentProjects.Insert(0, new RecentProject
        {
            FilePath = filePath,
            ProjectName = projectName,
            LastOpenedAt = DateTime.Now
        });

        // Limit to max items
        if (_recentProjects.Count > MaxRecentProjects)
        {
            _recentProjects = _recentProjects.Take(MaxRecentProjects).ToList();
        }

        SaveRecentProjects();
    }

    public void RemoveProject(string filePath)
    {
        _recentProjects.RemoveAll(p => p.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase));
        SaveRecentProjects();
    }

    public void Clear()
    {
        _recentProjects.Clear();
        SaveRecentProjects();
    }

    private void LoadRecentProjects()
    {
        if (!File.Exists(_mruFilePath))
        {
            _recentProjects = new List<RecentProject>();
            return;
        }

        try
        {
            var json = File.ReadAllText(_mruFilePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var loaded = JsonSerializer.Deserialize<List<RecentProject>>(json, options);
            _recentProjects = loaded ?? new List<RecentProject>();

            // Remove projects that no longer exist
            _recentProjects = _recentProjects
                .Where(p => File.Exists(p.FilePath))
                .ToList();
        }
        catch
        {
            _recentProjects = new List<RecentProject>();
        }
    }

    private void SaveRecentProjects()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(_recentProjects, options);
            File.WriteAllText(_mruFilePath, json);
        }
        catch
        {
            // Silently fail if we can't save
        }
    }
}

public class RecentProject
{
    public string FilePath { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public DateTime LastOpenedAt { get; set; } = DateTime.Now;
}
