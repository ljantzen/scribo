using System;
using System.Linq;

namespace Scribo.Services;

public class TextStatisticsService
{
    public TextStatistics CalculateStatistics(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new TextStatistics();
        }

        var words = text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var paragraphs = text.Split(new[] { "\r\n\r\n", "\n\n", "\r\r" }, StringSplitOptions.RemoveEmptyEntries);
        var sentences = text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();

        return new TextStatistics
        {
            WordCount = words.Length,
            CharacterCount = text.Length,
            CharacterCountNoSpaces = text.Replace(" ", "").Replace("\n", "").Replace("\r", "").Replace("\t", "").Length,
            ParagraphCount = paragraphs.Length,
            SentenceCount = sentences.Length,
            LineCount = text.Split('\n').Length
        };
    }
}

public class TextStatistics
{
    public int WordCount { get; set; }
    public int CharacterCount { get; set; }
    public int CharacterCountNoSpaces { get; set; }
    public int ParagraphCount { get; set; }
    public int SentenceCount { get; set; }
    public int LineCount { get; set; }
}
