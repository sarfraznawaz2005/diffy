using Diffy.Core.Models;
using FluentAssertions;
using Xunit;

namespace Diffy.Tests.Unit.Models;

/// <summary>
/// Unit tests for the DiffModels classes.
/// Tests diff data structures and calculations.
/// </summary>
public class DiffModelsTests
{
    #region DiffLine

    [Fact]
    public void DiffLine_Properties_SetCorrectly()
    {
        // Arrange
        var line = new DiffLine
        {
            OldLineNumber = 1,
            NewLineNumber = 1,
            Content = "test content",
            Kind = DiffLineKind.Unchanged,
            Highlights = new List<HighlightedSegment>()
        };

        // Assert
        line.OldLineNumber.Should().Be(1);
        line.NewLineNumber.Should().Be(1);
        line.Content.Should().Be("test content");
        line.Kind.Should().Be(DiffLineKind.Unchanged);
        line.Highlights.Should().NotBeNull();
    }

    [Fact]
    public void DiffLine_DefaultValues_AreCorrect()
    {
        // Arrange
        var line = new DiffLine();

        // Assert
        line.OldLineNumber.Should().Be(-1);
        line.NewLineNumber.Should().Be(-1);
        line.Content.Should().BeEmpty();
        line.Kind.Should().Be(DiffLineKind.Added); // Default enum value (0)
        line.Highlights.Should().BeNull();
    }

    #endregion

    #region DiffLineKind Enum

    [Fact]
    public void DiffLineKind_AllValues_Defined()
    {
        // Assert
        Enum.GetValues<DiffLineKind>().Should().ContainInOrder(
            DiffLineKind.Added,
            DiffLineKind.Removed,
            DiffLineKind.Unchanged,
            DiffLineKind.Context,
            DiffLineKind.Header,
            DiffLineKind.Placeholder
        );
    }

    [Theory]
    [InlineData(DiffLineKind.Added, 0)]
    [InlineData(DiffLineKind.Removed, 1)]
    [InlineData(DiffLineKind.Unchanged, 2)]
    [InlineData(DiffLineKind.Context, 3)]
    [InlineData(DiffLineKind.Header, 4)]
    [InlineData(DiffLineKind.Placeholder, 5)]
    public void DiffLineKind_Values_AreCorrect(DiffLineKind kind, int expectedValue)
    {
        // Assert
        ((int)kind).Should().Be(expectedValue);
    }

    #endregion

    #region FileDiff

    [Fact]
    public void FileDiff_Defaults_AreCorrect()
    {
        // Arrange
        var fileDiff = new FileDiff();

        // Assert
        fileDiff.FilePath.Should().BeEmpty();
        fileDiff.IsBinary.Should().BeFalse();
        fileDiff.Blocks.Should().NotBeNull();
        fileDiff.Blocks.Should().BeEmpty();
        fileDiff.InlineLines.Should().NotBeNull();
        fileDiff.InlineLines.Should().BeEmpty();
        fileDiff.AlignedRows.Should().NotBeNull();
        fileDiff.AlignedRows.Should().BeEmpty();
    }

    [Fact]
    public void FileDiff_Additions_CanBeSet()
    {
        // Arrange
        var fileDiff = new FileDiff { Additions = 5 };

        // Act & Assert
        fileDiff.Additions.Should().Be(5);
    }

    [Fact]
    public void FileDiff_Deletions_CanBeSet()
    {
        // Arrange
        var fileDiff = new FileDiff { Deletions = 3 };

        // Act & Assert
        fileDiff.Deletions.Should().Be(3);
    }

    [Fact]
    public void FileDiff_Additions_WithNoInlineLines_ReturnsZero()
    {
        // Arrange
        var fileDiff = new FileDiff();

        // Act & Assert
        fileDiff.Additions.Should().Be(0);
    }

    [Fact]
    public void FileDiff_Deletions_WithNoInlineLines_ReturnsZero()
    {
        // Arrange
        var fileDiff = new FileDiff();

        // Act & Assert
        fileDiff.Deletions.Should().Be(0);
    }

    [Fact]
    public void FileDiff_IsBinary_CanBeSet()
    {
        // Arrange
        var fileDiff = new FileDiff { IsBinary = true };

        // Assert
        fileDiff.IsBinary.Should().BeTrue();
    }

    [Fact]
    public void FileDiff_FilePath_CanBeSet()
    {
        // Arrange
        var fileDiff = new FileDiff { FilePath = "test.cs" };

        // Assert
        fileDiff.FilePath.Should().Be("test.cs");
    }

