using Avalonia;
using Avalonia.Media;

namespace Scribo.Models;

public enum MarkdownBlockType
{
    Paragraph,
    Heading1,
    Heading2,
    Heading3,
    Heading4,
    ListItem,
    CodeBlock
}

public class MarkdownBlock
{
    public MarkdownBlockType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    
    public string DisplayText
    {
        get
        {
            return Type switch
            {
                MarkdownBlockType.Heading1 => Content,
                MarkdownBlockType.Heading2 => Content,
                MarkdownBlockType.Heading3 => Content,
                MarkdownBlockType.Heading4 => Content,
                MarkdownBlockType.ListItem => $"• {Content}",
                MarkdownBlockType.CodeBlock => Content,
                _ => Content
            };
        }
    }
    
    public double FontSize
    {
        get
        {
            return Type switch
            {
                MarkdownBlockType.Heading1 => 24,
                MarkdownBlockType.Heading2 => 20,
                MarkdownBlockType.Heading3 => 18,
                MarkdownBlockType.Heading4 => 16,
                MarkdownBlockType.CodeBlock => 12,
                _ => 14
            };
        }
    }
    
    public FontWeight FontWeight
    {
        get
        {
            return Type switch
            {
                MarkdownBlockType.Heading1 => FontWeight.Bold,
                MarkdownBlockType.Heading2 => FontWeight.Bold,
                MarkdownBlockType.Heading3 => FontWeight.Bold,
                MarkdownBlockType.Heading4 => FontWeight.Bold,
                MarkdownBlockType.CodeBlock => FontWeight.Normal,
                _ => FontWeight.Normal
            };
        }
    }
    
    public string FontFamily
    {
        get
        {
            return Type == MarkdownBlockType.CodeBlock ? "Consolas" : "Inter";
        }
    }
    
    public Avalonia.Thickness Padding
    {
        get
        {
            return Type == MarkdownBlockType.CodeBlock 
                ? new Avalonia.Thickness(10) 
                : new Avalonia.Thickness(0);
        }
    }
    
    public Avalonia.Media.IBrush Background
    {
        get
        {
            return Type == MarkdownBlockType.CodeBlock 
                ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(244, 244, 244))
                : Avalonia.Media.Brushes.Transparent;
        }
    }
}
