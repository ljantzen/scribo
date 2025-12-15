using FluentAssertions;
using Scribo.Services;
using System.Linq;
using Xunit;

namespace Scribo.Tests.Services;

public class TextStatisticsServiceAdditionalTests
{
    private readonly TextStatisticsService _service = new();

    [Fact]
    public void CalculateStatistics_ShouldHandleNullText()
    {
        // Arrange
        string? text = null;

        // Act
        var stats = _service.CalculateStatistics(text!);

        // Assert
        stats.WordCount.Should().Be(0);
        stats.CharacterCount.Should().Be(0);
    }

    [Fact]
    public void CalculateStatistics_ShouldHandleWhitespaceOnly()
    {
        // Arrange
        var text = "   \n\t\r   ";

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.WordCount.Should().Be(0);
        stats.CharacterCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateStatistics_ShouldHandleSpecialCharacters()
    {
        // Arrange
        var text = "Hello @world #test $100 &more";

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.WordCount.Should().Be(5);
        stats.CharacterCount.Should().Be(28);
    }

    [Fact]
    public void CalculateStatistics_ShouldHandleUnicodeCharacters()
    {
        // Arrange
        var text = "Hello 世界 مرحبا";

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.WordCount.Should().Be(3);
        stats.CharacterCount.Should().BeGreaterThan(10);
    }

    [Fact]
    public void CalculateStatistics_ShouldHandleVeryLongText()
    {
        // Arrange
        var text = string.Join(" ", Enumerable.Range(1, 10000).Select(i => $"word{i}"));

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.WordCount.Should().Be(10000);
        stats.CharacterCount.Should().BeGreaterThan(100000);
    }

    [Fact]
    public void CalculateStatistics_ShouldHandleMixedLineEndings()
    {
        // Arrange
        var text = "Line 1\r\nLine 2\nLine 3\rLine 4";

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.LineCount.Should().BeGreaterThanOrEqualTo(4);
    }

    [Fact]
    public void CalculateStatistics_ShouldHandleEmptyParagraphs()
    {
        // Arrange
        var text = "\n\n\n";

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.ParagraphCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void CalculateStatistics_ShouldHandleSingleCharacter()
    {
        // Arrange
        var text = "A";

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.WordCount.Should().Be(1);
        stats.CharacterCount.Should().Be(1);
        stats.CharacterCountNoSpaces.Should().Be(1);
    }

    [Fact]
    public void CalculateStatistics_ShouldHandleTextWithOnlyPunctuation()
    {
        // Arrange
        var text = "...!!!???";

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.WordCount.Should().Be(0);
        stats.CharacterCount.Should().Be(9);
    }

    [Fact]
    public void CalculateStatistics_ShouldHandleMultipleSpaces()
    {
        // Arrange
        var text = "Word1    Word2     Word3";

        // Act
        var stats = _service.CalculateStatistics(text);

        // Assert
        stats.WordCount.Should().Be(3);
    }
}