    #endregion

    #region DiffBlock

    [Fact]
    public void DiffBlock_Defaults_AreCorrect()
    {
        // Arrange
        var block = new DiffBlock();

        // Assert
        block.FilePath.Should().BeEmpty();
        block.OldLines.Should().NotBeNull();
        block.OldLines.Should().BeEmpty();
        block.NewLines.Should().NotBeNull();
        block.NewLines.Should().BeEmpty();
    }

    [Fact]
    public void DiffBlock_Properties_SetCorrectly()
    {
        // Arrange
        var block = new DiffBlock
        {
            FilePath = "test.cs",
            OldLines = new List<DiffLine> { new() { Content = "old" } },
            NewLines = new List<DiffLine> { new() { Content = "new" } }
        };

        // Assert
        block.FilePath.Should().Be("test.cs");
        block.OldLines.Should().HaveCount(1);
        block.NewLines.Should().HaveCount(1);
    }

    #endregion

    #region AlignedDiffRow

    [Fact]
    public void AlignedDiffRow_Properties_SetCorrectly()
    {
        // Arrange
        var oldLine = new DiffLine { Content = "old", Kind = DiffLineKind.Removed };
        var newLine = new DiffLine { Content = "new", Kind = DiffLineKind.Added };

        var row = new AlignedDiffRow
        {
            OldLine = oldLine,
            NewLine = newLine
        };

        // Assert
        row.OldLine.Should().Be(oldLine);
        row.NewLine.Should().Be(newLine);
    }

    [Fact]
    public void AlignedDiffRow_Defaults_AreInitialized()
    {
        // Arrange
        var row = new AlignedDiffRow();

        // Assert
        row.OldLine.Should().NotBeNull();
        row.NewLine.Should().NotBeNull();
        row.OldLine.Content.Should().BeEmpty();
        row.NewLine.Content.Should().BeEmpty();
    }

    #endregion

    #region HighlightedSegment

    [Fact]
    public void HighlightedSegment_Properties_SetCorrectly()
    {
        // Arrange
        var segment = new HighlightedSegment
        {
            Offset = 10,
            Length = 5,
            ColorHex = "#FF0000",
            BackgroundHex = "#FFFF00",
            IsBold = true,
            IsItalic = false
        };

        // Assert
        segment.Offset.Should().Be(10);
        segment.Length.Should().Be(5);
        segment.ColorHex.Should().Be("#FF0000");
        segment.BackgroundHex.Should().Be("#FFFF00");
        segment.IsBold.Should().BeTrue();
        segment.IsItalic.Should().BeFalse();
    }

    [Fact]
    public void HighlightedSegment_Defaults_AreCorrect()
    {
        // Arrange
        var segment = new HighlightedSegment();

        // Assert
        segment.Offset.Should().Be(0);
        segment.Length.Should().Be(0);
        segment.ColorHex.Should().BeNull();
        segment.BackgroundHex.Should().BeNull();
        segment.IsBold.Should().BeFalse();
        segment.IsItalic.Should().BeFalse();
    }

    #endregion

    #region FilterSettings

    [Fact]
    public void FilterSettings_Defaults_AreCorrect()
    {
        // Arrange
        var settings = new FilterSettings();

        // Assert
        settings.IncludedStatuses.Should().NotBeNull();
        settings.IncludedStatuses.Should().ContainInOrder(
            FileStatusKind.Modified,
            FileStatusKind.New,
            FileStatusKind.Deleted,
            FileStatusKind.Renamed,
            FileStatusKind.Unstaged
        );
        settings.IncludedExtensions.Should().NotBeNull();
        settings.IncludedExtensions.Should().BeEmpty();
        settings.ExcludedPatterns.Should().NotBeNull();
        settings.ExcludedPatterns.Should().ContainInOrder("**/bin/**", "**/obj/**", "**/.vs/**", "**/node_modules/**");
        settings.ShowBinaryFiles.Should().BeTrue();
    }

    [Fact]
    public void FilterSettings_IncludedStatuses_CanBeModified()
    {
        // Arrange
        var settings = new FilterSettings();

        // Act
        settings.IncludedStatuses.Add(FileStatusKind.Ignored);
        settings.IncludedStatuses.Remove(FileStatusKind.Unstaged);

        // Assert
        settings.IncludedStatuses.Should().Contain(FileStatusKind.Ignored);
        settings.IncludedStatuses.Should().NotContain(FileStatusKind.Unstaged);
    }

