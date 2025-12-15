using FluentAssertions;
using Scribo.Services;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Scribo.Tests.Services;

public class FileServiceIntegrationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileService _fileService;

    public FileServiceIntegrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _fileService = new FileService();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task SaveAndLoadFileAsync_ShouldPreserveContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        var originalContent = "This is test content\nWith multiple lines\nAnd special chars: !@#$%";

        // Act
        await _fileService.SaveFileAsync(filePath, originalContent);
        var loadedContent = await _fileService.LoadFileAsync(filePath);

        // Assert
        loadedContent.Should().Be(originalContent);
    }

    [Fact]
    public void SaveAndLoadFile_ShouldPreserveContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        var originalContent = "This is test content\nWith multiple lines\nAnd special chars: !@#$%";

        // Act
        _fileService.SaveFile(filePath, originalContent);
        var loadedContent = _fileService.LoadFile(filePath);

        // Assert
        loadedContent.Should().Be(originalContent);
    }

    [Fact]
    public async Task SaveFileAsync_ShouldHandleLargeContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "large.txt");
        var largeContent = string.Join("\n", Enumerable.Range(1, 10000).Select(i => $"Line {i}"));

        // Act
        await _fileService.SaveFileAsync(filePath, largeContent);
        var loadedContent = await _fileService.LoadFileAsync(filePath);

        // Assert
        loadedContent.Should().Be(largeContent);
        loadedContent.Split('\n').Length.Should().Be(10000);
    }

    [Fact]
    public async Task SaveFileAsync_ShouldHandleEmptyContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "empty.txt");

        // Act
        await _fileService.SaveFileAsync(filePath, string.Empty);
        var loadedContent = await _fileService.LoadFileAsync(filePath);

        // Assert
        loadedContent.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleSaveOperations_ShouldWorkCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");

        // Act
        await _fileService.SaveFileAsync(filePath, "First content");
        var firstLoad = await _fileService.LoadFileAsync(filePath);
        
        await _fileService.SaveFileAsync(filePath, "Second content");
        var secondLoad = await _fileService.LoadFileAsync(filePath);

        // Assert
        firstLoad.Should().Be("First content");
        secondLoad.Should().Be("Second content");
    }

    [Fact]
    public void SaveFile_ShouldHandleNestedDirectories()
    {
        // Arrange
        var nestedPath = Path.Combine(_testDirectory, "level1", "level2", "level3", "test.txt");
        var content = "Nested content";

        // Act
        _fileService.SaveFile(nestedPath, content);
        var loaded = _fileService.LoadFile(nestedPath);

        // Assert
        loaded.Should().Be(content);
        File.Exists(nestedPath).Should().BeTrue();
    }
}
