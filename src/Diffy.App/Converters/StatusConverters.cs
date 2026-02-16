using System.Globalization;
using Avalonia.Controls.Documents;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Diffy.Core.Models;

namespace Diffy.App.Converters;


/// <summary>
/// Converts FileStatusKind to a color for display.
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    public static readonly StatusToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileStatusKind status)
        {
            return status switch
            {
                FileStatusKind.Modified => Brushes.Orange,
                FileStatusKind.New => Brushes.Green,
                FileStatusKind.Deleted => Brushes.Red,
                FileStatusKind.Renamed => Brushes.Blue,
                FileStatusKind.Unstaged => Brushes.Yellow,
                FileStatusKind.Unmerged => Brushes.Purple,
                _ => Brushes.Gray
            };
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts FileStatusKind to a short label.
/// </summary>
public class StatusToLabelConverter : IValueConverter
{
    public static readonly StatusToLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is FileStatusKind status)
        {
            return status switch
            {
                FileStatusKind.Modified => "M",
                FileStatusKind.New => "A",
                FileStatusKind.Deleted => "D",
                FileStatusKind.Renamed => "R",
                FileStatusKind.Unstaged => "?",
                FileStatusKind.Unmerged => "U",
                FileStatusKind.Ignored => "I",
                _ => "?"
            };
        }
        return "?";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts DiffLineKind to a background color.
/// </summary>
public class DiffLineKindToColorConverter : IValueConverter
{
    public static readonly DiffLineKindToColorConverter Instance = new();

    private static readonly ISolidColorBrush AddedBrush = new SolidColorBrush(Color.FromRgb(30, 60, 30));
    private static readonly ISolidColorBrush RemovedBrush = new SolidColorBrush(Color.FromRgb(60, 30, 30));

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DiffLineKind kind)
        {
            return kind switch
            {
                DiffLineKind.Added => AddedBrush,
                DiffLineKind.Removed => RemovedBrush,
                _ => Brushes.Transparent
            };
        }
        return Brushes.Transparent;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts DiffLineKind to its string representation (Added, Removed, etc) for Tag binding.
/// </summary>
public class DiffLineKindToStringConverter : IValueConverter
{
    public static readonly DiffLineKindToStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString();
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Returns true if value is not null.
/// </summary>
public class ObjectToBoolConverter : IValueConverter
{
    public static readonly ObjectToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Returns true if the DiffMode matches the parameter.
/// </summary>
public class DiffModeToBoolConverter : IValueConverter
{
    public static readonly DiffModeToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DiffMode mode && parameter is string targetModeStr && Enum.TryParse<DiffMode>(targetModeStr, out var targetMode))
        {
            return mode == targetMode;
        }
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Returns 0 opacity if line number is -1, else 1.
/// </summary>
public class LineNumberToOpacityConverter : IValueConverter
{
    public static readonly LineNumberToOpacityConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int num && num == -1)
            return 0.0;
        return 1.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Returns true if value is null.
/// </summary>
public class NullToBoolConverter : IValueConverter
{
    public static readonly NullToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Hides the Old line number in Inline view if it matches the New line number (redundant).
/// </summary>
public class InlineOldLineNumberVisibilityConverter : IMultiValueConverter
{
    public static readonly InlineOldLineNumberVisibilityConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && values[0] is int oldNum && values[1] is int newNum)
        {
            // If equal (and not -1 placeholder), hide the old number to avoid "1 1" redundancy
            if (oldNum == newNum && oldNum != -1)
                return false;

            // If old is -1 (inserted line), it should be hidden anyway
            if (oldNum == -1)
                return false;

            return true;
        }
        return true;
    }
}

/// <summary>
/// Selects the appropriate line number for a single-column inline view.
/// Prioritizes NewLineNumber, falls back to OldLineNumber.
/// </summary>
public class InlineLineNumberValueConverter : IMultiValueConverter
{
    public static readonly InlineLineNumberValueConverter Instance = new();

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count >= 2 && values[0] is int oldNum && values[1] is int newNum)
        {
            // If NewLine exists (Added or Unchanged/Modified), show it
            if (newNum != -1) return newNum.ToString();

            // If only OldLine exists (Deleted), show it
            if (oldNum != -1) return oldNum.ToString();

            return "";
        }
        return "";
    }
}


public class StringNotEmptyToBoolConverter : IValueConverter
{
    public static readonly StringNotEmptyToBoolConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return !string.IsNullOrEmpty(value as string);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ChangeType string to a color.
/// </summary>
public class StringStatusToColorConverter : IValueConverter
{
    public static readonly StringStatusToColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            if (status.Contains("Add", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "a", StringComparison.OrdinalIgnoreCase)) return Brushes.Green;
            if (status.Contains("Mod", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "m", StringComparison.OrdinalIgnoreCase)) return Brushes.Orange;
            if (status.Contains("Del", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "d", StringComparison.OrdinalIgnoreCase)) return Brushes.Red;
            if (status.Contains("Ren", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "r", StringComparison.OrdinalIgnoreCase)) return Brushes.Blue;
        }
        return Brushes.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ChangeType string to a short label (A, M, D, R).
/// </summary>
public class StringStatusToLabelConverter : IValueConverter
{
    public static readonly StringStatusToLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            if (status.Contains("Add", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "a", StringComparison.OrdinalIgnoreCase)) return "A";
            if (status.Contains("Mod", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "m", StringComparison.OrdinalIgnoreCase)) return "M";
            if (status.Contains("Del", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "d", StringComparison.OrdinalIgnoreCase)) return "D";
            if (status.Contains("Ren", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "r", StringComparison.OrdinalIgnoreCase)) return "R";
        }
        return "?";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
