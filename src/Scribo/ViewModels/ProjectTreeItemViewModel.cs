using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Scribo.Models;

namespace Scribo.ViewModels;

public partial class ProjectTreeItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string icon = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ProjectTreeItemViewModel> children = new();

    /// <summary>
    /// Indicates whether this is the root project node.
    /// </summary>
    [ObservableProperty]
    private bool isRoot = false;

    /// <summary>
    /// Indicates whether this is the Manuscript folder node.
    /// </summary>
    [ObservableProperty]
    private bool isManuscriptFolder = false;

    /// <summary>
    /// Indicates whether this is the Characters folder node.
    /// </summary>
    [ObservableProperty]
    private bool isCharactersFolder = false;

    /// <summary>
    /// Indicates whether this is the Locations folder node.
    /// </summary>
    [ObservableProperty]
    private bool isLocationsFolder = false;

    /// <summary>
    /// Indicates whether this is the Research folder node.
    /// </summary>
    [ObservableProperty]
    private bool isResearchFolder = false;

    /// <summary>
    /// Indicates whether this is the Notes folder node.
    /// </summary>
    [ObservableProperty]
    private bool isNotesFolder = false;

    /// <summary>
    /// Indicates whether this is the Other folder node.
    /// </summary>
    [ObservableProperty]
    private bool isOtherFolder = false;

    /// <summary>
    /// Indicates whether this is the Timeline folder node.
    /// </summary>
    [ObservableProperty]
    private bool isTimelineFolder = false;

    /// <summary>
    /// Indicates whether this is the Plot folder node.
    /// </summary>
    [ObservableProperty]
    private bool isPlotFolder = false;

    /// <summary>
    /// Indicates whether this is the Object folder node.
    /// </summary>
    [ObservableProperty]
    private bool isObjectFolder = false;

    /// <summary>
    /// Indicates whether this is the Entity folder node.
    /// </summary>
    [ObservableProperty]
    private bool isEntityFolder = false;

    /// <summary>
    /// Indicates whether this is the Trashcan folder node.
    /// </summary>
    [ObservableProperty]
    private bool isTrashcanFolder = false;

    /// <summary>
    /// Folder path for subfolders (e.g., "Main Characters" for a subfolder in Characters).
    /// Empty for main type folders.
    /// </summary>
    [ObservableProperty]
    private string folderPath = string.Empty;

    /// <summary>
    /// Document type that this folder can contain (for subfolders).
    /// Null for main folders or document nodes.
    /// </summary>
    public DocumentType? FolderDocumentType { get; set; }

    /// <summary>
    /// Indicates whether this item is currently being renamed.
    /// </summary>
    [ObservableProperty]
    private bool isRenaming = false;

    /// <summary>
    /// Temporary name used during renaming.
    /// </summary>
    [ObservableProperty]
    private string renameText = string.Empty;

    /// <summary>
    /// Indicates whether this tree node is expanded.
    /// </summary>
    [ObservableProperty]
    private bool isExpanded = false;

    /// <summary>
    /// Reference to the Document model, if this tree item represents a document.
    /// </summary>
    public Document? Document { get; set; }

    /// <summary>
    /// Indicates whether this is a chapter node (has a Document with Type == Chapter).
    /// </summary>
    public bool IsChapter => Document?.Type == DocumentType.Chapter;

    /// <summary>
    /// Indicates whether this is a scene node (has a Document with Type == Scene).
    /// </summary>
    public bool IsScene => Document?.Type == DocumentType.Scene;

    /// <summary>
    /// Indicates whether this is a folder node (not a document).
    /// </summary>
    public bool IsFolder => Document == null;

    /// <summary>
    /// Indicates whether this is a subfolder (a folder that's not a main type folder).
    /// </summary>
    public bool IsSubfolder => IsFolder && !IsRoot && !IsManuscriptFolder && 
                               !IsCharactersFolder && !IsLocationsFolder && !IsResearchFolder && !IsNotesFolder && !IsOtherFolder && 
                               !IsTimelineFolder && !IsPlotFolder && !IsObjectFolder && !IsEntityFolder && !IsTrashcanFolder;

    /// <summary>
    /// Indicates whether this folder can have subfolders created in it.
    /// </summary>
    public bool CanCreateSubfolder => IsManuscriptFolder || IsCharactersFolder || IsLocationsFolder || IsResearchFolder || IsNotesFolder || 
                                      IsOtherFolder || IsTimelineFolder || IsPlotFolder || IsObjectFolder || IsEntityFolder || IsSubfolder;

    /// <summary>
    /// Indicates whether this subfolder can contain characters.
    /// </summary>
    public bool CanContainCharacters => IsCharactersFolder || (IsSubfolder && FolderDocumentType == DocumentType.Character);

    /// <summary>
    /// Indicates whether this subfolder can contain locations.
    /// </summary>
    public bool CanContainLocations => IsLocationsFolder || (IsSubfolder && FolderDocumentType == DocumentType.Location);

    /// <summary>
    /// Indicates whether this folder can contain chapters (Manuscript folder or its subfolders).
    /// </summary>
    public bool CanContainChapters => IsManuscriptFolder || (IsSubfolder && FolderDocumentType == DocumentType.Chapter);

    /// <summary>
    /// Indicates whether this folder can contain notes (Notes folder or Research folder or their subfolders).
    /// </summary>
    public bool CanContainNotes => IsNotesFolder || IsResearchFolder || (IsSubfolder && (FolderDocumentType == DocumentType.Note || FolderDocumentType == DocumentType.Research));

    /// <summary>
    /// Indicates whether this folder can contain other documents (Other folder or its subfolders).
    /// </summary>
    public bool CanContainOther => IsOtherFolder || (IsSubfolder && FolderDocumentType == DocumentType.Other);

    /// <summary>
    /// Indicates whether this folder can contain timeline documents (Timeline folder or its subfolders).
    /// </summary>
    public bool CanContainTimeline => IsTimelineFolder || (IsSubfolder && FolderDocumentType == DocumentType.Timeline);

    /// <summary>
    /// Indicates whether this folder can contain plot documents (Plot folder or its subfolders).
    /// </summary>
    public bool CanContainPlot => IsPlotFolder || (IsSubfolder && FolderDocumentType == DocumentType.Plot);

    /// <summary>
    /// Indicates whether this folder can contain object documents (Object folder or its subfolders).
    /// </summary>
    public bool CanContainObject => IsObjectFolder || (IsSubfolder && FolderDocumentType == DocumentType.Object);

    /// <summary>
    /// Indicates whether this folder can contain entity documents (Entity folder or its subfolders).
    /// </summary>
    public bool CanContainEntity => IsEntityFolder || (IsSubfolder && FolderDocumentType == DocumentType.Entity);

    /// <summary>
    /// Indicates whether this item can be renamed (documents and subfolders).
    /// </summary>
    public bool CanRename => Document != null || IsSubfolder;

    /// <summary>
    /// Indicates whether this item can be deleted (documents and subfolders, but not root or main folders).
    /// </summary>
    public bool CanDelete => (Document != null || IsSubfolder) && 
                            !IsRoot && !IsManuscriptFolder && !IsCharactersFolder && 
                            !IsLocationsFolder && !IsResearchFolder && !IsNotesFolder && !IsOtherFolder && 
                            !IsTimelineFolder && !IsPlotFolder && !IsObjectFolder && !IsEntityFolder && !IsTrashcanFolder;
}
