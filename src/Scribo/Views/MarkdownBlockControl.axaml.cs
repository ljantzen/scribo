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
    // Set to false to disable all debug tracing
    private const bool ENABLE_DEBUG_TRACING = true;
    
    private void DebugTrace(string message)
    {
        if (ENABLE_DEBUG_TRACING)
        {
            Console.WriteLine($"[MarkdownBlockControl] {message}");
        }
    }
    
    public MarkdownBlockControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DebugTrace("MarkdownBlockControl constructor called");
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        DebugTrace($"OnDataContextChanged (event handler) called, sender: {sender?.GetType().Name}");
        UpdateContent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        DebugTrace($"OnDataContextChanged (override) called, DataContext: {DataContext?.GetType().Name}");
        base.OnDataContextChanged(e);
        UpdateContent();
    }

    private void UpdateContent()
    {
        DebugTrace("UpdateContent called");
        
        if (DataContext is not MarkdownBlock block)
        {
            DebugTrace("  DataContext is not MarkdownBlock");
            return;
        }
        
        DebugTrace($"  Block Type: {block.Type}, Content length: {block.Content?.Length ?? 0}, DisplayText length: {block.DisplayText?.Length ?? 0}");
        DebugTrace($"  Links count: {block.Links?.Count ?? 0}");
        
        if (block.Links != null && block.Links.Count > 0)
        {
            for (int i = 0; i < block.Links.Count; i++)
            {
                var link = block.Links[i];
                DebugTrace($"    Link[{i}]: LinkText='{link.LinkText}', DisplayText='{link.DisplayText}', StartIndex={link.StartIndex}, Length={link.Length}");
                DebugTrace($"      IsResolved={link.IsResolved}, TargetDocumentId='{link.TargetDocumentId}'");
            }
        }
            
        var textBlock = contentTextBlock;
        if (textBlock == null)
        {
            DebugTrace("  contentTextBlock is null");
            return;
        }

        var inlines = textBlock.Inlines;
        if (inlines == null)
        {
            DebugTrace("  textBlock.Inlines is null");
            return;
        }
            
        DebugTrace("  Clearing inlines");
        inlines.Clear();

        if (block.Links == null || block.Links.Count == 0)
        {
            DebugTrace("  No links, adding plain text");
            // No links, just display the text
            inlines.Add(new Run(block.DisplayText));
            return;
        }

        DebugTrace("  Building inlines with clickable links");
        // Build inlines with clickable links
        // Parse the original content to find [[links]] and replace them with styled text
        var originalContent = block.Content;
        var text = block.DisplayText;
        var links = block.Links.OrderBy(l => l.StartIndex).ToList();
        
        DebugTrace($"  Processing {links.Count} links in text: '{text}'");
        
        // Since ProcessInlineMarkdownText replaces [[links]] with display text,
        // we need to find the display text in the final text and style it
        int currentIndex = 0;
        
        foreach (var link in links)
        {
            DebugTrace($"    Processing link: DisplayText='{link.DisplayText}', StartIndex={link.StartIndex}, currentIndex={currentIndex}");
            
            // Find where this link's display text appears in the processed text
            var linkDisplayStart = text.IndexOf(link.DisplayText, currentIndex, StringComparison.Ordinal);
            
            DebugTrace($"      linkDisplayStart={linkDisplayStart}");
            
            if (linkDisplayStart >= 0)
            {
                // Add text before the link
                if (linkDisplayStart > currentIndex)
                {
                    var beforeText = text.Substring(currentIndex, linkDisplayStart - currentIndex);
                    DebugTrace($"      Adding text before link: '{beforeText}'");
                    inlines.Add(new Run(beforeText));
                }

                // Add the clickable link as a Run with styling
                DebugTrace($"      Adding link Run: DisplayText='{link.DisplayText}', IsResolved={link.IsResolved}, TargetDocumentId='{link.TargetDocumentId}'");
                var linkRun = new Run(link.DisplayText)
                {
                    Foreground = new SolidColorBrush(link.IsResolved 
                        ? Color.FromRgb(0, 102, 204) // Blue for resolved links
                        : Color.FromRgb(128, 128, 128)), // Gray for unresolved links
                    TextDecorations = TextDecorations.Underline
                };
                
                inlines.Add(linkRun);
                currentIndex = linkDisplayStart + link.DisplayText.Length;
                DebugTrace($"      Updated currentIndex to {currentIndex}");
            }
            else
            {
                DebugTrace($"      Link display text not found in text starting from index {currentIndex}");
            }
        }

        // Add remaining text after the last link
        if (currentIndex < text.Length)
        {
            var afterText = text.Substring(currentIndex);
            DebugTrace($"  Adding remaining text after last link: '{afterText}'");
            inlines.Add(new Run(afterText));
        }
        
        DebugTrace($"  UpdateContent completed, total inlines: {inlines.Count}");
    }

    private void OnTextBlockPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        DebugTrace($"OnTextBlockPointerPressed called");
        DebugTrace($"  Sender: {sender?.GetType().Name}");
        DebugTrace($"  Pointer position: {e.GetPosition(this)}");
        DebugTrace($"  Pointer properties: IsLeftButtonPressed={e.GetCurrentPoint(this).Properties.IsLeftButtonPressed}");
        
        if (DataContext is not MarkdownBlock block)
        {
            DebugTrace("  DataContext is not MarkdownBlock");
            return;
        }
        
        DebugTrace($"  Block Type: {block.Type}");
        DebugTrace($"  Block Links: {block.Links?.Count ?? 0}");
        
        if (block.Links == null || block.Links.Count == 0)
        {
            DebugTrace("  No links in block");
            return;
        }

        DebugTrace("  Listing all links:");
        for (int i = 0; i < block.Links.Count; i++)
        {
            var link = block.Links[i];
            DebugTrace($"    Link[{i}]: LinkText='{link.LinkText}', DisplayText='{link.DisplayText}'");
            DebugTrace($"      StartIndex={link.StartIndex}, Length={link.Length}");
            DebugTrace($"      IsResolved={link.IsResolved}, TargetDocumentId='{link.TargetDocumentId}'");
        }

        // Try to find which link was clicked by calculating character position
        var textBlock = contentTextBlock;
        if (textBlock != null)
        {
            var clickPosition = e.GetPosition(textBlock);
            DebugTrace($"  Click position relative to textBlock: {clickPosition}");
            
            // Try to get the character index at the click position
            // Note: This is approximate and may not work perfectly with all fonts/layouts
            try
            {
                var hitTestResult = textBlock.InputHitTest(clickPosition);
                DebugTrace($"  Hit test result: {hitTestResult?.GetType().Name}");
            }
            catch (Exception ex)
            {
                DebugTrace($"  Hit test exception: {ex.Message}");
            }
        }

        // Find the first resolved link and navigate to it
        // A more sophisticated implementation would calculate exact character positions
        DebugTrace("  Searching for first resolved link");
        var resolvedLinks = block.Links
            .Where(l => l.IsResolved && !string.IsNullOrEmpty(l.TargetDocumentId))
            .ToList();
        
        DebugTrace($"  Found {resolvedLinks.Count} resolved links");
        
        var resolvedLink = resolvedLinks.FirstOrDefault();
        
        if (resolvedLink != null && !string.IsNullOrEmpty(resolvedLink.TargetDocumentId))
        {
            DebugTrace($"  Navigating to document: TargetDocumentId='{resolvedLink.TargetDocumentId}'");
            DebugTrace($"  NavigateToDocumentRequested handler count: {(NavigateToDocumentRequested?.GetInvocationList().Length ?? 0)}");
            NavigateToDocumentRequested?.Invoke(resolvedLink.TargetDocumentId);
            DebugTrace("  NavigateToDocumentRequested invoked");
        }
        else
        {
            DebugTrace("  No resolved link found or TargetDocumentId is empty");
            if (resolvedLink == null)
                DebugTrace("    resolvedLink is null");
            else
                DebugTrace($"    resolvedLink.TargetDocumentId is empty: '{resolvedLink.TargetDocumentId}'");
        }
    }

    public event Action<string>? NavigateToDocumentRequested;
}
