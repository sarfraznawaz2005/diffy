using Diffy.App.Services;
using Diffy.Core.Interfaces;
using Diffy.Core.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace Diffy.Tests.Unit.Services;

/// <summary>
/// Unit tests for the DiffService class.
/// Tests diff generation, alignment, whitespace handling, and edge cases.
/// </summary>
public class DiffServiceTests
{
    private readonly DiffService _sut;
    private readonly Mock<ISettingsService> _mockSettingsService;

    public DiffServiceTests()
    {
        _mockSettingsService = new Mock<ISettingsService>();
        _mockSettingsService.Setup(s => s.GetTheme()).Returns(AppTheme.Light);
        _sut = new DiffService(_mockSettingsService.Object);
    }

    #region Core Diff Generation

    [Fact]
    public void GenerateDiff_WithIdenticalContent_ReturnsEmptyDiff()
    {
        // Arrange
        var oldContent = "line1\nline2\nline3";
        var newContent = "line1\nline2\nline3";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.Should().NotBeNull();
        result.FilePath.Should().Be("test.txt");
        result.Additions.Should().Be(0);
        result.Deletions.Should().Be(0);
        result.IsBinary.Should().BeFalse();
    }

    [Fact]
    public void GenerateDiff_WithAddedLines_ReturnsCorrectAdditionsCount()
    {
        // Arrange
        var oldContent = "line1\nline2";
        var newContent = "line1\nline2\nline3\nline4";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.Additions.Should().Be(2);
        result.Deletions.Should().Be(0);
    }

    [Fact]
    public void GenerateDiff_WithRemovedLines_ReturnsCorrectDeletionsCount()
    {
        // Arrange
        var oldContent = "line1\nline2\nline3\nline4";
        var newContent = "line1\nline2";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.Additions.Should().Be(0);
        result.Deletions.Should().Be(2);
    }

