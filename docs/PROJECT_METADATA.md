# Scribo Project Metadata Reference

This document describes all metadata available in Scribo project files (`.json` format), modeled after Scrivener's comprehensive project structure.

## 1. Basic Project Information

Core project identification and description fields:

- **`Title`** - Project title (automatically synced with project name)
- **`Author`** - Author name
- **`Publisher`** - Publisher name
- **`ISBN`** - ISBN number
- **`Copyright`** - Copyright notice
- **`Comments`** - General comments about the project
- **`ProjectNotes`** - Project-wide notes

## 2. Word Count Targets and Goals (`WordCountTargets`)

Writing goals and progress tracking:

- **`TargetWordCount`** - Target word count (default: `0`)
- **`TargetCharacterCount`** - Target character count (default: `0`)
- **`TargetPageCount`** - Target page count (default: `0`)
- **`TargetDate`** - Optional deadline date (nullable)
- **`ShowTargetInStatusBar`** - Display target in status bar (default: `true`)
- **`IncludeNotesInCount`** - Include notes in word count calculations (default: `false`)
- **`IncludeResearchInCount`** - Include research documents in word count (default: `false`)

## 3. Project Statistics (`ProjectStatistics`)

Automatically calculated project statistics:

- **`TotalWordCount`** - Total words across all documents
- **`TotalCharacterCount`** - Total characters (including spaces)
- **`TotalCharacterCountNoSpaces`** - Total characters (excluding spaces)
- **`TotalPageCount`** - Estimated page count (based on ~250 words per page)
- **`ParagraphCount`** - Total number of paragraphs
- **`SentenceCount`** - Total number of sentences
- **`LastCalculatedAt`** - Timestamp of last statistics calculation (nullable)

## 4. Session Information (`SessionInfo`)

Writing session tracking for productivity monitoring:

- **`CurrentSession`** - Active writing session
  - **`StartTime`** - Session start timestamp
  - **`EndTime`** - Session end timestamp (null if session is active)
  - **`WordsWritten`** - Words written during this session
  - **`CharactersWritten`** - Characters written during this session
  - **`Notes`** - Session-specific notes
- **`SessionHistory`** - List of all past writing sessions

## 5. Keywords and Tags

Organization and categorization:

- **`Keywords`** - List of project keywords (array of strings)
- **`Tags`** - List of project tags (array of strings)

## 6. Custom Metadata Fields (`CustomFields`)

User-defined key-value pairs for flexible metadata:

- Dictionary structure allowing custom fields like:
  - Genre (e.g., "Science Fiction", "Fantasy")
  - Status (e.g., "Draft", "In Progress", "Complete")
  - Priority, Rating, or any custom categorization

## 7. Compile Settings (`CompileSettings`)

Export/compile configuration for generating final documents:

- **`OutputFormat`** - Output format (default: `"PDF"`)
- **`PageSize`** - Page size (default: `"A4"`)
- **`FontFamily`** - Font family name (default: `"Times New Roman"`)
- **`FontSize`** - Font size in points (default: `12`)
- **`LineSpacing`** - Line spacing multiplier (default: `1.5`)
- **`MarginTop`** - Top margin in cm (default: `2.54`)
- **`MarginBottom`** - Bottom margin in cm (default: `2.54`)
- **`MarginLeft`** - Left margin in cm (default: `3.18`)
- **`MarginRight`** - Right margin in cm (default: `3.18`)
- **`IncludeTitlePage`** - Include title page (default: `true`)
- **`IncludeTableOfContents`** - Include table of contents (default: `true`)
- **`IncludePageNumbers`** - Include page numbers (default: `true`)
- **`PageNumberPosition`** - Page number position (default: `"Bottom Center"`)

## 8. Document Organization Settings (`DocumentOrganizationSettings`)

UI and document organization preferences:

- **`ShowBinderInSidebar`** - Show binder in sidebar (default: `true`)
- **`ShowCorkboard`** - Show corkboard view (default: `false`)
- **`ShowOutliner`** - Show outliner view (default: `false`)
- **`DefaultDocumentType`** - Default document type for new documents (default: `"Chapter"`)
- **`AutoNumberDocuments`** - Automatically number documents (default: `false`)
- **`NumberingFormat`** - Numbering format string (default: `"Chapter {n}"`)

## 9. Research Folder Settings (`ResearchFolderSettings`)

Research folder configuration:

- **`ShowResearchInSidebar`** - Show research folder in sidebar (default: `true`)
- **`AllowWebResearch`** - Allow web research links (default: `true`)
- **`MaxResearchFileSize`** - Maximum file size in MB (default: `50`)
- **`AllowedResearchExtensions`** - List of allowed file extensions (default: `.pdf`, `.doc`, `.docx`, `.txt`, `.md`, `.rtf`, `.html`, `.jpg`, `.png`, `.gif`)

## 10. Project Settings (`ProjectSettings`)

