using FluentAssertions;
using Scribo.Services;
using Xunit;

namespace Scribo.Tests.Services;

public class TextStatisticsServiceTests
{
    private readonly TextStatisticsService _service = new();

    [Fact]
    public void CalculateStatistics_ShouldReturnZeroForEmptyText()
    {
        // Arrange
        var text = string.Empty;

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.WordCount.Should().Be(0);
        stats.CharacterCount.Should().Be(0);
        stats.CharacterCountNoSpaces.Should().Be(0);
    }

    [Fact]
    public void CalculateStatistics_ShouldCountWordsCorrectly()
    {
        // Arrange
        var text = "This is a test document";

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.WordCount.Should().Be(5);
    }

    [Fact]
    public void CalculateStatistics_ShouldCountCharactersCorrectly()
    {
        // Arrange
        var text = "Hello World";

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.CharacterCount.Should().Be(11);
        stats.CharacterCountNoSpaces.Should().Be(10);
    }

    [Fact]
    public void CalculateStatistics_ShouldCountParagraphs()
    {
        // Arrange
        var text = "First paragraph.\n\nSecond paragraph.";

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.ParagraphCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public void CalculateStatistics_ShouldCountSentences()
    {
        // Arrange
        var text = "First sentence. Second sentence! Third sentence?";

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.SentenceCount.Should().Be(3);
    }

    [Fact]
    public void CalculateStatistics_ShouldCountLines()
    {
        // Arrange
        var text = "Line 1\nLine 2\nLine 3";

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.LineCount.Should().Be(3);
    }

    [Fact]
    public void CalculateStatistics_ShouldHandleMultilineText()
    {
        // Arrange
        var text = "This is line one.\nThis is line two.\nThis is line three.";

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.LineCount.Should().Be(3);
        stats.WordCount.Should().Be(15);
    }
}
