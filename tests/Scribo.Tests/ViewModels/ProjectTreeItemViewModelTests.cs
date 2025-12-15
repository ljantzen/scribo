using FluentAssertions;
using Scribo.ViewModels;
using Xunit;

namespace Scribo.Tests.ViewModels;

public class ProjectTreeItemViewModelTests
{
    [Fact]
    public void ProjectTreeItemViewModel_ShouldInitializeWithEmptyValues()
    {
        // Arrange & Act
        var item = new ProjectTreeItemViewModel();

        // Assert
        item.Name.Should().BeEmpty();
        item.Icon.Should().BeEmpty();
        item.Children.Should().NotBeNull();
        item.Children.Should().BeEmpty();
    }

    [Fact]
    public void ProjectTreeItemViewModel_ShouldAllowSettingProperties()
    {
        // Arrange
        var item = new ProjectTreeItemViewModel();

        // Act
        item.Name = "Test Item";
        item.Icon = "📄";

        // Assert
        item.Name.Should().Be("Test Item");
        item.Icon.Should().Be("📄");
    }

    [Fact]
    public void ProjectTreeItemViewModel_ShouldAllowAddingChildren()
    {
        // Arrange
        var parent = new ProjectTreeItemViewModel { Name = "Parent" };
        var child = new ProjectTreeItemViewModel { Name = "Child" };

        // Act
        parent.Children.Add(child);

        // Assert
        parent.Children.Should().Contain(child);
        parent.Children.Should().HaveCount(1);
    }
}