Application and editor preferences:

- **`AutoSave`** - Enable auto-save (default: `true`)
- **`AutoSaveIntervalMinutes`** - Auto-save interval in minutes (default: `5`)
- **`BackupOnSave`** - Create backup on save (default: `true`)
- **`MaxBackups`** - Maximum number of backup files to keep (default: `10`)
- **`BackupLocation`** - Backup directory path (empty = default location)
- **`ShowWordCountInStatusBar`** - Show word count in status bar (default: `true`)
- **`ShowCharacterCountInStatusBar`** - Show character count in status bar (default: `true`)
- **`ShowPageCountInStatusBar`** - Show page count in status bar (default: `false`)
- **`DefaultFontFamily`** - Default editor font family (default: `"Consolas"`)
- **`DefaultFontSize`** - Default editor font size (default: `14`)
- **`WordWrap`** - Enable word wrap (default: `true`)
- **`ShowLineNumbers`** - Show line numbers (default: `false`)
- **`ShowRuler`** - Show ruler (default: `false`)
- **`Theme`** - UI theme name (default: `"Light"`)

## 11. Version Information

Project versioning and timestamps:

- **`Version`** - Project format version (default: `"1.0"`)
- **`CreatedAt`** - Project creation timestamp
- **`ModifiedAt`** - Last modification timestamp
- **`LastOpenedAt`** - Last opened timestamp (nullable)

## JSON Structure Example

```json
{
  "name": "My Writing Project",
  "filePath": "/path/to/project.json",
  "documents": [],
  "metadata": {
    "title": "My Writing Project",
    "author": "John Doe",
    "publisher": "",
    "isbn": "",
    "copyright": "2024",
    "comments": "",
    "projectNotes": "",
    "wordCountTargets": {
      "targetWordCount": 50000,
      "targetCharacterCount": 0,
      "targetPageCount": 200,
      "targetDate": null,
      "showTargetInStatusBar": true,
      "includeNotesInCount": false,
      "includeResearchInCount": false
    },
    "statistics": {
      "totalWordCount": 0,
      "totalCharacterCount": 0,
      "totalCharacterCountNoSpaces": 0,
      "totalPageCount": 0,
      "paragraphCount": 0,
      "sentenceCount": 0,
      "lastCalculatedAt": null
    },
    "currentSession": {
      "startTime": "2024-12-15T10:00:00Z",
      "endTime": null,
      "wordsWritten": 0,
      "charactersWritten": 0,
      "notes": ""
    },
    "sessionHistory": [],
    "keywords": ["fiction", "novel"],
    "tags": ["draft"],
    "customFields": {
      "genre": "Science Fiction",
      "status": "In Progress"
    },
    "compileSettings": {
      "outputFormat": "PDF",
      "pageSize": "A4",
      "fontFamily": "Times New Roman",
      "fontSize": 12,
      "lineSpacing": 1.5,
      "marginTop": 2.54,
      "marginBottom": 2.54,
      "marginLeft": 3.18,
      "marginRight": 3.18,
      "includeTitlePage": true,
      "includeTableOfContents": true,
      "includePageNumbers": true,
      "pageNumberPosition": "Bottom Center"
    },
    "documentOrganization": {
      "showBinderInSidebar": true,
      "showCorkboard": false,
      "showOutliner": false,
      "defaultDocumentType": "Chapter",
      "autoNumberDocuments": false,
      "numberingFormat": "Chapter {n}"
    },
    "researchSettings": {
      "showResearchInSidebar": true,
      "allowWebResearch": true,
      "maxResearchFileSize": 50,
      "allowedResearchExtensions": [
        ".pdf", ".doc", ".docx", ".txt", ".md", ".rtf", ".html",
        ".jpg", ".png", ".gif"
      ]
    },
    "settings": {
      "autoSave": true,
      "autoSaveIntervalMinutes": 5,
      "backupOnSave": true,
      "maxBackups": 10,
      "backupLocation": "",
      "showWordCountInStatusBar": true,
      "showCharacterCountInStatusBar": true,
      "showPageCountInStatusBar": false,
      "defaultFontFamily": "Consolas",
      "defaultFontSize": 14,
      "wordWrap": true,
      "showLineNumbers": false,
      "showRuler": false,
      "theme": "Light"
    },
    "version": "1.0",
    "createdAt": "2024-12-15T10:00:00Z",
    "modifiedAt": "2024-12-15T10:00:00Z",
    "lastOpenedAt": null
  },
  "createdAt": "2024-12-15T10:00:00Z",
  "modifiedAt": "2024-12-15T10:00:00Z"
}
```

## Notes

- All metadata is stored in the project `.json` file
- Statistics are automatically recalculated when saving the project
- Custom fields allow for project-specific metadata without code changes
- Session tracking helps monitor writing productivity over time
- Compile settings control how the project is exported to final formats
- All settings persist across application sessions
