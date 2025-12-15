using FluentAssertions;
using Scribo.Models;
using Scribo.Services;
using System.IO;
using System.Text.Json;
using Xunit;

namespace Scribo.Tests.Services;

public class ProjectServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ProjectService _service;

    public ProjectServiceTests()
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
    public void CreateNewProject_ShouldCreateProjectWithName()
    {
        // Arrange
        var projectName = "Test Project";

        // Act
        var project = _service.CreateNewProject(projectName);

        // Assert
        project.Should().NotBeNull();
        project.Name.Should().Be(projectName);
        project.CreatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CreateNewProject_ShouldThrowWhenNameIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.CreateNewProject(null!));
    }

    [Fact]
    public void CreateNewProject_ShouldThrowWhenNameIsEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _service.CreateNewProject(string.Empty));
    }

    [Fact]
    public void SaveProject_ShouldSaveProjectToFile()
    {
        // Arrange
        var project = new Project { Name = "Test Project" };
        var filePath = Path.Combine(_testDirectory, "test.json");

        // Act
        _service.SaveProject(project, filePath);

        // Assert
        File.Exists(filePath).Should().BeTrue();
        project.FilePath.Should().Be(filePath);
    }

    [Fact]
    public void SaveProject_ShouldUpdateModifiedAt()
    {
        // Arrange
        var project = new Project { Name = "Test Project" };
        var filePath = Path.Combine(_testDirectory, "test.json");
        var beforeSave = DateTime.Now;

        // Act
        _service.SaveProject(project, filePath);
        var afterSave = DateTime.Now;

        // Assert
        project.ModifiedAt.Should().BeAfter(beforeSave.AddSeconds(-1));
        project.ModifiedAt.Should().BeBefore(afterSave.AddSeconds(1));
    }

    [Fact]
    public void SaveProject_ShouldThrowWhenProjectIsNull()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.json");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _service.SaveProject(null!, filePath));
    }

    [Fact]
    public void LoadProject_ShouldLoadProjectFromFile()
    {
        // Arrange
        var originalProject = new Project 
        { 
            Name = "Test Project",
            Documents = new List<Document> { new Document { Title = "Doc1" } }
        };
        var filePath = Path.Combine(_testDirectory, "test.json");
        _service.SaveProject(originalProject, filePath);

        // Act
        var loadedProject = _service.LoadProject(filePath);

        // Assert
        loadedProject.Should().NotBeNull();
        loadedProject.Name.Should().Be(originalProject.Name);
        loadedProject.Documents.Should().HaveCount(1);
    }

    [Fact]
    public void LoadProject_ShouldThrowWhenFileDoesNotExist()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent.json");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _service.LoadProject(filePath));
    }

    [Fact]
    public void SaveAndLoadProject_ShouldPreserveAllProperties()
    {
        // Arrange
        var originalProject = new Project
        {
            Name = "Test Project",
            FilePath = "original.json",
            Documents = new List<Document>
            {
                new Document { Title = "Document 1", Content = "Content 1" },
                new Document { Title = "Document 2", Content = "Content 2" }
            }
        };
        var filePath = Path.Combine(_testDirectory, "test.json");

        // Act
        _service.SaveProject(originalProject, filePath);
        var loadedProject = _service.LoadProject(filePath);

        // Assert
        loadedProject.Name.Should().Be(originalProject.Name);
        loadedProject.Documents.Should().HaveCount(2);
        loadedProject.Documents[0].Title.Should().Be("Document 1");
        loadedProject.Documents[1].Title.Should().Be("Document 2");
    }
}
