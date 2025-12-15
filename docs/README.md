# Scribo - Writing Assistant

A modern text editor application built with .NET 10 and AvaloniaUI, designed specifically for writers. Scribo is a Scrivener competitor that provides a comprehensive writing environment with project management capabilities.

## Features

### Core Features

- **Modern GUI**: Built with AvaloniaUI for cross-platform support (Windows, Linux, macOS)
- **Project Management**: TreeView-based project structure for organizing documents
- **Flexible Layout**: Project tree can be positioned on the left or right side
- **Text Editing**: Full-featured text editor with word wrap and scrolling
- **Statistics**: Word count, character count, and other writing statistics
- **Plugin System**: Extensible plugin architecture for custom functionality
- **Test Coverage**: Comprehensive test suite covering all functionality

### Document Organization

- **Hierarchical Structure**: Organize your writing with chapters, scenes, characters, locations, research, and notes
- **Subfolders**: Create subfolders within main folders (Manuscript, Characters, Locations, Research, Notes) for better organization
- **Drag and Drop**: Move documents between folders and reorder them within folders
  - Drag scenes between chapters
  - Drag characters, locations, and research documents between folders and subfolders
  - Reorder documents within folders using drag-and-drop or context menu (Move Up/Down)
- **Inline Renaming**: Rename documents and folders directly in the tree view
- **Context Menus**: Right-click context menus for quick access to common operations

### Project Management

- **Project Properties**: Edit project metadata (title, author, ISBN, etc.) through the project properties dialog
- **Most Recently Used (MRU)**: Quick access to recently opened projects via the File menu
- **Auto-Save**: Automatic saving of document content to markdown files
- **File Organization**: Content stored in separate markdown files organized by type and folder structure
- **Empty Project Template**: Start with a clean project structure or use sample documents

### Document Types

- **Chapters**: Main manuscript chapters with their own content
- **Scenes**: Scenes belonging to chapters
- **Characters**: Character profiles and descriptions
- **Locations**: Location descriptions and details
- **Research**: Research notes and materials
- **Notes**: General notes and plot ideas

## Requirements

- .NET 10.0 SDK or later
- AvaloniaUI 11.0.7

## Building

```bash
dotnet restore
dotnet build
```

## Running

```bash
dotnet run --project src/Scribo/Scribo.csproj
```

## Testing

```bash
dotnet test
```

## Usage Guide

### Creating a New Project

1. File → New Project
2. A new project is created with sample documents to get you started
3. You can delete the sample documents or use them as templates

### Adding Documents

- **Add Chapter**: Right-click on "Manuscript" folder → "Add Chapter"
- **Add Scene**: Right-click on a chapter → "Create scene"
- **Add Character**: Right-click on "Characters" folder → "Add Character"
- **Add Location**: Right-click on "Locations" folder → "Add Location"
- **Add Note**: Right-click on "Notes" or "Research" folder → "Add Note"
- **Create Subfolder**: Right-click on any main folder → "Create Subfolder"

### Organizing Documents

- **Drag and Drop**: Click and drag documents to move them between folders
  - Scenes can be moved between chapters
  - Characters, locations, and research can be moved between folders/subfolders
- **Reorder**: Right-click on a document → "Move Up" or "Move Down" to reorder within a folder
- **Rename**: Right-click on a document or folder → "Rename" (or double-click)

### Project Properties

- Right-click on the project root node → "Project Properties"
- Edit project metadata, word count targets, and other settings
- Changes are saved when you save the project

### File Structure

Projects are saved as JSON files (`.json` extension). Document content is stored in separate markdown files (`.md` extension) organized in folders:

```
project-name/
├── project.json                    # Project metadata and structure
├── Manuscript/
│   ├── Chapter-1/
│   │   ├── content.md              # Chapter content
│   │   └── Scene-1.md              # Scene content
│   └── Chapter-2/
│       └── content.md
├── characters/
│   └── Character-Name.md
├── locations/
│   └── Location-Name.md
├── research/
│   └── Research-Topic.md
└── notes/
    └── Note-Name.md
```

## Plugin System

Scribo includes a comprehensive plugin system that allows you to extend the application's functionality.

### Creating a Plugin

To create a plugin, implement the `IPlugin` interface or inherit from `PluginBase`:

```csharp
using Scribo.Plugins;

public class MyPlugin : PluginBase
{
    public override string Id => "my-plugin";
    public override string Name => "My Plugin";
    public override string Version => "1.0.0";
    public override string Description => "A sample plugin";
    public override string Author => "Your Name";

    public override void OnEnabled()
    {
        // Plugin enabled logic
    }

    public override void OnDisabled()
    {
        // Plugin disabled logic
    }
}
```

### Plugin Management

Plugins can be managed through the Plugin Manager window:
- **Access**: File → Preferences → Plugin Manager (or Tools → Plugin Manager)
- **Install**: Copy plugin DLL files to the plugins directory
- **Enable/Disable**: Toggle plugins on or off
- **Remove**: Uninstall plugins

Plugin files are stored in:
- **Windows**: `%APPDATA%\Scribo\Plugins`
- **Linux/Mac**: `~/.config/Scribo/Plugins` or `~/Library/Application Support/Scribo/Plugins`

### Plugin Context

Plugins receive a `IPluginContext` that provides:
- Access to the main window
- Service registration and retrieval
- Menu item registration
- Logging capabilities

## Project Structure

```
Scribo/
├── src/
│   └── Scribo/
│       ├── Models/          # Domain models (Project, Document, PluginInfo)
│       ├── Plugins/         # Plugin interfaces and base classes
│       ├── Services/        # Business logic services
│       ├── ViewModels/      # MVVM view models
│       └── Views/           # UI views
└── tests/
    └── Scribo.Tests/        # Unit tests
```

## Architecture

The application follows the MVVM (Model-View-ViewModel) pattern:

- **Models**: Domain entities (Project, Document, PluginInfo)
- **ViewModels**: Presentation logic using CommunityToolkit.Mvvm
- **Views**: AvaloniaUI XAML views
- **Services**: Business logic and data persistence
- **Plugins**: Extensible plugin system for custom functionality

## Keyboard Shortcuts

- **Ctrl+N**: New Project
- **Ctrl+O**: Open Project
- **Ctrl+S**: Save Project
- **Ctrl+Shift+S**: Save Project As
- **Ctrl+Q**: Quit (Linux/Mac)
- **Alt+F4**: Quit (Windows)

## Recent Changes

### Drag and Drop
- Added drag-and-drop support for moving documents between folders
- Scenes can be moved between chapters
- Characters, locations, and research can be moved between folders/subfolders
- Files are automatically moved to the correct location when saving

### Document Reordering
- Added "Move Up" and "Move Down" context menu items
- Documents maintain their order within folders
- Order is persisted in the project file

### Subfolders
- Support for subfolders within main folders
- Organize documents hierarchically
- Subfolders can be renamed and reorganized

### File Management
- Document content stored in separate markdown files
- Files are automatically organized by type and folder structure
- Files are moved when documents are moved between folders

## License

This project is provided as-is for development purposes.
