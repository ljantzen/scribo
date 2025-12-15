using FluentAssertions;
using Scribo.ViewModels;
using Xunit;

namespace Scribo.Tests.ViewModels;

public class MainWindowViewModelTests
{
    [Fact]
    public void MainWindowViewModel_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var viewModel = new MainWindowViewModel();

        // Assert
        viewModel.EditorText.Should().BeEmpty();
        viewModel.ProjectTreeItems.Should().NotBeNull();
        viewModel.IsProjectTreeOnLeft.Should().BeTrue();
        viewModel.IsProjectTreeOnRight.Should().BeFalse();
    }

    [Fact]
    public void MainWindowViewModel_ShouldInitializeProjectTree()
    {
        // Arrange & Act
        var viewModel = new MainWindowViewModel();

        // Assert
        viewModel.ProjectTreeItems.Should().NotBeEmpty();
        viewModel.ProjectTreeItems.Should().HaveCount(1);
        viewModel.ProjectTreeItems[0].Name.Should().Be("My Writing Project");
    }

    [Fact]
    public void ToggleProjectTreeSide_ShouldSwitchFromLeftToRight()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        viewModel.IsProjectTreeOnLeft = true;
        viewModel.IsProjectTreeOnRight = false;

        // Act
        viewModel.ToggleProjectTreeSideCommand.Execute(null);

        // Assert
        viewModel.IsProjectTreeOnLeft.Should().BeFalse();
        viewModel.IsProjectTreeOnRight.Should().BeTrue();
    }

    [Fact]
    public void ToggleProjectTreeSide_ShouldSwitchFromRightToLeft()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        viewModel.IsProjectTreeOnLeft = false;
        viewModel.IsProjectTreeOnRight = true;

        // Act
        viewModel.ToggleProjectTreeSideCommand.Execute(null);

        // Assert
        viewModel.IsProjectTreeOnLeft.Should().BeTrue();
        viewModel.IsProjectTreeOnRight.Should().BeFalse();
    }

    [Fact]
    public void EditorText_ShouldBeSettable()
    {
        // Arrange
        var viewModel = new MainWindowViewModel();
        var testText = "This is test content";

        // Act
        viewModel.EditorText = testText;

        // Assert
        viewModel.EditorText.Should().Be(testText);
    }
}
