using FluentAssertions;
using Scribo.Models;
using Scribo.Services;
using Scribo.ViewModels;
using System.Linq;
using Xunit;

namespace Scribo.Tests.ViewModels;

/// <summary>
/// Tests for drag-and-drop and document reordering functionality.
/// </summary>
public class MainWindowViewModelDragDropTests
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ProjectService _projectService;

    public MainWindowViewModelDragDropTests()
    {
        _projectService = new ProjectService();
        _viewModel = new MainWindowViewModel(projectService: _projectService);
    }

    [Fact]
    public void MoveSceneToChapter_ShouldUpdateSceneParentId()
    {
        // Arrange
        var project = _projectService.CreateNewProject("Test Project");
        var chapter1 = new Document { Title = "Chapter 1", Type = DocumentType.Chapter };
        var chapter2 = new Document { Title = "Chapter 2", Type = DocumentType.Chapter };
        var scene = new Document { Title = "Scene 1", Type = DocumentType.Scene, ParentId = chapter1.Id };
        
        project.Documents.Add(chapter1);
        project.Documents.Add(chapter2);
        project.Documents.Add(scene);
        
        _viewModel.LoadProjectIntoTree(project);
        
        var chapter1Node = _viewModel.ProjectTreeItems[0].Children
            .First(c => c.IsManuscriptFolder)
            .Children.First(c => c.Document?.Id == chapter1.Id);
        var chapter2Node = _viewModel.ProjectTreeItems[0].Children
            .First(c => c.IsManuscriptFolder)
            .Children.First(c => c.Document?.Id == chapter2.Id);
        var sceneNode = chapter1Node.Children.First(c => c.Document?.Id == scene.Id);

        // Act
        _viewModel.MoveSceneToChapter(sceneNode, chapter2Node);

        // Assert
        scene.ParentId.Should().Be(chapter2.Id);
        sceneNode.Document.Should().NotBeNull();
        sceneNode.Document!.ParentId.Should().Be(chapter2.Id);
        chapter2Node.Children.Should().Contain(sceneNode);
        chapter1Node.Children.Should().NotContain(sceneNode);
    }

    [Fact]
    public void MoveDocumentToFolder_ShouldUpdateDocumentFolderPath()
    {
        // Arrange
        var project = _projectService.CreateNewProject("Test Project");
        var character = new Document { Title = "Character 1", Type = DocumentType.Character, FolderPath = "" };
        project.Documents.Add(character);
        
        _viewModel.LoadProjectIntoTree(project);
        
        var charactersFolder = _viewModel.ProjectTreeItems[0].Children
            .First(c => c.IsCharactersFolder);
        var characterNode = charactersFolder.Children.First(c => c.Document?.Id == character.Id);
        
        // Create a subfolder
        var subfolderNode = new ProjectTreeItemViewModel
        {
            Name = "Main Characters",
            FolderPath = "Main Characters",
            FolderDocumentType = DocumentType.Character
        };
        charactersFolder.Children.Add(subfolderNode);

        // Act
        _viewModel.MoveDocumentToFolder(characterNode, subfolderNode);

        // Assert
        character.FolderPath.Should().Be("Main Characters");
        characterNode.Document.Should().NotBeNull();
        characterNode.Document!.FolderPath.Should().Be("Main Characters");
        subfolderNode.Children.Should().Contain(characterNode);
        charactersFolder.Children.Should().NotContain(characterNode);
    }

    [Fact]
    public void ReorderDocumentToPosition_ShouldUpdateDocumentOrder()
    {
        // Arrange
        var project = _projectService.CreateNewProject("Test Project");
        var doc1 = new Document { Title = "Doc 1", Type = DocumentType.Character, Order = 0 };
        var doc2 = new Document { Title = "Doc 2", Type = DocumentType.Character, Order = 1 };
        var doc3 = new Document { Title = "Doc 3", Type = DocumentType.Character, Order = 2 };
        
        project.Documents.Add(doc1);
        project.Documents.Add(doc2);
        project.Documents.Add(doc3);
        
        _viewModel.LoadProjectIntoTree(project);
        
        var charactersFolder = _viewModel.ProjectTreeItems[0].Children
            .First(c => c.IsCharactersFolder);
        var doc1Node = charactersFolder.Children.First(c => c.Document?.Id == doc1.Id);
        var doc3Node = charactersFolder.Children.First(c => c.Document?.Id == doc3.Id);

        // Act - Move doc1 to position after doc3
        int targetIndex = charactersFolder.Children.IndexOf(doc3Node) + 1;
        _viewModel.ReorderDocumentToPosition(doc1Node, charactersFolder, targetIndex);

        // Assert
        var doc1Index = charactersFolder.Children.IndexOf(doc1Node);
        var doc3Index = charactersFolder.Children.IndexOf(doc3Node);
        doc1Index.Should().BeGreaterThan(doc3Index);
    }

    [Fact]
    public void MoveDocumentUp_ShouldDecreaseOrder()
    {
        // Arrange
        var project = _projectService.CreateNewProject("Test Project");
        var doc1 = new Document { Title = "Doc 1", Type = DocumentType.Character, Order = 0 };
        var doc2 = new Document { Title = "Doc 2", Type = DocumentType.Character, Order = 1 };
        
        project.Documents.Add(doc1);
        project.Documents.Add(doc2);
        
        _viewModel.LoadProjectIntoTree(project);
        
        var charactersFolder = _viewModel.ProjectTreeItems[0].Children
            .First(c => c.IsCharactersFolder);
        var doc2Node = charactersFolder.Children.First(c => c.Document?.Id == doc2.Id);
        var initialOrder = doc2.Order;
        var initialIndex = charactersFolder.Children.IndexOf(doc2Node);

        // Act
        _viewModel.MoveDocumentUpCommand.Execute(doc2Node);

        // Assert
        doc2.Order.Should().BeLessThan(initialOrder);
        var doc2NewIndex = charactersFolder.Children.IndexOf(doc2Node);
        doc2NewIndex.Should().BeLessThan(initialIndex, "Document should move up in the list");
    }

    [Fact]
    public void MoveDocumentDown_ShouldIncreaseOrder()
    {
        // Arrange
        var project = _projectService.CreateNewProject("Test Project");
        var doc1 = new Document { Title = "Doc 1", Type = DocumentType.Character, Order = 0 };
        var doc2 = new Document { Title = "Doc 2", Type = DocumentType.Character, Order = 1 };
        
        project.Documents.Add(doc1);
        project.Documents.Add(doc2);
        
        _viewModel.LoadProjectIntoTree(project);
        
        var charactersFolder = _viewModel.ProjectTreeItems[0].Children
            .First(c => c.IsCharactersFolder);
        var doc1Node = charactersFolder.Children.First(c => c.Document?.Id == doc1.Id);
        var initialOrder = doc1.Order;
        var initialIndex = charactersFolder.Children.IndexOf(doc1Node);

        // Act
        _viewModel.MoveDocumentDownCommand.Execute(doc1Node);

        // Assert
        doc1.Order.Should().BeGreaterThan(initialOrder);
        var doc1NewIndex = charactersFolder.Children.IndexOf(doc1Node);
        doc1NewIndex.Should().BeGreaterThan(initialIndex, "Document should move down in the list");
    }
}
