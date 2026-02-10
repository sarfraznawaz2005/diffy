using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Diffy.App.Controls;
using Diffy.Core.Models;
using FluentAssertions;
using Xunit;

namespace Diffy.Tests.Unit.Controls;

/// <summary>
/// Unit tests for the DiffMinimapControl class.
/// Tests rendering, interaction, and property behavior.
/// </summary>
public class DiffMinimapControlTests
{
    #region Property Tests

    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Arrange & Act
        var control = new DiffMinimapControl();

        // Assert
        control.MinimapWidth.Should().Be(40.0);
        control.ClipToBounds.Should().BeTrue();
        control.AddedColor.Should().NotBeNull();
        control.RemovedColor.Should().NotBeNull();
        control.ViewportColor.Should().NotBeNull();
        control.MinimapBackground.Should().NotBeNull();
        control.BorderBrush.Should().NotBeNull();
    }

    [Fact]
    public void MinimapWidth_CanBeSet()
    {
        // Arrange
        var control = new DiffMinimapControl();

        // Act
        control.MinimapWidth = 100.0;

        // Assert
        control.MinimapWidth.Should().Be(100.0);
    }

    [Fact]
    public void DiffLines_CanBeSet()
    {
        // Arrange
        var control = new DiffMinimapControl();
        var lines = new List<DiffLine>
        {
            new DiffLine { Kind = DiffLineKind.Added, Content = "added line", NewLineNumber = 1 },
            new DiffLine { Kind = DiffLineKind.Removed, Content = "removed line", OldLineNumber = 1 }
        };

        // Act
        control.DiffLines = lines;

        // Assert
        control.DiffLines.Should().BeEquivalentTo(lines);
    }

    [Fact]
    public void Colors_CanBeCustomized()
    {
        // Arrange
        var control = new DiffMinimapControl();
        var customGreen = new SolidColorBrush(Colors.Green);
        var customRed = new SolidColorBrush(Colors.Red);

        // Act
        control.AddedColor = customGreen;
        control.RemovedColor = customRed;

        // Assert
        control.AddedColor.Should().Be(customGreen);
        control.RemovedColor.Should().Be(customRed);
    }

    #endregion

    #region Diff Line Processing Tests

    [Fact]
    public void DiffLines_WithNullValue_DoesNotThrow()
    {
        // Arrange
        var control = new DiffMinimapControl();

        // Act
        Action act = () => control.DiffLines = null;

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void DiffLines_WithEmptyList_DoesNotThrow()
    {
        // Arrange
        var control = new DiffMinimapControl();

        // Act
        Action act = () => control.DiffLines = new List<DiffLine>();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void DiffLines_WithMixedKinds_AcceptsAllTypes()
    {
        // Arrange
        var control = new DiffMinimapControl();
        var lines = new List<DiffLine>
        {
            new DiffLine { Kind = DiffLineKind.Added, Content = "added", NewLineNumber = 1 },
            new DiffLine { Kind = DiffLineKind.Removed, Content = "removed", OldLineNumber = 1 },
            new DiffLine { Kind = DiffLineKind.Unchanged, Content = "unchanged", OldLineNumber = 2, NewLineNumber = 2 },
            new DiffLine { Kind = DiffLineKind.Context, Content = "context", OldLineNumber = 3, NewLineNumber = 3 },
            new DiffLine { Kind = DiffLineKind.Header, Content = "header" }
        };

        // Act
        control.DiffLines = lines;

        // Assert
        control.DiffLines.Should().HaveCount(5);
        control.DiffLines.Should().Contain(l => l.Kind == DiffLineKind.Added);
        control.DiffLines.Should().Contain(l => l.Kind == DiffLineKind.Removed);
        control.DiffLines.Should().Contain(l => l.Kind == DiffLineKind.Unchanged);
    }

    [Fact]
    public void DiffLines_WithLargeCount_HandlesEfficiently()
    {
        // Arrange
        var control = new DiffMinimapControl();
        var lines = Enumerable.Range(1, 10000)
            .Select(i => new DiffLine
            {
                Kind = i % 2 == 0 ? DiffLineKind.Added : DiffLineKind.Removed,
                Content = $"line {i}",
                NewLineNumber = i
            })
            .ToList();

        // Act
        var startTime = DateTime.Now;
        control.DiffLines = lines;
        var elapsed = DateTime.Now - startTime;

        // Assert
        control.DiffLines.Should().HaveCount(10000);
        elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(100));
    }

    #endregion

    #region Color Validation Tests

    [Fact]
    public void DefaultColors_HaveCorrectOpacity()
    {
        // Arrange
        var control = new DiffMinimapControl();

        // Act
        var addedBrush = control.AddedColor as SolidColorBrush;
        var removedBrush = control.RemovedColor as SolidColorBrush;

        // Assert
        addedBrush.Should().NotBeNull();
        removedBrush.Should().NotBeNull();
        addedBrush!.Color.A.Should().Be(200); // Opacity for better visibility
        removedBrush!.Color.A.Should().Be(200);
    }

    [Fact]
    public void ViewportColor_HasCorrectOpacity()
    {
        // Arrange
        var control = new DiffMinimapControl();

        // Act
        var viewportBrush = control.ViewportColor as SolidColorBrush;

        // Assert
        viewportBrush.Should().NotBeNull();
        viewportBrush!.Color.A.Should().Be(120); // Darker for visibility
    }

    [Fact]
    public void BorderBrush_HasCorrectOpacity()
    {
        // Arrange
        var control = new DiffMinimapControl();

        // Act
        var borderBrush = control.BorderBrush as SolidColorBrush;

        // Assert
        borderBrush.Should().NotBeNull();
        borderBrush!.Color.A.Should().Be(80); // Semi-transparent
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void DiffLines_WithOnlyAdditions_StoresCorrectly()
    {
        // Arrange
        var control = new DiffMinimapControl();
        var lines = new List<DiffLine>
        {
            new DiffLine { Kind = DiffLineKind.Added, Content = "line1", NewLineNumber = 1 },
            new DiffLine { Kind = DiffLineKind.Added, Content = "line2", NewLineNumber = 2 },
            new DiffLine { Kind = DiffLineKind.Added, Content = "line3", NewLineNumber = 3 }
        };

        // Act
        control.DiffLines = lines;

        // Assert
        control.DiffLines.Should().OnlyContain(l => l.Kind == DiffLineKind.Added);
        control.DiffLines.Should().HaveCount(3);
    }

    [Fact]
    public void DiffLines_WithOnlyDeletions_StoresCorrectly()
    {
        // Arrange
        var control = new DiffMinimapControl();
        var lines = new List<DiffLine>
        {
            new DiffLine { Kind = DiffLineKind.Removed, Content = "line1", OldLineNumber = 1 },
            new DiffLine { Kind = DiffLineKind.Removed, Content = "line2", OldLineNumber = 2 }
        };

        // Act
        control.DiffLines = lines;

        // Assert
        control.DiffLines.Should().OnlyContain(l => l.Kind == DiffLineKind.Removed);
        control.DiffLines.Should().HaveCount(2);
    }

    [Fact]
    public void DiffLines_WithMixedChanges_CountsCorrectly()
    {
        // Arrange
        var control = new DiffMinimapControl();
        var lines = new List<DiffLine>
        {
            new DiffLine { Kind = DiffLineKind.Unchanged, Content = "unchanged1" },
            new DiffLine { Kind = DiffLineKind.Added, Content = "added1", NewLineNumber = 2 },
            new DiffLine { Kind = DiffLineKind.Removed, Content = "removed1", OldLineNumber = 2 },
            new DiffLine { Kind = DiffLineKind.Unchanged, Content = "unchanged2" },
            new DiffLine { Kind = DiffLineKind.Added, Content = "added2", NewLineNumber = 4 }
        };

        // Act
        control.DiffLines = lines;

        // Assert
        control.DiffLines.Should().HaveCount(5);
        control.DiffLines.Count(l => l.Kind == DiffLineKind.Added).Should().Be(2);
        control.DiffLines.Count(l => l.Kind == DiffLineKind.Removed).Should().Be(1);
        control.DiffLines.Count(l => l.Kind == DiffLineKind.Unchanged).Should().Be(2);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void MinimapWidth_WithZeroValue_AcceptsValue()
    {
        // Arrange
        var control = new DiffMinimapControl();

        // Act
        control.MinimapWidth = 0;

        // Assert
        control.MinimapWidth.Should().Be(0);
    }

    [Fact]
    public void MinimapWidth_WithNegativeValue_AcceptsValue()
    {
        // Arrange
        var control = new DiffMinimapControl();

        // Act
        control.MinimapWidth = -10;

        // Assert
        control.MinimapWidth.Should().Be(-10);
    }

    [Fact]
    public void MinimapWidth_WithVeryLargeValue_AcceptsValue()
    {
        // Arrange
        var control = new DiffMinimapControl();

        // Act
        control.MinimapWidth = 10000;

        // Assert
        control.MinimapWidth.Should().Be(10000);
    }

    [Fact]
    public void DiffLines_UpdatedMultipleTimes_RetainsLatestValue()
    {
        // Arrange
        var control = new DiffMinimapControl();
        var firstSet = new List<DiffLine>
        {
            new DiffLine { Kind = DiffLineKind.Added, Content = "first" }
        };
        var secondSet = new List<DiffLine>
        {
            new DiffLine { Kind = DiffLineKind.Removed, Content = "second" }
        };

        // Act
        control.DiffLines = firstSet;
        control.DiffLines = secondSet;

        // Assert
        control.DiffLines.Should().BeEquivalentTo(secondSet);
        control.DiffLines.Should().HaveCount(1);
        control.DiffLines.First().Kind.Should().Be(DiffLineKind.Removed);
    }

    #endregion

    #region Realistic Scenarios

    [Fact]
    public void RealWorldScenario_SmallFileDiff_HandlesCorrectly()
    {
        // Arrange
        var control = new DiffMinimapControl { MinimapWidth = 40 };
        var lines = new List<DiffLine>
        {
            new DiffLine { Kind = DiffLineKind.Unchanged, Content = "import React from 'react';", OldLineNumber = 1, NewLineNumber = 1 },
            new DiffLine { Kind = DiffLineKind.Unchanged, Content = "", OldLineNumber = 2, NewLineNumber = 2 },
            new DiffLine { Kind = DiffLineKind.Removed, Content = "const App = () => {", OldLineNumber = 3 },
            new DiffLine { Kind = DiffLineKind.Added, Content = "export const App = () => {", NewLineNumber = 3 },
            new DiffLine { Kind = DiffLineKind.Unchanged, Content = "  return <div>Hello</div>;", OldLineNumber = 4, NewLineNumber = 4 },
            new DiffLine { Kind = DiffLineKind.Unchanged, Content = "};", OldLineNumber = 5, NewLineNumber = 5 }
        };

        // Act
        control.DiffLines = lines;

        // Assert
        control.DiffLines.Should().HaveCount(6);
        control.DiffLines.Count(l => l.Kind == DiffLineKind.Added).Should().Be(1);
        control.DiffLines.Count(l => l.Kind == DiffLineKind.Removed).Should().Be(1);
    }

    [Fact]
    public void RealWorldScenario_LargeFileDiff_HandlesEfficiently()
    {
        // Arrange
        var control = new DiffMinimapControl { MinimapWidth = 40 };
        var lines = new List<DiffLine>();

        // Simulate a large file with scattered changes
        for (int i = 1; i <= 1000; i++)
        {
            if (i % 10 == 0)
            {
                lines.Add(new DiffLine { Kind = DiffLineKind.Added, Content = $"new line {i}", NewLineNumber = i });
            }
            else if (i % 15 == 0)
            {
                lines.Add(new DiffLine { Kind = DiffLineKind.Removed, Content = $"old line {i}", OldLineNumber = i });
            }
            else
            {
                lines.Add(new DiffLine { Kind = DiffLineKind.Unchanged, Content = $"line {i}", OldLineNumber = i, NewLineNumber = i });
            }
        }

        // Act
        var startTime = DateTime.Now;
        control.DiffLines = lines;
        var elapsed = DateTime.Now - startTime;

        // Assert
        control.DiffLines.Should().HaveCount(1000);
        elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(50));
    }

    #endregion
}
