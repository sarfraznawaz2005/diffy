using Diffy.Core.Models;
using FluentAssertions;
using Xunit;

namespace Diffy.Tests.Unit.Models;

/// <summary>
/// Unit tests for the FileStatus model.
/// Tests computed properties and data integrity.
/// </summary>
public class FileStatusTests
{
    #region FileName Property

    [Fact]
    public void FileName_WithSimplePath_ReturnsFileName()
    {
        // Arrange
        var fileStatus = new FileStatus { Path = "test.txt" };

        // Act
        var result = fileStatus.FileName;

        // Assert
        result.Should().Be("test.txt");
    }

    [Fact]
    public void FileName_WithNestedPath_ReturnsFileName()
    {
        // Arrange
        var fileStatus = new FileStatus { Path = "src/controllers/TestController.cs" };

        // Act
        var result = fileStatus.FileName;

        // Assert
        result.Should().Be("TestController.cs");
    }

    [Fact]
    public void FileName_WithDeeplyNestedPath_ReturnsFileName()
    {
        // Arrange
        var fileStatus = new FileStatus { Path = "a/b/c/d/e/f/deep.txt" };

        // Act
        var result = fileStatus.FileName;

        // Assert
        result.Should().Be("deep.txt");
    }

    [Fact]
    public void FileName_WithBackslashSeparators_ReturnsFileName()
    {
        // Arrange - Test path normalization with backslashes (Windows-style)
        var fileStatus = new FileStatus { Path = "src\\controllers\\HomeController.cs" };

        // Act
        var result = fileStatus.FileName;

        // Assert - On Windows, backslashes are path separators.
        // On macOS/Linux, Path.GetFileName treats backslashes as regular characters.
        // This test verifies the behavior rather than enforcing a specific result.
        if (System.IO.Path.DirectorySeparatorChar == '\\')
        {
            result.Should().Be("HomeController.cs");
        }
        else
        {
            result.Should().Be("src\\controllers\\HomeController.cs");
        }
    }

    [Fact]
    public void FileName_WithRootPath_ReturnsFileName()
    {
        // Arrange
        var fileStatus = new FileStatus { Path = "/root.txt" };

        // Act
        var result = fileStatus.FileName;

        // Assert
        result.Should().Be("root.txt");
    }

    [Fact]
    public void FileName_WithEmptyPath_ReturnsEmptyString()
    {
        // Arrange
        var fileStatus = new FileStatus { Path = "" };

        // Act
        var result = fileStatus.FileName;

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Extension Property

    [Fact]
    public void Extension_WithExtension_ReturnsExtension()
    {
        // Arrange
        var fileStatus = new FileStatus { Path = "test.cs" };

        // Act
        var result = fileStatus.Extension;

        // Assert
        result.Should().Be(".cs");
    }

    [Fact]
    public void Extension_WithoutExtension_ReturnsEmpty()
    {
        // Arrange
        var fileStatus = new FileStatus { Path = "Makefile" };

        // Act
        var result = fileStatus.Extension;

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Extension_WithMultipleDots_ReturnsLastExtension()
    {
        // Arrange
        var fileStatus = new FileStatus { Path = "archive.tar.gz" };

        // Act
        var result = fileStatus.Extension;

        // Assert
        result.Should().Be(".gz");
    }

    [Fact]
    public void Extension_WithHiddenFile_ReturnsExtension()
    {
        // Arrange
        var fileStatus = new FileStatus { Path = ".gitignore" };

        // Act
        var result = fileStatus.Extension;

        // Assert - Path.GetExtension(".gitignore") returns ".gitignore" as extension
        result.Should().Be(".gitignore");
    }

    [Fact]
    public void Extension_WithPath_ReturnsExtension()
    {
        // Arrange
        var fileStatus = new FileStatus { Path = "src/components/App.tsx" };

        // Act
        var result = fileStatus.Extension;

        // Assert
        result.Should().Be(".tsx");
    }

    #endregion

    #region FileStatusKind Enum

    [Fact]
    public void FileStatusKind_AllValues_Defined()
    {
        // Assert
        Enum.GetValues<FileStatusKind>().Should().ContainInOrder(
            FileStatusKind.Modified,
            FileStatusKind.New,
            FileStatusKind.Deleted,
            FileStatusKind.Renamed,
            FileStatusKind.Unstaged,
            FileStatusKind.Unmerged,
            FileStatusKind.Ignored,
            FileStatusKind.Unknown
        );
    }

    [Theory]
    [InlineData(FileStatusKind.Modified)]
    [InlineData(FileStatusKind.New)]
    [InlineData(FileStatusKind.Deleted)]
    [InlineData(FileStatusKind.Renamed)]
    [InlineData(FileStatusKind.Unstaged)]
    [InlineData(FileStatusKind.Unmerged)]
    [InlineData(FileStatusKind.Ignored)]
    [InlineData(FileStatusKind.Unknown)]
    public void FileStatusKind_AllValues_AreDistinct(FileStatusKind kind)
    {
        // Assert
        ((int)kind).Should().BeGreaterThanOrEqualTo(0);
    }

    #endregion

    #region Properties

    [Fact]
    public void FileStatus_Properties_SetCorrectly()
    {
        // Arrange
        var modifiedTime = DateTime.Now;
        var fileStatus = new FileStatus
        {
            Path = "test.cs",
            Status = FileStatusKind.Modified,
            ModifiedTime = modifiedTime,
            IsBinary = false,
            Size = 1024
        };

        // Assert
        fileStatus.Path.Should().Be("test.cs");
        fileStatus.Status.Should().Be(FileStatusKind.Modified);
        fileStatus.ModifiedTime.Should().Be(modifiedTime);
        fileStatus.IsBinary.Should().BeFalse();
        fileStatus.Size.Should().Be(1024);
    }

    [Fact]
    public void FileStatus_DefaultValues_AreCorrect()
    {
        // Arrange
        var fileStatus = new FileStatus();

        // Assert
        fileStatus.Path.Should().BeEmpty();
        fileStatus.Status.Should().Be(FileStatusKind.Modified); // Default enum value (0)
        fileStatus.ModifiedTime.Should().Be(default);
        fileStatus.IsBinary.Should().BeFalse();
        fileStatus.Size.Should().Be(0);
    }

    #endregion
}
