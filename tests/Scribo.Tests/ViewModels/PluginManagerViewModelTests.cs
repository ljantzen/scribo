using FluentAssertions;
using Scribo.Services;
using Scribo.ViewModels;
using Xunit;

namespace Scribo.Tests.ViewModels;

public class PluginManagerViewModelTests
{
    [Fact]
    public void PluginManagerViewModel_ShouldInitializeWithEmptyPlugins()
    {
        // Arrange
        var pluginManager = new PluginManager();

        // Act
        var viewModel = new PluginManagerViewModel(pluginManager);

        // Assert
        viewModel.Plugins.Should().NotBeNull();
        viewModel.StatusMessage.Should().BeEmpty();
        viewModel.HasSelectedPlugin.Should().BeFalse();
    }

    [Fact]
    public void HasSelectedPlugin_ShouldReturnFalseWhenNoPluginSelected()
    {
        // Arrange
        var pluginManager = new PluginManager();
        var viewModel = new PluginManagerViewModel(pluginManager);

        // Act & Assert
        viewModel.HasSelectedPlugin.Should().BeFalse();
    }

    [Fact]
    public void RefreshCommand_ShouldUpdateStatusMessage()
    {
        // Arrange
        var pluginManager = new PluginManager();
        var viewModel = new PluginManagerViewModel(pluginManager);

        // Act
        viewModel.RefreshCommand.Execute(null);

        // Assert
        viewModel.StatusMessage.Should().Be("Plugins refreshed");
    }
}
