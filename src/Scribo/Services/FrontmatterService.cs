using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Scribo.Services;

public class FrontmatterService
{
    private readonly IDeserializer _yamlDeserializer;
    private readonly ISerializer _yamlSerializer;

    public FrontmatterService()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        _yamlSerializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
    }

    /// <summary>
    /// Parses frontmatter from markdown content and returns the content without frontmatter.
    /// </summary>
    public (Dictionary<string, object>? frontmatter, string content) ParseFrontmatter(string markdownContent)
    {
        if (string.IsNullOrEmpty(markdownContent))
            return (null, markdownContent);

        // Check if content starts with frontmatter delimiter
        if (!markdownContent.TrimStart().StartsWith("---"))
            return (null, markdownContent);

        var lines = markdownContent.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
        if (lines.Length < 2)
            return (null, markdownContent);

        // Find the first --- delimiter
        int startIndex = -1;
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                startIndex = i;
                break;
            }
        }

        if (startIndex == -1)
            return (null, markdownContent);

        // Find the second --- delimiter
        int endIndex = -1;
        for (int i = startIndex + 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "---")
            {
                endIndex = i;
                break;
            }
        }

        if (endIndex == -1)
            return (null, markdownContent);

        // Extract frontmatter YAML
        var frontmatterLines = lines.Skip(startIndex + 1).Take(endIndex - startIndex - 1);
        var frontmatterYaml = string.Join("\n", frontmatterLines);

        // Extract content (everything after the second ---)
        var contentLines = lines.Skip(endIndex + 1);
        var content = string.Join("\n", contentLines);

        // Parse YAML frontmatter
        Dictionary<string, object>? frontmatter = null;
        try
        {
            frontmatter = _yamlDeserializer.Deserialize<Dictionary<string, object>>(frontmatterYaml);
        }
        catch
        {
            // If parsing fails, return content without frontmatter
            return (null, markdownContent);
        }

        return (frontmatter, content.TrimStart());
    }

    /// <summary>
    /// Writes frontmatter and content to a markdown string.
    /// </summary>
    public string WriteFrontmatter(Dictionary<string, object> frontmatter, string content)
    {
        if (frontmatter == null || frontmatter.Count == 0)
            return content;

        var yaml = _yamlSerializer.Serialize(frontmatter);
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.Append(yaml);
        sb.AppendLine("---");
        sb.Append(content);

        return sb.ToString();
    }

    /// <summary>
    /// Extracts a value from frontmatter dictionary with type conversion.
    /// </summary>
    public T? GetValue<T>(Dictionary<string, object>? frontmatter, string key, T? defaultValue = default)
    {
        if (frontmatter == null || !frontmatter.ContainsKey(key))
            return defaultValue;

        var value = frontmatter[key];
        if (value == null)
            return defaultValue;

        try
        {
            if (value is T directValue)
                return directValue;

            // Handle string to other types
            if (typeof(T) == typeof(string))
                return (T)(object)value.ToString()!;

            if (typeof(T) == typeof(DateTime) && value is string dateStr)
            {
                if (DateTime.TryParse(dateStr, out var date))
                    return (T)(object)date;
            }

            if (typeof(T) == typeof(List<string>) && value is List<object> list)
            {
                return (T)(object)list.Select(x => x?.ToString() ?? string.Empty).ToList();
            }

            // Try direct conversion
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }
}
