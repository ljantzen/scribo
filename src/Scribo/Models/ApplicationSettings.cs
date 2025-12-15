using System.Collections.Generic;

namespace Scribo.Models;

public class ApplicationSettings
{
    public Dictionary<string, string> KeyboardShortcuts { get; set; } = new();
    
    // Other application settings
    public bool AutoSave { get; set; } = true;
    public int AutoSaveIntervalMinutes { get; set; } = 5;
    public bool BackupOnSave { get; set; } = true;
    public int MaxBackups { get; set; } = 10;
    public string DefaultFontFamily { get; set; } = "Consolas";
    public int DefaultFontSize { get; set; } = 14;
    public bool WordWrap { get; set; } = true;
    public bool ShowLineNumbers { get; set; } = false;
    public bool ShowRuler { get; set; } = false;
    public string Theme { get; set; } = "Light";
    public bool ShowWordCountInStatusBar { get; set; } = true;
    public bool ShowCharacterCountInStatusBar { get; set; } = true;
    public bool ShowPageCountInStatusBar { get; set; } = false;

    public static Dictionary<string, string> GetDefaultShortcuts()
    {
        return new Dictionary<string, string>
        {
            { "NewProject", "Ctrl+N" },
            { "OpenProject", "Ctrl+O" },
            { "SaveProject", "Ctrl+S" },
            { "SaveProjectAs", "Ctrl+Shift+S" },
            { "Preferences", "Ctrl+Comma" },
            { "Exit", "Ctrl+Q" },
            { "Undo", "Ctrl+Z" },
            { "Redo", "Ctrl+Y" },
            { "Cut", "Ctrl+X" },
            { "Copy", "Ctrl+C" },
            { "Paste", "Ctrl+V" },
            { "ToggleViewMode", "Ctrl+P" },
            { "Rename", "F2" }
        };
    }
}
