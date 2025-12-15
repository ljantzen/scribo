# Test Coverage Summary

## Overall Coverage Statistics

- **Line Coverage**: 28.79% (652 lines covered out of 2,264 total)
- **Branch Coverage**: 24.54% (136 branches covered out of 554 total)
- **Test Results**: 152 Passed, 14 Failed, 0 Skipped (166 total)

## Test Files Overview

### Models (6 test files)
- ✅ `DocumentTests.cs` - Basic document functionality
- ✅ `DocumentAdditionalTests.cs` - Extended document tests
- ✅ `ProjectTests.cs` - Project model tests
- ✅ `ProjectMetadataTests.cs` - Project metadata tests
- ✅ `PluginInfoTests.cs` - Plugin info model tests

### Services (8 test files)
- ✅ `FileServiceTests.cs` - File operations
- ✅ `FileServiceIntegrationTests.cs` - File service integration
- ✅ `ProjectServiceTests.cs` - Project service basics
- ✅ `ProjectServiceAdditionalTests.cs` - Extended project service tests
- ✅ `ProjectServiceJsonFileTests.cs` - JSON file handling
- ✅ `TextStatisticsServiceTests.cs` - Text statistics
- ✅ `TextStatisticsServiceAdditionalTests.cs` - Extended statistics
- ✅ `PluginManagerTests.cs` - Plugin management
- ✅ `PluginContextTests.cs` - Plugin context

### ViewModels (6 test files)
- ✅ `MainWindowViewModelTests.cs` - Basic view model tests
- ✅ `MainWindowViewModelAdditionalTests.cs` - Extended view model tests
- ⚠️ `MainWindowViewModelFileTests.cs` - File operations (1 failing test)
- ✅ `PluginManagerViewModelTests.cs` - Plugin manager view model
- ✅ `PluginManagerViewModelAdditionalTests.cs` - Extended plugin manager tests
- ✅ `ProjectTreeItemViewModelTests.cs` - Tree item view model
- ✅ `ProjectTreeItemViewModelAdditionalTests.cs` - Extended tree item tests

### Plugins (1 test file)
- ✅ `PluginBaseTests.cs` - Plugin base class tests

## Known Issues

### Failing Tests (14 total)

1. **MainWindowViewModelAdditionalTests.Commands_ShouldExecuteWithoutThrowing**
   - **Issue**: `ShowPluginManagerCommand` tries to create `PluginManagerWindow` which requires Avalonia initialization
   - **Error**: `System.InvalidOperationException: Unable to locate 'Avalonia.Platform.IWindowingPlatform'`
   - **Fix Needed**: Mock the window creation or skip UI-dependent commands in unit tests

2. **ProjectServiceAdditionalTests.SaveProject_ShouldCreateDirectoryIfNotExists**
   - **Issue**: Directory creation test failing
   - **Fix Needed**: Review test implementation

3. **TextStatisticsServiceTests.CalculateStatistics_ShouldHandleMultilineText**
   - **Issue**: Multiline text statistics calculation
   - **Fix Needed**: Review calculation logic or test expectations

4. **DocumentAdditionalTests.Document_CharacterCountNoSpaces_ShouldExcludeAllWhitespace**
   - **Issue**: Character count excluding whitespace
   - **Fix Needed**: Review character count implementation

5. **TextStatisticsServiceAdditionalTests** (Multiple tests)
   - Tests for very long text, punctuation-only text, special characters, mixed line endings
   - **Fix Needed**: Review text statistics service implementation

6. **MainWindowViewModelTests.MainWindowViewModel_ShouldInitializeProjectTree**
   - **Issue**: Project tree initialization test
   - **Fix Needed**: May be related to recent changes in tree structure

7. **DocumentTests.CharacterCountNoSpaces_ShouldExcludeSpacesAndNewlines**
   - **Issue**: Character count excluding spaces and newlines
   - **Fix Needed**: Review character count implementation

8. **ProjectServiceJsonFileTests.LoadProject_ShouldCreateMetadataIfMissing**
   - **Issue**: Metadata creation on project load
   - **Fix Needed**: Review metadata handling logic

## Coverage Gaps

### Low Coverage Areas

1. **Views** (0% coverage)
   - All `.axaml.cs` code-behind files
   - Window initialization and event handlers
   - UI interaction logic
   - Drag-and-drop handlers
   - Context menu handlers

