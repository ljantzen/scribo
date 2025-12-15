using FluentAssertions;
using Scribo.Services;
using Scribo.ViewModels;
using Xunit;

namespace Scribo.Tests.ViewModels;

public class MainWindowViewModelAdditionalTests
{
    [Fact]
    public void MainWindowViewModel_ShouldAcceptPluginManager()
    {
        // Arrange
        var pluginManager = new PluginManager();

        // Act
        var viewModel = new MainWindowViewModel(pluginManager);

        // Assert
        viewModel.Should().NotBeNull();
    }

    [Fact]
    public void ToggleProjectTreeSide_ShouldToggleMultipleTimes()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Act & Assert
        viewModel.IsProjectTreeOnLeft.Should().BeTrue();
        viewModel.IsProjectTreeOnRight.Should().BeFalse();

        viewModel.ToggleProjectTreeSideCommand.Execute(null);
        viewModel.IsProjectTreeOnLeft.Should().BeFalse();
        viewModel.IsProjectTreeOnRight.Should().BeTrue();

        viewModel.ToggleProjectTreeSideCommand.Execute(null);
        viewModel.IsProjectTreeOnLeft.Should().BeTrue();
        viewModel.IsProjectTreeOnRight.Should().BeFalse();
    }

    [Fact]
    public void Commands_ShouldNotBeNull()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Act & Assert
        viewModel.NewProjectCommand.Should().NotBeNull();
        viewModel.OpenProjectCommand.Should().NotBeNull();
        viewModel.SaveProjectCommand.Should().NotBeNull();
        viewModel.ExitCommand.Should().NotBeNull();
        viewModel.UndoCommand.Should().NotBeNull();
        viewModel.RedoCommand.Should().NotBeNull();
        viewModel.CutCommand.Should().NotBeNull();
        viewModel.CopyCommand.Should().NotBeNull();
        viewModel.PasteCommand.Should().NotBeNull();
        viewModel.ToggleProjectTreeSideCommand.Should().NotBeNull();
        viewModel.ShowWordCountCommand.Should().NotBeNull();
        viewModel.ShowCharacterCountCommand.Should().NotBeNull();
        viewModel.ShowAboutCommand.Should().NotBeNull();
        viewModel.ShowPluginManagerCommand.Should().NotBeNull();
    }

    [Fact]
    public void Commands_ShouldExecuteWithoutThrowing()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();

        // Act & Assert
        viewModel.Invoking(vm => vm.NewProjectCommand.Execute(null)).Should().NotThrow();
        viewModel.Invoking(vm => vm.OpenProjectCommand.Execute(null)).Should().NotThrow();
        viewModel.Invoking(vm => vm.SaveProjectCommand.Execute(null)).Should().NotThrow();
        viewModel.Invoking(vm => vm.UndoCommand.Execute(null)).Should().NotThrow();
        viewModel.Invoking(vm => vm.RedoCommand.Execute(null)).Should().NotThrow();
        viewModel.Invoking(vm => vm.CutCommand.Execute(null)).Should().NotThrow();
        viewModel.Invoking(vm => vm.CopyCommand.Execute(null)).Should().NotThrow();
        viewModel.Invoking(vm => vm.PasteCommand.Execute(null)).Should().NotThrow();
        viewModel.Invoking(vm => vm.ShowWordCountCommand.Execute(null)).Should().NotThrow();
        viewModel.Invoking(vm => vm.ShowCharacterCountCommand.Execute(null)).Should().NotThrow();
        viewModel.Invoking(vm => vm.ShowAboutCommand.Execute(null)).Should().NotThrow();
        viewModel.Invoking(vm => vm.ShowPluginManagerCommand.Execute(null)).Should().NotThrow();
    }

    [Fact]
    public void SelectedProjectItem_ShouldBeSettable()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var item = new ProjectTreeItemViewModel { Name = "Test Item" };

        // Act
        viewModel.SelectedProjectItem = item;

        // Assert
        viewModel.SelectedProjectItem.Should().Be(item);
    }

    [Fact]
    public void ProjectTreeItems_ShouldAllowAddingItems()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var initialCount = viewModel.ProjectTreeItems.Count;
        var newItem = new ProjectTreeItemViewModel { Name = "New Item" };

        // Act
        viewModel.ProjectTreeItems.Add(newItem);

        // Assert
        viewModel.ProjectTreeItems.Should().HaveCount(initialCount + 1);
        viewModel.ProjectTreeItems.Should().Contain(newItem);
    }
}
