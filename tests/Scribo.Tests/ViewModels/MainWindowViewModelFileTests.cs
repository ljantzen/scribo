using FluentAssertions;
using Scribo.Models;
using Scribo.Services;
using Scribo.ViewModels;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Scribo.Tests.ViewModels;

public class MainWindowViewModelFileTests
{
    [Fact]
    public void CurrentFilePath_ShouldBeEmptyInitially()
    {
        // Arrange & Act
        var viewModel = new MainWindowViewModel();

        // Assert
        viewModel.CurrentFilePath.Should().BeEmpty();
    }

    [Fact]
    public void CurrentProjectPath_ShouldBeEmptyInitially()
    {
        // Arrange & Act
        var viewModel = new MainWindowViewModel();

        // Assert
        viewModel.CurrentProjectPath.Should().BeEmpty();
    }

    [Fact]
    public void HasUnsavedChanges_ShouldBeFalseInitially()
    {
        // Arrange & Act
        var viewModel = new MainWindowViewModel();

        // Assert
        viewModel.HasUnsavedChanges.Should().BeFalse();
    }

    [Fact]
    public void EditorTextChanged_ShouldSetHasUnsavedChanges()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Act
        viewModel.EditorText = "New content";

        // Assert
        viewModel.HasUnsavedChanges.Should().BeTrue();
    }

    [Fact]
    public void CurrentFilePath_ShouldBeSettable()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var filePath = "/path/to/file.txt";

        // Act
        viewModel.CurrentFilePath = filePath;

        // Assert
        viewModel.CurrentFilePath.Should().Be(filePath);
    }

    [Fact]
    public void CurrentProjectPath_ShouldBeSettable()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var projectPath = "/path/to/project.scribo";

        // Act
        viewModel.CurrentProjectPath = projectPath;

        // Assert
        viewModel.CurrentProjectPath.Should().Be(projectPath);
    }

    [Fact]
    public void NewProjectCommand_ShouldExist()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Act & Assert
        viewModel.NewProjectCommand.Should().NotBeNull();
    }

    [Fact]
    public void OpenFileCommand_ShouldExist()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Act & Assert
        viewModel.OpenFileCommand.Should().NotBeNull();
    }

    [Fact]
    public void SaveFileCommand_ShouldExist()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Act & Assert
        viewModel.SaveFileCommand.Should().NotBeNull();
    }

    [Fact]
    public void SaveFileAsCommand_ShouldExist()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Act & Assert
        viewModel.SaveFileAsCommand.Should().NotBeNull();
    }

    [Fact]
    public void SaveProjectAsCommand_ShouldExist()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Act & Assert
        viewModel.SaveProjectAsCommand.Should().NotBeNull();
    }
}
