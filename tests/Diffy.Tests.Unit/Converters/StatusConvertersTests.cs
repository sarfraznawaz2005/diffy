using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Diffy.App.Converters;
using Diffy.Core.Models;
using FluentAssertions;
using Xunit;

namespace Diffy.Tests.Unit.Converters;

/// <summary>
/// Unit tests for the StatusConverters class.
/// Tests value converters for UI binding.
/// </summary>
public class StatusConvertersTests
{
    #region StatusToColorConverter

    [Fact]
    public void Convert_Modified_ReturnsOrange()
    {
        // Arrange
        var converter = StatusToColorConverter.Instance;

        // Act
        var result = converter.Convert(FileStatusKind.Modified, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(Brushes.Orange);
    }

    [Fact]
    public void Convert_New_ReturnsGreen()
    {
        // Arrange
        var converter = StatusToColorConverter.Instance;

        // Act
        var result = converter.Convert(FileStatusKind.New, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(Brushes.Green);
    }

    [Fact]
    public void Convert_Deleted_ReturnsRed()
    {
        // Arrange
        var converter = StatusToColorConverter.Instance;

        // Act
        var result = converter.Convert(FileStatusKind.Deleted, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(Brushes.Red);
    }

    [Fact]
    public void Convert_Renamed_ReturnsBlue()
    {
        // Arrange
        var converter = StatusToColorConverter.Instance;

        // Act
        var result = converter.Convert(FileStatusKind.Renamed, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(Brushes.Blue);
    }

    [Fact]
    public void Convert_Unstaged_ReturnsYellow()
    {
        // Arrange
        var converter = StatusToColorConverter.Instance;

        // Act
        var result = converter.Convert(FileStatusKind.Unstaged, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(Brushes.Yellow);
    }

    [Fact]
    public void Convert_Unmerged_ReturnsPurple()
    {
        // Arrange
        var converter = StatusToColorConverter.Instance;

        // Act
        var result = converter.Convert(FileStatusKind.Unmerged, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(Brushes.Purple);
    }

    [Fact]
    public void Convert_Ignored_ReturnsGray()
    {
        // Arrange
        var converter = StatusToColorConverter.Instance;

        // Act
        var result = converter.Convert(FileStatusKind.Ignored, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(Brushes.Gray);
    }

    [Fact]
    public void Convert_Unknown_ReturnsGray()
    {
        // Arrange
        var converter = StatusToColorConverter.Instance;

        // Act
        var result = converter.Convert(FileStatusKind.Unknown, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(Brushes.Gray);
    }

    [Fact]
    public void Convert_Null_ReturnsGray()
    {
        // Arrange
        var converter = StatusToColorConverter.Instance;

        // Act
        var result = converter.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(Brushes.Gray);
    }

    #endregion

    #region StatusToLabelConverter

    [Fact]
    public void Convert_Modified_ReturnsM()
    {
        // Arrange
        var converter = StatusToLabelConverter.Instance;

        // Act
        var result = converter.Convert(FileStatusKind.Modified, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("M");
    }

    [Fact]
    public void Convert_New_ReturnsA()
    {
        // Arrange
        var converter = StatusToLabelConverter.Instance;

        // Act
        var result = converter.Convert(FileStatusKind.New, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("A");
    }

    [Fact]
    public void Convert_Deleted_ReturnsD()
    {
        // Arrange
        var converter = StatusToLabelConverter.Instance;

        // Act
        var result = converter.Convert(FileStatusKind.Deleted, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("D");
    }

    [Fact]
    public void Convert_Renamed_ReturnsR()
    {
        // Arrange
        var converter = StatusToLabelConverter.Instance;

        // Act
        var result = converter.Convert(FileStatusKind.Renamed, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("R");
    }

    [Fact]
    public void Convert_Unstaged_ReturnsQuestionMark()
    {
        // Arrange
        var converter = StatusToLabelConverter.Instance;

        // Act
        var result = converter.Convert(FileStatusKind.Unstaged, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("?");
    }

    [Fact]
    public void Convert_Unmerged_ReturnsU()
    {
        // Arrange
        var converter = StatusToLabelConverter.Instance;

        // Act
        var result = converter.Convert(FileStatusKind.Unmerged, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("U");
    }

    [Fact]
    public void Convert_Ignored_ReturnsI()
    {
        // Arrange
        var converter = StatusToLabelConverter.Instance;

        // Act
        var result = converter.Convert(FileStatusKind.Ignored, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("I");
    }

    [Fact]
    public void Convert_Null_ReturnsQuestionMark()
    {
        // Arrange
        var converter = StatusToLabelConverter.Instance;

        // Act
        var result = converter.Convert(null, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be("?");
    }

    #endregion

    #region DiffLineKindToColorConverter

    [Fact]
    public void Convert_Added_ReturnsDarkGreen()
    {
        // Arrange
        var converter = DiffLineKindToColorConverter.Instance;

        // Act
        var result = converter.Convert(DiffLineKind.Added, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().BeOfType<SolidColorBrush>();
        var brush = result as SolidColorBrush;
        brush!.Color.R.Should().Be(30);
        brush.Color.G.Should().Be(60);
        brush.Color.B.Should().Be(30);
    }

    [Fact]
    public void Convert_Removed_ReturnsDarkRed()
    {
        // Arrange
        var converter = DiffLineKindToColorConverter.Instance;

        // Act
        var result = converter.Convert(DiffLineKind.Removed, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().BeOfType<SolidColorBrush>();
        var brush = result as SolidColorBrush;
        brush!.Color.R.Should().Be(60);
        brush.Color.G.Should().Be(30);
        brush.Color.B.Should().Be(30);
    }

    [Fact]
    public void Convert_Other_ReturnsTransparent()
    {
        // Arrange
        var converter = DiffLineKindToColorConverter.Instance;

        // Act
        var result = converter.Convert(DiffLineKind.Unchanged, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(Brushes.Transparent);
    }

    #endregion

    #region DiffModeToBoolConverter

    [Fact]
    public void Convert_SideBySideWithSideBySideParameter_ReturnsTrue()
    {
        // Arrange
        var converter = DiffModeToBoolConverter.Instance;

        // Act
        var result = converter.Convert(DiffMode.SideBySide, typeof(bool), "SideBySide", CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void Convert_InlineWithSideBySideParameter_ReturnsFalse()
    {
        // Arrange
        var converter = DiffModeToBoolConverter.Instance;

        // Act
        var result = converter.Convert(DiffMode.Inline, typeof(bool), "SideBySide", CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void Convert_InvalidParameter_ReturnsFalse()
    {
        // Arrange
        var converter = DiffModeToBoolConverter.Instance;

        // Act
        var result = converter.Convert(DiffMode.SideBySide, typeof(bool), "Invalid", CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    #endregion

    #region LineNumberToOpacityConverter

    [Fact]
    public void Convert_MinusOne_ReturnsZero()
    {
        // Arrange
        var converter = LineNumberToOpacityConverter.Instance;

        // Act
        var result = converter.Convert(-1, typeof(double), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(0.0);
    }

    [Fact]
    public void Convert_ValidNumber_ReturnsOne()
    {
        // Arrange
        var converter = LineNumberToOpacityConverter.Instance;

        // Act
        var result = converter.Convert(42, typeof(double), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(1.0);
    }

    [Fact]
    public void Convert_Zero_ReturnsOne()
    {
        // Arrange
        var converter = LineNumberToOpacityConverter.Instance;

        // Act
        var result = converter.Convert(0, typeof(double), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(1.0);
    }

    #endregion

    #region ObjectToBoolConverter

    [Fact]
    public void Convert_NonNullValue_ReturnsTrue()
    {
        // Arrange
        var converter = ObjectToBoolConverter.Instance;

        // Act
        var result = converter.Convert("not null", typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void Convert_NullValue_ReturnsFalse()
    {
        // Arrange
        var converter = ObjectToBoolConverter.Instance;

        // Act
        var result = converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    #endregion

    #region NullToBoolConverter

    [Fact]
    public void NullToBoolConvert_NonNullValue_ReturnsFalse()
    {
        // Arrange
        var converter = NullToBoolConverter.Instance;

        // Act
        var result = converter.Convert("not null", typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void NullToBoolConvert_NullValue_ReturnsTrue()
    {
        // Arrange
        var converter = NullToBoolConverter.Instance;

        // Act
        var result = converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(true);
    }

    #endregion

    #region StringNotEmptyToBoolConverter

    [Fact]
    public void StringNotEmptyConvert_NonEmptyString_ReturnsTrue()
    {
        // Arrange
        var converter = StringNotEmptyToBoolConverter.Instance;

        // Act
        var result = converter.Convert("hello", typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void StringNotEmptyConvert_EmptyString_ReturnsFalse()
    {
        // Arrange
        var converter = StringNotEmptyToBoolConverter.Instance;

        // Act
        var result = converter.Convert("", typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void StringNotEmptyConvert_NullString_ReturnsFalse()
    {
        // Arrange
        var converter = StringNotEmptyToBoolConverter.Instance;

        // Act
        var result = converter.Convert(null, typeof(bool), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(false);
    }

    #endregion

    #region StringStatusToColorConverter

    [Theory]
    [InlineData("added", "green")]
    [InlineData("A", "green")]
    [InlineData("modified", "orange")]
    [InlineData("M", "orange")]
    [InlineData("deleted", "red")]
    [InlineData("D", "red")]
    [InlineData("renamed", "blue")]
    [InlineData("R", "blue")]
    [InlineData("unknown", "gray")]
    public void StringStatusToColorConvert_VariousStatuses_ReturnsCorrectColor(string status, string expectedColor)
    {
        // Arrange
        var converter = StringStatusToColorConverter.Instance;

        // Act
        var result = converter.Convert(status, typeof(IBrush), null, CultureInfo.InvariantCulture);

        // Assert
        switch (expectedColor.ToLower())
        {
            case "green":
                result.Should().Be(Brushes.Green);
                break;
            case "orange":
                result.Should().Be(Brushes.Orange);
                break;
            case "red":
                result.Should().Be(Brushes.Red);
                break;
            case "blue":
                result.Should().Be(Brushes.Blue);
                break;
            case "gray":
                result.Should().Be(Brushes.Gray);
                break;
        }
    }

    #endregion

    #region StringStatusToLabelConverter

    [Theory]
    [InlineData("added", "A")]
    [InlineData("A", "A")]
    [InlineData("modified", "M")]
    [InlineData("M", "M")]
    [InlineData("deleted", "D")]
    [InlineData("D", "D")]
    [InlineData("renamed", "R")]
    [InlineData("R", "R")]
    [InlineData("unknown", "?")]
    public void StringStatusToLabelConvert_VariousStatuses_ReturnsCorrectLabel(string status, string expectedLabel)
    {
        // Arrange
        var converter = StringStatusToLabelConverter.Instance;

        // Act
        var result = converter.Convert(status, typeof(string), null, CultureInfo.InvariantCulture);

        // Assert
        result.Should().Be(expectedLabel);
    }

    #endregion

    #region ConvertBack Methods

    [Fact]
    public void AllConverters_ConvertBack_ThrowsNotImplementedException()
    {
        // Arrange
        var converters = new IValueConverter[]
        {
            StatusToColorConverter.Instance,
            StatusToLabelConverter.Instance,
            DiffLineKindToColorConverter.Instance,
            DiffLineKindToStringConverter.Instance,
            ObjectToBoolConverter.Instance,
            DiffModeToBoolConverter.Instance,
            LineNumberToOpacityConverter.Instance,
            NullToBoolConverter.Instance,
            StringNotEmptyToBoolConverter.Instance,
            StringStatusToColorConverter.Instance,
            StringStatusToLabelConverter.Instance
        };

        // Act & Assert
        foreach (var converter in converters)
        {
            Action act = () => converter.ConvertBack(null, typeof(object), null, CultureInfo.InvariantCulture);
            act.Should().Throw<NotImplementedException>();
        }
    }

    #endregion
}