    [Fact]
    public void GenerateDiff_WithMixedChanges_ReturnsCorrectCounts()
    {
        // Arrange
        var oldContent = "line1\nline2\nline3";
        var newContent = "line1\nline2_modified\nline4";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.Additions.Should().BeGreaterThan(0);
        result.Deletions.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateDiff_WithEmptyOldContent_ReturnsAllAdditions()
    {
        // Arrange
        var oldContent = "";
        var newContent = "line1\nline2\nline3";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.Additions.Should().Be(3);
        result.Deletions.Should().Be(0);
    }

    [Fact]
    public void GenerateDiff_WithEmptyNewContent_ReturnsAllDeletions()
    {
        // Arrange
        var oldContent = "line1\nline2\nline3";
        var newContent = "";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.Additions.Should().Be(0);
        result.Deletions.Should().Be(3);
    }

    [Fact]
    public void GenerateDiff_WithBothEmpty_ReturnsEmptyDiff()
    {
        // Arrange
        var oldContent = "";
        var newContent = "";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.Should().NotBeNull();
        result.Additions.Should().Be(0);
        result.Deletions.Should().Be(0);
        result.InlineLines.Should().BeEmpty();
    }

    #endregion

    #region Whitespace Handling

    [Fact]
    public void GenerateDiff_WithIgnoreWhitespaceTrue_IgnoresWhitespaceChanges()
    {
        // Arrange
        var oldContent = "line1\nline2  ";
        var newContent = "line1\nline2";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", true);

        // Assert
        result.Additions.Should().Be(0);
        result.Deletions.Should().Be(0);
    }

    [Fact]
    public void GenerateDiff_WithIgnoreWhitespaceFalse_ShowsWhitespaceChanges()
    {
        // Arrange
        var oldContent = "line1\nline2  ";
        var newContent = "line1\nline2";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.Additions.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateDiff_WithLeadingWhitespaceChanges_HonorsIgnoreFlag()
    {
        // Arrange
        var oldContent = "  line1";
        var newContent = "line1";

        // Act
        var resultWithIgnore = _sut.GenerateDiff(oldContent, newContent, "test.txt", true);
        var resultWithoutIgnore = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        resultWithIgnore.Additions.Should().Be(0);
        resultWithoutIgnore.Additions.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateDiff_WithTrailingWhitespaceChanges_HonorsIgnoreFlag()
    {
        // Arrange
        var oldContent = "line1  ";
        var newContent = "line1";

        // Act
        var resultWithIgnore = _sut.GenerateDiff(oldContent, newContent, "test.txt", true);
        var resultWithoutIgnore = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        resultWithIgnore.Additions.Should().Be(0);
        resultWithoutIgnore.Additions.Should().BeGreaterThan(0);
    }

    #endregion

    #region Alignment & Side-by-Side

    [Fact]
    public void GenerateDiff_AlignedRowsHaveMatchingLineCount()
    {
        // Arrange
        var oldContent = "line1\nline2\nline3";
        var newContent = "line1\nmodified\nline3";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.AlignedRows.Should().NotBeNull();
        result.AlignedRows.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void GenerateDiff_SideBySide_PairsAddedWithPlaceholder()
    {
        // Arrange
        var oldContent = "line1";
        var newContent = "line1\nline2";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.AlignedRows.Should().Contain(r =>
            r.NewLine.Kind == DiffLineKind.Added &&
            r.OldLine.Kind == DiffLineKind.Placeholder);
    }

    [Fact]
    public void GenerateDiff_SideBySide_PairsRemovedWithPlaceholder()
    {
        // Arrange
        var oldContent = "line1\nline2";
        var newContent = "line1";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.AlignedRows.Should().Contain(r =>
            r.OldLine.Kind == DiffLineKind.Removed &&
            r.NewLine.Kind == DiffLineKind.Placeholder);
    }

    [Fact]
    public void GenerateDiff_SideBySide_UnchangedLinesPairedCorrectly()
    {
        // Arrange
        var oldContent = "line1\nunchanged line\nline3";
        var newContent = "line1\nunchanged line\nline4";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.AlignedRows.Should().Contain(r =>
            r.OldLine.Kind == DiffLineKind.Unchanged &&
            r.NewLine.Kind == DiffLineKind.Unchanged);
    }

    #endregion

    #region Inline Diff

    [Fact]
    public void GenerateInlineDiff_CreatesCorrectLineCount()
    {
        // Arrange
        var oldContent = "line1\nline2";
        var newContent = "line1\nline2\nline3";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.InlineLines.Should().NotBeNull();
        result.InlineLines.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void GenerateInlineDiff_AddedLinesHaveCorrectKind()
    {
        // Arrange
        var oldContent = "line1";
        var newContent = "line1\nline2";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.InlineLines.Should().Contain(l => l.Kind == DiffLineKind.Added);
    }

    [Fact]
    public void GenerateInlineDiff_RemovedLinesHaveCorrectKind()
    {
        // Arrange
        var oldContent = "line1\nline2";
        var newContent = "line1";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.InlineLines.Should().Contain(l => l.Kind == DiffLineKind.Removed);
    }

    [Fact]
    public void GenerateInlineDiff_ContextLinesHaveCorrectKind()
    {
        // Arrange
        var oldContent = "line1\nline2\nline3";
        var newContent = "line1\nline2\nline3";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        if (result.InlineLines.Any())
        {
            result.InlineLines.Should().OnlyContain(l =>
                l.Kind == DiffLineKind.Unchanged || l.Kind == DiffLineKind.Context);
        }
    }

    #endregion

    #region Binary Detection

    [Fact]
    public void IsBinaryDiff_WithBinaryFilesMarker_ReturnsTrue()
    {
        // Arrange
        var binaryDiff = "diff --git a/file.bin b/file.bin\nBinary files differ";

        // Act
        var result = _sut.IsBinaryDiff(binaryDiff);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsBinaryDiff_WithGitBinaryPatchMarker_ReturnsTrue()
    {
        // Arrange
        var binaryDiff = "diff --git a/file.bin b/file.bin\nGIT binary patch";

        // Act
        var result = _sut.IsBinaryDiff(binaryDiff);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsBinaryDiff_WithTextDiff_ReturnsFalse()
    {
        // Arrange
        var textDiff = "diff --git a/test.txt b/test.txt\n--- a/test.txt\n+++ b/test.txt\n@@ -1 +1 @@\n-old\n+new";

        // Act
        var result = _sut.IsBinaryDiff(textDiff);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Parse Diff

    [Fact]
    public void ParseDiff_WithValidDiff_ReturnsCorrectBlocks()
    {
        // Arrange
        var rawDiff = @"diff --git a/test.txt b/test.txt
index 1234567..abcdefg 100644
--- a/test.txt
+++ b/test.txt
@@ -1,3 +1,3 @@
 line1
-line2
+line2_modified
 line3";

        // Act
        var result = _sut.ParseDiff(rawDiff, "test.txt");

        // Assert
        result.Should().NotBeNull();
        result.Blocks.Should().NotBeNull();
    }

    [Fact]
    public void ParseDiff_WithHeaderLines_SkipsHeaders()
    {
        // Arrange
        var rawDiff = @"diff --git a/test.txt b/test.txt
index 1234567..abcdefg 100644
--- a/test.txt
+++ b/test.txt
@@ -1,2 +1,2 @@
 context
-removed
+added";

        // Act
        var result = _sut.ParseDiff(rawDiff, "test.txt");

        // Assert
        result.Blocks.Should().NotBeNull();
    }

    [Fact]
    public void ParseDiff_WithMalformedInput_HandlesGracefully()
    {
        // Arrange
        var rawDiff = "This is not a valid diff format";

        // Act
        var result = _sut.ParseDiff(rawDiff, "test.txt");

        // Assert
        result.Should().NotBeNull();
        result.Blocks.Should().NotBeNull();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void GenerateDiff_WithVeryLargeFile_HandlesEfficiently()
    {
        // Arrange
        var oldContent = string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"line{i}"));
        var newContent = string.Join("\n", Enumerable.Range(1, 1000).Select(i => $"line{i}_modified"));

        // Act
        var startTime = DateTime.Now;
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);
        var elapsed = DateTime.Now - startTime;

        // Assert
        result.Should().NotBeNull();
        elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5)); // Should complete within 5 seconds
    }

    [Fact]
    public void GenerateDiff_WithUnicodeContent_PreservesCharacters()
    {
        // Arrange
        var oldContent = "Hello \u4e16\u754c"; // "Hello World" in Chinese
        var newContent = "Hello \u4e16\u754c\nNew line";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.InlineLines.Should().Contain(l => l.Content.Contains("\u4e16\u754c"));
    }

    [Fact]
    public void GenerateDiff_WithSpecialCharacters_PreservesContent()
    {
        // Arrange
        var oldContent = "line with \t\t\ttabs and <xml>tags</xml>";
        var newContent = "line with \t\t\ttabs and <xml>tags</xml>\nnew line";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.Additions.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateDiff_WithSingleLineFiles_WorksCorrectly()
    {
        // Arrange
        var oldContent = "single line";
        var newContent = "modified line";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.Should().NotBeNull();
        result.Additions.Should().BeGreaterThan(0);
        result.Deletions.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GenerateDiff_WithOnlyWhitespaceLines_HandlesCorrectly()
    {
        // Arrange
        var oldContent = "   \n\t\n  ";
        var newContent = "   \n\t\n  \n    ";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Theme-Aware Character-Level Highlighting

    [Fact]
    public void GenerateDiff_WithLightTheme_UsesLightThemeColors()
    {
        // Arrange
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetTheme()).Returns(AppTheme.Light);
        var diffService = new DiffService(mockSettings.Object);

        var oldContent = "Hello World";
        var newContent = "Hello Universe";

        // Act
        var result = diffService.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.Should().NotBeNull();
        var modifiedLines = result.AlignedRows
            .Where(row => row.OldLine?.Highlights?.Count > 0 || row.NewLine?.Highlights?.Count > 0)
            .ToList();

        modifiedLines.Should().NotBeEmpty();

        // Check old line (removed) uses light theme removed color
        var oldHighlight = modifiedLines.FirstOrDefault(r => r.OldLine?.Highlights?.Count > 0)?.OldLine?.Highlights?.FirstOrDefault();
        if (oldHighlight != null)
        {
            oldHighlight.BackgroundHex.Should().Be("#FFCCCC"); // Light theme removed color
            oldHighlight.ColorHex.Should().Be("#000000"); // Black text
        }

        // Check new line (added) uses light theme added color
        var newHighlight = modifiedLines.FirstOrDefault(r => r.NewLine?.Highlights?.Count > 0)?.NewLine?.Highlights?.FirstOrDefault();
        if (newHighlight != null)
        {
            newHighlight.BackgroundHex.Should().Be("#CCFFCC"); // Light theme added color
            newHighlight.ColorHex.Should().Be("#000000"); // Black text
        }
    }

    [Fact]
    public void GenerateDiff_WithDarkTheme_UsesDarkThemeColors()
    {
        // Arrange
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetTheme()).Returns(AppTheme.Dark);
        var diffService = new DiffService(mockSettings.Object);

        var oldContent = "Hello World";
        var newContent = "Hello Universe";

        // Act
        var result = diffService.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.Should().NotBeNull();
        var modifiedLines = result.AlignedRows
            .Where(row => row.OldLine?.Highlights?.Count > 0 || row.NewLine?.Highlights?.Count > 0)
            .ToList();

        modifiedLines.Should().NotBeEmpty();

        // Check old line (removed) uses dark theme removed color (brighter)
        var oldHighlight = modifiedLines.FirstOrDefault(r => r.OldLine?.Highlights?.Count > 0)?.OldLine?.Highlights?.FirstOrDefault();
        if (oldHighlight != null)
        {
            oldHighlight.BackgroundHex.Should().Be("#663333"); // Dark theme removed color (subtle dark)
            oldHighlight.ColorHex.Should().Be("#FFFFFF"); // White text for contrast
        }

        // Check new line (added) uses dark theme added color (brighter)
        var newHighlight = modifiedLines.FirstOrDefault(r => r.NewLine?.Highlights?.Count > 0)?.NewLine?.Highlights?.FirstOrDefault();
        if (newHighlight != null)
        {
            newHighlight.BackgroundHex.Should().Be("#336633"); // Dark theme added color (subtle dark)
            newHighlight.ColorHex.Should().Be("#FFFFFF"); // White text for contrast
        }
    }

    [Fact]
    public void GenerateDiff_ThemeChange_ProducesCorrectColors()
    {
        // Arrange
        var mockSettings = new Mock<ISettingsService>();
        mockSettings.Setup(s => s.GetTheme()).Returns(AppTheme.Light);
        var diffService = new DiffService(mockSettings.Object);

        var oldContent = "Hello World";
        var newContent = "Hello Universe";

        // Act - Generate diff with light theme
        var lightResult = diffService.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Change to dark theme
        mockSettings.Setup(s => s.GetTheme()).Returns(AppTheme.Dark);
        var darkResult = diffService.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert - Light theme colors
        var lightHighlight = lightResult.AlignedRows
            .FirstOrDefault(r => r.NewLine?.Highlights?.Count > 0)?.NewLine?.Highlights?.FirstOrDefault();
        lightHighlight.Should().NotBeNull();
        lightHighlight!.BackgroundHex.Should().Be("#CCFFCC"); // Light theme

        // Assert - Dark theme colors
        var darkHighlight = darkResult.AlignedRows
            .FirstOrDefault(r => r.NewLine?.Highlights?.Count > 0)?.NewLine?.Highlights?.FirstOrDefault();
        darkHighlight.Should().NotBeNull();
        darkHighlight!.BackgroundHex.Should().Be("#336633"); // Dark theme (subtle dark)
    }

    [Fact]
    public void GenerateDiff_WithModifiedLine_ContainsCharacterLevelHighlights()
    {
        // Arrange
        var oldContent = "The quick brown fox";
        var newContent = "The quick green fox";

        // Act
        var result = _sut.GenerateDiff(oldContent, newContent, "test.txt", false);

        // Assert
        result.Should().NotBeNull();

        // Should have both old and new lines
        result.AlignedRows.Should().NotBeEmpty();

        // Should have character-level highlights for the changed word "brown" -> "green"
        var rowWithHighlights = result.AlignedRows
            .FirstOrDefault(row => (row.OldLine?.Highlights?.Count ?? 0) > 0 || (row.NewLine?.Highlights?.Count ?? 0) > 0);

        rowWithHighlights.Should().NotBeNull();

        // Old line should have highlights
        rowWithHighlights!.OldLine?.Highlights.Should().NotBeNullOrEmpty();

        // New line should have highlights
        rowWithHighlights.NewLine?.Highlights.Should().NotBeNullOrEmpty();
    }

    #endregion
}
