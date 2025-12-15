using FluentAssertions;
using Scribo.Models;
using Xunit;

namespace Scribo.Tests.Models;

public class DocumentTests
{
    [Fact]
    public void Document_ShouldHaveUniqueId()
    {
        // Arrange & Act
        var doc1 = new Document();
        var doc2 = new Document();

        // Assert
        doc1.Id.Should().NotBeEmpty();
        doc2.Id.Should().NotBeEmpty();
        doc1.Id.Should().NotBe(doc2.Id);
    }

    [Fact]
    public void WordCount_ShouldCountWordsCorrectly()
    {
        // Arrange
        var document = new Document
        {
            Content = "This is a test document with multiple words."
        };

        // Act
        var wordCount = document.WordCount;

        // Assert
        wordCount.Should().Be(8);
    }

    [Fact]
    public void WordCount_ShouldHandleEmptyContent()
    {
        // Arrange
        var document = new Document
        {
            Content = string.Empty
        };

        // Act
        var wordCount = document.WordCount;

        // Assert
        wordCount.Should().Be(0);
    }

    [Fact]
    public void WordCount_ShouldHandleMultilineContent()
    {
        // Arrange
        var document = new Document
        {
            Content = "Line one\nLine two\nLine three"
        };

        // Act
        var wordCount = document.WordCount;

        // Assert
        wordCount.Should().Be(6);
    }

    [Fact]
    public void CharacterCount_ShouldCountAllCharacters()
    {
        // Arrange
        var document = new Document
        {
            Content = "Hello World"
        };

        // Act
        var characterCount = document.CharacterCount;

        // Assert
        characterCount.Should().Be(11);
    }

    [Fact]
    public void CharacterCountNoSpaces_ShouldExcludeSpacesAndNewlines()
    {
        // Arrange
        var document = new Document
        {
            Content = "Hello World\nTest"
        };

        // Act
        var characterCount = document.CharacterCountNoSpaces;

        // Assert
        characterCount.Should().Be(13); // "HelloWorldTest"
    }

    [Fact]
    public void Document_ShouldHaveDefaultType()
    {
        // Arrange & Act
        var document = new Document();

        // Assert
        document.Type.Should().Be(DocumentType.Chapter);
    }

    [Fact]
    public void Document_ShouldSetCreatedAt()
    {
        // Arrange
        var beforeCreation = DateTime.Now;

        // Act
        var document = new Document();
        var afterCreation = DateTime.Now;

        // Assert
        document.CreatedAt.Should().BeAfter(beforeCreation.AddSeconds(-1));
        document.CreatedAt.Should().BeBefore(afterCreation.AddSeconds(1));
    }
}
