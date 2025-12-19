using System;
using System.Collections.Generic;
using System.Linq;
using Scribo.Models;

namespace Scribo.Services;

/// <summary>
/// Service for validating and resolving metadata references to documents.
/// </summary>
public class MetadataReferenceService
{
    private readonly Project? _project;

    public MetadataReferenceService(Project? project = null)
    {
        _project = project;
    }

    /// <summary>
    /// Sets the project for this service instance.
    /// </summary>
    public void SetProject(Project project)
    {
        // Note: This service is designed to be stateless, but we store project for convenience
        // In practice, you'd pass project to each method call
    }

    /// <summary>
    /// Validates that a character name references an actual character document.
    /// </summary>
    public bool IsValidCharacterReference(string? characterName, Project project)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            return false;

        return project.Documents.Any(d => 
            d.Type == DocumentType.Character && 
            string.Equals(d.Title, characterName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the character document referenced by name, or null if not found.
    /// </summary>
    public Document? GetCharacterDocument(string? characterName, Project project)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            return null;

        return project.Documents.FirstOrDefault(d => 
            d.Type == DocumentType.Character && 
            string.Equals(d.Title, characterName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validates that all character names in a list reference actual character documents.
    /// </summary>
    public List<string> GetInvalidCharacterReferences(List<string> characterNames, Project project)
    {
        if (characterNames == null || characterNames.Count == 0)
            return new List<string>();

        var validCharacterTitles = project.Documents
            .Where(d => d.Type == DocumentType.Character)
            .Select(d => d.Title)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return characterNames
            .Where(name => !string.IsNullOrWhiteSpace(name) && !validCharacterTitles.Contains(name))
            .ToList();
    }

    /// <summary>
    /// Gets all character documents referenced by names in a list.
    /// </summary>
    public List<Document> GetCharacterDocuments(List<string> characterNames, Project project)
    {
        if (characterNames == null || characterNames.Count == 0)
            return new List<Document>();

        var characterTitles = characterNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return project.Documents
            .Where(d => d.Type == DocumentType.Character && characterTitles.Contains(d.Title))
            .ToList();
    }

    /// <summary>
    /// Validates that a timeline name references an actual timeline document.
    /// </summary>
    public bool IsValidTimelineReference(string? timelineName, Project project)
    {
        if (string.IsNullOrWhiteSpace(timelineName))
            return false;

        return project.Documents.Any(d => 
            d.Type == DocumentType.Timeline && 
            string.Equals(d.Title, timelineName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the timeline document referenced by name, or null if not found.
    /// </summary>
    public Document? GetTimelineDocument(string? timelineName, Project project)
    {
        if (string.IsNullOrWhiteSpace(timelineName))
            return null;

        return project.Documents.FirstOrDefault(d => 
            d.Type == DocumentType.Timeline && 
            string.Equals(d.Title, timelineName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validates that a plot name references an actual plot document.
    /// </summary>
    public bool IsValidPlotReference(string? plotName, Project project)
    {
        if (string.IsNullOrWhiteSpace(plotName))
            return false;

        return project.Documents.Any(d => 
            d.Type == DocumentType.Plot && 
            string.Equals(d.Title, plotName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the plot document referenced by name, or null if not found.
    /// </summary>
    public Document? GetPlotDocument(string? plotName, Project project)
    {
        if (string.IsNullOrWhiteSpace(plotName))
            return null;

        return project.Documents.FirstOrDefault(d => 
            d.Type == DocumentType.Plot && 
            string.Equals(d.Title, plotName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validates that an object name references an actual object document.
    /// </summary>
    public bool IsValidObjectReference(string? objectName, Project project)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return false;

        return project.Documents.Any(d => 
            d.Type == DocumentType.Object && 
            string.Equals(d.Title, objectName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the object document referenced by name, or null if not found.
    /// </summary>
    public Document? GetObjectDocument(string? objectName, Project project)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        return project.Documents.FirstOrDefault(d => 
            d.Type == DocumentType.Object && 
            string.Equals(d.Title, objectName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validates that all object names in a list reference actual object documents.
    /// </summary>
    public List<string> GetInvalidObjectReferences(List<string> objectNames, Project project)
    {
        if (objectNames == null || objectNames.Count == 0)
            return new List<string>();

        var validObjectTitles = project.Documents
            .Where(d => d.Type == DocumentType.Object)
            .Select(d => d.Title)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return objectNames
            .Where(name => !string.IsNullOrWhiteSpace(name) && !validObjectTitles.Contains(name))
            .ToList();
    }

    /// <summary>
    /// Validates that an entity name references an actual entity document.
    /// </summary>
    public bool IsValidEntityReference(string? entityName, Project project)
    {
        if (string.IsNullOrWhiteSpace(entityName))
            return false;

        return project.Documents.Any(d => 
            d.Type == DocumentType.Entity && 
            string.Equals(d.Title, entityName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets the entity document referenced by name, or null if not found.
    /// </summary>
    public Document? GetEntityDocument(string? entityName, Project project)
    {
        if (string.IsNullOrWhiteSpace(entityName))
            return null;

        return project.Documents.FirstOrDefault(d => 
            d.Type == DocumentType.Entity && 
            string.Equals(d.Title, entityName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Validates that all entity names in a list reference actual entity documents.
    /// </summary>
    public List<string> GetInvalidEntityReferences(List<string> entityNames, Project project)
    {
        if (entityNames == null || entityNames.Count == 0)
            return new List<string>();

        var validEntityTitles = project.Documents
            .Where(d => d.Type == DocumentType.Entity)
            .Select(d => d.Title)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return entityNames
            .Where(name => !string.IsNullOrWhiteSpace(name) && !validEntityTitles.Contains(name))
            .ToList();
    }

    /// <summary>
    /// Gets all available character document names for autocomplete/validation.
    /// </summary>
    public List<string> GetAvailableCharacterNames(Project project)
    {
        return project.Documents
            .Where(d => d.Type == DocumentType.Character)
            .Select(d => d.Title)
            .OrderBy(title => title)
            .ToList();
    }

    /// <summary>
    /// Gets all available timeline document names for autocomplete/validation.
    /// </summary>
    public List<string> GetAvailableTimelineNames(Project project)
    {
        return project.Documents
            .Where(d => d.Type == DocumentType.Timeline)
            .Select(d => d.Title)
            .OrderBy(title => title)
            .ToList();
    }

    /// <summary>
    /// Gets all available plot document names for autocomplete/validation.
    /// </summary>
    public List<string> GetAvailablePlotNames(Project project)
    {
        return project.Documents
            .Where(d => d.Type == DocumentType.Plot)
            .Select(d => d.Title)
            .OrderBy(title => title)
            .ToList();
    }

    /// <summary>
    /// Gets all available object document names for autocomplete/validation.
    /// </summary>
    public List<string> GetAvailableObjectNames(Project project)
    {
        return project.Documents
            .Where(d => d.Type == DocumentType.Object)
            .Select(d => d.Title)
            .OrderBy(title => title)
            .ToList();
    }

    /// <summary>
    /// Gets all available entity document names for autocomplete/validation.
    /// </summary>
    public List<string> GetAvailableEntityNames(Project project)
    {
        return project.Documents
            .Where(d => d.Type == DocumentType.Entity)
            .Select(d => d.Title)
            .OrderBy(title => title)
            .ToList();
    }
}
