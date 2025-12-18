using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
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
        
        // Try to attach handler when control is loaded
        Loaded += OnMarkdownBlockControlLoaded;
    }
    
    private void OnMarkdownBlockControlLoaded(object? sender, RoutedEventArgs e)
    {
        TryAttachNavigationHandler();
    }
    
    private void TryAttachNavigationHandler()
    {
        
        // Find the MainWindow in the visual tree
        var window = this.GetVisualRoot() as Window;
        
        if (window is MainWindow mainWindow)
        {
            NavigateToDocumentRequested += mainWindow.OnNavigateToDocument;
        }
        else
        {
            
            // Try alternative: find via visual tree traversal
            var parent = this.GetVisualParent();
            int depth = 0;
            while (parent != null && depth < 10)
            {
                if (parent is MainWindow mw)
                {
                    NavigateToDocumentRequested += mw.OnNavigateToDocument;
                    break;
                }
                parent = parent.GetVisualParent();
                depth++;
            }
        }
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
        {
            return;
        }
        
        
        if (block.Links != null && block.Links.Count > 0)
        {
            for (int i = 0; i < block.Links.Count; i++)
            {
                var link = block.Links[i];
            }
        }
            
        var textBlock = contentTextBlock;
        if (textBlock == null)
        {
            return;
        }

        var inlines = textBlock.Inlines;
        if (inlines == null)
        {
            return;
        }
            
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
        
        if (DataContext is not MarkdownBlock block)
        {
            return;
        }
        
        if (block.Links == null || block.Links.Count == 0)
        {
            return;
        }

        for (int i = 0; i < block.Links.Count; i++)
        {
            var link = block.Links[i];
        }

        var textBlock = contentTextBlock;
        if (textBlock != null)
        {
            var clickPosition = e.GetPosition(textBlock);
            
            // Try to get the character index at the click position
            // Note: This is approximate and may not work perfectly with all fonts/layouts
            try
            {
                var hitTestResult = textBlock.InputHitTest(clickPosition);
                
                // Try to find which inline was clicked
                if (textBlock.Inlines != null)
                {
                    for (int i = 0; i < textBlock.Inlines.Count; i++)
                    {
                        var inline = textBlock.Inlines[i];
                        if (inline is Run run)
                        {
                        }
                    }
                }
            }
            catch
            {
            }
        }
        else
        {
        }

        // Find the first resolved link and navigate to it
        // A more sophisticated implementation would calculate exact character positions
        var resolvedLinks = block.Links
            .Where(l => l.IsResolved && !string.IsNullOrEmpty(l.TargetDocumentId))
            .ToList();
        
        
        var resolvedLink = resolvedLinks.FirstOrDefault();
        
        if (resolvedLink != null && !string.IsNullOrEmpty(resolvedLink.TargetDocumentId))
        {
            
            // Check if handler is attached (can't directly check invocation list from outside)
            
            try
            {
                NavigateToDocumentRequested?.Invoke(resolvedLink.TargetDocumentId);
            }
            catch
            {
            }
        }
    }

    public event Action<string>? NavigateToDocumentRequested;
}
