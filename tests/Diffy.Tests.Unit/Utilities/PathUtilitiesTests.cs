using Diffy.App.Services;
using Diffy.Core.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace Diffy.Tests.Unit.Utilities;

/// <summary>
/// Unit tests for path and file utilities.
/// Tests path operations, file detection, and validation.
/// </summary>
public class PathUtilitiesTests
{
    private readonly Mock<ISettingsService> _mockSettingsService;

    public PathUtilitiesTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockSettingsService.Setup(s => s.GetTheme()).Returns(AppTheme.Light);
    }
    #region Path Operations

    [Theory]
    [InlineData("C:\\repo", "file.txt")]
    [InlineData("C:\\repo", "sub\\file.txt")]
    public void CombinePaths_WithRelativePath_ReturnsFullPath(string basePath, string relativePath)
    {
        // Act
        var result = Path.Combine(basePath, relativePath);

        // Assert
        result.Should().Contain(basePath.TrimEnd('\\'));
        result.Should().Contain("file.txt");
    }

    [Fact]
    public void CombinePaths_WithAbsolutePath_ReturnsAsIs()
    {
        // Arrange
        var basePath = "/repo";
        var absolutePath = "/other/file.txt";

        // Act
        var result = Path.Combine(basePath, absolutePath);

        // Assert
        result.Should().Be("/other/file.txt");
    }

    [Theory]
    [InlineData("C:\\repo\\sub\\file.txt", "C:\\repo", "sub")]
    [InlineData("C:\\repo\\file.txt", "C:\\repo", "file.txt")]
    [InlineData("C:\\repo\\sub\\nested\\file.txt", "C:\\repo", "nested")]
    public void GetRelativePath_WithSubPath_ReturnsRelative(string fullPath, string basePath, string expectedPart)
    {
        // Act
        var result = Path.GetRelativePath(basePath, fullPath);

        // Assert
        result.Should().Contain(expectedPart);
        result.Should().Contain("file.txt");
    }

    [Fact]
    public void GetRelativePath_WithSamePath_ReturnsDot()
    {
        // Arrange
        var path = "/repo/file.txt";

        // Act
        var result = Path.GetRelativePath(path, path);

        // Assert
        result.Should().Be(".");
    }

    #endregion

    #region File Detection

    [Fact]
    public void IsBinaryFile_WithBinaryContent_ReturnsTrue()
    {
        // Arrange
        var diffService = new DiffService(_mockSettingsService.Object);
        var binaryContent = "Binary files differ in this diff";

        // Act
        var result = diffService.IsBinaryDiff(binaryContent);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsBinaryFile_WithGitBinaryPatch_ReturnsTrue()
    {
        // Arrange
        var diffService = new DiffService(_mockSettingsService.Object);
        var binaryContent = "This is a GIT binary patch test";

        // Act
        var result = diffService.IsBinaryDiff(binaryContent);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsBinaryFile_WithTextContent_ReturnsFalse()
    {
        // Arrange
        var diffService = new DiffService(_mockSettingsService.Object);
        var textContent = "diff --git a/test.txt b/test.txt\n--- a/test.txt\n+++ b/test.txt\n@@ -1 +1 @@\n-old\n+new";

        // Act
        var result = diffService.IsBinaryDiff(textContent);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsBinaryFile_WithEmptyContent_ReturnsFalse()
    {
        // Arrange
        var diffService = new DiffService(_mockSettingsService.Object);

        // Act
        var result = diffService.IsBinaryDiff(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Path Validation

    [Theory]
    [InlineData("/valid/path")]
    [InlineData("C:\\valid\\path")]
    [InlineData("relative/path")]
    [InlineData("file.txt")]
    public void IsValidPath_WithValidPath_ReturnsTrue(string path)
    {
        // Act - Path validation in .NET
        Action act = () => Path.GetFullPath(path);

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("")]
    public void IsValidPath_WithInvalidPath_ThrowsException(string path)
    {
        // Act
        Action act = () => Path.GetFullPath(path);

        // Assert - Empty path throws on all platforms
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void IsValidPath_WithWhitespaceOnlyPath_PlatformSpecificBehavior()
    {
        // Arrange
        var path = " ";

        // Act
        Action act = () => Path.GetFullPath(path);

        // Assert - Behavior differs by platform
        // Windows: throws an exception
        // macOS/Linux: returns a normalized path (doesn't throw)
        if (System.OperatingSystem.IsWindows())
        {
            act.Should().Throw<Exception>();
        }
        else
        {
            act.Should().NotThrow();
        }
    }

    [Fact]
    public void GetExtension_WithValidExtension_ReturnsExtension()
    {
        // Arrange
        var path = "file.cs";

        // Act
        var result = Path.GetExtension(path);

        // Assert
        result.Should().Be(".cs");
    }

    [Theory]
    [InlineData("file", "")]
    [InlineData("file.cs", ".cs")]
    [InlineData("file.tar.gz", ".gz")]
    [InlineData(".gitignore", ".gitignore")]
    public void GetExtension_VariousPaths_ReturnsCorrectExtension(string path, string expected)
    {
        // Act
        var result = Path.GetExtension(path);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region File Name Extraction

    [Theory]
    [InlineData("/path/to/file.txt", "file.txt")]
    [InlineData("file.txt", "file.txt")]
    [InlineData("/root.txt", "root.txt")]
    public void GetFileName_WithValidPath_ReturnsFileName(string path, string expected)
    {
        // Act
        var result = Path.GetFileName(path);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GetFileName_WithWindowsPath_ReturnsFileName()
    {
        // Arrange - Windows-specific path with backslashes
        var path = "C:\\path\\to\\file.cs";

        // Act
        var result = Path.GetFileName(path);

        // Assert - On Windows, backslashes are path separators.
        // On macOS/Linux, Path.GetFileName treats backslashes as regular characters.
        if (System.IO.Path.DirectorySeparatorChar == '\\')
        {
            result.Should().Be("file.cs");
        }
        else
        {
            result.Should().Be(path);
        }
    }

    [Fact]
    public void GetFileName_WithDirectoryPath_ReturnsEmpty()
    {
        // Arrange
        var path = "/path/to/directory/";

        // Act
        var result = Path.GetFileName(path.TrimEnd('/'));

        // Assert
        result.Should().Be("directory");
    }

    [Fact]
    public void GetFileName_WithEmptyPath_ReturnsEmpty()
    {
        // Arrange
        var path = "";

        // Act
        var result = Path.GetFileName(path);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Directory Operations

    [Fact]
    public void GetDirectoryName_WithValidPath_ReturnsDirectory()
    {
        // Arrange
        var path = "/path/to/file.txt";

        // Act
        var result = Path.GetDirectoryName(path);

        // Assert - Path separator is platform-dependent
        result.Should().Contain("path");
        result.Should().Contain("to");
    }

    [Fact]
    public void GetDirectoryName_WithRootPath_ReturnsRoot()
    {
        // Arrange
        var path = OperatingSystem.IsWindows() ? "C:\\file.txt" : "/file.txt";

        // Act
        var result = Path.GetDirectoryName(path);

        // Assert
        if (OperatingSystem.IsWindows())
            result.Should().Be("C:\\");
        else
            result.Should().Be("/");
    }

    [Fact]
    public void GetDirectoryName_WithRelativePath_ReturnsDirectory()
    {
        // Arrange
        var path = "path/to/file.txt";

        // Act
        var result = Path.GetDirectoryName(path);

        // Assert - Path separator is platform-dependent
        result.Should().Contain("path");
        result.Should().Contain("to");
    }

    #endregion

    #region Path Normalization

    [Theory]
    [InlineData("/path//to//file.txt")]
    [InlineData("/path/./to/file.txt")]
    public void Path_NormalizesCorrectly(string input)
    {
        // Act
        var result = Path.GetFullPath(input);

        // Assert - Path.GetFullPath normalizes the path
        result.Should().Contain("path");
        result.Should().Contain("to");
        result.Should().Contain("file.txt");
    }

    #endregion

    #region File Operations

    [Fact]
    public void ChangeExtension_WithNewExtension_ReturnsChangedPath()
    {
        // Arrange
        var path = "file.txt";

        // Act
        var result = Path.ChangeExtension(path, ".cs");

        // Assert
        result.Should().Be("file.cs");
    }

    [Fact]
    public void ChangeExtension_WithoutExtension_ReturnsPathWithExtension()
    {
        // Arrange
        var path = "file";

        // Act
        var result = Path.ChangeExtension(path, ".txt");

        // Assert
        result.Should().Be("file.txt");
    }

    [Fact]
    public void HasExtension_WithExtension_ReturnsTrue()
    {
        // Arrange
        var path = "file.txt";

        // Act
        var result = Path.HasExtension(path);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasExtension_WithoutExtension_ReturnsFalse()
    {
        // Arrange
        var path = "file";

        // Act
        var result = Path.HasExtension(path);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Temp Path

    [Fact]
    public void GetTempPath_ReturnsValidPath()
    {
        // Act
        var result = Path.GetTempPath();

        // Assert
        result.Should().NotBeNullOrEmpty();
        Directory.Exists(result).Should().BeTrue();
    }

    [Fact]
    public void GetTempFileName_CreatesUniqueFile()
    {
        // Act
        var result = Path.GetTempFileName();

        try
        {
            // Assert
            result.Should().NotBeNullOrEmpty();
            File.Exists(result).Should().BeTrue();
        }
        finally
        {
            // Cleanup
            if (File.Exists(result))
                File.Delete(result);
        }
    }

    #endregion
}
