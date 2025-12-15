using FluentAssertions;
using Scribo.Services;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Scribo.Tests.Services;

public class FileServiceTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly FileService _fileService;

    public FileServiceTests()
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
    public void SaveFile_ShouldCreateFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        var content = "Hello, World!";

        // Act
        _fileService.SaveFile(filePath, content);

        // Assert
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public void SaveFile_ShouldWriteContentCorrectly()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        var content = "Hello, World!\nThis is a test.";

        // Act
        _fileService.SaveFile(filePath, content);

        // Assert
        var readContent = File.ReadAllText(filePath, Encoding.UTF8);
        readContent.Should().Be(content);
    }

    [Fact]
    public void SaveFile_ShouldCreateDirectoryIfNotExists()
    {
        // Arrange
        var subDirectory = Path.Combine(_testDirectory, "subdir");
        var filePath = Path.Combine(subDirectory, "test.txt");
        var content = "Test content";

        // Act
        _fileService.SaveFile(filePath, content);

        // Assert
        Directory.Exists(subDirectory).Should().BeTrue();
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public void SaveFile_ShouldThrowWhenFilePathIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _fileService.SaveFile(null!, "content"));
    }

    [Fact]
    public void SaveFile_ShouldThrowWhenFilePathIsEmpty()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _fileService.SaveFile(string.Empty, "content"));
    }

    [Fact]
    public void SaveFile_ShouldThrowWhenContentIsNull()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _fileService.SaveFile(filePath, null!));
    }

    [Fact]
    public void LoadFile_ShouldReadFileContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        var content = "Hello, World!";
        File.WriteAllText(filePath, content, Encoding.UTF8);

        // Act
        var loadedContent = _fileService.LoadFile(filePath);

        // Assert
        loadedContent.Should().Be(content);
    }

    [Fact]
    public void LoadFile_ShouldThrowWhenFileNotFound()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => _fileService.LoadFile(filePath));
    }

    [Fact]
    public void LoadFile_ShouldThrowWhenFilePathIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _fileService.LoadFile(null!));
    }

    [Fact]
    public async Task SaveFileAsync_ShouldCreateFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        var content = "Hello, World!";

        // Act
        await _fileService.SaveFileAsync(filePath, content);

        // Assert
        File.Exists(filePath).Should().BeTrue();
    }

    [Fact]
    public async Task LoadFileAsync_ShouldReadFileContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        var content = "Hello, World!";
        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);

        // Act
        var loadedContent = await _fileService.LoadFileAsync(filePath);

        // Assert
        loadedContent.Should().Be(content);
    }

    [Fact]
    public void FileExists_ShouldReturnTrueForExistingFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(filePath, "test");

        // Act
        var exists = _fileService.FileExists(filePath);

        // Assert
        exists.Should().BeTrue();
    }

    [Fact]
    public void FileExists_ShouldReturnFalseForNonExistentFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "nonexistent.txt");

        // Act
        var exists = _fileService.FileExists(filePath);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public void FileExists_ShouldReturnFalseForNullPath()
    {
        // Act
        var exists = _fileService.FileExists(null!);

        // Assert
        exists.Should().BeFalse();
    }

    [Fact]
    public void GetFileName_ShouldReturnFileName()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");

        // Act
        var fileName = _fileService.GetFileName(filePath);

        // Assert
        fileName.Should().Be("test.txt");
    }

    [Fact]
    public void GetFileNameWithoutExtension_ShouldReturnFileNameWithoutExtension()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");

        // Act
        var fileName = _fileService.GetFileNameWithoutExtension(filePath);

        // Assert
        fileName.Should().Be("test");
    }

    [Fact]
    public void GetDirectoryName_ShouldReturnDirectoryName()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");

        // Act
        var directoryName = _fileService.GetDirectoryName(filePath);

        // Assert
        directoryName.Should().Be(_testDirectory);
    }

    [Fact]
    public void CombinePath_ShouldCombinePaths()
    {
        // Arrange
        var path1 = _testDirectory;
        var path2 = "subdir";
        var path3 = "file.txt";

        // Act
        var combined = _fileService.CombinePath(_fileService.CombinePath(path1, path2), path3);

        // Assert
        combined.Should().Be(Path.Combine(_testDirectory, "subdir", "file.txt"));
    }

    [Fact]
    public void SaveFile_ShouldHandleUnicodeContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        var content = "Hello 世界 مرحبا";

        // Act
        _fileService.SaveFile(filePath, content);
        var loaded = _fileService.LoadFile(filePath);

        // Assert
        loaded.Should().Be(content);
    }

    [Fact]
    public void SaveFile_ShouldHandleMultilineContent()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        var content = "Line 1\nLine 2\nLine 3";

        // Act
        _fileService.SaveFile(filePath, content);
        var loaded = _fileService.LoadFile(filePath);

        // Assert
        loaded.Should().Be(content);
    }

    [Fact]
    public void SaveFile_ShouldOverwriteExistingFile()
    {
        // Arrange
        var filePath = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(filePath, "Old content");

        // Act
        _fileService.SaveFile(filePath, "New content");

        // Assert
        var content = File.ReadAllText(filePath, Encoding.UTF8);
        content.Should().Be("New content");
    }
}
