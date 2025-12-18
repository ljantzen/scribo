using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Scribo.Models;
using Scribo.Services;

namespace Scribo.ViewModels.Managers;

public class MarkdownRenderer
{
    private readonly DocumentLinkService _documentLinkService;

    public MarkdownRenderer(DocumentLinkService documentLinkService)
    {
        _documentLinkService = documentLinkService;
    }

    public List<MarkdownBlock> RenderMarkdown(string markdown, Project? currentProject)
    {
        var blocks = new List<MarkdownBlock>();
        
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return blocks;
        }

        var lines = markdown.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        var codeBlockLines = new List<string>();
        bool inCodeBlock = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimEnd();

            // Code blocks
            if (trimmedLine.StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    if (codeBlockLines.Count > 0)
                    {
                        blocks.Add(new MarkdownBlock 
                        { 
                            Type = MarkdownBlockType.CodeBlock, 
                            Content = string.Join("\n", codeBlockLines) 
                        });
                    }
                    codeBlockLines.Clear();
                    inCodeBlock = false;
                }
                else
                {
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                codeBlockLines.Add(line);
                continue;
            }

            // Headers
            if (trimmedLine.StartsWith("# "))
            {
                var headingBlock = new MarkdownBlock 
                { 
                    Type = MarkdownBlockType.Heading1, 
                    Content = ProcessInlineMarkdownText(trimmedLine.Substring(2), currentProject, out var h1Links)
                };
                headingBlock.Links = h1Links;
                blocks.Add(headingBlock);
                continue;
            }
            if (trimmedLine.StartsWith("## "))
            {
                var headingBlock = new MarkdownBlock 
                { 
                    Type = MarkdownBlockType.Heading2, 
                    Content = ProcessInlineMarkdownText(trimmedLine.Substring(3), currentProject, out var h2Links)
                };
                headingBlock.Links = h2Links;
                blocks.Add(headingBlock);
                continue;
            }
            if (trimmedLine.StartsWith("### "))
            {
                var headingBlock = new MarkdownBlock 
                { 
                    Type = MarkdownBlockType.Heading3, 
                    Content = ProcessInlineMarkdownText(trimmedLine.Substring(4), currentProject, out var h3Links)
                };
                headingBlock.Links = h3Links;
                blocks.Add(headingBlock);
                continue;
            }
            if (trimmedLine.StartsWith("#### "))
            {
                var headingBlock = new MarkdownBlock 
                { 
                    Type = MarkdownBlockType.Heading4, 
                    Content = ProcessInlineMarkdownText(trimmedLine.Substring(5), currentProject, out var h4Links)
                };
                headingBlock.Links = h4Links;
                blocks.Add(headingBlock);
                continue;
            }

            // Lists
            if (trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("* "))
            {
                var listBlock = new MarkdownBlock 
                { 
                    Type = MarkdownBlockType.ListItem, 
                    Content = ProcessInlineMarkdownText(trimmedLine.Substring(2), currentProject, out var listLinks)
                };
                listBlock.Links = listLinks;
                blocks.Add(listBlock);
                continue;
            }

