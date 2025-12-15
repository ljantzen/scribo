using FluentAssertions;
using Scribo.Models;
using Scribo.Services;
using System.IO;
using System.Text;
using Xunit;

namespace Scribo.Tests.Services;

public class ProjectServiceAdditionalTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ProjectService _service;

    public ProjectServiceAdditionalTests()
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
    public void SaveProject_ShouldThrowWhenFilePathIsNull()
    {
        // Arrange
        var project = new Project { Name = "Test" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.SaveProject(project, null!));
    }

    [Fact]
    public void SaveProject_ShouldThrowWhenFilePathIsEmpty()
    {
        // Arrange
        var project = new Project { Name = "Test" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.SaveProject(project, string.Empty));
    }

    [Fact]
    public void SaveProject_ShouldThrowWhenFilePathIsWhitespace()
    {
        // Arrange
        var project = new Project { Name = "Test" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.SaveProject(project, "   "));
    }

    [Fact]
    public void LoadProject_ShouldThrowWhenFilePathIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.LoadProject(null!));
    }

    [Fact]
    public void LoadProject_ShouldThrowWhenFilePathIsEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.LoadProject(string.Empty));
    }

    [Fact]
    public void LoadProject_ShouldThrowWhenFileContainsInvalidJson()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "invalid.json");
        File.WriteAllText(filePath, "This is not valid JSON");

        // Act & Assert
        Assert.ThrowsAny<Exception>(() => _service.LoadProject(filePath));
    }

    [Fact]
    public void SaveAndLoadProject_ShouldPreserveComplexDocumentProperties()
    {
        // Arrange
        var originalProject = new Project
        {
            Name = "Complex Project",
            Documents = new List<Document>
            {
                new Document
                {
                    Title = "Chapter 1",
                    Content = "This is chapter 1 content with\nmultiple lines.",
                    Type = DocumentType.Chapter,
                    CreatedAt = DateTime.Now.AddDays(-5),
                    ModifiedAt = DateTime.Now.AddDays(-1)
                },
                new Document
                {
                    Title = "Research Notes",
                    Content = "Research content here.",
                    Type = DocumentType.Research
                }
            }
        };
        var filePath = Path.Combine(_testDirectory, "complex.json");

        // Act
        _service.SaveProject(originalProject, filePath);
        var loadedProject = _service.LoadProject(filePath);

        // Assert
        loadedProject.Documents.Should().HaveCount(2);
        loadedProject.Documents[0].Title.Should().Be("Chapter 1");
        loadedProject.Documents[0].Type.Should().Be(DocumentType.Chapter);
        loadedProject.Documents[1].Type.Should().Be(DocumentType.Research);
    }

    [Fact]
    public void SaveProject_ShouldCreateDirectoryIfNotExists()
    {
        // Arrange
        var project = new Project { Name = "Test" };
        var subDirectory = Path.Combine(_testDirectory, "subdir");
        var filePath = Path.Combine(subDirectory, "test.json");

        // Act
        _service.SaveProject(project, filePath);

        // Assert
        Directory.Exists(subDirectory).Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public void LoadProject_ShouldHandleEmptyProject()
    {
        // Arrange
        var project = new Project { Name = "Empty Project" };
        var filePath = Path.Combine(_testDirectory, "empty.json");
        _service.SaveProject(project, filePath);

        // Act
        var loaded = _service.LoadProject(filePath);

        // Assert
        loaded.Name.Should().Be("Empty Project");
        loaded.Documents.Should().BeEmpty();
    }
}
