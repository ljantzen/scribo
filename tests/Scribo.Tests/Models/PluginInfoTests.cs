using FluentAssertions;
using Scribo.Models;
using Xunit;

namespace Scribo.Tests.Models;

public class PluginInfoTests
{
    [Fact]
    public void PluginInfo_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var pluginInfo = new PluginInfo();

        // Assert
        pluginInfo.Id.Should().BeEmpty();
        pluginInfo.Name.Should().BeEmpty();
        pluginInfo.Version.Should().BeEmpty();
        pluginInfo.Description.Should().BeEmpty();
        pluginInfo.Author.Should().BeEmpty();
        pluginInfo.FilePath.Should().BeEmpty();
        pluginInfo.IsEnabled.Should().BeTrue();
        pluginInfo.IsInstalled.Should().BeTrue();
    }

    [Fact]
    public void PluginInfo_ShouldSetInstalledAt()
    {
        // Arrange
        var beforeCreation = DateTime.Now;

        // Act
        var pluginInfo = new PluginInfo();
        var afterCreation = DateTime.Now;

        // Assert
        pluginInfo.InstalledAt.Should().BeAfter(beforeCreation.AddSeconds(-1));
        pluginInfo.InstalledAt.Should().BeBefore(afterCreation.AddSeconds(1));
    }

    [Fact]
    public void PluginInfo_ShouldAllowSettingProperties()
    {
        // Arrange
        var pluginInfo = new PluginInfo();

        // Act
        pluginInfo.Id = "test-plugin";
        pluginInfo.Name = "Test Plugin";
        pluginInfo.Version = "1.0.0";
        pluginInfo.Description = "A test plugin";
        pluginInfo.Author = "Test Author";
        pluginInfo.IsEnabled = false;

        // Assert
        pluginInfo.Id.Should().Be("test-plugin");
        pluginInfo.Name.Should().Be("Test Plugin");
        pluginInfo.Version.Should().Be("1.0.0");
        pluginInfo.Description.Should().Be("A test plugin");
        pluginInfo.Author.Should().Be("Test Author");
        pluginInfo.IsEnabled.Should().BeFalse();
    }
}
