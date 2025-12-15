using FluentAssertions;
using Scribo.Models;
using Scribo.Services;
using System.IO;
using Xunit;

namespace Scribo.Tests.Services;

public class ProjectServiceJsonFileTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly ProjectService _service;

    public ProjectServiceJsonFileTests()
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
    public void SaveProject_ShouldAddJsonExtensionIfMissing()
    {
        // Arrange
        var project = new Project { Name = "Test Project" };
        var filePath = Path.Combine(_testDirectory, "test");

        // Act
        _service.SaveProject(project, filePath);

        // Assert
        var expectedPath = Path.Combine(_testDirectory, "test.json");
        File.Exists(expectedPath).Should().BeTrue();
        project.FilePath.Should().Be(expectedPath);
    }

    [Fact]
    public void SaveProject_ShouldPreserveJsonExtension()
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
    public void SaveProject_ShouldUpdateMetadata()
    {
        // Arrange
        var project = new Project { Name = "Test Project" };
        var filePath = Path.Combine(_testDirectory, "test.scr");

        // Act
        _service.SaveProject(project, filePath);

        // Assert
        project.Metadata.Should().NotBeNull();
        project.Metadata.Title.Should().Be("Test Project");
        project.Metadata.ModifiedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SaveProject_ShouldCalculateStatistics()
    {
        // Arrange
        var project = new Project
        {
            Name = "Test Project",
            Documents = new List<Document>
            {
                new Document { Title = "Doc 1", Content = "This is a test document with multiple words." },
                new Document { Title = "Doc 2", Content = "Another document with content." }
            }
        };
        var filePath = Path.Combine(_testDirectory, "test.scr");

        // Act
        _service.SaveProject(project, filePath);

        // Assert
        project.Metadata.Statistics.Should().NotBeNull();
        project.Metadata.Statistics.TotalWordCount.Should().BeGreaterThan(0);
        project.Metadata.Statistics.LastCalculatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LoadProject_ShouldLoadMetadata()
    {
        // Arrange
        var project = new Project
        {
            Name = "Test Project",
            Metadata = new ProjectMetadata
            {
                Title = "Test Project",
                Author = "Test Author",
                Keywords = new List<string> { "test", "project" }
            }
        };
        var filePath = Path.Combine(_testDirectory, "test.scr");
        _service.SaveProject(project, filePath);

        // Act
        var loaded = _service.LoadProject(filePath);

        // Assert
        loaded.Metadata.Should().NotBeNull();
        loaded.Metadata.Title.Should().Be("Test Project");
        loaded.Metadata.Author.Should().Be("Test Author");
        loaded.Metadata.Keywords.Should().HaveCount(2);
    }

    [Fact]
    public void LoadProject_ShouldCreateMetadataIfMissing()
    {
        // Arrange
        // Create a project file without metadata (backward compatibility)
        var project = new Project { Name = "Old Project" };
        var filePath = Path.Combine(_testDirectory, "old.scr");
        
        // Save without metadata
        var json = System.Text.Json.JsonSerializer.Serialize(project, new System.Text.Json.JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(filePath, json);

        // Act
        var loaded = _service.LoadProject(filePath);

        // Assert
        loaded.Metadata.Should().NotBeNull();
        loaded.Metadata.Title.Should().Be("Old Project");
    }

    [Fact]
    public void LoadProject_ShouldUpdateLastOpenedAt()
    {
        // Arrange
        var project = new Project { Name = "Test Project" };
        var filePath = Path.Combine(_testDirectory, "test.scr");
        _service.SaveProject(project, filePath);

        // Act
        var beforeLoad = DateTime.Now;
        var loaded = _service.LoadProject(filePath);
        var afterLoad = DateTime.Now;

        // Assert
        loaded.Metadata.LastOpenedAt.Should().NotBeNull();
        loaded.Metadata.LastOpenedAt!.Value.Should().BeAfter(beforeLoad.AddSeconds(-1));
        loaded.Metadata.LastOpenedAt.Value.Should().BeBefore(afterLoad.AddSeconds(1));
    }

    [Fact]
    public void CreateNewProject_ShouldInitializeMetadata()
    {
        // Arrange & Act
        var project = _service.CreateNewProject("New Project");

        // Assert
        project.Metadata.Should().NotBeNull();
        project.Metadata.Title.Should().Be("New Project");
        project.Metadata.CreatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void SaveProject_ShouldPreserveAllMetadataFields()
    {
        // Arrange
        var project = new Project
        {
            Name = "Test Project",
            Metadata = new ProjectMetadata
            {
                Title = "Test Project",
                Author = "John Doe",
                Publisher = "Test Publisher",
                ISBN = "123-456-789",
                Copyright = "2024",
                Comments = "Test comments",
                ProjectNotes = "Project notes here",
                Keywords = new List<string> { "keyword1", "keyword2" },
                Tags = new List<string> { "tag1", "tag2" },
                CustomFields = new Dictionary<string, string>
                {
                    { "Genre", "Fiction" },
                    { "Status", "Draft" }
                }
            }
        };
        var filePath = Path.Combine(_testDirectory, "test.scr");

        // Act
        _service.SaveProject(project, filePath);
        var loaded = _service.LoadProject(filePath);

        // Assert
        loaded.Metadata.Author.Should().Be("John Doe");
        loaded.Metadata.Publisher.Should().Be("Test Publisher");
        loaded.Metadata.ISBN.Should().Be("123-456-789");
        loaded.Metadata.Keywords.Should().HaveCount(2);
        loaded.Metadata.Tags.Should().HaveCount(2);
        loaded.Metadata.CustomFields.Should().HaveCount(2);
        loaded.Metadata.CustomFields["Genre"].Should().Be("Fiction");
    }
}