            // Empty line
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                blocks.Add(new MarkdownBlock { Type = MarkdownBlockType.Paragraph, Content = "" });
                continue;
            }

            // Regular paragraph
            var paragraphBlock = new MarkdownBlock 
            { 
                Type = MarkdownBlockType.Paragraph, 
                Content = ProcessInlineMarkdownText(trimmedLine, currentProject, out var links)
            };
            paragraphBlock.Links = links;
            blocks.Add(paragraphBlock);
        }

        if (inCodeBlock && codeBlockLines.Count > 0)
        {
            blocks.Add(new MarkdownBlock 
            { 
                Type = MarkdownBlockType.CodeBlock, 
                Content = string.Join("\n", codeBlockLines) 
            });
        }

        return blocks;
    }

    private string ProcessInlineMarkdownText(string text, Project? currentProject, out List<DocumentLink> links)
    {
        links = new List<DocumentLink>();
        
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Parse double bracket links first (before processing other markdown)
        var documentLinks = _documentLinkService.ParseLinks(text);
        
        // Resolve links if we have a current project
        if (currentProject != null)
        {
            _documentLinkService.ResolveLinks(documentLinks, currentProject);
        }
        
        links = documentLinks;

        // Remove markdown syntax for now - in a full implementation we'd parse and format
        // Bold: **text** -> text (will be formatted by TextBlock styling)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*(.+?)\*\*", "$1");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"__(.+?)__", "$1");
        
        // Italic: *text* -> text
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<!\*)\*(?!\*)([^\*]+?)(?<!\*)\*(?!\*)", "$1");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<!_)_([^_]+?)_(?!_)", "$1");
        
        // Code: `code` -> code
        text = System.Text.RegularExpressions.Regex.Replace(text, @"`([^`]+?)`", "$1");
        
        // Links: [text](url) -> text (url)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]+?)\]\(([^\)]+?)\)", "$1 ($2)");
        
        // Replace double bracket links with display text (in reverse order to maintain indices)
        foreach (var link in documentLinks.OrderByDescending(l => l.StartIndex))
        {
            var originalStart = link.StartIndex;
            var originalLength = link.Length;
            
            if (originalStart + originalLength <= text.Length)
            {
                text = text.Substring(0, originalStart) + 
                      link.DisplayText + 
                      text.Substring(originalStart + originalLength);
                
                // Update link position to point to the replaced display text
                link.StartIndex = originalStart;
                link.Length = link.DisplayText.Length;
            }
        }
        
        return text;
    }

    public string RenderMarkdownToHtml(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head>");
        html.AppendLine("<meta charset=\"utf-8\">");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: 'Inter', 'Segoe UI', sans-serif; font-size: 14px; line-height: 1.6; padding: 20px; margin: 0; }");
        html.AppendLine("h1 { font-size: 2em; margin-top: 0.67em; margin-bottom: 0.67em; font-weight: bold; }");
        html.AppendLine("h2 { font-size: 1.5em; margin-top: 0.83em; margin-bottom: 0.83em; font-weight: bold; }");
        html.AppendLine("h3 { font-size: 1.17em; margin-top: 1em; margin-bottom: 1em; font-weight: bold; }");
        html.AppendLine("h4 { font-size: 1em; margin-top: 1.33em; margin-bottom: 1.33em; font-weight: bold; }");
        html.AppendLine("p { margin-top: 1em; margin-bottom: 1em; }");
        html.AppendLine("ul, ol { margin-top: 1em; margin-bottom: 1em; padding-left: 2em; }");
        html.AppendLine("li { margin-top: 0.5em; margin-bottom: 0.5em; }");
        html.AppendLine("code { background-color: #f4f4f4; padding: 2px 4px; border-radius: 3px; font-family: 'Consolas', monospace; }");
        html.AppendLine("pre { background-color: #f4f4f4; padding: 10px; border-radius: 5px; overflow-x: auto; }");
        html.AppendLine("pre code { background-color: transparent; padding: 0; }");
        html.AppendLine("a { color: #0066cc; text-decoration: none; }");
        html.AppendLine("a:hover { text-decoration: underline; }");
        html.AppendLine("strong { font-weight: bold; }");
        html.AppendLine("em { font-style: italic; }");
        html.AppendLine("</style>");
        html.AppendLine("</head><body>");

        var lines = markdown.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        bool inCodeBlock = false;
        bool inList = false;
        bool inParagraph = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.TrimEnd();

            // Code blocks
            if (trimmedLine.StartsWith("```"))
            {
                if (inCodeBlock)
                {
                    html.AppendLine("</code></pre>");
                    inCodeBlock = false;
                }
                else
                {
                    var language = trimmedLine.Length > 3 ? trimmedLine.Substring(3).Trim() : "";
                    html.AppendLine($"<pre><code class=\"language-{HtmlEncode(language)}\">");
                    inCodeBlock = true;
                }
                continue;
            }

            if (inCodeBlock)
            {
                html.AppendLine(HtmlEncode(line));
                continue;
            }

            // Headers
            if (trimmedLine.StartsWith("# "))
            {
                CloseParagraph(ref inParagraph, html);
                html.AppendLine($"<h1>{ProcessInlineMarkdown(trimmedLine.Substring(2))}</h1>");
                continue;
            }
            if (trimmedLine.StartsWith("## "))
            {
                CloseParagraph(ref inParagraph, html);
                html.AppendLine($"<h2>{ProcessInlineMarkdown(trimmedLine.Substring(3))}</h2>");
                continue;
            }
            if (trimmedLine.StartsWith("### "))
            {
                CloseParagraph(ref inParagraph, html);
                html.AppendLine($"<h3>{ProcessInlineMarkdown(trimmedLine.Substring(4))}</h3>");
                continue;
            }
            if (trimmedLine.StartsWith("#### "))
            {
                CloseParagraph(ref inParagraph, html);
                html.AppendLine($"<h4>{ProcessInlineMarkdown(trimmedLine.Substring(5))}</h4>");
                continue;
            }

            // Lists
            if (trimmedLine.StartsWith("- ") || trimmedLine.StartsWith("* "))
            {
                CloseParagraph(ref inParagraph, html);
                if (!inList)
                {
                    html.AppendLine("<ul>");
                    inList = true;
                }
                var listItem = ProcessInlineMarkdown(trimmedLine.Substring(2));
                html.AppendLine($"<li>{listItem}</li>");
                continue;
            }

            // Close list if needed
            if (inList && string.IsNullOrWhiteSpace(trimmedLine))
            {
                html.AppendLine("</ul>");
                inList = false;
                continue;
            }

            // Empty line
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                CloseParagraph(ref inParagraph, html);
                continue;
            }

            // Regular paragraph
            if (!inParagraph)
            {
                html.Append("<p>");
                inParagraph = true;
            }
            else
            {
                html.Append(" ");
            }
            html.Append(ProcessInlineMarkdown(trimmedLine));
        }

        CloseParagraph(ref inParagraph, html);
        if (inList)
        {
            html.AppendLine("</ul>");
        }
        if (inCodeBlock)
        {
            html.AppendLine("</code></pre>");
        }

        html.AppendLine("</body></html>");
        return html.ToString();
    }

    private void CloseParagraph(ref bool inParagraph, StringBuilder html)
    {
        if (inParagraph)
        {
            html.AppendLine("</p>");
            inParagraph = false;
        }
    }

    private string ProcessInlineMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Escape HTML first
        text = HtmlEncode(text);

        // Bold: **text** or __text__
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"__(.+?)__", "<strong>$1</strong>");

        // Italic: *text* or _text_ (but not if part of **)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<![\*_])(?<!\*)\*(?!\*)([^\*]+?)(?<!\*)\*(?!\*)(?![\*_])", "<em>$1</em>");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"(?<!_)_([^_]+?)_(?!_)", "<em>$1</em>");

        // Code: `code`
        text = System.Text.RegularExpressions.Regex.Replace(text, @"`([^`]+?)`", "<code>$1</code>");

        // Links: [text](url)
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\[([^\]]+?)\]\(([^\)]+?)\)", "<a href=\"$2\">$1</a>");

        return text;
    }

    private string HtmlEncode(string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
