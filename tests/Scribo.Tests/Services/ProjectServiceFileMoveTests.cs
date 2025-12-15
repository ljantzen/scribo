using FluentAssertions;
using Scribo.Models;
using Scribo.Services;
using System.IO;
using Xunit;

namespace Scribo.Tests.Services;

public class ProjectServiceFileMoveTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ProjectService _service;

    public ProjectServiceFileMoveTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _service = new ProjectService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void SaveProject_ShouldMoveFileWhenFolderPathChanges()
    {
        // Arrange
        var projectPath = Path.Combine(_testDirectory, "test-project.json");
        var project = new Project
        {
            Name = "Test Project",
            Documents = new List<Document>
            {
                new Document
                {
                    Title = "Character 1",
                    Type = DocumentType.Character,
                    FolderPath = "",
                    Content = "Character content",
                    ContentFilePath = "characters/Character-1.md"
                }
            }
        };

        // Save project initially
        _service.SaveProject(project, projectPath);
        var oldFilePath = Path.Combine(_testDirectory, "characters", "Character-1.md");
        File.Exists(oldFilePath).Should().BeTrue();

        // Change folder path
        project.Documents[0].FolderPath = "Main Characters";
        project.Documents[0].ContentFilePath = "characters/Character-1.md"; // Keep old path

        // Act
        _service.SaveProject(project, projectPath);

        // Assert
        var newFilePath = Path.Combine(_testDirectory, "characters", "Main-Characters", "Character-1.md");
        File.Exists(newFilePath).Should().BeTrue("New file should exist");
        File.Exists(oldFilePath).Should().BeFalse("Old file should be deleted");
        File.ReadAllText(newFilePath).Should().Be("Character content");
        project.Documents[0].ContentFilePath.Should().Be("characters/Main-Characters/Character-1.md");
    }

    [Fact]
    public void SaveProject_ShouldMoveSceneFileWhenParentChapterChanges()
    {
        // Arrange
        var projectPath = Path.Combine(_testDirectory, "test-project.json");
        var chapter1 = new Document { Title = "Chapter 1", Type = DocumentType.Chapter, Id = "chapter1" };
        var chapter2 = new Document { Title = "Chapter 2", Type = DocumentType.Chapter, Id = "chapter2" };
        var scene = new Document
        {
            Title = "Scene 1",
            Type = DocumentType.Scene,
            ParentId = chapter1.Id,
            Content = "Scene content",
            ContentFilePath = "Manuscript/Chapter-1/Scene-1.md"
        };

        var project = new Project
        {
            Name = "Test Project",
            Documents = new List<Document> { chapter1, chapter2, scene }
        };

        // Save project initially
        _service.SaveProject(project, projectPath);
        var oldFilePath = Path.Combine(_testDirectory, "Manuscript", "Chapter-1", "Scene-1.md");
        File.Exists(oldFilePath).Should().BeTrue();

        // Change parent chapter
        scene.ParentId = chapter2.Id;
        scene.FolderPath = ""; // Chapter 2 has no folder path
        scene.ContentFilePath = "Manuscript/Chapter-1/Scene-1.md"; // Keep old path

        // Act
        _service.SaveProject(project, projectPath);

        // Assert
        var newFilePath = Path.Combine(_testDirectory, "Manuscript", "Chapter-2", "Scene-1.md");
        File.Exists(newFilePath).Should().BeTrue("New file should exist");
        File.Exists(oldFilePath).Should().BeFalse("Old file should be deleted");
        File.ReadAllText(newFilePath).Should().Be("Scene content");
        scene.ContentFilePath.Should().Be("Manuscript/Chapter-2/Scene-1.md");
    }

    [Fact]
    public void SaveProject_ShouldNotMoveFileIfPathUnchanged()
    {
        // Arrange
        var projectPath = Path.Combine(_testDirectory, "test-project.json");
        var document = new Document
        {
            Title = "Character 1",
            Type = DocumentType.Character,
            FolderPath = "",
            Content = "Character content",
            ContentFilePath = "characters/Character-1.md"
        };

        var project = new Project
        {
            Name = "Test Project",
            Documents = new List<Document> { document }
        };

        // Save project initially
        _service.SaveProject(project, projectPath);
        var filePath = Path.Combine(_testDirectory, "characters", "Character-1.md");
        var originalWriteTime = File.GetLastWriteTime(filePath);
        
        // Wait a bit to ensure different write time
        System.Threading.Thread.Sleep(100);

        // Act - Save again without changing folder path
        _service.SaveProject(project, projectPath);

        // Assert - File should still exist at same location
        File.Exists(filePath).Should().BeTrue();
        document.ContentFilePath.Should().Be("characters/Character-1.md");
    }
}
