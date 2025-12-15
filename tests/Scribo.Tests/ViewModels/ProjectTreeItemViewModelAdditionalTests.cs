using FluentAssertions;
using Scribo.ViewModels;
using Xunit;

namespace Scribo.Tests.ViewModels;

public class ProjectTreeItemViewModelAdditionalTests
{
    [Fact]
    public void ProjectTreeItemViewModel_ShouldAllowSettingName()
    {
        // Arrange
        var item = new ProjectTreeItemViewModel();

        // Act
        item.Name = "New Name";

        // Assert
        item.Name.Should().Be("New Name");
    }

    [Fact]
    public void ProjectTreeItemViewModel_ShouldAllowSettingIcon()
    {
        // Arrange
        var item = new ProjectTreeItemViewModel();

        // Act
        item.Icon = "📁";

        // Assert
        item.Icon.Should().Be("📁");
    }

    [Fact]
    public void ProjectTreeItemViewModel_ShouldAllowNestedChildren()
    {
        // Arrange
        var parent = new ProjectTreeItemViewModel { Name = "Parent" };
        var child1 = new ProjectTreeItemViewModel { Name = "Child 1" };
        var child2 = new ProjectTreeItemViewModel { Name = "Child 2" };
        var grandchild = new ProjectTreeItemViewModel { Name = "Grandchild" };

        // Act
        parent.Children.Add(child1);
        parent.Children.Add(child2);
        child1.Children.Add(grandchild);

        // Assert
        parent.Children.Should().HaveCount(2);
        child1.Children.Should().HaveCount(1);
        child1.Children[0].Should().Be(grandchild);
    }

    [Fact]
    public void ProjectTreeItemViewModel_ShouldAllowRemovingChildren()
    {
        // Arrange
        var parent = new ProjectTreeItemViewModel { Name = "Parent" };
        var child = new ProjectTreeItemViewModel { Name = "Child" };
        parent.Children.Add(child);

        // Act
        parent.Children.Remove(child);

        // Assert
        parent.Children.Should().BeEmpty();
    }

    [Fact]
    public void ProjectTreeItemViewModel_ShouldAllowClearingChildren()
    {
        // Arrange
        var parent = new ProjectTreeItemViewModel { Name = "Parent" };
        parent.Children.Add(new ProjectTreeItemViewModel { Name = "Child 1" });
        parent.Children.Add(new ProjectTreeItemViewModel { Name = "Child 2" });

        // Act
        parent.Children.Clear();

        // Assert
        parent.Children.Should().BeEmpty();
    }
}
