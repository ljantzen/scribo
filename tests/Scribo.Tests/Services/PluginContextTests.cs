using FluentAssertions;
using Scribo.Plugins;
using Scribo.Services;
using Xunit;

namespace Scribo.Tests.Services;

public class PluginContextTests
{
    [Fact]
    public void MainWindow_ShouldReturnNullInitially()
    {
        // Arrange
        var context = new PluginContext();

        // Act & Assert
        context.MainWindow.Should().BeNull();
    }

    [Fact]
    public void SetMainWindow_ShouldSetMainWindow()
    {
        // Arrange
        var context = new PluginContext();
        var mainWindow = new object();

        // Act
        context.SetMainWindow(mainWindow);

        // Assert
        context.MainWindow.Should().Be(mainWindow);
    }

    [Fact]
    public void RegisterService_ShouldStoreService()
    {
        // Arrange
        var context = new PluginContext();
        var service = new TestService();

        // Act
        context.RegisterService(service);

        // Assert
        var retrieved = context.GetService<TestService>();
        retrieved.Should().Be(service);
    }

    [Fact]
    public void GetService_ShouldReturnNullWhenServiceNotRegistered()
    {
        // Arrange
        var context = new PluginContext();

        // Act
        var service = context.GetService<TestService>();

        // Assert
        service.Should().BeNull();
    }

    [Fact]
    public void RegisterService_ShouldOverwriteExistingService()
    {
        // Arrange
        var context = new PluginContext();
        var service1 = new TestService { Value = "First" };
        var service2 = new TestService { Value = "Second" };

        // Act
        context.RegisterService(service1);
        context.RegisterService(service2);

        // Assert
        var retrieved = context.GetService<TestService>();
        retrieved.Should().Be(service2);
        retrieved!.Value.Should().Be("Second");
    }

    [Fact]
    public void RegisterMenuItem_ShouldNotThrow()
    {
        // Arrange
        var context = new PluginContext();

        // Act
        var act = () => context.RegisterMenuItem("Tools", "Test", () => { });

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Log_ShouldNotThrow()
    {
        // Arrange
        var context = new PluginContext();

        // Act
        var act = () => context.Log("Test message");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Log_ShouldHandleAllLogLevels()
    {
        // Arrange
        var context = new PluginContext();

        // Act & Assert
        context.Invoking(c => c.Log("Debug", LogLevel.Debug)).Should().NotThrow();
        context.Invoking(c => c.Log("Info", LogLevel.Info)).Should().NotThrow();
        context.Invoking(c => c.Log("Warning", LogLevel.Warning)).Should().NotThrow();
        context.Invoking(c => c.Log("Error", LogLevel.Error)).Should().NotThrow();
    }
}

public class TestService
{
    public string Value { get; set; } = string.Empty;
}
