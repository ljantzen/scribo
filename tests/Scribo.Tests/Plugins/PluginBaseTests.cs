using FluentAssertions;
using Scribo.Plugins;
using Scribo.Services;
using Xunit;

namespace Scribo.Tests.Plugins;

public class PluginBaseTests
{
    [Fact]
    public void Initialize_ShouldSetContext()
    {
        // Arrange
        var plugin = new TestPlugin();
        var context = new PluginContext();

        // Act
        plugin.Initialize(context);

        // Assert
        plugin.Context.Should().Be(context);
    }

    [Fact]
    public void OnEnabled_ShouldNotThrow()
    {
        // Arrange
        var plugin = new TestPlugin();

        // Act
        var act = () => plugin.OnEnabled();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void OnDisabled_ShouldNotThrow()
    {
        // Arrange
        var plugin = new TestPlugin();

        // Act
        var act = () => plugin.OnDisabled();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Shutdown_ShouldNotThrow()
    {
        // Arrange
        var plugin = new TestPlugin();

        // Act
        var act = () => plugin.Shutdown();

        // Assert
        act.Should().NotThrow();
    }
}

public class TestPlugin : PluginBase
{
    public new IPluginContext? Context => base.Context;

    public override string Id => "test-plugin";
    public override string Name => "Test Plugin";
    public override string Version => "1.0.0";
    public override string Description => "A test plugin";
    public override string Author => "Test Author";
}
