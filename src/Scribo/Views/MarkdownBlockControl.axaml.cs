using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Scribo.Models;

namespace Scribo.Views;

public partial class MarkdownBlockControl : UserControl
{
    public MarkdownBlockControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        UpdateContent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        UpdateContent();
    }

    private void UpdateContent()
    {
        if (DataContext is not MarkdownBlock block)
            return;
            
        var textBlock = contentTextBlock;
        if (textBlock == null)
            return;

        var inlines = contentTextBlock.Inlines;
        inlines.Clear();

        if (block.Links == null || block.Links.Count == 0)
        {
            // No links, just display the text
            inlines.Add(new Run(block.DisplayText));
            return;
        }

        // Build inlines with clickable links
        // Parse the original content to find [[links]] and replace them with styled text
        var originalContent = block.Content;
        var text = block.DisplayText;
        var links = block.Links.OrderBy(l => l.StartIndex).ToList();
        
        // Since ProcessInlineMarkdownText replaces [[links]] with display text,
        // we need to find the display text in the final text and style it
        int currentIndex = 0;
        
        foreach (var link in links)
        {
            // Find where this link's display text appears in the processed text
            var linkDisplayStart = text.IndexOf(link.DisplayText, currentIndex, StringComparison.Ordinal);
            
            if (linkDisplayStart >= 0)
            {
                // Add text before the link
                if (linkDisplayStart > currentIndex)
                {
                    var beforeText = text.Substring(currentIndex, linkDisplayStart - currentIndex);
                    inlines.Add(new Run(beforeText));
                }

                // Add the clickable link as a Run with styling
                var linkRun = new Run(link.DisplayText)
                {
                    Foreground = new SolidColorBrush(link.IsResolved 
                        ? Color.FromRgb(0, 102, 204) // Blue for resolved links
                        : Color.FromRgb(128, 128, 128)), // Gray for unresolved links
                    TextDecorations = TextDecorations.Underline
                };
                
                inlines.Add(linkRun);
                currentIndex = linkDisplayStart + link.DisplayText.Length;
            }
        }

        // Add remaining text after the last link
        if (currentIndex < text.Length)
        {
            var afterText = text.Substring(currentIndex);
            inlines.Add(new Run(afterText));
        }
    }

    private void OnTextBlockPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not MarkdownBlock block || block.Links == null || block.Links.Count == 0)
            return;

        // Find the first resolved link and navigate to it
        // A more sophisticated implementation would calculate exact character positions
        var resolvedLink = block.Links
            .FirstOrDefault(l => l.IsResolved && !string.IsNullOrEmpty(l.TargetDocumentId));
        
        if (resolvedLink != null && !string.IsNullOrEmpty(resolvedLink.TargetDocumentId))
        {
            NavigateToDocumentRequested?.Invoke(resolvedLink.TargetDocumentId);
        }
    }

    public event Action<string>? NavigateToDocumentRequested;
}
