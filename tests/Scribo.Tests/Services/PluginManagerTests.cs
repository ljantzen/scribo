using FluentAssertions;
using Scribo.Models;
using Scribo.Plugins;
using Scribo.Services;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Xunit;

namespace Scribo.Tests.Services;

public class PluginManagerTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly PluginManager _pluginManager;
    private readonly PluginContext _pluginContext;

    public PluginManagerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _pluginManager = new PluginManager(_testDirectory);
        _pluginContext = new PluginContext();
        _pluginManager.SetContext(_pluginContext);
    }

    public void Dispose()
    {
        _pluginManager.Dispose();
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void GetPlugins_ShouldReturnEmptyListInitially()
    {
        // Act
        var plugins = _pluginManager.GetPlugins();

        // Assert
        plugins.Should().BeEmpty();
    }

    [Fact]
    public void IsPluginLoaded_ShouldReturnFalseForNonExistentPlugin()
    {
        // Act
        var isLoaded = _pluginManager.IsPluginLoaded("non-existent");

        // Assert
        isLoaded.Should().BeFalse();
    }

    [Fact]
    public void IsPluginEnabled_ShouldReturnFalseForNonExistentPlugin()
    {
        // Act
        var isEnabled = _pluginManager.IsPluginEnabled("non-existent");

        // Assert
        isEnabled.Should().BeFalse();
    }

    [Fact]
    public void EnablePlugin_ShouldThrowWhenPluginNotFound()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _pluginManager.EnablePlugin("non-existent"));
    }

    [Fact]
    public void DisablePlugin_ShouldThrowWhenPluginNotFound()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _pluginManager.DisablePlugin("non-existent"));
    }

    [Fact]
    public void RemovePlugin_ShouldThrowWhenPluginNotFound()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _pluginManager.RemovePlugin("non-existent"));
    }

    [Fact]
    public void InstallPlugin_ShouldThrowWhenFileNotFound()
    {
        // Arrange
        var nonExistentFile = Path.Combine(_testDirectory, "non-existent.dll");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _pluginManager.InstallPlugin(nonExistentFile));
    }

    [Fact]
    public void LoadPlugins_ShouldThrowWhenContextNotSet()
    {
        // Arrange
        var manager = new PluginManager(_testDirectory);

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => manager.LoadPlugins());
    }

    [Fact]
    public void UnloadPlugin_ShouldNotThrowWhenPluginNotLoaded()
    {
        // Act
        var act = () => _pluginManager.UnloadPlugin("non-existent");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GetPluginInfo_ShouldReturnNullForNonExistentPlugin()
    {
        // Act
        var info = _pluginManager.GetPluginInfo("non-existent");

        // Assert
        info.Should().BeNull();
    }

    [Fact]
    public void Dispose_ShouldNotThrow()
    {
        // Act
        var act = () => _pluginManager.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void SetContext_ShouldSetContext()
    {
        // Arrange
        var manager = new PluginManager(_testDirectory);
        var context = new PluginContext();

        // Act
        manager.SetContext(context);

        // Assert
        // Context is set, LoadPlugins should not throw now
        manager.Invoking(m => m.LoadPlugins()).Should().NotThrow();
    }
}