2. **ViewModels** (Partial coverage)
   - Command execution paths
   - Complex state management
   - Dialog interactions
   - Drag-and-drop operations (`MoveSceneToChapter`, `MoveDocumentToFolder`, `ReorderDocumentToPosition`)
   - Document reordering (`MoveDocumentUp`, `MoveDocumentDown`)

3. **Services** (Partial coverage)
   - Error handling paths
   - Edge cases
   - Integration scenarios
   - File moving operations when documents are moved between folders

### Missing Test Coverage

1. **New Features Added Recently**:
   - ✅ Chapter creation (`AddChapterCommand`)
   - ✅ Scene creation (`AddSceneCommand`)
   - ✅ Character/Location/Note creation (`AddCharacterCommand`, `AddLocationCommand`, `AddNoteCommand`)
   - ✅ Subfolder creation (`CreateSubfolderCommand`)
   - ✅ Rename functionality (`RenameChapterCommand`, `CommitRename`, `CancelRename`)
   - ✅ Project properties dialog
   - ✅ Preferences dialog
   - ✅ Most Recently Used (MRU) service
   - ✅ Context menu interactions
   - ❌ **Drag and Drop Operations**:
     - `MoveSceneToChapter` - Moving scenes between chapters
     - `MoveDocumentToFolder` - Moving documents between folders/subfolders
     - `ReorderDocumentToPosition` - Reordering documents via drag-and-drop
   - ❌ **Document Reordering**:
     - `MoveDocumentUp` - Moving documents up within a folder
     - `MoveDocumentDown` - Moving documents down within a folder
     - `UpdateDocumentOrders` - Updating order properties

2. **Project Service**:
   - ✅ Content file path generation (`GenerateContentFilePath`)
   - ✅ File system operations for markdown files
   - ✅ Backward compatibility handling
   - ❌ **File Moving**:
     - Moving files when documents are moved between folders
     - Handling file path changes on save
     - Creating directories for new file locations

3. **Document Model**:
   - ✅ Content lazy loading from files
   - ✅ `SaveContent()` method
   - ✅ `ProjectDirectory` property usage
   - ✅ `FolderPath` property for subfolder organization
   - ✅ `Order` property for document ordering

4. **Tree View Operations**:
   - ✅ Tree item selection handling
   - ✅ Content loading on selection
   - ✅ Tree expansion/collapse state
   - ❌ **Drag and Drop UI**:
     - Pointer event handling
     - Drag threshold detection
     - Drop target validation

## Recommendations

1. **Fix Failing Tests**: Update `Commands_ShouldExecuteWithoutThrowing` to handle UI-dependent commands properly

2. **Add Tests for New Features**:
   - ✅ Test chapter/scene creation
   - ✅ Test rename functionality
   - ✅ Test MRU service
   - ✅ Test project properties updates
   - ❌ **Add tests for drag-and-drop**:
     - Test `MoveSceneToChapter` with various scenarios
     - Test `MoveDocumentToFolder` for different document types
     - Test `ReorderDocumentToPosition` for reordering
     - Test file moving when documents are moved
   - ❌ **Add tests for document reordering**:
     - Test `MoveDocumentUp` and `MoveDocumentDown`
     - Test order persistence
     - Test order updates when documents are added/removed

3. **Increase Coverage**:
   - Add integration tests for file operations
   - Test error handling paths
   - Test edge cases (empty projects, missing files, etc.)
   - Test file moving scenarios

4. **UI Testing**:
   - Consider adding UI automation tests for critical workflows
   - Or mock UI components for unit testing
   - Test drag-and-drop interactions

5. **Coverage Target**:
   - Aim for at least 60-70% line coverage
   - Focus on business logic and services first
   - UI code can have lower coverage if properly isolated

## Test Priorities

### High Priority
1. Drag-and-drop operations (`MoveSceneToChapter`, `MoveDocumentToFolder`)
2. File moving when documents are moved between folders
3. Document reordering (`MoveDocumentUp`, `MoveDocumentDown`)
4. Order property persistence

### Medium Priority
1. Subfolder operations
2. Context menu interactions
3. Error handling for file operations
4. Edge cases (missing files, invalid paths)

### Low Priority
1. UI event handlers (can be tested via integration tests)
2. Visual tree operations
3. Drag-and-drop UI interactions (can be tested via UI automation)
