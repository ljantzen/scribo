using FluentAssertions;
using Scribo.Models;
using System.Linq;
using Xunit;

namespace Scribo.Tests.Models;

public class DocumentAdditionalTests
{
    [Fact]
    public void Document_ShouldHandleNullContent()
    {
        // Arrange
        var document = new Document
        {
            Content = null!
        };

        // Act & Assert
        document.CharacterCount.Should().Be(0);
    }

    [Fact]
    public void Document_ShouldCalculateWordCountWithTabs()
    {
        // Arrange
        var document = new Document
        {
            Content = "Word1\tWord2\tWord3"
        };

        // Act
        var wordCount = document.WordCount;

        // Assert
        wordCount.Should().Be(3);
    }

    [Fact]
    public void Document_ShouldHandleVeryLongContent()
    {
        // Arrange
        var longContent = string.Join(" ", Enumerable.Range(1, 1000).Select(i => $"word{i}"));
        var document = new Document
        {
            Content = longContent
        };

        // Act & Assert
        document.WordCount.Should().Be(1000);
        document.CharacterCount.Should().BeGreaterThan(5000);
    }

    [Fact]
    public void Document_ShouldHandleUnicodeContent()
    {
        // Arrange
        var document = new Document
        {
            Content = "Hello 世界 مرحبا"
        };

        // Act & Assert
        document.WordCount.Should().Be(3);
        document.CharacterCount.Should().BeGreaterThan(10);
    }

    [Fact]
    public void Document_ShouldSetAllDocumentTypes()
    {
        // Arrange & Act
        var types = Enum.GetValues<DocumentType>();

        // Assert
        types.Should().Contain(DocumentType.Chapter);
        types.Should().Contain(DocumentType.Scene);
        types.Should().Contain(DocumentType.Note);
        types.Should().Contain(DocumentType.Research);
        types.Should().Contain(DocumentType.Character);
        types.Should().Contain(DocumentType.Location);
        types.Should().Contain(DocumentType.Other);
    }

    [Fact]
    public void Document_ShouldUpdateModifiedAt()
    {
        // Arrange
        var document = new Document
        {
            Title = "Test",
            ModifiedAt = DateTime.Now.AddDays(-1)
        };
        var originalModifiedAt = document.ModifiedAt;

        // Act
        document.ModifiedAt = DateTime.Now;

        // Assert
        document.ModifiedAt.Should().BeAfter(originalModifiedAt);
    }

    [Fact]
    public void Document_CharacterCountNoSpaces_ShouldExcludeAllWhitespace()
    {
        // Arrange
        var document = new Document
        {
            Content = "Hello World\nTest\tMore"
        };

        // Act
        var count = document.CharacterCountNoSpaces;

        // Assert
        count.Should().Be(14); // "HelloWorldTestMore"
    }
}
