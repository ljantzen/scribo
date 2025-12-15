using FluentAssertions;
using Scribo.Models;
using Xunit;

namespace Scribo.Tests.Models;

public class ProjectTests
{
    [Fact]
    public void Project_ShouldInitializeWithEmptyDocuments()
    {
        // Arrange & Act
        var project = new Project();

        // Assert
        project.Documents.Should().NotBeNull();
        project.Documents.Should().BeEmpty();
    }

    [Fact]
    public void Project_ShouldSetCreatedAt()
    {
        // Arrange
        var beforeCreation = DateTime.Now;

        // Act
        var project = new Project();
        var afterCreation = DateTime.Now;

        // Assert
        project.CreatedAt.Should().BeAfter(beforeCreation.AddSeconds(-1));
        project.CreatedAt.Should().BeBefore(afterCreation.AddSeconds(1));
    }

    [Fact]
    public void Project_ShouldAllowAddingDocuments()
    {
        // Arrange
        var project = new Project();
        var document = new Document { Title = "Test Document" };

        // Act
        project.Documents.Add(document);

        // Assert
        project.Documents.Should().Contain(document);
        project.Documents.Should().HaveCount(1);
    }

    [Fact]
    public void Project_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var project = new Project();

        // Assert
        project.Name.Should().BeEmpty();
        project.FilePath.Should().BeEmpty();
    }
}
