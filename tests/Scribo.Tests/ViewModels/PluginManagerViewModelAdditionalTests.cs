using FluentAssertions;
using Scribo.Models;
using Scribo.Services;
using Scribo.ViewModels;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Scribo.Tests.ViewModels;

public class PluginManagerViewModelAdditionalTests
{
    [Fact]
    public void EnablePlugin_ShouldNotThrowWhenNoPluginSelected()
    {
        // Arrange
        var pluginManager = new PluginManager();
        var viewModel = new PluginManagerViewModel(pluginManager);
        viewModel.SelectedPlugin = null;

        // Act
        var act = () => viewModel.EnablePluginCommand.Execute(null);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void DisablePlugin_ShouldNotThrowWhenNoPluginSelected()
    {
        // Arrange
        var pluginManager = new PluginManager();
        var viewModel = new PluginManagerViewModel(pluginManager);
        viewModel.SelectedPlugin = null;

        // Act
        var act = () => viewModel.DisablePluginCommand.Execute(null);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RemovePlugin_ShouldNotThrowWhenNoPluginSelected()
    {
        // Arrange
        var pluginManager = new PluginManager();
        var viewModel = new PluginManagerViewModel(pluginManager);
        viewModel.SelectedPlugin = null;

        // Act
        var act = () => viewModel.RemovePluginCommand.Execute(null);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Refresh_ShouldUpdatePluginsList()
    {
        // Arrange
        var pluginManager = new PluginManager();
        var viewModel = new PluginManagerViewModel(pluginManager);
        var initialCount = viewModel.Plugins.Count;

        // Act
        viewModel.RefreshCommand.Execute(null);

        // Assert
        viewModel.Plugins.Should().HaveCount(initialCount);
        viewModel.StatusMessage.Should().Be("Plugins refreshed");
    }

    [Fact]
    public void InstallPlugin_ShouldUpdateStatusMessage()
    {
        // Arrange
        var pluginManager = new PluginManager();
        var viewModel = new PluginManagerViewModel(pluginManager);

        // Act
        viewModel.InstallPluginCommand.Execute(null);

        // Assert
        viewModel.StatusMessage.Should().NotBeEmpty();
    }
}
