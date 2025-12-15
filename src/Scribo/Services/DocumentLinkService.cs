using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Scribo.Models;

namespace Scribo.Services;

public class DocumentLinkService
{
    /// <summary>
    /// Parses double bracket links from text: [[Link Text]]
    /// </summary>
    public List<DocumentLink> ParseLinks(string text)
    {
        var links = new List<DocumentLink>();
        if (string.IsNullOrEmpty(text))
            return links;

        // Match [[Link Text]] or [[Link Text|Display Text]]
        var pattern = @"\[\[([^\]]+?)\]\]";
        var matches = Regex.Matches(text, pattern);

        foreach (Match match in matches)
        {
            var linkContent = match.Groups[1].Value;
            var parts = linkContent.Split('|');
            
            var link = new DocumentLink
            {
                LinkText = parts[0].Trim(),
                DisplayText = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim(),
                StartIndex = match.Index,
                Length = match.Length
            };

            links.Add(link);
        }

        return links;
    }

    /// <summary>
    /// Resolves document links by finding matching documents in the project
    /// </summary>
    public void ResolveLinks(List<DocumentLink> links, Project project)
    {
        if (project?.Documents == null)
            return;

        foreach (var link in links)
        {
            // Try to find exact match first
            var document = project.Documents.FirstOrDefault(d => 
                d.Title.Equals(link.LinkText, StringComparison.OrdinalIgnoreCase));

            // If not found, try partial match
            if (document == null)
            {
                document = project.Documents.FirstOrDefault(d => 
                    d.Title.Contains(link.LinkText, StringComparison.OrdinalIgnoreCase));
            }

            if (document != null)
            {
                link.TargetDocumentId = document.Id;
                link.IsResolved = true;
            }
        }
    }

    /// <summary>
    /// Replaces double bracket links with formatted text for display
    /// </summary>
    public string ReplaceLinksForDisplay(string text, List<DocumentLink> links)
    {
        if (string.IsNullOrEmpty(text) || links.Count == 0)
            return text;

        // Replace links in reverse order to maintain indices
        var result = text;
        foreach (var link in links.OrderByDescending(l => l.StartIndex))
        {
            var replacement = link.IsResolved 
                ? $"🔗 {link.DisplayText}" 
                : $"❓ {link.DisplayText}";
            
            if (link.StartIndex + link.Length <= result.Length)
            {
                result = result.Substring(0, link.StartIndex) + 
                        replacement + 
                        result.Substring(link.StartIndex + link.Length);
            }
        }

        return result;
    }
}