    [Fact]
    public void FilterSettings_IncludedExtensions_CanBeAdded()
    {
        // Arrange
        var settings = new FilterSettings();

        // Act
        settings.IncludedExtensions.Add(".cs");
        settings.IncludedExtensions.Add(".txt");

        // Assert
        settings.IncludedExtensions.Should().HaveCount(2);
        settings.IncludedExtensions.Should().Contain(".cs");
        settings.IncludedExtensions.Should().Contain(".txt");
    }

    [Fact]
    public void FilterSettings_ExcludedPatterns_CanBeModified()
    {
        // Arrange
        var settings = new FilterSettings();

        // Act
        settings.ExcludedPatterns.Add("*.tmp");
        settings.ExcludedPatterns.Remove("bin");

        // Assert
        settings.ExcludedPatterns.Should().Contain("*.tmp");
        settings.ExcludedPatterns.Should().NotContain("bin");
    }

    [Fact]
    public void FilterSettings_ShowBinaryFiles_CanBeChanged()
    {
        // Arrange
        var settings = new FilterSettings { ShowBinaryFiles = false };

        // Assert
        settings.ShowBinaryFiles.Should().BeFalse();
    }

    #endregion

    #region RepositoryInfo

    [Fact]
    public void RepositoryInfo_Properties_SetCorrectly()
    {
        // Arrange
        var lastUpdated = DateTime.Now;
        var files = new List<FileStatus> { new() { Path = "test.cs" } };

        var info = new RepositoryInfo
        {
            Path = "/repo/path",
            Name = "myrepo",
            CurrentBranch = "main",
            BranchCount = 5,
            IsWatching = true,
            LastUpdated = lastUpdated,
            Files = files
        };

        // Assert
        info.Path.Should().Be("/repo/path");
        info.Name.Should().Be("myrepo");
        info.CurrentBranch.Should().Be("main");
        info.BranchCount.Should().Be(5);
        info.IsWatching.Should().BeTrue();
        info.LastUpdated.Should().Be(lastUpdated);
        info.Files.Should().BeEquivalentTo(files);
    }

    [Fact]
    public void RepositoryInfo_Defaults_AreCorrect()
    {
        // Arrange
        var info = new RepositoryInfo();

        // Assert
        info.Path.Should().BeEmpty();
        info.Name.Should().BeEmpty();
        info.CurrentBranch.Should().BeEmpty();
        info.BranchCount.Should().Be(0);
        info.IsWatching.Should().BeFalse();
        info.LastUpdated.Should().Be(default);
        info.Files.Should().NotBeNull();
        info.Files.Should().BeEmpty();
    }

    #endregion

    #region CommitInfo

    [Fact]
    public void CommitInfo_Properties_SetCorrectly()
    {
        // Arrange
        var date = DateTime.Now;
        var files = new List<ChangedFile> { new() { Path = "test.cs" } };

        var commit = new CommitInfo
        {
            Hash = "abc1234",
            FullHash = "abc1234567890abcdef",
            Message = "Test commit message",
            Author = "John Doe",
            Date = date,
            Files = files
        };

        // Assert
        commit.Hash.Should().Be("abc1234");
        commit.FullHash.Should().Be("abc1234567890abcdef");
        commit.Message.Should().Be("Test commit message");
        commit.Author.Should().Be("John Doe");
        commit.Date.Should().Be(date);
        commit.Files.Should().BeEquivalentTo(files);
    }

    [Fact]
    public void CommitInfo_Defaults_AreCorrect()
    {
        // Arrange
        var commit = new CommitInfo();

        // Assert
        commit.Hash.Should().BeEmpty();
        commit.FullHash.Should().BeEmpty();
        commit.Message.Should().BeEmpty();
        commit.Author.Should().BeEmpty();
        commit.Date.Should().Be(default);
        commit.Files.Should().NotBeNull();
        commit.Files.Should().BeEmpty();
    }

    #endregion

    #region ChangedFile

    [Fact]
    public void ChangedFile_Properties_SetCorrectly()
    {
        // Arrange
        var file = new ChangedFile
        {
            Path = "test.cs",
            ChangeType = "modified"
        };

        // Assert
        file.Path.Should().Be("test.cs");
        file.ChangeType.Should().Be("modified");
    }

    [Fact]
    public void ChangedFile_Defaults_AreCorrect()
    {
        // Arrange
        var file = new ChangedFile();

        // Assert
        file.Path.Should().BeEmpty();
        file.ChangeType.Should().BeEmpty();
    }

    #endregion
}
